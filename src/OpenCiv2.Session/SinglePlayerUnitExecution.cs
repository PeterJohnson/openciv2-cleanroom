using OpenCiv2.Base.Randomness;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Session;

public sealed record SinglePlayerUnitExecutionEffects(ITerrainWorkEffects Work, ICityFoundingEffects Founding,
    IBarbarianOrderEffects Barbarians, IBarbarianRansomEffects Ransom, ISettlerPopulationEffects Population,
    IHumanMovementPhaseEffects Human, Action RedrawCursor);

/// <summary>U03/U04/W04/W06 and A03–A07 execution over the shared local gameplay graph.
/// All tactical and execution children are actual services. The host supplies authored ransom
/// text and presentation, then admits movement inputs and Hold at each owning phase.
/// Rebuild together with gameplay after world/rule replacement.</summary>
public sealed class SinglePlayerUnitExecution
{
    public SharedGoals Goals { get; }
    public CoarsePathfinding Paths { get; }
    public UnitGoto Goto { get; }
    public TerrainWorkOrders Work { get; }
    public UnitFortification Fortification { get; }
    public Pillage Pillage { get; }
    public CityFounding Founding { get; }
    public BarbarianRansom Ransom { get; }
    public BarbarianOrders Barbarians { get; }
    public UnitTactics Tactics { get; }
    public UnitOrderExecution Orders { get; }
    public AutomatedOrders Automated { get; }
    public ActiveUnitCycle Active { get; }
    public HumanMovementPhase Human { get; }
    public AiUnitPasses Ai { get; }

    public SinglePlayerUnitExecution(GameState world, ProcessContext process, GameplayRandom random,
        SinglePlayerGameplay gameplay, CalendarValues calendar, IBarbarianRansomText ransomText,
        SinglePlayerUnitExecutionEffects effects)
    {
        var rules = process.Rules.Definitions;
        var stacks = gameplay.Stacks;
        var life = gameplay.Lifecycle;
        var combat = gameplay.Combat;
        Goals = new(world, rules);
        Paths = new(world, rules, stacks);
        Goto = new(world, rules, stacks, Paths);
        Work = new(world, process.Rules, stacks, effects.Work);
        Fortification = new(world, effects.Work.CityChanged, point => effects.Work.Redraw(point, 0), effects.Work.UnitChanged);
        Pillage = new(world, rules, gameplay.Diplomacy.Attacks, Goals, effects.Work.Redraw);
        Founding = new(world, process, gameplay.Movement.Cities, life.Cities, life.Elimination, life.Incidents, calendar, effects.Founding);
        Ransom = new(world, stacks, life.Loss, life.Elimination, life.Incidents, ransomText, effects.Ransom);
        Barbarians = new(world, rules, random, stacks, Goto, Pillage, Ransom, life.Loss, life.Elimination, life.Incidents, effects.Barbarians);
        var orders = new GroundOrders(world, rules, stacks, Goals);
        var recovery = new UnitRecovery(world, rules);
        var targets = new NavalTargets(world);
        var naval = new NavalSeaTactics(world, new(world, rules, stacks),
            new(world, rules, stacks, new(world, rules, stacks, Goals, random), new(world, rules, stacks, Goals, targets)),
            new(world, rules, stacks, targets, random), Goals);
        Tactics = new(world, rules, new(Barbarians,
            new(world, rules, new(world, rules, stacks, recovery), orders, random), orders,
            new(world, rules, stacks, combat.Strength, Goals), new(world, rules, stacks, recovery),
            new(world, rules, stacks), new(world, rules, life.Loss, life.Elimination, life.Incidents),
            new(world, rules, Goals, Founding), new(world, rules, stacks, combat.Paradrop), new(world), naval,
            new(world, rules, Pillage), new(world, process.Rules, stacks, Goals),
            new(world, life.Loss, life.Elimination, life.Incidents, effects.Population),
            new(world, rules, stacks, combat.Strength, combat.Preparation, gameplay.Missions.Units, random)));
        Orders = new(world, rules, Fortification, Work, Goto, gameplay.Movement.Dispatcher);
        Automated = new(world, rules, Tactics, Orders);
        Active = new(world, rules, Orders, Automated);
        Human = new(world, rules, gameplay.Selection, gameplay.Focus, Active, effects.Human);
        Ai = new(world, rules, stacks, Automated, effects.RedrawCursor);
    }
}
