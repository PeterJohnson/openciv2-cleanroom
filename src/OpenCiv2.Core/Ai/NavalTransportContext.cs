using OpenCiv2.Contracts;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

/// <summary>A05 state retained across port/landing candidates and transport retries.
/// Nullable values have no unconditional producer; consumers require them only when relevant.</summary>
public sealed class NavalTransportContext
{
    public UnitTacticsContext Tactics { get; }
    public UnitRoleSet Roles { get; set; }
    public int Released { get; private set; }
    public int ReservedDefenders { get; private set; }
    public bool Room { get; set; }
    public bool OnlySettlersWithoutGoto { get; set; }
    public bool ExpandNeighbors { get; set; }
    public bool? CargoNeeded { get; set; }
    public bool? GotoLandingRegion { get; set; }
    public CitySlot? SelectedPort { get; set; }
    public int? SelectedPortPolicy { get; set; }
    public CitySlot? WorkingCity { get; set; }
    public int? CivilizationCursor { get; set; }

    public NavalTransportContext(UnitTacticsContext tactics, NavalCargoResult cargo = default)
    { Tactics = tactics; ResetCargo(cargo); }

    /// <summary>A05/X01 production initializer for otherwise unwritten shared locals.
    /// A reached city guard or port/landing producer replaces these values. Retries do not reset them.</summary>
    public static NavalTransportContext ForChooser(UnitTacticsContext tactics)
    {
        tactics.DesiredDefenders ??= 0;
        return new(tactics) { CargoNeeded = false, GotoLandingRegion = false, SelectedPortPolicy = 0 };
    }

    public void ResetCargo(NavalCargoResult cargo)
    { Roles = cargo.Roles; Released = cargo.Released; ReservedDefenders = cargo.ReservedDefenders; }
}
