using System.Text.Json;

namespace PlasmaGameManager.Server;

public interface IGameManagerEventSink : IDisposable
{
    void Record(GameManagerServerEvent gameEvent);
}

public sealed record GameManagerServerEvent(
    DateTimeOffset Timestamp,
    string Profile,
    string Event,
    string Endpoint,
    string Kind,
    string StateBefore,
    string StateAfter,
    int PayloadLength,
    string Explanation,
    string HexPrefix,
    string CommandName = "",
    int? TransactionId = null,
    long? LocalId = null,
    long? GameId = null,
    int? PlayerId = null,
    IReadOnlyDictionary<string, string>? Fields = null,
    int? SourceSequence = null,
    int? SourceBodyLength = null,
    int? SourceSequenceDelta = null,
    string? SourcePacketShape = null,
    string? SourceNativeFrameKind = null,
    bool? SourceFitsInlineQueue = null,
    bool? SourceFitsNativeQueue = null,
    string? SourceFragmentHeaderHex = null,
    string? SourcePayloadSemanticKind = null,
    string? SourcePayloadSemanticRole = null,
    string? SourceCompactControlFamily = null,
    int? SourceCompactControlPrefixLength = null,
    string? SourceCompactControlPrefixHex = null,
    string? SourceMarkerlessPayloadFamily = null,
    string? SourceMarkerlessPayloadPrefixHex = null,
    string? SourceMarkerlessPayloadSuffixHex = null,
    double? SourceMarkerlessPayloadEntropy = null,
    double? SourceMarkerlessPayloadPrintableRatio = null,
    string[]? SourceEmbeddedMarkers = null,
    string[]? SourceEmbeddedRecordRoles = null,
    string[]? SourceEmbeddedObjectIds = null,
    string[]? SourceEmbeddedClassIds = null,
    string[]? SourceEmbeddedObjectLinks = null,
    string[]? SourceEmbeddedDisplayNames = null,
    string[]? SourceEmbeddedRecordSummaries = null,
    string[]? SourceEmbeddedObjectIdOrder = null,
    string[]? SourceEmbeddedClassIdOrder = null,
    string[]? SourceEmbeddedObjectLinkOrder = null,
    string[]? SourceEmbeddedDisplayNameOrder = null,
    int? SourceEmbeddedPrefixLength = null,
    string? SourceEmbeddedPrefixHex = null,
    string? SourceEmbeddedPrefixFamily = null,
    double? SourceEmbeddedPrefixEntropy = null,
    double? SourceEmbeddedPrefixPrintableRatio = null,
    string? SourceEmbeddedPrefixMeaning = null,
    int? SourceDirectionPacketCount = null,
    bool? SourceSequenceDecrease = null,
    string? SourceBodySignature = null,
    string? SourceClientPayloadRole = null,
    string? SourceClientBodyPrefixHex = null,
    int? SourceClientFirstUInt16BigEndian = null,
    int? SourceClientFirstUInt16LittleEndian = null,
    int? SourceClientLastUInt16BigEndian = null,
    int? SourceClientReliableAssociationMessageType = null,
    long? SourceClientReliableAssociationNativeToken = null,
    long? SourceClientReliableAssociationTokenBigEndian = null,
    long? SourceClientReliableAssociationTokenLittleEndian = null,
    int? SourceClientAttachedFrameKind = null,
    int? SourceClientAttachedFrameDeclaredLength = null,
    long? SourceClientAttachedFrameNativeToken = null,
    long? SourceClientAttachedFrameTokenBigEndian = null,
    long? SourceClientAttachedFrameTokenLittleEndian = null,
    int? SourceClientBitSidecarOffset = null,
    int? SourceClientBitSidecarBitCount = null,
    int? SourceClientBitSidecarPayloadLength = null,
    string? SourceClientPayloadObjectFrameKind = null,
    long? SourceClientPayloadObjectHeaderValue = null,
    int? SourceClientPayloadObjectSignedHeaderValue = null,
    int? SourceClientPayloadObjectInnerPayloadOffset = null,
    int? SourceClientPayloadObjectInnerPayloadLength = null,
    int? SourceClientPayloadObjectBitreaderFieldOffset = null,
    long? SourceClientPayloadObjectAssociatedToken = null,
    int? SourceClientPayloadObjectFragmentIndex = null,
    int? SourceClientPayloadObjectFragmentTotalCount = null,
    long? SourceClientPayloadObjectFragmentPacketCounter = null,
    bool? SourceClientPayloadObjectFragmentWrappedOrCompressed = null,
    int? SourceClientDecodedNetMessageType = null,
    string? SourceClientDecodedNetMessageName = null,
    string? SourceClientDecodedNetMessagePayloadKind = null,
    int? SourceClientDecodedNetMessagePayloadOffset = null,
    int? SourceClientDecodedNetMessagePayloadLength = null,
    int? SourceClientDecodedNetMessagePayloadBitCount = null,
    bool? SourceClientCommandDecoded = null,
    int? SourceClientCommandForwardMove = null,
    int? SourceClientCommandSideMove = null,
    int? SourceClientCommandUpMove = null,
    float? SourceClientCommandYawDelta = null,
    float? SourceClientCommandPitchDelta = null,
    int? SourceClientCommandButtons = null,
    int? SourceClientCommandWeaponSlotHint = null,
    int? SourceClientCommandTeamHint = null,
    int? SourceClientCommandClassHint = null,
    int? NativeSourceClientPacketCount = null,
    int? NativeSourceServerPacketCount = null,
    bool? NativeSourceSentInitialSetup = null,
    int? NativeSourceInitialSetupVariant = null,
    bool? NativeSourceSentServerInfo = null,
    bool? NativeSourceSentObjectState = null,
    long? NativeSourceRootObjectId = null,
    long? NativeSourceLocalPlayerObjectId = null,
    int? NativeSourceObjectStateIntroBatchIndex = null,
    int? NativeSourceLoadingStateLinkHeartbeatIndex = null,
    bool? NativeSourceSentLoadingStateLinkBurst = null,
    int? NativeSourceLoadingStateLinkBurstClientPacketCount = null,
    bool? NativeSourceSentLoadingPostBurstContinuation = null,
    int? NativeSourceLoadingContinuationStage = null,
    int? NativeSourcePostRosterContinuationStage = null,
    int? NativeSourceSuppressLoadingContinuationResponses = null,
    bool? NativeSourceSentLoadingMotdEvent = null,
    bool? NativeSourceSentRosterDescriptorState = null,
    int? NativeSourceSteadyStateLinkHeartbeatIndex = null,
    int? NativeSourceLastCommandSnapshotClientPacketCount = null);

public sealed class JsonLineGameManagerEventSink : IGameManagerEventSink
{
    private readonly TextWriter _writer;
    private readonly object _gate = new();

    public JsonLineGameManagerEventSink(TextWriter writer)
    {
        _writer = writer;
    }

    public void Record(GameManagerServerEvent gameEvent)
    {
        var line = JsonSerializer.Serialize(gameEvent);
        lock (_gate)
        {
            _writer.WriteLine(line);
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
