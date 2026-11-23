using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // Specification-derived U02 publication/landing/relocation expectations, using actual U01 grouping.
    private sealed class RelocationFixture : IMovementRelocationEffects
    {
        public RevelationFixture Data { get; }
        public GameState World => Data.World;
        public MovementStep Step = new(1, RuleDomain.Land, new(10, 8), new(12, 8), Terrain.Grassland, 1);
        public MovementViewerInputs Inputs = new(false);
        public List<string> Effects = [];
        public List<MovementLandingNotice> Notices = [];
        public List<CivilizationSet[]> Publications = [];
        public bool FailCosmetic, FailNotice;
        public int FailPublish;
        public RelocationFixture(RevelationFixture? data = null)
        {
            Data = data ?? new();
            if (data is null) Data.AddUnit(0, 1, 0, Step.From);
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
        }
        public MovementRelocationResult Move(bool ocean = false, int cost = 1) =>
            new MovementRelocation(World, Data.Rules, Data.Stacks, new(World, Data.Rules, Data.Stacks), this).Apply(new(0), Step, ocean, cost, Inputs);
        private void Cosmetic(string effect) { Effects.Add(effect); if (FailCosmetic) throw new InvalidOperationException("Cosmetic unavailable"); }
        public void MoveCursor(MapPoint point) { Equal(Step.To, point); Cosmetic("cursor"); }
        public void EnsureOriginVisible(MapPoint point) { Equal(Step.From, point); Cosmetic("visible"); }
        public void Redraw(MapPoint point) { Equal(Step.From, point); Cosmetic("redraw"); }
        public void Slide(MovementSlide movement)
        {
            Equal(UnitStaging.Lifted(Data.Unit(0).Owner.Value), Data.Unit(0).Position);
            Equal(Step.From, movement.Origin); Equal(Step.To, movement.Destination); Equal(Step.Direction, movement.Direction);
            Cosmetic("slide");
        }
        public void Landing(MovementLandingNotice notice) { Notices.Add(notice); Effects.Add("landing"); if (FailNotice) throw new InvalidOperationException("Required landing unavailable"); }
        public void Publish()
        {
            Effects.Add("publish"); Publications.Add(Enumerable.Range(0, World.Entities.Units.End).Select(i => Data.Unit(i).Visibility).ToArray());
            if (Publications.Count == FailPublish) throw new InvalidOperationException("Publication unavailable");
        }
        public void Observer(int observer)
        { World.Multiplayer.LocalHuman = observer; if (observer >= 0) World.Globals.HumanPlayers = CivilizationSet.Only(observer); }
    }
    private static void MovementViewerSelection()
    {
        foreach (int variant in Enumerable.Range(0, 8))
        {
            var f = new RelocationFixture(); f.Observer(2);
            if (variant == 0) f.Data.Unit(0).Visibility = CivilizationSet.Only(2);
            if (variant == 1) f.Data.Unit(0).Owner = new(2);
            if (variant == 2) f.Step = f.Step with { Owner = 2 };
            if (variant == 3) f.World.Globals.RevealMap = 1;
            if (variant == 4) f.Data.AddCity(0, 2, new(14, 8));
            if (variant == 5) f.Data.AddUnit(1, 2, 0, new(13, 9));
            if (variant == 6) f.Data.AddCity(0, 2, new(8, 8)); // Origin-only neighbor cannot disclose.
            var result = f.Move(); Equal(variant < 6, result.LocalSeen);
            Equal(variant == 2, result.LocalAnimated); Equal(f.Step.To, f.Data.Unit(0).Position);
        }
        foreach (int variant in Enumerable.Range(0, 4))
        {
            var f = new RelocationFixture(); f.World.Globals.HumanPlayers = default;
            if (variant > 0) { f.World.Multiplayer.Saved.AutoplayActive = true; f.Inputs = f.Inputs with { SavedHumans = CivilizationSet.Only(1) }; }
            if (variant == 2) f.World.Globals.CivsAlive = default;
            if (variant == 3) f.Inputs = f.Inputs with { SavedHumans = default(CivilizationSet) };
            Equal(variant == 1, f.Move().LocalSeen);
        }
    }
    private static void MovementViewerAnimation()
    {
        foreach (bool own in new[] { false, true })
            foreach (bool go in new[] { false, true })
                foreach (bool shift in new[] { false, true })
                    foreach (int cost in new[] { 0, 1 })
                        foreach (bool air in new[] { false, true })
                        {
                            var f = new RelocationFixture(); if (!own) f.Observer(2);
                            f.World.Globals.GameOptions.EnemyMoves = true; f.Data.Unit(0).Visibility = CivilizationSet.Only(2);
                            f.Data.Unit(0).Orders = go ? UnitOrder.GoTo : UnitOrder.None;
                            if (air) f.Step = f.Step with { Domain = RuleDomain.Air }; // Captured domain is the local test.
                            f.Inputs = new(shift, 7);
                            var result = f.Move(cost: cost); bool suppress = (cost == 0 || shift || own && air) && (!own || go);
                            Equal(!shift && !suppress, result.LocalAnimated); Equal(suppress ? own ? 2 : 1 : 7, result.Unanimated);
                            Equal(suppress && own, f.Effects.Contains("cursor"));
                        }
        var city = new RelocationFixture(); city.Observer(2); city.Data.Unit(0).Visibility = CivilizationSet.Only(2);
        city.Step = city.Step with { City = new(0) }; Equal(true, city.Move().LocalAnimated);
    }
    private static void MovementKnowledgePublication()
    {
        var f = new RelocationFixture(); f.Observer(2); f.Step = f.Step with { Owner = 0 };
        f.Data.AddCity(0, 2, new(8, 8)); f.Data.AddUnit(1, 1, 0, f.Step.From);
        f.World.Map.TileAt(f.Step.From).RevealedTo = CivilizationSet.Only(2).With(3);
        Equal(true, f.Move().LocalSeen); Equal(CivilizationSet.Only(2).With(3), f.Publications[0][0]);
        Equal(default(CivilizationSet), f.Publications[0][1]); Equal(default(CivilizationSet), f.Data.Unit(0).Visibility);
        foreach (int variant in Enumerable.Range(0, 5))
        {
            f = new(); f.Observer(2); f.Step = f.Step with { City = new(0), DestinationOwner = 3 };
            f.World.Map.TileAt(f.Step.To).RevealedTo = CivilizationSet.Only(2);
            if (variant == 0) f.World.Civilizations[2].Treaties[1].Embassy = true;
            if (variant == 1) f.World.Civilizations[2].Treaties[3].Embassy = true;
            if (variant == 2) f.Step = f.Step with { DestinationOwner = 2 };
            if (variant == 3) f.World.Civilizations[1].Treaties[2].Embassy = true;
            if (variant == 4) { f.World.Civilizations[2].Treaties[1].Embassy = true; f.World.Map.TileAt(f.Step.To).RevealedTo = default; }
            f.Data.AddUnit(1, 1, 0, f.Step.From); f.Move();
            Equal(variant < 3, f.Publications[0][0].Contains(2)); Equal(default(CivilizationSet), f.Publications[0][1]);
        }
    }
    private static void MovementLandingPrefixes()
    {
        foreach (int variant in Enumerable.Range(0, 7))
        {
            var f = new RelocationFixture(); f.Observer(2); f.Data.Unit(0).Visibility = CivilizationSet.Only(2);
            f.Step = f.Step with { DestinationOwner = 3 }; f.World.Map.TileAt(f.Step.To).Region = 4;
            f.Data.AddCity(variant == 0 ? 0 : 1, 2, new(8, 8));
            if (variant == 2) f.World.Civilizations[1].Treaties[3].Peace = true;
            if (variant == 3) f.World.Civilizations[1].Treaties[3].Ceasefire = true;
            if (variant == 4) f.World.Multiplayer.Saved.AutoplayActive = true;
            if (variant == 5) f.World.Civilizations[1].ContinentMilitary[4] = ushort.MaxValue;
            if (variant == 6) f.Data.Unit(0).Type = new(2); // Live domain, despite captured Land.
            f.Move(true); Equal((ushort)(variant == 5 ? ushort.MaxValue : variant == 6 ? 0 : 1), f.World.Civilizations[1].ContinentMilitary[4]);
            Equal(variant is 1 or 5 ? 1 : 0, f.Notices.Count);
        }
        foreach (bool failNotice in new[] { false, true })
        {
            var f = new RelocationFixture(); f.Observer(2); f.Data.Unit(0).Visibility = CivilizationSet.Only(2);
            f.Data.AddCity(1, 2, new(8, 8)); f.FailNotice = failNotice;
            if (failNotice) Throws<InvalidOperationException>(() => f.Move(true));
            else f.Move(true);
            Equal((ushort)1, f.World.Civilizations[1].ContinentMilitary[0]);
            Equal(failNotice ? f.Step.From : f.Step.To, f.Data.Unit(0).Position);
            Equal(failNotice ? 0 : 2, f.Publications.Count); Equal("visible", f.Effects[0]);
            Equal(1, f.Notices.Count);
        }
        var completion = new CompletionFixture(); completion.World.Multiplayer.LocalHuman = 2;
        completion.World.Globals.HumanPlayers = CivilizationSet.Only(2); completion.Movement.LocalHuman = false;
        completion.Data.Stacks.Relocate(0, completion.Step.From, false); completion.Data.Unit(0).Visibility = CivilizationSet.Only(2);
        completion.Step = completion.Step with { DestinationOwner = 2 }; completion.World.Civilizations[1].Treaties[2].Peace = true;
        var relocation = new RelocationFixture(completion.Data) { Step = completion.Step }; var moved = relocation.Move(true);
        int region = completion.World.Map.TileAt(completion.Step.To).Region;
        Equal((ushort)1, completion.World.Civilizations[1].ContinentMilitary[region]);
        completion.Inputs = completion.Inputs with { OriginOcean = true, HasHut = moved.HasHut, LocalSeen = moved.LocalSeen, Unanimated = moved.Unanimated != 0 };
        using var finish = completion.Begin(); completion.Complete(finish);
        Equal((ushort)2, completion.World.Civilizations[1].ContinentMilitary[region]); Equal(1, finish.Result);
    }
    private static void MovementRelocationCargoAndFailures()
    {
        var f = new RelocationFixture(); f.Data.Unit(0).Type = new(1); f.Data.Rules.Units[1] = f.Data.Rules.Units[1] with { Capacity = 1 };
        f.World.Map.TileAt(f.Step.From).Terrain = Terrain.Ocean;
        f.Data.AddUnit(1, 1, 0, f.Step.From); f.Data.Unit(1).Orders = UnitOrder.Sleeping; f.Data.Unit(1).Carrier = new(0);
        f.Data.AddUnit(2, 1, 0, f.Step.From); f.Data.Unit(2).Visibility = CivilizationSet.Only(4);
        f.Step = f.Step with { Domain = RuleDomain.Sea, DestinationOwner = 2 }; f.FailCosmetic = true;
        var result = f.Move(); Equal(2, result.PresentationFailures.Count); Equal(f.Step.To, f.Data.Unit(0).Position);
        Equal(f.Step.To, f.Data.Unit(1).Position); Equal(f.Step.From, f.Data.Unit(2).Position);
        Equal(CivilizationSet.Only(2), f.Data.Unit(0).Visibility); Equal(CivilizationSet.Only(2), f.Data.Unit(1).Visibility);
        Equal(CivilizationSet.Only(4), f.Data.Unit(2).Visibility); Equal(2, f.Publications.Count);
        f = new();
        var hut = new MapPoint(0, 0); f.World.Map.PatternSeed = 24; f.World.Map.TileAt(hut).Owner = null;
        f.Step = f.Step with { To = hut }; result = f.Move(); Equal(true, result.HasHut); Equal(false, f.World.Map.Hut(hut));
        foreach (int publication in new[] { 1, 2 })
        {
            f = new(); f.FailPublish = publication; Throws<InvalidOperationException>(() => f.Move());
            Equal(publication == 1 ? f.Step.From : f.Step.To, f.Data.Unit(0).Position);
        }
        f = new(); f.World.Globals.Multiplayer = GameMode.Tcp; Throws<InvalidOperationException>(() => f.Move()); Equal(0, f.Effects.Count);
    }
    private static void MovementViewerSafeDefaults()
    {
        foreach (int observer in new[] { -1, 0 })
            foreach (bool hut in new[] { false, true })
            {
                var absent = new RelocationFixture(); absent.Observer(observer);
                if (hut)
                {
                    absent.World.Map.PatternSeed = 24; absent.Step = absent.Step with { To = new(0, 0) };
                    absent.World.Map.TileAt(absent.Step.To).Owner = null;
                }
                else absent.World.Map.TileAt(absent.Step.To).Owner = 1;
                var moved = absent.Move(); Equal(hut, moved.HasHut);
                Equal(false, moved.LocalSeen); Equal(false, moved.LocalAnimated); Equal(0, absent.Notices.Count);
                Equal(absent.Step.To, absent.Data.Unit(0).Position); Equal(2, absent.Publications.Count);
                Equal(false, absent.Effects.Contains("slide") || absent.Effects.Contains("visible"));
            }
        var f = new RelocationFixture();
        f = new(); f.World.Globals.HumanPlayers = default; f.World.Multiplayer.Saved.AutoplayActive = true;
        Throws<InvalidOperationException>(() => f.Move()); Equal(f.Step.From, f.Data.Unit(0).Position);
    }
}
