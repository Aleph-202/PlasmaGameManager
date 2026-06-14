using System.Text;
using System.Security.Cryptography;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.Server;

public sealed class Ps3NativeSourceResponder
{
    public const string ImplementationLabel = "ps3-native-generated-sparse-snapshot-v22-live-sparse-mapload";
    private const int FragmentPayloadThresholdBytes = 1000;
    private const int QueuedPeerChunkPayloadBytes = 1000;
    private const int SteadyCommandSnapshotClientPacketInterval = 16;
    private const int FastCommandSnapshotClientPacketInterval = 3;
    private const int MinimumCommandSnapshotClientPacketCount = 32;
    private const int FastCommandSnapshotMaxClientPacketCount = 66;
    private const int QuickMatchTerminalPrompt1TargetLength = 620;
    private const int QuickMatchTerminalPrompt2TargetLength = 564;
    public const ushort SparseQuickMatchLateLoadingSequence = 1000;
    private const ushort SparseQuickMatchRosterHandoffSequence = 1114;
    public const int FirstCommandSnapshotClientPacketCount = MinimumCommandSnapshotClientPacketCount;
    public const int CommandSnapshotClientPacketInterval = SteadyCommandSnapshotClientPacketInterval;
    private static readonly int[] ObjectStateIntroTurnBatchCounts = [3, 4, 2, 1];
    private static readonly int[] Variant2ObjectStateIntroTurnBatchCounts = [5, 1, 1, 1, 1, 1];
    private static readonly int[] Variant3ObjectStateIntroTurnBatchCounts = [3, 3, 2, 1, 1, 1, 1, 1];
    private static readonly int[] Variant4ObjectStateIntroTurnBatchCounts = [1, 3, 2, 1, 1, 1];
    private static readonly int[] Variant5ObjectStateIntroTurnBatchCounts = [4, 2, 6, 2, 2];
    private static readonly LoadingContinuationFrame[][] LoadingContinuationStages =
    [
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 196, 24, 8),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1128),
            new(LoadingContinuationFrameKind.NativeSnapshot, 846)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 982)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 704)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 982),
            new(LoadingContinuationFrameKind.NativeSnapshot, 944),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 944)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 714),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1184),
            new(LoadingContinuationFrameKind.NativeSnapshot, 282),
            new(LoadingContinuationFrameKind.NativeSnapshot, 460)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 42, 10, 2),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1198),
            new(LoadingContinuationFrameKind.NativeSnapshot, 564)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 450)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 1138),
            new(LoadingContinuationFrameKind.NativeSnapshot, 338)
        ],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 450),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1180),
            new(LoadingContinuationFrameKind.NativeSnapshot, 634)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 676)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 1079),
            new(LoadingContinuationFrameKind.NativeSnapshot, 958),
            new(LoadingContinuationFrameKind.NativeSnapshot, 450)
        ],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 28),
            new(LoadingContinuationFrameKind.NativeSnapshot, 686),
            new(LoadingContinuationFrameKind.NativeSnapshot, 846)
        ],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 704),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1156)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 574)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 196),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1156),
            new(LoadingContinuationFrameKind.NativeSnapshot, 620)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 206, 14, 8),
            new(LoadingContinuationFrameKind.NativeSnapshot, 282),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1111),
            new(LoadingContinuationFrameKind.NativeSnapshot, 300)
        ],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 638),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1128),
            new(LoadingContinuationFrameKind.NativeSnapshot, 338)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 168, 24, 7),
            new(LoadingContinuationFrameKind.NativeSnapshot, 874)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 1222)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 1212)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 206),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1212),
            new(LoadingContinuationFrameKind.NativeSnapshot, 296)
        ],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 944),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 66),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1128),
            new(LoadingContinuationFrameKind.NativeSnapshot, 338)
        ],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 450),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1138),
            new(LoadingContinuationFrameKind.NativeSnapshot, 846)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 182, 14, 7),
            new(LoadingContinuationFrameKind.NativeSnapshot, 592),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1142),
            new(LoadingContinuationFrameKind.NativeSnapshot, 602)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 514)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 196),
            new(LoadingContinuationFrameKind.PlayerStateLink, 220, 14, 8),
            new(LoadingContinuationFrameKind.PlayerStateLink, 168, 24, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 206)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 196)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 210, 14, 8)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 220),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 196, 24, 8),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 28)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 206)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 52, 16, 3)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4),
            new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 10)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 28)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 10)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4, SuppressAfter: 1)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 24, 4, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 48)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 28)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 10),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21),
            new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 24),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 28)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5, SuppressAfter: 1)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 66, 18, 4, SuppressAfter: 1)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 28),
            new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 10)
        ],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21),
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 91, 13, 6)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 52, 16, 3)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21, SuppressAfter: 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 91, 13, 6)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 24, 4, 2)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 49)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 24, 4, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2, SuppressAfter: 1)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 52, 16, 3)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 31)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2, SuppressAfter: 1)
        ],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 48),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 94, 14, 7, SuppressAfter: 1)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4),
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 49)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2, SuppressAfter: 1)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 48),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 10)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 91, 13, 6),
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2)
        ],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 66, 18, 4),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 21, SuppressAfter: 1)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 91, 13, 6)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 38),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1149),
            new(LoadingContinuationFrameKind.NativeSnapshot, 846)
        ],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 436),
            new(LoadingContinuationFrameKind.NativeSnapshot, 324),
            new(LoadingContinuationFrameKind.NativeSnapshot, 1152),
            new(LoadingContinuationFrameKind.NativeSnapshot, 479)
        ],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 196),
            new(LoadingContinuationFrameKind.NativeSnapshot, 216),
            new(LoadingContinuationFrameKind.PlayerStateLink, 182, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 84, 12, 6)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 685)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 579)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 564)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 602),
            new(LoadingContinuationFrameKind.NativeSnapshot, 606),
            new(LoadingContinuationFrameKind.NativeSnapshot, 860)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 320)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 564)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 606)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 602),
            new(LoadingContinuationFrameKind.NativeSnapshot, 860)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 574)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 578),
            new(LoadingContinuationFrameKind.NativeSnapshot, 620),
            new(LoadingContinuationFrameKind.NativeSnapshot, 588)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 592)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 564)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 588)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 411),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)
        ],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 66),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 84)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 122, 10, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 84, 12, 6),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)
        ],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 66),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 112, 14, 8),
            new(LoadingContinuationFrameKind.PlayerStateLink, 80, 8, 6)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56),
            new(LoadingContinuationFrameKind.PlayerStateLink, 122, 10, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 80, 8, 6)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 122, 10, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 66)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 112, 14, 8),
            new(LoadingContinuationFrameKind.PlayerStateLink, 108, 12, 8),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 66)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 126, 14, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 66)
        ],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 84),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 122, 10, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 94)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 94, 14, 7)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56),
            new(LoadingContinuationFrameKind.PlayerStateLink, 108, 12, 8)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 84, 12, 6),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 66, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 84, 12, 6),
            new(LoadingContinuationFrameKind.PlayerStateLink, 136, 12, 10),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 66)],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 136, 12, 10),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)
        ],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56),
            new(LoadingContinuationFrameKind.PlayerStateLink, 94, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 112, 14, 8),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 94)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)],
        [
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56),
            new(LoadingContinuationFrameKind.PlayerStateLink, 274, 18, 18),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 94)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 126, 14, 9),
            new(LoadingContinuationFrameKind.NativeQueuedBoundary, 66)
        ],
        [new(LoadingContinuationFrameKind.NativeQueuedBoundary, 56)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 609)],
        [
            new(LoadingContinuationFrameKind.NativeSnapshot, 606),
            new(LoadingContinuationFrameKind.NativeSnapshot, 606)
        ],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 574)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 564)],
        [new(LoadingContinuationFrameKind.NativeSnapshot, 860, SuppressAfter: 1)]
    ];
    private static readonly LoadingContinuationFrame[] Variant2LoadingBurstFrames = ParseLoadingFrameLine("1098H;846H");
    private static readonly LoadingContinuationFrame[] Variant2LoadingPostBurstFrames = ParseLoadingFrameLine("704H;954L;28M");
    private static readonly LoadingContinuationFrame[] Variant3LoadingBurstFrames = ParseLoadingFrameLine("327L;1081H;846H");
    private static readonly LoadingContinuationFrame[] Variant3LoadingPostBurstFrames = ParseLoadingFrameLine("958H;1166L;366L");
    private static readonly LoadingContinuationFrame[] Variant4LoadingBurstFrames = ParseLoadingFrameLine("1129L");
    private static readonly LoadingContinuationFrame[] Variant4LoadingPostBurstFrames = ParseLoadingFrameLine("846H;450H");
    private static readonly LoadingContinuationFrame[][] Variant2LoadingContinuationStages = ParseLoadingContinuationStages(
        """
        436L;564H
        714H
        1212H
        718L;1222L;282H
        450L;28M;1194H;282H
        704H;1142L;634L
        460L;282H
        1212H;968H
        464L
        690L;578L
        915L;1108H;282H
        206H;1170L;578L
        690L;66M;1142L;592H
        450H
        1138H;338H
        450L;270L;1156L;307L
        874H
        1212H
        1236L;168L;84M;1236L
        196M;1128H
        846H
        460L;296L;1170L;592H
        714H;1128H;592H
        968H;1156L;394L
        906H;206H
        196M;220L;182L;70L;196M;178M
        196M
        206L;70L;210L;196M
        150M
        24L
        80L
        56L;49L;21M
        59M;84L
        10M;28L;77L
        42L
        52L;28L
        35L
        91L
        52L
        38L
        77L;35L
        21M
        52L;56L
        10M
        28L
        63L;21M;28M
        24L;70L
        35L
        35L;56L
        38M
        38L
        63L
        21M
        52L
        28L;10M
        28L
        77L;21M;21M
        42L;56L
        35L;63L
        28L
        38M
        52L;28L
        77L
        21M;21M
        48M
        28L;56L;10M
        84L;21M
        21M
        38M;70L
        21M
        91L
        38M
        94L
        63L
        63L;21M
        38M;70L
        24L
        70L
        21M
        21M;63L;28L
        10M
        35L
        77L
        62L
        70L
        63L
        49L;21M
        38M;84L
        10M
        28L
        84L
        21M;66L;56L
        10M;28L;77L
        21M
        10M;66L
        10M
        63L;63L
        21M
        10M;52L;28L
        10M
        28L;77L;21M;21M
        73L
        38L
        35L;1163L
        874L
        718L;1138H;479H
        196M
        216L;28M;182L;70L
        182M
        75M
        70L
        345L;616L;846H
        574H
        592L;606L;588L
        846H
        564H;630L
        592L
        564H;856H
        578L;376L
        578L;564H
        574H
        802L;84L;66M;56M
        56M
        150L;84L
        66M;56M
        70L;164L
        56M
        56M;66M
        126L;70L;94M
        56M;80L;112L
        70L
        94M;70L;70L;122L;56M
        94M
        70L;112L;80L;56M
        70L
        108L
        112L;56M;66M
        70L
        150L;70L;56M
        66M;70L;140L;80L;56M
        70L
        66M
        126L
        94M;56M
        84L
        122L;56M;84M
        66M
        84L;112L
        82M
        56M;108L
        112L;70L
        66M
        56M
        627L
        630L
        564H
        564H
        588L
        874L
        """);
    private static readonly LoadingContinuationFrame[][] Variant3LoadingContinuationStages = ParseLoadingContinuationStages(
        """
        690L;564H
        982L
        1184H;282H
        214L;1208L;578L
        704L;42L;1128H;602H
        196M;210L
        192L;592L;1128H;592H
        1222H
        704H;182L;80L
        877L;206H
        1212H;206H;1226L
        206L;324L;1128H;846H
        704H
        1138H;592H
        678L;310L;1170L
        324H
        1150H;958H
        220L;1198L;578L
        714L;28M;1128H;592H
        460H;1142L;860L
        436L;592L
        714H;958H
        968H;1212L;310L;182L
        56M;1166H
        196M;220L;182L;98L;168M
        206M
        196M;220L;168L;112L
        112M;10M
        80L
        21M
        49L
        80L
        38M
        56L
        10M
        49L;91L
        59M;10M
        56L;21M
        21M
        80L;28L;38M
        10M;28L;70L;21M
        10M;70L
        28L;38M
        52L;56L
        21M
        77L
        38M
        80L;28L
        77L;49L
        62L
        28L
        28L;31M
        21M
        28L;66L
        10M
        56L;21M
        71L
        46L
        38M;10M;28L
        35L;49L
        63L
        38M
        28L
        66L
        21M;21M;63L
        63L;59M
        10M;56L
        21M
        28L;56L;38M
        24L;28L
        21M
        63L
        63L
        38M
        52L
        98L
        49M
        10M
        21M
        70L
        38M
        10M
        56L
        21M
        56L;28L;38M
        38L;28L;28L
        63L;63L
        28M
        56L;28L
        10M
        21M
        49L
        28L;38L
        38M;70L
        21M
        35L;70L;38M
        10M
        21M;105L
        10M
        38M
        28L;56L;10M
        21M
        35L
        56L;48M
        35L;1163L
        282H;944L
        66M;1156H;310H
        196M
        615L;182L;70L
        220L;206H
        196M
        445L
        917L;574H
        564H
        324L
        630L;860L;564H
        320H
        592L;620L;574H
        846H
        574H
        606L;606L;574H
        592H
        578L;630L;145L
        56M
        66M;112L;122L;56M
        56M
        66M
        140L
        84L;66M;56M
        84L
        136L
        70L;66M;56M
        98L
        108L
        84M;56M
        66M
        140L
        56M;94M;56M
        108L
        98L
        56M;94M;56M
        140L
        66M;56M;66M
        98L
        126L
        66M
        56M
        56M;150L
        84L
        66M;62M
        70L;136L
        84M
        56M;66M;84L
        112L
        94M
        56M;80L;98L
        84L
        94M;72M;84L;122L;56M
        56M
        122L
        345L
        602L
        564H
        """);
    private static readonly LoadingContinuationFrame[][] Variant4LoadingContinuationStages = ParseLoadingContinuationStages(
        """
        460L;296L;1156L;620L
        982L
        958H
        1212H;446L;84L
        958L
        220L;81M;1156H;310H
        210L;728L;28L
        1170L;899L
        499L
        196M;958L;324L;1222L
        67L
        1226L
        245L;1030H;846H
        422L;376L;1142L;606L
        475L;282H;1138H;592H
        704L;282H;916L;108L
        958L;296L
        1152H;1123L
        196L;70L;1180L
        564H
        690L;28M;1212H
        728L;1184L;620L
        714L;282H
        972L
        1236L;1226L
        676L;606L
        206L
        1156H;564H
        643L;182L;84L;196L;28M
        206L;28M;196M;206L;28M
        168L;98L;196L
        206H;210L;112L;38L
        10M
        70L;35L
        84L
        56L;28L
        91L
        38L
        80L
        66L
        56L;63L
        66L
        70L
        38L;70L;56L
        21M
        35L;122L;28L
        66L
        63L
        63L
        104L;70L
        28L
        77L
        63L
        94L
        52L
        84L;63L
        21M
        73L
        84L
        10M;70L
        28L
        63L
        21M
        52L
        94L
        28L;49L
        91L
        49L;10M
        28L
        122L;10M
        28L
        91L
        49L;21M
        108L
        70L
        35L
        35L
        10M;108L
        56L
        28L
        80L;28L
        35L
        21M
        94L;80L
        105L;63L
        35L
        10M;70L
        24L;70L;63L
        35L
        38L;108L
        66L
        28L;63L
        21M
        122L
        38L
        84L
        21M
        66L
        56L
        38L;91L
        77L
        10M;56L;94L
        38L;28L;70L
        21M
        77L
        94L
        38L
        35L
        21M
        94L;38L
        70L;56L
        21M;35L
        136L
        24L
        56L;91L
        35L
        10M;98L;66L
        52L;56L
        49L
        1170L
        1222H;944L;338L
        196L;42L
        777L;210L
        206H;178L;126L
        463L
        603L;564H
        912L
        620L
        578L
        588L;578L
        662L
        602L;564H
        602L
        662L;348L
        578L
        564H
        616L
        634L;620L;574H
        592L
        126L;602L
        860L
        574H
        140L;140L
        66M;98L;70L;108L
        126L
        70L
        66M;140L;136L
        70L;66M
        140L;126L
        80L;56M;98L;136L;112L
        66M
        56M
        126L
        122L;98L;56M
        80L
        244L;98L
        108L;56M
        150L;70L;98L
        100M
        70L
        401L
        588L
        592L
        610L
        888L
        620L
        """);
    private static readonly (int Length, int PrefixLength, int MaxRecords)[] LoadingStateLinkHeartbeatShapes =
    [
        (21, 9, 1),
        (28, 4, 2),
        (38, 14, 2),
        (84, 12, 6),
        (63, 15, 4),
        (105, 9, 8)
    ];
    private static readonly (int Length, int PrefixLength, int MaxRecords)[] SteadyStateLinkHeartbeatShapes =
    [
        (21, 9, 1),
        (84, 12, 6),
        (56, 8, 4),
        (38, 14, 2),
        (105, 9, 8),
        (70, 10, 5),
        (28, 4, 2),
        (66, 18, 4),
        (98, 14, 7)
    ];
    private static readonly LoadingContinuationFrame[][] SteadyStateDeltaStages = ParseLoadingContinuationStages("""
        98L;84L;66M
        56M
        84L;136L;70L
        66M
        56M
        98L;136L;56M
        56M;94L;112L;94M
        56M
        56M;274L;98L;56M;94M
        70L
        126L;66M
        56M
        609H
        606L;606L
        574H
        564H
        56M
        66M;140L;136L
        70L;66M
        140L;126L
        80L;56M;98L;136L;112L
        66M
        56M
        126L
        122L;98L;56M
        80L
        244L;98L
        108L;56M
        150L;70L;98L
        100M
        70L
        401L
        588L
        592L
        610L
        888L
        620L
        """);

    public IReadOnlyList<Ps3NativeSourceResponse> BuildResponses(
        GameManagerSession game,
        PlayerSession player,
        ReadOnlySpan<byte> clientPayload)
    {
        if (SourceBackendPayloadAdapter.IsClassicSourceConnectionless(clientPayload)
            || !Ps3SourceTransportPacket.TryDecode(clientPayload, out var clientPacket))
        {
            return [];
        }

        var state = player.NativeSourceResponder;
        state.ClientPacketCount++;
        player.SourceState.ObserveClientSourcePacket(clientPacket.CandidateSequence, clientPacket.Body, state.ClientPacketCount);
        var clientBodyForHandling = clientPacket.Body.AsSpan();
        var waitingForMoreClientFragments = false;
        if (TryHandleClientFragment(state, clientPacket.Body, out var reassembledClientBody, out waitingForMoreClientFragments))
        {
            clientBodyForHandling = reassembledClientBody;
            player.SourceState.ObserveClientSourcePacket(clientPacket.CandidateSequence, clientBodyForHandling, state.ClientPacketCount);
        }

        game.ApplyDecodedNativeSourceClientState(player);
        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        if (!state.IsSeeded)
        {
            state.NextServerSequence = (ushort)(clientPacket.CandidateSequence + 5);
            state.IsSeeded = true;
        }
        else
        {
            if (!ShouldPreserveLateLargeCommandPreambleSequence(state, clientPacket.PayloadLength)
                && !ShouldPreserveQuickMatchTerminalPromptSequence(state, clientPacket.PayloadLength)
                && !ShouldPreservePostRosterContinuationSequence(state, player, clientPacket)
                && !ShouldPreservePostTerminalMapLoadStateAckSequence(state, clientPacket.PayloadLength))
            {
                AlignServerSequenceToClient(state, clientPacket.CandidateSequence);
            }
        }

        var responses = new List<Ps3NativeSourceResponse>(10);
        var clientHasEmbeddedObjectState = HasEmbeddedObjectState(clientBodyForHandling);
        if (clientHasEmbeddedObjectState && state.InitialSetupVariant == 1 && !state.SentObjectState)
        {
            state.SawInitialClientFrozenStateUpload = true;
            state.PendingInitialClientFrozenStateUpload = true;
        }

        if (!state.SentInitialSetup)
        {
            state.InitialSetupVariant = InitialSetupVariant(clientPacket.Body);
            AddInitialSetupResponses(responses, state, game, player, clientPacket.CandidateSequence);
            state.SentInitialSetup = true;
            if (state.InitialSetupContinuationStage > 0)
            {
                return responses;
            }
        }

        if (state.InitialSetupContinuationStage > 0)
        {
            responses.Clear();
            if (state.SuppressInitialSetupContinuationResponses > 0)
            {
                state.SuppressInitialSetupContinuationResponses--;
                return responses;
            }

            AddInitialSetupContinuationResponses(responses, state, game, player);
            return responses;
        }

        if (!state.SentServerInfo && state.ClientPacketCount >= 2)
        {
            if (state.InitialSetupVariant == 1)
            {
                AddPacket(
                    responses,
                    state,
                    BuildQuickMatchSetupContinuationBody(game, player, state),
                    "generated PS3 Source native quick-match setup continuation");
                state.SentQuickMatchSetupContinuation = true;
                state.SentServerInfo = true;
                return responses;
            }

            AddCriticalSourceObjectStreamBootstrapResponses(
                responses,
                state,
                game,
                player,
                "generated PS3 Source native server-info/sign-on object-stream bootstrap batch");
            if (state.InitialSetupVariant == 4)
            {
                state.SuppressObjectStateIntroResponses = Math.Max(state.SuppressObjectStateIntroResponses, 1);
            }

            state.SentCriticalSourceNetMessageBootstrap = true;
            state.SentServerInfo = true;
            if (state.InitialSetupVariant != 1)
            {
                return responses;
            }
        }

        if (ShouldAcknowledgeReliableAssociation(state, player))
        {
            AddPacket(
                responses,
                state,
                BuildReliableAssociationAckBody(player.SourceState.LastClientSourceReliableAssociationNativeToken!.Value),
                "generated PS3 Source native reliable-association ack",
                sequenceAdvance: 1);
            state.LastAcknowledgedReliableAssociationToken = player.SourceState.LastClientSourceReliableAssociationNativeToken;
        }

        if (waitingForMoreClientFragments)
        {
            AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for client fragment");

            return responses;
        }

        if (state.InitialSetupVariant == 1
            && state.PendingInitialClientFrozenStateUpload
            && clientHasEmbeddedObjectState)
        {
            return responses;
        }

        var sentObjectIntroThisTurn = false;
        if (!state.SentObjectState && ShouldSendObjectState(state, player, clientHasEmbeddedObjectState))
        {
            if (state.SuppressObjectStateIntroResponses > 0)
            {
                state.SuppressObjectStateIntroResponses--;
                AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for object-intro pacing");
                return responses;
            }

            state.SentObjectState = true;
            state.PendingInitialClientFrozenStateUpload = false;
            AddNextObjectStateIntroTurn(responses, state, game, player);
            sentObjectIntroThisTurn = true;
        }
        else if (state.SentObjectState && !ObjectStateIntroComplete(state, game, player))
        {
            if (state.SuppressObjectStateIntroResponses > 0)
            {
                state.SuppressObjectStateIntroResponses--;
                AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for object-intro pacing");
                return responses;
            }

            AddNextObjectStateIntroTurn(responses, state, game, player);
            sentObjectIntroThisTurn = true;
        }
        else if (state.SentServerInfo && state.SentObjectState && !state.SentRosterDescriptorState)
        {
            responses.Insert(0, BuildPacket(state, BuildShortControlBody(clientPacket.CandidateSequence, state.ClientPacketCount), "generated PS3 Source short control/ack"));
        }

        if (!sentObjectIntroThisTurn
            && ObjectStateIntroComplete(state, game, player)
            && !state.SentRosterDescriptorState
            && !state.SentLoadingStateLinkBurst
            && state.ClientPacketCount >= 8)
        {
            responses.Clear();
            var burstFrames = LoadingBurstFramesFor(state.InitialSetupVariant);
            AddLoadingFrames(responses, state, game, player, burstFrames, 0, "generated PS3 Source native loading bulk burst", state.InitialSetupVariant);
            state.SuppressLoadingContinuationResponses = Math.Max(
                state.SuppressLoadingContinuationResponses,
                LoadingBurstSuppressAfter(state.InitialSetupVariant));
            state.SentLoadingStateLinkBurst = true;
            state.LoadingStateLinkBurstClientPacketCount = state.ClientPacketCount;
            return responses;
        }

        if (state.SentLoadingStateLinkBurst
            && !state.SentRosterDescriptorState
            && !state.SentLoadingPostBurstContinuation)
        {
            responses.Clear();
            if (ConsumeSilentLoadingContinuationResponse(state))
            {
                return responses;
            }

            if (state.SuppressLoadingContinuationResponses > 0)
            {
                state.SuppressLoadingContinuationResponses--;
                AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for loading pacing");
                return responses;
            }

            if (state.InitialSetupVariant == 1 && state.ClientPacketCount - state.LoadingStateLinkBurstClientPacketCount <= 1)
            {
                return responses;
            }

            var postBurstFrames = LoadingPostBurstFramesFor(state.InitialSetupVariant);
            AddLoadingFrames(responses, state, game, player, postBurstFrames, 1, "generated PS3 Source native loading post-burst continuation", state.InitialSetupVariant);
            state.SentLoadingPostBurstContinuation = true;
            state.LoadingContinuationStage = 1;
            state.SuppressLoadingContinuationResponses = Math.Max(
                state.SuppressLoadingContinuationResponses,
                LoadingPostBurstSuppressAfter(state.InitialSetupVariant));
            return responses;
        }

        var loadingContinuationStages = LoadingContinuationStagesFor(state.InitialSetupVariant);
        if (ShouldForceLiveLoadingHandoff(state, player, clientPacket.CandidateSequence))
        {
            responses.Clear();
            AddLateLoadingHandoffResponses(responses, state, game, player);
            return responses;
        }

        if (state.SentLoadingPostBurstContinuation
            && !state.SentRosterDescriptorState
            && state.LoadingContinuationStage is > 0
            && state.LoadingContinuationStage <= loadingContinuationStages.Length)
        {
            CatchUpLoadingContinuationStage(state, clientPacket.CandidateSequence, loadingContinuationStages.Length);
            if (ShouldForceLiveLoadingHandoff(state, player, clientPacket.CandidateSequence))
            {
                responses.Clear();
                AddLateLoadingHandoffResponses(responses, state, game, player);
                return responses;
            }

            responses.Clear();
            if (ConsumeSilentLoadingContinuationResponse(state))
            {
                return responses;
            }

            if (state.SuppressLoadingContinuationResponses > 0)
            {
                state.SuppressLoadingContinuationResponses--;
                AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for loading continuation pacing");
                return responses;
            }

            AddLoadingContinuationStage(responses, state, game, player, loadingContinuationStages);
            state.LoadingContinuationStage++;
            if (ShouldForceLiveLoadingHandoff(state, player, clientPacket.CandidateSequence))
            {
                responses.Clear();
                AddLateLoadingHandoffResponses(responses, state, game, player);
                return responses;
            }

            return responses;
        }

        if (state.InitialSetupVariant is 2 or 3 or 4
            && state.SentLoadingPostBurstContinuation
            && !state.SentRosterDescriptorState
            && state.LoadingContinuationStage > loadingContinuationStages.Length)
        {
            responses.Clear();
            AddLateLoadingHandoffResponses(responses, state, game, player);
            return responses;
        }

        if (state.SentLoadingPostBurstContinuation
            && !state.SentRosterDescriptorState
            && state.SuppressLoadingContinuationResponses != 0)
        {
            responses.Clear();
            if (ConsumeSilentLoadingContinuationResponse(state))
            {
                return responses;
            }

            state.SuppressLoadingContinuationResponses--;
            AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for loading continuation pacing");
            return responses;
        }

        if (!sentObjectIntroThisTurn
            && ObjectStateIntroComplete(state, game, player)
            && !state.SentRosterDescriptorState
            && state.ClientPacketCount % 8 == 0)
        {
            AddPacket(
                responses,
                state,
                BuildPlayerStateLinkSlotReplacementPayload(game, player, state, 30, 0x8f, 89, "pre-roster-state-link-heartbeat"),
                "generated PS3 Source native compact state-link heartbeat",
                sequenceAdvance: 3);
        }

        if (!sentObjectIntroThisTurn && ObjectStateIntroComplete(state, game, player) && !state.SentRosterDescriptorState)
        {
            AddLoadingStateLinkHeartbeat(responses, state, game, player);
        }

        if (!sentObjectIntroThisTurn
            && ObjectStateIntroComplete(state, game, player)
            && !state.SentRosterDescriptorState
            && !state.SentLoadingMotdEvent)
        {
            AddPacket(
                responses,
                state,
                BuildLoadingMotdEventBody(game, player),
                "generated PS3 Source native loading MOTD event",
                sequenceAdvance: 6,
                allowNativeWrap: true);
            state.SentLoadingMotdEvent = true;
        }

        if (!sentObjectIntroThisTurn && ObjectStateIntroComplete(state, game, player) && !state.SentRosterDescriptorState && state.ClientPacketCount >= 12)
        {
            AddPacket(responses, state, BuildPlayerObjectBody(game, player, 166, 18), "generated PS3 Source native COc player-object roster batch", sequenceAdvance: 6);
            AddPacket(responses, state, BuildPlayerDescriptorBody(game, player, 64, 16), "generated PS3 Source native DSC player-descriptor batch", sequenceAdvance: 4);
            state.SentRosterDescriptorState = true;
        }

        var sendCommandSnapshot = ShouldSendCommandSnapshot(state, player);
        if (state.SentRosterDescriptorState
            && ShouldStartPostRosterContinuation(state, player, clientPacket))
        {
            responses.Clear();
            AddPostRosterMapLoadContinuationResponse(responses, state, game, player);
            state.PostRosterContinuationStage = 2;
            state.QuickMatchTerminalPromptStage = 0;
            return responses;
        }

        if (state.SentRosterDescriptorState)
        {
            AddPendingSourceServerCommandEvents(responses, state, game, player);
        }

        if (state.SentRosterDescriptorState
            && ShouldSendSparseQuickMatchTerminalMapLoadAfterShortAck(state, player, clientPacket))
        {
            responses.Clear();
            AddQuickMatchTerminalObjectStreamBootstrapResponses(responses, state, game, player);
            state.SentQuickMatchTerminalMapLoad = true;
            state.SentLateLargeCommandContinuation = true;
            state.QuickMatchTerminalPromptStage = 3;
            state.QuickMatchTerminalPrompt1WaitClientPacketCount = 0;
            state.LateLargeCommandFollowupClientPacketCount = 0;
            state.PostTerminalMapLoadClientPacketCount = 0;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldCapturePostRosterFrozenStateUpload(state, clientPacket))
        {
            responses.Clear();
            CapturePostRosterFrozenStateUpload(player, clientPacket);
            state.PendingPostRosterFrozenStateUpload = true;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldTreatLateDirectUploadAsQuickMatchTerminalPrompt2(state, player, clientPacket))
        {
            responses.Clear();
            AddPacket(
                responses,
                state,
                BuildQuickMatchTerminalPrompt2Body(game, player),
                "generated PS3 Source native quick-match terminal upload prompt 2 for late direct upload",
                sequenceAdvance: 141);
            state.QuickMatchTerminalPromptStage = 2;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendPostRosterFrozenStateBatches(state, clientPacket))
        {
            responses.Clear();
            AddPostRosterFrozenStateBatches(responses, state, game, player);
            state.PendingPostRosterFrozenStateUpload = false;
            state.SentPostRosterFrozenStateBatches = true;
            state.WaitingForPostRosterFrozenStateContinuation = true;

            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendPostRosterFrozenStateContinuation(state, clientPacket))
        {
            responses.Clear();
            AddPostRosterFrozenStateContinuationResponses(responses, state, game, player);
            state.WaitingForPostRosterFrozenStateContinuation = false;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && state.PostRosterContinuationStage > 0
            && IsShortClientControl(clientPacket))
        {
            responses.Clear();
            AddPostRosterContinuationResponse(responses, state, game, player, clientPacket);
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendPostRosterMapLoadClientBatchAck(state, clientPacket))
        {
            responses.Clear();
            AddPacket(
                responses,
                state,
                BuildPostRosterClientObjectBatchAckBody(game, player),
                "generated PS3 Source native post-roster client object-batch snapshot ack",
                sequenceAdvance: 9);
            state.SentPostRosterMapLoadClientBatchAck = true;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendPostRosterShortGameplayAck(state, clientPacket))
        {
            responses.Clear();
            AddPacket(
                responses,
                state,
                BuildPostRosterShortGameplayAckBody(game, player),
                "generated PS3 Source native post-roster short gameplay ack",
                sequenceAdvance: 9);
            state.SentPostRosterShortGameplayAck = true;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendQuickMatchTerminalPrompt1(state, player, clientPacket.PayloadLength))
        {
            responses.Clear();
            AddPacket(
                responses,
                state,
                BuildQuickMatchTerminalPrompt1Body(game, player),
                "generated PS3 Source native quick-match terminal upload prompt 1",
                sequenceAdvance: 144);
            state.QuickMatchTerminalPromptStage = 1;
            state.QuickMatchTerminalPrompt1WaitClientPacketCount = 0;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendLateLargeCommandPreamble(state, player, clientPacket.PayloadLength))
        {
            responses.Clear();
            foreach (var batch in BuildLateLargeCommandPrepBatches(game, player, state))
            {
                AddPacket(
                    responses,
                    state,
                    batch.Body,
                    batch.Explanation,
                    sequenceAdvance: batch.SequenceAdvance);
            }

            AddPacket(
                responses,
                state,
                BuildLateLargeCommandPreambleBody(game, player, state),
                "generated PS3 Source native late large-command preamble state-link frame",
                sequenceAdvance: 1);
            state.SentLateLargeCommandPreamble = true;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendQuickMatchTerminalPrompt2(state, clientPacket.PayloadLength))
        {
            responses.Clear();
            AddPacket(
                responses,
                state,
                BuildQuickMatchTerminalPrompt2Body(game, player),
                "generated PS3 Source native quick-match terminal upload prompt 2",
                sequenceAdvance: 141);
            state.QuickMatchTerminalPromptStage = 2;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendQuickMatchTerminalMapLoad(state, clientPacket.PayloadLength))
        {
            responses.Clear();
            AddQuickMatchTerminalObjectStreamBootstrapResponses(responses, state, game, player);
            state.SentQuickMatchTerminalMapLoad = true;
            state.SentLateLargeCommandContinuation = true;
            state.QuickMatchTerminalPromptStage = 3;
            state.QuickMatchTerminalPrompt1WaitClientPacketCount = 0;
            state.LateLargeCommandFollowupClientPacketCount = 0;
            state.PostTerminalMapLoadClientPacketCount = 0;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldWaitForQuickMatchTerminalUpload(state, clientPacket.PayloadLength))
        {
            responses.Clear();
            if (state.QuickMatchTerminalPromptStage == 1)
            {
                state.QuickMatchTerminalPrompt1WaitClientPacketCount++;
            }

            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendLateLargeCommandOpaqueContinuation(state, player, clientPacket.PayloadLength))
        {
            responses.Clear();
            AddObjectStreamBootstrapResponses(
                responses,
                state,
                game,
                player,
                "generated PS3 Source native late large-command object-stream fallback batch");
            state.SentQuickMatchTerminalMapLoad = true;
            state.SentLateLargeCommandContinuation = true;
            state.QuickMatchTerminalPromptStage = 3;
            state.QuickMatchTerminalPrompt1WaitClientPacketCount = 0;
            state.LateLargeCommandFollowupClientPacketCount = 0;
            state.PostTerminalMapLoadClientPacketCount = 0;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendLateLargeCommandFollowup(state, clientPacket))
        {
            responses.Clear();
            AddPacket(
                responses,
                state,
                BuildLateLargeCommandFollowupBody(game, player, state),
                "generated PS3 Source native late large-command follow-up state-link frame",
                sequenceAdvance: 788);
            state.SentLateLargeCommandFollowup = true;
            state.PostTerminalMapLoadClientPacketCount = 0;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldPacePostTerminalMapLoadStateAck(state, clientPacket.PayloadLength))
        {
            responses.Clear();
            state.PostTerminalMapLoadClientPacketCount++;
            if (state.PostTerminalMapLoadClientPacketCount >= 7)
            {
                AddPacket(
                    responses,
                    state,
                    BuildPostTerminalMapLoadStateAckBody(game, player, state),
                    "generated PS3 Source native post-terminal map-load state-link ack",
                    sequenceAdvance: 40);
                state.SentPostTerminalMapLoadStateAck = true;
            }

            return responses;
        }

        if (state.SentRosterDescriptorState && !sendCommandSnapshot)
        {
            AddSteadyStateDeltaTurn(
                responses,
                state,
                game,
                player,
                includeSemanticBootstrap: ShouldIncludeSteadySemanticBootstrap(state, game, player));
        }

        if (state.SentRosterDescriptorState && sendCommandSnapshot)
        {
            var sendFullCommandSnapshot = state.LastCommandSnapshotClientPacketCount == 0
                && !HasScoreboardSourceState(player, game);
            AddPacket(
                responses,
                state,
                sendFullCommandSnapshot
                    ? BuildSnapshotFrameBody(game, player)
                    : BuildCompactCommandSnapshotFrameBody(game, player),
                sendFullCommandSnapshot
                    ? "generated PS3 Source native command-driven snapshot/entity-delta frame"
                    : "generated PS3 Source native compact command-driven snapshot/entity-delta frame",
                sequenceAdvance: 4,
                allowFragmentation: sendFullCommandSnapshot,
                allowNativeWrap: sendFullCommandSnapshot);
            AddCompactCommandSnapshotSemanticDeltas(responses, state, game, player);

            if (sendCommandSnapshot)
            {
                state.LastCommandSnapshotClientPacketCount = state.ClientPacketCount;
                AddUrgentNativeSourceStateDeltas(responses, state, game, player);
            }
        }

        AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack fallback");

        return responses;
    }

    private static bool ShouldCapturePostRosterFrozenStateUpload(
        Ps3NativeSourceResponderState state,
        Ps3SourceTransportPacket clientPacket)
    {
        return !state.PendingPostRosterFrozenStateUpload
            && !state.SentPostRosterFrozenStateBatches
            && state.QuickMatchTerminalPromptStage == 0
            && state.PostRosterContinuationStage == 0
            && clientPacket.PayloadLength is 235 or 236
            && clientPacket.Body.Length is 233 or 234;
    }

    private static bool ShouldSendPostRosterFrozenStateBatches(
        Ps3NativeSourceResponderState state,
        Ps3SourceTransportPacket clientPacket)
    {
        return state.PendingPostRosterFrozenStateUpload
            && !state.SentPostRosterFrozenStateBatches
            && IsShortClientControl(clientPacket);
    }

    private static void CapturePostRosterFrozenStateUpload(PlayerSession player, Ps3SourceTransportPacket clientPacket)
    {
        var records = Ps3SourceEmbeddedObjectRecords.Extract(clientPacket.Body)
            .Where(static record =>
                record.Role == Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject
                && record.FieldA is > 0
                && record.FieldB is > 0)
            .ToArray();
        if (records.Length == 0)
        {
            return;
        }

        var rootObjectId = records
            .GroupBy(static record => record.FieldA!.Value)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key)
            .First()
            .Key;
        var requestedObjectIds = records
            .Select(static record => record.FieldB!.Value)
            .Where(objectId => objectId != 0 && objectId != rootObjectId)
            .Distinct()
            .ToArray();

        player.SourceState.ApplyFrozenStateClientUpload(rootObjectId, requestedObjectIds);
    }

    private static void AddPostRosterFrozenStateBatches(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        var batches = BuildPostRosterFrozenStateObjectBatches(game, player);
        foreach (var batch in batches)
        {
            AddPacket(
                responses,
                state,
                batch.Body,
                $"generated PS3 Source native post-roster {batch.Explanation}",
                sequenceAdvance: batch.SequenceAdvance);
        }
    }

    private static bool ShouldSendPostRosterFrozenStateContinuation(
        Ps3NativeSourceResponderState state,
        Ps3SourceTransportPacket clientPacket)
    {
        return state.WaitingForPostRosterFrozenStateContinuation
            && !state.SentQuickMatchTerminalMapLoad
            && state.PostRosterContinuationStage == 0
            && clientPacket.PayloadLength is >= 250 and <= 340
            && clientPacket.Body.Length == clientPacket.PayloadLength - 2;
    }

    private static void AddPostRosterFrozenStateContinuationResponses(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        var playerDisplayName = SourceDisplayName(player);
        AddPacket(
            responses,
            state,
            BuildFrozenStateObjectBody(
                game,
                player,
                78,
                4,
                [(PlayerSourceObjectId(player), playerDisplayName), .. FrozenStateNamedPeers(player).Take(1)]),
            "generated PS3 Source native post-roster frozen-state continuation COc player batch",
            sequenceAdvance: 1);
        AddPacket(
            responses,
            state,
            BuildQueuedBoundaryOnlyBody(
                game,
                player,
                state,
                32,
                0xa3,
                "post-roster-frozen-state-continuation-boundary"),
            "generated PS3 Source native post-roster frozen-state continuation boundary",
            sequenceAdvance: 8);
        AddPacket(
            responses,
            state,
            BuildQueuedBoundaryOnlyBody(
                game,
                player,
                state,
                21,
                0xa4,
                "post-roster-frozen-state-continuation-short-control"),
            "generated PS3 Source native post-roster frozen-state continuation short control",
            sequenceAdvance: 6);
        AddPacket(
            responses,
            state,
            BuildPurePlayerStateLinkBody(player, 2),
            "generated PS3 Source native post-roster frozen-state continuation PNG state-link without generated prefix",
            sequenceAdvance: 7);
    }

    private static bool ShouldStartPostRosterContinuation(
        Ps3NativeSourceResponderState state,
        PlayerSession player,
        Ps3SourceTransportPacket clientPacket)
    {
        return state.PostRosterContinuationStage == 0
            && state.QuickMatchTerminalPromptStage is 0 or 1
            && state.SentLateLargeCommandPreamble
            && clientPacket.PayloadLength == 235
            && clientPacket.Body.Length == 233
            && player.SourceState.LastClientCommandDecoded
            && state.LastCommandSnapshotClientPacketCount > 0
            && state.ClientPacketCount > FastCommandSnapshotMaxClientPacketCount;
    }

    private static bool IsShortClientControl(Ps3SourceTransportPacket clientPacket)
    {
        return Ps3SourceGameplaySession.ClassifyShape(clientPacket) == Ps3SourceGameplayPacketShape.ShortControl;
    }

    private static bool ShouldSendPostRosterMapLoadClientBatchAck(
        Ps3NativeSourceResponderState state,
        Ps3SourceTransportPacket clientPacket)
    {
        if (state.SentPostRosterMapLoadClientBatchAck
            || state.PostRosterContinuationStage != 0
            || clientPacket.PayloadLength < 120)
        {
            return false;
        }

        var semantic = Ps3SourcePayloadSemantics.Analyze(clientPacket.Body);
        return semantic.Role == Ps3SourcePayloadSemanticRole.PlayerRosterObjectBatch
            && semantic.EmbeddedMarkers.Count() > 0;
    }

    private static bool ShouldSendPostRosterShortGameplayAck(
        Ps3NativeSourceResponderState state,
        Ps3SourceTransportPacket clientPacket)
    {
        return state.SentPostRosterMapLoadClientBatchAck
            && !state.SentPostRosterShortGameplayAck
            && state.PostRosterContinuationStage == 0
            && clientPacket.PayloadLength == 89
            && clientPacket.Body.Length == 87;
    }

    private void AddPostRosterContinuationResponse(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player,
        Ps3SourceTransportPacket clientPacket)
    {
        if (state.PostRosterContinuationStage == 1)
        {
            AddPostRosterMapLoadContinuationResponse(responses, state, game, player);
            state.PostRosterContinuationStage = 2;
            return;
        }

        AddPostRosterMapLoadContinuationResponse(responses, state, game, player);
    }

    private void AddPostRosterMapLoadContinuationResponse(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        AddObjectStreamBootstrapResponses(
            responses,
            state,
            game,
            player,
            "generated PS3 Source native post-roster map-load continuation object-stream bootstrap batch");
        state.PostRosterContinuationStage = 0;
    }

    public byte[] BuildDiagnosticSnapshotFrame(GameManagerSession game, PlayerSession player)
    {
        return BuildSnapshotFrameBody(game, player);
    }

    public IReadOnlyList<byte[]> BuildDiagnosticCriticalSourceNetMessageBootstrap(GameManagerSession game, PlayerSession player)
    {
        return BuildDiagnosticCriticalSourceNetMessageBootstrapFrames(game, player)
            .Select(frame => frame.Payload)
            .ToArray();
    }

    public IReadOnlyList<Ps3SourceNetMessageFrame> BuildDiagnosticCriticalSourceNetMessageBootstrapFrames(
        GameManagerSession game,
        PlayerSession player)
    {
        var mapName = SourceMapName(game);
        var hostName = string.IsNullOrWhiteSpace(game.Name) ? "A Game" : game.Name;
        var playerName = string.IsNullOrWhiteSpace(player.Name) ? "Player" : player.Name;
        var stringTablePayload = BuildUserInfoStringTableData(playerName);
        var precacheStringTables = BuildBootstrapPrecacheStringTableFrames(mapName);
        var sendTablePayload = ToBitPayload(string.Join('\0', Tf2Ps3SourceCatalog.BootstrapSendTables) + "\0");
        var classInfo = new Ps3SourceSvcClassInfo(
            NumServerClasses: checked((short)Tf2Ps3SourceCatalog.ServerClasses.Count),
            CreateOnClient: false,
            Classes: Tf2Ps3SourceCatalog.ServerClasses);

        var frames = new List<Ps3SourceNetMessageFrame>
        {
            Ps3SourceNetMessages.BuildServerInfoFrame(new Ps3SourceSvcServerInfo(
                Protocol: 14,
                ServerCount: 1,
                IsHltv: false,
                IsDedicated: true,
                LegacyClientCrc: 0xffffffff,
                MaxClasses: checked((ushort)classInfo.NumServerClasses),
                MapCrcOrDigest32: StableSourceDigest32(mapName),
                PlayerSlot: SourcePlayerSlotIndex(game, player),
                MaxClients: checked((byte)Math.Clamp(game.MaxPlayers, 1, 32)),
                TickInterval: 1.0f / 30.0f,
                OperatingSystem: (byte)'l',
                GameDirectory: "tf",
                MapName: mapName,
                SkyName: SourceSkyName(mapName),
                HostName: hostName)),
            Ps3SourceNetMessages.BuildSendTableFrame(new Ps3SourceSvcSendTable(
                NeedsDecoder: true,
                Data: sendTablePayload,
                DataBitCount: sendTablePayload.Length * 8)),
            Ps3SourceNetMessages.BuildClassInfoFrame(classInfo),
            Ps3SourceNetMessages.BuildCreateStringTableFrame(new Ps3SourceSvcStringTable(
                TableName: "userinfo",
                MaxEntries: 2048,
                NumEntries: 1,
                UserDataFixedSize: false,
                UserDataSize: 0,
                UserDataSizeBits: 0,
                Data: stringTablePayload.Payload,
                DataBitCount: stringTablePayload.BitCount))
        };
        frames.AddRange(precacheStringTables);
        frames.AddRange(
        [
            Ps3SourceNetMessages.BuildUpdateStringTableFrame(new Ps3SourceSvcStringTableUpdate(
                TableId: 0,
                ChangedEntries: 1,
                Data: stringTablePayload.Payload,
                DataBitCount: stringTablePayload.BitCount)),
            Ps3SourceNetMessages.BuildPacketEntitiesFrame(new Ps3SourceSvcPacketEntities(
                MaxEntries: 2047,
                IsDelta: false,
                DeltaFrom: 0,
                Baseline: 0,
                UpdatedEntries: 0,
                UpdateBaseline: false,
                Data: [],
                DataBitCount: 0))
        ]);

        return frames;
    }

    public Ps3SourceNetMessageFrame BuildDiagnosticClassInfoFollowUpAppendFrame(GameManagerSession game, PlayerSession player)
    {
        return BuildNativeClassInfoFollowUpRawAppendFrame(game, player);
    }

    public Ps3SourceNetMessageFrame BuildNativeClassInfoFollowUpRawAppendFrame(GameManagerSession game, PlayerSession player)
    {
        return BuildNativeServerSignonBufferFrame(game, player);
    }

    public Ps3SourceNetMessageFrame BuildNativeServerSignonBufferFrame(GameManagerSession game, PlayerSession player)
    {
        // TF.elf 00a56cb0 appends a cached +0xd8/+0xe4 bit-buffer immediately after
        // SVC_ClassInfo. The matching Source path is CBaseClient::SendSignonData,
        // where m_Server->m_Signon carries persistent init messages built during
        // baseline creation rather than a per-player string-table update.
        var gameEventDescriptors = Tf2SourceGameEventResourceCatalog.LoadOrDefault(
            game.NativeSourceContentRootPath,
            Tf2Ps3SourceCatalog.BootstrapGameEventDescriptors);
        var frames = new List<Ps3SourceNetMessageFrame>
        {
            Ps3SourceNetMessages.BuildVoiceInitFrame(new Ps3SourceSvcVoiceInit(
                Codec: "vaudio_speex",
                LegacyQuality: 255)),
            Ps3SourceNetMessages.BuildGameEventListFrame(gameEventDescriptors)
        };
        frames.AddRange(BuildNativeServerSignonMapInitFrames(game, player));
        return Ps3SourceNetMessages.ConcatenateFrames(frames);
    }

    private static IReadOnlyList<Ps3SourceNetMessageFrame> BuildNativeServerSignonMapInitFrames(
        GameManagerSession game,
        PlayerSession player)
    {
        var anchor = NativeServerSignonWorldAnchor(game, player);
        return
        [
            Ps3SourceNetMessages.BuildSoundsFrame(
                reliableSound: true,
                [
                    Ps3SourceSoundInfo.Default with
                    {
                        SequenceNumber = checked((int)(StableSourceDigest32($"signon-sound-seq:{game.MapName}") % (1U << Ps3SourceNetMessageConstants.SoundSequenceNumberBits))),
                        EntityIndex = 0,
                        Channel = Ps3SourceNetMessageConstants.DefaultSoundChannel,
                        Origin = anchor,
                        Volume = 1.0f,
                        SoundLevel = Ps3SourceNetMessageConstants.DefaultSoundLevel,
                        Pitch = Ps3SourceNetMessageConstants.DefaultSoundPitch,
                        SoundNumber = NativeServerSignonSoundIndex(game),
                        IsAmbient = true
                    }
                ]),
            Ps3SourceNetMessages.BuildBspDecalFrame(new Ps3SourceSvcBspDecal(
                Position: anchor,
                DecalTextureIndex: NativeServerSignonDecalTextureIndex(game),
                EntityIndex: 0,
                ModelIndex: 0,
                LowPriority: false))
        ];
    }

    private static Ps3SourceVector NativeServerSignonWorldAnchor(GameManagerSession game, PlayerSession player)
    {
        var metadata = game.CurrentMapMetadata;
        if (metadata is not null)
        {
            var controlPoint = metadata.ControlPoints.FirstOrDefault();
            if (controlPoint is not null)
            {
                return new Ps3SourceVector(controlPoint.X, controlPoint.Y, controlPoint.Z);
            }

            var flag = metadata.Flags.FirstOrDefault();
            if (flag is not null)
            {
                return new Ps3SourceVector(flag.X, flag.Y, flag.Z);
            }

            var spawn = metadata.SpawnPoints.FirstOrDefault(static point => point.Enabled)
                ?? metadata.SpawnPoints.FirstOrDefault();
            if (spawn is not null)
            {
                return new Ps3SourceVector(spawn.X, spawn.Y, spawn.Z);
            }

            if (metadata.Bounds is { } bounds)
            {
                return new Ps3SourceVector(
                    (bounds.MinX + bounds.MaxX) * 0.5f,
                    (bounds.MinY + bounds.MaxY) * 0.5f,
                    (bounds.MinZ + bounds.MaxZ) * 0.5f);
            }
        }

        return new Ps3SourceVector(
            player.SourceState.OriginX,
            player.SourceState.OriginY,
            player.SourceState.OriginZ);
    }

    private static int NativeServerSignonSoundIndex(GameManagerSession game)
    {
        return 1;
    }

    private static int NativeServerSignonDecalTextureIndex(GameManagerSession game)
    {
        return 1;
    }

    public IReadOnlyList<byte[]> BuildDiagnosticCriticalSourceObjectStreamBootstrap(GameManagerSession game, PlayerSession player)
    {
        var ownerOrCallbackId = player.SourceState.RootObjectId == 0
            ? checked((uint)Math.Max(player.PlayerId, 0))
            : checked((uint)player.SourceState.RootObjectId);
        return BuildDiagnosticCriticalSourceNetMessageBootstrapFrames(game, player)
            .Select((frame, index) => Ps3SourceObjectStream.Encode(new Ps3SourceObjectStreamRecord(
                MessageKind: SourceNetFrameObjectStreamKind(frame),
                OwnerOrCallbackId: ownerOrCallbackId,
                Sequence: checked((uint)(index + 1)),
                Payload: frame.Payload,
                PayloadBitCount: frame.BitCount)))
            .ToArray();
    }

    public IReadOnlyList<byte[]> BuildDiagnosticCriticalSourceObjectStreamBootstrapBatches(GameManagerSession game, PlayerSession player)
    {
        var ownerOrCallbackId = player.SourceState.RootObjectId == 0
            ? checked((uint)Math.Max(player.PlayerId, 0))
            : checked((uint)player.SourceState.RootObjectId);
        var frames = BuildDiagnosticCriticalSourceNetMessageBootstrapFrames(game, player);
        if (frames.Count < 6)
        {
            throw new InvalidOperationException("Critical Source bootstrap frame builder returned an incomplete frame set.");
        }

        var serverInfo = RequiredSourceNetFrame(frames, Ps3SourceNetMessageConstants.SvcServerInfo);
        var sendTable = RequiredSourceNetFrame(frames, Ps3SourceNetMessageConstants.SvcSendTable);
        var classInfo = RequiredSourceNetFrame(frames, Ps3SourceNetMessageConstants.SvcClassInfo);
        var createStringTables = SourceNetFramesOfType(frames, Ps3SourceNetMessageConstants.SvcCreateStringTable);
        var spawnCount = 1;
        var viewEntityIndex = checked((int)(player.SourceState.LocalPlayerObjectId == 0
            ? player.SourceState.RootObjectId
            : player.SourceState.LocalPlayerObjectId));
        var signonState3 = Ps3SourceNetMessages.BuildNetSignonStateFrame(new Ps3SourceNetSignonState(3, spawnCount));
        var signonState4 = Ps3SourceNetMessages.BuildNetSignonStateFrame(new Ps3SourceNetSignonState(4, spawnCount));
        var setView = Ps3SourceNetMessages.BuildSetViewFrame(new Ps3SourceSvcSetView(viewEntityIndex));
        var signonState5 = Ps3SourceNetMessages.BuildNetSignonStateFrame(new Ps3SourceNetSignonState(5, spawnCount));
        var signonState6 = Ps3SourceNetMessages.BuildNetSignonStateFrame(new Ps3SourceNetSignonState(6, spawnCount));
        var classInfoFollowUp = BuildNativeClassInfoFollowUpRawAppendFrame(game, player);
        Ps3SourceNetMessageFrame[] batches =
        [
            Ps3SourceNetMessages.ConcatenateFrames([serverInfo, sendTable, .. createStringTables, signonState3]),
            Ps3SourceNetMessages.ConcatenateFrames([classInfo, classInfoFollowUp, signonState4]),
            Ps3SourceNetMessages.ConcatenateFrames([setView, signonState5, signonState6])
        ];

        return batches
            .Select((frame, index) => Ps3SourceObjectStream.Encode(new Ps3SourceObjectStreamRecord(
                MessageKind: 1,
                OwnerOrCallbackId: ownerOrCallbackId,
                Sequence: checked((uint)(index + 1)),
                Payload: frame.Payload,
                PayloadBitCount: frame.BitCount)))
            .ToArray();
    }

    private static byte SourceNetFrameObjectStreamKind(Ps3SourceNetMessageFrame frame)
    {
        return Ps3SourceNetMessages.TryReadMessageType(frame.Payload, out var messageType)
            && messageType == Ps3SourceNetMessageConstants.SvcUpdateStringTable
                ? (byte)2
                : (byte)1;
    }

    private static Ps3SourceNetMessageFrame RequiredSourceNetFrame(
        IReadOnlyList<Ps3SourceNetMessageFrame> frames,
        int messageType)
    {
        foreach (var frame in frames)
        {
            if (Ps3SourceNetMessages.TryReadMessageType(frame.Payload, out var candidate)
                && candidate == messageType)
            {
                return frame;
            }
        }

        throw new InvalidOperationException($"Critical Source bootstrap frame set is missing message type {messageType}.");
    }

    private static Ps3SourceNetMessageFrame[] SourceNetFramesOfType(
        IReadOnlyList<Ps3SourceNetMessageFrame> frames,
        int messageType)
    {
        return frames
            .Where(frame => Ps3SourceNetMessages.TryReadMessageType(frame.Payload, out var candidate)
                && candidate == messageType)
            .ToArray();
    }

    private void AddQuickMatchTerminalObjectStreamBootstrapResponses(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        AddObjectStreamBootstrapResponses(
            responses,
            state,
            game,
            player,
            "generated PS3 Source native quick-match terminal map-load object-stream bootstrap batch");
        AddPostTerminalSteadyBootstrapResponse(responses, state, game, player);
    }

    private static void AddPostTerminalSteadyBootstrapResponse(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        if (state.SentSteadySemanticBootstrapSnapshot)
        {
            return;
        }

        AddPacket(
            responses,
            state,
            BuildUrgentStateSnapshotFrameBody(game, player, includeRagdoll: !player.SourceState.Alive),
            "generated PS3 Source native post-terminal steady semantic bootstrap snapshot/entity-delta frame",
            sequenceAdvance: 4,
            allowFragmentation: true,
            allowNativeWrap: true);
        state.SentSteadySemanticBootstrapSnapshot = true;
    }

    private void AddCriticalSourceObjectStreamBootstrapResponses(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player,
        string explanationPrefix)
    {
        AddObjectStreamBootstrapResponses(
            responses,
            state,
            game,
            player,
            explanationPrefix,
            sequenceAdvance: 1);
    }

    private void AddObjectStreamBootstrapResponses(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player,
        string explanationPrefix,
        int? sequenceAdvance = null)
    {
        var batches = BuildDiagnosticCriticalSourceObjectStreamBootstrapBatches(game, player);
        for (var i = 0; i < batches.Count; i++)
        {
            AddPacket(
                responses,
                state,
                batches[i],
                $"{explanationPrefix} {i + 1}",
                sequenceAdvance: sequenceAdvance,
                allowFragmentation: true);
        }
    }

    private static void AddFallbackShortControlIfNeeded(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        ushort clientSequence,
        string explanation)
    {
        if (responses.Count != 0 || !state.SentInitialSetup)
        {
            return;
        }

        responses.Add(BuildPacket(state, BuildShortControlBody(clientSequence, state.ClientPacketCount), explanation));
    }

    private static void AddLoadingStateLinkHeartbeat(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        var shape = LoadingStateLinkHeartbeatShapes[state.LoadingStateLinkHeartbeatIndex % LoadingStateLinkHeartbeatShapes.Length];
        state.LoadingStateLinkHeartbeatIndex++;
        var detail = BuildPlayerStateLinkSlotReplacementBody(
            game,
            player,
            state,
            shape.Length,
            LoadingContinuationSeed(90, state.LoadingStateLinkHeartbeatIndex, shape.Length),
            90 + state.LoadingStateLinkHeartbeatIndex,
            $"loading-heartbeat-{state.LoadingStateLinkHeartbeatIndex}",
            out var body,
            out var builtNativeSnapshot);
        AddPacket(
            responses,
            state,
            body,
            builtNativeSnapshot
                ? "generated PS3 Source native loading state-link heartbeat snapshot/entity-delta"
                : $"generated PS3 Source native loading state-link heartbeat {detail}",
            sequenceAdvance: 3);
    }

    private static void AddLoadingContinuationStage(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player,
        LoadingContinuationFrame[][] stages)
    {
        var stageIndex = state.LoadingContinuationStage - 1;
        var frames = stages[stageIndex];
        AddLoadingFrames(
            responses,
            state,
            game,
            player,
            frames,
            stageIndex + 2,
            "generated PS3 Source native loading continuation frame",
            state.InitialSetupVariant);

        var suppressAfter = 0;
        foreach (var frame in frames)
        {
            suppressAfter = Math.Max(suppressAfter, frame.SuppressAfter);
        }

        if (state.InitialSetupVariant == 1 && ShouldSuppressNextLoadingContinuationResponse(stageIndex, frames))
        {
            suppressAfter = Math.Max(suppressAfter, 1);
        }

        suppressAfter = Math.Max(suppressAfter, LoadingContinuationSuppressAfter(state.InitialSetupVariant, stageIndex));

        if (suppressAfter > 0)
        {
            state.SuppressLoadingContinuationResponses = ShouldSilenceNextLoadingContinuationResponse(stageIndex, frames)
                ? -suppressAfter
                : suppressAfter;
        }
    }

    private static bool ConsumeSilentLoadingContinuationResponse(Ps3NativeSourceResponderState state)
    {
        if (state.SuppressLoadingContinuationResponses >= 0)
        {
            return false;
        }

        state.SuppressLoadingContinuationResponses++;
        return true;
    }

    private static void AddLoadingFrames(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player,
        IReadOnlyList<LoadingContinuationFrame> frames,
        int seedStageIndex,
        string explanation,
        int initialSetupVariant = 1)
    {
        for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            var frame = frames[frameIndex];
            var seed = LoadingContinuationSeed(seedStageIndex, frameIndex, frame.Length);
            var builtNativeSnapshot = false;
            byte[] body = [];
            var forceMarkerlessNativeChunk = frame.Kind == LoadingContinuationFrameKind.NativeSnapshot
                || (initialSetupVariant == 1
                    && seedStageIndex == 0
                    && frame.Length >= 1000);
            var detail = forceMarkerlessNativeChunk
                ? "queued-peer high-entropy loading chunk"
                : frame.Kind == LoadingContinuationFrameKind.PlayerStateLink
                ? "queued PNG state-link loading frame"
                : TryBuildNativeLoadingContinuationBody(
                    game,
                    player,
                    state,
                    frame,
                    seed,
                    seedStageIndex,
                    frameIndex,
                    out body,
                    out builtNativeSnapshot,
                    out var nativeDetail)
                    ? nativeDetail
                    : "queued-boundary placeholder";

            if (forceMarkerlessNativeChunk)
            {
                body = BuildQueuedBoundaryOnlyBody(
                    game,
                    player,
                    state,
                    frame.Length,
                    (byte)seed,
                    $"loading-initial-queued-chunk-{seedStageIndex}-{frameIndex}-{frame.Length}");
            }
            else if (frame.Kind == LoadingContinuationFrameKind.PlayerStateLink)
            {
                body = BuildNativeQueuedLoadingPlayerStateLinkBody(
                    game,
                    player,
                    state,
                    frame.Length,
                    frame.PrefixLength,
                    frame.MaxRecords,
                    $"loading-state-link-{seedStageIndex}-{frameIndex}-{frame.Length}");
            }
            else if (detail == "queued-boundary placeholder")
            {
                body = BuildQueuedBoundaryOnlyBody(
                    game,
                    player,
                    state,
                    frame.Length,
                    (byte)seed,
                    $"loading-{frame.Kind}-{seedStageIndex}-{frameIndex}-{frame.Length}");
            }

            AddPacket(
                responses,
                state,
                body,
                builtNativeSnapshot
                    ? $"{explanation} snapshot/entity-delta"
                    : $"{explanation} {detail}",
                sequenceAdvance: LoadingContinuationSequenceAdvance(frame.Length));
        }
    }

    private static bool TryBuildNativeLoadingContinuationBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        LoadingContinuationFrame frame,
        uint seed,
        int seedStageIndex,
        int frameIndex,
        out byte[] body,
        out bool builtNativeSnapshot,
        out string detail)
    {
        builtNativeSnapshot = false;
        detail = "";

        if (frame.Kind == LoadingContinuationFrameKind.NativeQueuedBoundary
            && TryBuildCompactLoadingControlBody(game, player, state, frame.Length, (byte)seed, out body, out detail))
        {
            return true;
        }

        if (frame.Kind == LoadingContinuationFrameKind.NativeQueuedBoundary
            && TryBuildEmbeddedLoadingBoundaryBody(game, player, state, frame.Length, (byte)seed, seedStageIndex, frameIndex, out body, out detail))
        {
            return true;
        }

        if (TryBuildNativeSnapshotBody(game, player, frame.Length, seed, seedStageIndex, out body))
        {
            builtNativeSnapshot = true;
            detail = "snapshot/entity-delta";
            return true;
        }

        if (TryBuildEmbeddedLoadingBoundaryBody(game, player, state, frame.Length, (byte)seed, seedStageIndex, frameIndex, out body, out detail))
        {
            return true;
        }

        body = [];
        return false;
    }

    private static string BuildPlayerStateLinkSlotReplacementBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int targetLength,
        uint seed,
        int stageIndex,
        string family,
        out byte[] body,
        out bool builtNativeSnapshot)
    {
        builtNativeSnapshot = false;
        if (targetLength >= 48 && TryBuildNativeSnapshotBody(game, player, targetLength, seed, stageIndex, out body))
        {
            builtNativeSnapshot = true;
            return "snapshot/entity-delta";
        }

        var recordCount = Math.Max(1, Math.Min(10, targetLength / Ps3SourcePlayerStateLinkRecord.Length));
        body = BuildPurePlayerStateLinkBody(player, recordCount);
        return $"PNG state-link records without generated prefix for {family}";
    }

    private static byte[] BuildPlayerStateLinkSlotReplacementPayload(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int targetLength,
        uint seed,
        int stageIndex,
        string family)
    {
        _ = BuildPlayerStateLinkSlotReplacementBody(
            game,
            player,
            state,
            targetLength,
            seed,
            stageIndex,
            family,
            out var body,
            out _);
        return body;
    }

    private static byte[] BuildPurePlayerStateLinkBody(PlayerSession player, int recordCount)
    {
        var objectIds = RepeatingFrozenStateObjectIds(player, Math.Max(1, recordCount))
            .Select(objectId => new Ps3SourcePlayerStateLinkRecord(objectId, RootSourceObjectId(player)))
            .ToArray();
        return Ps3SourcePlayerStateLinkRecord.BuildBatch(objectIds);
    }

    private static bool TryBuildCompactLoadingControlBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int length,
        byte streamKind,
        out byte[] body,
        out string detail)
    {
        var gameId = unchecked((uint)game.GameId);
        var playerId = unchecked((uint)player.PlayerId);
        var clientSequence = player.SourceState.LastClientSourceSequence;
        body = length switch
        {
            Ps3SourceCompactControlFrame.AckToken10BodyBytes => Ps3SourceCompactControlFrame.EncodeAckToken10(
                gameId,
                playerId,
                clientSequence,
                state.ClientPacketCount,
                state.NextServerSequence,
                streamKind),
            Ps3SourceCompactControlFrame.Control17BodyBytes => Ps3SourceCompactControlFrame.EncodeControl17(
                gameId,
                playerId,
                clientSequence,
                state.ClientPacketCount,
                state.NextServerSequence,
                streamKind),
            Ps3SourceCompactControlFrame.AckWindow21BodyBytes => Ps3SourceCompactControlFrame.EncodeAckWindow21(
                gameId,
                playerId,
                clientSequence,
                state.ClientPacketCount,
                state.NextServerSequence,
                streamKind),
            Ps3SourceCompactControlFrame.AckWindow28BodyBytes => Ps3SourceCompactControlFrame.EncodeAckWindow28(
                gameId,
                playerId,
                clientSequence,
                state.ClientPacketCount,
                state.NextServerSequence,
                streamKind),
            Ps3SourceCompactControlFrame.ServerControl31BodyBytes => Ps3SourceCompactControlFrame.EncodeServerControl31(
                gameId,
                playerId,
                clientSequence,
                state.ClientPacketCount,
                state.NextServerSequence,
                streamKind),
            _ => []
        };

        if (body.Length == 0)
        {
            detail = "";
            return false;
        }

        detail = length switch
        {
            Ps3SourceCompactControlFrame.AckToken10BodyBytes => "compact ack-token",
            Ps3SourceCompactControlFrame.Control17BodyBytes => "compact control",
            Ps3SourceCompactControlFrame.AckWindow21BodyBytes => "compact ack-window",
            Ps3SourceCompactControlFrame.AckWindow28BodyBytes => "compact ack-window28",
            Ps3SourceCompactControlFrame.ServerControl31BodyBytes => "compact server-control31",
            _ => "compact control"
        };
        return true;
    }

    private static bool TryBuildEmbeddedLoadingBoundaryBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int length,
        byte streamKind,
        int seedStageIndex,
        int frameIndex,
        out byte[] body,
        out string detail)
    {
        var family = $"loading-boundary-{seedStageIndex}-{frameIndex}-{length}";
        if (TryGetLoadingStateLinkShape(length, out var stateLinkPrefix, out var stateLinkRecords))
        {
            body = BuildEmbeddedPlayerStateLinkBoundaryBody(
                game,
                player,
                state,
                length,
                stateLinkPrefix,
                stateLinkRecords,
                streamKind,
                family + ":png");
            detail = "PNG state-link boundary";
            return true;
        }

        if (TryGetLoadingFrozenStateShape(length, out var frozenPrefix, out var frozenRecords))
        {
            body = BuildEmbeddedFrozenStateBoundaryBody(
                game,
                player,
                state,
                length,
                frozenPrefix,
                frozenRecords,
                streamKind,
                family + ":coc");
            detail = "COc FrozenState boundary";
            return true;
        }

        body = [];
        detail = "";
        return false;
    }

    private static bool TryGetLoadingStateLinkShape(int length, out int prefixLength, out int recordCount)
    {
        (prefixLength, recordCount) = length switch
        {
            24 => (0, 2),
            28 => (4, 2),
            35 => (11, 2),
            38 => (14, 2),
            42 => (6, 3),
            48 => (0, 4),
            49 => (25, 2),
            52 => (16, 3),
            56 => (8, 4),
            63 => (15, 4),
            66 => (18, 4),
            70 => (10, 5),
            77 => (13, 5),
            84 => (12, 6),
            91 => (13, 6),
            94 => (10, 7),
            98 => (14, 7),
            196 => (4, 16),
            206 => (14, 16),
            220 => (16, 17),
            _ => (-1, 0)
        };

        return prefixLength >= 0;
    }

    private static bool TryGetLoadingFrozenStateShape(int length, out int prefixLength, out int recordCount)
    {
        (prefixLength, recordCount) = length switch
        {
            53 => (16, 1),
            60 => (23, 1),
            74 => (0, 2),
            _ => (-1, 0)
        };

        return prefixLength >= 0;
    }

    private static bool ShouldBuildPlayerStateLinkAsNativeSnapshot(int initialSetupVariant, int length, int seedStageIndex)
    {
        if (length >= 48)
        {
            return true;
        }

        return initialSetupVariant switch
        {
            2 => length == 210 || length is >= 256 and < 998,
            3 => length is >= 256 and < 998 || (length == 220 && seedStageIndex is 19 or 28),
            4 => length == 220 || length is >= 256 and < 998,
            _ => false
        };
    }

    private static LoadingContinuationFrame[][] LoadingContinuationStagesFor(int initialSetupVariant)
    {
        return initialSetupVariant switch
        {
            2 => Variant2LoadingContinuationStages,
            3 => Variant3LoadingContinuationStages,
            4 => Variant4LoadingContinuationStages,
            _ => LoadingContinuationStages
        };
    }

    private static void CatchUpLoadingContinuationStage(
        Ps3NativeSourceResponderState state,
        ushort clientSequence,
        int stageCount)
    {
        if (clientSequence < 1000 || stageCount <= 0 || state.ClientPacketCount >= 100)
        {
            return;
        }

        // Live RPCS3 currently advances client Source sequence numbers faster than
        // the historical PCAP cadence. Keep the generated stream aligned to the
        // late-loading region instead of replaying early-loading continuation turns
        // long after the client has moved past them.
        var targetStage = LoadingContinuationStageForSparseQuickMatch(clientSequence);
        if (targetStage > state.LoadingContinuationStage)
        {
            state.LoadingContinuationStage = Math.Clamp(targetStage, 1, stageCount);
            state.SuppressLoadingContinuationResponses = 0;
        }
    }

    private static int LoadingContinuationStageForSparseQuickMatch(ushort clientSequence)
    {
        ReadOnlySpan<(ushort Sequence, int Stage)> thresholds =
        [
            (1000, 120),
            (1020, 121),
            (1030, 122),
            (1037, 123),
            (1047, 124),
            (1057, 125),
            (1062, 126),
            (1068, 127),
            (1078, 128),
            (1088, 129),
            (1098, 130),
            (1106, 131),
            (1109, 132),
            (1118, 133),
            (1126, 134),
            (1129, 135),
            (1132, 136),
            (1144, 137),
            (1150, 138),
            (1159, 139),
            (1165, 140),
            (1174, 141),
            (1183, 142),
            (1189, 143),
            (1198, 144),
            (1204, 145)
        ];

        var stage = 120;
        foreach (var threshold in thresholds)
        {
            if (clientSequence < threshold.Sequence)
            {
                break;
            }

            stage = threshold.Stage;
        }

        return stage;
    }

    private static bool ShouldForceLiveLoadingHandoff(
        Ps3NativeSourceResponderState state,
        PlayerSession player,
        ushort clientSequence)
    {
        var continuationExhausted = state.InitialSetupVariant == 1
            && state.LoadingContinuationStage > LoadingContinuationStages.Length + 2;
        var commandCadence = state.ClientPacketCount >= 320
            && player.SourceState.LastClientSourceBodyLength >= 69
            && player.SourceState.LastClientSourceSequenceDelta is >= 16 and <= 20;
        var decodedCommandCadence = player.SourceState.LastClientCommandDecoded
            && state.ClientPacketCount >= 320;
        var lateClientPacketCount = state.ClientPacketCount >= 320;
        var sparseQuickMatchRosterHandoff = state.InitialSetupVariant == 1
            && !state.SawInitialClientFrozenStateUpload
            && state.ClientPacketCount >= 60
            && (clientSequence >= SparseQuickMatchRosterHandoffSequence
                || player.SourceState.LastClientSourceSequence >= SparseQuickMatchRosterHandoffSequence
                || state.LoadingContinuationStage >= 132);
        var lateClientSequence = clientSequence >= 4000
            || player.SourceState.LastClientSourceSequence >= 4000;

        return state.SentLoadingPostBurstContinuation
            && !state.SentRosterDescriptorState
            && (continuationExhausted
                || sparseQuickMatchRosterHandoff
                || lateClientSequence
                || lateClientPacketCount
                || commandCadence
                || decodedCommandCadence);
    }

    private static void AddLateLoadingHandoffResponses(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        if (!state.SentLoadingMotdEvent)
        {
            AddPacket(
                responses,
                state,
                BuildLoadingMotdEventBody(game, player),
                "generated PS3 Source native sparse loading MOTD event",
                sequenceAdvance: 6,
                allowNativeWrap: true);
            state.SentLoadingMotdEvent = true;
        }

        AddPacket(
            responses,
            state,
            BuildPlayerObjectBody(game, player, 166, 18),
            "generated PS3 Source native sparse COc player-object roster batch",
            sequenceAdvance: 6);
        AddPacket(
            responses,
            state,
            BuildPlayerDescriptorBody(game, player, 64, 16),
            "generated PS3 Source native sparse DSC player-descriptor batch",
            sequenceAdvance: 4);
        state.SentRosterDescriptorState = true;
    }

    private static LoadingContinuationFrame[] LoadingBurstFramesFor(int initialSetupVariant)
    {
        return initialSetupVariant switch
        {
            2 => Variant2LoadingBurstFrames,
            3 => Variant3LoadingBurstFrames,
            4 => Variant4LoadingBurstFrames,
            _ => [new(LoadingContinuationFrameKind.PlayerStateLink, 1112, 28, 9), new(LoadingContinuationFrameKind.NativeSnapshot, 1212)]
        };
    }

    private static LoadingContinuationFrame[] LoadingPostBurstFramesFor(int initialSetupVariant)
    {
        return initialSetupVariant switch
        {
            2 => Variant2LoadingPostBurstFrames,
            3 => Variant3LoadingPostBurstFrames,
            4 => Variant4LoadingPostBurstFrames,
            _ => [new(LoadingContinuationFrameKind.NativeSnapshot, 206)]
        };
    }

    private static int LoadingBurstSuppressAfter(int initialSetupVariant)
    {
        return initialSetupVariant switch
        {
            4 => 1,
            _ => 0
        };
    }

    private static int LoadingPostBurstSuppressAfter(int initialSetupVariant)
    {
        return initialSetupVariant switch
        {
            3 => 1,
            _ => 0
        };
    }

    private static LoadingContinuationFrame[][] ParseLoadingContinuationStages(string encoded)
    {
        return encoded
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseLoadingFrameLine)
            .ToArray();
    }

    private static LoadingContinuationFrame[] ParseLoadingFrameLine(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return [];
        }

        return encoded
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseLoadingFrame)
            .ToArray();
    }

    private static LoadingContinuationFrame ParseLoadingFrame(string encoded)
    {
        var kindCode = encoded[^1];
        var length = int.Parse(encoded.AsSpan(0, encoded.Length - 1));
        return kindCode switch
        {
            'L' => PlayerStateLinkFrame(length),
            'M' => new LoadingContinuationFrame(LoadingContinuationFrameKind.NativeQueuedBoundary, length),
            _ => new LoadingContinuationFrame(LoadingContinuationFrameKind.NativeSnapshot, length)
        };
    }

    private static LoadingContinuationFrame PlayerStateLinkFrame(int length)
    {
        if (length >= 512)
        {
            var recordCount = LargePlayerStateLinkRecordCount(length);
            return new LoadingContinuationFrame(
                LoadingContinuationFrameKind.PlayerStateLink,
                length,
                Math.Max(0, length - (recordCount * Ps3SourcePlayerStateLinkRecord.Length)),
                recordCount);
        }

        var (prefixLength, maxRecords) = length switch
        {
            >= 250 => (18, 9),
            >= 160 => (14, 8),
            >= 120 => (14, 7),
            >= 90 => (13, 6),
            >= 70 => (10, 5),
            >= 56 => (8, 4),
            >= 49 => (25, 2),
            >= 35 => (11, 2),
            _ => (4, 2)
        };

        return new LoadingContinuationFrame(LoadingContinuationFrameKind.PlayerStateLink, length, prefixLength, maxRecords);
    }

    private static int LargePlayerStateLinkRecordCount(int length)
    {
        ReadOnlySpan<(int BodyLength, int RecordCount)> retailBodies =
        [
            (1212, 2),
            (1198, 3),
            (1180, 3),
            (1156, 2),
            (1152, 1),
            (1149, 1),
            (1142, 1),
            (1112, 1),
            (982, 1),
            (944, 3),
            (944, 1),
            (874, 2),
            (860, 1),
            (686, 4),
            (685, 1),
            (634, 3),
            (620, 2),
            (620, 4),
            (606, 3),
            (602, 2),
            (592, 2),
            (588, 1),
            (578, 1)
        ];

        var bestRecordCount = 1;
        var bestDelta = int.MaxValue;
        foreach (var (bodyLength, recordCount) in retailBodies)
        {
            var delta = Math.Abs(length - bodyLength);
            if (delta >= bestDelta)
            {
                continue;
            }

            bestDelta = delta;
            bestRecordCount = recordCount;
        }

        return bestRecordCount;
    }

    private static bool ShouldSuppressNextLoadingContinuationResponse(int stageIndex, IReadOnlyList<LoadingContinuationFrame> frames)
    {
        if (stageIndex >= 48)
        {
            if (frames.Count == 3
                && frames[0].Kind == LoadingContinuationFrameKind.NativeQueuedBoundary
                && frames[1].Kind == LoadingContinuationFrameKind.PlayerStateLink
                && frames[2].Kind == LoadingContinuationFrameKind.NativeQueuedBoundary
                && (frames[0].Length, frames[1].Length, frames[2].Length) is (28, 77, 21))
            {
                return true;
            }

            if (frames.Count == 2
                && ((frames[0].Kind, frames[0].Length, frames[1].Kind, frames[1].Length) is
                    (LoadingContinuationFrameKind.PlayerStateLink, 70, LoadingContinuationFrameKind.NativeQueuedBoundary, 10) or
                    (LoadingContinuationFrameKind.PlayerStateLink, 77, LoadingContinuationFrameKind.PlayerStateLink, 35)))
            {
                return true;
            }
        }

        if (frames.Count == 3
            && frames[0].Kind == LoadingContinuationFrameKind.NativeQueuedBoundary
            && frames[1].Kind == LoadingContinuationFrameKind.NativeQueuedBoundary
            && frames[2].Kind == LoadingContinuationFrameKind.PlayerStateLink)
        {
            return (frames[0].Length, frames[1].Length, frames[2].Length) is (10, 21, 63);
        }

        if (frames.Count != 2
            || frames[0].Kind != LoadingContinuationFrameKind.PlayerStateLink
            || frames[1].Kind != LoadingContinuationFrameKind.PlayerStateLink)
        {
            return false;
        }

        return (frames[0].Length, frames[1].Length) is (63, 63) or (49, 49);
    }

    private static bool ShouldSilenceNextLoadingContinuationResponse(int stageIndex, IReadOnlyList<LoadingContinuationFrame> frames)
    {
        if (ShouldSuppressNextLoadingContinuationResponse(stageIndex, frames))
        {
            return true;
        }

        if (frames.Any(static frame => frame.SuppressAfter > 0))
        {
            return true;
        }

        if (frames.Count == 2
            && frames[0].Kind == LoadingContinuationFrameKind.PlayerStateLink
            && frames[1].Kind == LoadingContinuationFrameKind.PlayerStateLink
            && (frames[0].Length, frames[1].Length) is (63, 63) or (49, 49))
        {
            return true;
        }

        return frames.Count > 0
            && frames.All(static frame => frame.Kind == LoadingContinuationFrameKind.PlayerStateLink)
            && frames.Any(static frame => frame.SuppressAfter > 0);
    }

    private static int LoadingContinuationSuppressAfter(int initialSetupVariant, int stageIndex)
    {
        if (initialSetupVariant is not (2 or 3 or 4))
        {
            return 0;
        }

        if (initialSetupVariant == 2)
        {
            return stageIndex switch
            {
                51 or 58 => 2,
                24 or 31 or 33 or 34 or 36 or 40 or 47 or 71 or 73 or 80 or 86 or 87 or 92 or 103 or 115 or 122 => 1,
                _ => 0
            };
        }

        if (initialSetupVariant == 3)
        {
            return stageIndex switch
            {
                47 or 104 => 2,
                8 or 22 or 28 or 38 or 41 or 48 or 56 or 59 or 69 or 75 or 77 or 85 or 86 or 87 or 94 or 97 or 119 or 142 or 163 => 1,
                _ => 0
            };
        }

        return stageIndex switch
        {
            74 => 3,
            41 or 49 or 89 or 107 => 2,
            3 or 14 or 17 or 32 or 34 or 40 or 44 or 46 or 52 or 55 or 84 or 87 or 90 or 95 or 96 or 102 or 110 or 111 or 116 or 117 or 131 or 137 or 147 or 158 or 162 or 164 => 1,
            _ => 0
        };
    }

    private static uint LoadingContinuationSeed(int stageIndex, int frameIndex, int length)
    {
        return 0x7f4a7c15u
            ^ ((uint)(stageIndex + 1) * 0x45d9f3bu)
            ^ ((uint)(frameIndex + 1) * 0x119de1f3u)
            ^ (uint)length;
    }

    private static int LoadingContinuationSequenceAdvance(int bodyLength)
    {
        return bodyLength switch
        {
            >= 1000 => 7,
            >= 900 => 6,
            >= 400 => 5,
            >= 128 => 4,
            _ => 3
        };
    }

    private static void AddSteadyStateLinkHeartbeat(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        var shape = SteadyStateLinkHeartbeatShapes[state.SteadyStateLinkHeartbeatIndex % SteadyStateLinkHeartbeatShapes.Length];
        state.SteadyStateLinkHeartbeatIndex++;
        var detail = BuildPlayerStateLinkSlotReplacementBody(
            game,
            player,
            state,
            shape.Length,
            LoadingContinuationSeed(300, state.SteadyStateLinkHeartbeatIndex, shape.Length),
            300 + state.SteadyStateLinkHeartbeatIndex,
            $"steady-heartbeat-{state.SteadyStateLinkHeartbeatIndex}",
            out var body,
            out var builtNativeSnapshot);
        AddPacket(
            responses,
            state,
            body,
            builtNativeSnapshot
                ? "generated PS3 Source native steady state-link heartbeat snapshot/entity-delta"
                : $"generated PS3 Source native steady state-link heartbeat {detail}",
            sequenceAdvance: 3);
    }

    private static void AddSteadyStateDeltaTurn(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player,
        bool includeSemanticBootstrap)
    {
        AdvanceNativeSourceWorldState(game, player);

        if (SteadyStateDeltaStages.Length == 0)
        {
            AddSteadyStateLinkHeartbeat(responses, state, game, player);
            return;
        }

        if (state.SteadyStateLinkHeartbeatIndex == 0)
        {
            AddSteadyStateLinkHeartbeat(responses, state, game, player);
            return;
        }

        var stageIndex = state.SteadyStateLinkHeartbeatIndex % SteadyStateDeltaStages.Length;
        state.SteadyStateLinkHeartbeatIndex++;
        AddLoadingFrames(
            responses,
            state,
            game,
            player,
            SteadyStateDeltaStages[stageIndex],
            200 + stageIndex,
            "generated PS3 Source native steady state delta",
            state.InitialSetupVariant);
        if (includeSemanticBootstrap)
        {
            var useCompactSemanticBootstrap = HasScoreboardSourceState(player, game);
            if (useCompactSemanticBootstrap)
            {
                AddPacket(
                    responses,
                    state,
                    BuildSnapshotCompactPlayerEntityDelta(game, player),
                    "generated PS3 Source native compact steady semantic player entity delta",
                    sequenceAdvance: 3);
                AddPacket(
                    responses,
                    state,
                    BuildSnapshotTinyGameplayRulesDelta(game, player),
                    "generated PS3 Source native compact steady semantic gameplay delta",
                    sequenceAdvance: 3);
                AddPacket(
                    responses,
                    state,
                    BuildSnapshotTinyWeaponEntityDelta(player),
                    "generated PS3 Source native compact steady semantic weapon delta",
                    sequenceAdvance: 3);
                AddPacket(
                    responses,
                    state,
                    BuildSnapshotMicroRoundTimerDelta(game),
                    "generated PS3 Source native compact steady semantic round timer delta",
                    sequenceAdvance: 3);
            }
            else
            {
                AddPacket(
                    responses,
                    state,
                    BuildUrgentStateSnapshotFrameBody(game, player, includeRagdoll: !player.SourceState.Alive),
                    "generated PS3 Source native steady semantic bootstrap snapshot/entity-delta frame",
                    sequenceAdvance: 4,
                    allowFragmentation: true,
                    allowNativeWrap: true);
            }

            state.SentSteadySemanticBootstrapSnapshot = true;
        }

        AddUrgentNativeSourceStateDeltas(responses, state, game, player);
    }

    private static void AddPendingSourceServerCommandEvents(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        PendingSourceServerCommand[] pending;
        lock (game.SyncRoot)
        {
            pending = game.DrainPendingSourceServerCommands(player.Endpoint);
        }

        foreach (var command in pending)
        {
            AddPacket(
                responses,
                state,
                BuildSourceServerCommandEventBody(command),
                command.Type switch
                {
                    SourceServerCommandType.Chat => "generated PS3 Source admin chat event",
                    SourceServerCommandType.TeamChat => "generated PS3 Source admin team chat event",
                    SourceServerCommandType.PrivateChat => "generated PS3 Source admin private chat event",
                    SourceServerCommandType.CenterMessage => "generated PS3 Source admin center message event",
                    SourceServerCommandType.HintMessage => "generated PS3 Source admin hint message event",
                    SourceServerCommandType.PanelMessage => "generated PS3 Source admin panel message event",
                    SourceServerCommandType.HudMessage => "generated PS3 Source admin HUD message event",
                    SourceServerCommandType.Sound => "generated PS3 Source admin sound event",
                    SourceServerCommandType.GameEvent => "generated PS3 Source admin gameplay event",
                    SourceServerCommandType.DeathNotice => "generated PS3 Source admin death notice event",
                    SourceServerCommandType.Vote => "generated PS3 Source admin vote event",
                    SourceServerCommandType.ClearChat => "generated PS3 Source admin clear-chat event",
                    _ => "generated PS3 Source admin console command event"
                },
                sequenceAdvance: 4,
                allowNativeWrap: true);
        }
    }

    private static byte[] BuildQuickMatchTerminalPrompt1Body(GameManagerSession game, PlayerSession player)
    {
        return BuildQuickMatchTerminalPromptBody(game, player, QuickMatchTerminalPrompt1TargetLength, 1);
    }

    private static byte[] BuildQuickMatchTerminalPrompt2Body(GameManagerSession game, PlayerSession player)
    {
        return BuildQuickMatchTerminalPromptBody(game, player, QuickMatchTerminalPrompt2TargetLength, 2);
    }

    private static byte[] BuildQuickMatchTerminalPromptBody(
        GameManagerSession game,
        PlayerSession player,
        int targetLength,
        int promptIndex)
    {
        var stageIndex = promptIndex == 1 ? 180 : 181;
        var seed = (uint)(0x90 + promptIndex);
        if (TryBuildNativeSnapshotBody(game, player, targetLength, seed, stageIndex, out var body)
            && body.Length > 0)
        {
            return body;
        }

        foreach (var candidate in BuildQuickMatchTerminalPromptFallbackCandidates(game, player))
        {
            var packedCandidate = Ps3SourceEntityDeltaFrameBuilder.PackEncodedNativeRecords([candidate]);
            if (packedCandidate.Length <= targetLength
                && Ps3SourceEntityDeltaFrameBuilder.TryDecodeNativeRecords(packedCandidate, out var records, out var consumedBits)
                && records.Length > 0
                && consumedBits > 0)
            {
                return packedCandidate;
            }
        }

        return BuildPostRosterClientObjectBatchAckBody(game, player);
    }

    private static byte[][] BuildQuickMatchTerminalPromptFallbackCandidates(GameManagerSession game, PlayerSession player)
    {
        return
        [
            BuildTinyPromptEntityDeltaRecord(game, player),
            BuildSnapshotMicroRoundTimerDelta(game),
            BuildSnapshotMicroPlayerResourceDelta(game, player),
            BuildSnapshotMicroPlayerEntityDelta(game, player),
            BuildSnapshotMicroObjectiveResourceDelta(game),
            BuildSnapshotNanoGameplayRulesDelta(game, player)
        ];
    }

    private static byte[] BuildTinyPromptEntityDeltaRecord(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        var payload = Encoding.ASCII.GetBytes(
            $"DT_TeamRoundTimer\0m_flTimeRemaining\0{game.MapName}\0{sourceState.TeamNumber}\0");
        return Ps3SourceEntityDeltaFrameBuilder.EncodeNativeRecord(new Ps3SourceEntityDeltaNativeRecordOptions(
            GroupIndex: 5,
            IsPartialRun: true,
            StartIndex: 0,
            EntityCount: 1,
            ObjectId: 0xa2,
            ObjectName: "CTeamRoundTimer",
            QueuedHandle: 0xa2,
            BitLength: payload.Length * 8,
            RawPayload: payload,
            UseNativePartialWindow: true));
    }

    private static bool TryBuildNativeSnapshotBody(
        GameManagerSession game,
        PlayerSession player,
        int targetLength,
        uint seed,
        int stageIndex,
        out byte[] body)
    {
        body = [];
        if (targetLength < 48)
        {
            return false;
        }

        var state = player.NativeSourceResponder;
        var frameIndex = state.SnapshotFrameIndex;
        var baseFrame = state.SnapshotBaseFrame;
        var candidates = BuildCompactSnapshotSectionCandidates(game, player, stageIndex);
        foreach (var sections in candidates)
        {
            if (sections.Length == 0)
            {
                continue;
            }

            var packedSections = PackSnapshotEntityDeltaSections(sections);

            var frame = new Ps3SourceSnapshotFrame(
                frameIndex,
                baseFrame,
                UpdateFlags: 0,
                PendingCount: 1,
                HasEntityDelta: true,
                ExtraSections: packedSections);
            var framePayload = Ps3SourceSnapshotFrameBuilder.Encode(frame);
            var wrapped = Ps3SourceLzss.TryEncode(framePayload, out var compressed)
                ? compressed
                : Ps3SourceLzss.EncodeLiteralStream(framePayload);
            if (wrapped.Length > targetLength)
            {
                var greedy = Ps3SourceLzss.EncodeGreedy(framePayload);
                if (greedy.Length <= targetLength && IsDecodableNativeSnapshotOrEntityDeltaBody(greedy))
                {
                    body = greedy;
                    state.SnapshotBaseFrame = state.SnapshotFrameIndex;
                    state.SnapshotFrameIndex++;
                    return true;
                }

                var directPayload = packedSections.Length == 1
                    ? packedSections[0]
                    : packedSections.SelectMany(static section => section).ToArray();
                if (directPayload.Length > targetLength)
                {
                    continue;
                }

                if (!Ps3SourceEntityDeltaFrameBuilder.TryDecodeNativeRecords(directPayload, out var directRecords, out var directConsumedBits)
                    || directRecords.Length == 0
                    || directConsumedBits <= 0)
                {
                    continue;
                }

                body = directPayload;
                state.SnapshotBaseFrame = state.SnapshotFrameIndex;
                state.SnapshotFrameIndex++;
                return true;
            }

            if (IsDecodableNativeSnapshotOrEntityDeltaBody(wrapped))
            {
                body = wrapped;
                state.SnapshotBaseFrame = state.SnapshotFrameIndex;
                state.SnapshotFrameIndex++;
                return true;
            }
        }

        return false;
    }

    private static bool IsDecodableNativeSnapshotOrEntityDeltaBody(byte[] body)
    {
        var semanticPayload = Ps3SourceLzss.TryDecode(body, out var decoded)
            ? decoded
            : body;
        return Ps3SourceSnapshotFrameBuilder.TryDecode(semanticPayload, out var frame)
                && frame.Header.HasEntityDelta
                && frame.EntityDeltaSection is { Records.Length: > 0 }
            || Ps3SourceEntityDeltaFrameBuilder.TryDecodeNativeRecords(
                semanticPayload,
                out var records,
                out var consumedBits)
                && records.Length > 0
                && consumedBits > 0;
    }

    private static void AddNextObjectStateIntroTurn(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        var batches = BuildObjectStateIntroBatches(game, player, state);
        if (state.ObjectStateIntroBatchIndex >= batches.Length)
        {
            return;
        }

        var startingBatchIndex = state.ObjectStateIntroBatchIndex;
        var batchCounts = ObjectStateIntroBatchCounts(state.InitialSetupVariant);
        var turnIndex = ObjectStateIntroTurnIndex(state.ObjectStateIntroBatchIndex, batchCounts);
        var batchCount = batchCounts[Math.Min(turnIndex, batchCounts.Length - 1)];
        var endIndex = Math.Min(batches.Length, state.ObjectStateIntroBatchIndex + batchCount);
        var suppressAfter = 0;
        for (; state.ObjectStateIntroBatchIndex < endIndex; state.ObjectStateIntroBatchIndex++)
        {
            var batch = batches[state.ObjectStateIntroBatchIndex];
            AddPacket(responses, state, batch.Body, batch.Explanation, batch.SequenceAdvance);
            suppressAfter = Math.Max(suppressAfter, batch.SuppressAfter);
        }

        if (suppressAfter > 0)
        {
            state.SuppressObjectStateIntroResponses = suppressAfter;
        }
    }

    private static int[] ObjectStateIntroBatchCounts(int initialSetupVariant)
    {
        return initialSetupVariant switch
        {
            2 => Variant2ObjectStateIntroTurnBatchCounts,
            3 => Variant3ObjectStateIntroTurnBatchCounts,
            4 => Variant4ObjectStateIntroTurnBatchCounts,
            5 => Variant5ObjectStateIntroTurnBatchCounts,
            _ => ObjectStateIntroTurnBatchCounts
        };
    }

    private static int ObjectStateIntroTurnIndex(int batchIndex, IReadOnlyList<int> batchCounts)
    {
        var remaining = batchIndex;
        for (var i = 0; i < batchCounts.Count; i++)
        {
            if (remaining < batchCounts[i])
            {
                return i;
            }

            remaining -= batchCounts[i];
        }

        return batchCounts.Count - 1;
    }

    private static bool ObjectStateIntroComplete(
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        return state.SentObjectState
            && state.ObjectStateIntroBatchIndex >= ObjectStateIntroBatchLength(state.InitialSetupVariant);
    }

    private static int ObjectStateIntroBatchLength(int initialSetupVariant)
    {
        return initialSetupVariant switch
        {
            2 => 10,
            3 => 13,
            4 => 9,
            5 => 9,
            _ => 10
        };
    }

    private static bool ShouldSendCommandSnapshot(Ps3NativeSourceResponderState state, PlayerSession player)
    {
        if (!state.SentRosterDescriptorState || !player.SourceState.LastClientCommandDecoded)
        {
            return false;
        }

        if (state.ClientPacketCount < MinimumCommandSnapshotClientPacketCount)
        {
            return false;
        }

        if (state.LastCommandSnapshotClientPacketCount == 0)
        {
            return true;
        }

        var useFastCatchUpInterval = player.SourceState.LastClientCommandDecoded
            && player.SourceState.LastClientSourceBodyLength >= 160
            && state.ClientPacketCount <= FastCommandSnapshotMaxClientPacketCount;
        var interval = useFastCatchUpInterval ? FastCommandSnapshotClientPacketInterval : SteadyCommandSnapshotClientPacketInterval;
        return state.ClientPacketCount - state.LastCommandSnapshotClientPacketCount >= interval;
    }

    private static void AddCompactCommandSnapshotSemanticDeltas(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        AddPacket(
            responses,
            state,
            BuildSnapshotCompactPlayerEntityDelta(game, player),
            "generated PS3 Source native compact command semantic player entity delta",
            sequenceAdvance: 3);
        AddPacket(
            responses,
            state,
            BuildSnapshotTinyGameplayRulesDelta(game, player),
            "generated PS3 Source native compact command semantic gameplay delta",
            sequenceAdvance: 3);
        AddPacket(
            responses,
            state,
            BuildSnapshotTinyWeaponEntityDelta(player),
            "generated PS3 Source native compact command semantic weapon delta",
            sequenceAdvance: 3);
        AddPacket(
            responses,
            state,
            BuildSnapshotMicroRoundTimerDelta(game),
            "generated PS3 Source native compact command semantic round timer delta",
            sequenceAdvance: 3);
    }

    private static bool ShouldIncludeSteadySemanticBootstrap(
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        return !state.SentSteadySemanticBootstrapSnapshot
            && !ShouldSendLateLargeCommandOpaqueContinuation(state, player);
    }

    private static bool ShouldSendLateLargeCommandOpaqueContinuation(
        Ps3NativeSourceResponderState state,
        PlayerSession player,
        int clientPayloadLength = 0)
    {
        return !state.SentLateLargeCommandContinuation
            && state.QuickMatchTerminalPromptStage == 0
            && state.PostRosterContinuationStage == 0
            && (clientPayloadLength == 176
                || (player.SourceState.LastClientCommandDecoded
                    && player.SourceState.LastClientSourceBodyLength >= 160))
            && state.ClientPacketCount > FastCommandSnapshotMaxClientPacketCount;
    }

    private static bool ShouldSendQuickMatchTerminalPrompt1(
        Ps3NativeSourceResponderState state,
        PlayerSession player,
        int clientPayloadLength)
    {
        return state.QuickMatchTerminalPromptStage == 0
            && !state.SentQuickMatchTerminalMapLoad
            && !state.SentLateLargeCommandContinuation
            && state.PostRosterContinuationStage == 0
            && clientPayloadLength == 71
            && player.SourceState.LastClientCommandDecoded
            && state.LastCommandSnapshotClientPacketCount > 0
            && state.ClientPacketCount - state.LastCommandSnapshotClientPacketCount >= 3
            && state.ClientPacketCount > FastCommandSnapshotMaxClientPacketCount;
    }

    private static bool ShouldSendQuickMatchTerminalPrompt2(
        Ps3NativeSourceResponderState state,
        int clientPayloadLength)
    {
        return state.QuickMatchTerminalPromptStage == 1
            && !state.SentQuickMatchTerminalMapLoad
            && !state.SentLateLargeCommandContinuation
            && state.PostRosterContinuationStage == 0
            && state.QuickMatchTerminalPrompt1WaitClientPacketCount == 0
            && !IsQuickMatchDirectMapLoadUpload(clientPayloadLength)
            && IsQuickMatchTerminalPrompt1Upload(clientPayloadLength);
    }

    private static bool ShouldSendQuickMatchTerminalMapLoad(
        Ps3NativeSourceResponderState state,
        int clientPayloadLength)
    {
        return !state.SentQuickMatchTerminalMapLoad
            && !state.SentLateLargeCommandContinuation
            && state.PostRosterContinuationStage == 0
            && ((clientPayloadLength == 56 && state.QuickMatchTerminalPromptStage is 1 or 2)
                || (state.QuickMatchTerminalPromptStage == 1
                    && IsQuickMatchTerminalPrompt1Upload(clientPayloadLength)
                    && (state.QuickMatchTerminalPrompt1WaitClientPacketCount > 0
                        || IsQuickMatchDirectMapLoadUpload(clientPayloadLength))));
    }

    private static bool ShouldSendSparseQuickMatchTerminalMapLoadAfterShortAck(
        Ps3NativeSourceResponderState state,
        PlayerSession player,
        Ps3SourceTransportPacket clientPacket)
    {
        return !state.SentQuickMatchTerminalMapLoad
            && !state.SentLateLargeCommandContinuation
            && state.InitialSetupVariant == 1
            && !state.SawInitialClientFrozenStateUpload
            && state.SentLoadingPostBurstContinuation
            && state.SentLoadingMotdEvent
            && state.SentRosterDescriptorState
            && state.PostRosterContinuationStage == 0
            && state.QuickMatchTerminalPromptStage == 0
            && state.LoadingContinuationStage >= 120
            && state.ClientPacketCount >= 69
            && player.SourceState.LastClientSourceSequence >= SparseQuickMatchRosterHandoffSequence
            && IsShortClientControl(clientPacket);
    }

    private static bool ShouldWaitForQuickMatchTerminalUpload(
        Ps3NativeSourceResponderState state,
        int clientPayloadLength)
    {
        return !state.SentQuickMatchTerminalMapLoad
            && !state.SentLateLargeCommandContinuation
            && ((state.QuickMatchTerminalPromptStage == 1
                    && !IsQuickMatchTerminalPrompt1Upload(clientPayloadLength)
                    && clientPayloadLength != 56)
                || (state.QuickMatchTerminalPromptStage == 2
                    && clientPayloadLength != 56));
    }

    private static bool ShouldSendLateLargeCommandPreamble(
        Ps3NativeSourceResponderState state,
        PlayerSession player,
        int clientPayloadLength)
    {
        return !state.SentLateLargeCommandPreamble
            && !state.SentLateLargeCommandContinuation
            && state.QuickMatchTerminalPromptStage == 0
            && state.PostRosterContinuationStage == 0
            && clientPayloadLength == 71
            && player.SourceState.LastClientCommandDecoded
            && state.LastCommandSnapshotClientPacketCount > 0
            && state.ClientPacketCount - state.LastCommandSnapshotClientPacketCount >= 3
            && state.ClientPacketCount > FastCommandSnapshotMaxClientPacketCount;
    }

    private static bool ShouldPreserveLateLargeCommandPreambleSequence(
        Ps3NativeSourceResponderState state,
        int clientPayloadLength)
    {
        return state.SentLateLargeCommandPreamble
            && !state.SentLateLargeCommandContinuation
            && clientPayloadLength == 176;
    }

    private static bool ShouldPreservePostRosterContinuationSequence(
        Ps3NativeSourceResponderState state,
        PlayerSession player,
        Ps3SourceTransportPacket clientPacket)
    {
        return ShouldStartPostRosterContinuation(state, player, clientPacket)
            || (state.PostRosterContinuationStage > 0 && IsShortClientControl(clientPacket));
    }

    private static bool ShouldPreserveQuickMatchTerminalPromptSequence(
        Ps3NativeSourceResponderState state,
        int clientPayloadLength)
    {
        return !state.SentQuickMatchTerminalMapLoad
            && ((state.QuickMatchTerminalPromptStage == 1)
                || (state.QuickMatchTerminalPromptStage == 2
                    && clientPayloadLength is 12 or 56 or 191));
    }

    private static bool IsQuickMatchTerminalPrompt1Upload(int clientPayloadLength)
    {
        return clientPayloadLength is >= 160 and <= 260;
    }

    private static bool IsQuickMatchDirectMapLoadUpload(int clientPayloadLength)
    {
        return clientPayloadLength is >= 220 and <= 260;
    }

    private static bool ShouldTreatLateDirectUploadAsQuickMatchTerminalPrompt2(
        Ps3NativeSourceResponderState state,
        PlayerSession player,
        Ps3SourceTransportPacket clientPacket)
    {
        return state.QuickMatchTerminalPromptStage == 0
            && !state.SentQuickMatchTerminalMapLoad
            && !state.SentLateLargeCommandPreamble
            && !state.SentLateLargeCommandContinuation
            && !state.PendingPostRosterFrozenStateUpload
            && !state.SentPostRosterFrozenStateBatches
            && state.PostRosterContinuationStage == 0
            && IsQuickMatchTerminalPrompt1Upload(clientPacket.PayloadLength)
            && clientPacket.CandidateSequence is >= 0x0440 and <= 0x04c0
            && state.ClientPacketCount > FastCommandSnapshotMaxClientPacketCount
            && state.LastCommandSnapshotClientPacketCount > 0
            && player.SourceState.LastClientCommandDecoded;
    }

    private static bool ShouldSendLateLargeCommandFollowup(
        Ps3NativeSourceResponderState state,
        Ps3SourceTransportPacket clientPacket)
    {
        if (!state.SentLateLargeCommandContinuation
            || state.SentQuickMatchTerminalMapLoad
            || state.SentLateLargeCommandFollowup)
        {
            return false;
        }

        state.LateLargeCommandFollowupClientPacketCount++;
        return state.LateLargeCommandFollowupClientPacketCount >= 3
            || clientPacket.PayloadLength is >= 50 and <= 80;
    }

    private static bool ShouldPacePostTerminalMapLoadStateAck(Ps3NativeSourceResponderState state, int clientPayloadLength)
    {
        return !state.SentPostTerminalMapLoadStateAck
            && (state.SentQuickMatchTerminalMapLoad || state.SentLateLargeCommandFollowup)
            && (state.PostTerminalMapLoadClientPacketCount > 0
                || IsPostTerminalMapLoadStateAckUploadStart(clientPayloadLength));
    }

    private static bool ShouldPreservePostTerminalMapLoadStateAckSequence(Ps3NativeSourceResponderState state, int clientPayloadLength)
    {
        return ShouldPacePostTerminalMapLoadStateAck(state, clientPayloadLength);
    }

    private static bool IsPostTerminalMapLoadStateAckUploadStart(int clientPayloadLength)
    {
        return clientPayloadLength is >= 160 and <= 260;
    }

    private static bool ShouldAcknowledgeReliableAssociation(Ps3NativeSourceResponderState state, PlayerSession player)
    {
        return player.SourceState.LastClientSourceRole == Ps3SourceClientPayloadRole.ReliableAssociationProbe
            && player.SourceState.LastClientSourceReliableAssociationNativeToken is { } token
            && state.LastAcknowledgedReliableAssociationToken != token;
    }

    private static bool ShouldSendObjectState(
        Ps3NativeSourceResponderState state,
        PlayerSession player,
        bool clientHasEmbeddedObjectState)
    {
        if (!state.SentServerInfo)
        {
            return false;
        }

        if (state.InitialSetupVariant == 1 && !state.SentObjectState)
        {
            return state.PendingInitialClientFrozenStateUpload
                    && player.SourceState.LastClientSourceRole == Ps3SourceClientPayloadRole.ShortControlAck
                || player.SourceState.LastClientSourceRole == Ps3SourceClientPayloadRole.ReliableAssociationProbe
                || ShouldStartQuickMatchObjectStateFromLiveCommandCadence(state, player);
        }

        return clientHasEmbeddedObjectState
            || player.SourceState.LastClientSourceRole is Ps3SourceClientPayloadRole.ReliableAssociationProbe
                or Ps3SourceClientPayloadRole.AttachedPlayerPayloadFrame
                or Ps3SourceClientPayloadRole.UserCommandCandidate
                or Ps3SourceClientPayloadRole.BinaryControlPayload
            || state.ClientPacketCount >= 3;
    }

    private static bool ShouldStartQuickMatchObjectStateFromLiveCommandCadence(
        Ps3NativeSourceResponderState state,
        PlayerSession player)
    {
        if (!state.SentQuickMatchSetupContinuation
            || state.PendingInitialClientFrozenStateUpload
            || state.SawInitialClientFrozenStateUpload
            || state.ClientPacketCount < 4)
        {
            return false;
        }

        if (player.SourceState.LastClientCommandDecoded)
        {
            return true;
        }

        if (player.SourceState.LastClientSourceBodyLength is < 38 or > 90)
        {
            return false;
        }

        return player.SourceState.LastClientSourceRole is Ps3SourceClientPayloadRole.SetupControlPayload
                or Ps3SourceClientPayloadRole.UserCommandCandidate
                or Ps3SourceClientPayloadRole.AttachedPlayerControlFrame
                or Ps3SourceClientPayloadRole.AttachedPlayerPayloadFrame
                or Ps3SourceClientPayloadRole.BinaryControlPayload;
    }

    private static bool TryHandleClientFragment(
        Ps3NativeSourceResponderState state,
        ReadOnlySpan<byte> body,
        out byte[] reassembledBody,
        out bool waitingForMoreFragments)
    {
        reassembledBody = [];
        waitingForMoreFragments = false;
        if (!Ps3SourceFragmentHeader.TryDecode(body, out var header))
        {
            return false;
        }

        var baseCounter = header.PacketCounter - header.FragmentIndex;
        var key = (baseCounter, header.TotalCount);
        if (!state.ClientFragmentAssemblies.TryGetValue(key, out var assembly))
        {
            assembly = new Ps3NativeSourceClientFragmentAssembly(header.TotalCount, header.WrappedOrCompressed);
            state.ClientFragmentAssemblies.Add(key, assembly);
        }

        var completed = assembly.Add(
            header.FragmentIndex,
            body[Ps3SourceFragmentHeader.HeaderBytes..],
            header.WrappedOrCompressed);
        if (!completed)
        {
            waitingForMoreFragments = true;
            return false;
        }

        state.ClientFragmentAssemblies.Remove(key);
        var assembled = assembly.Reassemble();
        if ((assembly.WrappedOrCompressed || Ps3SourceLzss.IsWrapped(assembled))
            && Ps3SourceLzss.TryDecode(assembled, out var decoded))
        {
            reassembledBody = decoded;
            return true;
        }

        reassembledBody = assembled;
        return true;
    }

    private static void AddPacket(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        ReadOnlySpan<byte> body,
        string explanation,
        int? sequenceAdvance = null,
        bool allowFragmentation = false,
        bool allowNativeWrap = false)
    {
        var staged = Ps3SourceSendWrapper.StageNativePayload(
            body,
            bitPayload: [],
            bitCount: 0,
            allowCompression: allowNativeWrap);
        var stagedExplanation = staged.WrappedOrCompressed
            ? $"{explanation} native LZSS-wrapped"
            : explanation;
        if (staged.Payload.Length <= FragmentPayloadThresholdBytes)
        {
            responses.Add(BuildPacket(state, staged.Payload, stagedExplanation, sequenceAdvance));
            return;
        }

        var fragmentExplanation = allowFragmentation
            ? stagedExplanation
            : $"{stagedExplanation} auto-fragmented";
        responses.AddRange(BuildQueuedPeerPackets(state, staged.Payload, fragmentExplanation, staged.WrappedOrCompressed));
    }

    private static IReadOnlyList<Ps3NativeSourceResponse> BuildQueuedPeerPackets(
        Ps3NativeSourceResponderState state,
        ReadOnlySpan<byte> body,
        string explanation,
        bool wrappedOrCompressed)
    {
        var packetCounterBase = state.FragmentSequenceCounter;
        var fragments = Ps3SourceFragmentedSend.BuildFragments(
            body,
            QueuedPeerChunkPayloadBytes,
            packetCounterBase,
            wrappedOrCompressed);
        state.FragmentSequenceCounter += checked((uint)fragments.Length);

        var result = new List<Ps3NativeSourceResponse>(fragments.Length);
        foreach (var fragment in fragments)
        {
            result.Add(BuildPacket(
                state,
                fragment.Body,
                $"{explanation} queued peer-channel chunk {result.Count + 1}",
                sequenceAdvance: 1));
        }

        return result;
    }

    private static Ps3NativeSourceResponse BuildPacket(
        Ps3NativeSourceResponderState state,
        ReadOnlySpan<byte> body,
        string explanation,
        int? sequenceAdvance = null)
    {
        var sequence = state.NextServerSequence;
        var payload = Ps3SourceTransportPacket.Encode(sequence, body);
        state.NextServerSequence = (ushort)(sequence + (sequenceAdvance ?? SequenceAdvance(body.Length)));
        state.ServerPacketCount++;
        return new Ps3NativeSourceResponse(payload, explanation);
    }

    private static void AlignServerSequenceToClient(Ps3NativeSourceResponderState state, ushort clientSequence)
    {
        var target = (ushort)(clientSequence + 5);
        var forwardDistanceToTarget = Ps3SourceTransportPacket.SequenceDelta(state.NextServerSequence, target);
        if (forwardDistanceToTarget is > 0 and < 0x8000)
        {
            state.NextServerSequence = target;
        }
    }

    private static int SequenceAdvance(int bodyLength)
    {
        return Math.Max(1, (bodyLength + 3) / 4);
    }

    private static int InitialSetupVariant(ReadOnlySpan<byte> firstClientBody)
    {
        var firstClientBodyLength = firstClientBody.Length;
        var isCustomMatch = firstClientBody.Length >= 4
            && firstClientBody[0] == 0xf4
            && firstClientBody[1] == 0x67
            && firstClientBody[2] == 0x2e;

        if (isCustomMatch)
        {
            return firstClientBodyLength switch
            {
                49 => 4,
                48 => 5,
                _ => 4
            };
        }

        return firstClientBodyLength switch
        {
            50 => 2,
            49 => 3,
            _ => 1
        };
    }

    private static void AddInitialSetupResponses(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player,
        ushort clientSequence)
    {
        AddPacket(responses, state, BuildShortControlBody(clientSequence, state.ClientPacketCount), "generated PS3 Source initial short control/ack");
        switch (state.InitialSetupVariant)
        {
            case 2:
                state.InitialSetupContinuationStage = 1;
                return;
            case 3:
                AddPacket(responses, state, BuildCompactAckWindowBody(game, player, state, clientSequence), "generated PS3 Source native initial compact setup/control");
                state.InitialSetupContinuationStage = 1;
                return;
            case 4:
                state.InitialSetupContinuationStage = 1;
                return;
            case 5:
                AddPacket(responses, state, BuildCompactAckWindowBody(game, player, state, clientSequence), "generated PS3 Source native custom-match initial compact setup/control");
                AddPacket(responses, state, BuildResourceStringTableBody(game, player, 581), "generated PS3 Source custom-match resource/class setup table");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player), "generated PS3 Source custom-match player summary setup tail");
                state.InitialSetupContinuationStage = 1;
                state.SuppressInitialSetupContinuationResponses = 1;
                return;
            default:
                AddPacket(responses, state, BuildCompactAckWindowBody(game, player, state, clientSequence), "generated PS3 Source native initial compact setup/control");
                AddPacket(responses, state, BuildQuickMatchInitialApprovalBody(game, player, state), "generated PS3 Source native quick-match markerless setup approval");
                AddPacket(responses, state, BuildQuickMatchInitialSetupTailBody(game, player, state, 0x4b), "generated PS3 Source native quick-match setup tail");
                AddPacket(responses, state, BuildQuickMatchInitialSetupTailBody(game, player, state, 0x4c), "generated PS3 Source native quick-match setup tail");
                return;
        }
    }

    private void AddInitialSetupContinuationResponses(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        switch (state.InitialSetupVariant)
        {
            case 2:
                AddPacket(responses, state, BuildCompactAckWindowBody(game, player, state, player.SourceState.LastClientSourceSequence), "generated PS3 Source native deferred compact setup/control");
                AddPacket(responses, state, BuildResourceStringTableBody(game, player, 515), "generated PS3 Source deferred resource/class setup table");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player), "generated PS3 Source deferred player summary setup tail");
                break;
            case 3:
                AddPacket(responses, state, BuildResourceStringTableBody(game, player, 562), "generated PS3 Source deferred resource/class setup table");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player), "generated PS3 Source deferred player summary setup tail");
                break;
            case 4:
                AddPacket(responses, state, BuildCompactAckWindowBody(game, player, state, player.SourceState.LastClientSourceSequence), "generated PS3 Source native custom-match deferred compact setup/control");
                AddPacket(responses, state, BuildResourceStringTableBody(game, player, 583), "generated PS3 Source custom-match deferred resource/class setup table");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player), "generated PS3 Source custom-match deferred player summary setup tail");
                AddPacket(
                    responses,
                    state,
                    BuildEmbeddedPlayerObjectBoundaryBody(game, player, state, 81, 7, maxRecords: 2, 0x47, "custom-match-deferred-setup-tail"),
                    "generated PS3 Source native custom-match deferred player-object setup tail");
                break;
            case 5:
                AddCriticalSourceObjectStreamBootstrapResponses(
                    responses,
                    state,
                    game,
                    player,
                    "generated PS3 Source native custom-match deferred server-info/sign-on object-stream bootstrap batch");
                state.SuppressObjectStateIntroResponses = Math.Max(state.SuppressObjectStateIntroResponses, 1);
                state.SentCriticalSourceNetMessageBootstrap = true;
                state.SentServerInfo = true;
                break;
        }

        state.InitialSetupContinuationStage = 0;
    }

    private static byte[] BuildShortControlBody(ushort clientSequence, int packetCount)
    {
        return
        [
            0x49,
            0x31,
            (byte)(clientSequence >> 8),
            (byte)clientSequence,
            (byte)Math.Min(packetCount, 0xff),
            0x00,
            0x00,
            0x00,
            0x00,
            0x00
        ];
    }

    private static byte[] BuildReliableAssociationAckBody(uint token)
    {
        return Ps3SourceReliableAssociationProbe.EncodeAssociationAck(token);
    }

    private static byte[] BuildCompactAckWindowBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        ushort acknowledgedClientSequence,
        byte streamKind = 0x44)
    {
        return Ps3SourceCompactControlFrame.EncodeAckWindow21(
            unchecked((uint)game.GameId),
            unchecked((uint)player.PlayerId),
            acknowledgedClientSequence,
            state.ClientPacketCount,
            state.NextServerSequence,
            streamKind);
    }

    private static byte[] BuildCompactControl17Body(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        byte streamKind)
    {
        return Ps3SourceCompactControlFrame.EncodeControl17(
            unchecked((uint)game.GameId),
            unchecked((uint)player.PlayerId),
            player.SourceState.LastClientSourceSequence,
            state.ClientPacketCount,
            state.NextServerSequence,
            streamKind);
    }

    private static byte[] BuildQuickMatchSetupContinuationBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        return BuildQuickMatchMarkerlessSetupBody(
            game,
            player,
            state,
            112,
            0x4a,
            "quick-match-setup-continuation");
    }

    private static byte[] BuildQuickMatchInitialApprovalBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        return BuildQuickMatchMarkerlessSetupBody(
            game,
            player,
            state,
            573,
            0x45,
            "quick-match-initial-approval");
    }

    private static byte[] BuildQuickMatchInitialSetupTailBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        byte streamKind)
    {
        return BuildQuickMatchMarkerlessSetupBody(
            game,
            player,
            state,
            81,
            streamKind,
            "quick-match-setup-tail");
    }

    private static byte[] BuildQuickMatchMarkerlessSetupBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int length,
        byte streamKind,
        string family)
    {
        var body = new byte[length];
        var mapName = string.IsNullOrWhiteSpace(game.MapName) ? "ctf_2fort" : game.MapName;
        var playerName = SourceDisplayName(player);
        var rootObjectId = RootSourceObjectId(player);
        var localPlayerObjectId = PlayerSourceObjectId(player);
        var mapHash = StableSourceDigest32(mapName);
        var playerHash = StableSourceDigest32(playerName);
        var offset = 0;
        var blockIndex = 0;
        while (offset < body.Length)
        {
            var seed = Encoding.UTF8.GetBytes(string.Create(
                null,
                $"{family}|kind={streamKind:x2}|gid={game.GameId}|pid={player.PlayerId}|root={rootObjectId}|local={localPlayerObjectId}|clientSeq={player.SourceState.LastClientSourceSequence}|serverSeq={state.NextServerSequence}|clientPackets={state.ClientPacketCount}|mapHash={mapHash}|playerHash={playerHash}|block={blockIndex}"));
            var hash = SHA256.HashData(seed);
            hash.AsSpan(0, Math.Min(hash.Length, body.Length - offset)).CopyTo(body.AsSpan(offset));
            offset += hash.Length;
            blockIndex++;
        }

        MixMarkerlessSetupField(body, 3, unchecked((uint)game.GameId));
        MixMarkerlessSetupField(body, 11, unchecked((uint)player.PlayerId));
        MixMarkerlessSetupField(body, 19, rootObjectId);
        MixMarkerlessSetupField(body, 29, localPlayerObjectId);
        MixMarkerlessSetupField(body, 37, player.SourceState.LastClientSourceSequence);
        MixMarkerlessSetupField(body, 43, state.NextServerSequence);
        MixMarkerlessSetupField(body, 53, unchecked((uint)state.ClientPacketCount));
        return body;
    }

    private static void MixMarkerlessSetupField(byte[] body, int offset, uint value)
    {
        if (body.Length < offset + 4)
        {
            return;
        }

        Span<byte> field = stackalloc byte[4];
        PlasmaIntegerCodec.WriteUInt32BigEndian(field, value);
        for (var i = 0; i < field.Length; i++)
        {
            body[offset + i] ^= field[i];
        }
    }

    private static void MixMarkerlessSetupField(byte[] body, int offset, ushort value)
    {
        if (body.Length < offset + 2)
        {
            return;
        }

        Span<byte> field = stackalloc byte[2];
        PlasmaIntegerCodec.WriteUInt16BigEndian(field, value);
        for (var i = 0; i < field.Length; i++)
        {
            body[offset + i] ^= field[i];
        }
    }

    private static byte[] BuildCompactAckTokenBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        byte streamKind)
    {
        return Ps3SourceCompactControlFrame.EncodeAckToken10(
            unchecked((uint)game.GameId),
            unchecked((uint)player.PlayerId),
            player.SourceState.LastClientSourceSequence,
            state.ClientPacketCount,
            state.NextServerSequence,
            streamKind);
    }

    private static byte[] BuildPlayerSummaryBody(GameManagerSession game, PlayerSession player)
    {
        var activePlayers = ActiveSourcePlayers(game).ToArray();
        if (activePlayers.Length == 0)
        {
            activePlayers = [player];
        }

        return Ps3SourceNativeMessages.BuildPlayerSummary(
            (byte)Math.Clamp(activePlayers.Length, 0, 0xff),
            activePlayers
                .Select(candidate => new Ps3SourcePlayerSummaryEntry(
                    SourcePlayerSlotIndex(game, candidate),
                    string.IsNullOrWhiteSpace(candidate.Name) ? "Player" : candidate.Name,
                    ScoreOrStat: checked((int)Math.Min(candidate.SourceState.Score, int.MaxValue)),
                    FloatValue: candidate.SourceState.Health))
                .ToArray());
    }

    private static byte[] BuildResourceStringTableBody(
        GameManagerSession game,
        PlayerSession player,
        int maxEncodedLength,
        bool padToEncodedLength = false)
    {
        var body = Ps3SourceNativeMessages.BuildResourceStringTable(BuildResourceStringEntries(game, maxEncodedLength));
        if (!padToEncodedLength || body.Length >= maxEncodedLength)
        {
            return body;
        }

        var padded = new byte[maxEncodedLength];
        body.CopyTo(padded.AsSpan());
        var seed = unchecked((byte)(0x45 ^ maxEncodedLength ^ game.GameId ^ player.PlayerId));
        FillDeterministic(padded.AsSpan(body.Length), seed, protectedPrefixBytes: 0);
        return padded;
    }

    private static Ps3SourceResourceStringEntry[] BuildResourceStringEntries(GameManagerSession game, int maxEncodedLength)
    {
        var mapName = string.IsNullOrWhiteSpace(game.MapName) ? "ctf_2fort" : game.MapName;
        return Tf2Ps3SourceCatalog.BuildBootstrapResourceStringEntries(mapName, maxEncodedLength);
    }

    private static byte[] BuildLoadingMotdEventBody(GameManagerSession game, PlayerSession player)
    {
        var serverName = string.IsNullOrWhiteSpace(game.Name) ? "A Game" : game.Name;
        var mapName = string.IsNullOrWhiteSpace(game.MapName) ? "ctf_2fort" : game.MapName;
        var playerName = string.IsNullOrWhiteSpace(player.Name) ? "Player" : player.Name;
        return Ps3SourceNativeMessages.BuildFormattedTextEvent($"MOTD {serverName} {mapName} {playerName}");
    }

    private static byte[] BuildSourceServerCommandEventBody(PendingSourceServerCommand command)
    {
        var text = command.Type switch
        {
            SourceServerCommandType.Chat => $"SayText2 {command.IssuedBy}: {command.Text}",
            SourceServerCommandType.TeamChat => $"SayText2 [TEAM] {command.IssuedBy}: {command.Text}",
            SourceServerCommandType.PrivateChat => $"SayText2 [PM] {command.IssuedBy}: {command.Text}",
            SourceServerCommandType.CenterMessage => $"CenterPrint {command.Text}",
            SourceServerCommandType.HintMessage => $"HintText {command.Text}",
            SourceServerCommandType.PanelMessage => $"PanelText title=\"{EscapeSourceText(command.Text)}\"",
            SourceServerCommandType.HudMessage => $"HudMsg {command.Text}",
            SourceServerCommandType.Sound => $"EmitSound {command.Text}",
            SourceServerCommandType.GameEvent => $"GameEvent {command.Text}",
            SourceServerCommandType.DeathNotice => $"DeathNotice {command.Text}",
            SourceServerCommandType.Vote => $"Vote {command.Text}",
            SourceServerCommandType.ClearChat => "ClearChat",
            _ => $"SERVER_COMMAND {command.Text}"
        };
        return Ps3SourceNativeMessages.BuildFormattedTextEvent(text);
    }

    private static string EscapeSourceText(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static byte[] BuildDeterministicBinaryBody(GameManagerSession game, PlayerSession player, int length, byte seed)
    {
        var body = new byte[length];
        FillDeterministic(body, seed, protectedPrefixBytes: 0);
        if (length >= 32)
        {
            body[0] = seed;
            body[1] = (byte)(game.GameId >> 8);
            body[2] = (byte)game.GameId;
            body[3] = (byte)player.PlayerId;
        }

        return body;
    }

    private static SourceObjectStateBatch[] BuildLateLargeCommandPrepBatches(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        _ = BuildPlayerStateLinkSlotReplacementBody(
            game,
            player,
            state,
            602,
            0xb1,
            220,
            "late-large-command-prep-segment-a",
            out var segmentA,
            out _);
        _ = BuildPlayerStateLinkSlotReplacementBody(
            game,
            player,
            state,
            606,
            0xb2,
            221,
            "late-large-command-prep-segment-b",
            out var segmentB,
            out _);
        return
        [
            new SourceObjectStateBatch(
                segmentA,
                SequenceAdvance: 145,
                Explanation: "native PS3 Source late large-command prep snapshot/entity-delta segment A"),
            new SourceObjectStateBatch(
                segmentB,
                SequenceAdvance: 143,
                Explanation: "native PS3 Source late large-command prep snapshot/entity-delta segment B")
        ];
    }

    private static byte[] BuildLateLargeCommandFollowupBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        _ = BuildPlayerStateLinkSlotReplacementBody(
            game,
            player,
            state,
            860,
            0xb3,
            222,
            "late-large-command-followup",
            out var body,
            out _);
        return body;
    }

    private static byte[] BuildPostTerminalMapLoadStateAckBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        _ = BuildPlayerStateLinkSlotReplacementBody(
            game,
            player,
            state,
            122,
            0xb4,
            223,
            "post-terminal-map-load-state-ack",
            out var body,
            out _);
        return body;
    }

    private static byte[] BuildPostRosterClientObjectBatchAckBody(GameManagerSession game, PlayerSession player)
    {
        var state = player.NativeSourceResponder;
        AdvanceNativeSourceWorldState(game, player);
        var entityDeltas = PackSnapshotEntityDeltaSections(
            [
                BuildSnapshotMicroPlayerResourceDelta(game, player),
                BuildSnapshotMicroPlayerEntityDelta(game, player),
                BuildSnapshotMicroGameplayRulesDelta(game, player),
                BuildSnapshotMicroRoundTimerDelta(game)
            ]);
        var frame = new Ps3SourceSnapshotFrame(
            state.SnapshotFrameIndex,
            state.SnapshotBaseFrame,
            UpdateFlags: 0x01,
            PendingCount: 1,
            HasEntityDelta: true,
            ExtraSections: entityDeltas);
        state.SnapshotBaseFrame = state.SnapshotFrameIndex;
        state.SnapshotFrameIndex++;
        return Ps3SourceSnapshotFrameBuilder.Encode(frame);
    }

    private static byte[] BuildPostRosterShortGameplayAckBody(GameManagerSession game, PlayerSession player)
    {
        return BuildPostRosterClientObjectBatchAckBody(game, player);
    }

    private static void WritePlayerStateLinkTail(byte[] body, ReadOnlySpan<uint> objectIds, uint linkedObjectId)
    {
        var records = new Ps3SourcePlayerStateLinkRecord[objectIds.Length];
        for (var index = 0; index < objectIds.Length; index++)
        {
            records[index] = new Ps3SourcePlayerStateLinkRecord(objectIds[index], linkedObjectId);
        }

        var tail = Ps3SourcePlayerStateLinkRecord.BuildBatch(records);
        if (tail.Length > body.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(objectIds), "player state-link tail is larger than the packet body");
        }

        tail.CopyTo(body.AsSpan(body.Length - tail.Length));
    }

    private static byte[] BuildNativeQueuedPlayerStateLinkBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int length,
        int prefixLength,
        IReadOnlyList<uint> objectIds,
        uint linkedObjectId,
        string family)
    {
        if (prefixLength < 0 || prefixLength > length)
        {
            throw new ArgumentOutOfRangeException(nameof(prefixLength), "queued state-link prefix must fit in the packet body");
        }

        var records = objectIds
            .Select(objectId => new Ps3SourcePlayerStateLinkRecord(objectId, linkedObjectId))
            .ToArray();
        var tail = Ps3SourcePlayerStateLinkRecord.BuildBatch(records);
        if (prefixLength + tail.Length > length)
        {
            throw new ArgumentOutOfRangeException(nameof(objectIds), "queued state-link records do not fit after the prefix");
        }

        var body = new byte[length];
        var seed = StableSourceDigest32(BuildQueuedPlayerStateLinkSeed(game, player, state, family, objectIds, linkedObjectId));
        WriteQueuedBoundaryBytes(body.AsSpan(0, prefixLength), game, player, state, (byte)seed, family);
        tail.CopyTo(body.AsSpan(prefixLength, tail.Length));
        if (prefixLength + tail.Length < length)
        {
            WriteQueuedBoundaryBytes(
                body.AsSpan(prefixLength + tail.Length),
                game,
                player,
                state,
                (byte)(seed >> 8),
                family + ":tail");
        }

        return body;
    }

    private static byte[] BuildNativeQueuedLoadingPlayerStateLinkBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int length,
        int prefixLength,
        int maxRecords,
        string family)
    {
        var offset = Math.Min(prefixLength, length);
        var availableRecordCount = Math.Max(0, (length - offset) / Ps3SourcePlayerStateLinkRecord.Length);
        var recordCount = Math.Min(maxRecords, availableRecordCount);
        var objectIds = RepeatingFrozenStateObjectIds(player, recordCount).ToArray();
        return BuildNativeQueuedPlayerStateLinkBody(
            game,
            player,
            state,
            length,
            prefixLength,
            objectIds,
            RootSourceObjectId(player),
            family);
    }

    private static byte[] BuildQueuedBoundaryOnlyBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int length,
        byte streamKind,
        string family)
    {
        var body = new byte[length];
        WriteQueuedBoundaryBytes(body, game, player, state, streamKind, family);
        return body;
    }

    private static string BuildQueuedPlayerStateLinkSeed(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        string family,
        IReadOnlyList<uint> objectIds,
        uint linkedObjectId)
    {
        var seed = new StringBuilder()
            .Append(family)
            .Append(':')
            .Append(game.GameId)
            .Append(':')
            .Append(game.ServerUid)
            .Append(':')
            .Append(game.UniqueGameId)
            .Append(':')
            .Append(game.MapName)
            .Append(':')
            .Append(player.PlayerId)
            .Append(':')
            .Append(PlayerSourceObjectId(player))
            .Append(':')
            .Append(RootSourceObjectId(player))
            .Append(':')
            .Append(state.NextServerSequence)
            .Append(':')
            .Append(linkedObjectId)
            .Append(':');
        foreach (var objectId in objectIds)
        {
            seed.Append(objectId).Append(',');
        }

        return seed.ToString();
    }

    private static SourceObjectStateBatch[] BuildObjectStateIntroBatches(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        return state.InitialSetupVariant switch
        {
            2 => BuildVariant2ObjectStateIntroBatches(game, player, state),
            3 => BuildVariant3ObjectStateIntroBatches(game, player, state),
            4 => BuildVariant4ObjectStateIntroBatches(game, player, state),
            5 => BuildVariant5ObjectStateIntroBatches(game, player, state),
            _ => BuildStandardObjectStateIntroBatches(game, player, state)
        };
    }

    private static SourceObjectStateBatch[] BuildStandardObjectStateIntroBatches(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        var playerDisplayName = SourceDisplayName(player);
        var peers = FrozenStateNamedPeers(player);
        var introPeers = peers.Take(4).ToArray();
        var secondaryPeers = peers.Skip(4).Take(2).ToArray();
        if (secondaryPeers.Length == 0)
        {
            secondaryPeers = [(PlayerSourceObjectId(player), playerDisplayName)];
        }
        var hostRefreshPeers = FrozenStateNamedPeersById(player, 0x9c, 0x6d);
        if (hostRefreshPeers.Length < 2)
        {
            hostRefreshPeers = secondaryPeers.Length >= 2 ? secondaryPeers : peers.Take(2).ToArray();
        }

        return
        [
            new(
                BuildFrozenStateObjectBody(game, player, 173, 25, introPeers),
                7,
                "generated PS3 Source native COc FrozenStateObject intro batch"),
            new(
                BuildFrozenStateObjectBody(game, player, 78, 4, [(PlayerSourceObjectId(player), playerDisplayName), .. secondaryPeers.Take(1)]),
                3,
                "generated PS3 Source native COc FrozenStateObject player batch"),
            new(
                BuildFrozenStateObjectBody(game, player, 60, 23, [(PlayerSourceObjectId(player), playerDisplayName)]),
                6,
                "generated PS3 Source native COc FrozenStateObject focus batch"),
            new(
                BuildFrozenStateObjectBody(game, player, 78, 4, hostRefreshPeers),
                1,
                "generated PS3 Source native COc FrozenStateObject host batch"),
            new(
                BuildDeterministicBinaryBody(game, player, 32, 0xc1),
                8,
                "generated PS3 Source native object-state binary boundary"),
            new(
                BuildCompactAckWindowBody(game, player, state, player.SourceState.LastClientSourceSequence, 0x44),
                6,
                "generated PS3 Source native object-state compact ack/window refresh"),
            new(
                BuildNativeQueuedLoadingPlayerStateLinkBody(game, player, state, 49, 25, 2, "object-intro-state-link-a"),
                7,
                "generated PS3 Source native PNG state-link intro batch"),
            new(
                BuildNativeQueuedLoadingPlayerStateLinkBody(game, player, state, 63, 15, 4, "object-intro-state-link-b"),
                7,
                "generated PS3 Source native PNG state-link mesh batch"),
            new(
                BuildNativeQueuedLoadingPlayerStateLinkBody(game, player, state, 116, 20, 8, "object-intro-state-link-c"),
                7,
                "generated PS3 Source native PNG state-link mesh extension batch"),
            new(
                BuildNativeQueuedLoadingPlayerStateLinkBody(game, player, state, 28, 4, 2, "object-intro-state-link-tail"),
                3,
                "generated PS3 Source native PNG state-link object-intro tail")
        ];
    }

    private static SourceObjectStateBatch[] BuildVariant2ObjectStateIntroBatches(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        return
        [
            new(BuildCompactControl17Body(game, player, state, 0x51), 1, "generated PS3 Source variant2 object-intro compact control boundary"),
            new(BuildCompactControl17Body(game, player, state, 0x52), 1, "generated PS3 Source variant2 object-intro compact control boundary"),
            new(BuildFrozenStateObjectBody(game, player, 78, 4, FrozenStateNamedPeers(player).Take(2).ToArray()), 3, "generated PS3 Source variant2 COc FrozenStateObject batch"),
            new(BuildFrozenStateObjectBody(game, player, 198, 24, FrozenStateNamedPeers(player).Take(4).ToArray()), 7, "generated PS3 Source variant2 COc FrozenStateObject batch"),
            new(BuildCompactAckWindowBody(game, player, state, player.SourceState.LastClientSourceSequence, 0x53), 1, "generated PS3 Source variant2 object-intro compact ack/window boundary"),
            new(BuildFrozenStateObjectBody(game, player, 49, 9, FrozenStateNamedPeers(player).Take(1).ToArray()), 3, "generated PS3 Source variant2 COc FrozenStateObject tail", SuppressAfter: 1),
            new(BuildFrozenStateObjectBody(game, player, 159, 21, FrozenStateNamedPeers(player).Take(3).ToArray()), 6, "generated PS3 Source variant2 COc FrozenStateObject tail"),
            new(BuildEmbeddedFrozenStateBoundaryBody(game, player, state, 53, 16, maxRecords: 1, 0x54, "variant2-frozen-state-boundary"), 3, "generated PS3 Source variant2 COc FrozenStateObject native boundary"),
            new(BuildEmbeddedPlayerStateLinkBoundaryBody(game, player, state, 102, 18, maxRecords: 7, 0x55, "variant2-object-state-links"), 4, "generated PS3 Source variant2 PNG object-state link boundary"),
            new(BuildPurePlayerStateLinkBody(player, 2), 3, "generated PS3 Source variant2 PNG state-link object-intro tail without generated prefix")
        ];
    }

    private static SourceObjectStateBatch[] BuildVariant3ObjectStateIntroBatches(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        return
        [
            new(BuildFrozenStateObjectBody(game, player, 73, 7, FrozenStateNamedPeers(player).Take(2).ToArray()), 3, "generated PS3 Source variant3 COc FrozenStateObject batch"),
            new(BuildFrozenStateObjectBody(game, player, 195, 23, FrozenStateNamedPeers(player).Take(4).ToArray()), 7, "generated PS3 Source variant3 COc FrozenStateObject batch"),
            new(BuildEmbeddedPlayerStateLinkBoundaryBody(game, player, state, 56, 8, maxRecords: 4, 0x61, "variant3-object-state-links-a"), 3, "generated PS3 Source variant3 PNG object-state link boundary"),
            new(BuildCompactControl17Body(game, player, state, 0x62), 1, "generated PS3 Source variant3 object-intro compact control boundary"),
            new(BuildPurePlayerStateLinkBody(player, 8), 7, "generated PS3 Source variant3 PNG state-link mesh extension batch without generated prefix"),
            new(BuildEmbeddedFrozenStateBoundaryBody(game, player, state, 74, 0, maxRecords: 2, 0x63, "variant3-frozen-state-boundary-a"), 4, "generated PS3 Source variant3 COc FrozenStateObject boundary"),
            new(BuildCompactAckWindowBody(game, player, state, player.SourceState.LastClientSourceSequence, 0x64), 1, "generated PS3 Source variant3 object-intro compact ack/window boundary"),
            new(BuildFrozenStateObjectBody(game, player, 159, 21, FrozenStateNamedPeers(player).Take(3).ToArray()), 6, "generated PS3 Source variant3 COc FrozenStateObject tail", SuppressAfter: 1),
            new(BuildPurePlayerStateLinkBody(player, 2), 3, "generated PS3 Source variant3 PNG state-link object-intro tail without generated prefix"),
            new(BuildEmbeddedFrozenStateBoundaryBody(game, player, state, 53, 16, maxRecords: 1, 0x65, "variant3-frozen-state-boundary-b"), 3, "generated PS3 Source variant3 COc FrozenStateObject native boundary"),
            new(BuildEmbeddedFrozenStateBoundaryBody(game, player, state, 53, 16, maxRecords: 1, 0x66, "variant3-frozen-state-boundary-c"), 3, "generated PS3 Source variant3 COc FrozenStateObject native boundary"),
            new(BuildEmbeddedPlayerStateLinkBoundaryBody(game, player, state, 102, 18, maxRecords: 7, 0x67, "variant3-object-state-links-b"), 4, "generated PS3 Source variant3 PNG object-state link boundary"),
            new(BuildEmbeddedPlayerStateLinkBoundaryBody(game, player, state, 56, 8, maxRecords: 4, 0x68, "variant3-object-state-links-c"), 3, "generated PS3 Source variant3 PNG object-state link boundary")
        ];
    }

    private static SourceObjectStateBatch[] BuildVariant4ObjectStateIntroBatches(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        return
        [
            new(BuildCompactControl17Body(game, player, state, 0x71), 1, "generated PS3 Source custom-match variant4 object-intro compact control boundary"),
            new(BuildPlayerObjectBody(game, player, 138, 27), 6, "generated PS3 Source custom-match variant4 player-object batch"),
            new(BuildPlayerObjectBody(game, player, 177, 25), 7, "generated PS3 Source custom-match variant4 player-object batch"),
            new(BuildCompactAckWindowBody(game, player, state, player.SourceState.LastClientSourceSequence, 0x72), 1, "generated PS3 Source custom-match variant4 object-intro compact ack/window boundary"),
            new(BuildPlayerObjectBody(game, player, 81, 7), 3, "generated PS3 Source custom-match variant4 player-object tail"),
            new(BuildPlayerObjectBody(game, player, 106, 4), 4, "generated PS3 Source custom-match variant4 roster-object tail"),
            new(BuildPlayerObjectBody(game, player, 120, 9), 4, "generated PS3 Source custom-match variant4 player-object tail"),
            new(BuildEmbeddedFrozenStateBoundaryBody(game, player, state, 60, 23, maxRecords: 1, 0x73, "variant4-frozen-state-boundary"), 3, "generated PS3 Source custom-match variant4 COc FrozenStateObject boundary"),
            new(BuildPurePlayerStateLinkBody(player, 5), 4, "generated PS3 Source custom-match variant4 PNG state-link boundary without generated prefix")
        ];
    }

    private static SourceObjectStateBatch[] BuildVariant5ObjectStateIntroBatches(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        return
        [
            new(BuildCompactControl17Body(game, player, state, 0x81), 1, "generated PS3 Source custom-match variant5 object-intro compact control boundary"),
            new(BuildCompactControl17Body(game, player, state, 0x82), 1, "generated PS3 Source custom-match variant5 object-intro compact control boundary"),
            new(BuildPlayerObjectBody(game, player, 294, 18), 8, "generated PS3 Source custom-match variant5 player-object batch"),
            new(BuildPlayerObjectBody(game, player, 99, 12), 4, "generated PS3 Source custom-match variant5 player-object tail"),
            new(BuildCompactAckTokenBody(game, player, state, 0x83), 1, "generated PS3 Source custom-match variant5 object-intro compact ack-token boundary"),
            new(BuildPlayerObjectBody(game, player, 77, 8), 4, "generated PS3 Source custom-match variant5 roster-object batch"),
            new(BuildPurePlayerStateLinkBody(player, 6), 4, "generated PS3 Source custom-match variant5 PNG state-link boundary without generated prefix"),
            new(BuildPurePlayerStateLinkBody(player, 2), 3, "generated PS3 Source custom-match variant5 PNG state-link boundary without generated prefix"),
            new(BuildEmbeddedPlayerStateLinkBoundaryBody(game, player, state, 32, 8, maxRecords: 2, 0x84, "variant5-object-state-links"), 2, "generated PS3 Source custom-match variant5 PNG object-state link boundary")
        ];
    }

    private static SourceObjectStateBatch[] BuildPostRosterFrozenStateObjectBatches(GameManagerSession game, PlayerSession player)
    {
        var playerDisplayName = SourceDisplayName(player);
        var peers = FrozenStateNamedPeers(player);
        var introPeers = peers.Take(4).ToArray();
        var secondaryPeers = peers.Skip(4).Take(2).ToArray();
        if (secondaryPeers.Length == 0)
        {
            secondaryPeers = [(PlayerSourceObjectId(player), playerDisplayName)];
        }

        return
        [
            new(
                BuildFrozenStateObjectBody(game, player, 173, 25, introPeers),
                7,
                "generated PS3 Source native COc FrozenStateObject intro batch"),
            new(
                BuildFrozenStateObjectBody(game, player, 78, 4, [(PlayerSourceObjectId(player), playerDisplayName), .. secondaryPeers.Take(1)]),
                3,
                "generated PS3 Source native COc FrozenStateObject player batch"),
            new(
                BuildFrozenStateObjectBody(game, player, 60, 23, [(PlayerSourceObjectId(player), playerDisplayName)]),
                6,
                "generated PS3 Source native COc FrozenStateObject focus batch")
        ];
    }

    private static (uint ObjectId, string Name)[] FrozenStateNamedPeers(PlayerSession player)
    {
        var peers = player.SourceState.FrozenStatePeerObjectIds.Length == 0
            ? [0x9fU, 0x93U, 0x95U, 0x9cU, 0x6dU]
            : player.SourceState.FrozenStatePeerObjectIds;
        return peers
            .Select((objectId, index) => (objectId, SourcePeerDisplayName(player, objectId, index)))
            .ToArray();
    }

    private static (uint ObjectId, string Name)[] FrozenStateNamedPeersById(PlayerSession player, params uint[] objectIds)
    {
        var peers = FrozenStateNamedPeers(player)
            .GroupBy(static peer => peer.ObjectId)
            .ToDictionary(static group => group.Key, static group => group.First().Name);
        return objectIds
            .Where(peers.ContainsKey)
            .Select(objectId => (objectId, peers[objectId]))
            .ToArray();
    }

    private static string SourceDisplayName(PlayerSession player)
    {
        return SanitizeSourceObjectName(player.Name, $"player{Math.Max(1, player.PlayerId)}");
    }

    private static string SourcePeerDisplayName(PlayerSession player, uint objectId, int index)
    {
        if (objectId == PlayerSourceObjectId(player) || objectId == RootSourceObjectId(player))
        {
            return SourceDisplayName(player);
        }

        return SanitizeSourceObjectName($"obj_{objectId:x8}", $"peer{index + 1}");
    }

    private static string SanitizeSourceObjectName(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        Span<char> buffer = stackalloc char[21];
        var count = 0;
        var replacementCount = 0;
        var plausibleCount = 0;
        foreach (var ch in value.Trim())
        {
            if (ch < 0x20 || ch > 0x7e)
            {
                continue;
            }

            if (ch == '?')
            {
                replacementCount++;
            }

            if (char.IsLetterOrDigit(ch) || ch is '_' or '-' or '[' or ']' or '(' or ')' or ' ')
            {
                plausibleCount++;
            }

            buffer[count++] = ch;
            if (count >= buffer.Length)
            {
                break;
            }
        }

        if (count == 0
            || plausibleCount < Math.Max(2, count / 2)
            || replacementCount > Math.Max(0, count / 4))
        {
            return fallback;
        }

        return new string(buffer[..count]).Trim();
    }

    private static byte[] BuildFrozenStateObjectBody(
        GameManagerSession game,
        PlayerSession player,
        int length,
        int prefixLength,
        IReadOnlyList<(uint ObjectId, string Name)> records)
    {
        var body = new byte[length];
        FillDeterministic(body.AsSpan(0, Math.Min(prefixLength, body.Length)), (byte)(0x5a ^ game.GameId ^ player.PlayerId), 0);

        var offset = Math.Min(prefixLength, body.Length);
        foreach (var record in records)
        {
            if (offset + 37 > body.Length)
            {
                break;
            }

            WriteFrozenStateObject(body.AsSpan(offset, 37), record.ObjectId, RootSourceObjectId(player), record.Name);
            offset += 37;
        }

        if (offset < body.Length)
        {
            FillDeterministic(body.AsSpan(offset), (byte)(0x71 ^ game.GameId ^ player.PlayerId), 0);
        }

        return body;
    }

    private static byte[] BuildPlayerStateLinkBody(GameManagerSession game, PlayerSession player, int length, int prefixLength, int maxRecords = int.MaxValue)
    {
        var body = new byte[length];
        FillDeterministic(body.AsSpan(0, Math.Min(prefixLength, body.Length)), (byte)(0x33 ^ game.GameId ^ player.PlayerId), 0);
        var offset = Math.Min(prefixLength, body.Length);
        var availableRecordCount = Math.Max(0, (body.Length - offset) / Ps3SourcePlayerStateLinkRecord.Length);
        var recordCount = Math.Min(maxRecords, availableRecordCount);
        var records = FrozenStateObjectIds(player)
            .Take(recordCount)
            .Select(objectId => new Ps3SourcePlayerStateLinkRecord(objectId, RootSourceObjectId(player)))
            .ToArray();
        var batch = Ps3SourcePlayerStateLinkRecord.BuildBatch(records);
        batch.CopyTo(body.AsSpan(offset));
        offset += batch.Length;

        if (offset < body.Length)
        {
            FillDeterministic(body.AsSpan(offset), (byte)(0x49 ^ game.GameId ^ player.PlayerId), 0);
        }

        return body;
    }

    private static byte[] BuildEmbeddedFrozenStateBoundaryBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int length,
        int prefixLength,
        int maxRecords,
        byte streamKind,
        string family)
    {
        var body = new byte[length];
        var offset = Math.Min(prefixLength, body.Length);
        WriteQueuedBoundaryBytes(body.AsSpan(0, offset), game, player, state, streamKind, family);

        foreach (var record in FrozenStateNamedPeers(player).Take(maxRecords))
        {
            if (offset + Ps3SourceEmbeddedObjectWireRecord.FrozenStateObjectLength > body.Length)
            {
                break;
            }

            WriteFrozenStateObject(
                body.AsSpan(offset, Ps3SourceEmbeddedObjectWireRecord.FrozenStateObjectLength),
                record.ObjectId,
                RootSourceObjectId(player),
                record.Name);
            offset += Ps3SourceEmbeddedObjectWireRecord.FrozenStateObjectLength;
        }

        WriteQueuedBoundaryBytes(body.AsSpan(offset), game, player, state, (byte)(streamKind ^ 0x80), family + ":tail");
        return body;
    }

    private static byte[] BuildEmbeddedPlayerStateLinkBoundaryBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int length,
        int prefixLength,
        int maxRecords,
        byte streamKind,
        string family)
    {
        var body = new byte[length];
        var offset = Math.Min(prefixLength, body.Length);
        WriteQueuedBoundaryBytes(body.AsSpan(0, offset), game, player, state, streamKind, family);

        var linkedObjectId = RootSourceObjectId(player);
        foreach (var objectId in RepeatingFrozenStateObjectIds(player, maxRecords))
        {
            if (offset + Ps3SourcePlayerStateLinkRecord.Length > body.Length)
            {
                break;
            }

            WritePlayerStateLink(body.AsSpan(offset, Ps3SourcePlayerStateLinkRecord.Length), objectId, linkedObjectId);
            offset += Ps3SourcePlayerStateLinkRecord.Length;
        }

        WriteQueuedBoundaryBytes(body.AsSpan(offset), game, player, state, (byte)(streamKind ^ 0x80), family + ":tail");
        return body;
    }

    private static byte[] BuildEmbeddedPlayerObjectBoundaryBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        int length,
        int prefixLength,
        int maxRecords,
        byte streamKind,
        string family)
    {
        var body = new byte[length];
        var offset = Math.Min(prefixLength, body.Length);
        WriteQueuedBoundaryBytes(body.AsSpan(0, offset), game, player, state, streamKind, family);

        foreach (var record in BuildPlayerObjectRecords(game, player).Take(maxRecords))
        {
            if (offset + Ps3SourceEmbeddedObjectWireRecord.PlayerObjectLength > body.Length)
            {
                break;
            }

            WritePlayerObject(
                body.AsSpan(offset, Ps3SourceEmbeddedObjectWireRecord.PlayerObjectLength),
                record.ObjectId,
                record.ParentId,
                record.Name);
            offset += Ps3SourceEmbeddedObjectWireRecord.PlayerObjectLength;
        }

        WriteQueuedBoundaryBytes(body.AsSpan(offset), game, player, state, (byte)(streamKind ^ 0x80), family + ":tail");
        return body;
    }

    private static void WriteQueuedBoundaryBytes(
        Span<byte> destination,
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        byte streamKind,
        string family)
    {
        if (destination.Length == 0)
        {
            return;
        }

        var seed = StableSourceDigest32(
            $"{family}:{streamKind:x2}:{game.GameId}:{game.ServerUid}:{player.PlayerId}:{player.Name}:{state.ClientPacketCount}:{state.NextServerSequence}:{player.SourceState.LastClientSourceSequence}");
        var baseOffset = ChooseLowPrintablePermutationOffset(destination.Length, seed, streamKind);
        for (var i = 0; i < destination.Length; i++)
        {
            var blockOffset = unchecked(baseOffset + (int)((seed >> ((i >> 8) % 4 * 8)) & 0xff) + ((i >> 8) * 73));
            destination[i] = (byte)(((i & 0xff) * 149 + blockOffset) & 0xff);
        }
    }

    private static int ChooseLowPrintablePermutationOffset(int length, uint seed, byte streamKind)
    {
        if (length <= 0)
        {
            return 0;
        }

        var preferred = (int)((seed ^ streamKind) & 0xff);
        var bestOffset = preferred;
        var bestPrintable = int.MaxValue;
        for (var candidate = 0; candidate < 256; candidate++)
        {
            var offset = (preferred + candidate) & 0xff;
            var printable = 0;
            for (var i = 0; i < length; i++)
            {
                var value = (byte)(((i & 0xff) * 149 + offset) & 0xff);
                if (value is >= 0x20 and <= 0x7e)
                {
                    printable++;
                }
            }

            if (printable >= bestPrintable)
            {
                continue;
            }

            bestPrintable = printable;
            bestOffset = offset;
            if (printable * 100 < length * 30)
            {
                break;
            }
        }

        return bestOffset;
    }

    private static byte[] BuildLateLargeCommandPreambleBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        return BuildCompactPlayerStateLinkPulseBody(
            game,
            player,
            state,
            [0x0000001fu, 0x00000014u],
            0x0000001du,
            "late-large-command-preamble");
    }

    private static byte[] BuildCompactPlayerStateLinkPulseBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state,
        IReadOnlyList<uint> objectIds,
        uint linkedObjectId,
        string family)
    {
        var records = objectIds
            .Select(objectId => new Ps3SourcePlayerStateLinkRecord(objectId, linkedObjectId))
            .ToArray();
        var body = new byte[4 + (records.Length * Ps3SourcePlayerStateLinkRecord.Length)];
        var seed = new StringBuilder()
            .Append(family)
            .Append(':')
            .Append(game.GameId)
            .Append(':')
            .Append(player.PlayerId)
            .Append(':')
            .Append(state.NextServerSequence)
            .Append(':')
            .Append(player.Name)
            .Append(':')
            .Append(linkedObjectId)
            .Append(':');
        foreach (var record in records)
        {
            seed.Append(record.ObjectId).Append(',');
        }

        WriteUInt32BigEndian(body.AsSpan(0, 4), StableSourceDigest32(seed.ToString()));
        Ps3SourcePlayerStateLinkRecord.BuildBatch(records).CopyTo(body.AsSpan(4));
        return body;
    }

    private static byte[] BuildPlayerObjectBody(GameManagerSession game, PlayerSession player, int length, int prefixLength)
    {
        var body = new byte[length];
        FillDeterministic(body.AsSpan(0, Math.Min(prefixLength, body.Length)), (byte)(0x27 ^ game.GameId ^ player.PlayerId), 0);
        var offset = Math.Min(prefixLength, body.Length);
        foreach (var record in BuildPlayerObjectRecords(game, player))
        {
            if (offset + 37 > body.Length)
            {
                break;
            }

            WritePlayerObject(body.AsSpan(offset, 37), record.ObjectId, record.ParentId, record.Name);
            offset += 37;
        }

        if (offset < body.Length)
        {
            FillDeterministic(body.AsSpan(offset), (byte)(0x2d ^ game.GameId ^ player.PlayerId), 0);
        }

        return body;
    }

    private static (uint ObjectId, uint ParentId, string Name)[] BuildPlayerObjectRecords(GameManagerSession game, PlayerSession recipient)
    {
        var rootObjectId = RootSourceObjectId(recipient);
        var activePlayers = ActiveSourcePlayers(game).ToArray();
        if (activePlayers.Length == 0)
        {
            activePlayers = [recipient];
        }

        var records = new List<(uint ObjectId, uint ParentId, string Name)>();
        foreach (var candidate in activePlayers)
        {
            records.Add((
                PlayerSourceObjectId(candidate),
                rootObjectId,
                SourceDisplayName(candidate)));
        }

        records.Add((rootObjectId, PlayerSourceObjectId(recipient), SourceDisplayName(recipient)));
        return records.ToArray();
    }

    private static byte[] BuildPlayerDescriptorBody(GameManagerSession game, PlayerSession player, int length, int prefixLength)
    {
        var body = new byte[length];
        FillDeterministic(body.AsSpan(0, Math.Min(prefixLength, body.Length)), (byte)(0x39 ^ game.GameId ^ player.PlayerId), 0);
        var offset = Math.Min(prefixLength, body.Length);
        foreach (var record in BuildPlayerDescriptorRecords(game, player))
        {
            if (offset + 16 > body.Length)
            {
                break;
            }

            WritePlayerDescriptor(body.AsSpan(offset, 16), record.ObjectId, record.LinkedId);
            offset += 16;
        }

        if (offset < body.Length)
        {
            FillDeterministic(body.AsSpan(offset), (byte)(0x41 ^ game.GameId ^ player.PlayerId), 0);
        }

        return body;
    }

    private static (uint ObjectId, uint LinkedId)[] BuildPlayerDescriptorRecords(GameManagerSession game, PlayerSession recipient)
    {
        var rootObjectId = RootSourceObjectId(recipient);
        var activePlayers = ActiveSourcePlayers(game).ToArray();
        if (activePlayers.Length == 0)
        {
            activePlayers = [recipient];
        }

        return activePlayers
            .Select(candidate => (ObjectId: PlayerSourceObjectId(candidate), LinkedId: rootObjectId))
            .Concat([(ObjectId: rootObjectId, LinkedId: PlayerSourceObjectId(recipient))])
            .ToArray();
    }

    private static byte[][][] BuildCompactSnapshotSectionCandidates(GameManagerSession game, PlayerSession player, int stageIndex)
    {
        var playerResource = BuildSnapshotPlayerResourceDelta(game, player);
        var tinyPlayerResource = BuildSnapshotTinyPlayerResourceDelta(game, player);
        var microPlayerResource = BuildSnapshotMicroPlayerResourceDelta(game, player);
        var playerEntity = BuildSnapshotCompactPlayerEntityDelta(game, player);
        var tinyPlayerEntity = BuildSnapshotTinyPlayerEntityDelta(game, player);
        var microPlayerEntity = BuildSnapshotMicroPlayerEntityDelta(game, player);
        var weaponEntity = BuildSnapshotWeaponEntityDelta(player);
        var roundTimer = BuildSnapshotRoundTimerDelta(game);
        var microRoundTimer = BuildSnapshotMicroRoundTimerDelta(game);
        var gameplayRules = BuildSnapshotCompactGameplayRulesDelta(game, player);
        var tinyGameplayRules = BuildSnapshotTinyGameplayRulesDelta(game, player);
        var microGameplayRules = BuildSnapshotMicroGameplayRulesDelta(game, player);
        var nanoGameplayRules = BuildSnapshotNanoGameplayRulesDelta(game, player);
        var objective = BuildSnapshotCompactObjectiveResourceDelta(game);
        var tinyObjective = BuildSnapshotTinyObjectiveResourceDelta(game);
        var microObjective = BuildSnapshotMicroObjectiveResourceDelta(game);
        var tinyObjectiveGameplay = BuildSnapshotTinyObjectiveGameplayDelta(game, player);
        var healthPriority = player.SourceState.Health < player.SourceState.MaxHealth || !player.SourceState.Alive
            ? new byte[][][] { [microPlayerResource], [microPlayerEntity], [tinyPlayerResource] }
            : [];
        var roundEndPriority = game.World.RoundState >= 5
            ? (stageIndex % 3) switch
            {
                0 => new byte[][][] { [microGameplayRules], [microRoundTimer], [microObjective], [microPlayerResource] },
                1 => new byte[][][] { [microRoundTimer], [microObjective], [microGameplayRules], [microPlayerResource] },
                _ => new byte[][][] { [microObjective], [microGameplayRules], [microRoundTimer], [microPlayerResource] }
            }
            : [];
        var statusEvent = Ps3SourceNativeMessages.BuildFormattedTextEvent(
            $"{(string.IsNullOrWhiteSpace(player.Name) ? "Player" : player.Name)} active on {game.MapName}");

        return (stageIndex % 6) switch
        {
            0 =>
            [
                .. roundEndPriority,
                .. healthPriority,
                [microPlayerResource],
                [microRoundTimer],
                [microObjective],
                [tinyObjectiveGameplay],
                [nanoGameplayRules, tinyObjective],
                [tinyObjective],
                [objective],
                [playerResource, gameplayRules, objective, roundTimer],
                [gameplayRules, objective, roundTimer],
                [gameplayRules, roundTimer],
                [playerResource, playerEntity, weaponEntity, roundTimer],
                [playerResource, playerEntity, roundTimer],
                [playerResource, roundTimer],
                [gameplayRules],
                [playerResource],
                [tinyGameplayRules, tinyPlayerEntity],
                [tinyGameplayRules],
                [tinyPlayerEntity],
                [microGameplayRules],
                [nanoGameplayRules],
                [microPlayerResource],
                [tinyPlayerResource],
                [microPlayerEntity],
            ],
            1 =>
            [
                .. roundEndPriority,
                .. healthPriority,
                [microGameplayRules],
                [microPlayerResource],
                [microRoundTimer],
                [microObjective],
                [tinyObjectiveGameplay],
                [nanoGameplayRules, tinyObjective],
                [tinyObjective],
                [playerResource, gameplayRules, statusEvent],
                [gameplayRules, statusEvent],
                [gameplayRules],
                [playerResource, playerEntity, statusEvent],
                [playerResource, playerEntity],
                [playerResource, statusEvent],
                [playerResource],
                [tinyGameplayRules],
                [tinyPlayerEntity],
                [microGameplayRules],
                [nanoGameplayRules],
                [microPlayerResource],
                [tinyPlayerResource],
                [microPlayerEntity],
            ],
            2 =>
            [
                .. roundEndPriority,
                .. healthPriority,
                [microRoundTimer],
                [microPlayerResource],
                [microObjective],
                [tinyObjectiveGameplay],
                [nanoGameplayRules, tinyObjective],
                [tinyObjective],
                [objective],
                [playerResource, objective, roundTimer],
                [objective, roundTimer],
                [gameplayRules],
                [playerResource, weaponEntity, roundTimer],
                [playerResource, weaponEntity],
                [playerResource, roundTimer],
                [playerResource],
                [tinyGameplayRules, tinyObjective],
                [tinyGameplayRules],
                [tinyObjective],
                [microGameplayRules, microPlayerEntity],
                [microGameplayRules],
                [nanoGameplayRules],
                [microPlayerResource],
                [tinyPlayerResource],
                [microPlayerEntity],
            ],
            3 =>
            [
                .. roundEndPriority,
                .. healthPriority,
                [microPlayerResource],
                [microObjective],
                [microGameplayRules],
                [microRoundTimer],
                [tinyObjectiveGameplay],
                [nanoGameplayRules, tinyObjective],
                [tinyObjective],
                [objective],
                [playerEntity, gameplayRules, roundTimer],
                [gameplayRules, roundTimer],
                [gameplayRules],
                [playerEntity, weaponEntity, roundTimer],
                [playerEntity, roundTimer],
                [playerEntity],
                [playerResource],
                [tinyPlayerEntity, tinyGameplayRules],
                [tinyGameplayRules],
                [tinyPlayerEntity],
                [microGameplayRules],
                [nanoGameplayRules],
                [tinyPlayerResource],
                [microPlayerEntity],
            ],
            4 =>
            [
                .. roundEndPriority,
                .. healthPriority,
                [microObjective],
                [tinyObjectiveGameplay],
                [microPlayerResource],
                [microRoundTimer],
                [nanoGameplayRules, tinyObjective],
                [tinyObjective],
                [objective],
                [playerResource, playerEntity, gameplayRules],
                [playerEntity, gameplayRules],
                [gameplayRules],
                [playerResource, playerEntity, weaponEntity],
                [playerResource, playerEntity],
                [playerResource, weaponEntity],
                [playerResource],
                [tinyGameplayRules, tinyObjective],
                [tinyGameplayRules],
                [tinyObjective],
                [microGameplayRules],
                [nanoGameplayRules],
                [microPlayerResource],
                [tinyPlayerResource],
                [microPlayerEntity],
                [microPlayerEntity]
            ],
            _ =>
            [
                .. roundEndPriority,
                .. healthPriority,
                [microGameplayRules],
                [microPlayerResource],
                [microRoundTimer],
                [microObjective],
                [tinyObjectiveGameplay],
                [nanoGameplayRules, tinyObjective],
                [tinyObjective],
                [objective],
                [playerResource, objective, statusEvent],
                [objective, statusEvent],
                [playerResource, roundTimer, statusEvent],
                [playerResource, roundTimer],
                [playerResource, statusEvent],
                [playerResource],
                [tinyPlayerEntity, tinyObjective],
                [tinyObjective],
                [tinyPlayerEntity],
                [microGameplayRules],
                [nanoGameplayRules],
                [microPlayerResource],
                [tinyPlayerResource],
                [microPlayerEntity],
            ]
        };
    }

    private static byte[] BuildSnapshotPlayerResourceDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildPlayerResourceDelta(new Ps3SourcePlayerResourceDelta(
            SourcePlayerSlotIndex(game, player),
            Health: sourceState.Health,
            Rating: 0,
            RatingDelta: 0,
            Connected: true,
            ObjectId: PlayerSourceObjectId(player),
            StatusText: $"{(string.IsNullOrWhiteSpace(player.Name) ? "Player" : player.Name)} connected to {game.MapName}",
            Score: sourceState.Score,
            Deaths: sourceState.Deaths,
            Team: sourceState.TeamNumber,
            Alive: sourceState.Alive));
    }

    private static byte[] BuildSnapshotTinyPlayerResourceDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildTinyPlayerResourceDelta(new Ps3SourcePlayerResourceDelta(
            SourcePlayerSlotIndex(game, player),
            Health: sourceState.Health,
            Rating: 0,
            RatingDelta: 0,
            Connected: true,
            ObjectId: PlayerSourceObjectId(player),
            StatusText: string.Empty,
            Score: sourceState.Score,
            Deaths: sourceState.Deaths,
            Team: sourceState.TeamNumber,
            Alive: sourceState.Alive));
    }

    private static byte[] BuildSnapshotMicroPlayerResourceDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildMicroPlayerResourceDelta(new Ps3SourcePlayerResourceDelta(
            SourcePlayerSlotIndex(game, player),
            Health: sourceState.Health,
            Rating: 0,
            RatingDelta: 0,
            Connected: true,
            ObjectId: PlayerSourceObjectId(player),
            StatusText: string.Empty,
            Score: sourceState.Score,
            Deaths: sourceState.Deaths,
            Team: sourceState.TeamNumber,
            Alive: sourceState.Alive));
    }

    private static byte[] BuildSnapshotPlayerEntityDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildPlayerEntityDelta(new Ps3SourcePlayerEntityDelta(
            SourcePlayerSlotIndex(game, player),
            ObjectId: PlayerSourceObjectId(player),
            TeamNumber: (byte)Math.Clamp(sourceState.TeamNumber, 0, 0xff),
            Health: sourceState.Health,
            LifeState: sourceState.Alive ? (byte)0 : (byte)1,
            Flags: sourceState.Flags,
            ClassNumber: sourceState.ClassNumber,
            OriginX: sourceState.OriginX,
            OriginY: sourceState.OriginY,
            OriginZ: sourceState.OriginZ,
            RotationPitch: 0,
            RotationYaw: sourceState.Yaw,
            RotationRoll: 0,
            EyePitch: sourceState.Pitch,
            EyeYaw: sourceState.Yaw,
            SimulationTime: sourceState.SimulationTime,
            ModelIndex: sourceState.ModelIndex,
            Effects: sourceState.Effects,
            RenderMode: sourceState.RenderMode,
            RenderFx: sourceState.RenderFx,
            RenderColor: sourceState.RenderColor,
            MoveType: sourceState.MoveType,
            MoveCollide: sourceState.MoveCollide,
            CollisionGroup: sourceState.CollisionGroup,
            CollisionMinsX: -24,
            CollisionMinsY: -24,
            CollisionMinsZ: 0,
            CollisionMaxsX: 24,
            CollisionMaxsY: 24,
            CollisionMaxsZ: 82,
            SolidType: sourceState.SolidType,
            SolidFlags: sourceState.SolidFlags,
            SurroundType: 1,
            TextureFrameIndex: sourceState.TextureFrameIndex,
            Sequence: sourceState.Sequence,
            ForceBone: sourceState.ForceBone,
            ForceX: sourceState.ForceX,
            ForceY: sourceState.ForceY,
            ForceZ: sourceState.ForceZ,
            Skin: sourceState.Skin,
            Body: sourceState.Body,
            HitboxSet: sourceState.HitboxSet,
            ModelWidthScale: sourceState.ModelWidthScale,
            PlaybackRate: sourceState.PlaybackRate,
            ClientSideAnimation: sourceState.ClientSideAnimation,
            ClientSideFrameReset: sourceState.ClientSideFrameReset,
            NewSequenceParity: sourceState.NewSequenceParity,
            ResetEventsParity: sourceState.ResetEventsParity,
            MuzzleFlashParity: sourceState.MuzzleFlashParity,
            LightingOriginHandle: sourceState.LightingOriginHandle,
            LightingOriginRelativeHandle: sourceState.LightingOriginRelativeHandle,
            ServerAnimationCycle: sourceState.ServerAnimationCycle,
            FadeMinDistance: sourceState.FadeMinDistance,
            FadeMaxDistance: sourceState.FadeMaxDistance,
            FadeScale: sourceState.FadeScale));
    }

    private static byte[] BuildSnapshotCompactPlayerEntityDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildCompactPlayerEntityDelta(new Ps3SourcePlayerEntityDelta(
            SourcePlayerSlotIndex(game, player),
            ObjectId: PlayerSourceObjectId(player),
            TeamNumber: (byte)Math.Clamp(sourceState.TeamNumber, 0, 0xff),
            Health: sourceState.Health,
            LifeState: sourceState.Alive ? (byte)0 : (byte)1,
            Flags: sourceState.Flags,
            ClassNumber: sourceState.ClassNumber,
            OriginX: sourceState.OriginX,
            OriginY: sourceState.OriginY,
            OriginZ: sourceState.OriginZ,
            RotationPitch: 0,
            RotationYaw: sourceState.Yaw,
            RotationRoll: 0,
            EyePitch: sourceState.Pitch,
            EyeYaw: sourceState.Yaw,
            SimulationTime: sourceState.SimulationTime,
            MoveType: sourceState.MoveType,
            MoveCollide: sourceState.MoveCollide,
            CollisionGroup: sourceState.CollisionGroup,
            SolidType: sourceState.SolidType,
            SolidFlags: sourceState.SolidFlags));
    }

    private static byte[] BuildSnapshotTinyPlayerEntityDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildTinyPlayerEntityDelta(new Ps3SourcePlayerEntityDelta(
            SourcePlayerSlotIndex(game, player),
            ObjectId: PlayerSourceObjectId(player),
            TeamNumber: (byte)Math.Clamp(sourceState.TeamNumber, 0, 0xff),
            Health: sourceState.Health,
            LifeState: sourceState.Alive ? (byte)0 : (byte)1,
            Flags: sourceState.Flags,
            ClassNumber: sourceState.ClassNumber,
            OriginX: sourceState.OriginX,
            OriginY: sourceState.OriginY,
            OriginZ: sourceState.OriginZ,
            RotationPitch: 0,
            RotationYaw: sourceState.Yaw,
            RotationRoll: 0,
            EyePitch: sourceState.Pitch,
            EyeYaw: sourceState.Yaw,
            SimulationTime: sourceState.SimulationTime,
            MoveType: sourceState.MoveType,
            MoveCollide: sourceState.MoveCollide,
            CollisionGroup: sourceState.CollisionGroup,
            SolidType: sourceState.SolidType,
            SolidFlags: sourceState.SolidFlags));
    }

    private static byte[] BuildSnapshotMicroPlayerEntityDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildMicroPlayerEntityDelta(new Ps3SourcePlayerEntityDelta(
            SourcePlayerSlotIndex(game, player),
            ObjectId: PlayerSourceObjectId(player),
            TeamNumber: (byte)Math.Clamp(sourceState.TeamNumber, 0, 0xff),
            Health: sourceState.Health,
            LifeState: sourceState.Alive ? (byte)0 : (byte)1,
            Flags: sourceState.Flags,
            ClassNumber: sourceState.ClassNumber,
            OriginX: sourceState.OriginX,
            OriginY: sourceState.OriginY,
            OriginZ: sourceState.OriginZ,
            RotationPitch: 0,
            RotationYaw: sourceState.Yaw,
            RotationRoll: 0,
            EyePitch: sourceState.Pitch,
            EyeYaw: sourceState.Yaw,
            SimulationTime: sourceState.SimulationTime,
            MoveType: sourceState.MoveType,
            MoveCollide: sourceState.MoveCollide,
            CollisionGroup: sourceState.CollisionGroup,
            SolidType: sourceState.SolidType,
            SolidFlags: sourceState.SolidFlags));
    }

    private static byte[] BuildSnapshotWeaponEntityDelta(PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildWeaponEntityDelta(new Ps3SourceWeaponEntityDelta(
            ObjectId: sourceState.ActiveWeaponHandle,
            OwnerHandle: PlayerSourceObjectId(player),
            State: sourceState.WeaponState,
            ViewModelIndex: sourceState.WeaponViewModelIndex,
            WorldModelIndex: sourceState.WeaponWorldModelIndex,
            NextPrimaryAttack: sourceState.NextPrimaryAttack,
            NextSecondaryAttack: sourceState.NextSecondaryAttack,
            TimeWeaponIdle: sourceState.TimeWeaponIdle,
            PrimaryAmmoType: sourceState.PrimaryAmmoType,
            SecondaryAmmoType: sourceState.SecondaryAmmoType,
            Clip1: sourceState.WeaponClip1,
            Clip2: sourceState.WeaponClip2,
            Lowered: sourceState.WeaponLowered,
            ReloadMode: sourceState.WeaponReloadMode,
            ResetParity: sourceState.WeaponResetParity,
            ReloadedThroughAnimEvent: sourceState.WeaponReloadedThroughAnimEvent,
            InReload: sourceState.WeaponInReload,
            FireOnEmpty: sourceState.WeaponFireOnEmpty,
            NextEmptySoundTime: sourceState.WeaponNextEmptySoundTime,
            BuildState: sourceState.WeaponBuildState,
            BuildObjectType: sourceState.WeaponBuildObjectType,
            ObjectBeingBuiltHandle: sourceState.WeaponObjectBeingBuiltHandle,
            TfWeaponState: sourceState.TfWeaponState,
            CritFire: sourceState.WeaponCritFire,
            Healing: sourceState.WeaponHealing,
            Attacking: sourceState.WeaponAttacking,
            ChargeRelease: sourceState.WeaponChargeRelease,
            Holstered: sourceState.WeaponHolstered,
            HealingTargetHandle: sourceState.WeaponHealingTargetHandle,
            HealEffectLifetime: sourceState.WeaponHealEffectLifetime,
            ChargeLevel: sourceState.WeaponChargeLevel,
            BottleBroken: sourceState.WeaponBottleBroken,
            PipebombCount: sourceState.WeaponPipebombCount,
            ChargeBeginTime: sourceState.WeaponChargeBeginTime,
            SoonestPrimaryAttack: sourceState.WeaponSoonestPrimaryAttack,
            MinigunCritShot: sourceState.WeaponMinigunCritShot));
    }

    private static byte[] BuildSnapshotTinyWeaponEntityDelta(PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildTinyWeaponEntityDelta(new Ps3SourceWeaponEntityDelta(
            ObjectId: sourceState.ActiveWeaponHandle,
            OwnerHandle: PlayerSourceObjectId(player),
            State: sourceState.WeaponState,
            ViewModelIndex: sourceState.WeaponViewModelIndex,
            WorldModelIndex: sourceState.WeaponWorldModelIndex,
            NextPrimaryAttack: sourceState.NextPrimaryAttack,
            NextSecondaryAttack: sourceState.NextSecondaryAttack,
            TimeWeaponIdle: sourceState.TimeWeaponIdle,
            PrimaryAmmoType: sourceState.PrimaryAmmoType,
            SecondaryAmmoType: sourceState.SecondaryAmmoType,
            Clip1: sourceState.WeaponClip1,
            Clip2: sourceState.WeaponClip2,
            Lowered: sourceState.WeaponLowered,
            ReloadMode: sourceState.WeaponReloadMode,
            ResetParity: sourceState.WeaponResetParity,
            ReloadedThroughAnimEvent: sourceState.WeaponReloadedThroughAnimEvent,
            InReload: sourceState.WeaponInReload,
            FireOnEmpty: sourceState.WeaponFireOnEmpty,
            NextEmptySoundTime: sourceState.WeaponNextEmptySoundTime,
            BuildState: sourceState.WeaponBuildState,
            BuildObjectType: sourceState.WeaponBuildObjectType,
            ObjectBeingBuiltHandle: sourceState.WeaponObjectBeingBuiltHandle,
            TfWeaponState: sourceState.TfWeaponState,
            CritFire: sourceState.WeaponCritFire,
            Healing: sourceState.WeaponHealing,
            Attacking: sourceState.WeaponAttacking,
            ChargeRelease: sourceState.WeaponChargeRelease,
            Holstered: sourceState.WeaponHolstered,
            HealingTargetHandle: sourceState.WeaponHealingTargetHandle,
            HealEffectLifetime: sourceState.WeaponHealEffectLifetime,
            ChargeLevel: sourceState.WeaponChargeLevel,
            BottleBroken: sourceState.WeaponBottleBroken,
            PipebombCount: sourceState.WeaponPipebombCount,
            ChargeBeginTime: sourceState.WeaponChargeBeginTime,
            SoonestPrimaryAttack: sourceState.WeaponSoonestPrimaryAttack,
            MinigunCritShot: sourceState.WeaponMinigunCritShot));
    }

    private static byte[] BuildSnapshotRoundTimerDelta(GameManagerSession game)
    {
        var timer = game.World.Timer;
        return Ps3SourceNativeMessages.BuildTeamRoundTimerDelta(new Ps3SourceTeamRoundTimerDelta(
            ObjectId: 0xa2,
            TimerPaused: timer.TimerPaused,
            TimeRemaining: timer.TimeRemainingSeconds,
            TimerEndTime: timer.TimerEndTimeSeconds,
            TimerMaxLength: timer.TimerMaxLengthSeconds,
            IsDisabled: timer.IsDisabled,
            ShowInHud: timer.ShowInHud,
            TimerLength: timer.TimerLengthSeconds,
            TimerInitialLength: timer.TimerInitialLengthSeconds,
            AutoCountdown: timer.AutoCountdown,
            SetupTimeLength: timer.SetupTimeLengthSeconds,
            State: timer.State,
            StartPaused: timer.StartPaused));
    }

    private static byte[] BuildSnapshotMicroRoundTimerDelta(GameManagerSession game)
    {
        var timer = game.World.Timer;
        return Ps3SourceNativeMessages.BuildMicroTeamRoundTimerDelta(new Ps3SourceTeamRoundTimerDelta(
            ObjectId: 0xa2,
            TimerPaused: timer.TimerPaused,
            TimeRemaining: timer.TimeRemainingSeconds,
            TimerEndTime: timer.TimerEndTimeSeconds,
            TimerMaxLength: timer.TimerMaxLengthSeconds,
            IsDisabled: timer.IsDisabled,
            ShowInHud: timer.ShowInHud,
            TimerLength: timer.TimerLengthSeconds,
            TimerInitialLength: timer.TimerInitialLengthSeconds,
            AutoCountdown: timer.AutoCountdown,
            SetupTimeLength: timer.SetupTimeLengthSeconds,
            State: timer.State,
            StartPaused: timer.StartPaused));
    }

    private static byte[] BuildSnapshotCompactGameplayRulesDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        var world = game.World;
        var timer = world.Timer;
        var sourceTeam = world.Team(sourceState.TeamNumber);
        return Ps3SourceNativeMessages.BuildCompactGameplayRulesDelta(new Ps3SourceGameplayStateDelta(
            SourcePlayerSlotIndex(game, player),
            ObjectId: RootSourceObjectId(player),
            Ammo: [sourceState.PrimaryAmmo, sourceState.SecondaryAmmo, sourceState.Metal],
            Fov: sourceState.Fov,
            FovStart: sourceState.FovStart,
            FovTime: sourceState.FovTime,
            DefaultFov: sourceState.DefaultFov,
            ObserverMode: sourceState.ObserverMode,
            ObserverTargetHandle: sourceState.ObserverTargetHandle,
            ViewModelHandles: [sourceState.ViewModelHandle0, sourceState.ViewModelHandle1],
            ViewOffsetX: sourceState.ViewOffsetX,
            ViewOffsetY: sourceState.ViewOffsetY,
            ViewOffsetZ: sourceState.ViewOffsetZ,
            Friction: sourceState.Friction,
            TickBase: sourceState.TickBase,
            NextThinkTick: sourceState.NextThinkTick,
            GroundEntityHandle: sourceState.GroundEntityHandle,
            MaxHealth: sourceState.MaxHealth,
            PlayerClass: sourceState.ClassNumber,
            RoundState: world.RoundState,
            WinningTeam: world.WinningTeam,
            InSetup: world.InSetup,
            InOvertime: world.InOvertime,
            GameType: world.GameType,
            WeaponHandles: [sourceState.ActiveWeaponHandle],
            ActiveWeaponHandle: sourceState.ActiveWeaponHandle,
            LastWeaponHandle: 0,
            ObjectUpgradeLevel: sourceState.SentryGun.UpgradeLevel,
            ObjectState: sourceState.SentryGun.State,
            ObjectAmmoShells: sourceState.SentryGun.AmmoShells,
            ObjectAmmoRockets: sourceState.SentryGun.AmmoRockets,
            ObjectUpgradeMetal: sourceState.SentryGun.UpgradeMetal,
            TeleporterState: sourceState.Teleporter.State,
            TeleporterRechargeTime: sourceState.Teleporter.RechargeTime,
            TeleporterTimesUsed: sourceState.Teleporter.TimesUsed,
            TeleporterYawToExit: sourceState.Teleporter.YawToExit,
            MaxSpeed: sourceState.MaxSpeed,
            VelocityX: sourceState.VelocityX,
            VelocityY: sourceState.VelocityY,
            VelocityZ: sourceState.VelocityZ,
            BaseVelocityX: sourceState.BaseVelocityX,
            BaseVelocityY: sourceState.BaseVelocityY,
            BaseVelocityZ: sourceState.BaseVelocityZ,
            DeathTime: sourceState.DeathTime,
            WaterLevel: sourceState.WaterLevel,
            Ducked: sourceState.Ducked,
            Ducking: sourceState.Ducking,
            InDuckJump: sourceState.InDuckJump,
            FallVelocity: sourceState.FallVelocity,
            PunchAngleX: sourceState.PunchAngleX,
            PunchAngleY: sourceState.PunchAngleY,
            PunchAngleZ: sourceState.PunchAngleZ,
            PunchAngleVelocityX: sourceState.PunchAngleVelocityX,
            PunchAngleVelocityY: sourceState.PunchAngleVelocityY,
            PunchAngleVelocityZ: sourceState.PunchAngleVelocityZ,
            DrawViewModel: sourceState.DrawViewModel,
            AllowAutoMovement: sourceState.AllowAutoMovement,
            RagdollHandle: sourceState.RagdollHandle,
            SpawnCounter: sourceState.SpawnCounter,
            TotalScore: sourceState.Score,
            Deaths: sourceState.Deaths,
            Captures: sourceState.Captures,
            Defenses: sourceState.Defenses,
            Dominations: sourceState.Dominations,
            Revenge: sourceState.Revenge,
            BuildingsDestroyed: sourceState.BuildingsDestroyed,
            Headshots: sourceState.Headshots,
            Backstabs: sourceState.Backstabs,
            HealPoints: sourceState.HealPoints,
            Invulns: sourceState.Invulns,
            Teleports: sourceState.Teleports,
            ResupplyPoints: sourceState.ResupplyPoints,
            KillAssists: sourceState.KillAssists,
            InWaitingForPlayers: world.InWaitingForPlayers ? (byte)1 : (byte)0,
            AwaitingReadyRestart: world.AwaitingReadyRestart ? (byte)1 : (byte)0,
            RestartRoundTime: timer.RestartRoundTimeSeconds,
            MapResetTime: timer.MapResetTimeSeconds,
            SourceTeamNumber: sourceState.TeamNumber,
            SourceTeamScore: sourceTeam.Score,
            SourceTeamRoundsWon: sourceTeam.RoundsWon,
            SourceTeamName: sourceTeam.Name,
            TeamFlagCaptures: sourceTeam.FlagCaptures,
            TeamRole: sourceState.TeamNumber,
            Jumping: sourceState.Jumping,
            PlayerState: sourceState.PlayerState,
            DisguiseTeam: sourceState.DisguiseTeam,
            DisguiseClass: sourceState.DisguiseClass,
            DisguiseTargetIndex: sourceState.DisguiseTargetIndex,
            DisguiseHealth: sourceState.DisguiseHealth));
    }

    private static byte[] BuildSnapshotTinyGameplayRulesDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        var world = game.World;
        var timer = world.Timer;
        var sourceTeam = world.Team(sourceState.TeamNumber);
        return Ps3SourceNativeMessages.BuildTinyGameplayRulesDelta(new Ps3SourceGameplayStateDelta(
            SourcePlayerSlotIndex(game, player),
            ObjectId: RootSourceObjectId(player),
            Ammo: [sourceState.PrimaryAmmo, sourceState.SecondaryAmmo, sourceState.Metal],
            Fov: sourceState.Fov,
            FovStart: sourceState.FovStart,
            FovTime: sourceState.FovTime,
            DefaultFov: sourceState.DefaultFov,
            ObserverMode: sourceState.ObserverMode,
            ObserverTargetHandle: sourceState.ObserverTargetHandle,
            ViewModelHandles: [sourceState.ViewModelHandle0, sourceState.ViewModelHandle1],
            ViewOffsetX: sourceState.ViewOffsetX,
            ViewOffsetY: sourceState.ViewOffsetY,
            ViewOffsetZ: sourceState.ViewOffsetZ,
            Friction: sourceState.Friction,
            TickBase: sourceState.TickBase,
            NextThinkTick: sourceState.NextThinkTick,
            GroundEntityHandle: sourceState.GroundEntityHandle,
            MaxHealth: sourceState.MaxHealth,
            PlayerClass: sourceState.ClassNumber,
            RoundState: world.RoundState,
            WinningTeam: world.WinningTeam,
            InSetup: world.InSetup,
            InOvertime: world.InOvertime,
            GameType: world.GameType,
            WeaponHandles: [sourceState.ActiveWeaponHandle],
            ActiveWeaponHandle: sourceState.ActiveWeaponHandle,
            LastWeaponHandle: 0,
            ObjectUpgradeLevel: sourceState.SentryGun.UpgradeLevel,
            ObjectState: sourceState.SentryGun.State,
            ObjectAmmoShells: sourceState.SentryGun.AmmoShells,
            ObjectAmmoRockets: sourceState.SentryGun.AmmoRockets,
            ObjectUpgradeMetal: sourceState.SentryGun.UpgradeMetal,
            TeleporterState: sourceState.Teleporter.State,
            TeleporterRechargeTime: sourceState.Teleporter.RechargeTime,
            TeleporterTimesUsed: sourceState.Teleporter.TimesUsed,
            TeleporterYawToExit: sourceState.Teleporter.YawToExit,
            MaxSpeed: sourceState.MaxSpeed,
            DeathTime: sourceState.DeathTime,
            Ducked: sourceState.Ducked,
            Ducking: sourceState.Ducking,
            InDuckJump: sourceState.InDuckJump,
            FallVelocity: sourceState.FallVelocity,
            DrawViewModel: sourceState.DrawViewModel,
            AllowAutoMovement: sourceState.AllowAutoMovement,
            RagdollHandle: sourceState.RagdollHandle,
            SpawnCounter: sourceState.SpawnCounter,
            TotalScore: sourceState.Score,
            Deaths: sourceState.Deaths,
            Captures: sourceState.Captures,
            Defenses: sourceState.Defenses,
            Dominations: sourceState.Dominations,
            Revenge: sourceState.Revenge,
            BuildingsDestroyed: sourceState.BuildingsDestroyed,
            Headshots: sourceState.Headshots,
            Backstabs: sourceState.Backstabs,
            HealPoints: sourceState.HealPoints,
            Invulns: sourceState.Invulns,
            Teleports: sourceState.Teleports,
            ResupplyPoints: sourceState.ResupplyPoints,
            KillAssists: sourceState.KillAssists,
            InWaitingForPlayers: world.InWaitingForPlayers ? (byte)1 : (byte)0,
            AwaitingReadyRestart: world.AwaitingReadyRestart ? (byte)1 : (byte)0,
            RestartRoundTime: timer.RestartRoundTimeSeconds,
            MapResetTime: timer.MapResetTimeSeconds,
            SourceTeamNumber: sourceState.TeamNumber,
            SourceTeamScore: sourceTeam.Score,
            SourceTeamRoundsWon: sourceTeam.RoundsWon,
            SourceTeamName: sourceTeam.Name,
            TeamFlagCaptures: sourceTeam.FlagCaptures,
            TeamRole: sourceState.TeamNumber,
            PlayerState: sourceState.PlayerState));
    }

    private static byte[] BuildSnapshotMicroGameplayRulesDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        var world = game.World;
        var timer = world.Timer;
        var sourceTeam = world.Team(sourceState.TeamNumber);
        return Ps3SourceNativeMessages.BuildMicroGameplayRulesDelta(new Ps3SourceGameplayStateDelta(
            SourcePlayerSlotIndex(game, player),
            ObjectId: RootSourceObjectId(player),
            Ammo: [sourceState.PrimaryAmmo, sourceState.SecondaryAmmo, sourceState.Metal],
            Fov: sourceState.Fov,
            FovStart: sourceState.FovStart,
            FovTime: sourceState.FovTime,
            DefaultFov: sourceState.DefaultFov,
            ObserverMode: sourceState.ObserverMode,
            ObserverTargetHandle: sourceState.ObserverTargetHandle,
            ViewModelHandles: [sourceState.ViewModelHandle0, sourceState.ViewModelHandle1],
            ViewOffsetX: sourceState.ViewOffsetX,
            ViewOffsetY: sourceState.ViewOffsetY,
            ViewOffsetZ: sourceState.ViewOffsetZ,
            Friction: sourceState.Friction,
            TickBase: sourceState.TickBase,
            NextThinkTick: sourceState.NextThinkTick,
            GroundEntityHandle: sourceState.GroundEntityHandle,
            MaxHealth: sourceState.MaxHealth,
            PlayerClass: sourceState.ClassNumber,
            RoundState: world.RoundState,
            WinningTeam: world.WinningTeam,
            InSetup: world.InSetup,
            InOvertime: world.InOvertime,
            GameType: world.GameType,
            WeaponHandles: [sourceState.ActiveWeaponHandle],
            ActiveWeaponHandle: sourceState.ActiveWeaponHandle,
            LastWeaponHandle: 0,
            ObjectUpgradeLevel: sourceState.SentryGun.UpgradeLevel,
            ObjectState: sourceState.SentryGun.State,
            ObjectAmmoShells: sourceState.SentryGun.AmmoShells,
            ObjectAmmoRockets: sourceState.SentryGun.AmmoRockets,
            ObjectUpgradeMetal: sourceState.SentryGun.UpgradeMetal,
            TeleporterState: sourceState.Teleporter.State,
            TeleporterRechargeTime: sourceState.Teleporter.RechargeTime,
            TeleporterTimesUsed: sourceState.Teleporter.TimesUsed,
            TeleporterYawToExit: sourceState.Teleporter.YawToExit,
            MaxSpeed: sourceState.MaxSpeed,
            DeathTime: sourceState.DeathTime,
            Ducked: sourceState.Ducked,
            Ducking: sourceState.Ducking,
            InDuckJump: sourceState.InDuckJump,
            FallVelocity: sourceState.FallVelocity,
            DrawViewModel: sourceState.DrawViewModel,
            AllowAutoMovement: sourceState.AllowAutoMovement,
            RagdollHandle: sourceState.RagdollHandle,
            SpawnCounter: sourceState.SpawnCounter,
            TotalScore: sourceState.Score,
            Deaths: sourceState.Deaths,
            Captures: sourceState.Captures,
            Defenses: sourceState.Defenses,
            Dominations: sourceState.Dominations,
            Revenge: sourceState.Revenge,
            BuildingsDestroyed: sourceState.BuildingsDestroyed,
            Headshots: sourceState.Headshots,
            Backstabs: sourceState.Backstabs,
            HealPoints: sourceState.HealPoints,
            Invulns: sourceState.Invulns,
            Teleports: sourceState.Teleports,
            ResupplyPoints: sourceState.ResupplyPoints,
            KillAssists: sourceState.KillAssists,
            InWaitingForPlayers: world.InWaitingForPlayers ? (byte)1 : (byte)0,
            AwaitingReadyRestart: world.AwaitingReadyRestart ? (byte)1 : (byte)0,
            RestartRoundTime: timer.RestartRoundTimeSeconds,
            MapResetTime: timer.MapResetTimeSeconds,
            SourceTeamNumber: sourceState.TeamNumber,
            SourceTeamScore: sourceTeam.Score,
            SourceTeamRoundsWon: sourceTeam.RoundsWon,
            SourceTeamName: sourceTeam.Name,
            TeamFlagCaptures: sourceTeam.FlagCaptures,
            TeamRole: sourceState.TeamNumber,
            PlayerState: sourceState.PlayerState));
    }

    private static byte[] BuildSnapshotNanoGameplayRulesDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        var world = game.World;
        return Ps3SourceNativeMessages.BuildNanoGameplayRulesDelta(new Ps3SourceGameplayStateDelta(
            SourcePlayerSlotIndex(game, player),
            ObjectId: RootSourceObjectId(player),
            Ammo: [sourceState.PrimaryAmmo, sourceState.SecondaryAmmo, sourceState.Metal],
            Fov: sourceState.Fov,
            FovStart: sourceState.FovStart,
            FovTime: sourceState.FovTime,
            DefaultFov: sourceState.DefaultFov,
            ObserverMode: sourceState.ObserverMode,
            ObserverTargetHandle: sourceState.ObserverTargetHandle,
            ViewModelHandles: [sourceState.ViewModelHandle0, sourceState.ViewModelHandle1],
            ViewOffsetX: sourceState.ViewOffsetX,
            ViewOffsetY: sourceState.ViewOffsetY,
            ViewOffsetZ: sourceState.ViewOffsetZ,
            Friction: sourceState.Friction,
            TickBase: sourceState.TickBase,
            NextThinkTick: sourceState.NextThinkTick,
            GroundEntityHandle: sourceState.GroundEntityHandle,
            MaxHealth: sourceState.MaxHealth,
            PlayerClass: sourceState.ClassNumber,
            RoundState: world.RoundState,
            WinningTeam: world.WinningTeam,
            InSetup: world.InSetup,
            InOvertime: world.InOvertime,
            GameType: world.GameType,
            WeaponHandles: [sourceState.ActiveWeaponHandle],
            ActiveWeaponHandle: sourceState.ActiveWeaponHandle,
            LastWeaponHandle: 0,
            ObjectUpgradeLevel: sourceState.SentryGun.UpgradeLevel,
            ObjectState: sourceState.SentryGun.State,
            ObjectAmmoShells: sourceState.SentryGun.AmmoShells,
            ObjectAmmoRockets: sourceState.SentryGun.AmmoRockets,
            ObjectUpgradeMetal: sourceState.SentryGun.UpgradeMetal,
            TeleporterState: sourceState.Teleporter.State,
            TeleporterRechargeTime: sourceState.Teleporter.RechargeTime,
            TeleporterTimesUsed: sourceState.Teleporter.TimesUsed,
            TeleporterYawToExit: sourceState.Teleporter.YawToExit,
            TotalScore: sourceState.Score,
            Captures: sourceState.Captures,
            SourceTeamNumber: sourceState.TeamNumber,
            TeamRole: sourceState.TeamNumber,
            PlayerState: sourceState.PlayerState));
    }

    private static byte[] BuildSnapshotCompactObjectiveResourceDelta(GameManagerSession game)
    {
        return Ps3SourceNativeMessages.BuildCompactObjectiveResourceDelta(BuildObjectiveResourceDelta(game));
    }

    private static byte[] BuildSnapshotTinyObjectiveResourceDelta(GameManagerSession game)
    {
        return Ps3SourceNativeMessages.BuildTinyObjectiveResourceDelta(BuildObjectiveResourceDelta(game));
    }

    private static byte[] BuildSnapshotMicroObjectiveResourceDelta(GameManagerSession game)
    {
        return Ps3SourceNativeMessages.BuildMicroObjectiveResourceDelta(BuildObjectiveResourceDelta(game));
    }

    private static void AddUrgentNativeSourceStateDeltas(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        var sourceState = player.SourceState;
        var world = game.World;
        var hasObjectiveProgress = world.GameType == 2
            && world.ControlPoints.Any(static point =>
                point.LazyCapPercentage > 0
                || point.CappingTeam != 0
                || point.TeamInZone != 0);
        var roundEnding = world.RoundState >= 5;
        var hasCombatTransition = sourceState.Deaths > 0 || !sourceState.Alive || sourceState.SpawnCounter > 1;

        AddScoreboardSourceStateDeltaIfNeeded(responses, state, game, player);

        if (!hasObjectiveProgress && !roundEnding && !hasCombatTransition)
        {
            return;
        }

        var signature = BuildUrgentSourceStateSignature(game, player);
        if (string.Equals(state.LastUrgentSourceStateSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        state.LastUrgentSourceStateSignature = signature;

        if (hasObjectiveProgress || roundEnding || hasCombatTransition)
        {
            AddPacket(
                responses,
                state,
                BuildUrgentStateSnapshotFrameBody(game, player, includeRagdoll: !sourceState.Alive),
                "generated PS3 Source native urgent semantic snapshot",
                sequenceAdvance: 4,
                allowFragmentation: true,
                allowNativeWrap: true);
        }

        if (hasObjectiveProgress || roundEnding)
        {
            AddPacket(
                responses,
                state,
                BuildSnapshotMicroObjectiveResourceDelta(game),
                "generated PS3 Source native urgent objective delta",
                sequenceAdvance: 3);
        }

        if (roundEnding)
        {
            AddPacket(
                responses,
                state,
                BuildSnapshotMicroRoundTimerDelta(game),
                "generated PS3 Source native urgent round timer delta",
                sequenceAdvance: 3);
        }

        if (hasCombatTransition)
        {
            AddPacket(
                responses,
                state,
                BuildSnapshotMicroPlayerResourceDelta(game, player),
                "generated PS3 Source native urgent player resource delta",
                sequenceAdvance: 3);
            AddPacket(
                responses,
                state,
                BuildSnapshotCompactGameplayRulesDelta(game, player),
                "generated PS3 Source native urgent player gameplay delta",
                sequenceAdvance: 3);
            state.LastScoreboardSourceStateSignature = BuildScoreboardSourceStateSignature(game, player);

            if (!sourceState.Alive)
            {
                AddPacket(
                    responses,
                    state,
                    BuildSnapshotRagdollDelta(game, player),
                    "generated PS3 Source native urgent ragdoll delta",
                    sequenceAdvance: 3);
            }
        }
    }

    private static void AddScoreboardSourceStateDeltaIfNeeded(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        if (!HasScoreboardSourceState(player, game))
        {
            return;
        }

        var signature = BuildScoreboardSourceStateSignature(game, player);
        if (string.Equals(state.LastScoreboardSourceStateSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        state.LastScoreboardSourceStateSignature = signature;
        AddPacket(
            responses,
            state,
            BuildSnapshotTinyGameplayRulesDelta(game, player),
            "generated PS3 Source native scoreboard gameplay delta",
            sequenceAdvance: 3);
    }

    private static bool HasScoreboardSourceState(PlayerSession player, GameManagerSession game)
    {
        var sourceState = player.SourceState;
        var team = game.World.Team(sourceState.TeamNumber);
        return sourceState.Score != 0
            || sourceState.Captures != 0
            || sourceState.Defenses != 0
            || sourceState.Dominations != 0
            || sourceState.Revenge != 0
            || sourceState.BuildingsDestroyed != 0
            || sourceState.Headshots != 0
            || sourceState.Backstabs != 0
            || sourceState.HealPoints != 0
            || sourceState.Invulns != 0
            || sourceState.Teleports != 0
            || sourceState.ResupplyPoints != 0
            || sourceState.KillAssists != 0
            || sourceState.Deaths != 0
            || team.Score != 0
            || team.RoundsWon != 0
            || team.FlagCaptures != 0;
    }

    private static string BuildScoreboardSourceStateSignature(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        var team = game.World.Team(sourceState.TeamNumber);
        return string.Join(
            ';',
            sourceState.Score,
            sourceState.Captures,
            sourceState.Defenses,
            sourceState.Dominations,
            sourceState.Revenge,
            sourceState.BuildingsDestroyed,
            sourceState.Headshots,
            sourceState.Backstabs,
            sourceState.HealPoints,
            sourceState.Invulns,
            sourceState.Teleports,
            sourceState.ResupplyPoints,
            sourceState.KillAssists,
            sourceState.Deaths,
            sourceState.TeamNumber,
            team.Score,
            team.RoundsWon,
            team.FlagCaptures,
            game.World.RoundState,
            game.World.WinningTeam);
    }

    private static string BuildUrgentSourceStateSignature(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        var world = game.World;
        var builder = new StringBuilder(128);
        builder
            .Append("round=").Append(world.RoundState)
            .Append(";win=").Append(world.WinningTeam)
            .Append(";game=").Append(world.GameType)
            .Append(";alive=").Append(sourceState.Alive ? 1 : 0)
            .Append(";deaths=").Append(sourceState.Deaths)
            .Append(";spawn=").Append(sourceState.SpawnCounter)
            .Append(";ragdoll=").Append(sourceState.RagdollHandle)
            .Append(";state=").Append(sourceState.PlayerState)
            .Append(";obs=").Append(sourceState.ObserverMode)
            .Append(";team=").Append(sourceState.TeamNumber);

        foreach (var point in world.ControlPoints)
        {
            builder
                .Append(";cp").Append(point.Index)
                .Append('=').Append(point.OwnerTeam)
                .Append('/').Append(point.CappingTeam)
                .Append('/').Append(point.TeamInZone)
                .Append('/').Append(MathF.Round(point.LazyCapPercentage, 3));
        }

        return builder.ToString();
    }

    private static byte[] BuildUrgentStateSnapshotFrameBody(
        GameManagerSession game,
        PlayerSession player,
        bool includeRagdoll)
    {
        var state = player.NativeSourceResponder;
        var sections = new List<byte[]>
        {
            BuildSnapshotPlayerResourceDelta(game, player),
            BuildSnapshotCompactPlayerEntityDelta(game, player),
            BuildSnapshotCompactGameplayRulesDelta(game, player),
            BuildSnapshotWeaponEntityDelta(player),
            BuildSnapshotRoundTimerDelta(game),
            Ps3SourceNativeMessages.BuildObjectiveResourceDelta(BuildObjectiveResourceDelta(game))
        };
        if (includeRagdoll)
        {
            sections.Add(BuildSnapshotRagdollDelta(game, player));
        }

        var frame = new Ps3SourceSnapshotFrame(
            state.SnapshotFrameIndex,
            state.SnapshotBaseFrame,
            UpdateFlags: 0,
            PendingCount: 1,
            HasEntityDelta: true,
            ExtraSections: PackSnapshotEntityDeltaSections(sections));
        state.SnapshotBaseFrame = state.SnapshotFrameIndex;
        state.SnapshotFrameIndex++;
        return Ps3SourceSnapshotFrameBuilder.Encode(frame);
    }

    private static byte[][] PackSnapshotEntityDeltaSections(IReadOnlyList<byte[]> sections)
    {
        var packed = new List<byte[]>(sections.Count);
        var pendingEntityDeltas = new List<byte[]>();
        foreach (var section in sections)
        {
            if (IsNativeEntityDeltaSection(section))
            {
                pendingEntityDeltas.Add(section);
                continue;
            }

            FlushPackedEntityDeltas(packed, pendingEntityDeltas);
            packed.Add(section);
        }

        FlushPackedEntityDeltas(packed, pendingEntityDeltas);
        return [.. packed];
    }

    private static void FlushPackedEntityDeltas(List<byte[]> packed, List<byte[]> pendingEntityDeltas)
    {
        if (pendingEntityDeltas.Count == 0)
        {
            return;
        }

        packed.Add(pendingEntityDeltas.Count == 1
            ? pendingEntityDeltas[0]
            : Ps3SourceEntityDeltaFrameBuilder.PackEncodedNativeRecords(pendingEntityDeltas));
        pendingEntityDeltas.Clear();
    }

    private static bool IsNativeEntityDeltaSection(byte[] section)
    {
        return Ps3SourceEntityDeltaFrameBuilder.TryDecodeNativeRecord(section, out var record, out var consumedBits)
            && consumedBits > 0
            && consumedBits <= section.Length * 8
            && record.IsPartialRun
            && record.ObjectId.HasValue
            && !string.IsNullOrWhiteSpace(record.ObjectName)
            && record.ObjectName[0] == 'C';
    }

    private static byte[] BuildSnapshotRagdollDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        return Ps3SourceNativeMessages.BuildTfRagdollDelta(new Ps3SourceTfRagdollDelta(
            ObjectId: 0xa4,
            OriginX: sourceState.OriginX,
            OriginY: sourceState.OriginY,
            OriginZ: sourceState.OriginZ,
            PlayerIndex: SourcePlayerSlotIndex(game, player),
            ForceX: 0,
            ForceY: 0,
            ForceZ: 0,
            VelocityX: sourceState.VelocityX,
            VelocityY: sourceState.VelocityY,
            VelocityZ: sourceState.VelocityZ,
            ForceBone: 0,
            Gib: false,
            Burning: false,
            Team: sourceState.TeamNumber,
            Class: sourceState.ClassNumber));
    }

    private static byte[] BuildSnapshotTinyObjectiveGameplayDelta(GameManagerSession game, PlayerSession player)
    {
        var sourceState = player.SourceState;
        var world = game.World;
        var sourceTeam = world.Team(sourceState.TeamNumber);
        return Ps3SourceNativeMessages.BuildTinyObjectiveGameplayDelta(
            BuildObjectiveResourceDelta(game),
            new Ps3SourceGameplayStateDelta(
                SourcePlayerSlotIndex(game, player),
                ObjectId: RootSourceObjectId(player),
                Ammo: [sourceState.PrimaryAmmo, sourceState.SecondaryAmmo, sourceState.Metal],
                Fov: sourceState.Fov,
                FovStart: sourceState.FovStart,
                FovTime: sourceState.FovTime,
                DefaultFov: sourceState.DefaultFov,
                ObserverMode: sourceState.ObserverMode,
                ObserverTargetHandle: sourceState.ObserverTargetHandle,
                ViewModelHandles: [sourceState.ViewModelHandle0, sourceState.ViewModelHandle1],
                ViewOffsetX: sourceState.ViewOffsetX,
                ViewOffsetY: sourceState.ViewOffsetY,
                ViewOffsetZ: sourceState.ViewOffsetZ,
                Friction: sourceState.Friction,
                TickBase: sourceState.TickBase,
                NextThinkTick: sourceState.NextThinkTick,
                GroundEntityHandle: sourceState.GroundEntityHandle,
                MaxHealth: sourceState.MaxHealth,
                PlayerClass: sourceState.ClassNumber,
                RoundState: world.RoundState,
                WinningTeam: world.WinningTeam,
                InSetup: world.InSetup,
                InOvertime: world.InOvertime,
                GameType: world.GameType,
                WeaponHandles: [sourceState.ActiveWeaponHandle],
                ActiveWeaponHandle: sourceState.ActiveWeaponHandle,
                LastWeaponHandle: 0,
                ObjectUpgradeLevel: sourceState.SentryGun.UpgradeLevel,
                ObjectState: sourceState.SentryGun.State,
                ObjectAmmoShells: sourceState.SentryGun.AmmoShells,
                ObjectAmmoRockets: sourceState.SentryGun.AmmoRockets,
                ObjectUpgradeMetal: sourceState.SentryGun.UpgradeMetal,
                TeleporterState: sourceState.Teleporter.State,
                TeleporterRechargeTime: sourceState.Teleporter.RechargeTime,
                TeleporterTimesUsed: sourceState.Teleporter.TimesUsed,
                TeleporterYawToExit: sourceState.Teleporter.YawToExit,
                DeathTime: sourceState.DeathTime,
                RagdollHandle: sourceState.RagdollHandle,
                SpawnCounter: sourceState.SpawnCounter,
                TotalScore: sourceState.Score,
                Deaths: sourceState.Deaths,
                Captures: sourceState.Captures,
                Defenses: sourceState.Defenses,
                Dominations: sourceState.Dominations,
                Revenge: sourceState.Revenge,
                BuildingsDestroyed: sourceState.BuildingsDestroyed,
                Headshots: sourceState.Headshots,
                Backstabs: sourceState.Backstabs,
                HealPoints: sourceState.HealPoints,
                Invulns: sourceState.Invulns,
                Teleports: sourceState.Teleports,
                ResupplyPoints: sourceState.ResupplyPoints,
                KillAssists: sourceState.KillAssists,
                InWaitingForPlayers: world.InWaitingForPlayers ? (byte)1 : (byte)0,
                AwaitingReadyRestart: world.AwaitingReadyRestart ? (byte)1 : (byte)0,
                RestartRoundTime: world.Timer.RestartRoundTimeSeconds,
                MapResetTime: world.Timer.MapResetTimeSeconds,
                SourceTeamNumber: sourceState.TeamNumber,
                SourceTeamScore: sourceTeam.Score,
                SourceTeamRoundsWon: sourceTeam.RoundsWon,
                SourceTeamName: sourceTeam.Name,
                TeamFlagCaptures: sourceTeam.FlagCaptures,
                TeamRole: sourceState.TeamNumber,
                PlayerState: sourceState.PlayerState));
    }

    private static byte[] BuildSnapshotFrameBody(GameManagerSession game, PlayerSession player)
    {
        var state = player.NativeSourceResponder;
        var sourceState = player.SourceState;
        AdvanceNativeSourceWorldState(game, player);
        var world = game.World;
        var playerSlot = SourcePlayerSlotIndex(game, player);
        var playerObjectId = PlayerSourceObjectId(player);
        var rootObjectId = RootSourceObjectId(player);
        var roundLengthSeconds = world.RoundLengthSeconds;
        var timer = world.Timer;
        var sourceTeam = world.Team(sourceState.TeamNumber);
        var extra = Ps3SourceNativeMessages.BuildPlayerResourceDelta(new Ps3SourcePlayerResourceDelta(
            playerSlot,
            Health: sourceState.Health,
            Rating: 0,
            RatingDelta: 0,
            Connected: true,
            ObjectId: playerObjectId,
            StatusText: $"{(string.IsNullOrWhiteSpace(player.Name) ? "Player" : player.Name)} connected to {game.MapName}",
            Score: sourceState.Score,
            Deaths: sourceState.Deaths,
            Team: sourceState.TeamNumber,
            Alive: sourceState.Alive));
        var playerEntity = Ps3SourceNativeMessages.BuildPlayerEntityDelta(new Ps3SourcePlayerEntityDelta(
            playerSlot,
            ObjectId: playerObjectId,
            TeamNumber: (byte)Math.Clamp(sourceState.TeamNumber, 0, 0xff),
            Health: sourceState.Health,
            LifeState: sourceState.Alive ? (byte)0 : (byte)1,
            Flags: sourceState.Flags,
            ClassNumber: sourceState.ClassNumber,
            OriginX: sourceState.OriginX,
            OriginY: sourceState.OriginY,
            OriginZ: sourceState.OriginZ,
            RotationPitch: 0,
            RotationYaw: sourceState.Yaw,
            RotationRoll: 0,
            EyePitch: sourceState.Pitch,
            EyeYaw: sourceState.Yaw,
            SimulationTime: sourceState.SimulationTime,
            ModelIndex: sourceState.ModelIndex,
            Effects: sourceState.Effects,
            RenderMode: sourceState.RenderMode,
            RenderFx: sourceState.RenderFx,
            RenderColor: sourceState.RenderColor,
            MoveType: sourceState.MoveType,
            MoveCollide: sourceState.MoveCollide,
            CollisionGroup: sourceState.CollisionGroup,
            CollisionMinsX: -24,
            CollisionMinsY: -24,
            CollisionMinsZ: 0,
            CollisionMaxsX: 24,
            CollisionMaxsY: 24,
            CollisionMaxsZ: 82,
            SolidType: sourceState.SolidType,
            SolidFlags: sourceState.SolidFlags,
            SurroundType: 1,
            TextureFrameIndex: sourceState.TextureFrameIndex,
            Sequence: sourceState.Sequence,
            ForceBone: sourceState.ForceBone,
            ForceX: sourceState.ForceX,
            ForceY: sourceState.ForceY,
            ForceZ: sourceState.ForceZ,
            Skin: sourceState.Skin,
            Body: sourceState.Body,
            HitboxSet: sourceState.HitboxSet,
            ModelWidthScale: sourceState.ModelWidthScale,
            PlaybackRate: sourceState.PlaybackRate,
            ClientSideAnimation: sourceState.ClientSideAnimation,
            ClientSideFrameReset: sourceState.ClientSideFrameReset,
            NewSequenceParity: sourceState.NewSequenceParity,
            ResetEventsParity: sourceState.ResetEventsParity,
            MuzzleFlashParity: sourceState.MuzzleFlashParity,
            LightingOriginHandle: sourceState.LightingOriginHandle,
            LightingOriginRelativeHandle: sourceState.LightingOriginRelativeHandle,
            ServerAnimationCycle: sourceState.ServerAnimationCycle,
            FadeMinDistance: sourceState.FadeMinDistance,
            FadeMaxDistance: sourceState.FadeMaxDistance,
            FadeScale: sourceState.FadeScale));
        var gameplayState = Ps3SourceNativeMessages.BuildGameplayStateDelta(new Ps3SourceGameplayStateDelta(
            playerSlot,
            ObjectId: rootObjectId,
            Ammo: [sourceState.PrimaryAmmo, sourceState.SecondaryAmmo, sourceState.Metal, 0, 0, 0, 0, 0],
            Fov: sourceState.Fov,
            FovStart: sourceState.FovStart,
            FovTime: sourceState.FovTime,
            DefaultFov: sourceState.DefaultFov,
            ObserverMode: sourceState.ObserverMode,
            ObserverTargetHandle: sourceState.ObserverTargetHandle,
            ViewModelHandles: [sourceState.ViewModelHandle0, sourceState.ViewModelHandle1],
            ViewOffsetX: sourceState.ViewOffsetX,
            ViewOffsetY: sourceState.ViewOffsetY,
            ViewOffsetZ: sourceState.ViewOffsetZ,
            Friction: sourceState.Friction,
            TickBase: sourceState.TickBase,
            NextThinkTick: sourceState.NextThinkTick,
            GroundEntityHandle: sourceState.GroundEntityHandle,
            MaxHealth: sourceState.MaxHealth,
            PlayerClass: sourceState.ClassNumber,
            RoundState: world.RoundState,
            WinningTeam: world.WinningTeam,
            InSetup: world.InSetup,
            InOvertime: world.InOvertime,
            GameType: world.GameType,
            WeaponHandles: [sourceState.ActiveWeaponHandle, 0, 0, 0, 0, 0],
            ActiveWeaponHandle: sourceState.ActiveWeaponHandle,
            LastWeaponHandle: 0,
            ObjectUpgradeLevel: sourceState.SentryGun.UpgradeLevel,
            ObjectState: sourceState.SentryGun.State,
            ObjectAmmoShells: sourceState.SentryGun.AmmoShells,
            ObjectAmmoRockets: sourceState.SentryGun.AmmoRockets,
            ObjectUpgradeMetal: sourceState.SentryGun.UpgradeMetal,
            TeleporterState: sourceState.Teleporter.State,
            TeleporterRechargeTime: sourceState.Teleporter.RechargeTime,
            TeleporterTimesUsed: sourceState.Teleporter.TimesUsed,
            TeleporterYawToExit: sourceState.Teleporter.YawToExit,
            ZoomOwnerHandle: sourceState.ZoomOwnerHandle,
            VehicleHandle: sourceState.VehicleHandle,
            UseEntityHandle: sourceState.UseEntityHandle,
            MaxSpeed: sourceState.MaxSpeed,
            LastPlaceName: SourcePlaceName(game),
            NoInterpParity: sourceState.NoInterpParity,
            OnTarget: sourceState.OnTarget,
            VelocityX: sourceState.VelocityX,
            VelocityY: sourceState.VelocityY,
            VelocityZ: sourceState.VelocityZ,
            BaseVelocityX: sourceState.BaseVelocityX,
            BaseVelocityY: sourceState.BaseVelocityY,
            BaseVelocityZ: sourceState.BaseVelocityZ,
            ConstraintEntityHandle: sourceState.ConstraintEntityHandle,
            ConstraintCenterX: sourceState.ConstraintCenterX,
            ConstraintCenterY: sourceState.ConstraintCenterY,
            ConstraintCenterZ: sourceState.ConstraintCenterZ,
            ConstraintRadius: sourceState.ConstraintRadius,
            ConstraintWidth: sourceState.ConstraintWidth,
            ConstraintSpeedFactor: sourceState.ConstraintSpeedFactor,
            DeathTime: sourceState.DeathTime,
            WaterLevel: sourceState.WaterLevel,
            LaggedMovementValue: sourceState.LaggedMovementValue,
            Ducked: sourceState.Ducked,
            Ducking: sourceState.Ducking,
            InDuckJump: sourceState.InDuckJump,
            DuckTime: sourceState.DuckTime,
            DuckJumpTime: sourceState.DuckJumpTime,
            JumpTime: sourceState.JumpTime,
            FallVelocity: sourceState.FallVelocity,
            PunchAngleX: sourceState.PunchAngleX,
            PunchAngleY: sourceState.PunchAngleY,
            PunchAngleZ: sourceState.PunchAngleZ,
            PunchAngleVelocityX: sourceState.PunchAngleVelocityX,
            PunchAngleVelocityY: sourceState.PunchAngleVelocityY,
            PunchAngleVelocityZ: sourceState.PunchAngleVelocityZ,
            DrawViewModel: sourceState.DrawViewModel,
            WearingSuit: sourceState.WearingSuit,
            Poisoned: sourceState.Poisoned,
            StepSize: sourceState.StepSize,
            AllowAutoMovement: sourceState.AllowAutoMovement,
            SaveMeParity: sourceState.SaveMeParity,
            RagdollHandle: sourceState.RagdollHandle,
            ItemHandle: sourceState.ItemHandle,
            SpawnCounter: sourceState.SpawnCounter,
            NextAttack: sourceState.NextPrimaryAttack,
            TotalScore: sourceState.Score,
            Deaths: sourceState.Deaths,
            Captures: sourceState.Captures,
            Defenses: sourceState.Defenses,
            Dominations: sourceState.Dominations,
            Revenge: sourceState.Revenge,
            BuildingsDestroyed: sourceState.BuildingsDestroyed,
            Headshots: sourceState.Headshots,
            Backstabs: sourceState.Backstabs,
            HealPoints: sourceState.HealPoints,
            Invulns: sourceState.Invulns,
            Teleports: sourceState.Teleports,
            ResupplyPoints: sourceState.ResupplyPoints,
            KillAssists: sourceState.KillAssists,
            InWaitingForPlayers: world.InWaitingForPlayers ? (byte)1 : (byte)0,
            AwaitingReadyRestart: world.AwaitingReadyRestart ? (byte)1 : (byte)0,
            RestartRoundTime: timer.RestartRoundTimeSeconds,
            MapResetTime: timer.MapResetTimeSeconds,
            NextRespawnWave: world.NextRespawnWaveBuckets(),
            TeamRespawnWaveTimes: world.TeamRespawnWaveTimeBuckets(),
            TeamGoalStringRed: SourceTeamGoalString(game, red: true),
            TeamGoalStringBlue: SourceTeamGoalString(game, red: false),
            SourceTeamNumber: sourceState.TeamNumber,
            SourceTeamScore: sourceTeam.Score,
            SourceTeamRoundsWon: sourceTeam.RoundsWon,
            SourceTeamName: sourceTeam.Name,
            TeamFlagCaptures: sourceTeam.FlagCaptures,
            TeamRole: sourceState.TeamNumber,
            PlayerCondition: sourceState.PlayerCondition,
            Jumping: sourceState.Jumping,
            PlayerState: sourceState.PlayerState,
            DesiredPlayerClass: sourceState.ClassNumber,
            DisguiseTeam: sourceState.DisguiseTeam,
            DisguiseClass: sourceState.DisguiseClass,
            DisguiseTargetIndex: sourceState.DisguiseTargetIndex,
            DisguiseHealth: sourceState.DisguiseHealth,
            DesiredDisguiseTeam: sourceState.DesiredDisguiseTeam,
            DesiredDisguiseClass: sourceState.DesiredDisguiseClass,
            CloakMeter: sourceState.CloakMeter,
            TfLocalOriginX: sourceState.OriginX,
            TfLocalOriginY: sourceState.OriginY,
            TfLocalOriginZ: sourceState.OriginZ,
            TfNonLocalOriginX: sourceState.OriginX,
            TfNonLocalOriginY: sourceState.OriginY,
            TfNonLocalOriginZ: sourceState.OriginZ));
        var weaponEntity = Ps3SourceNativeMessages.BuildWeaponEntityDelta(new Ps3SourceWeaponEntityDelta(
            ObjectId: sourceState.ActiveWeaponHandle,
            OwnerHandle: playerObjectId,
            State: sourceState.WeaponState,
            ViewModelIndex: sourceState.WeaponViewModelIndex,
            WorldModelIndex: sourceState.WeaponWorldModelIndex,
            NextPrimaryAttack: sourceState.NextPrimaryAttack,
            NextSecondaryAttack: sourceState.NextSecondaryAttack,
            TimeWeaponIdle: sourceState.TimeWeaponIdle,
            PrimaryAmmoType: sourceState.PrimaryAmmoType,
            SecondaryAmmoType: sourceState.SecondaryAmmoType,
            Clip1: sourceState.WeaponClip1,
            Clip2: sourceState.WeaponClip2,
            Lowered: sourceState.WeaponLowered,
            ReloadMode: sourceState.WeaponReloadMode,
            ResetParity: sourceState.WeaponResetParity,
            ReloadedThroughAnimEvent: sourceState.WeaponReloadedThroughAnimEvent,
            InReload: sourceState.WeaponInReload,
            FireOnEmpty: sourceState.WeaponFireOnEmpty,
            NextEmptySoundTime: sourceState.WeaponNextEmptySoundTime,
            BuildState: sourceState.WeaponBuildState,
            BuildObjectType: sourceState.WeaponBuildObjectType,
            ObjectBeingBuiltHandle: sourceState.WeaponObjectBeingBuiltHandle,
            TfWeaponState: sourceState.TfWeaponState,
            CritFire: sourceState.WeaponCritFire,
            Healing: sourceState.WeaponHealing,
            Attacking: sourceState.WeaponAttacking,
            ChargeRelease: sourceState.WeaponChargeRelease,
            Holstered: sourceState.WeaponHolstered,
            HealingTargetHandle: sourceState.WeaponHealingTargetHandle,
            HealEffectLifetime: sourceState.WeaponHealEffectLifetime,
            ChargeLevel: sourceState.WeaponChargeLevel,
            BottleBroken: sourceState.WeaponBottleBroken,
            PipebombCount: sourceState.WeaponPipebombCount,
            ChargeBeginTime: sourceState.WeaponChargeBeginTime,
            SoonestPrimaryAttack: sourceState.WeaponSoonestPrimaryAttack,
            MinigunCritShot: sourceState.WeaponMinigunCritShot));
        var roundTimer = Ps3SourceNativeMessages.BuildTeamRoundTimerDelta(new Ps3SourceTeamRoundTimerDelta(
            ObjectId: 0xa2,
            TimerPaused: timer.TimerPaused,
            TimeRemaining: timer.TimeRemainingSeconds,
            TimerEndTime: timer.TimerEndTimeSeconds,
            TimerMaxLength: timer.TimerMaxLengthSeconds,
            IsDisabled: timer.IsDisabled,
            ShowInHud: timer.ShowInHud,
            TimerLength: timer.TimerLengthSeconds,
            TimerInitialLength: timer.TimerInitialLengthSeconds,
            AutoCountdown: timer.AutoCountdown,
            SetupTimeLength: timer.SetupTimeLengthSeconds,
            State: timer.State,
            StartPaused: timer.StartPaused));
        var objectiveResource = Ps3SourceNativeMessages.BuildObjectiveResourceDelta(BuildObjectiveResourceDelta(game));
        var fireBullets = Ps3SourceNativeMessages.BuildFireBulletsEvent(new Ps3SourceFireBulletsEvent(
            ObjectId: 0xa3,
            OriginX: sourceState.OriginX,
            OriginY: sourceState.OriginY,
            OriginZ: sourceState.OriginZ + 48,
            AnglePitch: sourceState.Pitch,
            AngleYaw: sourceState.Yaw,
            WeaponId: 0,
            Mode: 0,
            Seed: checked((uint)Math.Max(state.SnapshotFrameIndex, 0)),
            PlayerIndex: playerSlot,
            Spread: 0));
        var ragdoll = Ps3SourceNativeMessages.BuildTfRagdollDelta(new Ps3SourceTfRagdollDelta(
            ObjectId: 0xa4,
            OriginX: sourceState.OriginX,
            OriginY: sourceState.OriginY,
            OriginZ: sourceState.OriginZ,
            PlayerIndex: playerSlot,
            ForceX: 0,
            ForceY: 0,
            ForceZ: 0,
            VelocityX: sourceState.VelocityX,
            VelocityY: sourceState.VelocityY,
            VelocityZ: sourceState.VelocityZ,
            ForceBone: 0,
            Gib: false,
            Burning: false,
            Team: sourceState.TeamNumber,
            Class: sourceState.ClassNumber));
        var tfExplosion = Ps3SourceNativeMessages.BuildTfExplosionEvent(new Ps3SourceTfExplosionEvent(
            ObjectId: 0xa5,
            OriginX: sourceState.OriginX,
            OriginY: sourceState.OriginY,
            OriginZ: sourceState.OriginZ,
            NormalX: 0,
            NormalY: 0,
            NormalZ: 1,
            WeaponId: 0,
            EntityIndex: playerSlot));
        var playerAnimEvent = Ps3SourceNativeMessages.BuildPlayerAnimEvent(new Ps3SourcePlayerAnimEvent(
            ObjectId: 0xa6,
            PlayerIndex: playerSlot,
            EventId: 0,
            Data: 0));
        var entityDeltas = Ps3SourceEntityDeltaFrameBuilder.PackEncodedNativeRecords(
            [
                extra,
                playerEntity,
                gameplayState,
                weaponEntity,
                roundTimer,
                objectiveResource,
                fireBullets,
                ragdoll,
                tfExplosion,
                playerAnimEvent
            ]);
        var statusEvent = Ps3SourceNativeMessages.BuildFormattedTextEvent(
            $"{(string.IsNullOrWhiteSpace(player.Name) ? "Player" : player.Name)} connected to {game.MapName}");
        var frame = new Ps3SourceSnapshotFrame(
            state.SnapshotFrameIndex,
            state.SnapshotBaseFrame,
            UpdateFlags: 0,
            PendingCount: 1,
            HasEntityDelta: true,
            ExtraSections:
            [
                entityDeltas,
                statusEvent,
                BuildSnapshotPlayerStateRunBody(game, player, 1120)
            ]);
        state.SnapshotBaseFrame = state.SnapshotFrameIndex;
        state.SnapshotFrameIndex++;
        return Ps3SourceSnapshotFrameBuilder.Encode(frame);
    }

    private static byte[] BuildCompactCommandSnapshotFrameBody(GameManagerSession game, PlayerSession player)
    {
        var state = player.NativeSourceResponder;
        AdvanceNativeSourceWorldState(game, player);
        var entityDeltas = Ps3SourceEntityDeltaFrameBuilder.PackEncodedNativeRecords(
            [
                BuildSnapshotMicroPlayerResourceDelta(game, player),
                BuildSnapshotMicroPlayerEntityDelta(game, player),
                BuildSnapshotNanoGameplayRulesDelta(game, player),
                BuildSnapshotMicroRoundTimerDelta(game),
                BuildSnapshotMicroObjectiveResourceDelta(game)
            ]);
        var stateRunLength = game.World.GameType == 2 ? 704 : 588;
        var frame = new Ps3SourceSnapshotFrame(
            state.SnapshotFrameIndex,
            state.SnapshotBaseFrame,
            UpdateFlags: 0x01,
            PendingCount: 1,
            HasEntityDelta: true,
            ExtraSections:
            [
                entityDeltas,
                BuildSnapshotPlayerStateRunBody(game, player, stateRunLength)
            ]);
        state.SnapshotBaseFrame = state.SnapshotFrameIndex;
        state.SnapshotFrameIndex++;
        return Ps3SourceSnapshotFrameBuilder.Encode(frame);
    }

    private static Ps3SourceObjectiveResourceDelta BuildObjectiveResourceDelta(GameManagerSession game)
    {
        var world = game.World;
        var points = world.ControlPoints;
        var pointCount = checked((uint)points.Count);
        var positions = new float[points.Count * 3];
        var visible = new byte[points.Count];
        var lazyCap = new float[points.Count];
        var previousPoints = new uint[points.Count];
        var teamCanCap = new byte[points.Count * 2];
        var teamRequiredCappers = new uint[points.Count * 2];
        var teamCapTimes = new float[points.Count * 2];
        var teamBaseIcons = new uint[points.Count];
        var baseControlPoints = new uint[points.Count];
        var inMiniRound = new byte[points.Count];
        var warnOnCap = new byte[points.Count];
        var owners = new uint[points.Count];
        var cappingTeam = new uint[points.Count];
        var teamInZone = new uint[points.Count];
        var blocked = new byte[points.Count];
        var teamIcons = new uint[points.Count * 2];

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            positions[index * 3] = point.X;
            positions[index * 3 + 1] = point.Y;
            positions[index * 3 + 2] = point.Z;
            visible[index] = point.Visible ? (byte)1 : (byte)0;
            lazyCap[index] = point.LazyCapPercentage;
            previousPoints[index] = point.PreviousPoint;
            teamCanCap[index * 2] = point.TeamCanCapRed ? (byte)1 : (byte)0;
            teamCanCap[index * 2 + 1] = point.TeamCanCapBlue ? (byte)1 : (byte)0;
            teamRequiredCappers[index * 2] = point.RequiredCappersRed;
            teamRequiredCappers[index * 2 + 1] = point.RequiredCappersBlue;
            teamCapTimes[index * 2] = point.CapTimeRed;
            teamCapTimes[index * 2 + 1] = point.CapTimeBlue;
            teamBaseIcons[index] = point.TeamBaseIcon;
            baseControlPoints[index] = point.BaseControlPoint;
            inMiniRound[index] = point.InMiniRound ? (byte)1 : (byte)0;
            warnOnCap[index] = point.WarnOnCap ? (byte)1 : (byte)0;
            owners[index] = point.OwnerTeam;
            cappingTeam[index] = point.CappingTeam;
            teamInZone[index] = point.TeamInZone;
            blocked[index] = point.Blocked ? (byte)1 : (byte)0;
            teamIcons[index * 2] = point.OwnerTeam == 2 ? 2U : 0U;
            teamIcons[index * 2 + 1] = point.OwnerTeam == 3 ? 3U : 0U;
        }

        return new Ps3SourceObjectiveResourceDelta(
            ObjectId: 0xa8,
            TimerToShowInHud: 0,
            NumControlPoints: pointCount,
            PlayingMiniRounds: false,
            ControlPointsReset: false,
            UpdateCapHudParity: world.UpdateCapHudParity,
            CpPositions: positions,
            CpIsVisible: visible,
            LazyCapPercentages: lazyCap,
            TeamIcons: teamIcons,
            TeamOverlays: teamIcons,
            TeamRequiredCappers: teamRequiredCappers,
            TeamCapTimes: teamCapTimes,
            PreviousPoints: previousPoints,
            TeamCanCap: teamCanCap,
            TeamBaseIcons: teamBaseIcons,
            BaseControlPoints: baseControlPoints,
            InMiniRound: inMiniRound,
            WarnOnCap: warnOnCap,
            NumTeamMembers: world.TeamMemberBuckets(),
            CappingTeam: cappingTeam,
            TeamInZone: teamInZone,
            Blocked: blocked,
            Owner: owners,
            CapLayoutInHud: points.Count == 0
                ? ""
                : string.Join(' ', points.Select(static point => point.Index.ToString(System.Globalization.CultureInfo.InvariantCulture))));
    }

    private static byte[] BuildSnapshotPlayerStateRunBody(GameManagerSession game, PlayerSession player, int length)
    {
        var body = new byte[length];
        var objectIds = new List<uint>
        {
            PlayerSourceObjectId(player),
            0x0000001dU,
            RootSourceObjectId(player),
            0x00000015U,
            0x00000010U,
            0x00000019U,
            0x0000006dU
        };
        objectIds.AddRange(player.SourceState.FrozenStatePeerObjectIds);

        var offset = 0;
        var index = 0;
        while (offset + 12 <= body.Length)
        {
            var objectId = objectIds[index % objectIds.Count];
            var linkedObjectId = index % 3 == 0 ? PlayerSourceObjectId(player) : RootSourceObjectId(player);
            WritePlayerStateLink(body.AsSpan(offset, 12), objectId, linkedObjectId);
            offset += 12;
            index++;
        }

        if (offset < body.Length)
        {
            FillDeterministic(body.AsSpan(offset), (byte)(0x57 ^ game.GameId ^ player.PlayerId), 0);
        }

        return body;
    }

    private static void WriteFrozenStateObject(Span<byte> destination, uint objectId, uint linkedObjectId, string name)
    {
        Ps3SourceEmbeddedObjectWireRecord
            .FrozenStateObject(objectId, linkedObjectId, string.IsNullOrWhiteSpace(name) ? "FrozenState_" : name)
            .WriteTo(destination);
    }

    private static void WritePlayerObject(Span<byte> destination, uint objectId, uint parentId, string name)
    {
        Ps3SourceEmbeddedObjectWireRecord
            .PlayerObject(objectId, parentId, string.IsNullOrWhiteSpace(name) ? "Player" : name)
            .WriteTo(destination);
    }

    private static void WritePlayerStateLink(Span<byte> destination, uint objectId, uint linkedObjectId)
    {
        new Ps3SourcePlayerStateLinkRecord(objectId, linkedObjectId).WriteTo(destination);
    }

    private static void WritePlayerDescriptor(Span<byte> destination, uint objectId, uint linkedObjectId)
    {
        Ps3SourceEmbeddedObjectWireRecord
            .PlayerDescriptor(objectId, linkedObjectId)
            .WriteTo(destination);
    }

    private static uint PlayerSourceObjectId(PlayerSession player)
    {
        return player.SourceState.LocalPlayerObjectId == 0
            ? Tf2SourcePlayerState.DefaultLocalPlayerObjectId
            : player.SourceState.LocalPlayerObjectId;
    }

    private static uint RootSourceObjectId(PlayerSession player)
    {
        return player.SourceState.RootObjectId == 0
            ? Tf2SourcePlayerState.DefaultRootObjectId
            : player.SourceState.RootObjectId;
    }

    private static uint[] FrozenStateObjectIds(PlayerSession player)
    {
        var peers = player.SourceState.FrozenStatePeerObjectIds.Length == 0
            ? [0x9fU, 0x93U, 0x95U, 0x9cU, 0x6dU]
            : player.SourceState.FrozenStatePeerObjectIds;
        var ids = new uint[peers.Length + 1];
        ids[0] = PlayerSourceObjectId(player);
        peers.CopyTo(ids.AsSpan(1));
        return ids;
    }

    private static IEnumerable<uint> RepeatingFrozenStateObjectIds(PlayerSession player, int count)
    {
        var ids = FrozenStateObjectIds(player);
        for (var index = 0; index < count; index++)
        {
            yield return ids[index % ids.Length];
        }
    }

    private static uint FrozenStateObjectId(PlayerSession player, int index)
    {
        var ids = FrozenStateObjectIds(player);
        return (uint)index < (uint)ids.Length
            ? ids[index]
            : RootSourceObjectId(player);
    }

    private static byte SourcePlayerSlotIndex(GameManagerSession game, PlayerSession player)
    {
        var index = 0;
        foreach (var candidate in game.Players.Values)
        {
            if (ReferenceEquals(candidate, player)
                || string.Equals(candidate.Endpoint, player.Endpoint, StringComparison.Ordinal))
            {
                return (byte)Math.Clamp(index, 0, 0xff);
            }

            index++;
        }

        return 0;
    }

    private static IEnumerable<PlayerSession> ActiveSourcePlayers(GameManagerSession game)
    {
        return game.ActivePlayers()
            .Where(static candidate => candidate.State != PlayerJoinState.Left);
    }

    private static void AdvanceNativeSourceWorldState(GameManagerSession game, PlayerSession player)
    {
        var state = player.NativeSourceResponder;
        var simulationFrameIndex = Math.Max(state.SnapshotFrameIndex, state.ClientPacketCount);
        player.SourceState.AdvanceSnapshot(simulationFrameIndex);
        if (player.SourceState.TryConsumeGeneratedSourceEvent(out var sourceEventText))
        {
            game.RecordGeneratedSourceServerEvent(
                SourceServerCommandType.GameEvent,
                sourceEventText,
                targetEndpoint: player.Endpoint,
                issuedBy: string.IsNullOrWhiteSpace(player.Name) ? "player" : player.Name);
        }

        game.World.AdvanceSnapshot(simulationFrameIndex, game.Players.Values);
    }

    private static string SourcePlaceName(GameManagerSession game)
    {
        var mapName = game.MapName ?? "";
        if (mapName.StartsWith("cp_", StringComparison.OrdinalIgnoreCase))
        {
            return "control point";
        }

        if (mapName.StartsWith("ctf_", StringComparison.OrdinalIgnoreCase))
        {
            return "intelligence";
        }

        return "spawn";
    }

    private static string SourceMapName(GameManagerSession game)
    {
        return string.IsNullOrWhiteSpace(game.MapName) ? "ctf_2fort" : game.MapName;
    }

    private static string SourceSkyName(string mapName)
    {
        if (mapName.Contains("dustbowl", StringComparison.OrdinalIgnoreCase))
        {
            return "sky_dustbowl_01";
        }

        return "sky_tf2_04";
    }

    private static uint StableSourceDigest32(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;
        foreach (var ch in value.ToLowerInvariant())
        {
            hash ^= ch;
            hash *= prime;
        }

        return hash == 0 ? offsetBasis : hash;
    }

    private static string SourceTeamGoalString(GameManagerSession game, bool red)
    {
        var mapName = game.MapName ?? "";
        if (mapName.StartsWith("cp_", StringComparison.OrdinalIgnoreCase)
            || game.GameMode.Contains("control", StringComparison.OrdinalIgnoreCase))
        {
            return red ? "Defend the control points." : "Capture the control points.";
        }

        if (mapName.StartsWith("ctf_", StringComparison.OrdinalIgnoreCase)
            || game.GameMode.Contains("flag", StringComparison.OrdinalIgnoreCase))
        {
            return red ? "Defend your intelligence." : "Capture the enemy intelligence.";
        }

        return red ? "Defend the objective." : "Capture the objective.";
    }

    private static byte[] ToBitPayload(string value)
    {
        return Encoding.ASCII.GetBytes(value);
    }

    private static Ps3SourceNetMessageFrame[] BuildBootstrapPrecacheStringTableFrames(string mapName)
    {
        return Tf2Ps3SourceCatalog.BuildBootstrapPrecacheStringTables(mapName)
            .Select(table =>
            {
                var payload = BuildStringTableData(table.MaxEntries, table.Entries);
                return Ps3SourceNetMessages.BuildCreateStringTableFrame(new Ps3SourceSvcStringTable(
                    TableName: table.TableName,
                    MaxEntries: table.MaxEntries,
                    NumEntries: table.Entries.Count,
                    UserDataFixedSize: false,
                    UserDataSize: 0,
                    UserDataSizeBits: 0,
                    Data: payload.Payload,
                    DataBitCount: payload.BitCount));
            })
            .ToArray();
    }

    private static Ps3SourceNetMessageFrame BuildUserInfoStringTableData(string playerName)
    {
        return BuildStringTableData(2048, [playerName]);
    }

    private static Ps3SourceNetMessageFrame BuildStringTableData(
        ushort maxEntries,
        IReadOnlyList<string> values)
    {
        return Ps3SourceNetMessages.BuildStringTableUpdateDataFrame(
            maxEntries: maxEntries,
            entries: values
                .Select((value, index) => new Ps3SourceStringTableEntry(
                    Index: index,
                    Value: value,
                    UserData: [],
                    UserDataBitCount: 0))
                .ToArray());
    }

    private static void WriteAscii(Span<byte> destination, int offset, string value)
    {
        Encoding.ASCII.GetBytes(value, destination[offset..]);
    }

    private static void WriteAsciiPadded(Span<byte> destination, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }

    private static void WriteBoundedAscii(Span<byte> destination, string value)
    {
        destination.Clear();
        var bytes = Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, Math.Max(0, destination.Length - 1))).CopyTo(destination);
    }

    private static void WriteUInt32BigEndian(Span<byte> destination, uint value)
    {
        destination[0] = (byte)(value >> 24);
        destination[1] = (byte)(value >> 16);
        destination[2] = (byte)(value >> 8);
        destination[3] = (byte)value;
    }

    private static void FillDeterministic(Span<byte> destination, byte seed, int protectedPrefixBytes)
    {
        uint value = seed;
        for (var i = protectedPrefixBytes; i < destination.Length; i++)
        {
            value = unchecked(value * 1103515245u + 12345u + (uint)i);
            destination[i] = (byte)(value >> 16);
        }
    }

    private static bool HasEmbeddedObjectState(ReadOnlySpan<byte> body)
    {
        return IndexOf(body, "COc"u8) >= 0 || IndexOf(body, "PNG"u8) >= 0 || IndexOf(body, "DSC"u8) >= 0;
    }

    private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private enum LoadingContinuationFrameKind
    {
        NativeSnapshot,
        PlayerStateLink,
        NativeQueuedBoundary
    }

    private readonly record struct LoadingContinuationFrame(
        LoadingContinuationFrameKind Kind,
        int Length,
        int PrefixLength = 0,
        int MaxRecords = 0,
        int SuppressAfter = 0);

    private sealed record SourceObjectStateBatch(byte[] Body, int SequenceAdvance, string Explanation, int SuppressAfter = 0);
}

public sealed record Ps3NativeSourceResponse(byte[] Payload, string Explanation);
