using OpenCiv2.Core.State;

namespace OpenCiv2.Formats;

public static partial class StateConversion
{
    /// <summary>F06 §2: only the eight assigned game flags enter live state.</summary>
    public static GameStatus DecodeGameStatus(ushort bits) => new()
    {
        AiCoordination = (bits & 0x01) != 0,
        Won = (bits & 0x02) != 0,
        LargeMap = (bits & 0x04) != 0,
        SmallMap = (bits & 0x08) != 0,
        Cheated = (bits & 0x10) != 0,
        Scored = (bits & 0x20) != 0,
        ScenarioExport = (bits & 0x40) != 0,
        Scenario = (bits & 0x80) != 0
    };

    public static ushort EncodeGameStatus(GameStatus value) => (ushort)(
        (value.AiCoordination ? 0x01 : 0) | (value.Won ? 0x02 : 0) |
        (value.LargeMap ? 0x04 : 0) | (value.SmallMap ? 0x08 : 0) |
        (value.Cheated ? 0x10 : 0) | (value.Scored ? 0x20 : 0) |
        (value.ScenarioExport ? 0x40 : 0) | (value.Scenario ? 0x80 : 0));
}
