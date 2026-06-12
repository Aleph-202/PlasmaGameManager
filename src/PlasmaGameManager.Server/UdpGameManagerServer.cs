using System.Net;
using System.Net.Sockets;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.Server;

public sealed class UdpGameManagerServer
{
    private readonly IGameManagerProfile _profile;
    private readonly IGameManagerEventSink? _eventSink;
    private readonly SourceBackendProxy _sourceProxy;
    private readonly Ps3NativeSourceResponder _nativeSourceResponder = new();
    private readonly PlasmaPacketClassifier _classifier = new();
    private readonly GameManagerCommandDecoder _commandDecoder = new();
    private readonly GameManagerSession _game;
    private readonly SemaphoreSlim _packetGate = new(1, 1);
    private readonly string? _sourceLaunchProfilePath;
    private readonly string? _sourceLaunchProfileGlob;
    private string? _sourceLaunchProfileObservedPath;
    private DateTime _sourceLaunchProfileWriteTimeUtc;
    private long _sourceLaunchProfileLength = -1;
    private string? _deferredSourceLaunchProfilePath;
    private DateTime _deferredSourceLaunchProfileWriteTimeUtc;
    private long _deferredSourceLaunchProfileLength = -1;
    private readonly TimeSpan _generatedSourcePlayerInactivityTimeout;

    public UdpGameManagerServer(
        IGameManagerProfile profile,
        IGameManagerEventSink? eventSink = null,
        SourceBackendOptions? sourceBackend = null,
        GameManagerSessionOptions? sessionOptions = null,
        string? sourceLaunchProfilePath = null,
        string? sourceLaunchProfileGlob = null,
        TimeSpan? generatedSourcePlayerInactivityTimeout = null)
    {
        _profile = profile;
        _eventSink = eventSink;
        _game = new GameManagerSession(sessionOptions ?? GameManagerSessionOptions.Default);
        Control = new GameManagerControlService(_game);
        _sourceProxy = new SourceBackendProxy(sourceBackend ?? SourceBackendOptions.Disabled, RecordSourceProxyError);
        _sourceLaunchProfilePath = string.IsNullOrWhiteSpace(sourceLaunchProfilePath) ? null : sourceLaunchProfilePath;
        _sourceLaunchProfileGlob = string.IsNullOrWhiteSpace(sourceLaunchProfileGlob) ? null : sourceLaunchProfileGlob;
        _generatedSourcePlayerInactivityTimeout = generatedSourcePlayerInactivityTimeout ?? TimeSpan.FromSeconds(30);
    }

    public GameManagerControlService Control { get; }

    public async Task RunAsync(IPAddress address, int port, CancellationToken ct)
    {
        await RunAsync(address, new[] { port }, ct);
    }

    public async Task RunAsync(IPAddress address, IReadOnlyCollection<int> ports, CancellationToken ct)
    {
        if (ports.Count == 0)
        {
            throw new ArgumentException("At least one UDP port must be supplied.", nameof(ports));
        }

        var sockets = ports
            .Distinct()
            .OrderBy(static p => p)
            .Select(port => new UdpClient(new IPEndPoint(address, port)))
            .ToArray();

        try
        {
            var tasks = sockets.Select(socket => RunSocketAsync(socket, address, ((IPEndPoint)socket.Client.LocalEndPoint!).Port, ct)).ToArray();
            await Task.WhenAll(tasks);
        }
        finally
        {
            foreach (var socket in sockets)
            {
                socket.Dispose();
            }

            await _sourceProxy.DisposeAsync();
        }
    }

    private async Task RunSocketAsync(UdpClient socket, IPAddress address, int port, CancellationToken ct)
    {
        Console.WriteLine($"PlasmaGameManager listening on {address}:{port} profile={_profile.Name}");
        RecordServerLifecycleEvent(
            "server-listening",
            $"{address}:{port}",
            $"PlasmaGameManager UDP listener ready on {address}:{port} profile={_profile.Name} game={_game.GameId} map={_game.MapName} advertised={_game.AdvertisedHost}:{_game.AdvertisedPort}");

        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await socket.ReceiveAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await _packetGate.WaitAsync(ct);
            try
            {
                await HandleReceivedAsync(socket, received, ct);
            }
            finally
            {
                _packetGate.Release();
            }
        }
    }

    private async Task HandleReceivedAsync(UdpClient socket, UdpReceiveResult received, CancellationToken ct)
    {
        ReloadSourceLaunchProfileIfChanged();
        var endpoint = received.RemoteEndPoint.ToString();
        var player = _game.GetOrAddPlayer(endpoint);
        player.LastSeen = DateTimeOffset.UtcNow;
        var packet = _classifier.Decode(received.Buffer, enableNativeBinary: true);
        var command = _commandDecoder.Decode(packet);
        var stateBefore = player.State;
        Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} <= {endpoint} {packet.Explanation} hex={packet.HexPrefix()} ascii=\"{packet.AsciiPreview(64)}\"");

        if (stateBefore == PlayerJoinState.SourceHandoff)
        {
            await HandleSourceTrafficAsync(socket, received, endpoint, player, packet, command, ct);
            return;
        }

        var responses = _profile.Handle(_game, player, packet);
        RecordPacketEvent("receive", endpoint, packet, command, stateBefore, player.State);
        if (stateBefore != PlayerJoinState.SourceHandoff && player.State == PlayerJoinState.SourceHandoff)
        {
            RecordPacketEvent("source-handoff", endpoint, packet, command, stateBefore, player.State);
            Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} ** {endpoint} SOURCE_HANDOFF profile={_profile.Name} via={packet.Kind}");
        }

        if (player.State == PlayerJoinState.SourceHandoff && packet.Kind == PlasmaCommandKind.SourceProbe)
        {
            await HandleSourceTrafficAsync(socket, received, endpoint, player, packet, command, ct);
            return;
        }

        if (stateBefore != PlayerJoinState.SourceHandoff
            && player.State == PlayerJoinState.SourceHandoff
            && packet.Kind is PlasmaCommandKind.Unknown or PlasmaCommandKind.TextCommand)
        {
            await HandleSourceTrafficAsync(socket, received, endpoint, player, packet, command, ct);
            return;
        }

        foreach (var response in responses)
        {
            await socket.SendAsync(response.Payload, response.Payload.Length, received.RemoteEndPoint);
            Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} => {endpoint} {response.Kind} {response.Explanation} len={response.Payload.Length}");
            var responsePacket = _classifier.Decode(response.Payload, enableNativeBinary: true);
            var responseCommand = _commandDecoder.Decode(responsePacket);
            RecordPacketEvent("send", endpoint, responsePacket, responseCommand, player.State, player.State, response.Explanation);
        }
    }

    private void ReloadSourceLaunchProfileIfChanged()
    {
        var selectedPath = ResolveSourceLaunchProfilePath();
        if (selectedPath is null)
        {
            return;
        }

        FileInfo info;
        try
        {
            info = new FileInfo(selectedPath);
        }
        catch (Exception ex)
        {
            RecordSourceLaunchProfileError($"could not stat TF2 Source launch profile {selectedPath}: {ex.Message}");
            return;
        }

        if (string.Equals(selectedPath, _sourceLaunchProfileObservedPath, StringComparison.Ordinal)
            && info.LastWriteTimeUtc == _sourceLaunchProfileWriteTimeUtc
            && info.Length == _sourceLaunchProfileLength)
        {
            return;
        }

        if (IsDynamicSourceLaunchProfilePath(selectedPath) && _game.ActiveSourcePlayerCount > 0)
        {
            var expired = _game.ExpireInactiveGeneratedSourcePlayers(DateTimeOffset.UtcNow, _generatedSourcePlayerInactivityTimeout);
            if (expired > 0)
            {
                RecordSourceLaunchProfileEvent(
                    "source-player-expired",
                    $"expired {expired} inactive generated Source player(s) after {_generatedSourcePlayerInactivityTimeout.TotalSeconds:0.###}s before applying dynamic TF2 Source launch profile {selectedPath}",
                    selectedPath);
            }
        }

        if (IsDynamicSourceLaunchProfilePath(selectedPath) && _game.ActiveSourcePlayerCount > 0)
        {
            RecordDeferredSourceLaunchProfile(selectedPath, info);
            return;
        }

        try
        {
            var profile = Tf2SourceLaunchProfile.LoadFromJsonFile(selectedPath);
            _game.ApplySourceLaunchProfile(profile);
            _sourceLaunchProfileObservedPath = selectedPath;
            _sourceLaunchProfileWriteTimeUtc = info.LastWriteTimeUtc;
            _sourceLaunchProfileLength = info.Length;
            _deferredSourceLaunchProfilePath = null;
            _deferredSourceLaunchProfileWriteTimeUtc = default;
            _deferredSourceLaunchProfileLength = -1;
            var explanation = $"applied TF2 Source launch profile {selectedPath}: map={_game.MapName} name=\"{_game.Name}\" maxPlayers={_game.MaxPlayers} gid={_game.GameId}";
            Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} ** {explanation}");
            RecordSourceLaunchProfileEvent("source-launch-profile-applied", explanation, selectedPath);
        }
        catch (Exception ex)
        {
            RecordSourceLaunchProfileError($"could not load TF2 Source launch profile {selectedPath}: {ex.Message}");
        }
    }

    private void RecordDeferredSourceLaunchProfile(string selectedPath, FileInfo info)
    {
        if (string.Equals(selectedPath, _deferredSourceLaunchProfilePath, StringComparison.Ordinal)
            && info.LastWriteTimeUtc == _deferredSourceLaunchProfileWriteTimeUtc
            && info.Length == _deferredSourceLaunchProfileLength)
        {
            return;
        }

        _deferredSourceLaunchProfilePath = selectedPath;
        _deferredSourceLaunchProfileWriteTimeUtc = info.LastWriteTimeUtc;
        _deferredSourceLaunchProfileLength = info.Length;
        var explanation = $"deferred TF2 Source launch profile {selectedPath}: {_game.ActiveSourcePlayerCount} active generated Source player(s) are already in object-state/world setup";
        Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} ** {explanation}");
        RecordSourceLaunchProfileEvent("source-launch-profile-deferred", explanation, selectedPath);
    }

    private string? ResolveSourceLaunchProfilePath()
    {
        var candidates = EnumerateSourceLaunchProfileCandidates()
            .Select(static path => new FileInfo(path))
            .Where(static info => info.Exists)
            .OrderByDescending(static info => info.LastWriteTimeUtc)
            .ThenByDescending(static info => info.Length)
            .Select(static info => info.FullName)
            .ToArray();

        return candidates.Length == 0 ? null : candidates[0];
    }

    private IEnumerable<string> EnumerateSourceLaunchProfileCandidates()
    {
        if (_sourceLaunchProfilePath is not null)
        {
            yield return _sourceLaunchProfilePath;
        }

        if (_sourceLaunchProfileGlob is null)
        {
            yield break;
        }

        var pattern = NormalizeSourceLaunchProfileGlob(_sourceLaunchProfileGlob);
        var directory = Path.GetDirectoryName(pattern);
        var filePattern = Path.GetFileName(pattern);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        if (string.IsNullOrWhiteSpace(filePattern))
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, filePattern).ToArray()
                : Array.Empty<string>();
        }
        catch (Exception ex)
        {
            RecordSourceLaunchProfileError($"could not enumerate TF2 Source launch profile pattern {_sourceLaunchProfileGlob}: {ex.Message}");
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }
    }

    private static string NormalizeSourceLaunchProfileGlob(string pattern)
    {
        if (pattern.Contains("*", StringComparison.Ordinal) || pattern.Contains("?", StringComparison.Ordinal))
        {
            return pattern;
        }

        var normalized = pattern;
        var start = normalized.IndexOf("{", StringComparison.Ordinal);
        while (start >= 0)
        {
            var end = normalized.IndexOf("}", start + 1, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            normalized = string.Concat(normalized.AsSpan(0, start), "*", normalized.AsSpan(end + 1));
            start = normalized.IndexOf("{", start + 1, StringComparison.Ordinal);
        }

        return normalized;
    }

    private bool IsDynamicSourceLaunchProfilePath(string selectedPath)
    {
        if (_sourceLaunchProfileGlob is null)
        {
            return false;
        }

        if (_sourceLaunchProfilePath is null)
        {
            return true;
        }

        var staticPath = Path.GetFullPath(_sourceLaunchProfilePath);
        return !string.Equals(Path.GetFullPath(selectedPath), staticPath, StringComparison.Ordinal);
    }

    private void RecordSourceLaunchProfileError(string explanation)
    {
        Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} !! {explanation}");
        RecordSourceLaunchProfileEvent("source-launch-profile-error", explanation);
    }

    private void RecordSourceLaunchProfileEvent(string eventName, string explanation, string? endpointOverride = null)
    {
        _eventSink?.Record(new GameManagerServerEvent(
            DateTimeOffset.UtcNow,
            _profile.Name,
            eventName,
            endpointOverride ?? _sourceLaunchProfileObservedPath ?? _sourceLaunchProfilePath ?? _sourceLaunchProfileGlob ?? "local",
            "SourceLaunchProfile",
            "SessionConfig",
            "SessionConfig",
            0,
            explanation,
            ""));
    }

    private void RecordServerLifecycleEvent(string eventName, string endpoint, string explanation)
    {
        _eventSink?.Record(new GameManagerServerEvent(
            DateTimeOffset.UtcNow,
            _profile.Name,
            eventName,
            endpoint,
            "Lifecycle",
            "SessionConfig",
            "SessionConfig",
            0,
            explanation,
            ""));
    }

    private async Task HandleSourceTrafficAsync(
        UdpClient socket,
        UdpReceiveResult received,
        string endpoint,
        PlayerSession player,
        PlasmaPacket packet,
        GameManagerCommand command,
        CancellationToken ct)
    {
        _game.EnsureNativeSourceLifecycle(player);
        var clientSourceObservation = player.SourceGameplay.Observe(Ps3SourceGameplayDirection.ClientToServer, packet.Payload);
        RecordPacketEvent("source-traffic", endpoint, packet, command, player.State, player.State, sourceObservation: clientSourceObservation, nativeSourcePlayer: player);
        Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} ~~ {endpoint} SOURCE_TRAFFIC {packet.Explanation} len={packet.Payload.Length}");
        if ((!_sourceProxy.IsEnabled || _sourceProxy.Protocol == SourceBackendProtocol.Ps3NativeGenerated)
            && SourceQueryResponseBuilder.TryBuildInfoResponse(_game, packet.Payload, out var queryResponse))
        {
            await socket.SendAsync(queryResponse.Payload, queryResponse.Payload.Length, received.RemoteEndPoint);
            RecordRawEvent(
                "source-send",
                endpoint,
                queryResponse.Kind.ToString(),
                player.State,
                player.State,
                queryResponse.Payload.Length,
                queryResponse.Explanation,
                Convert.ToHexString(queryResponse.Payload.AsSpan(0, Math.Min(8, queryResponse.Payload.Length))).ToLowerInvariant(),
                queryResponse.Payload);
            Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} ~~> {endpoint} {queryResponse.Explanation} len={queryResponse.Payload.Length}");
            return;
        }

        if (_sourceProxy.Protocol == SourceBackendProtocol.Ps3NativeGenerated)
        {
            var generatedResponses = _nativeSourceResponder.BuildResponses(_game, player, packet.Payload);
            if (generatedResponses.Count == 0)
            {
                if (IsExpectedNativeSourceWait(player, packet.Payload.Length))
                {
                    return;
                }

                RecordPacketEvent(
                    "source-generated-drop",
                    endpoint,
                    packet,
                    command,
                    player.State,
                    player.State,
                    "PS3-native generated Source responder did not recognize a response-worthy transport packet.",
                    clientSourceObservation,
                    nativeSourcePlayer: player);
                return;
            }

            foreach (var generated in generatedResponses)
            {
                var outboundPayload = generated.Payload;
                var generatedObservation = player.SourceGameplay.Observe(Ps3SourceGameplayDirection.ServerToClient, outboundPayload);
                await socket.SendAsync(outboundPayload, outboundPayload.Length, received.RemoteEndPoint);
                RecordRawEvent(
                    "source-generated-send",
                    endpoint,
                    "GeneratedPs3SourceDatagram",
                    player.State,
                    player.State,
                    outboundPayload.Length,
                    generated.Explanation,
                    Convert.ToHexString(outboundPayload.AsSpan(0, Math.Min(8, outboundPayload.Length))).ToLowerInvariant(),
                    outboundPayload,
                    generatedObservation,
                    nativeSourcePlayer: player);
                Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} ~~> {endpoint} generated PS3 Source datagram len={outboundPayload.Length}: {generated.Explanation}");
            }

            return;
        }

        if (_sourceProxy.IsEnabled)
        {
            var forwardResult = await _sourceProxy.ForwardAsync(
                endpoint,
                received.RemoteEndPoint,
                packet.Payload,
                async (datagram, callbackCt) =>
                {
                    var backendDecision = SourceBackendPayloadAdapter.PrepareBackendToClient(_sourceProxy.Protocol, datagram.Payload);
                    var observedPayload = backendDecision.ShouldForward ? backendDecision.Payload : datagram.Payload;
                    var backendObservation = player.SourceGameplay.Observe(Ps3SourceGameplayDirection.ServerToClient, observedPayload.Span);
                    if (!backendDecision.ShouldForward)
                    {
                        RecordRawEvent(
                            "source-proxy-backend-drop",
                            datagram.ClientEndpoint,
                            "SourceBackendDatagram",
                            player.State,
                            player.State,
                            datagram.Payload.Length,
                            $"dropped Source backend datagram from {_sourceProxy.BackendEndpoint}: {backendDecision.Explanation}",
                            Convert.ToHexString(datagram.Payload.AsSpan(0, Math.Min(8, datagram.Payload.Length))).ToLowerInvariant(),
                            datagram.Payload,
                            backendObservation);
                        Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} ~~x {datagram.ClientEndpoint} dropped Source backend datagram len={datagram.Payload.Length}: {backendDecision.Explanation}");
                        return;
                    }

                    var outboundPayload = backendDecision.Payload.ToArray();
                    await socket.SendAsync(outboundPayload, outboundPayload.Length, datagram.ClientRemoteEndpoint);
                    RecordRawEvent(
                        "source-proxy-send",
                        datagram.ClientEndpoint,
                        "SourceBackendDatagram",
                        player.State,
                        player.State,
                        outboundPayload.Length,
                        $"proxied Source backend datagram from {_sourceProxy.BackendEndpoint}: {backendDecision.Explanation}",
                        Convert.ToHexString(outboundPayload.AsSpan(0, Math.Min(8, outboundPayload.Length))).ToLowerInvariant(),
                        outboundPayload,
                        backendObservation);
                    Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} ~~> {datagram.ClientEndpoint} proxied Source backend datagram len={outboundPayload.Length}");
                },
                ct);
            if (forwardResult.Forwarded)
            {
                RecordPacketEvent(
                    "source-proxy-forward",
                    endpoint,
                    packet,
                    command,
                    player.State,
                    player.State,
                    $"proxied client datagram from PS3-facing GameManager flow to Source backend {_sourceProxy.BackendEndpoint} via {_sourceProxy.ProtocolName}: {forwardResult.Explanation}",
                    clientSourceObservation);
                return;
            }

            if (forwardResult.Dropped)
            {
                RecordPacketEvent(
                    "source-proxy-drop",
                    endpoint,
                    packet,
                    command,
                    player.State,
                    player.State,
                    $"did not forward client datagram to Source backend {_sourceProxy.BackendEndpoint} via {_sourceProxy.ProtocolName}: {forwardResult.Explanation}",
                    clientSourceObservation);
                return;
            }
        }

        if (!SourceQueryResponseBuilder.TryBuildInfoResponse(_game, packet.Payload, out var response))
        {
            return;
        }

        await socket.SendAsync(response.Payload, response.Payload.Length, received.RemoteEndPoint);
        RecordRawEvent("source-send", endpoint, response.Kind.ToString(), player.State, player.State, response.Payload.Length, response.Explanation, Convert.ToHexString(response.Payload.AsSpan(0, Math.Min(8, response.Payload.Length))).ToLowerInvariant(), response.Payload);
        Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} ~~> {endpoint} {response.Explanation} len={response.Payload.Length}");
    }

    private static bool IsExpectedNativeSourceWait(PlayerSession player, int payloadLength)
    {
        var state = player.NativeSourceResponder;
        return (state.PendingPostRosterFrozenStateUpload && payloadLength >= 200)
            || (state.QuickMatchTerminalPromptStage == 2
            && !state.SentQuickMatchTerminalMapLoad
            && payloadLength != 56);
    }

    private void RecordPacketEvent(
        string eventName,
        string endpoint,
        PlasmaPacket packet,
        GameManagerCommand command,
        PlayerJoinState stateBefore,
        PlayerJoinState stateAfter,
        string? explanationOverride = null,
        Ps3SourceGameplayObservation? sourceObservation = null,
        PlayerSession? nativeSourcePlayer = null)
    {
        var sourceTransport = BuildSourceEventData(eventName, packet.Payload, sourceObservation);
        _eventSink?.Record(new GameManagerServerEvent(
            DateTimeOffset.UtcNow,
            _profile.Name,
            eventName,
            endpoint,
            packet.Kind.ToString(),
            stateBefore.ToString(),
            stateAfter.ToString(),
            packet.Payload.Length,
            explanationOverride ?? packet.Explanation,
            packet.HexPrefix(),
            command.Name,
            command.TransactionId,
            command.LocalId,
            command.GameId,
            command.PlayerId,
            command.Fields,
            SourceSequence: sourceTransport.Sequence,
            SourceBodyLength: sourceTransport.BodyLength,
            SourceSequenceDelta: sourceTransport.SequenceDelta,
            SourcePacketShape: sourceTransport.Shape,
            SourceNativeFrameKind: sourceTransport.NativeFrameKind,
            SourceFitsInlineQueue: sourceTransport.FitsInlineQueue,
            SourceFitsNativeQueue: sourceTransport.FitsNativeQueue,
            SourceFragmentHeaderHex: sourceTransport.FragmentHeaderHex,
            SourcePayloadSemanticKind: sourceTransport.PayloadSemanticKind,
            SourcePayloadSemanticRole: sourceTransport.PayloadSemanticRole,
            SourceCompactControlFamily: sourceTransport.CompactControlFamily,
            SourceCompactControlPrefixLength: sourceTransport.CompactControlPrefixLength,
            SourceCompactControlPrefixHex: sourceTransport.CompactControlPrefixHex,
            SourceMarkerlessPayloadFamily: sourceTransport.MarkerlessPayloadFamily,
            SourceMarkerlessPayloadPrefixHex: sourceTransport.MarkerlessPayloadPrefixHex,
            SourceMarkerlessPayloadSuffixHex: sourceTransport.MarkerlessPayloadSuffixHex,
            SourceMarkerlessPayloadEntropy: sourceTransport.MarkerlessPayloadEntropy,
            SourceMarkerlessPayloadPrintableRatio: sourceTransport.MarkerlessPayloadPrintableRatio,
            SourceEmbeddedMarkers: sourceTransport.EmbeddedMarkers,
            SourceEmbeddedRecordRoles: sourceTransport.EmbeddedRecordRoles,
            SourceEmbeddedObjectIds: sourceTransport.EmbeddedObjectIds,
            SourceEmbeddedClassIds: sourceTransport.EmbeddedClassIds,
            SourceEmbeddedObjectLinks: sourceTransport.EmbeddedObjectLinks,
            SourceEmbeddedDisplayNames: sourceTransport.EmbeddedDisplayNames,
            SourceEmbeddedRecordSummaries: sourceTransport.EmbeddedRecordSummaries,
            SourceEmbeddedObjectIdOrder: sourceTransport.EmbeddedObjectIdOrder,
            SourceEmbeddedClassIdOrder: sourceTransport.EmbeddedClassIdOrder,
            SourceEmbeddedObjectLinkOrder: sourceTransport.EmbeddedObjectLinkOrder,
            SourceEmbeddedDisplayNameOrder: sourceTransport.EmbeddedDisplayNameOrder,
            SourceEmbeddedPrefixLength: sourceTransport.EmbeddedPrefixLength,
            SourceEmbeddedPrefixHex: sourceTransport.EmbeddedPrefixHex,
            SourceEmbeddedPrefixFamily: sourceTransport.EmbeddedPrefixFamily,
            SourceEmbeddedPrefixEntropy: sourceTransport.EmbeddedPrefixEntropy,
            SourceEmbeddedPrefixPrintableRatio: sourceTransport.EmbeddedPrefixPrintableRatio,
            SourceEmbeddedPrefixMeaning: sourceTransport.EmbeddedPrefixMeaning,
            SourceDirectionPacketCount: sourceTransport.DirectionPacketCount,
            SourceSequenceDecrease: sourceTransport.SequenceDecrease,
            SourceBodySignature: sourceTransport.BodySignature,
            SourceClientPayloadRole: sourceTransport.ClientPayloadRole,
            SourceClientBodyPrefixHex: sourceTransport.ClientBodyPrefixHex,
            SourceClientFirstUInt16BigEndian: sourceTransport.ClientFirstUInt16BigEndian,
            SourceClientFirstUInt16LittleEndian: sourceTransport.ClientFirstUInt16LittleEndian,
            SourceClientLastUInt16BigEndian: sourceTransport.ClientLastUInt16BigEndian,
            SourceClientReliableAssociationMessageType: sourceTransport.ClientReliableAssociationMessageType,
            SourceClientReliableAssociationNativeToken: sourceTransport.ClientReliableAssociationNativeToken,
            SourceClientReliableAssociationTokenBigEndian: sourceTransport.ClientReliableAssociationTokenBigEndian,
            SourceClientReliableAssociationTokenLittleEndian: sourceTransport.ClientReliableAssociationTokenLittleEndian,
            SourceClientAttachedFrameKind: sourceTransport.ClientAttachedFrameKind,
            SourceClientAttachedFrameDeclaredLength: sourceTransport.ClientAttachedFrameDeclaredLength,
            SourceClientAttachedFrameNativeToken: sourceTransport.ClientAttachedFrameNativeToken,
            SourceClientAttachedFrameTokenBigEndian: sourceTransport.ClientAttachedFrameTokenBigEndian,
            SourceClientAttachedFrameTokenLittleEndian: sourceTransport.ClientAttachedFrameTokenLittleEndian,
            SourceClientBitSidecarOffset: sourceTransport.ClientBitSidecarOffset,
            SourceClientBitSidecarBitCount: sourceTransport.ClientBitSidecarBitCount,
            SourceClientBitSidecarPayloadLength: sourceTransport.ClientBitSidecarPayloadLength,
            SourceClientPayloadObjectFrameKind: sourceTransport.ClientPayloadObjectFrameKind,
            SourceClientPayloadObjectHeaderValue: sourceTransport.ClientPayloadObjectHeaderValue,
            SourceClientPayloadObjectSignedHeaderValue: sourceTransport.ClientPayloadObjectSignedHeaderValue,
            SourceClientPayloadObjectInnerPayloadOffset: sourceTransport.ClientPayloadObjectInnerPayloadOffset,
            SourceClientPayloadObjectInnerPayloadLength: sourceTransport.ClientPayloadObjectInnerPayloadLength,
            SourceClientPayloadObjectBitreaderFieldOffset: sourceTransport.ClientPayloadObjectBitreaderFieldOffset,
            SourceClientPayloadObjectAssociatedToken: sourceTransport.ClientPayloadObjectAssociatedToken,
            SourceClientPayloadObjectFragmentIndex: sourceTransport.ClientPayloadObjectFragmentIndex,
            SourceClientPayloadObjectFragmentTotalCount: sourceTransport.ClientPayloadObjectFragmentTotalCount,
            SourceClientPayloadObjectFragmentPacketCounter: sourceTransport.ClientPayloadObjectFragmentPacketCounter,
            SourceClientPayloadObjectFragmentWrappedOrCompressed: sourceTransport.ClientPayloadObjectFragmentWrappedOrCompressed,
            SourceClientDecodedNetMessageType: sourceTransport.ClientDecodedNetMessageType,
            SourceClientDecodedNetMessageName: sourceTransport.ClientDecodedNetMessageName,
            SourceClientDecodedNetMessagePayloadKind: sourceTransport.ClientDecodedNetMessagePayloadKind,
            SourceClientDecodedNetMessagePayloadOffset: sourceTransport.ClientDecodedNetMessagePayloadOffset,
            SourceClientDecodedNetMessagePayloadLength: sourceTransport.ClientDecodedNetMessagePayloadLength,
            SourceClientDecodedNetMessagePayloadBitCount: sourceTransport.ClientDecodedNetMessagePayloadBitCount,
            SourceClientCommandDecoded: sourceTransport.ClientCommandDecoded,
            SourceClientCommandForwardMove: sourceTransport.ClientCommandForwardMove,
            SourceClientCommandSideMove: sourceTransport.ClientCommandSideMove,
            SourceClientCommandUpMove: sourceTransport.ClientCommandUpMove,
            SourceClientCommandYawDelta: sourceTransport.ClientCommandYawDelta,
            SourceClientCommandPitchDelta: sourceTransport.ClientCommandPitchDelta,
            SourceClientCommandButtons: sourceTransport.ClientCommandButtons,
            SourceClientCommandWeaponSlotHint: sourceTransport.ClientCommandWeaponSlotHint,
            SourceClientCommandTeamHint: sourceTransport.ClientCommandTeamHint,
            SourceClientCommandClassHint: sourceTransport.ClientCommandClassHint,
            NativeSourceClientPacketCount: nativeSourcePlayer?.NativeSourceResponder.ClientPacketCount,
            NativeSourceServerPacketCount: nativeSourcePlayer?.NativeSourceResponder.ServerPacketCount,
            NativeSourceSentInitialSetup: nativeSourcePlayer?.NativeSourceResponder.SentInitialSetup,
            NativeSourceInitialSetupVariant: nativeSourcePlayer?.NativeSourceResponder.InitialSetupVariant,
            NativeSourceSentServerInfo: nativeSourcePlayer?.NativeSourceResponder.SentServerInfo,
            NativeSourceSentObjectState: nativeSourcePlayer?.NativeSourceResponder.SentObjectState,
            NativeSourceRootObjectId: nativeSourcePlayer?.SourceState.RootObjectId,
            NativeSourceLocalPlayerObjectId: nativeSourcePlayer?.SourceState.LocalPlayerObjectId,
            NativeSourceObjectStateIntroBatchIndex: nativeSourcePlayer?.NativeSourceResponder.ObjectStateIntroBatchIndex,
            NativeSourceLoadingStateLinkHeartbeatIndex: nativeSourcePlayer?.NativeSourceResponder.LoadingStateLinkHeartbeatIndex,
            NativeSourceSentLoadingStateLinkBurst: nativeSourcePlayer?.NativeSourceResponder.SentLoadingStateLinkBurst,
            NativeSourceLoadingStateLinkBurstClientPacketCount: nativeSourcePlayer?.NativeSourceResponder.LoadingStateLinkBurstClientPacketCount,
            NativeSourceSentLoadingPostBurstContinuation: nativeSourcePlayer?.NativeSourceResponder.SentLoadingPostBurstContinuation,
            NativeSourceLoadingContinuationStage: nativeSourcePlayer?.NativeSourceResponder.LoadingContinuationStage,
            NativeSourcePostRosterContinuationStage: nativeSourcePlayer?.NativeSourceResponder.PostRosterContinuationStage,
            NativeSourceSuppressLoadingContinuationResponses: nativeSourcePlayer?.NativeSourceResponder.SuppressLoadingContinuationResponses,
            NativeSourceSentLoadingMotdEvent: nativeSourcePlayer?.NativeSourceResponder.SentLoadingMotdEvent,
            NativeSourceSentRosterDescriptorState: nativeSourcePlayer?.NativeSourceResponder.SentRosterDescriptorState,
            NativeSourceSteadyStateLinkHeartbeatIndex: nativeSourcePlayer?.NativeSourceResponder.SteadyStateLinkHeartbeatIndex,
            NativeSourceLastCommandSnapshotClientPacketCount: nativeSourcePlayer?.NativeSourceResponder.LastCommandSnapshotClientPacketCount));
    }

    private void RecordRawEvent(
        string eventName,
        string endpoint,
        string kind,
        PlayerJoinState stateBefore,
        PlayerJoinState stateAfter,
        int payloadLength,
        string explanation,
        string hexPrefix,
        ReadOnlyMemory<byte> payload = default,
        Ps3SourceGameplayObservation? sourceObservation = null,
        PlayerSession? nativeSourcePlayer = null)
    {
        var sourceTransport = BuildSourceEventData(eventName, payload, sourceObservation);
        _eventSink?.Record(new GameManagerServerEvent(
            DateTimeOffset.UtcNow,
            _profile.Name,
            eventName,
            endpoint,
            kind,
            stateBefore.ToString(),
            stateAfter.ToString(),
            payloadLength,
            explanation,
            hexPrefix,
            SourceSequence: sourceTransport.Sequence,
            SourceBodyLength: sourceTransport.BodyLength,
            SourceSequenceDelta: sourceTransport.SequenceDelta,
            SourcePacketShape: sourceTransport.Shape,
            SourceNativeFrameKind: sourceTransport.NativeFrameKind,
            SourceFitsInlineQueue: sourceTransport.FitsInlineQueue,
            SourceFitsNativeQueue: sourceTransport.FitsNativeQueue,
            SourceFragmentHeaderHex: sourceTransport.FragmentHeaderHex,
            SourcePayloadSemanticKind: sourceTransport.PayloadSemanticKind,
            SourcePayloadSemanticRole: sourceTransport.PayloadSemanticRole,
            SourceCompactControlFamily: sourceTransport.CompactControlFamily,
            SourceCompactControlPrefixLength: sourceTransport.CompactControlPrefixLength,
            SourceCompactControlPrefixHex: sourceTransport.CompactControlPrefixHex,
            SourceMarkerlessPayloadFamily: sourceTransport.MarkerlessPayloadFamily,
            SourceMarkerlessPayloadPrefixHex: sourceTransport.MarkerlessPayloadPrefixHex,
            SourceMarkerlessPayloadSuffixHex: sourceTransport.MarkerlessPayloadSuffixHex,
            SourceMarkerlessPayloadEntropy: sourceTransport.MarkerlessPayloadEntropy,
            SourceMarkerlessPayloadPrintableRatio: sourceTransport.MarkerlessPayloadPrintableRatio,
            SourceEmbeddedMarkers: sourceTransport.EmbeddedMarkers,
            SourceEmbeddedRecordRoles: sourceTransport.EmbeddedRecordRoles,
            SourceEmbeddedObjectIds: sourceTransport.EmbeddedObjectIds,
            SourceEmbeddedClassIds: sourceTransport.EmbeddedClassIds,
            SourceEmbeddedObjectLinks: sourceTransport.EmbeddedObjectLinks,
            SourceEmbeddedDisplayNames: sourceTransport.EmbeddedDisplayNames,
            SourceEmbeddedRecordSummaries: sourceTransport.EmbeddedRecordSummaries,
            SourceEmbeddedObjectIdOrder: sourceTransport.EmbeddedObjectIdOrder,
            SourceEmbeddedClassIdOrder: sourceTransport.EmbeddedClassIdOrder,
            SourceEmbeddedObjectLinkOrder: sourceTransport.EmbeddedObjectLinkOrder,
            SourceEmbeddedDisplayNameOrder: sourceTransport.EmbeddedDisplayNameOrder,
            SourceEmbeddedPrefixLength: sourceTransport.EmbeddedPrefixLength,
            SourceEmbeddedPrefixHex: sourceTransport.EmbeddedPrefixHex,
            SourceEmbeddedPrefixFamily: sourceTransport.EmbeddedPrefixFamily,
            SourceEmbeddedPrefixEntropy: sourceTransport.EmbeddedPrefixEntropy,
            SourceEmbeddedPrefixPrintableRatio: sourceTransport.EmbeddedPrefixPrintableRatio,
            SourceEmbeddedPrefixMeaning: sourceTransport.EmbeddedPrefixMeaning,
            SourceDirectionPacketCount: sourceTransport.DirectionPacketCount,
            SourceSequenceDecrease: sourceTransport.SequenceDecrease,
            SourceBodySignature: sourceTransport.BodySignature,
            SourceClientPayloadRole: sourceTransport.ClientPayloadRole,
            SourceClientBodyPrefixHex: sourceTransport.ClientBodyPrefixHex,
            SourceClientFirstUInt16BigEndian: sourceTransport.ClientFirstUInt16BigEndian,
            SourceClientFirstUInt16LittleEndian: sourceTransport.ClientFirstUInt16LittleEndian,
            SourceClientLastUInt16BigEndian: sourceTransport.ClientLastUInt16BigEndian,
            SourceClientReliableAssociationMessageType: sourceTransport.ClientReliableAssociationMessageType,
            SourceClientReliableAssociationNativeToken: sourceTransport.ClientReliableAssociationNativeToken,
            SourceClientReliableAssociationTokenBigEndian: sourceTransport.ClientReliableAssociationTokenBigEndian,
            SourceClientReliableAssociationTokenLittleEndian: sourceTransport.ClientReliableAssociationTokenLittleEndian,
            SourceClientAttachedFrameKind: sourceTransport.ClientAttachedFrameKind,
            SourceClientAttachedFrameDeclaredLength: sourceTransport.ClientAttachedFrameDeclaredLength,
            SourceClientAttachedFrameNativeToken: sourceTransport.ClientAttachedFrameNativeToken,
            SourceClientAttachedFrameTokenBigEndian: sourceTransport.ClientAttachedFrameTokenBigEndian,
            SourceClientAttachedFrameTokenLittleEndian: sourceTransport.ClientAttachedFrameTokenLittleEndian,
            SourceClientBitSidecarOffset: sourceTransport.ClientBitSidecarOffset,
            SourceClientBitSidecarBitCount: sourceTransport.ClientBitSidecarBitCount,
            SourceClientBitSidecarPayloadLength: sourceTransport.ClientBitSidecarPayloadLength,
            SourceClientPayloadObjectFrameKind: sourceTransport.ClientPayloadObjectFrameKind,
            SourceClientPayloadObjectHeaderValue: sourceTransport.ClientPayloadObjectHeaderValue,
            SourceClientPayloadObjectSignedHeaderValue: sourceTransport.ClientPayloadObjectSignedHeaderValue,
            SourceClientPayloadObjectInnerPayloadOffset: sourceTransport.ClientPayloadObjectInnerPayloadOffset,
            SourceClientPayloadObjectInnerPayloadLength: sourceTransport.ClientPayloadObjectInnerPayloadLength,
            SourceClientPayloadObjectBitreaderFieldOffset: sourceTransport.ClientPayloadObjectBitreaderFieldOffset,
            SourceClientPayloadObjectAssociatedToken: sourceTransport.ClientPayloadObjectAssociatedToken,
            SourceClientPayloadObjectFragmentIndex: sourceTransport.ClientPayloadObjectFragmentIndex,
            SourceClientPayloadObjectFragmentTotalCount: sourceTransport.ClientPayloadObjectFragmentTotalCount,
            SourceClientPayloadObjectFragmentPacketCounter: sourceTransport.ClientPayloadObjectFragmentPacketCounter,
            SourceClientPayloadObjectFragmentWrappedOrCompressed: sourceTransport.ClientPayloadObjectFragmentWrappedOrCompressed,
            SourceClientDecodedNetMessageType: sourceTransport.ClientDecodedNetMessageType,
            SourceClientDecodedNetMessageName: sourceTransport.ClientDecodedNetMessageName,
            SourceClientDecodedNetMessagePayloadKind: sourceTransport.ClientDecodedNetMessagePayloadKind,
            SourceClientDecodedNetMessagePayloadOffset: sourceTransport.ClientDecodedNetMessagePayloadOffset,
            SourceClientDecodedNetMessagePayloadLength: sourceTransport.ClientDecodedNetMessagePayloadLength,
            SourceClientDecodedNetMessagePayloadBitCount: sourceTransport.ClientDecodedNetMessagePayloadBitCount,
            SourceClientCommandDecoded: sourceTransport.ClientCommandDecoded,
            SourceClientCommandForwardMove: sourceTransport.ClientCommandForwardMove,
            SourceClientCommandSideMove: sourceTransport.ClientCommandSideMove,
            SourceClientCommandUpMove: sourceTransport.ClientCommandUpMove,
            SourceClientCommandYawDelta: sourceTransport.ClientCommandYawDelta,
            SourceClientCommandPitchDelta: sourceTransport.ClientCommandPitchDelta,
            SourceClientCommandButtons: sourceTransport.ClientCommandButtons,
            SourceClientCommandWeaponSlotHint: sourceTransport.ClientCommandWeaponSlotHint,
            SourceClientCommandTeamHint: sourceTransport.ClientCommandTeamHint,
            SourceClientCommandClassHint: sourceTransport.ClientCommandClassHint,
            NativeSourceClientPacketCount: nativeSourcePlayer?.NativeSourceResponder.ClientPacketCount,
            NativeSourceServerPacketCount: nativeSourcePlayer?.NativeSourceResponder.ServerPacketCount,
            NativeSourceSentInitialSetup: nativeSourcePlayer?.NativeSourceResponder.SentInitialSetup,
            NativeSourceInitialSetupVariant: nativeSourcePlayer?.NativeSourceResponder.InitialSetupVariant,
            NativeSourceSentServerInfo: nativeSourcePlayer?.NativeSourceResponder.SentServerInfo,
            NativeSourceSentObjectState: nativeSourcePlayer?.NativeSourceResponder.SentObjectState,
            NativeSourceRootObjectId: nativeSourcePlayer?.SourceState.RootObjectId,
            NativeSourceLocalPlayerObjectId: nativeSourcePlayer?.SourceState.LocalPlayerObjectId,
            NativeSourceObjectStateIntroBatchIndex: nativeSourcePlayer?.NativeSourceResponder.ObjectStateIntroBatchIndex,
            NativeSourceLoadingStateLinkHeartbeatIndex: nativeSourcePlayer?.NativeSourceResponder.LoadingStateLinkHeartbeatIndex,
            NativeSourceSentLoadingStateLinkBurst: nativeSourcePlayer?.NativeSourceResponder.SentLoadingStateLinkBurst,
            NativeSourceLoadingStateLinkBurstClientPacketCount: nativeSourcePlayer?.NativeSourceResponder.LoadingStateLinkBurstClientPacketCount,
            NativeSourceSentLoadingPostBurstContinuation: nativeSourcePlayer?.NativeSourceResponder.SentLoadingPostBurstContinuation,
            NativeSourceLoadingContinuationStage: nativeSourcePlayer?.NativeSourceResponder.LoadingContinuationStage,
            NativeSourcePostRosterContinuationStage: nativeSourcePlayer?.NativeSourceResponder.PostRosterContinuationStage,
            NativeSourceSuppressLoadingContinuationResponses: nativeSourcePlayer?.NativeSourceResponder.SuppressLoadingContinuationResponses,
            NativeSourceSentLoadingMotdEvent: nativeSourcePlayer?.NativeSourceResponder.SentLoadingMotdEvent,
            NativeSourceSentRosterDescriptorState: nativeSourcePlayer?.NativeSourceResponder.SentRosterDescriptorState,
            NativeSourceSteadyStateLinkHeartbeatIndex: nativeSourcePlayer?.NativeSourceResponder.SteadyStateLinkHeartbeatIndex,
            NativeSourceLastCommandSnapshotClientPacketCount: nativeSourcePlayer?.NativeSourceResponder.LastCommandSnapshotClientPacketCount));
    }

    private static SourceEventData BuildSourceEventData(
        string eventName,
        ReadOnlyMemory<byte> payload,
        Ps3SourceGameplayObservation? observation)
    {
        if (observation is not null)
        {
            Ps3SourceNativeFrameInfo? nativeFrame = null;
            Ps3SourcePayloadSemanticInfo? payloadSemantic = null;
            SourceEmbeddedObjectEventData embeddedObjectData = SourceEmbeddedObjectEventData.Empty;
            SourceCompactControlEventData compactControlData = SourceCompactControlEventData.Empty;
            SourceMarkerlessPayloadEventData markerlessPayloadData = SourceMarkerlessPayloadEventData.Empty;
            Ps3SourceClientPayloadInfo? clientPayload = null;
            Ps3SourceClientCommandIntent? clientCommand = null;
            if (Ps3SourceTransportPacket.TryDecode(payload.Span, out var packet))
            {
                nativeFrame = packet.ClassifyNativeFrame();
                payloadSemantic = observation.Direction == Ps3SourceGameplayDirection.ClientToServer
                    && observation.DirectionPacketCount == 1
                    ? Ps3SourcePayloadSemantics.AnalyzeInitialClientHandoffProbe(packet.Body)
                    : Ps3SourcePayloadSemantics.Analyze(packet.Body);
                embeddedObjectData = ExtractEmbeddedObjectEventData(
                    packet.Body,
                    observation.Direction.ToString());
                compactControlData = ExtractCompactControlEventData(packet.Body);
                if (observation.Direction == Ps3SourceGameplayDirection.ClientToServer)
                {
                    clientPayload = Ps3SourceClientPayloadClassifier.Classify(
                        packet,
                        observation.DirectionPacketCount,
                        observation.SequenceDeltaFromPreviousSameDirection);
                    if (Ps3SourceClientCommandIntent.TryDecode(clientPayload, packet.Body, out var command))
                    {
                        clientCommand = command;
                    }
                }

                markerlessPayloadData = ExtractMarkerlessPayloadEventData(
                    observation.Direction,
                    observation.DirectionPacketCount,
                    observation.Shape,
                    nativeFrame,
                    payloadSemantic,
                    embeddedObjectData,
                    compactControlData,
                    clientPayload,
                    packet.Body);
            }

            return new SourceEventData(
                observation.Sequence,
                observation.BodyLength == 0 ? null : observation.BodyLength,
                observation.SequenceDeltaFromPreviousSameDirection,
                observation.Shape.ToString(),
                clientPayload?.NativeFrameKind.ToString() ?? observation.NativeFrameKind.ToString(),
                nativeFrame?.FitsInlineQueue,
                nativeFrame?.FitsNativeQueue,
                nativeFrame?.FragmentHeaderHex is { Length: > 0 } ? nativeFrame.FragmentHeaderHex : null,
                payloadSemantic?.Kind.ToString(),
                payloadSemantic?.Role.ToString(),
                compactControlData.Family,
                compactControlData.PrefixLength,
                compactControlData.PrefixHex,
                markerlessPayloadData.Family,
                markerlessPayloadData.PrefixHex,
                markerlessPayloadData.SuffixHex,
                markerlessPayloadData.Entropy,
                markerlessPayloadData.PrintableRatio,
                payloadSemantic?.EmbeddedMarkers.Select(static marker => marker.Marker).Distinct(StringComparer.Ordinal).ToArray(),
                embeddedObjectData.RecordRoles,
                embeddedObjectData.ObjectIds,
                embeddedObjectData.ClassIds,
                embeddedObjectData.ObjectLinks,
                embeddedObjectData.DisplayNames,
                embeddedObjectData.RecordSummaries,
                embeddedObjectData.ObjectIdOrder,
                embeddedObjectData.ClassIdOrder,
                embeddedObjectData.ObjectLinkOrder,
                embeddedObjectData.DisplayNameOrder,
                embeddedObjectData.PrefixLength,
                embeddedObjectData.PrefixHex,
                embeddedObjectData.PrefixFamily,
                embeddedObjectData.PrefixEntropy,
                embeddedObjectData.PrefixPrintableRatio,
                embeddedObjectData.PrefixMeaning,
                observation.DirectionPacketCount,
                observation.SequenceDecrease,
                SourceBodySignature(eventName, payload),
                clientPayload?.Role.ToString(),
                clientPayload?.BodyPrefixHex,
                clientPayload?.FirstUInt16BigEndian,
                clientPayload?.FirstUInt16LittleEndian,
                clientPayload?.LastUInt16BigEndian,
                clientPayload?.ReliableAssociationMessageType,
                clientPayload?.ReliableAssociationNativeToken,
                clientPayload?.ReliableAssociationTokenBigEndian,
                clientPayload?.ReliableAssociationTokenLittleEndian,
                clientPayload?.AttachedFrameKind,
                clientPayload?.AttachedFrameDeclaredLength,
                clientPayload?.AttachedFrameNativeToken,
                clientPayload?.AttachedFrameTokenBigEndian,
                clientPayload?.AttachedFrameTokenLittleEndian,
                clientPayload?.BitSidecarOffset,
                clientPayload?.BitSidecarBitCount,
                clientPayload?.BitSidecarPayloadLength,
                clientPayload?.DecodedNetMessageType,
                clientPayload?.DecodedNetMessageName,
                clientPayload?.DecodedNetMessagePayloadKind,
                clientPayload?.DecodedNetMessagePayloadOffset,
                clientPayload?.DecodedNetMessagePayloadLength,
                clientPayload?.DecodedNetMessagePayloadBitCount,
                clientCommand is not null,
                clientCommand?.ForwardMove,
                clientCommand?.SideMove,
                clientCommand?.UpMove,
                clientCommand?.YawDelta,
                clientCommand?.PitchDelta,
                clientCommand?.Buttons,
                clientCommand?.WeaponSlotHint,
                clientCommand?.TeamHint,
                clientCommand?.ClassHint,
                ClientPayloadObjectFrameKind: clientPayload?.PayloadObjectFrameKind,
                ClientPayloadObjectHeaderValue: clientPayload?.PayloadObjectHeaderValue,
                ClientPayloadObjectSignedHeaderValue: clientPayload?.PayloadObjectSignedHeaderValue,
                ClientPayloadObjectInnerPayloadOffset: clientPayload?.PayloadObjectInnerPayloadOffset,
                ClientPayloadObjectInnerPayloadLength: clientPayload?.PayloadObjectInnerPayloadLength,
                ClientPayloadObjectBitreaderFieldOffset: clientPayload?.PayloadObjectBitreaderFieldOffset,
                ClientPayloadObjectAssociatedToken: clientPayload?.PayloadObjectAssociatedToken,
                ClientPayloadObjectFragmentIndex: clientPayload?.PayloadObjectFragmentIndex,
                ClientPayloadObjectFragmentTotalCount: clientPayload?.PayloadObjectFragmentTotalCount,
                ClientPayloadObjectFragmentPacketCounter: clientPayload?.PayloadObjectFragmentPacketCounter,
                ClientPayloadObjectFragmentWrappedOrCompressed: clientPayload?.PayloadObjectFragmentWrappedOrCompressed);
        }

        var fallback = DecodeSourceTransport(eventName, payload);
        var fallbackCommand = DecodeSourceCommand(eventName, payload);
        return new SourceEventData(
            fallback.Sequence,
            fallback.BodyLength,
            null,
            null,
            fallback.NativeFrameKind,
            fallback.FitsInlineQueue,
            fallback.FitsNativeQueue,
            fallback.FragmentHeaderHex,
            fallback.PayloadSemanticKind,
            fallback.PayloadSemanticRole,
            fallback.CompactControlFamily,
            fallback.CompactControlPrefixLength,
            fallback.CompactControlPrefixHex,
            fallback.MarkerlessPayloadFamily,
            fallback.MarkerlessPayloadPrefixHex,
            fallback.MarkerlessPayloadSuffixHex,
            fallback.MarkerlessPayloadEntropy,
            fallback.MarkerlessPayloadPrintableRatio,
            fallback.EmbeddedMarkers,
            fallback.EmbeddedRecordRoles,
            fallback.EmbeddedObjectIds,
            fallback.EmbeddedClassIds,
            fallback.EmbeddedObjectLinks,
            fallback.EmbeddedDisplayNames,
            fallback.EmbeddedRecordSummaries,
            fallback.EmbeddedObjectIdOrder,
            fallback.EmbeddedClassIdOrder,
            fallback.EmbeddedObjectLinkOrder,
            fallback.EmbeddedDisplayNameOrder,
            fallback.EmbeddedPrefixLength,
            fallback.EmbeddedPrefixHex,
            fallback.EmbeddedPrefixFamily,
            fallback.EmbeddedPrefixEntropy,
            fallback.EmbeddedPrefixPrintableRatio,
            fallback.EmbeddedPrefixMeaning,
            null,
            null,
            SourceBodySignature(eventName, payload),
            fallback.ClientPayloadRole,
            fallback.ClientBodyPrefixHex,
            fallback.ClientFirstUInt16BigEndian,
            fallback.ClientFirstUInt16LittleEndian,
            fallback.ClientLastUInt16BigEndian,
            fallback.ClientReliableAssociationMessageType,
            fallback.ClientReliableAssociationNativeToken,
            fallback.ClientReliableAssociationTokenBigEndian,
            fallback.ClientReliableAssociationTokenLittleEndian,
            fallback.ClientAttachedFrameKind,
            fallback.ClientAttachedFrameDeclaredLength,
            fallback.ClientAttachedFrameNativeToken,
            fallback.ClientAttachedFrameTokenBigEndian,
            fallback.ClientAttachedFrameTokenLittleEndian,
            fallback.ClientBitSidecarOffset,
            fallback.ClientBitSidecarBitCount,
            fallback.ClientBitSidecarPayloadLength,
            fallback.ClientDecodedNetMessageType,
            fallback.ClientDecodedNetMessageName,
            fallback.ClientDecodedNetMessagePayloadKind,
            fallback.ClientDecodedNetMessagePayloadOffset,
            fallback.ClientDecodedNetMessagePayloadLength,
            fallback.ClientDecodedNetMessagePayloadBitCount,
            fallbackCommand is not null,
            fallbackCommand?.ForwardMove,
            fallbackCommand?.SideMove,
            fallbackCommand?.UpMove,
            fallbackCommand?.YawDelta,
            fallbackCommand?.PitchDelta,
            fallbackCommand?.Buttons,
            fallbackCommand?.WeaponSlotHint,
            fallbackCommand?.TeamHint,
            fallbackCommand?.ClassHint,
            ClientPayloadObjectFrameKind: fallback.ClientPayloadObjectFrameKind,
            ClientPayloadObjectHeaderValue: fallback.ClientPayloadObjectHeaderValue,
            ClientPayloadObjectSignedHeaderValue: fallback.ClientPayloadObjectSignedHeaderValue,
            ClientPayloadObjectInnerPayloadOffset: fallback.ClientPayloadObjectInnerPayloadOffset,
            ClientPayloadObjectInnerPayloadLength: fallback.ClientPayloadObjectInnerPayloadLength,
            ClientPayloadObjectBitreaderFieldOffset: fallback.ClientPayloadObjectBitreaderFieldOffset,
            ClientPayloadObjectAssociatedToken: fallback.ClientPayloadObjectAssociatedToken,
            ClientPayloadObjectFragmentIndex: fallback.ClientPayloadObjectFragmentIndex,
            ClientPayloadObjectFragmentTotalCount: fallback.ClientPayloadObjectFragmentTotalCount,
            ClientPayloadObjectFragmentPacketCounter: fallback.ClientPayloadObjectFragmentPacketCounter,
            ClientPayloadObjectFragmentWrappedOrCompressed: fallback.ClientPayloadObjectFragmentWrappedOrCompressed);
    }

    private static Ps3SourceClientCommandIntent? DecodeSourceCommand(string eventName, ReadOnlyMemory<byte> payload)
    {
        if (!eventName.StartsWith("source-", StringComparison.Ordinal)
            || IsClassicSourceConnectionless(payload.Span)
            || !Ps3SourceTransportPacket.TryDecode(payload.Span, out var packet))
        {
            return null;
        }

        var clientPayload = eventName is "source-handoff" or "source-traffic" or "source-generated-drop"
            ? Ps3SourceClientPayloadClassifier.Classify(packet, 1, null)
            : null;
        if (clientPayload is null)
        {
            return null;
        }

        return Ps3SourceClientCommandIntent.TryDecode(clientPayload, packet.Body, out var command)
            ? command
            : null;
    }

    private static DecodedSourceTransportEventData DecodeSourceTransport(string eventName, ReadOnlyMemory<byte> payload)
    {
        if (!eventName.StartsWith("source-", StringComparison.Ordinal)
            || IsClassicSourceConnectionless(payload.Span)
            || !Ps3SourceTransportPacket.TryDecode(payload.Span, out var packet))
        {
            return DecodedSourceTransportEventData.Empty;
        }

        var nativeFrame = packet.ClassifyNativeFrame();
        var payloadSemantic = eventName is "source-handoff" or "source-traffic"
            ? Ps3SourcePayloadSemantics.AnalyzeInitialClientHandoffProbe(packet.Body)
            : Ps3SourcePayloadSemantics.Analyze(packet.Body);
        var direction = eventName is "source-send" or "source-replay-send" or "source-backend-send"
            ? Ps3SourceGameplayDirection.ServerToClient
            : Ps3SourceGameplayDirection.ClientToServer;
        var embeddedObjectData = ExtractEmbeddedObjectEventData(packet.Body, direction.ToString());
        var compactControlData = ExtractCompactControlEventData(packet.Body);
        var clientPayload = eventName is "source-handoff" or "source-traffic"
            ? Ps3SourceClientPayloadClassifier.Classify(packet, 1, null)
            : null;
        var markerlessPayloadData = ExtractMarkerlessPayloadEventData(
            direction,
            null,
            Ps3SourceGameplaySession.ClassifyShape(packet),
            nativeFrame,
            payloadSemantic,
            embeddedObjectData,
            compactControlData,
            clientPayload,
            packet.Body);
        return new DecodedSourceTransportEventData(
            packet.CandidateSequence,
            packet.Body.Length,
            clientPayload?.NativeFrameKind.ToString() ?? nativeFrame.Kind.ToString(),
            nativeFrame.FitsInlineQueue,
            nativeFrame.FitsNativeQueue,
            nativeFrame.FragmentHeaderHex is { Length: > 0 } ? nativeFrame.FragmentHeaderHex : null,
            payloadSemantic.Kind.ToString(),
            payloadSemantic.Role.ToString(),
            compactControlData.Family,
            compactControlData.PrefixLength,
            compactControlData.PrefixHex,
            markerlessPayloadData.Family,
            markerlessPayloadData.PrefixHex,
            markerlessPayloadData.SuffixHex,
            markerlessPayloadData.Entropy,
            markerlessPayloadData.PrintableRatio,
            payloadSemantic.EmbeddedMarkers.Select(static marker => marker.Marker).Distinct(StringComparer.Ordinal).ToArray(),
            embeddedObjectData.RecordRoles,
            embeddedObjectData.ObjectIds,
            embeddedObjectData.ClassIds,
            embeddedObjectData.ObjectLinks,
            embeddedObjectData.DisplayNames,
            embeddedObjectData.RecordSummaries,
            embeddedObjectData.ObjectIdOrder,
            embeddedObjectData.ClassIdOrder,
            embeddedObjectData.ObjectLinkOrder,
            embeddedObjectData.DisplayNameOrder,
            embeddedObjectData.PrefixLength,
            embeddedObjectData.PrefixHex,
            embeddedObjectData.PrefixFamily,
            embeddedObjectData.PrefixEntropy,
            embeddedObjectData.PrefixPrintableRatio,
            embeddedObjectData.PrefixMeaning,
            clientPayload?.Role.ToString(),
            clientPayload?.BodyPrefixHex,
            clientPayload?.FirstUInt16BigEndian,
            clientPayload?.FirstUInt16LittleEndian,
            clientPayload?.LastUInt16BigEndian,
            clientPayload?.ReliableAssociationMessageType,
            clientPayload?.ReliableAssociationNativeToken,
            clientPayload?.ReliableAssociationTokenBigEndian,
            clientPayload?.ReliableAssociationTokenLittleEndian,
            clientPayload?.AttachedFrameKind,
            clientPayload?.AttachedFrameDeclaredLength,
            clientPayload?.AttachedFrameNativeToken,
            clientPayload?.AttachedFrameTokenBigEndian,
            clientPayload?.AttachedFrameTokenLittleEndian,
            clientPayload?.BitSidecarOffset,
            clientPayload?.BitSidecarBitCount,
            clientPayload?.BitSidecarPayloadLength,
            clientPayload?.DecodedNetMessageType,
            clientPayload?.DecodedNetMessageName,
            clientPayload?.DecodedNetMessagePayloadKind,
            clientPayload?.DecodedNetMessagePayloadOffset,
            clientPayload?.DecodedNetMessagePayloadLength,
            clientPayload?.DecodedNetMessagePayloadBitCount,
            ClientPayloadObjectFrameKind: clientPayload?.PayloadObjectFrameKind,
            ClientPayloadObjectHeaderValue: clientPayload?.PayloadObjectHeaderValue,
            ClientPayloadObjectSignedHeaderValue: clientPayload?.PayloadObjectSignedHeaderValue,
            ClientPayloadObjectInnerPayloadOffset: clientPayload?.PayloadObjectInnerPayloadOffset,
            ClientPayloadObjectInnerPayloadLength: clientPayload?.PayloadObjectInnerPayloadLength,
            ClientPayloadObjectBitreaderFieldOffset: clientPayload?.PayloadObjectBitreaderFieldOffset,
            ClientPayloadObjectAssociatedToken: clientPayload?.PayloadObjectAssociatedToken,
            ClientPayloadObjectFragmentIndex: clientPayload?.PayloadObjectFragmentIndex,
            ClientPayloadObjectFragmentTotalCount: clientPayload?.PayloadObjectFragmentTotalCount,
            ClientPayloadObjectFragmentPacketCounter: clientPayload?.PayloadObjectFragmentPacketCounter,
            ClientPayloadObjectFragmentWrappedOrCompressed: clientPayload?.PayloadObjectFragmentWrappedOrCompressed);
    }

    private static SourceCompactControlEventData ExtractCompactControlEventData(ReadOnlySpan<byte> body)
    {
        var records = Ps3SourceEmbeddedObjectRecords.Extract(body)
            .Where(static record => record.Role != Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise)
            .ToArray();
        var compact = Ps3SourceCompactControlFrame.TryAnalyze(body, records);
        return compact is null
            ? SourceCompactControlEventData.Empty
            : new SourceCompactControlEventData(
                compact.Family,
                compact.PrefixLength,
                compact.PrefixHex);
    }

    private static SourceMarkerlessPayloadEventData ExtractMarkerlessPayloadEventData(
        Ps3SourceGameplayDirection direction,
        int? directionPacketCount,
        Ps3SourceGameplayPacketShape shape,
        Ps3SourceNativeFrameInfo nativeFrame,
        Ps3SourcePayloadSemanticInfo semantic,
        SourceEmbeddedObjectEventData embeddedObjectData,
        SourceCompactControlEventData compactControlData,
        Ps3SourceClientPayloadInfo? clientPayload,
        ReadOnlySpan<byte> body)
    {
        if (body.Length == 0
            || compactControlData.Family is not null
            || embeddedObjectData.RecordRoles is { Length: > 0 })
        {
            return SourceMarkerlessPayloadEventData.Empty;
        }

        var family = MarkerlessPayloadFamilyFor(
            direction,
            directionPacketCount,
            shape,
            nativeFrame,
            semantic,
            clientPayload,
            body.Length);
        return new SourceMarkerlessPayloadEventData(
            family,
            Convert.ToHexString(body[..Math.Min(body.Length, 16)]).ToLowerInvariant(),
            Convert.ToHexString(body[^Math.Min(body.Length, 16)..]).ToLowerInvariant(),
            semantic.Entropy,
            semantic.PrintableRatio);
    }

    private static string MarkerlessPayloadFamilyFor(
        Ps3SourceGameplayDirection direction,
        int? directionPacketCount,
        Ps3SourceGameplayPacketShape shape,
        Ps3SourceNativeFrameInfo nativeFrame,
        Ps3SourcePayloadSemanticInfo semantic,
        Ps3SourceClientPayloadInfo? clientPayload,
        int bodyLength)
    {
        if (semantic.Role == Ps3SourcePayloadSemanticRole.InitialHandoffClientProbe)
        {
            return "MarkerlessInitialHandoffProbe";
        }

        if (semantic.Role == Ps3SourcePayloadSemanticRole.NativeLzssCompressed)
        {
            return "MarkerlessNativeLzssWrappedPayload";
        }

        if (semantic.Role is Ps3SourcePayloadSemanticRole.NativeServerInfo49
            or Ps3SourcePayloadSemanticRole.NativeResourceStringTable45
            or Ps3SourcePayloadSemanticRole.NativePlayerSummary44)
        {
            return $"Markerless{semantic.Role}";
        }

        if (clientPayload?.Role == Ps3SourceClientPayloadRole.UserCommandCandidate)
        {
            return "MarkerlessClientUserCommandOrLoadingAck";
        }

        if (nativeFrame.Kind == Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate)
        {
            return direction == Ps3SourceGameplayDirection.ServerToClient
                ? "MarkerlessServerQueuedPeerChunk"
                : "MarkerlessClientQueuedPeerChunk";
        }

        if (shape == Ps3SourceGameplayPacketShape.NearMtuFragment)
        {
            return direction == Ps3SourceGameplayDirection.ServerToClient
                ? "MarkerlessServerNearMtuLoadingChunk"
                : "MarkerlessClientNearMtuUploadChunk";
        }

        var earlyDirectionPacket = directionPacketCount is null or <= 8;
        if (earlyDirectionPacket && direction == Ps3SourceGameplayDirection.ServerToClient)
        {
            return bodyLength >= 512
                ? "MarkerlessServerSetupApprovalBlob"
                : $"MarkerlessServerSetupStatePulse{bodyLength}";
        }

        if (earlyDirectionPacket && direction == Ps3SourceGameplayDirection.ClientToServer)
        {
            return bodyLength >= 256
                ? "MarkerlessClientSetupUpload"
                : $"MarkerlessClientSetupControl{bodyLength}";
        }

        if (bodyLength >= 512)
        {
            return direction == Ps3SourceGameplayDirection.ServerToClient
                ? "MarkerlessServerLargeLoadingOrEntityState"
                : "MarkerlessClientLargeStateUpload";
        }

        return direction == Ps3SourceGameplayDirection.ServerToClient
            ? $"MarkerlessServerSteadyStatePulse{bodyLength}"
            : $"MarkerlessClientSteadyStateInput{bodyLength}";
    }

    private static SourceEmbeddedObjectEventData ExtractEmbeddedObjectEventData(
        ReadOnlySpan<byte> body,
        string? directionHint = null)
    {
        var records = Ps3SourceEmbeddedObjectRecords.Extract(body)
            .Where(static record => record.Role != Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise)
            .ToArray();
        if (records.Length == 0)
        {
            return SourceEmbeddedObjectEventData.Empty;
        }

        var prefixLength = records[0].Offset;
        var prefixHex = prefixLength <= 0
            ? null
            : Convert.ToHexString(body[..Math.Min(prefixLength, body.Length)]).ToLowerInvariant();
        var prefix = Ps3SourceEmbeddedPrefixFrame.TryAnalyze(body, records, directionHint);
        return new SourceEmbeddedObjectEventData(
            Distinct(records.Select(static record => record.Role.ToString())),
            Distinct(records.Select(static record => Hex(record.ObjectId))),
            Distinct(records.Select(static record => Hex(record.ClassId))),
            Distinct(records
                .Where(static record => record.ObjectId is not null
                    && record.FieldB is not null
                    && record.Role is Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink
                        or Ps3SourceEmbeddedObjectRecordRole.PlayerObject
                        or Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject)
                .Select(static record => $"{Hex(record.ObjectId)}->{Hex(record.FieldB)}")),
            Distinct(records.Select(static record => record.DisplayName ?? "")),
            Ordered(records.Select(RecordSummary)),
            Ordered(records.Select(static record => Hex(record.ObjectId))),
            Ordered(records.Select(static record => Hex(record.ClassId))),
            Ordered(records
                .Where(static record => record.ObjectId is not null
                    && record.FieldB is not null
                    && record.Role is Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink
                        or Ps3SourceEmbeddedObjectRecordRole.PlayerObject
                        or Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject)
                .Select(static record => $"{Hex(record.ObjectId)}->{Hex(record.FieldB)}")),
            Ordered(records.Select(static record => record.DisplayName ?? "")),
            prefixLength,
            prefixHex,
            prefix?.Family,
            prefix?.PrefixEntropy,
            prefix?.PrefixPrintableRatio,
            prefix?.Meaning);
    }

    private static string[]? Distinct(IEnumerable<string> values)
    {
        var result = values
            .Where(static value => !string.IsNullOrWhiteSpace(value) && value != "none")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(32)
            .ToArray();
        return result.Length == 0 ? null : result;
    }

    private static string[]? Ordered(IEnumerable<string> values)
    {
        var result = values
            .Where(static value => !string.IsNullOrWhiteSpace(value) && value != "none")
            .Take(64)
            .ToArray();
        return result.Length == 0 ? null : result;
    }

    private static string RecordSummary(Ps3SourceEmbeddedObjectRecord record)
    {
        return $"offset={record.Offset} marker={record.Marker} role={record.Role} object={Hex(record.ObjectId)} link={Hex(record.FieldB)} class={Hex(record.ClassId)} name={record.DisplayName ?? ""}";
    }

    private static string Hex(uint? value)
    {
        return value is null ? "none" : value.Value.ToString("x8");
    }

    private static string? SourceBodySignature(string eventName, ReadOnlyMemory<byte> payload)
    {
        if (!eventName.StartsWith("source-", StringComparison.Ordinal)
            || payload.IsEmpty
            || IsClassicSourceConnectionless(payload.Span)
            || !Ps3SourceTransportPacket.TryDecode(payload.Span, out _))
        {
            return null;
        }

        return Ps3SourceGameplaySignatures.BodyRunSignature([payload.ToArray()]);
    }

    private sealed record SourceEventData(
        int? Sequence,
        int? BodyLength,
        int? SequenceDelta,
        string? Shape,
        string? NativeFrameKind,
        bool? FitsInlineQueue,
        bool? FitsNativeQueue,
        string? FragmentHeaderHex,
        string? PayloadSemanticKind,
        string? PayloadSemanticRole,
        string? CompactControlFamily,
        int? CompactControlPrefixLength,
        string? CompactControlPrefixHex,
        string? MarkerlessPayloadFamily,
        string? MarkerlessPayloadPrefixHex,
        string? MarkerlessPayloadSuffixHex,
        double? MarkerlessPayloadEntropy,
        double? MarkerlessPayloadPrintableRatio,
        string[]? EmbeddedMarkers,
        string[]? EmbeddedRecordRoles,
        string[]? EmbeddedObjectIds,
        string[]? EmbeddedClassIds,
        string[]? EmbeddedObjectLinks,
        string[]? EmbeddedDisplayNames,
        string[]? EmbeddedRecordSummaries,
        string[]? EmbeddedObjectIdOrder,
        string[]? EmbeddedClassIdOrder,
        string[]? EmbeddedObjectLinkOrder,
        string[]? EmbeddedDisplayNameOrder,
        int? EmbeddedPrefixLength,
        string? EmbeddedPrefixHex,
        string? EmbeddedPrefixFamily,
        double? EmbeddedPrefixEntropy,
        double? EmbeddedPrefixPrintableRatio,
        string? EmbeddedPrefixMeaning,
        int? DirectionPacketCount,
        bool? SequenceDecrease,
        string? BodySignature,
        string? ClientPayloadRole,
        string? ClientBodyPrefixHex,
        int? ClientFirstUInt16BigEndian,
        int? ClientFirstUInt16LittleEndian,
        int? ClientLastUInt16BigEndian,
        int? ClientReliableAssociationMessageType,
        long? ClientReliableAssociationNativeToken,
        long? ClientReliableAssociationTokenBigEndian,
        long? ClientReliableAssociationTokenLittleEndian,
        int? ClientAttachedFrameKind,
        int? ClientAttachedFrameDeclaredLength,
        long? ClientAttachedFrameNativeToken,
        long? ClientAttachedFrameTokenBigEndian,
        long? ClientAttachedFrameTokenLittleEndian,
        int? ClientBitSidecarOffset,
        int? ClientBitSidecarBitCount,
        int? ClientBitSidecarPayloadLength,
        int? ClientDecodedNetMessageType,
        string? ClientDecodedNetMessageName,
        string? ClientDecodedNetMessagePayloadKind,
        int? ClientDecodedNetMessagePayloadOffset,
        int? ClientDecodedNetMessagePayloadLength,
        int? ClientDecodedNetMessagePayloadBitCount,
        bool? ClientCommandDecoded,
        int? ClientCommandForwardMove,
        int? ClientCommandSideMove,
        int? ClientCommandUpMove,
        float? ClientCommandYawDelta,
        float? ClientCommandPitchDelta,
        int? ClientCommandButtons,
        int? ClientCommandWeaponSlotHint,
        int? ClientCommandTeamHint,
        int? ClientCommandClassHint,
        string? ClientPayloadObjectFrameKind = null,
        long? ClientPayloadObjectHeaderValue = null,
        int? ClientPayloadObjectSignedHeaderValue = null,
        int? ClientPayloadObjectInnerPayloadOffset = null,
        int? ClientPayloadObjectInnerPayloadLength = null,
        int? ClientPayloadObjectBitreaderFieldOffset = null,
        long? ClientPayloadObjectAssociatedToken = null,
        int? ClientPayloadObjectFragmentIndex = null,
        int? ClientPayloadObjectFragmentTotalCount = null,
        long? ClientPayloadObjectFragmentPacketCounter = null,
        bool? ClientPayloadObjectFragmentWrappedOrCompressed = null);

    private sealed record DecodedSourceTransportEventData(
        int? Sequence,
        int? BodyLength,
        string? NativeFrameKind,
        bool? FitsInlineQueue,
        bool? FitsNativeQueue,
        string? FragmentHeaderHex,
        string? PayloadSemanticKind,
        string? PayloadSemanticRole,
        string? CompactControlFamily,
        int? CompactControlPrefixLength,
        string? CompactControlPrefixHex,
        string? MarkerlessPayloadFamily,
        string? MarkerlessPayloadPrefixHex,
        string? MarkerlessPayloadSuffixHex,
        double? MarkerlessPayloadEntropy,
        double? MarkerlessPayloadPrintableRatio,
        string[]? EmbeddedMarkers,
        string[]? EmbeddedRecordRoles,
        string[]? EmbeddedObjectIds,
        string[]? EmbeddedClassIds,
        string[]? EmbeddedObjectLinks,
        string[]? EmbeddedDisplayNames,
        string[]? EmbeddedRecordSummaries,
        string[]? EmbeddedObjectIdOrder,
        string[]? EmbeddedClassIdOrder,
        string[]? EmbeddedObjectLinkOrder,
        string[]? EmbeddedDisplayNameOrder,
        int? EmbeddedPrefixLength,
        string? EmbeddedPrefixHex,
        string? EmbeddedPrefixFamily,
        double? EmbeddedPrefixEntropy,
        double? EmbeddedPrefixPrintableRatio,
        string? EmbeddedPrefixMeaning,
        string? ClientPayloadRole,
        string? ClientBodyPrefixHex,
        int? ClientFirstUInt16BigEndian,
        int? ClientFirstUInt16LittleEndian,
        int? ClientLastUInt16BigEndian,
        int? ClientReliableAssociationMessageType,
        long? ClientReliableAssociationNativeToken,
        long? ClientReliableAssociationTokenBigEndian,
        long? ClientReliableAssociationTokenLittleEndian,
        int? ClientAttachedFrameKind,
        int? ClientAttachedFrameDeclaredLength,
        long? ClientAttachedFrameNativeToken,
        long? ClientAttachedFrameTokenBigEndian,
        long? ClientAttachedFrameTokenLittleEndian,
        int? ClientBitSidecarOffset,
        int? ClientBitSidecarBitCount,
        int? ClientBitSidecarPayloadLength,
        int? ClientDecodedNetMessageType,
        string? ClientDecodedNetMessageName,
        string? ClientDecodedNetMessagePayloadKind,
        int? ClientDecodedNetMessagePayloadOffset,
        int? ClientDecodedNetMessagePayloadLength,
        int? ClientDecodedNetMessagePayloadBitCount,
        string? ClientPayloadObjectFrameKind = null,
        long? ClientPayloadObjectHeaderValue = null,
        int? ClientPayloadObjectSignedHeaderValue = null,
        int? ClientPayloadObjectInnerPayloadOffset = null,
        int? ClientPayloadObjectInnerPayloadLength = null,
        int? ClientPayloadObjectBitreaderFieldOffset = null,
        long? ClientPayloadObjectAssociatedToken = null,
        int? ClientPayloadObjectFragmentIndex = null,
        int? ClientPayloadObjectFragmentTotalCount = null,
        long? ClientPayloadObjectFragmentPacketCounter = null,
        bool? ClientPayloadObjectFragmentWrappedOrCompressed = null)
    {
        public static DecodedSourceTransportEventData Empty { get; } = new(
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null);
    }

    private sealed record SourceCompactControlEventData(
        string? Family,
        int? PrefixLength,
        string? PrefixHex)
    {
        public static SourceCompactControlEventData Empty { get; } = new(null, null, null);
    }

    private sealed record SourceMarkerlessPayloadEventData(
        string? Family,
        string? PrefixHex,
        string? SuffixHex,
        double? Entropy,
        double? PrintableRatio)
    {
        public static SourceMarkerlessPayloadEventData Empty { get; } = new(null, null, null, null, null);
    }

    private sealed record SourceEmbeddedObjectEventData(
        string[]? RecordRoles,
        string[]? ObjectIds,
        string[]? ClassIds,
        string[]? ObjectLinks,
        string[]? DisplayNames,
        string[]? RecordSummaries,
        string[]? ObjectIdOrder,
        string[]? ClassIdOrder,
        string[]? ObjectLinkOrder,
        string[]? DisplayNameOrder,
        int? PrefixLength,
        string? PrefixHex,
        string? PrefixFamily,
        double? PrefixEntropy,
        double? PrefixPrintableRatio,
        string? PrefixMeaning)
    {
        public static SourceEmbeddedObjectEventData Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
    }

    private static bool IsClassicSourceConnectionless(ReadOnlySpan<byte> payload)
    {
        return SourceBackendPayloadAdapter.IsClassicSourceConnectionless(payload);
    }

    private void RecordSourceProxyError(string endpoint, Exception exception)
    {
        _eventSink?.Record(new GameManagerServerEvent(
            DateTimeOffset.UtcNow,
            _profile.Name,
            "source-proxy-error",
            endpoint,
            "SourceBackend",
            PlayerJoinState.SourceHandoff.ToString(),
            PlayerJoinState.SourceHandoff.ToString(),
            0,
            exception.Message,
            ""));
    }
}
