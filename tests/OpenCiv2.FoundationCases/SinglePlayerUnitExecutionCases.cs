using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Formats.Content;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class UnitExecutionFixture : ITerrainWorkEffects, ICityFoundingEffects,
        IBarbarianOrderEffects, IBarbarianRansomEffects, ISettlerPopulationEffects, IHumanMovementPhaseEffects
    {
        public GameplayFixture F { get; } = new();
        public RevelationFixture Data => F.Data;
        public GameState World => F.World;
        public SinglePlayerUnitExecution Services { get; }
        public ClassicTextInput Text { get; } = new();
        public List<string> Calls { get; } = [];
        public List<MapPoint> WorkRedraws { get; } = [];
        public bool FailCursor, FailFocus;
        public int CursorRedraws;
        public CitySlot? CityToRefresh => null;
        public UnitExecutionFixture()
        {
            Text.Files["scenario/CITY.TXT"] = "@BARBARIANS\n1\nSelected ransom message\n";
            Services = new(World, Data.Process, F.F.Life.Random, F.Services, GameplayContent.Builtin.Values.Calendar,
                new ClassicRansomText(Text.Reader()), new(this, this, this, this, this, this,
                    () => { ++CursorRedraws; if (FailCursor) throw new IOException("Cursor unavailable"); }));
        }
        public void Notice(UnitSlot unit, UnitOrder job, TerrainWorkProblem problem) => Calls.Add("work-notice");
        public void Redraw(MapPoint point, int radius) => WorkRedraws.Add(point);
        public void RedrawStatus() => Calls.Add("status");
        public void UnitChanged(UnitSlot unit) => Calls.Add("unit");
        public void CityChanged(CitySlot city) => Calls.Add("city");
        public void StopBlinking() => Calls.Add("blink");
        public void EnsureVisible(MapPoint point, int owner) => Calls.Add("visible");
        public void RefreshCity(CitySlot city) => Calls.Add("city");
        public void Rejected(FoundingProblem problem) => Calls.Add("founding-rejected");
        public void BuildSound() => Calls.Add("build-sound");
        public void Founded(FoundedCity city) => Calls.Add("founded");
        public void FirstProduction(int owner, CitySlot city) => Calls.Add("first-production");
        public void Redraw(MapPoint point) => Calls.Add("draw");
        public void Landing(BarbarianLandingNotice notice) => Calls.Add("landing");
        public void RefreshStatus() => Calls.Add("status");
        public void FocusInput() { Calls.Add("focus"); if (FailFocus) throw new IOException("Focus unavailable"); }
        public void EndOfTurn(bool moveUnits) => Calls.Add(moveUnits ? "end-move" : "end-view");
        public void CloseCity(CitySlot city) => Calls.Add("close");
        public void FlushInput() => Calls.Add("flush");
        public void Consumed() => F.F.Consumed();
    }

    // U04/U03/U02/W03/V04/S02: queued Goto reaches actual retained contact through the shared graph.
    private static void SinglePlayerExecutionGotoContact()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new UnitExecutionFixture(); var destination = new MapPoint(20, 12);
            f.Data.Unit(0).Orders = UnitOrder.GoTo; f.Data.Unit(0).Destination = destination;
            f.Data.AddUnit(2, 2, 4, new(21, 13)); f.World.Multiplayer.LocalHuman = 2; f.World.Events.Loaded = true;
            var entry = NegotiationEvent(f.World, 1, 2, deny: true);
            entry.Values.Actions = new() { Text = true, JustOnce = true }; entry.SetString(Event.TextBegin, new("Order contact"));
            using var setup = f.F.Begin();
            using var operation = f.Services.Orders.Begin(new(0), f.F.F.F.Inputs, "orders");
            var pending = operation.Resume();
            Equal(true, pending is MovementDecision.Finish { Decision: MovementCompletionDecision.Contact });
            Equal(destination, f.Data.Unit(0).Position); Equal(pending, operation.Resume());
            // W03 interrupts Goto before the contact decision; disposing cannot restore that prefix.
            Equal(false, entry.Values.Actions.Consumed); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
            if (dispose)
            { operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(new(0))); }
            else
            {
                operation.Resume(new(0));
                for (int i = 0; i < 100 && !operation.Completed; ++i) Equal(null, operation.Resume());
                Equal(true, operation.Completed); Equal(1, operation.Result); Equal(UnitOrder.None, f.Data.Unit(0).Orders);
                Equal(true, entry.Values.Actions.Consumed); Equal((byte)1, f.Data.Unit(0).LastDirection);
            }
            Equal(false, f.World.UnitMovementActive); f.Consumed();
        }
    }

    // A01/A04/U04/W04: reverse AI passes choose and execute work, preserving cursor-failure prefixes.
    private static void SinglePlayerExecutionAiWork()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new UnitExecutionFixture { FailCursor = fail };
            f.World.Globals.TurnCiv = 1; f.World.Globals.HumanPlayers = CivilizationSet.Only(2);
            f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle, Movement = 6 };
            f.Data.Rules.Terrain[(int)Terrain.Grassland] = f.Data.Rules.Terrain[(int)Terrain.Grassland] with
            { Work = new(new(TerrainResult.ImproveInPlace, 1, 4), new(TerrainResult.ImproveInPlace, 1, 4), (TerrainResult)Terrain.Plains) };
            f.Data.Unit(0).Type = new(0); f.Data.AddUnit(2, 1, 0, new(22, 12));
            foreach (int slot in new[] { 0, 2 })
            { f.Data.Unit(slot).Orders = UnitOrder.Road; f.Data.Unit(slot).Counter = 0; f.Data.Unit(slot).MovesUsed = 0; }
            using var operation = f.Services.Ai.Begin(new(), f.F.F.F.Inputs, false, "ai-work");
            for (int i = 0; i < 100 && !operation.Completed; ++i) Equal(null, operation.Resume());
            Equal(true, operation.Completed); Equal(4, f.CursorRedraws); Equal(fail ? 4 : 0, operation.PresentationFailures.Count);
            Sequence(new[] { new MapPoint(22, 12), new MapPoint(18, 12) }, f.WorkRedraws);
            foreach (int slot in new[] { 0, 2 })
            { Equal((byte)1, f.Data.Unit(slot).Counter); Equal((byte)6, f.Data.Unit(slot).MovesUsed); Equal(UnitOrder.Road, f.Data.Unit(slot).Orders); }
            f.Consumed();
        }
    }

    // F04/U04: actual selection, player-input release and fortification finish the human phase.
    private static void SinglePlayerExecutionHumanPhase()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new UnitExecutionFixture { FailFocus = fail };
            f.World.Globals.HumanPlayers = CivilizationSet.Only(1); f.World.Globals.TurnCiv = 1;
            f.World.TurnLoopActive = true; f.World.MovementMode = MovementMode.MoveUnits;
            f.Data.Unit(0).Orders = UnitOrder.None; f.Data.Unit(0).MovesUsed = 0;
            using var phase = f.Services.Human.Begin(new(), f.F.F.F.Inputs, false, "human");
            var view = new HumanMovementView(new(18, 12), null);
            HumanMovementDecision? Advance(HumanMovementReply? reply = null)
            {
                var result = phase.Resume(view, reply);
                for (int i = 0; i < 100 && !phase.Completed && result is null; ++i) result = phase.Resume(view);
                return result;
            }
            var pending = Advance(); Equal(true, pending is HumanMovementDecision.Active { Value: ActiveUnitDecision.PlayerInput });
            Equal(pending, Advance()); Equal(true, f.World.PlayerPhaseActive);
            f.Services.Fortification.Apply(new(0)); Equal(null, Advance(ActiveInputHandled()));
            Equal(true, phase.Completed); Equal(true, phase.WaitedForInput); Equal(true, phase.AutomaticallyEnded);
            Equal(false, f.World.PlayerPhaseActive); Equal(null, f.World.Globals.ActiveUnit);
            Equal(fail ? 1 : 0, phase.PresentationFailures.Count); Equal(UnitOrder.Fortified, f.Data.Unit(0).Orders); f.Consumed();
        }
    }

    // W06/A04/U01: barbarian tactics reach authored ransom and actual shared lifecycle deletion.
    private static void SinglePlayerExecutionRansom()
    {
        foreach (bool pay in new[] { false, true })
        {
            var f = new UnitExecutionFixture();
            f.World.Entities.Units.SetEnd(0); f.World.Entities.Cities.SetEnd(0);
            for (int i = 0; i < EntityLimits.Civilizations; ++i)
            { f.World.Civilizations[i] = default; SharedGoals.ResetStanding(ref f.World.Civilizations[i]); }
            foreach (ref var tile in f.World.Map.Tiles)
            { tile = default; tile.Terrain = Terrain.Grassland; tile.Region = 1; tile.Owner = 0; }
            f.World.Map.Regions[1] = new(480, 64, 0, default);
            f.World.Globals.HumanPlayers = CivilizationSet.Only(2); f.World.Multiplayer.LocalHuman = 2;
            f.World.Globals.Turn = 3; f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            f.Data.Rules.Units[4] = new() { Role = RuleRole.Attack, Domain = RuleDomain.Land, Attack = 1, Defense = 1, Movement = 6, HitPoints = 10, Firepower = 1 };
            f.Data.AddUnit(0, 0, 4, new(10, 10)); f.Data.AddCity(0, 2, new(11, 9), 4);
            f.World.Civilizations[2].CityCount = 1; f.World.Civilizations[2].Gold = 150;
            using var operation = f.Services.Tactics.Begin(new(0), new(), "ransom");
            var pending = operation.Resume();
            Equal(true, pending is UnitTacticsDecision.Barbarian { Decision: BarbarianOrderDecision.Ransom { Decision: BarbarianRansomDecision.Offer } });
            var offer = (BarbarianRansomDecision.Offer)((BarbarianOrderDecision.Ransom)((UnitTacticsDecision.Barbarian)pending!).Decision).Decision;
            Equal(2, offer.Victim); Equal(100, offer.Demand); Equal("Selected ransom message", offer.Message);
            Equal(pending, operation.Resume()); Equal(150, f.World.Civilizations[2].Gold); Equal(1, f.Text.Traces["scenario/CITY.TXT"].Closes);
            operation.Resume(new(Choice: pay ? 1 : 0));
            for (int i = 0; i < 100 && operation.Result is null; ++i) Equal(null, operation.Resume());
            Equal(pay ? 1 : 0, operation.Result); Equal(pay ? 50 : 150, f.World.Civilizations[2].Gold);
            Equal(!pay, f.Data.Unit(0).Token.Occupied); Equal(false, f.World.UnitDeletion.Active); f.Consumed();
        }
    }
}
