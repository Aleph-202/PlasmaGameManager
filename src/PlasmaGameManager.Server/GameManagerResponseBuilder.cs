using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.Server;

public sealed class GameManagerResponseBuilder
{
    private static readonly (byte[] Prefix, byte[] Response)[] ProbePrefixResponses =
    [
        (Convert.FromHexString("00007bec7a9d"), Convert.FromHexString("000616177f0eedc250754fd03b981c955f0d72f2")),
        (Convert.FromHexString("0000a4875dc1"), Convert.FromHexString("0006f4812c73b50f6a8dc077a17bf1bc40b44d0f")),
        (Convert.FromHexString("0000b85026bd"), Convert.FromHexString("0006c509f6a221857ea80c4a87ec5119d37d5aec")),
        (Convert.FromHexString("0000c7e60c1e"), Convert.FromHexString("0006249f1d41d5ac9846ef09a6368021c6d25f9e")),
        (Convert.FromHexString("0000c92c018b"), Convert.FromHexString("00060d8535a196ff85199afa428d5d7b79000ef2")),
        (Convert.FromHexString("0000ef55554e"), Convert.FromHexString("0006053cf4cda6054aeec8244baf39a620512cae")),
        (Convert.FromHexString("0000196fa498"), Convert.FromHexString("000687f204fffeae02da6ea8611465edd265c9e2")),
        (Convert.FromHexString("00008bddafda"), Convert.FromHexString("00066058265b53d423c78f04a58b4a3775b232c8")),
    ];

    private static readonly (byte[] Prefix, byte[] Response)[] ProbeExactResponses =
    [
        (Convert.FromHexString("0000c24591e5f16031761099a4c828c19ed62d70a272f123"), Convert.FromHexString("00065df67bae61effaa9bb307d6632286a2a99f2")),
        (Convert.FromHexString("0000e578ad8f63859bfef73f3c45f60c7d9c6693b2191bf6"), Convert.FromHexString("00064fbfc823911148a68e4ae70e51bc38ae4e11")),
        (Convert.FromHexString("00064fbfc86391a2c46d334be70e51bf38ae4e13fd7d0ce1"), Convert.FromHexString("000b0c864c1a473d77ed5e2d1437b6cbbd07dc23")),
    ];

    private static readonly (byte[] Prefix, byte[] TailXor)[] ProbeTailXors =
    [
        (Convert.FromHexString("0000a4875dc1"), Convert.FromHexString("e5ac4845")),
        (Convert.FromHexString("0000b85026bd"), Convert.FromHexString("4aa36c68")),
    ];

    private static readonly byte[] DefaultProbeResponse = Convert.FromHexString("0006c509f6a221857ea80c4a87ec5119d37d5aec");
    private static readonly byte[] DefaultProbeTailXor = Convert.FromHexString("4aa36c68");

    private readonly GameManagerSession _game;
    private readonly PlayerSession _player;

    public GameManagerResponseBuilder(GameManagerSession game, PlayerSession player)
    {
        _game = game;
        _player = player;
    }

    public PlasmaResponse ServerHello(PlasmaPacket request)
    {
        var payload = BuildProbeResponse(request.Payload, out var explanation);
        if (request.Payload.Length >= 2)
        {
            var sequence = PlasmaIntegerCodec.ReadUInt16BigEndian(request.Payload);
            PlasmaIntegerCodec.WriteUInt16BigEndian(payload, unchecked((ushort)(sequence + 6)));
        }

        return new PlasmaResponse(PlasmaCommandKind.ServerHello, payload, explanation);
    }

    private static byte[] BuildProbeResponse(ReadOnlySpan<byte> request, out string explanation)
    {
        foreach (var (probe, response) in ProbeExactResponses)
        {
            if (request.SequenceEqual(probe))
            {
                explanation = "20-byte server hello exact PCAP probe response";
                return response.ToArray();
            }
        }

        foreach (var (prefix, response) in ProbePrefixResponses)
        {
            if (request.StartsWith(prefix))
            {
                var payload = response.ToArray();
                ApplyTailXor(request, payload, TryFindTailXor(prefix));
                explanation = $"20-byte server hello PCAP prefix response prefix={Convert.ToHexString(prefix).ToLowerInvariant()}";
                return payload;
            }
        }

        var fallback = DefaultProbeResponse.ToArray();
        ApplyTailXor(request, fallback, DefaultProbeTailXor);
        explanation = "20-byte server hello fallback quick-match response shape for unknown probe prefix";
        return fallback;
    }

    private static byte[]? TryFindTailXor(ReadOnlySpan<byte> prefix)
    {
        foreach (var (knownPrefix, tailXor) in ProbeTailXors)
        {
            if (prefix.SequenceEqual(knownPrefix))
            {
                return tailXor;
            }
        }

        return null;
    }

    private static void ApplyTailXor(ReadOnlySpan<byte> request, Span<byte> response, ReadOnlySpan<byte> tailXor)
    {
        if (tailXor.IsEmpty || request.Length < 20 || response.Length < 20)
        {
            return;
        }

        for (var index = 0; index < 4; index++)
        {
            response[16 + index] = (byte)(request[16 + index] ^ tailXor[index]);
        }
    }

    public PlasmaResponse ReservationGranted(int tid)
    {
        return Text(
            PlasmaCommandKind.ReservationGranted,
            GameManagerMessageBuilder.Notify("EGRS", "0x40000000")
                .Field("LID", _game.LocalId)
                .Field("GID", _game.GameId)
                .Field("ALLOWED", 1)
                .Field("PID", _player.PlayerId)
                .Field("TID", tid),
            "reservation accepted");
    }

    public PlasmaResponse PlayerEntered(int tid)
    {
        return Text(
            PlasmaCommandKind.PlayerEntered,
            GameManagerMessageBuilder.Notify("PENT", "0x40000000")
                .Field("LID", _game.LocalId)
                .Field("GID", _game.GameId)
                .Field("PID", _player.PlayerId)
                .Field("TID", tid),
            "player entered roster");
    }

    public PlasmaResponse Roster(int tid)
    {
        return Text(
            PlasmaCommandKind.Roster,
            GameManagerMessageBuilder.Notify("COc")
                .Field("LID", _game.LocalId)
                .Field("GID", _game.GameId)
                .Field("PID", _player.PlayerId)
                .Field("NAME", _player.Name)
                .Field("STATE", 4)
                .Field("TID", tid),
            "single-player roster element");
    }

    public PlasmaResponse MeshUpdate(int tid, bool joined)
    {
        var join = joined ? 1 : 0;
        return Text(
            PlasmaCommandKind.MeshUpdate,
            GameManagerMessageBuilder.Notify("UBRA", "0x40000000")
                .Field("LID", _game.LocalId)
                .Field("GID", _game.GameId)
                .Field("JOIN", join)
                .Field("START", 0)
                .Field("TID", tid),
            "mesh membership update");
    }

    public PlasmaResponse JoinAnnouncement(int tid)
    {
        return Text(
            PlasmaCommandKind.JoinAnnouncement,
            GameManagerMessageBuilder.Request("UGAM", "0x40000000")
                .Field("LID", _game.LocalId)
                .Field("GID", _game.GameId)
                .Field("JOIN", 0)
                .Field("MAX-PLAYERS", _game.MaxPlayers)
                .Field("B-maxObservers", 0)
                .Field("B-numObservers", 0)
                .Field("TID", tid),
            "game join announcement");
    }

    public PlasmaResponse JoinComplete(int tid)
    {
        return Text(
            PlasmaCommandKind.JoinComplete,
            GameManagerMessageBuilder.Notify("UBRA", "0x40000000")
                .Field("LID", _game.LocalId)
                .Field("GID", _game.GameId)
                .Field("START", 1)
                .Field("TID", tid),
            "join complete/source handoff ready");
    }

    public PlasmaResponse Ack(PlasmaCommandKind kind, int tid, string name)
    {
        return Text(
            kind,
            GameManagerMessageBuilder.Notify(name)
                .Field("PID", _player.PlayerId)
                .Field("TID", tid),
            $"{name} ack");
    }

    public PlasmaResponse NativeRosterHeader(short elementCount)
    {
        return new PlasmaResponse(
            PlasmaCommandKind.Roster,
            NativeGameManagerMessage.RosterHeader(elementCount),
            "native roster header type 2");
    }

    public PlasmaResponse NativeRosterElement()
    {
        return new PlasmaResponse(
            PlasmaCommandKind.Roster,
            NativeGameManagerMessage.RosterElement(),
            "native roster element type 3");
    }

    public PlasmaResponse NativeRosterAckToHost(int hostPlayerId)
    {
        return new PlasmaResponse(
            PlasmaCommandKind.RosterAck,
            NativeGameManagerMessage.RosterAckToHost(hostPlayerId),
            "native roster ack to host type 4");
    }

    public PlasmaResponse NativeJoinAnnouncement()
    {
        return new PlasmaResponse(
            PlasmaCommandKind.JoinAnnouncement,
            NativeGameManagerMessage.JoinAnnouncement(),
            "native join announcement type 5");
    }

    public PlasmaResponse NativeJoinMeshAnnouncement()
    {
        return new PlasmaResponse(
            PlasmaCommandKind.MeshUpdate,
            NativeGameManagerMessage.JoinMeshAnnouncement(),
            "native join mesh announcement type 8");
    }

    public PlasmaResponse NativeAddressedJoinDetails(int targetPlayerId)
    {
        return new PlasmaResponse(
            PlasmaCommandKind.MeshUpdate,
            NativeGameManagerMessage.AddressedJoinDetails(targetPlayerId),
            "native addressed join details type 9");
    }

    public PlasmaResponse NativePeerMeshToHost(int hostPlayerId)
    {
        return new PlasmaResponse(
            PlasmaCommandKind.MeshUpdate,
            NativeGameManagerMessage.PeerMeshToHost(hostPlayerId),
            "native peer mesh to host type 11");
    }

    private static PlasmaResponse Text(PlasmaCommandKind kind, GameManagerMessageBuilder builder, string explanation)
    {
        return new PlasmaResponse(kind, builder.BuildBytes(), explanation);
    }
}
