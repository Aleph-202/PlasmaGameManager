namespace PlasmaGameManager.Server;

public static class SourceBackendPayloadAdapter
{
    public static SourceBackendPayloadDecision PrepareClientToBackend(
        SourceBackendProtocol protocol,
        ReadOnlyMemory<byte> payload)
    {
        return protocol switch
        {
            SourceBackendProtocol.Ps3NativePassthrough => SourceBackendPayloadDecision.Forward(payload, "PS3-native Source/gameplay payload passed through unchanged."),
            SourceBackendProtocol.Ps3NativeGenerated =>
                SourceBackendPayloadDecision.Drop("PS3-native generated Source mode consumes client packets inside PlasmaGameManager instead of forwarding them to a backend."),
            SourceBackendProtocol.PcSourceConnectionlessOnly when IsClassicSourceConnectionless(payload.Span) =>
                SourceBackendPayloadDecision.Forward(payload, "Classic connectionless Source packet forwarded to PC Source backend."),
            SourceBackendProtocol.PcSourceConnectionlessOnly =>
                SourceBackendPayloadDecision.Drop("PS3 Source/gameplay transport packet is not a classic PC Source connectionless packet; translation is required before forwarding to a PC Source backend."),
            _ => SourceBackendPayloadDecision.Drop($"Unsupported Source backend protocol: {protocol}")
        };
    }

    public static SourceBackendPayloadDecision PrepareBackendToClient(
        SourceBackendProtocol protocol,
        ReadOnlyMemory<byte> payload)
    {
        return protocol switch
        {
            SourceBackendProtocol.Ps3NativePassthrough when IsClassicSourceServerEnvelope(payload.Span) =>
                SourceBackendPayloadDecision.Drop("Classic PC Source backend envelope is not valid PS3-native Source/gameplay traffic."),
            SourceBackendProtocol.Ps3NativePassthrough =>
                SourceBackendPayloadDecision.Forward(payload, "Source backend packet forwarded on PS3-visible GameManager/game-server flow."),
            SourceBackendProtocol.Ps3NativeGenerated =>
                SourceBackendPayloadDecision.Drop("PS3-native generated Source mode does not accept backend datagrams."),
            SourceBackendProtocol.PcSourceConnectionlessOnly =>
                SourceBackendPayloadDecision.Forward(payload, "Classic PC Source backend packet forwarded unchanged."),
            _ => SourceBackendPayloadDecision.Drop($"Unsupported Source backend protocol: {protocol}")
        };
    }

    public static bool IsClassicSourceConnectionless(ReadOnlySpan<byte> payload)
    {
        return payload.Length >= 4
            && payload[0] == 0xff
            && payload[1] == 0xff
            && payload[2] == 0xff
            && payload[3] == 0xff;
    }

    public static bool IsClassicSourceServerEnvelope(ReadOnlySpan<byte> payload)
    {
        return IsClassicSourceSplitOrConnectionlessEnvelope(payload)
            || IsClassicSourceNetchanEnvelope(payload);
    }

    public static bool IsClassicSourceSplitOrConnectionlessEnvelope(ReadOnlySpan<byte> payload)
    {
        return payload.Length >= 4
            && payload[1] == 0xff
            && payload[2] == 0xff
            && payload[3] == 0xff
            && payload[0] is 0xff or 0xfe or 0xfd;
    }

    public static bool IsClassicSourceNetchanEnvelope(ReadOnlySpan<byte> payload)
    {
        return payload.Length >= 8
            && payload[1] == 0x00
            && payload[2] == 0x00
            && payload[3] == 0x00
            && payload[4] == 0x00
            && payload[5] == 0x00
            && payload[6] == 0x00
            && payload[7] == 0x00
            && payload[0] != 0x00;
    }
}

public sealed record SourceBackendPayloadDecision(
    bool ShouldForward,
    ReadOnlyMemory<byte> Payload,
    string Explanation)
{
    public static SourceBackendPayloadDecision Forward(ReadOnlyMemory<byte> payload, string explanation)
    {
        return new SourceBackendPayloadDecision(true, payload, explanation);
    }

    public static SourceBackendPayloadDecision Drop(string explanation)
    {
        return new SourceBackendPayloadDecision(false, ReadOnlyMemory<byte>.Empty, explanation);
    }
}

public sealed record SourceBackendForwardResult(
    bool Forwarded,
    bool Dropped,
    string Explanation)
{
    public static SourceBackendForwardResult Disabled { get; } = new(false, false, "Source backend proxy is disabled.");
}
