using System.Text.Json;
using PlasmaGameManager.Protocol;
using PlasmaGameManager.Server;

namespace PlasmaGameManager.ReTools;

public sealed class PcapGeneratedSourceTransformProbeAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapGeneratedSourceTransformProbeReport> AnalyzeDirectoryAsync(
        string inputDirectory,
        string outputPath,
        string? ekey = null)
    {
        var report = AnalyzeDirectory(inputDirectory, ekey);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        return report;
    }

    public PcapGeneratedSourceTransformProbeReport AnalyzeDirectory(string inputDirectory, string? ekey = null)
    {
        var ekeyBytes = DecodeEkey(ekey);
        var files = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var analyses = files.Select(path => AnalyzeFile(inputDirectory, path, ekeyBytes)).ToArray();
        var active = analyses.Where(static analysis => analysis.HasActiveSourceFlow).ToArray();
        var probes = active.SelectMany(static analysis => analysis.Probes).ToArray();
        return new PcapGeneratedSourceTransformProbeReport(
            "pcap-generated-source-transform-probes",
            new PcapGeneratedSourceTransformProbeSummary(
                files.Length,
                active.Length,
                probes.Length,
                probes.Count(static probe => probe.EqualBodyLength),
                probes.Count(static probe => probe.XorConstant),
                probes.Count(static probe => probe.BitwiseNot),
                probes.Count(static probe => probe.CapturedLzssWrapped),
                probes.Count(static probe => probe.GeneratedLzssWrapped),
                ekeyBytes.Length,
                probes.Count(static probe => probe.GeneratedRc4EkeyEqualsCaptured),
                probes.Count(static probe => probe.CapturedRc4EkeyEqualsGenerated),
                probes.Count(static probe => probe.GeneratedAesCtrZeroIvEkeyEqualsCaptured),
                probes.Count(static probe => probe.CapturedAesCtrZeroIvEkeyEqualsGenerated),
                probes.Any(static probe => probe.GeneratedSemanticRole == "NativeResourceStringTable45")
                    ? "resource-table-transform-probe-ready"
                    : "no-resource-table-probes"),
            analyses);
    }

    private PcapGeneratedSourceTransformProbeFileAnalysis AnalyzeFile(string inputDirectory, string file, byte[] ekey)
    {
        var relativePath = Path.GetRelativePath(inputDirectory, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapGeneratedSourceTransformProbeFileAnalysis(
                relativePath,
                false,
                "",
                "",
                []);
        }

        var steps = replay.SourcePackets
            .Select(static packet => new Ps3SourceGameplayReplayStep(
                packet.Direction == PcapActiveFlowDirection.ClientToServer
                    ? Ps3SourceGameplayDirection.ClientToServer
                    : Ps3SourceGameplayDirection.ServerToClient,
                packet.Payload,
                packet.PacketIndex,
                packet.TimestampMicroseconds))
            .ToArray();
        var turns = Ps3SourceGameplayTurnReplayDriver.BuildTurns(steps);
        var responder = new Ps3NativeSourceResponder();
        var game = new GameManagerSession(BuildSessionOptions(relativePath, replay));
        var player = game.GetOrAddPlayer(replay.ClientEndpoint);
        player.Name = "CapturedPlayer";

        var probes = new List<PcapGeneratedSourceTransformProbe>();
        foreach (var turn in turns)
        {
            var generated = new List<byte[]>();
            foreach (var clientPacket in turn.ClientPackets)
            {
                generated.AddRange(responder.BuildResponses(game, player, clientPacket.Payload)
                    .Select(static response => response.Payload));
            }

            var captured = turn.ServerResponses.Select(static packet => packet.Payload).ToArray();
            var count = Math.Min(captured.Length, generated.Count);
            for (var i = 0; i < count; i++)
            {
                if (!Ps3SourceTransportPacket.TryDecode(captured[i], out var capturedPacket)
                    || !Ps3SourceTransportPacket.TryDecode(generated[i], out var generatedPacket))
                {
                    continue;
                }

                if (capturedPacket.Body.Length != generatedPacket.Body.Length)
                {
                    continue;
                }

                var capturedSemantics = Ps3SourcePayloadSemantics.Analyze(capturedPacket.Body);
                var generatedSemantics = Ps3SourcePayloadSemantics.Analyze(generatedPacket.Body);
                if (capturedSemantics.Role == generatedSemantics.Role
                    && Math.Abs(capturedSemantics.Entropy - generatedSemantics.Entropy) < 0.5)
                {
                    continue;
                }

                probes.Add(BuildProbe(
                    turn.TurnIndex,
                    i,
                    capturedPacket,
                    generatedPacket,
                    capturedSemantics,
                    generatedSemantics,
                    ekey));
                if (probes.Count >= 64)
                {
                    break;
                }
            }

            if (probes.Count >= 64)
            {
                break;
            }
        }

        return new PcapGeneratedSourceTransformProbeFileAnalysis(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            probes.ToArray());
    }

    private static PcapGeneratedSourceTransformProbe BuildProbe(
        int turnIndex,
        int packetIndex,
        Ps3SourceTransportPacket captured,
        Ps3SourceTransportPacket generated,
        Ps3SourcePayloadSemanticInfo capturedSemantics,
        Ps3SourcePayloadSemanticInfo generatedSemantics,
        byte[] ekey)
    {
        var xor = Xor(captured.Body, generated.Body);
        var generatedRc4 = ekey.Length == 0 ? [] : Rc4(ekey, generated.Body);
        var capturedRc4 = ekey.Length == 0 ? [] : Rc4(ekey, captured.Body);
        var generatedAesCtr = ekey.Length == 16 ? AesCtrZeroIv(ekey, generated.Body) : [];
        var capturedAesCtr = ekey.Length == 16 ? AesCtrZeroIv(ekey, captured.Body) : [];
        return new PcapGeneratedSourceTransformProbe(
            turnIndex,
            packetIndex,
            captured.PayloadLength,
            captured.Body.Length,
            captured.Body.Length == generated.Body.Length,
            captured.ClassifyNativeFrame().Kind.ToString(),
            generated.ClassifyNativeFrame().Kind.ToString(),
            capturedSemantics.Role.ToString(),
            generatedSemantics.Role.ToString(),
            capturedSemantics.Entropy,
            generatedSemantics.Entropy,
            capturedSemantics.PrintableRatio,
            generatedSemantics.PrintableRatio,
            Ps3SourceLzss.IsWrapped(captured.Body),
            Ps3SourceLzss.IsWrapped(generated.Body),
            Convert.ToHexString(captured.Body.AsSpan(0, Math.Min(captured.Body.Length, 16))).ToLowerInvariant(),
            Convert.ToHexString(generated.Body.AsSpan(0, Math.Min(generated.Body.Length, 16))).ToLowerInvariant(),
            Convert.ToHexString(xor.AsSpan(0, Math.Min(xor.Length, 16))).ToLowerInvariant(),
            Entropy(xor),
            PrintableRatio(xor),
            xor.Distinct().Count(),
            xor.Length != 0 && xor.All(value => value == xor[0]),
            IsBitwiseNot(captured.Body, generated.Body),
            LongestEqualRun(captured.Body, generated.Body),
            LongestXorRun(xor),
            ekey.Length != 0 && generatedRc4.AsSpan().SequenceEqual(captured.Body),
            ekey.Length != 0 && capturedRc4.AsSpan().SequenceEqual(generated.Body),
            PrefixHex(generatedRc4, 16),
            PrefixHex(capturedRc4, 16),
            generatedRc4.Length == 0 ? 0 : LongestEqualRun(captured.Body, generatedRc4),
            capturedRc4.Length == 0 ? 0 : LongestEqualRun(generated.Body, capturedRc4),
            ekey.Length == 16 && generatedAesCtr.AsSpan().SequenceEqual(captured.Body),
            ekey.Length == 16 && capturedAesCtr.AsSpan().SequenceEqual(generated.Body),
            PrefixHex(generatedAesCtr, 16),
            PrefixHex(capturedAesCtr, 16),
            generatedAesCtr.Length == 0 ? 0 : LongestEqualRun(captured.Body, generatedAesCtr),
            capturedAesCtr.Length == 0 ? 0 : LongestEqualRun(generated.Body, capturedAesCtr));
    }

    private static byte[] DecodeEkey(string? ekey)
    {
        if (string.IsNullOrWhiteSpace(ekey))
        {
            return [];
        }

        var decoded = Uri.UnescapeDataString(ekey.Trim());
        try
        {
            return Convert.FromBase64String(decoded);
        }
        catch (FormatException)
        {
            return System.Text.Encoding.ASCII.GetBytes(decoded);
        }
    }

    private static string PrefixHex(byte[] body, int length)
    {
        return Convert.ToHexString(body.AsSpan(0, Math.Min(body.Length, length))).ToLowerInvariant();
    }

    private static byte[] Rc4(ReadOnlySpan<byte> key, ReadOnlySpan<byte> body)
    {
        Span<byte> s = stackalloc byte[256];
        for (var i = 0; i < s.Length; i++)
        {
            s[i] = (byte)i;
        }

        var j = 0;
        for (var i = 0; i < s.Length; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xff;
            (s[i], s[j]) = (s[j], s[i]);
        }

        var result = new byte[body.Length];
        var x = 0;
        j = 0;
        for (var offset = 0; offset < body.Length; offset++)
        {
            x = (x + 1) & 0xff;
            j = (j + s[x]) & 0xff;
            (s[x], s[j]) = (s[j], s[x]);
            result[offset] = (byte)(body[offset] ^ s[(s[x] + s[j]) & 0xff]);
        }

        return result;
    }

    private static byte[] AesCtrZeroIv(ReadOnlySpan<byte> key, ReadOnlySpan<byte> body)
    {
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Mode = System.Security.Cryptography.CipherMode.ECB;
        aes.Padding = System.Security.Cryptography.PaddingMode.None;
        aes.Key = key.ToArray();

        using var encryptor = aes.CreateEncryptor();
        Span<byte> counter = stackalloc byte[16];
        var result = new byte[body.Length];
        for (var offset = 0; offset < body.Length; offset += 16)
        {
            var counterBlock = counter.ToArray();
            var streamBlock = new byte[16];
            encryptor.TransformBlock(counterBlock, 0, counterBlock.Length, streamBlock, 0);
            var count = Math.Min(16, body.Length - offset);
            for (var i = 0; i < count; i++)
            {
                result[offset + i] = (byte)(body[offset + i] ^ streamBlock[i]);
            }

            IncrementBigEndian(counter);
        }

        return result;
    }

    private static void IncrementBigEndian(Span<byte> counter)
    {
        for (var i = counter.Length - 1; i >= 0; i--)
        {
            counter[i]++;
            if (counter[i] != 0)
            {
                break;
            }
        }
    }

    private static byte[] Xor(byte[] left, byte[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        var result = new byte[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = (byte)(left[i] ^ right[i]);
        }

        return result;
    }

    private static bool IsBitwiseNot(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if ((byte)~right[i] != left[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int LongestEqualRun(byte[] left, byte[] right)
    {
        var current = 0;
        var longest = 0;
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            if (left[i] == right[i])
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 0;
            }
        }

        return longest;
    }

    private static int LongestXorRun(byte[] xor)
    {
        if (xor.Length == 0)
        {
            return 0;
        }

        var current = 1;
        var longest = 1;
        for (var i = 1; i < xor.Length; i++)
        {
            if (xor[i] == xor[i - 1])
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 1;
            }
        }

        return longest;
    }

    private static double Entropy(byte[] body)
    {
        if (body.Length == 0)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var b in body)
        {
            counts[b]++;
        }

        double entropy = 0;
        foreach (var count in counts)
        {
            if (count == 0)
            {
                continue;
            }

            var p = (double)count / body.Length;
            entropy -= p * Math.Log2(p);
        }

        return Math.Round(entropy, 4);
    }

    private static double PrintableRatio(byte[] body)
    {
        if (body.Length == 0)
        {
            return 0;
        }

        return Math.Round(body.Count(static b => b is >= 32 and <= 126) / (double)body.Length, 4);
    }

    private static GameManagerSessionOptions BuildSessionOptions(string relativePath, PcapActiveFlowReplay replay)
    {
        var map = relativePath.Contains("dustbowl", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("cp_db", StringComparison.OrdinalIgnoreCase)
            ? "cp_dustbowl"
            : "ctf_2fort";
        var gameMode = map.StartsWith("cp_", StringComparison.Ordinal)
            ? "control-point"
            : "capture-the-flag";
        return new GameManagerSessionOptions(
            GameId: 9843,
            MapName: map,
            Name: "TF2PS3",
            MaxPlayers: 24,
            PreferredPlayerId: 197,
            GameMode: gameMode,
            RankingMode: "Unranked",
            IsRanked: false,
            TimeLimitMinutes: gameMode == "control-point" ? 15 : 30,
            MaxRounds: 5,
            FlagCaptureLimit: gameMode == "capture-the-flag" ? 3 : 0,
            AutoBalance: true,
            AdvertisedHost: replay.ServerEndpoint.Split(':')[0],
            AdvertisedPort: replay.ServerPort);
    }
}

public sealed record PcapGeneratedSourceTransformProbeReport(
    string Status,
    PcapGeneratedSourceTransformProbeSummary Summary,
    PcapGeneratedSourceTransformProbeFileAnalysis[] Files);

public sealed record PcapGeneratedSourceTransformProbeSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int ProbeCount,
    int EqualBodyLengthProbeCount,
    int XorConstantProbeCount,
    int BitwiseNotProbeCount,
    int CapturedLzssWrappedProbeCount,
    int GeneratedLzssWrappedProbeCount,
    int EkeyByteCount,
    int GeneratedRc4EkeyEqualsCapturedCount,
    int CapturedRc4EkeyEqualsGeneratedCount,
    int GeneratedAesCtrZeroIvEkeyEqualsCapturedCount,
    int CapturedAesCtrZeroIvEkeyEqualsGeneratedCount,
    string Conclusion);

public sealed record PcapGeneratedSourceTransformProbeFileAnalysis(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    PcapGeneratedSourceTransformProbe[] Probes);

public sealed record PcapGeneratedSourceTransformProbe(
    int TurnIndex,
    int PacketIndex,
    int PayloadLength,
    int BodyLength,
    bool EqualBodyLength,
    string CapturedNativeFrameKind,
    string GeneratedNativeFrameKind,
    string CapturedSemanticRole,
    string GeneratedSemanticRole,
    double CapturedEntropy,
    double GeneratedEntropy,
    double CapturedPrintableRatio,
    double GeneratedPrintableRatio,
    bool CapturedLzssWrapped,
    bool GeneratedLzssWrapped,
    string CapturedPrefixHex,
    string GeneratedPrefixHex,
    string XorPrefixHex,
    double XorEntropy,
    double XorPrintableRatio,
    int XorDistinctByteCount,
    bool XorConstant,
    bool BitwiseNot,
    int LongestEqualByteRun,
    int LongestEqualXorByteRun,
    bool GeneratedRc4EkeyEqualsCaptured,
    bool CapturedRc4EkeyEqualsGenerated,
    string GeneratedRc4EkeyPrefixHex,
    string CapturedRc4EkeyPrefixHex,
    int GeneratedRc4EkeyLongestCapturedRun,
    int CapturedRc4EkeyLongestGeneratedRun,
    bool GeneratedAesCtrZeroIvEkeyEqualsCaptured,
    bool CapturedAesCtrZeroIvEkeyEqualsGenerated,
    string GeneratedAesCtrZeroIvEkeyPrefixHex,
    string CapturedAesCtrZeroIvEkeyPrefixHex,
    int GeneratedAesCtrZeroIvEkeyLongestCapturedRun,
    int CapturedAesCtrZeroIvEkeyLongestGeneratedRun);
