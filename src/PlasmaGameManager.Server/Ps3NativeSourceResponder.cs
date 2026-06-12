using System.Text;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.Server;

public sealed class Ps3NativeSourceResponder
{
    public const string ImplementationLabel = "ps3-native-generated-sparse-snapshot-v20-frozen-state-client-graph";
    private const int FragmentPayloadThresholdBytes = 1000;
    private const int QueuedPeerChunkPayloadBytes = 1000;
    private const int SteadyCommandSnapshotClientPacketInterval = 16;
    private const int FastCommandSnapshotClientPacketInterval = 3;
    private const int MinimumCommandSnapshotClientPacketCount = 32;
    private const int FastCommandSnapshotMaxClientPacketCount = 66;
    private static readonly byte[] PostRosterLargeClientAckBody = Convert.FromHexString(
        "a23f118cd163aa55f0252338f1f8120e74f62066ab9dc80b982a93b3339f043c6c07ac5de1fe308ffd59a55fa2");
    private static readonly byte[] PostRosterContinuationBody = Convert.FromHexString(
        "066ce64dbe63ee9f7fa821cf94a5bd1b7a0da0ec051843770e653e7767c5af88bdf1d705b9e0ce03d7454f5ce5f48019ddbdd4b22ff49b3f8822aa84727fe395e9af9f9d7c61bb87eda458c76ca6e2cae36161c41682cd4598e52d0da183b3e385b8bfa35a323334fe43ea69c71d655acff7256433b2736709561bdaa2435552f1c3065863c8d03306de73bb8a66b1487e4394dff0a6ae2e4ab0ca76712bebb7c7a893e6923ec7a4233947f2b9dd8430078b595c12506df7d8a96b86ed065ba428acbda78294139885345f1447836be3991eaff20e8b4e6bc95c0c");
    private static readonly byte[] PostRosterClientBatchAckBody = Convert.FromHexString(
        "13fe27dd132371ae5dfeddf2e9c9a5466990a704ba19b7f4332914f58876e097");
    private static readonly byte[] PostRosterShortGameplayAckPrefix = Convert.FromHexString(
        "52983a3e59c6ea1fdea9130e746c3a0511291bd466f5aa4a1a9cd0f002b81743b0d3a5cc13de92f502d0398b");
    private static readonly byte[] PostRosterFrozenStateIntroPrefix = Convert.FromHexString(
        "b246eb36c35504f1e4006d60e435ef7f5c375b6563e6fab319");
    private static readonly byte[] PostRosterFrozenStatePlayerPrefix = Convert.FromHexString(
        "1b336d4e");
    private static readonly byte[] PostRosterFrozenStateFocusPrefix = Convert.FromHexString(
        "5594a53c9e7ca81e3746fcbbc0052ec38dc332ee0d7c7d");
    private static readonly byte[] EarlyLoadLargeCommandContinuationPrefix = Convert.FromHexString(
        "595ddd1340bea4edab092b7633902422d685173b698dce5e1c3bb7bf7b89bab064816d74a004ec657e714b02b977522b87a0e2e1cb9e3e88c9345dcfcec67fd70316f3623c3fc510b5d6ed5618214d2f95047451bd3da8110ea47dacb9985c1ee9005b9697686bc7a412ae7e391dec76c8cd3e48ed021931c3225b7941f4aced2d09b9f12dd164c9bfc50448334e1515c6d0ba18002b47e0aa6331254aa2a5b683ba53d2ab8427bf21b571317ea59eb10c1991d23ed262e36e8e8dfcca730381204346e9f003d5893ec782c64aaeae1adad3b59c0c1494813b0c7007329da7bb2955dd0c631a34d611218b8e5ecde3d4d0213bda9928a0a9d86118876acd498d5bd9a4ba162e4783821af327e79d6aff888d7583e35dd73ca7d5fe5bc0a59d60b440686bf4ef8e8f3b9ee6af3f1784f46550c75a0f109006c5b38886c9692ccdb4e440946716b7c527950ede599409401984771bdfa49257246a5baac5a2e40c1ea9d10e5d433f125f5a64928edaccf02ba00bad30c9798a523f7075e1d94dde9f33af0499a6678867be09dacab937344af1b813f30eab7862b78028e7c2bf5da55c0c4db91baa99d12432f4c6efabcbacd47b61f8c02b6c20f2b2ff5c442ad5f530ebf28df8058e26572d79db9a67c4f14a2a541904213f80ea686679c8cb6a9e0e9f1f5dc9958ffb8f1d3c4e20cab7b64e632e84b2cb885af44acb20104d4da545661358aa56f990c49f118b0c92ac6e265c756d5c2fb370c4e68caae5efde8f1d90c79bf5ef369d779d828bee6324d72eaad93d23823c28883f184c49c729c0adc0963c22");
    private static readonly byte[] QuickMatchTerminalPrompt1Body = Convert.FromHexString(
        "62e6413e138a4ed096b9406ffe5fad9635c8ddeeff6616dc57450604e51d44d19990920143e80d5fc5464185fa24267b7198dd94ec71811fc079e2a12db07dae97aa9951506bda78f05d8a5ecf56e7f2811a3e5e38bf1a4346e89122dfee06198af524dd90a3c1784f5052675fb3536e971f4f1206b3022763a25df64465aae729b14f2cac1c2cf042ca430276af450907d3a20c1bb42904331f62d37816aa730cd5339b5ad39dfbf3082aafcf0a141dc97b9613e93b10136a542ae903eb255154ed0bea449f62d888c25cd80fc552a9c3de0dcfedc468c77f16fe9c3f6a3217e2c772990414c70b2958c08931cda5afbd701a2be812c71cdec7aae006111116e86ea5a7cdd8c1eaac9404ef5bd43f4ed53215a3789ae4c8837191bef0abbb9599ee728949c56bd6134fac2ddb38fc78952c8024ad854af512ef74431a6c030ad10d579b74af9967f7313a36323ba3db7882efb4ee1528ed447e01668215359d7fe2126bb56a02fa9e52bf550f042bc3d84ad8a0859c82ff099376873bcf3b65ff7edf804df74e5bdaadbb88d2c32a1cf34fb1224b9d34d846825319a89ac97c1d92a898a73dba3ddab8edad7a3453a92ae86f2f6d8440591b9bd57b3d96da1be3f1869b9934896b415ecd3506ad6530edc62910b51181693350ccf033519297142ea03e3e9d810ad78cb61f9a633ad7a232bf6edd163a763fa412c043105025b33bf29c26803a66d9baf33bdd9d540d06a1f7f4fb5d2e96e1e3d1892bb9eb6652cf4fc32c19aa2e63925d9e57874280726e30c16b608210ecddc2ebb2b4");
    private static readonly byte[] QuickMatchTerminalPrompt2Body = Convert.FromHexString(
        "40cac21cfe0ce51a8cc4666e97e9a0fd038142d1fcc23d552516266c35372fcb102bfcbd758ff5d3d7095cb5fd5a3541490631a7440eb5b15b2a61f07d7965f11ceb324e37ff5e59c9d7a66ac2028fe7bdb3530b08a55453bda0f5f2efcebcecde6cf6d3fd379245af9656fdbf0338297377b98c1f93c4a4527855f7c0ddca52a062aaa39646ce6393ab13a08698c990efc7e4a7bbf104e6149a52b6c2803cec2f5ee109e73ca261f0f4b9ede8d51d3ab6e534c5b592ec1748a03e24cc75b39c8d30cda2d93d94c60751e2e00553226096b4e476f6c8e9eed72bffc2120fdcd510c16be2e78467ecb8ff521ffed5f6bd5899bc1013e975477b284cd7b58307c8fbd67e036a16dbdfdd25b2a79e12652017884d0c6009d04d5e5c278f74d210b2bb654e2ca86145b013f56ebb49cbc5a1f7d5031e6d5b4aa626c1b899ea8fc771e2dbcf4fec890d6f3120048f090664083030a1a2ca2b5d8a585c02ccc27ac72bda5aba101da3aec361a590ce25d42078d5b2434864355879910246c487c94006e8a78fa5a845302ecf87008eeadf323fe1b9e1798d933fba1acb70d54e81e5747220b31fc17660dfe8b559f8bc70ff25f940dab31c52fce3870c0390c28c003fe45bd6062e6b966b6625b99984f5e53b001508c89ee71754f9a89635345bc5db66751ad14c75b2c659df827eb0a12717e6df8a1169d09aa0e60dafbeda3c2b184914e85e445c16bd340297e26df2d64489081d0ba6021f002e82075dbacea24b5e0c5b0a71d26a3b84a4fabc6880987f6251e5c0");
    private static readonly byte[] PostTerminalMapLoadStateAckPrefix = Convert.FromHexString(
        "0782c6a02fa2251502dccc22f9ea43e07f94b56b8be89ae0bf96070f88aeaa990b787336cf24b2b10bc3eb719885546320ead6ec631b18ca0c22cdfc050c1e2e8be178b6afd08f2c1710b83cb89edeb2605de776addf1845808a66dffc9a0e516723");
    public const ushort SparseQuickMatchLateLoadingSequence = 1000;
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
            new(LoadingContinuationFrameKind.HighEntropy, 1128),
            new(LoadingContinuationFrameKind.HighEntropy, 846)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 982)],
        [new(LoadingContinuationFrameKind.HighEntropy, 704)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 982),
            new(LoadingContinuationFrameKind.HighEntropy, 944),
            new(LoadingContinuationFrameKind.MixedBinary, 56)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 944)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 714),
            new(LoadingContinuationFrameKind.HighEntropy, 1184),
            new(LoadingContinuationFrameKind.HighEntropy, 282),
            new(LoadingContinuationFrameKind.HighEntropy, 460)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 42, 10, 2),
            new(LoadingContinuationFrameKind.HighEntropy, 1198),
            new(LoadingContinuationFrameKind.HighEntropy, 564)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 450)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 1138),
            new(LoadingContinuationFrameKind.HighEntropy, 338)
        ],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 450),
            new(LoadingContinuationFrameKind.HighEntropy, 1180),
            new(LoadingContinuationFrameKind.HighEntropy, 634)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 676)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 1079),
            new(LoadingContinuationFrameKind.HighEntropy, 958),
            new(LoadingContinuationFrameKind.HighEntropy, 450)
        ],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 28),
            new(LoadingContinuationFrameKind.HighEntropy, 686),
            new(LoadingContinuationFrameKind.HighEntropy, 846)
        ],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 704),
            new(LoadingContinuationFrameKind.HighEntropy, 1156)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 574)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 196),
            new(LoadingContinuationFrameKind.HighEntropy, 1156),
            new(LoadingContinuationFrameKind.HighEntropy, 620)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 206, 14, 8),
            new(LoadingContinuationFrameKind.HighEntropy, 282),
            new(LoadingContinuationFrameKind.HighEntropy, 1111),
            new(LoadingContinuationFrameKind.HighEntropy, 300)
        ],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 638),
            new(LoadingContinuationFrameKind.HighEntropy, 1128),
            new(LoadingContinuationFrameKind.HighEntropy, 338)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 168, 24, 7),
            new(LoadingContinuationFrameKind.HighEntropy, 874)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 1222)],
        [new(LoadingContinuationFrameKind.HighEntropy, 1212)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 206),
            new(LoadingContinuationFrameKind.HighEntropy, 1212),
            new(LoadingContinuationFrameKind.HighEntropy, 296)
        ],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 944),
            new(LoadingContinuationFrameKind.MixedBinary, 66),
            new(LoadingContinuationFrameKind.HighEntropy, 1128),
            new(LoadingContinuationFrameKind.HighEntropy, 338)
        ],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 450),
            new(LoadingContinuationFrameKind.HighEntropy, 1138),
            new(LoadingContinuationFrameKind.HighEntropy, 846)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 182, 14, 7),
            new(LoadingContinuationFrameKind.HighEntropy, 592),
            new(LoadingContinuationFrameKind.HighEntropy, 1142),
            new(LoadingContinuationFrameKind.HighEntropy, 602)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 514)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 196),
            new(LoadingContinuationFrameKind.PlayerStateLink, 220, 14, 8),
            new(LoadingContinuationFrameKind.PlayerStateLink, 168, 24, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.MixedBinary, 206)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 196)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 210, 14, 8)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 220),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 196, 24, 8),
            new(LoadingContinuationFrameKind.MixedBinary, 28)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 206)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 52, 16, 3)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4),
            new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 21)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 38),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 10)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5),
            new(LoadingContinuationFrameKind.MixedBinary, 21)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 28)],
        [new(LoadingContinuationFrameKind.MixedBinary, 10)],
        [new(LoadingContinuationFrameKind.MixedBinary, 21)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4, SuppressAfter: 1)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 24, 4, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)],
        [new(LoadingContinuationFrameKind.MixedBinary, 48)],
        [new(LoadingContinuationFrameKind.MixedBinary, 28)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 10),
            new(LoadingContinuationFrameKind.MixedBinary, 21),
            new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 24),
            new(LoadingContinuationFrameKind.MixedBinary, 28)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5, SuppressAfter: 1)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2)],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 66, 18, 4, SuppressAfter: 1)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 28),
            new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5),
            new(LoadingContinuationFrameKind.MixedBinary, 21)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.MixedBinary, 10)
        ],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 21),
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 91, 13, 6)],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 52, 16, 3)],
        [new(LoadingContinuationFrameKind.MixedBinary, 21, SuppressAfter: 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 91, 13, 6)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2),
            new(LoadingContinuationFrameKind.MixedBinary, 21)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 21)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 38),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 24, 4, 2)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4),
            new(LoadingContinuationFrameKind.MixedBinary, 21)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 49)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 21),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 21)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4),
            new(LoadingContinuationFrameKind.MixedBinary, 38)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 24, 4, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2, SuppressAfter: 1)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5)],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 52, 16, 3)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 21)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 38),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 31)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2, SuppressAfter: 1)
        ],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 48),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 21)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 94, 14, 7, SuppressAfter: 1)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4),
            new(LoadingContinuationFrameKind.PlayerStateLink, 49, 25, 2, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 21)],
        [new(LoadingContinuationFrameKind.MixedBinary, 49)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2, SuppressAfter: 1)],
        [new(LoadingContinuationFrameKind.MixedBinary, 21)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 77, 13, 5)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 48),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 10)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 91, 13, 6),
            new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2)
        ],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 21),
            new(LoadingContinuationFrameKind.MixedBinary, 38)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 66, 18, 4),
            new(LoadingContinuationFrameKind.PlayerStateLink, 28, 4, 2)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 21, SuppressAfter: 1)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 91, 13, 6)],
        [new(LoadingContinuationFrameKind.MixedBinary, 38)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2),
            new(LoadingContinuationFrameKind.PlayerStateLink, 56, 8, 4)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 35, 11, 2)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 63, 15, 4)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 38, 14, 2)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 38),
            new(LoadingContinuationFrameKind.HighEntropy, 1149),
            new(LoadingContinuationFrameKind.HighEntropy, 846)
        ],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 436),
            new(LoadingContinuationFrameKind.HighEntropy, 324),
            new(LoadingContinuationFrameKind.HighEntropy, 1152),
            new(LoadingContinuationFrameKind.HighEntropy, 479)
        ],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 196),
            new(LoadingContinuationFrameKind.HighEntropy, 216),
            new(LoadingContinuationFrameKind.PlayerStateLink, 182, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 84, 12, 6)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 685)],
        [new(LoadingContinuationFrameKind.HighEntropy, 579)],
        [new(LoadingContinuationFrameKind.HighEntropy, 564)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 602),
            new(LoadingContinuationFrameKind.HighEntropy, 606),
            new(LoadingContinuationFrameKind.HighEntropy, 860)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 320)],
        [new(LoadingContinuationFrameKind.HighEntropy, 564)],
        [new(LoadingContinuationFrameKind.HighEntropy, 606)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 602),
            new(LoadingContinuationFrameKind.HighEntropy, 860)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 574)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 578),
            new(LoadingContinuationFrameKind.HighEntropy, 620),
            new(LoadingContinuationFrameKind.HighEntropy, 588)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 592)],
        [new(LoadingContinuationFrameKind.HighEntropy, 564)],
        [new(LoadingContinuationFrameKind.HighEntropy, 588)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 411),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)
        ],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 66),
            new(LoadingContinuationFrameKind.MixedBinary, 84)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 122, 10, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 84, 12, 6),
            new(LoadingContinuationFrameKind.MixedBinary, 56)
        ],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 66),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 112, 14, 8),
            new(LoadingContinuationFrameKind.PlayerStateLink, 80, 8, 6)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 56)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 56),
            new(LoadingContinuationFrameKind.PlayerStateLink, 122, 10, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 80, 8, 6)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 56)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 122, 10, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.MixedBinary, 56)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 66)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 112, 14, 8),
            new(LoadingContinuationFrameKind.PlayerStateLink, 108, 12, 8),
            new(LoadingContinuationFrameKind.MixedBinary, 56)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 66)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 126, 14, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.MixedBinary, 66)
        ],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 84),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.PlayerStateLink, 122, 10, 9),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.MixedBinary, 56)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 94)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7)],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 94, 14, 7)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5),
            new(LoadingContinuationFrameKind.MixedBinary, 56),
            new(LoadingContinuationFrameKind.PlayerStateLink, 108, 12, 8)
        ],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 84, 12, 6),
            new(LoadingContinuationFrameKind.MixedBinary, 66, SuppressAfter: 1)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 56)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 84, 12, 6),
            new(LoadingContinuationFrameKind.PlayerStateLink, 136, 12, 10),
            new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 66)],
        [new(LoadingContinuationFrameKind.MixedBinary, 56)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 136, 12, 10),
            new(LoadingContinuationFrameKind.MixedBinary, 56)
        ],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 56),
            new(LoadingContinuationFrameKind.PlayerStateLink, 94, 14, 7),
            new(LoadingContinuationFrameKind.PlayerStateLink, 112, 14, 8),
            new(LoadingContinuationFrameKind.MixedBinary, 94)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 56)],
        [
            new(LoadingContinuationFrameKind.MixedBinary, 56),
            new(LoadingContinuationFrameKind.PlayerStateLink, 274, 18, 18),
            new(LoadingContinuationFrameKind.PlayerStateLink, 98, 14, 7),
            new(LoadingContinuationFrameKind.MixedBinary, 56),
            new(LoadingContinuationFrameKind.MixedBinary, 94)
        ],
        [new(LoadingContinuationFrameKind.PlayerStateLink, 70, 10, 5)],
        [
            new(LoadingContinuationFrameKind.PlayerStateLink, 126, 14, 9),
            new(LoadingContinuationFrameKind.MixedBinary, 66)
        ],
        [new(LoadingContinuationFrameKind.MixedBinary, 56)],
        [new(LoadingContinuationFrameKind.HighEntropy, 609)],
        [
            new(LoadingContinuationFrameKind.HighEntropy, 606),
            new(LoadingContinuationFrameKind.HighEntropy, 606)
        ],
        [new(LoadingContinuationFrameKind.HighEntropy, 574)],
        [new(LoadingContinuationFrameKind.HighEntropy, 564)],
        [new(LoadingContinuationFrameKind.HighEntropy, 860, SuppressAfter: 1)]
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
            AddPacket(responses, state, BuildServerInfoBody(game, player, 112), "generated PS3 Source native 0x49 server-info bitstream");
            if (state.InitialSetupVariant == 2)
            {
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 17, 0x49), "generated PS3 Source native server-info short tail");
            }
            else if (state.InitialSetupVariant == 4)
            {
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 17, 0x49), "generated PS3 Source native custom-match server-info short tail");
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 17, 0x4a), "generated PS3 Source native custom-match server-info short tail");
                state.SuppressObjectStateIntroResponses = Math.Max(state.SuppressObjectStateIntroResponses, 1);
            }

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

        var sentObjectIntroThisTurn = false;
        if (!state.SentObjectState && ShouldSendObjectState(state, player, clientBodyForHandling))
        {
            if (state.SuppressObjectStateIntroResponses > 0)
            {
                state.SuppressObjectStateIntroResponses--;
                AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for object-intro pacing");
                return responses;
            }

            state.SentObjectState = true;
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
            if (state.SuppressLoadingContinuationResponses > 0)
            {
                state.SuppressLoadingContinuationResponses--;
                AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for loading pacing");
                return responses;
            }

            if (state.InitialSetupVariant == 1 && state.ClientPacketCount - state.LoadingStateLinkBurstClientPacketCount <= 1)
            {
                AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for loading post-burst gate");
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
            && state.SuppressLoadingContinuationResponses > 0)
        {
            responses.Clear();
            state.SuppressLoadingContinuationResponses--;
            AddFallbackShortControlIfNeeded(responses, state, clientPacket.CandidateSequence, "generated PS3 Source short control/ack for loading continuation pacing");
            return responses;
        }

        if (!sentObjectIntroThisTurn
            && ObjectStateIntroComplete(state, game, player)
            && !state.SentRosterDescriptorState
            && state.ClientPacketCount % 8 == 0)
        {
            AddPacket(responses, state, BuildPlayerStateLinkBody(game, player, 30, 4), "generated PS3 Source native PNG state-link heartbeat", sequenceAdvance: 3);
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
            AddPacket(
                responses,
                state,
                PostRosterLargeClientAckBody,
                "generated PS3 Source native post-roster large-client ack",
                sequenceAdvance: 12);
            state.PostRosterContinuationStage = 1;
            state.QuickMatchTerminalPromptStage = 0;
            return responses;
        }

        if (state.SentRosterDescriptorState)
        {
            AddPendingSourceServerCommandEvents(responses, state, game, player);
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
                QuickMatchTerminalPrompt2Body,
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
                PostRosterClientBatchAckBody,
                "generated PS3 Source native post-roster client object-batch ack",
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
                BuildPostRosterShortGameplayAckBody(player),
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
                QuickMatchTerminalPrompt1Body,
                "generated PS3 Source native quick-match terminal upload prompt 1",
                sequenceAdvance: 144);
            state.QuickMatchTerminalPromptStage = 1;
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
                QuickMatchTerminalPrompt2Body,
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
            state.LateLargeCommandFollowupClientPacketCount = 0;
            state.PostTerminalMapLoadClientPacketCount = 0;
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldWaitForQuickMatchTerminalUpload(state, clientPacket.PayloadLength))
        {
            responses.Clear();
            return responses;
        }

        if (state.SentRosterDescriptorState
            && ShouldSendLateLargeCommandOpaqueContinuation(state, player, clientPacket.PayloadLength))
        {
            responses.Clear();
            AddPacket(
                responses,
                state,
                BuildLateLargeCommandOpaqueContinuationBody(game, player, state),
                "generated PS3 Source native late large-command opaque continuation",
                sequenceAdvance: 146);
            state.SentLateLargeCommandContinuation = true;
            state.LateLargeCommandFollowupClientPacketCount = 0;
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
                    BuildPostTerminalMapLoadStateAckBody(player),
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
            AddPacket(
                responses,
                state,
                PostRosterContinuationBody,
                "generated PS3 Source native post-roster continuation frame",
                sequenceAdvance: 55);
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
        var stringTablePayload = ToBitPayload(playerName + "\0");
        var sendTablePayload = ToBitPayload(string.Join('\0', Tf2Ps3SourceCatalog.BootstrapSendTables) + "\0");
        var classInfo = new Ps3SourceSvcClassInfo(
            NumServerClasses: checked((short)Tf2Ps3SourceCatalog.ServerClasses.Count),
            CreateOnClient: false,
            Classes: Tf2Ps3SourceCatalog.ServerClasses);

        return
        [
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
                Data: stringTablePayload,
                DataBitCount: stringTablePayload.Length * 8)),
            Ps3SourceNetMessages.BuildUpdateStringTableFrame(new Ps3SourceSvcStringTableUpdate(
                TableId: 0,
                ChangedEntries: 1,
                Data: stringTablePayload,
                DataBitCount: stringTablePayload.Length * 8)),
            Ps3SourceNetMessages.BuildPacketEntitiesFrame(new Ps3SourceSvcPacketEntities(
                MaxEntries: 2047,
                IsDelta: false,
                DeltaFrom: 0,
                Baseline: 0,
                UpdatedEntries: 0,
                UpdateBaseline: false,
                Data: [],
                DataBitCount: 0))
        ];
    }

    public IReadOnlyList<byte[]> BuildDiagnosticCriticalSourceObjectStreamBootstrap(GameManagerSession game, PlayerSession player)
    {
        var ownerOrCallbackId = player.SourceState.RootObjectId == 0
            ? checked((uint)Math.Max(player.PlayerId, 0))
            : checked((uint)player.SourceState.RootObjectId);
        return BuildDiagnosticCriticalSourceNetMessageBootstrapFrames(game, player)
            .Select((frame, index) => Ps3SourceObjectStream.Encode(new Ps3SourceObjectStreamRecord(
                MessageKind: index == 4 ? (byte)2 : (byte)1,
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

        var spawnCount = 1;
        var viewEntityIndex = checked((int)(player.SourceState.LocalPlayerObjectId == 0
            ? player.SourceState.RootObjectId
            : player.SourceState.LocalPlayerObjectId));
        var signonState3 = Ps3SourceNetMessages.BuildNetSignonStateFrame(new Ps3SourceNetSignonState(3, spawnCount));
        var signonState4 = Ps3SourceNetMessages.BuildNetSignonStateFrame(new Ps3SourceNetSignonState(4, spawnCount));
        var setView = Ps3SourceNetMessages.BuildSetViewFrame(new Ps3SourceSvcSetView(viewEntityIndex));
        var signonState5 = Ps3SourceNetMessages.BuildNetSignonStateFrame(new Ps3SourceNetSignonState(5, spawnCount));
        Ps3SourceNetMessageFrame[] batches =
        [
            Ps3SourceNetMessages.ConcatenateFrames([frames[0], frames[1], frames[3], signonState3]),
            Ps3SourceNetMessages.ConcatenateFrames([frames[2], signonState4]),
            Ps3SourceNetMessages.ConcatenateFrames([setView, signonState5]),
            frames[4]
        ];

        return batches
            .Select((frame, index) => Ps3SourceObjectStream.Encode(new Ps3SourceObjectStreamRecord(
                MessageKind: index == 3 ? (byte)2 : (byte)1,
                OwnerOrCallbackId: ownerOrCallbackId,
                Sequence: checked((uint)(index + 1)),
                Payload: frame.Payload,
                PayloadBitCount: frame.BitCount)))
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
    }

    private void AddObjectStreamBootstrapResponses(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player,
        string explanationPrefix)
    {
        var batches = BuildDiagnosticCriticalSourceObjectStreamBootstrapBatches(game, player);
        for (var i = 0; i < batches.Count; i++)
        {
            AddPacket(
                responses,
                state,
                batches[i],
                $"{explanationPrefix} {i + 1}");
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
        AddPacket(
            responses,
            state,
            BuildPlayerStateLinkBody(game, player, shape.Length, shape.PrefixLength, shape.MaxRecords),
            "generated PS3 Source native loading PNG state-link heartbeat",
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
            state.SuppressLoadingContinuationResponses = suppressAfter;
        }
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
            var body = frame.Kind switch
            {
                LoadingContinuationFrameKind.PlayerStateLink when seedStageIndex >= 200
                    && TryBuildSteadyNativeSnapshotBody(game, player, frame.Length, seed, seedStageIndex, out var nativeSnapshot) =>
                    nativeSnapshot,
                LoadingContinuationFrameKind.PlayerStateLink when ShouldBuildPlayerStateLinkAsHighEntropy(initialSetupVariant, frame.Length, seedStageIndex) =>
                    BuildHighEntropyBinaryBody(game, player, frame.Length, seed),
                LoadingContinuationFrameKind.PlayerStateLink => BuildPlayerStateLinkBody(
                    game,
                    player,
                    frame.Length,
                    frame.PrefixLength,
                    frame.MaxRecords),
                LoadingContinuationFrameKind.MixedBinary when seedStageIndex >= 200
                    && TryBuildSteadyNativeSnapshotBody(game, player, frame.Length, seed, seedStageIndex, out var nativeSnapshot) =>
                    nativeSnapshot,
                LoadingContinuationFrameKind.MixedBinary => BuildMixedBinaryBody(
                    game,
                    player,
                    frame.Length,
                    (byte)seed),
                LoadingContinuationFrameKind.HighEntropy when seedStageIndex >= 200
                    && TryBuildSteadyNativeSnapshotBody(game, player, frame.Length, seed, seedStageIndex, out var nativeSnapshot) =>
                    nativeSnapshot,
                _ => BuildHighEntropyBinaryBody(game, player, frame.Length, seed)
            };

            AddPacket(
                responses,
                state,
                body,
                frame.Kind switch
                {
                    LoadingContinuationFrameKind.PlayerStateLink => $"{explanation} PNG state-link",
                    LoadingContinuationFrameKind.MixedBinary => $"{explanation} mixed",
                    _ => $"{explanation} binary"
                },
                sequenceAdvance: LoadingContinuationSequenceAdvance(frame.Length));
        }
    }

    private static bool ShouldBuildPlayerStateLinkAsHighEntropy(int initialSetupVariant, int length, int seedStageIndex)
    {
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
        var commandCadence = state.ClientPacketCount >= MinimumCommandSnapshotClientPacketCount
            && player.SourceState.LastClientSourceBodyLength >= 69
            && player.SourceState.LastClientSourceSequenceDelta is >= 16 and <= 20;
        var decodedCommandCadence = player.SourceState.LastClientCommandDecoded
            && state.ClientPacketCount >= MinimumCommandSnapshotClientPacketCount;
        var lateClientPacketCount = state.ClientPacketCount >= 64;
        var lateClientSequence = clientSequence >= SparseQuickMatchLateLoadingSequence
            || player.SourceState.LastClientSourceSequence >= SparseQuickMatchLateLoadingSequence;

        return state.SentLoadingPostBurstContinuation
            && !state.SentRosterDescriptorState
            && (lateClientSequence
                || state.LoadingContinuationStage >= 120
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
            _ => [new(LoadingContinuationFrameKind.PlayerStateLink, 1112, 28, 9), new(LoadingContinuationFrameKind.HighEntropy, 1212)]
        };
    }

    private static LoadingContinuationFrame[] LoadingPostBurstFramesFor(int initialSetupVariant)
    {
        return initialSetupVariant switch
        {
            2 => Variant2LoadingPostBurstFrames,
            3 => Variant3LoadingPostBurstFrames,
            4 => Variant4LoadingPostBurstFrames,
            _ => [new(LoadingContinuationFrameKind.HighEntropy, 206)]
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
            'M' => new LoadingContinuationFrame(LoadingContinuationFrameKind.MixedBinary, length),
            _ => new LoadingContinuationFrame(LoadingContinuationFrameKind.HighEntropy, length)
        };
    }

    private static LoadingContinuationFrame PlayerStateLinkFrame(int length)
    {
        var (prefixLength, maxRecords) = length switch
        {
            >= 1000 => (28, 9),
            >= 600 => (24, 9),
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

    private static bool ShouldSuppressNextLoadingContinuationResponse(int stageIndex, IReadOnlyList<LoadingContinuationFrame> frames)
    {
        if (stageIndex >= 48)
        {
            if (frames.Count == 3
                && frames[0].Kind == LoadingContinuationFrameKind.MixedBinary
                && frames[1].Kind == LoadingContinuationFrameKind.PlayerStateLink
                && frames[2].Kind == LoadingContinuationFrameKind.MixedBinary
                && (frames[0].Length, frames[1].Length, frames[2].Length) is (28, 77, 21))
            {
                return true;
            }

            if (frames.Count == 2
                && ((frames[0].Kind, frames[0].Length, frames[1].Kind, frames[1].Length) is
                    (LoadingContinuationFrameKind.PlayerStateLink, 70, LoadingContinuationFrameKind.MixedBinary, 10) or
                    (LoadingContinuationFrameKind.PlayerStateLink, 77, LoadingContinuationFrameKind.PlayerStateLink, 35)))
            {
                return true;
            }
        }

        if (frames.Count == 3
            && frames[0].Kind == LoadingContinuationFrameKind.MixedBinary
            && frames[1].Kind == LoadingContinuationFrameKind.MixedBinary
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
        AddPacket(
            responses,
            state,
            BuildPlayerStateLinkBody(game, player, shape.Length, shape.PrefixLength, shape.MaxRecords),
            "generated PS3 Source native steady PNG state-link heartbeat",
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

    private static bool TryBuildSteadyNativeSnapshotBody(
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
                if (greedy.Length <= targetLength)
                {
                    body = PadNativeWrappedPayload(greedy, targetLength, seed ^ checked((uint)frameIndex));
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

                body = PadNativeWrappedPayload(directPayload, targetLength, seed ^ checked((uint)frameIndex));
                state.SnapshotBaseFrame = state.SnapshotFrameIndex;
                state.SnapshotFrameIndex++;
                return true;
            }

            body = PadNativeWrappedPayload(wrapped, targetLength, seed ^ checked((uint)frameIndex));
            state.SnapshotBaseFrame = state.SnapshotFrameIndex;
            state.SnapshotFrameIndex++;
            return true;
        }

        return false;
    }

    private static void AddNextObjectStateIntroTurn(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        var batches = BuildObjectStateIntroBatches(game, player, state.InitialSetupVariant);
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

        if (startingBatchIndex == 0 && state.InitialSetupVariant == 1)
        {
            suppressAfter = Math.Max(suppressAfter, 1);
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
            && state.ObjectStateIntroBatchIndex >= BuildObjectStateIntroBatches(game, player, state.InitialSetupVariant).Length;
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
            && IsQuickMatchTerminalPrompt1Upload(clientPayloadLength);
    }

    private static bool ShouldSendQuickMatchTerminalMapLoad(
        Ps3NativeSourceResponderState state,
        int clientPayloadLength)
    {
        return !state.SentQuickMatchTerminalMapLoad
            && !state.SentLateLargeCommandContinuation
            && state.PostRosterContinuationStage == 0
            && clientPayloadLength == 56
            && state.QuickMatchTerminalPromptStage is 1 or 2;
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
        ReadOnlySpan<byte> clientBody)
    {
        if (!state.SentServerInfo)
        {
            return false;
        }

        return HasEmbeddedObjectState(clientBody)
            || player.SourceState.LastClientSourceRole is Ps3SourceClientPayloadRole.ReliableAssociationProbe
                or Ps3SourceClientPayloadRole.AttachedPlayerPayloadFrame
                or Ps3SourceClientPayloadRole.UserCommandCandidate
                or Ps3SourceClientPayloadRole.BinaryControlPayload
            || state.ClientPacketCount >= 3;
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
        if (!allowFragmentation || staged.Payload.Length <= FragmentPayloadThresholdBytes)
        {
            responses.Add(BuildPacket(state, staged.Payload, stagedExplanation, sequenceAdvance));
            return;
        }

        responses.AddRange(BuildQueuedPeerPackets(state, staged.Payload, stagedExplanation, staged.WrappedOrCompressed));
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
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 21, 0x44), "generated PS3 Source initial short setup/control");
                state.InitialSetupContinuationStage = 1;
                return;
            case 4:
                state.InitialSetupContinuationStage = 1;
                return;
            case 5:
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 21, 0x44), "generated PS3 Source custom-match initial short setup/control");
                AddPacket(responses, state, BuildResourceStringTableBody(game, player, 581), "generated PS3 Source custom-match resource/class setup table");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player, 316), "generated PS3 Source custom-match player summary setup tail");
                state.InitialSetupContinuationStage = 1;
                state.SuppressInitialSetupContinuationResponses = 1;
                return;
            default:
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 21, 0x44), "generated PS3 Source initial short setup/control");
                AddPacket(responses, state, BuildResourceStringTableBody(game, player, 573), "generated PS3 Source native resource/class setup table");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player, 81), "generated PS3 Source initial player summary setup tail");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player, 81), "generated PS3 Source initial player summary setup tail");
                return;
        }
    }

    private static void AddInitialSetupContinuationResponses(
        List<Ps3NativeSourceResponse> responses,
        Ps3NativeSourceResponderState state,
        GameManagerSession game,
        PlayerSession player)
    {
        switch (state.InitialSetupVariant)
        {
            case 2:
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 21, 0x44), "generated PS3 Source deferred short setup/control");
                AddPacket(responses, state, BuildResourceStringTableBody(game, player, 515), "generated PS3 Source deferred resource/class setup table");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player, 161), "generated PS3 Source deferred player summary setup tail");
                break;
            case 3:
                AddPacket(responses, state, BuildResourceStringTableBody(game, player, 562), "generated PS3 Source deferred resource/class setup table");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player, 242), "generated PS3 Source deferred player summary setup tail");
                break;
            case 4:
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 21, 0x44), "generated PS3 Source custom-match deferred short setup/control");
                AddPacket(responses, state, BuildResourceStringTableBody(game, player, 583), "generated PS3 Source custom-match deferred resource/class setup table");
                AddPacket(responses, state, BuildPlayerSummaryBody(game, player, 318), "generated PS3 Source custom-match deferred player summary setup tail");
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 81, 0x47), "generated PS3 Source custom-match deferred setup tail");
                break;
            case 5:
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 193, 0x45), "generated PS3 Source custom-match deferred server-info/setup table");
                AddPacket(responses, state, BuildMixedBinaryBody(game, player, 17, 0x46), "generated PS3 Source custom-match deferred short setup/control");
                state.SuppressObjectStateIntroResponses = Math.Max(state.SuppressObjectStateIntroResponses, 1);
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
        return
        [
            0x05,
            (byte)token,
            (byte)(token >> 8),
            (byte)(token >> 16),
            (byte)(token >> 24)
        ];
    }

    private static byte[] BuildPlayerSummaryBody(GameManagerSession game, PlayerSession player, int length)
    {
        var activePlayers = ActiveSourcePlayers(game).ToArray();
        if (activePlayers.Length == 0)
        {
            activePlayers = [player];
        }

        var bitstream = Ps3SourceNativeMessages.BuildPlayerSummary(
            (byte)Math.Clamp(activePlayers.Length, 0, 0xff),
            activePlayers
                .Select(candidate => new Ps3SourcePlayerSummaryEntry(
                    SourcePlayerSlotIndex(game, candidate),
                    string.IsNullOrWhiteSpace(candidate.Name) ? "Player" : candidate.Name,
                    ScoreOrStat: checked((int)Math.Min(candidate.SourceState.Score, int.MaxValue)),
                    FloatValue: candidate.SourceState.Health))
                .ToArray());
        return PadNativeBitstream(bitstream, length, 0x44, game, player);
    }

    private static byte[] BuildResourceStringTableBody(GameManagerSession game, PlayerSession player, int length)
    {
        var bitstream = Ps3SourceNativeMessages.BuildResourceStringTable(BuildResourceStringEntries(game, length));
        return PadNativeBitstream(bitstream, length, 0x45, game, player, highEntropyPadding: true);
    }

    private static Ps3SourceResourceStringEntry[] BuildResourceStringEntries(GameManagerSession game, int maxEncodedLength)
    {
        var mapName = string.IsNullOrWhiteSpace(game.MapName) ? "ctf_2fort" : game.MapName;
        var candidates = new List<Ps3SourceResourceStringEntry>
        {
            new("maps/" + mapName + ".bsp", "GAME"),
            new("motd.txt", "GAME"),
            new("cfg/MODSETTINGS.CFG", "GAME"),
            new("cfg/LISTENSERVER.CFG", "GAME"),
            new("maps/" + mapName + ".nav", "GAME"),
            new("scripts/items/items_game.txt", "GAME"),
            new("scripts/game_sounds_manifest.txt", "GAME"),
            new("resource/ClientScheme.res", "GAME"),
            new("resource/SourceScheme.res", "GAME"),
            new("materials/vgui/maps/menu_thumb_" + mapName + ".vmt", "GAME")
        };
        candidates.InsertRange(
            2,
            Tf2Ps3SourceCatalog.BootstrapSendTables.Select(static sendTable => new Ps3SourceResourceStringEntry(sendTable, "SENDTABLE")));

        var entries = new List<Ps3SourceResourceStringEntry>(candidates.Count);
        foreach (var candidate in candidates)
        {
            entries.Add(candidate);
            if (EncodedResourceStringTableLength(entries) <= maxEncodedLength)
            {
                continue;
            }

            entries.RemoveAt(entries.Count - 1);
            break;
        }

        return entries.ToArray();
    }

    private static int EncodedResourceStringTableLength(IReadOnlyList<Ps3SourceResourceStringEntry> entries)
    {
        var length = 7; // signed -1, message id, entry count
        foreach (var entry in entries)
        {
            length += Encoding.UTF8.GetByteCount(entry.ResourceName) + 1;
            length += Encoding.UTF8.GetByteCount(entry.Classification) + 1;
        }

        return length;
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

    private static byte[] BuildServerInfoBody(GameManagerSession game, PlayerSession player, int length)
    {
        var bitstream = Ps3SourceNativeMessages.BuildServerInfo(new Ps3SourceServerInfo(
            ServerName: game.Name.Length == 0 ? "A Game" : game.Name,
            MapName: game.MapName.Length == 0 ? "ctf_2fort" : game.MapName,
            GameDirectory: "tf",
            Description: player.Name.Length == 0 ? "Player" : player.Name,
            ListenPortOrNetworkShort: (short)Math.Clamp(game.MaxPlayers, 1, 32),
            CurrentPlayers: (byte)Math.Clamp(game.SourceVisiblePlayerCount, 0, 32),
            MaxPlayers: (byte)Math.Clamp(game.MaxPlayers, 1, 32),
            BotOrReservedCount: 0,
            ServerVariantCode: 100,
            PlatformCode: 0x6c,
            PasswordOrPrivateFlag: 0,
            ClientVisibleFlag: 1,
            ConnectionAddress: game.AdvertisedEndpoint));
        return PadNativeBitstream(bitstream, length, 0x49, game, player);
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

    private static byte[] BuildHighEntropyBinaryBody(GameManagerSession game, PlayerSession player, int length, uint seed)
    {
        var body = new byte[length];
        FillHighEntropyDeterministic(body, seed ^ checked((uint)game.GameId) ^ checked((uint)player.PlayerId));
        if (length is >= 128 and < 256)
        {
            for (var i = 0; i < body.Length; i++)
            {
                body[i] = (byte)((i * 251) + seed + game.GameId + player.PlayerId);
            }
        }

        return body;
    }

    private static byte[] BuildLateLargeCommandOpaqueContinuationBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        var body = new byte[630];
        EarlyLoadLargeCommandContinuationPrefix.CopyTo(body, 0);
        WritePlayerStateLinkTail(
            body,
            [0x00000010u, 0x00000012u, 0x0000000fu, 0x00000009u],
            0x0000001du);
        return body;
    }

    private static SourceObjectStateBatch[] BuildLateLargeCommandPrepBatches(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        var linkedObjectId = RootSourceObjectId(player);
        return
        [
            new SourceObjectStateBatch(
                BuildQueuedPlayerStateLinkBody(
                    game,
                    player,
                    state,
                    length: 602,
                    prefixLength: 578,
                    objectIds: [FrozenStateObjectId(player, 2), FrozenStateObjectId(player, 3)],
                    linkedObjectId: linkedObjectId,
                    family: "late-large-command-prep-segment-a"),
                SequenceAdvance: 145,
                Explanation: "generated PS3 Source native late large-command prep state-link segment A"),
            new SourceObjectStateBatch(
                BuildQueuedPlayerStateLinkBody(
                    game,
                    player,
                    state,
                    length: 606,
                    prefixLength: 570,
                    objectIds: [FrozenStateObjectId(player, 4), FrozenStateObjectId(player, 1), FrozenStateObjectId(player, 0)],
                    linkedObjectId: linkedObjectId,
                    family: "late-large-command-prep-segment-b"),
                SequenceAdvance: 143,
                Explanation: "generated PS3 Source native late large-command prep state-link segment B")
        ];
    }

    private static byte[] BuildLateLargeCommandFollowupBody(
        GameManagerSession game,
        PlayerSession player,
        Ps3NativeSourceResponderState state)
    {
        return BuildQueuedPlayerStateLinkBody(
            game,
            player,
            state,
            length: 860,
            prefixLength: 848,
            objectIds: [FrozenStateObjectId(player, 5)],
            linkedObjectId: RootSourceObjectId(player),
            family: "late-large-command-followup");
    }

    private static byte[] BuildPostTerminalMapLoadStateAckBody(PlayerSession player)
    {
        var body = new byte[PostTerminalMapLoadStateAckPrefix.Length + 24];
        PostTerminalMapLoadStateAckPrefix.CopyTo(body, 0);
        var linkedObjectId = RootSourceObjectId(player);
        var secondaryObjectId = linkedObjectId < uint.MaxValue
            ? linkedObjectId + 1
            : linkedObjectId;
        WritePlayerStateLinkTail(body, [0x00000003u, secondaryObjectId], linkedObjectId);
        return body;
    }

    private static byte[] BuildPostRosterShortGameplayAckBody(PlayerSession player)
    {
        var body = new byte[56];
        PostRosterShortGameplayAckPrefix.CopyTo(body, 0);
        var linkedObjectId = player.PlayerId > 0
            ? checked((uint)player.PlayerId)
            : RootSourceObjectId(player);
        var objectId = linkedObjectId > 0x17 ? linkedObjectId - 0x17 : 0x000000aeu;
        WritePlayerStateLinkTail(body, [objectId], linkedObjectId);
        return body;
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

    private static byte[] BuildQueuedPlayerStateLinkBody(
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
        FillHighEntropyDeterministic(body.AsSpan(0, prefixLength), seed);
        tail.CopyTo(body.AsSpan(prefixLength, tail.Length));
        if (prefixLength + tail.Length < length)
        {
            FillHighEntropyDeterministic(body.AsSpan(prefixLength + tail.Length), seed ^ 0xa5a55a5au);
        }

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

    private static byte[] BuildMixedBinaryBody(GameManagerSession game, PlayerSession player, int length, byte seed)
    {
        var body = new byte[length];
        for (var i = 0; i < body.Length; i++)
        {
            body[i] = (byte)(0x20 + (((i * 7) + seed + game.GameId + player.PlayerId) % 0x5f));
        }

        if (length >= 12)
        {
            body[0] = seed;
            body[1] = (byte)(game.GameId >> 8);
            body[2] = (byte)game.GameId;
            body[3] = (byte)player.PlayerId;
        }

        return body;
    }

    private static SourceObjectStateBatch[] BuildObjectStateIntroBatches(GameManagerSession game, PlayerSession player, int initialSetupVariant)
    {
        return initialSetupVariant switch
        {
            2 => BuildVariant2ObjectStateIntroBatches(game, player),
            3 => BuildVariant3ObjectStateIntroBatches(game, player),
            4 => BuildVariant4ObjectStateIntroBatches(game, player),
            5 => BuildVariant5ObjectStateIntroBatches(game, player),
            _ => BuildStandardObjectStateIntroBatches(game, player)
        };
    }

    private static SourceObjectStateBatch[] BuildStandardObjectStateIntroBatches(GameManagerSession game, PlayerSession player)
    {
        var peers = FrozenStateNamedPeers(player);
        var introPeers = peers.Take(4).ToArray();
        var secondaryPeers = peers.Skip(4).Take(2).ToArray();
        if (secondaryPeers.Length == 0)
        {
            secondaryPeers = [(PlayerSourceObjectId(player), player.Name)];
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
                BuildFrozenStateObjectBody(game, player, 78, 4, [(PlayerSourceObjectId(player), player.Name), .. secondaryPeers.Take(1)]),
                3,
                "generated PS3 Source native COc FrozenStateObject player batch"),
            new(
                BuildFrozenStateObjectBody(game, player, 60, 23, [(PlayerSourceObjectId(player), player.Name)]),
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
                BuildMixedBinaryBody(game, player, 21, 0x44),
                6,
                "generated PS3 Source native object-state short refresh"),
            new(
                BuildPlayerStateLinkBody(game, player, 49, 25, maxRecords: 2),
                7,
                "generated PS3 Source native PNG state-link intro batch"),
            new(
                BuildPlayerStateLinkBody(game, player, 63, 27, maxRecords: 3),
                7,
                "generated PS3 Source native PNG state-link mesh batch"),
            new(
                BuildPlayerStateLinkBody(game, player, 116, 20, maxRecords: 8),
                7,
                "generated PS3 Source native PNG state-link mesh extension batch"),
            new(
                BuildPlayerStateLinkBody(game, player, 28, 4, maxRecords: 2),
                3,
                "generated PS3 Source native PNG state-link object-intro tail")
        ];
    }

    private static SourceObjectStateBatch[] BuildVariant2ObjectStateIntroBatches(GameManagerSession game, PlayerSession player)
    {
        return
        [
            new(BuildMixedBinaryBody(game, player, 17, 0x51), 1, "generated PS3 Source variant2 object-intro short boundary"),
            new(BuildMixedBinaryBody(game, player, 17, 0x52), 1, "generated PS3 Source variant2 object-intro short boundary"),
            new(BuildFrozenStateObjectBody(game, player, 78, 4, FrozenStateNamedPeers(player).Take(2).ToArray()), 3, "generated PS3 Source variant2 COc FrozenStateObject batch"),
            new(BuildFrozenStateObjectBody(game, player, 198, 24, FrozenStateNamedPeers(player).Take(4).ToArray()), 7, "generated PS3 Source variant2 COc FrozenStateObject batch"),
            new(BuildMixedBinaryBody(game, player, 21, 0x53), 1, "generated PS3 Source variant2 object-intro short boundary"),
            new(BuildFrozenStateObjectBody(game, player, 49, 9, FrozenStateNamedPeers(player).Take(1).ToArray()), 3, "generated PS3 Source variant2 COc FrozenStateObject tail", SuppressAfter: 1),
            new(BuildFrozenStateObjectBody(game, player, 159, 21, FrozenStateNamedPeers(player).Take(3).ToArray()), 6, "generated PS3 Source variant2 COc FrozenStateObject tail"),
            new(BuildMixedBinaryBody(game, player, 53, 0x54), 3, "generated PS3 Source variant2 FrozenStateObject native boundary"),
            new(BuildMixedBinaryBody(game, player, 102, 0x55), 4, "generated PS3 Source variant2 object-state mixed boundary"),
            new(BuildPlayerStateLinkBody(game, player, 28, 4, maxRecords: 2), 3, "generated PS3 Source variant2 PNG state-link object-intro tail")
        ];
    }

    private static SourceObjectStateBatch[] BuildVariant3ObjectStateIntroBatches(GameManagerSession game, PlayerSession player)
    {
        return
        [
            new(BuildFrozenStateObjectBody(game, player, 73, 7, FrozenStateNamedPeers(player).Take(2).ToArray()), 3, "generated PS3 Source variant3 COc FrozenStateObject batch"),
            new(BuildFrozenStateObjectBody(game, player, 195, 23, FrozenStateNamedPeers(player).Take(4).ToArray()), 7, "generated PS3 Source variant3 COc FrozenStateObject batch"),
            new(BuildMixedBinaryBody(game, player, 56, 0x61), 3, "generated PS3 Source variant3 object-state mixed boundary"),
            new(BuildMixedBinaryBody(game, player, 17, 0x62), 1, "generated PS3 Source variant3 object-intro short boundary"),
            new(BuildPlayerStateLinkBody(game, player, 116, 20, maxRecords: 8), 7, "generated PS3 Source variant3 PNG state-link mesh extension batch"),
            new(BuildMixedBinaryBody(game, player, 74, 0x63), 4, "generated PS3 Source variant3 object-state mixed boundary"),
            new(BuildMixedBinaryBody(game, player, 21, 0x64), 1, "generated PS3 Source variant3 object-intro short boundary"),
            new(BuildFrozenStateObjectBody(game, player, 159, 21, FrozenStateNamedPeers(player).Take(3).ToArray()), 6, "generated PS3 Source variant3 COc FrozenStateObject tail", SuppressAfter: 1),
            new(BuildPlayerStateLinkBody(game, player, 28, 4, maxRecords: 2), 3, "generated PS3 Source variant3 PNG state-link object-intro tail"),
            new(BuildMixedBinaryBody(game, player, 53, 0x65), 3, "generated PS3 Source variant3 FrozenStateObject native boundary"),
            new(BuildMixedBinaryBody(game, player, 53, 0x66), 3, "generated PS3 Source variant3 FrozenStateObject native boundary"),
            new(BuildMixedBinaryBody(game, player, 102, 0x67), 4, "generated PS3 Source variant3 object-state mixed boundary"),
            new(BuildMixedBinaryBody(game, player, 56, 0x68), 3, "generated PS3 Source variant3 object-state mixed boundary")
        ];
    }

    private static SourceObjectStateBatch[] BuildVariant4ObjectStateIntroBatches(GameManagerSession game, PlayerSession player)
    {
        return
        [
            new(BuildMixedBinaryBody(game, player, 17, 0x71), 1, "generated PS3 Source custom-match variant4 object-intro short boundary"),
            new(BuildPlayerObjectBody(game, player, 138, 27), 6, "generated PS3 Source custom-match variant4 player-object batch"),
            new(BuildPlayerObjectBody(game, player, 177, 25), 7, "generated PS3 Source custom-match variant4 player-object batch"),
            new(BuildMixedBinaryBody(game, player, 21, 0x72), 1, "generated PS3 Source custom-match variant4 object-intro short boundary"),
            new(BuildPlayerObjectBody(game, player, 81, 7), 3, "generated PS3 Source custom-match variant4 player-object tail"),
            new(BuildPlayerObjectBody(game, player, 106, 4), 4, "generated PS3 Source custom-match variant4 roster-object tail"),
            new(BuildPlayerObjectBody(game, player, 120, 9), 4, "generated PS3 Source custom-match variant4 player-object tail"),
            new(BuildMixedBinaryBody(game, player, 60, 0x73), 3, "generated PS3 Source custom-match variant4 mixed boundary"),
            new(BuildPlayerStateLinkBody(game, player, 70, 10, maxRecords: 5), 4, "generated PS3 Source custom-match variant4 PNG state-link boundary")
        ];
    }

    private static SourceObjectStateBatch[] BuildVariant5ObjectStateIntroBatches(GameManagerSession game, PlayerSession player)
    {
        return
        [
            new(BuildMixedBinaryBody(game, player, 17, 0x81), 1, "generated PS3 Source custom-match variant5 object-intro short boundary"),
            new(BuildMixedBinaryBody(game, player, 17, 0x82), 1, "generated PS3 Source custom-match variant5 object-intro short boundary"),
            new(BuildPlayerObjectBody(game, player, 294, 18), 8, "generated PS3 Source custom-match variant5 player-object batch"),
            new(BuildPlayerObjectBody(game, player, 99, 12), 4, "generated PS3 Source custom-match variant5 player-object tail"),
            new(BuildMixedBinaryBody(game, player, 10, 0x83), 1, "generated PS3 Source custom-match variant5 object-intro short boundary"),
            new(BuildPlayerObjectBody(game, player, 77, 8), 4, "generated PS3 Source custom-match variant5 roster-object batch"),
            new(BuildPlayerStateLinkBody(game, player, 91, 13, maxRecords: 6), 4, "generated PS3 Source custom-match variant5 PNG state-link boundary"),
            new(BuildPlayerStateLinkBody(game, player, 35, 11, maxRecords: 2), 3, "generated PS3 Source custom-match variant5 PNG state-link boundary"),
            new(BuildMixedBinaryBody(game, player, 32, 0x84), 2, "generated PS3 Source custom-match variant5 mixed boundary")
        ];
    }

    private static SourceObjectStateBatch[] BuildPostRosterFrozenStateObjectBatches(GameManagerSession game, PlayerSession player)
    {
        var peers = FrozenStateNamedPeers(player);
        var introPeers = peers.Take(4).ToArray();
        var secondaryPeers = peers.Skip(4).Take(2).ToArray();
        if (secondaryPeers.Length == 0)
        {
            secondaryPeers = [(PlayerSourceObjectId(player), player.Name)];
        }

        return
        [
            new(
                BuildFrozenStateObjectBody(game, player, 173, 25, introPeers, useRetailPostRosterPrefix: true),
                7,
                "generated PS3 Source native COc FrozenStateObject intro batch"),
            new(
                BuildFrozenStateObjectBody(game, player, 78, 4, [(PlayerSourceObjectId(player), player.Name), .. secondaryPeers.Take(1)], useRetailPostRosterPrefix: true),
                3,
                "generated PS3 Source native COc FrozenStateObject player batch"),
            new(
                BuildFrozenStateObjectBody(game, player, 60, 23, [(PlayerSourceObjectId(player), player.Name)], useRetailPostRosterPrefix: true),
                6,
                "generated PS3 Source native COc FrozenStateObject focus batch")
        ];
    }

    private static (uint ObjectId, string Name)[] FrozenStateNamedPeers(PlayerSession player)
    {
        var names = new[] { "GR-NIGHT", "jojomissle", "hypertwins14", "FORdaLiberty", "MartinMathieu", "Peer" };
        var peers = player.SourceState.FrozenStatePeerObjectIds.Length == 0
            ? [0x9fU, 0x93U, 0x95U, 0x9cU, 0x6dU]
            : player.SourceState.FrozenStatePeerObjectIds;
        return peers
            .Select((objectId, index) => (objectId, names[Math.Min(index, names.Length - 1)]))
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

    private static byte[] BuildFrozenStateObjectBody(
        GameManagerSession game,
        PlayerSession player,
        int length,
        int prefixLength,
        IReadOnlyList<(uint ObjectId, string Name)> records,
        bool useRetailPostRosterPrefix = false)
    {
        var body = new byte[length];
        var prefix = useRetailPostRosterPrefix
            ? PostRosterFrozenStatePrefix(length, prefixLength)
            : ReadOnlySpan<byte>.Empty;
        if (!prefix.IsEmpty)
        {
            prefix.CopyTo(body);
        }
        else
        {
            FillDeterministic(body.AsSpan(0, Math.Min(prefixLength, body.Length)), (byte)(0x5a ^ game.GameId ^ player.PlayerId), 0);
        }

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

        if (useRetailPostRosterPrefix
            && length == 60
            && prefixLength == 23
            && records.Count == 1
            && body.Length > 0)
        {
            body[^1] = 1;
        }
        else if (offset < body.Length)
        {
            FillDeterministic(body.AsSpan(offset), (byte)(0x71 ^ game.GameId ^ player.PlayerId), 0);
        }

        return body;
    }

    private static ReadOnlySpan<byte> PostRosterFrozenStatePrefix(int length, int prefixLength)
    {
        return (length, prefixLength) switch
        {
            (173, 25) => PostRosterFrozenStateIntroPrefix,
            (78, 4) => PostRosterFrozenStatePlayerPrefix,
            (60, 23) => PostRosterFrozenStateFocusPrefix,
            _ => ReadOnlySpan<byte>.Empty
        };
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
                string.IsNullOrWhiteSpace(candidate.Name) ? $"player{candidate.PlayerId}" : candidate.Name));
        }

        records.Add((rootObjectId, PlayerSourceObjectId(recipient), string.IsNullOrWhiteSpace(recipient.Name) ? "Host" : recipient.Name));
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
            StatusText: $"{(string.IsNullOrWhiteSpace(player.Name) ? "Player" : player.Name)} joined {game.MapName}",
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
        return Ps3SourceEntityDeltaFrameBuilder.TryDecodeNativeRecords(section, out var records, out var consumedBits)
            && consumedBits > 0
            && consumedBits <= section.Length * 8
            && records.All(static record =>
                record.IsPartialRun
                && record.ObjectId.HasValue
                && !string.IsNullOrWhiteSpace(record.ObjectName)
                && record.ObjectName[0] == 'C');
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

    private static byte[] PadNativeWrappedPayload(byte[] payload, int targetLength, uint seed)
    {
        if (payload.Length == targetLength)
        {
            return payload;
        }

        var body = new byte[targetLength];
        payload.CopyTo(body.AsSpan());
        FillHighEntropyDeterministic(body.AsSpan(payload.Length), seed ^ 0xa4f0c91du);
        return body;
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
            StatusText: $"{(string.IsNullOrWhiteSpace(player.Name) ? "Player" : player.Name)} joined {game.MapName}",
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

    private static void WriteAscii(Span<byte> destination, int offset, string value)
    {
        Encoding.ASCII.GetBytes(value, destination[offset..]);
    }

    private static void WriteAsciiPadded(Span<byte> destination, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
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

    private static void FillHighEntropyDeterministic(Span<byte> destination, uint seed)
    {
        Span<byte> input = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(input, seed == 0 ? 0x6d2b79f5u : seed);
        var offset = 0;
        var counter = 0u;
        while (offset < destination.Length)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(input[4..], counter++);
            var block = System.Security.Cryptography.SHA256.HashData(input);
            var count = Math.Min(block.Length, destination.Length - offset);
            block.AsSpan(0, count).CopyTo(destination[offset..]);
            offset += count;
        }
    }

    private static byte[] PadNativeBitstream(
        byte[] bitstream,
        int length,
        byte seed,
        GameManagerSession game,
        PlayerSession player,
        bool highEntropyPadding = false)
    {
        if (bitstream.Length >= length)
        {
            return bitstream;
        }

        var body = new byte[length];
        bitstream.AsSpan(0, Math.Min(bitstream.Length, body.Length)).CopyTo(body);
        if (bitstream.Length < body.Length)
        {
            var fillSeed = (byte)(seed ^ (byte)game.GameId ^ (byte)player.PlayerId);
            if (highEntropyPadding)
            {
                FillHighEntropyDeterministic(
                    body.AsSpan(bitstream.Length),
                    ((uint)fillSeed << 24)
                    ^ ((uint)game.GameId << 8)
                    ^ (uint)player.PlayerId
                    ^ (uint)bitstream.Length);
            }
            else
            {
                FillDeterministic(body.AsSpan(bitstream.Length), fillSeed, protectedPrefixBytes: 0);
            }
        }

        return body;
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
        HighEntropy,
        PlayerStateLink,
        MixedBinary
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
