using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

/// <summary>W04/C01/W06/F04 informational and view effects for local unit execution.
/// The host owns input focus and the open city view. Required naming, ransom and city
/// visits remain retained decisions; input flushing never discards admitted commands.</summary>
public sealed class SinglePlayerUnitExecutionPresentation(Func<OperationContext> current, Func<int> viewer,
    Func<CitySlot?> selectedCity, Action focusInput, Action<CitySlot> closeCity) :
    ITerrainWorkEffects, ICityFoundingEffects, IBarbarianOrderEffects, IBarbarianRansomEffects,
    ISettlerPopulationEffects, IHumanMovementPhaseEffects
{
    private OperationContext Current => current();
    public SinglePlayerUnitExecutionEffects Effects => new(this, this, this, this, this, this, Publish);
    public CitySlot? CityToRefresh => selectedCity();
    public void Publish() => Current.Publish();
    public void Redraw(MapPoint point) => Publish();
    public void Redraw(MapPoint point, int radius) => Publish();
    void ICityFoundingEffects.Redraw(MapPoint point, int observer) => Publish();
    public void RedrawStatus() => Publish();
    public void RefreshStatus() => Publish();
    public void UnitChanged(UnitSlot unit) => Publish();
    public void CityChanged(CitySlot city) => Publish();
    public void RefreshCity(CitySlot city) => Publish();
    public void StopBlinking() => Publish();
    public void BuildSound() { }
    public void FocusInput() => focusInput();
    public void EndOfTurn(bool moveUnits) => Publish();
    public void CloseCity(CitySlot city) => closeCity(city);
    public void FlushInput() { }
    private void Focus(MapPoint point, int observer) => new SinglePlayerEmissaryPresentation(current).EnsureVisible(point, observer);
    // Founding and barbarian landing pass the unit owner, not the viewing player.
    void ICityFoundingEffects.EnsureVisible(MapPoint point, int owner) => Focus(point, viewer());
    void IBarbarianOrderEffects.EnsureVisible(MapPoint point, int owner) => Focus(point, viewer());
    // A ransom addresses its human victim even when another participant initiated the command.
    void IBarbarianRansomEffects.EnsureVisible(MapPoint point, int observer) => Focus(point, observer);
    private void Tell(int recipient, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recipient);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(recipient, EntityLimits.Civilizations);
        (Current.Notify ?? throw new InvalidOperationException("Unit execution requires a scoped notice publisher"))(
            new((byte)recipient), text);
    }
    private string Advance(int advance) => RuleText.Display(Current.Process.Rules.Definitions.Advances[advance].Identity.Name);
    private string UnitName(int type) => RuleText.Display(Current.Process.Rules.Definitions.Units[type].Identity.Name);
    public void Notice(UnitSlot unit, UnitOrder job, TerrainWorkProblem problem)
    {
        ref var actor = ref Current.World.Entities.Units.At(unit);
        string reason = problem switch
        {
            TerrainWorkProblem.OnlySettlers => "Only settler-role units can perform terrain work.",
            TerrainWorkProblem.RadioRequired => $"Requires {Advance(AdvanceIds.Radio)}.",
            TerrainWorkProblem.ConstructionRequired => $"Requires {Advance(AdvanceIds.Construction)}.",
            TerrainWorkProblem.CannotImprove => "This terrain cannot receive that improvement.",
            TerrainWorkProblem.AlreadyAirbase => "An airbase is already present.",
            TerrainWorkProblem.AlreadyFortress => "A fortress is already present.",
            TerrainWorkProblem.BridgeRequired => $"Crossing this river requires {Advance(AdvanceIds.BridgeBuilding)}.",
            TerrainWorkProblem.AlreadyRailroad => "A railroad is already present.",
            TerrainWorkProblem.RailroadRequired => $"Requires {Advance(AdvanceIds.Railroad)}.",
            TerrainWorkProblem.AlreadyMining => "A mine is already present.",
            TerrainWorkProblem.NoWater => "No irrigation water is available.",
            TerrainWorkProblem.AlreadyFarmland => "Farmland is already present.",
            TerrainWorkProblem.RefrigerationRequired => $"Requires {Advance(AdvanceIds.Refrigeration)}.",
            TerrainWorkProblem.EngineersRequired => $"Requires {UnitName(UnitTypeIds.Engineers)}.",
            TerrainWorkProblem.NoPollution => "There is no pollution to clean.",
            _ => throw new ArgumentOutOfRangeException(nameof(problem))
        };
        string action = job switch
        {
            UnitOrder.Fortress => "build a fortress",
            UnitOrder.Road => "build a road or railroad",
            UnitOrder.Irrigate => "irrigate",
            UnitOrder.Mine => "mine",
            UnitOrder.Transform => "transform terrain",
            UnitOrder.Clean => "clean pollution",
            UnitOrder.Airbase => "build an airbase",
            _ => "perform this terrain work"
        };
        Tell(actor.Owner.Value, $"{UnitName(actor.Type.Value)} cannot {action}. {reason}");
    }
    public void Rejected(FoundingProblem problem) => Tell(problem.Owner, problem.Reason switch
    {
        FoundingRejection.RequiresSettler => "Only settler-role units can found or join a city.",
        FoundingRejection.Ocean => "A city cannot be founded on the ocean.",
        FoundingRejection.AdjacentCity => "A city cannot be founded next to another city.",
        FoundingRejection.PopulationLimit => $"This city has reached the population limit of {problem.Limit} for joining settlers.",
        _ => throw new ArgumentOutOfRangeException(nameof(problem))
    });
    public void Founded(FoundedCity city) => Tell(city.Owner, $"Founded {city.Name} in year {city.Year}.");
    public void FirstProduction(int owner, CitySlot city) => Tell(owner,
        $"Choose what to produce in {Current.World.Entities.Cities.At(city).Name} using the city window.");
    public void Landing(BarbarianLandingNotice notice) => Tell(notice.Observer,
        $"Barbarians are landing near {notice.CityName} at ({notice.Position.X}, {notice.Position.Y}).");
}
