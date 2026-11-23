using OpenCiv2.Contracts;

namespace OpenCiv2.Core.State;

/// <summary>U06 local casualty history. Classic name bytes/padding belong to the format boundary.</summary>
public record struct CasualtyRecord
{
    public CasualtyRecord() { }
    public short Type, Year, OpponentColor;
    public MapPoint Position;
    public string OpponentName = string.Empty;
}
public sealed class CasualtyLog
{
    public const int Capacity = 300;
    private readonly CasualtyRecord[] records = Enumerable.Range(0, Capacity).Select(_ => new CasualtyRecord()).ToArray();
    public int First, Next, Count, Reported;
    public ref CasualtyRecord At(int index) => ref records[index];
}
/// <summary>World-owned, fresh-game isolated history. Ordinary ring operation retains 299 counted entries.</summary>
public sealed class CasualtyHistory
{
    private readonly CasualtyLog[] logs = Enumerable.Range(0, EntityLimits.Civilizations).Select(_ => new CasualtyLog()).ToArray();
    public CasualtyLog this[int civilization] => logs[civilization];
}
