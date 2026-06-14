namespace PlasmaGameManager.Server;

public static class OfficialEatf2ServerDllContracts
{
    public const int MaxUserCmdsPerBatch = 28;
    public const int CUserCmdStrideBytes = 0x40;
    public const int UserCmdDecodeFunction = 0x1021c080;
    public const int PlayerProcessUsercmdsVtableSlot = 0x5b4;
    public const int PlayerRunCommandVtableSlot = 0x5b8;
    public const int PhysicsCommandHistoryStrideBytes = 0x790;
    public const int MaxAdditionalPhysicsSimulationCommands = 0x18;
    public const int PhysicsSimulateTickGateOffset = 0x18;
    public const int GameManagerPlayerAddedVtableSlot = 0x10;
    public const int GameManagerPlayerRemovedVtableSlot = 0x18;
}
