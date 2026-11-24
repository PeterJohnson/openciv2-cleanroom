using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CityCaptureFixture : ICityCaptureEffects, ICivilWarEffects, ICaptureProductionEffects, ICapturePartisansEffects
    {
        public TheftFixture Theft { get; }
        public RevelationFixture Data => Theft.Data;
        public GameState World => Data.World;
        public ref OpenCiv2.Core.State.City City => ref Theft.City;
        public EliminationFixture Life => Theft.Damage.Lifecycle;
        public CityCapture Capture { get; }
        public List<CaptureNotice> Notices { get; } = [];
        public List<string> Order { get; } = [];
        public List<MapPoint> Draws { get; } = [];
        public List<CapturePartisansNotice> Partisans { get; } = [];
        public List<SpaceshipLossNotice> Ships { get; } = [];
        public List<LeonardoUpgradeNotice> Upgrades { get; } = [];
        public Action<CitySlot, int, int>? OnTaken;
        public Action<CaptureNotice>? OnNotice;
        public CityCaptureFixture(List<Choice>? choices = null, RevelationFixture? data = null, bool retainGrants = false,
            ScenarioTriggers? events = null, UnitCreation? partisanCreation = null)
        {
            Theft = new(choices, data, retainGrants); Data.AddCity(1, 2, new(26, 16), 4);
            World.Civilizations[2].CityCount = 2; World.Civilizations[2].TotalCitySize = 9;
            City.Size = 5; City.Founder = new(2); World.Civilizations[2].Gold = 100; World.Civilizations[1].Gold = 10;
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2); World.Globals.Difficulty = Difficulty.Prince;
            var rules = Data.Rules;
            rules.Cosmic = rules.Cosmic with { ShieldBoxRows = 10 };
            World.Calculation.ShieldRows = 10; World.Calculation.Food = 12; World.Calculation.Shields = 8;
            rules.Terrain[(int)Terrain.Grassland] = new() { DefenseFactor = 1 };
            var policy = new CityProductionChoice(World, Data.Process.Rules, new(), new(World, Data.Process.Rules, Data.Stacks), Data.Stacks,
                new(World, rules, ClassicShip, ClassicCalendar), new(World, rules, Data.Stacks, Life.Deletion, Life.Random, _ => { }));
            var orders = new ProductionOrders(World, rules, slot => policy.Choose(slot, "capture-choice"), _ => { });
            var removal = new CityRemoval(World, rules, Data.Stacks, Life.Loss, _ => Order.Add("removed"));
            var incidents = Theft.Damage.Incidents.Incidents;
            var war = new CivilWar(World, Data.Stacks, owner => Creation(Data, owner, Life.Random),
                _ => throw new InvalidOperationException("Unexpected NoSchism action"), this);
            var services = new CityCaptureServices(FoundationSuite.War(Data, new(), Life.Random), war,
                new(World, Life.Random, removal, Life.Elimination, incidents),
                new(World, rules, Data.Stacks, removal, Life.Elimination, incidents, orders, this),
                new(World, rules, partisanCreation ?? new(World, Data.Process, Data.Stacks, Data), Life.Random, this),
                Theft.Captured, Life.Elimination, incidents, orders, new(World, rules, Upgrades.Add, Redraw),
                new(World, rules, new MovementGoalQueries(World, rules)));
            Capture = events is null
                ? new(World, rules, Data.Stacks, Life.Random, services, (city, n, o) =>
                { Order.Add("event"); (OnTaken ?? throw new InvalidOperationException("Required CityTaken dispatcher unavailable"))(city, n, o); }, this)
                : new(World, rules, Data.Stacks, Life.Random, services, events, this);
        }
        public CityCapture.Operation Begin(int mode = 1, int owner = 1, bool incited = false, CitySlot? city = null) =>
            incited ? Capture.BeginIncited(city ?? new(0), owner, mode, 77, "capture") : Capture.Begin(city ?? new(0), owner, mode, 77, "capture");
        public void Complete(CityCapture.Operation op)
        {
            int limit = 50;
            while (!op.Completed && --limit > 0)
            {
                var pending = op.Resume();
                if (pending is CityCaptureDecision.Window) { op.Resume(new(0)); }
                else if (pending is not null) { throw new InvalidOperationException("Unexpected capture decision " + pending); }
            }
            Equal(true, op.Completed); Life.Random.RequireChoicesConsumed();
        }
        public void Notice(CaptureNotice notice) { Notices.Add(notice); Order.Add(notice.Kind.ToString()); OnNotice?.Invoke(notice); }
        public void SpaceshipLost(SpaceshipLossNotice notice) { Ships.Add(notice); Order.Add("ship"); }
        public void EnsureVisible(MapPoint point, int owner) => Order.Add("visible:" + point.X + ":" + owner);
        public void Redraw(MapPoint point) { Draws.Add(point); Order.Add("draw"); }
        public void RedrawStatus() => Order.Add("status");
        public void BarracksSound() => Order.Add("barracks");
        public void Schism(CivilWarNotice notice) => Order.Add("schism");
        public void RedrawMaps() => Order.Add("maps");
        public void RedrawNeighbors(MapPoint point) => Order.Add("neighbors:" + point.X);
        public void InfantrySound() => Order.Add("infantry");
        public void Notice(CapturePartisansNotice notice) { Partisans.Add(notice); Order.Add("partisans"); }
    }
    private static Choice CaptureParity(int value) => Choice.Create(new("C09.capture.building-parity", "capture"), new SamplingLaw.PrimaryModulo(2), ChoiceCode.Primary(value));

    private static void CityCaptureConquestOrdering()
    {
        var f = new CityCaptureFixture([CaptureParity(0)]); var w = f.World; var center = f.City.Position;
        f.City.ShieldBox = 50; f.City.FoodBox = 31; f.City.Flags = new()
        {
            Disorder = true,
            Celebrating = true,
            TechnologyStolen = true,
            Autobuild = true,
            DefenderLost = true,
            ImprovementSold = true,
            Investigated = true
        };
        f.Theft.Damage.Install(2, 3, 4, 7, 11, 15); w.Civilizations[2].UnitsInProduction[4] = 0;
        w.Civilizations[1].Government = Government.Democracy; w.Globals.PowerRank[1] = 7;
        w.Civilizations[1].Treaties[2].Contact = w.Civilizations[2].Treaties[1].Contact = true;
        w.Civilizations[1].Treaties[2].Aggressor = w.Civilizations[2].Treaties[1].Aggressor = true;
        w.Civilizations[1].StandingGoals[0] = new(new(13, 7), (sbyte)RuleRole.Settle, 8);
        w.Civilizations[1].StandingGoals[1] = new(new(13, 7), (sbyte)RuleRole.Attack, 9);
        w.Events.Loaded = true;
        f.OnTaken = (slot, n, o) =>
        {
            Equal(new CivilizationSlot(1), f.City.Owner); Equal((byte?)2, w.Map.Owner(center));
            Equal((byte)4, f.City.RememberedSize[2]); Equal((byte)1, w.Map.TileAt(new(13, 7)).Claimant);
            Equal((sbyte)-1, w.Civilizations[1].StandingGoals[0].Role); Equal((short)31, f.City.FoodBox);
            f.City.Name = "Renamed"; f.City.Position = new(20, 10); w.Globals.Turn = 300;
        };
        using var op = f.Begin(0); var window = (CityCaptureDecision.Window)op.Resume()!;
        Equal("Renamed", window.Request.Name); Equal("Birch", f.Notices.Single(n => n.Kind == CaptureNoticeKind.Capture).CityName);
        Equal(50, f.Notices.Single(n => n.Kind == CaptureNoticeKind.Capture).NumericArgument);
        Equal(50, w.Civilizations[2].Gold); Equal(60, w.Civilizations[1].Gold); Equal((short)0, f.City.ShieldBox);
        Equal((byte)255, w.Civilizations[2].UnitsInProduction[4]); Equal((sbyte)4, f.City.Size);
        Equal(true, f.City.Flags.ImprovementSold && f.City.Flags.Investigated && f.City.Flags.Disorder);
        Equal(false, f.City.Flags.Autobuild || f.City.Flags.Celebrating || f.City.Flags.TechnologyStolen || f.City.Flags.DefenderLost);
        Equal(true, f.Theft.Damage.Has(3) && f.Theft.Damage.Has(15)); Equal(false, f.Theft.Damage.Has(2) || f.Theft.Damage.Has(4) || f.Theft.Damage.Has(7) || f.Theft.Damage.Has(11));
        Equal((byte?)1, w.Map.Owner(center)); Equal(true, w.Civilizations[2].Treaties[1].CityCaptured);
        Equal(true, w.Civilizations[1].Treaties[2].War && w.Civilizations[2].Treaties[1].Incident);
        Equal(false, w.Civilizations[1].Treaties[2].Aggressor || w.Civilizations[2].Treaties[1].Aggressor);
        Equal(true, f.Order.IndexOf("event") < f.Order.IndexOf("barracks") && f.Order.IndexOf("barracks") < f.Order.IndexOf("Capture"));
        Equal(true, f.Order.Contains("neighbors:20")); Equal((short)2, w.Civilizations[2].CityCount); Equal((short)9, w.Civilizations[2].TotalCitySize);
        Equal(false, op.Completed); op.Resume(new(0)); Equal(true, op.Completed); Equal((byte)44, f.City.TurnsCaptured);
        Equal(new CivilizationSlot(2), f.City.Founder); f.Life.Random.RequireChoicesConsumed();
    }

    private static void CityCaptureModesAndRestoration()
    {
        foreach (var (mode, restored, size) in new[] { (0, true, 1), (1, true, 3), (1, false, 1), (2, false, 3), (0, false, 3) })
        {
            var f = new CityCaptureFixture(mode == 0 && !restored ? [CaptureParity(1)] : []);
            f.City.Founder = new((byte)(restored ? 1 : 2)); f.City.Size = (sbyte)size;
            f.Theft.Damage.Install(2, 3, 4, 7, 11); f.World.Civilizations[1].Treaties[2].Contact = true;
            using var op = f.Begin(mode); f.Complete(op);
            Equal((sbyte)(size > 1 ? size - 1 : 1), f.City.Size); Equal(restored, f.Theft.Damage.Has(4));
            Equal(mode != 0 || restored, f.Theft.Damage.Has(3)); Equal(true, f.Theft.Damage.Has(2));
            Equal(mode < 2, f.World.Civilizations[1].Treaties[2].War); Equal(new CivilizationSlot(2), f.City.Founder);
            Equal(restored, f.Notices.Single(n => n.Kind == CaptureNoticeKind.Capture).Restored);
        }
        var rank = new CityCaptureFixture(); rank.World.Globals.PowerRank[1] = 4; rank.World.Globals.PowerRank[2] = 2;
        rank.Data.AddCity(2, 1, new(4, 4)); rank.Data.AddCity(3, 1, new(6, 6));
        rank.World.Entities.Cities.Storage[2].Founder = rank.World.Entities.Cities.Storage[3].Founder = new(2);
        using var taken = rank.Begin(); rank.Complete(taken); Equal(true, rank.World.Civilizations[2].Treaties[1].CityCaptured);
        var ship = new CityCaptureFixture(); ship.World.Civilizations[2].SpaceshipFlags.Launched = true;
        using var spared = ship.Begin(); ship.Complete(spared); Equal(false, ship.World.Civilizations[1].Treaties[2].Incident);
    }

    private static void CityCapturePalaceAndSchism()
    {
        foreach (int response in new[] { 0, 1, 2 })
        {
            var f = new CityCaptureFixture(); var w = f.World; w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); w.Multiplayer.LocalHuman = 2;
            f.City.Size = 10; f.Theft.Damage.Install(1); w.Entities.Cities.Storage[1].Size = 9; w.Entities.Cities.Storage[1].Name = "Refuge";
            w.Civilizations[2].Gold = 1500; w.Civilizations[2].TotalCitySize = 19; w.Civilizations[2].SpaceshipFlags.Exists = true;
            using var op = f.Begin(); var choice = (CityCaptureDecision.Palace)op.Resume()!;
            Equal(new CitySlot(1), choice.Choice.Destination); Equal(1500, w.Civilizations[2].Gold); Equal(choice, op.Resume());
            var window = op.Resume(new(response)); Equal(true, window is CityCaptureDecision.Window);
            Equal(response == 1 ? 510 : 760, w.Civilizations[1].Gold); Equal(response == 1 ? 0 : 750, w.Civilizations[2].Gold);
            Equal(response == 1, BuildingQueries.HasBuilding(w, new(1), 1)); Equal(response == 1, w.Civilizations[2].SpaceshipFlags.Exists);
            Equal(response == 1 ? 0 : 1, f.Ships.Count); Equal(false, f.Theft.Damage.Has(1)); f.Complete(op);
        }
        // A real V01 zero result selects a replacement, bypasses payment and writes Palace production directly.
        var replacement = new CityCaptureFixture(); var rw = replacement.World; replacement.Theft.Damage.Install(1);
        for (int c = 1; c < 8; ++c) rw.Civilizations[c].CityCount = 1;
        rw.Civilizations[2].CityCount = 5; rw.Globals.PowerRank[2] = 4; rw.Civilizations[2].Gold = 3000;
        rw.Entities.Cities.Storage[1].Size = 9; rw.Entities.Cities.Storage[1].ShieldBox = 17;
        using var relocate = replacement.Begin(); replacement.Complete(relocate);
        Equal((sbyte)-1, rw.Entities.Cities.Storage[1].Production); Equal((short)17, rw.Entities.Cities.Storage[1].ShieldBox);
        Equal(0, replacement.Notices.Count(n => n.Kind == CaptureNoticeKind.PalaceEscape));
        // Slot zero wins the score but is deliberately excluded from payment; no second-best fallback.
        var zero = new CityCaptureFixture(); zero.City.Size = 20; zero.World.Entities.Cities.Storage[1].Size = 10;
        BuildingQueries.SetBuilding(zero.World, new(1), 1, true); zero.World.Civilizations[2].Gold = 3000;
        zero.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); zero.World.Multiplayer.LocalHuman = 2;
        using var noOffer = zero.Begin(city: new(1)); Equal(true, noOffer.Resume() is CityCaptureDecision.Window); zero.Complete(noOffer);
        Equal(0, zero.Notices.Count(n => n.Kind == CaptureNoticeKind.PalaceEscape));
    }

    private static void CityCaptureWonderAndObserverNotices()
    {
        var f = new CityCaptureFixture(); f.World.Globals.WonderCity[WonderIds.EiffelTower] = 0; f.World.Civilizations[1].Reputation = 9;
        f.City.Production = -39; f.World.Entities.Cities.Storage[1].Production = -39;
        f.OnNotice = n =>
        {
            if (n.Kind == CaptureNoticeKind.CapturedWonder) { Equal(new CivilizationSlot(2), f.City.Owner); Equal((byte)9, f.World.Civilizations[1].Reputation); }
        };
        using var op = f.Begin(); f.Complete(op); Equal((byte)4, f.World.Civilizations[1].Reputation);
        Equal(CaptureNoticeKind.StillOneWonder, f.Notices[0].Kind); Equal(1, f.Notices[0].NumericArgument);
        Equal("Birchfolk", f.Notices[0].LoserName); Equal(true, f.Notices[1].ScopedWonder);
        foreach (int kind in new[] { 0, 1, 2, 3 })
        {
            var ai = new CityCaptureFixture(); var w = ai.World; w.Globals.HumanPlayers = CivilizationSet.Only(3); w.Globals.CivsAlive = w.Globals.CivsAlive.With(3);
            w.Multiplayer.LocalHuman = 3; w.Globals.WonderCity[WonderIds.EiffelTower] = 0; w.Civilizations[1].Reputation = 9;
            ai.City.Production = -39; w.Entities.Cities.Storage[1].Production = -39;
            if (kind == 0) w.Civilizations[3].Treaties[1].Alliance = true;
            if (kind == 1) w.Civilizations[3].Treaties[2].Alliance = true;
            if (kind >= 2) w.Civilizations[3].Treaties[1].Embassy = true;
            if (kind == 3) w.Multiplayer.Saved.AutoplayActive = true;
            using var captured = ai.Begin(); ai.Complete(captured); Equal((byte)9, w.Civilizations[1].Reputation);
            if (kind == 3) Equal(0, ai.Notices.Count);
            else
            {
                var notice = ai.Notices.Last(); Equal(kind == 0 ? CaptureNoticeKind.WinnerAlly : kind == 1 ? CaptureNoticeKind.LoserAlly : CaptureNoticeKind.General, notice.Kind);
                Equal(1, notice.NumericArgument); Equal(kind == 1 ? "Birchfolk" : "Birch", notice.LoserName);
            }
        }
    }

    private static void CityCaptureDeletionTechnologyAndPartisans()
    {
        var f = new CityCaptureFixture([CaptureParity(0)]); f.City.Size = 1; f.Theft.Known(90);
        f.World.Scenario.Flags.NoConquestTechnology = true; // Ignored without scenario status.
        using var removed = f.Begin(0); var technology = (CityCaptureDecision.Technology)removed.Resume()!;
        Equal(null, removed.RemainingCity); Equal(false, f.City.Token.Occupied);
        Equal(null, f.Notices.Single(n => n.Kind == CaptureNoticeKind.Capture).City); Equal((byte?)1, f.World.Map.Owner(new(12, 8)));
        Equal(90, technology.Choice.Recommendation); removed.Resume(new(90)); Equal(true, removed.Completed);
        Equal(true, f.World.Civilizations[1].Advances.Contains(90)); Equal(false, f.Order.Any(s => s.StartsWith("neighbors:")));
        f.Life.Random.RequireChoicesConsumed();

        var partisan = new CityCaptureFixture([CaptureParity(0)]); var w = partisan.World; partisan.City.Size = 12;
        w.Civilizations[2].Government = Government.Communism;
        w.Civilizations[2].Advances = default(AdvanceSet).With(AdvanceIds.Conscription).With(AdvanceIds.GuerrillaWarfare);
        w.Globals.Status.Scenario = true; w.Scenario.Flags.NoConquestTechnology = true;
        using var resistance = partisan.Begin(0); partisan.Complete(resistance);
        Equal(6, w.UnitTotals[2]); Equal(1, partisan.Partisans.Count); Equal("Birch", partisan.Partisans.Single().CityName);
        Equal(true, partisan.Order.IndexOf("infantry") > partisan.Order.IndexOf("neighbors:12"));
        Equal(new CivilizationSlot(2), partisan.City.Founder);

        var upgrade = new CityCaptureFixture(); upgrade.World.Globals.WonderCity[WonderIds.Leonardo] = 0;
        upgrade.World.Civilizations[1].Advances = default(AdvanceSet).With(5);
        upgrade.Data.Rules.Units[47] = upgrade.Data.Rules.Units[47] with { ObsoleteBy = (AdvanceReference)5 };
        upgrade.Data.Rules.Units[48] = upgrade.Data.Rules.Units[47] with { ObsoleteBy = AdvanceReference.Never, Prerequisite = (AdvanceReference)5 };
        upgrade.Data.Unit(0).Flags.Veteran = true;
        using var leonardo = upgrade.Begin(); upgrade.Complete(leonardo);
        Equal(new UnitType(48), upgrade.Data.Unit(0).Type); Equal(false, upgrade.Data.Unit(0).Flags.Veteran); Equal(1, upgrade.Upgrades.Count);
    }

    private static void CityCaptureIncitedTransfersAndFailures()
    {
        var f = new CityCaptureFixture(); var w = f.World;
        f.Data.AddUnit(1, 2, 4, new(12, 8)); f.Data.AddUnit(2, 2, 4, new(13, 7)); f.Data.AddUnit(3, 2, 4, new(11, 7));
        f.Data.AddCity(2, 3, new(11, 7)); f.Data.AddUnit(4, 2, 4, new(16, 8));
        f.Data.Unit(1).Orders = UnitOrder.Fortified; f.Data.Unit(2).Orders = UnitOrder.Sleeping;
        f.Data.Unit(1).Home = f.Data.Unit(2).Home = new(0); f.Data.Unit(1).MovesUsed = 7;
        w.Civilizations[2].UnitsActive[4] = 1; w.Civilizations[1].UnitsActive[4] = 255;
        using var op = f.Begin(2, incited: true); f.Complete(op);
        Equal(new CivilizationSlot(1), f.Data.Unit(1).Owner); Equal(new CivilizationSlot(1), f.Data.Unit(2).Owner);
        Equal(new CivilizationSlot(2), f.Data.Unit(3).Owner); Equal(new CivilizationSlot(2), f.Data.Unit(4).Owner);
        Equal((byte)0, f.Data.Unit(1).MovesUsed); Equal(UnitOrder.Fortified, f.Data.Unit(1).Orders); Equal(UnitOrder.None, f.Data.Unit(2).Orders);
        Equal((byte)255, w.Civilizations[2].UnitsActive[4]); Equal((byte)1, w.Civilizations[1].UnitsActive[4]);
        Equal(true, w.Map.TileAt(new(14, 6)).RevealedTo.Contains(1)); Equal(CaptureNoticeKind.Incited, f.Notices[0].Kind);
        var negative = new CityCaptureFixture(); using var ignored = negative.Begin(owner: -1, incited: true); negative.Complete(ignored);
        Equal(new CivilizationSlot(2), negative.City.Owner); Equal(0, negative.Order.Count);
        var failed = new CityCaptureFixture(); failed.World.Events.Loaded = true;
        using var broken = failed.Begin(); Throws<InvalidOperationException>(() => broken.Resume()); Throws<InvalidOperationException>(() => broken.Resume());
        Equal(new CivilizationSlot(1), failed.City.Owner); Equal((byte?)2, failed.World.Map.Owner(new(12, 8))); Equal(60, failed.World.Civilizations[1].Gold);
        var remote = new CityCaptureFixture(); remote.World.Globals.Multiplayer = GameMode.Tcp;
        using var denied = remote.Begin(); Throws<InvalidOperationException>(() => denied.Resume()); Equal(100, remote.World.Civilizations[2].Gold);
        var held = new CityCaptureFixture(); var stopped = held.Begin(); Equal(true, stopped.Resume() is CityCaptureDecision.Window);
        stopped.Dispose(); Throws<ObjectDisposedException>(() => stopped.Resume(new(0))); Equal(true, held.World.Calculation.Processing);
        Equal(new CivilizationSlot(1), held.City.Owner); Equal((byte)0, held.City.TurnsCaptured);
    }

    private static void CityCapturePalaceScoringAndPayment()
    {
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            var f = new CityCaptureFixture(); var w = f.World; f.City.Size = 16; f.Theft.Damage.Install(1);
            w.Entities.Cities.Storage[1].Position = new(16, 8); w.Entities.Cities.Storage[1].Size = 8;
            f.Data.AddCity(2, 2, new(8, 8), variant == 3 ? 30 : 8);
            w.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); w.Multiplayer.LocalHuman = 2; w.Civilizations[2].Gold = 3000;
            if (variant > 0) f.Data.AddUnit(1, 3, 4, new(8, 8)); // Whole-stack count, even a foreign attacker.
            if (variant > 1) BuildingQueries.SetBuilding(w, new(1), BuildingIds.SamBattery, true);
            if (variant == 3)
            {
                BuildingQueries.SetBuilding(w, new(1), BuildingIds.CityWalls, true);
                BuildingQueries.SetBuilding(w, new(1), BuildingIds.SdiDefense, true);
                w.Map.TileAt(new(16, 8)).Region = 2;
            }
            using var op = f.Begin(); var pending = (CityCaptureDecision.Palace)op.Resume()!;
            Equal(new CitySlot((ushort)(variant == 1 ? 2 : 1)), pending.Choice.Destination);
            // No affordability recheck after the actual question. Debit fresh gold, then clamp retained plunder.
            w.Civilizations[2].Gold = 900; op.Resume(new(1)); f.Complete(op);
            Equal(0, w.Civilizations[2].Gold); Equal(-90, w.Civilizations[1].Gold);
        }
        foreach (bool autoplay in new[] { false, true })
        {
            var f = new CityCaptureFixture(); var w = f.World; f.Theft.Damage.Install(1);
            w.Entities.Cities.Storage[1].Size = 9; w.Civilizations[2].Gold = 3000; w.Globals.Difficulty = Difficulty.King;
            w.Multiplayer.Saved.AutoplayActive = autoplay;
            using var op = f.Begin(); f.Complete(op);
            Equal(true, BuildingQueries.HasBuilding(w, new(1), 1));
            Equal(1, f.Notices.Count(n => n.Kind == CaptureNoticeKind.PalaceEscape)); // Escape notice ignores autoplay.
        }
    }

    private static void CityCaptureNestedForeignLoss()
    {
        var f = new CityCaptureFixture([CaptureParity(0)]); var w = f.World; f.City.Size = 1;
        w.Globals.HumanPlayers = CivilizationSet.Only(1).With(3);
        f.Data.Rules.Units[46] = f.Data.Rules.Units[4] with { Role = RuleRole.Settle };
        f.Data.AddUnit(1, 3, 46, new(14, 10)); f.Data.Unit(1).Home = new(0); w.Civilizations[3].UnitsActive[46] = 1;
        f.Data.AddUnit(2, 4, 4, new(24, 10)); f.Data.Unit(2).Flags.SpyMission = true; f.Data.Unit(2).Counter = 3;
        w.Civilizations[3].Treaties[4].Peace = true; f.Life.Recreate = c => Creation(f.Data, c, f.Life.Random);
        using var op = f.Begin(0); var pending = (CityCaptureDecision.Lifecycle)op.Resume()!;
        Equal(3, ((EspionagePretext)pending.Decision).Victim.Civilization); Equal(true, f.City.Token.Occupied); Equal((sbyte)0, f.City.Size);
        Equal((byte?)2, w.Map.Owner(new(12, 8))); Equal(false, f.Order.Contains("Capture"));
        Equal(pending, op.Resume()); op.Resume(new(0)); f.Complete(op);
        Equal(null, op.RemainingCity); Equal(false, f.City.Token.Occupied); Equal((byte?)1, w.Map.Owner(new(12, 8)));
        Equal(true, f.Order.IndexOf("removed") < f.Order.IndexOf("Capture"));
    }

    private static void CityCaptureSessionDecisions()
    {
        using var runtime = new SessionRuntime(new(), new(200));
        var f = new CityCaptureFixture(data: CreationFixture(new(runtime.Process, runtime.World))); f.Theft.Damage.Install(1);
        f.World.Entities.Cities.Storage[1].Size = 9; f.World.Civilizations[2].Gold = 1500;
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2); f.World.Multiplayer.LocalHuman = 2; f.Theft.Known(90);
        ParticipantId actorSeat = default, victimSeat = default; int windows = 0;
        runtime.RegisterCommand(new(new(1, "test.city-capture", "Authored capture continuation", AccessClass.Player, new([]), new([new("city")])),
            (_, _, _) => new(), _ => new CityCaptureOperation(f.Begin(), c => c == 1 ? actorSeat : victimSeat, request =>
            { ++windows; return new("Authored P06 boundary", victimSeat, new CommandValue.City(request.City), new([0])); })));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime); var victim = SessionClient(runtime, 2);
        actorSeat = actor.Identity.Participant; victimSeat = victim.Identity.Participant;
        var command = actor.Submit(new("test.city-capture", new([]))).Correlation.Command; runtime.AdvanceOne();
        var palace = RequiredInteraction(victim, command); Equal(null, RequiredResult(actor, command).Interaction); Equal(false, actor.Reply(palace, 1).Accepted);
        Equal(true, victim.Reply(palace, 1).Accepted); runtime.AdvanceOne();
        var technology = RequiredInteraction(actor, command); Equal(false, victim.Reply(technology, 90).Accepted);
        Equal(true, actor.Reply(technology, 90).Accepted); runtime.AdvanceOne();
        var window = RequiredInteraction(actor, command); Equal(true, f.World.Calculation.Processing); Equal(1, windows);
        Equal(true, actor.Reply(window, 0).Accepted); runtime.AdvanceOne(); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status);
        Equal(false, f.World.Calculation.Processing); Equal(true, f.World.Civilizations[1].Advances.Contains(90)); f.Life.Random.RequireChoicesConsumed();
    }
}
