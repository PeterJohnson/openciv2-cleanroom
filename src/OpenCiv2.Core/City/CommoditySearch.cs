using OpenCiv2.Base;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public sealed record CommodityOption(int Commodity, string Name);
public abstract record CommoditySearchDecision;
public sealed record CommoditySearchMenu(OwnedList<CommodityOption> Options) : CommoditySearchDecision;
public sealed record CommoditySearchReport(int Commodity, string Name, OwnedList<CommodityCity> Cities) : CommoditySearchDecision;

/// <summary>C07 commodity search. Trade gates entry only; nonnegative report results return to
/// the live commodity list without selecting or zooming to a city. No economy refresh occurs.</summary>
public sealed class CommoditySearch(GameState world, RuleTables rules, int viewer) : IDisposable
{
    private bool started, faulted, disposed;
    public bool Completed { get; private set; }
    public CommoditySearchDecision? Pending { get; private set; }
    public CommoditySearchDecision? Resume(int? selected = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        {
            throw new InvalidOperationException("A failed commodity search cannot resume");
        }

        if (Completed)
        {
            return null;
        }

        try
        {
            MapKnowledge.RequireLocal(world);
            if (!started)
            {
                started = true;
                selected = null;
                if (!CivilizationKnowledge.KnowsAdvance(world.Civilizations, viewer, AdvanceIds.Trade))
                { Completed = true; return null; }
            }
            else if (Pending is not null && selected is null)
            {
                return Pending;
            }

            if (selected is < 0)
            { Pending = null; Completed = true; return null; }
            if (Pending is CommoditySearchMenu && selected is { } commodity)
            {
                if (commodity >= rules.Commodities.Length)
                { throw new InvalidOperationException("Commodity search requires explicit name backing: " + commodity); }
                return Pending = new CommoditySearchReport(commodity, rules.Commodities[commodity].Name,
                    CommodityReport.Cities(world, viewer, commodity));
            }
            return Pending = new CommoditySearchMenu(new(Enumerable.Range(0, rules.Commodities.Length)
                .Select(i => new CommodityOption(i, rules.Commodities[i].Name))));
        }
        catch { faulted = true; throw; }
    }
    public void Dispose() => disposed = true;
}
