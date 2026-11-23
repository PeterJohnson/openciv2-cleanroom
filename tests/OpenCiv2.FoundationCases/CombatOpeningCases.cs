using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class OpeningFixture
    {
        public CombatFixture Combat { get; } = new();
        public RevelationFixture Data => Combat.Data;
        public GameState World => Data.World;
        public CombatWorkspace State => Combat.State;
        public CombatOpening Opening { get; }
        public List<CombatOpeningPresentation> Views { get; } = [];
        public int Publications;
        public bool FailDisplay;
        public MapPoint? PositionDuringDisplay;
        public OpeningFixture()
        {
            World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            World.Names[1] = new() { Adjective = "Alder" }; World.Names[2] = new() { Adjective = "Birch" };
            Data.Rules.Units[10] = Data.Rules.Units[10] with { Identity = new("attacker", "Custom attacker") };
            Data.Rules.Units[0] = Data.Rules.Units[0] with { Identity = new("defender", "Custom defender") };
            Opening = new(World, Data.Rules, State, Data.Stacks, new(World, Data.Rules, Data.Stacks),
                () => ++Publications, view =>
                {
                    Views.Add(view); PositionDuringDisplay = Data.Unit(3).Position;
                    if (FailDisplay) throw new InvalidOperationException("Renderer unavailable");
                });
        }
        public PreparedCombat Prepare() => Combat.Prepare(true);
        public CombatOpeningResult Present(PreparedCombat? combat = null) => Opening.Present(combat ?? Prepare(), 1, "retained text");
        public void Type(int type, RuleDomain domain = RuleDomain.Land, byte range = 1, bool missile = false, bool submarine = false, byte attack = 10)
        {
            Data.Rules.Units[type] = Data.Rules.Units[10] with
            { Domain = domain, Range = range, Attack = attack, Abilities = new() { DestroyedAfterAttack = missile, Submarine = submarine } };
            Data.Unit(3).Type = new((byte)type);
        }
    }

    // U06 viewer eligibility and knowledge sources; viewer arrays are not a source of authority.
    private static void CombatOpeningViewers()
    {
        foreach (int variant in Enumerable.Range(0, 7))
        {
            var f = new OpeningFixture(); var combat = f.Prepare(); f.World.Multiplayer.LocalHuman = 3;
            f.World.Globals.HumanPlayers = CivilizationSet.Only(3); f.World.Globals.CivsAlive = CivilizationSet.Only(3);
            for (int i = 0; i < 8; ++i) f.State.Seen[i] = true;
            if (variant == 0) combat = combat with { AttackerOwner = 3 };
            if (variant == 1) combat = combat with { DefenderOwner = 3 };
            if (variant == 2) f.World.Globals.RevealMap = 1;
            if (variant == 3) f.Data.Unit(3).Visibility = CivilizationSet.Only(3);
            if (variant == 4) f.Data.Unit(2).Visibility = CivilizationSet.Only(3);
            if (variant == 5) f.Data.Unit(3).Owner = new(3);
            if (variant == 6) f.Data.Unit(2).Owner = new(3);
            Equal(true, f.Opening.PublishViewers(combat)); Equal(true, f.State.Seen[0]);
            for (int i = 1; i < 8; ++i) Equal(i == 3, f.State.Seen[i]);
        }
        foreach (int observer in new[] { -1, 0, 1, 8 })
            foreach (bool human in new[] { false, true })
                foreach (bool alive in new[] { false, true })
                {
                    var f = new OpeningFixture(); f.World.Multiplayer.LocalHuman = observer;
                    f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(1, human);
                    f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(1, alive); f.World.Globals.RevealMap = 1;
                    Equal(observer == 1 && human && alive, f.Opening.PublishViewers(f.Prepare()));
                }
        var hidden = new OpeningFixture(); hidden.World.Multiplayer.LocalHuman = 3;
        hidden.World.Globals.HumanPlayers = hidden.World.Globals.CivsAlive = CivilizationSet.Only(3);
        hidden.State.LastSound = CombatSound.JetCrash; hidden.State.SoundArgument = 9; hidden.Present();
        Equal(false, hidden.State.AnyViewer); Equal(0, hidden.Views.Count); Equal(0, hidden.Publications);
        Equal(CombatSound.JetCrash, hidden.State.LastSound); Equal(9, hidden.State.SoundArgument);
        Equal(false, hidden.Data.Unit(3).Visibility.Contains(2)); Equal(new MapPoint(8, 10), hidden.Data.Unit(3).Position);
    }

    private static void CombatOpeningScenarioBoundary()
    {
        var f = new OpeningFixture(); var combat = f.Prepare(); f.World.Globals.Status.Scenario = true;
        Equal(true, f.Opening.PublishViewers(combat)); // Participant knowledge is independent of the missing selector.
        f.World.Multiplayer.LocalHuman = 3; f.World.Globals.CivsAlive = f.World.Globals.HumanPlayers = CivilizationSet.Only(3);
        f.World.Scenario.Flags.WholeMapReveal = true;
        Throws<InvalidOperationException>(() => f.Opening.PublishViewers(combat));
        Equal(false, f.Opening.PublishViewers(combat, false)); Equal(true, f.Opening.PublishViewers(combat, true));
        f.World.Globals.Multiplayer = GameMode.Tcp; f.State.Seen[1] = true;
        Throws<InvalidOperationException>(() => f.Opening.PublishViewers(combat)); Equal(true, f.State.Seen[1]); Equal(0, f.Publications);
    }

    private static void CombatOpeningCargoAndKnowledge()
    {
        foreach (bool fail in new[] { false, true })
        {
            var f = new OpeningFixture { FailDisplay = fail }; var combat = f.Prepare();
            f.Type(UnitTypeIds.Transport, RuleDomain.Sea); f.Data.Rules.Units[UnitTypeIds.Transport] = f.Data.Rules.Units[UnitTypeIds.Transport] with { Capacity = 1 };
            f.Data.AddUnit(4, 1, 0, combat.Origin); f.Data.Unit(4).Orders = UnitOrder.Sleeping; f.Data.Unit(4).Carrier = new(3);
            f.Data.AddUnit(5, 1, 0, combat.Origin); f.Data.Unit(5).Orders = UnitOrder.Fortified;
            var result = f.Present(combat); Equal(true, result.Visible); Equal(fail, result.PresentationFailure is InvalidOperationException);
            Equal(1, f.Publications); Equal(UnitStaging.Lifted(1), f.PositionDuringDisplay);
            Equal(combat.Origin, f.Data.Unit(3).Position); Equal(combat.Origin, f.Data.Unit(4).Position); Equal(combat.Origin, f.Data.Unit(5).Position);
            Equal(new UnitSlot(3), f.Data.Unit(4).Carrier); Equal(UnitOrder.Sleeping, f.Data.Unit(4).Orders);
            Equal(true, f.Data.Unit(3).Visibility.Contains(2)); Equal(false, f.Data.Unit(4).Visibility.Contains(2));
            Equal(false, f.Data.Unit(5).Visibility.Contains(2)); Equal(false, f.World.Map.TileAt(combat.Origin).RevealedTo.Contains(2));
            Equal(3, f.Data.Stacks.Count(3)); Equal(combat.Defender, f.State.AnimationDefender); Equal(combat.Target, f.State.AnimationTarget);
            Equal(6, f.State.ExplosionKind); Equal((CombatSound?)null, f.State.LastSound); Equal((int?)null, f.State.SoundArgument);
            Equal(1, f.Views.Single().Recipient); Equal(combat.Target, f.Views.Single().Target);
        }
    }

    private static void CombatOpeningNotices()
    {
        var f = new OpeningFixture(); f.Combat.City(); f.World.Entities.Cities.Storage[0].Name = "Birch City";
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        f.Data.Rules.Improvements[BuildingIds.CoastalFortress] = new() { Identity = new("coast", "Custom coast") };
        f.Data.Rules.Improvements[BuildingIds.SdiDefense] = new() { Identity = new("sdi", "Custom shield") };
        var combat = f.Prepare() with { ShoreBombard = true, ImprovementNotice = BuildingIds.CoastalFortress, SdiBonus = true, Scramble = true };
        f.Type(10, missile: true); f.World.Map.TileAt(combat.Origin).Terrain = Terrain.Ocean;
        f.Present(combat); var notices = f.Views.Single().Notices;
        Equal(5, notices.Count);
        Equal(CombatOpeningNoticeKind.MissileAttack, notices[0].Kind); Equal("Birch City", notices[0].Arguments[0]);
        Equal(CombatOpeningNoticeKind.PearlHarbor, notices[1].Kind); Equal("Alder|Custom attacker|Birch", string.Join('|', notices[1].Arguments));
        Equal(CombatOpeningNoticeKind.BatteryWithSdi, notices[2].Kind); Equal("Alder|Custom coast|Birch City|Custom shield", string.Join('|', notices[2].Arguments));
        Equal(BuildingIds.CoastalFortress, notices[2].Improvement);
        Equal(CombatOpeningNoticeKind.Scramble, notices[3].Kind); Equal("Birch|Custom defender|Birch City", string.Join('|', notices[3].Arguments));
        Equal(new UnitTarget(combat.Defender, f.Data.Unit(2).Token), notices[3].Unit);
        Equal(CombatOpeningNoticeKind.AmphibiousAttack, notices[4].Kind); Equal("Birch|Alder|Custom attacker", string.Join('|', notices[4].Arguments));

        var absent = new OpeningFixture(); absent.World.Globals.HumanPlayers = CivilizationSet.Only(1).With(2);
        var noCity = absent.Prepare(); absent.Type(10, missile: true); absent.Present(noCity); Equal(0, absent.Views.Single().Notices.Count);
        var retained = new OpeningFixture(); retained.Combat.City(); retained.World.Entities.Cities.Storage[0].Name = "City";
        retained.Data.Rules.Improvements[BuildingIds.SdiDefense] = new() { Identity = new("sdi", "Shield") };
        retained.Present(retained.Prepare() with { ImprovementNotice = BuildingIds.SdiDefense, SdiBonus = true });
        var battery = retained.Views.Single().Notices.Single(); Equal(CombatOpeningNoticeKind.Battery, battery.Kind);
        Equal("retained text|Shield|City", string.Join('|', battery.Arguments));
    }

    private static void CombatOpeningSoundSlots()
    {
        // Fixed stock identities still resolve scenario overrides after arbitrary unit renaming.
        foreach (var (type, sound, explosion) in new (int, CombatSound?, int)[]
        {
            (54, CombatSound.Extra1, 0), (61, CombatSound.Extra8, 0), (51, CombatSound.CustomLand, 0),
            (52, CombatSound.CustomShip, 0), (53, CombatSound.CustomAir, 0), (17, CombatSound.Elephant, 0),
            (15, CombatSound.SwordHorse, 0), (16, CombatSound.SwordHorse, 0), (18, CombatSound.SwordHorse, 0), (19, CombatSound.SwordHorse, 0),
            (20, CombatSound.Cavalry, 0), (21, CombatSound.Cavalry, 0), (7, CombatSound.Infantry, 0), (9, CombatSound.Infantry, 0),
            (10, CombatSound.Infantry, 0), (11, CombatSound.Infantry, 0), (8, CombatSound.MachineGuns, 0), (12, CombatSound.MachineGuns, 0),
            (13, CombatSound.MachineGuns, 0), (14, CombatSound.MachineGuns, 0), (23, CombatSound.Catapult, 0), (22, null, 40),
            (24, CombatSound.Fire, 40), (25, CombatSound.Fire, 40), (26, CombatSound.Fire, 40), (2, CombatSound.SwordFight, 0)
        })
        {
            var f = new OpeningFixture(); var combat = f.Prepare(); f.Type(type); f.State.LastSound = CombatSound.JetCrash; f.State.SoundArgument = 9;
            f.Present(combat); Equal(sound, f.State.LastSound); Equal(sound is null ? (int?)null : 1, f.State.SoundArgument);
            Equal(explosion, f.State.ExplosionKind); Equal(sound, f.Views.Single().Sound?.Sound);
        }
        // EXTRA and CUSTOM precede even nuclear/missile and domain checks.
        foreach (int type in new[] { 51, 54 })
        {
            var f = new OpeningFixture(); var combat = f.Prepare(); f.Type(type, RuleDomain.Air, missile: true, attack: 99);
            f.Present(combat); Equal(type == 51 ? CombatSound.CustomLand : CombatSound.Extra1, f.State.LastSound);
        }
    }

    private static void CombatOpeningDomainSounds()
    {
        foreach (var (type, domain, range, missile, submarine, attack, airDefender, sound, argument, explosion) in new
            (int, RuleDomain, byte, bool, bool, byte, bool, CombatSound?, int?, int)[]
        {
            (44, RuleDomain.Air, 1, true, false, 98, false, CombatSound.Missile, 1, 0),
            (45, RuleDomain.Air, 1, true, false, 99, false, null, null, 0),
            (45, RuleDomain.Air, 1, true, false, 255, false, CombatSound.Missile, 1, 0),
            (27, RuleDomain.Air, 1, false, false, 10, true, CombatSound.AirCombat, 0, 0),
            (30, RuleDomain.Air, 1, false, false, 10, true, CombatSound.JetCombat, 0, 0),
            (29, RuleDomain.Air, 0, false, false, 10, false, CombatSound.Helicopter, 1, 0),
            (28, RuleDomain.Air, 1, false, false, 10, false, CombatSound.DiveBomb, 1, 0),
            (31, RuleDomain.Air, 1, false, false, 10, false, CombatSound.JetBomb, 1, 0),
            (41, RuleDomain.Sea, 1, false, true, 10, false, CombatSound.Torpedoes, 1, 0),
            (37, RuleDomain.Sea, 1, false, false, 10, false, null, null, 46),
            (38, RuleDomain.Sea, 1, false, false, 10, false, null, null, 46),
            (39, RuleDomain.Sea, 1, false, false, 10, false, null, null, 46),
            (40, RuleDomain.Sea, 1, false, false, 10, false, null, null, 46),
            (36, RuleDomain.Sea, 1, false, false, 10, false, null, null, 6)
        })
        {
            var f = new OpeningFixture(); var combat = f.Prepare(); f.Type(type, domain, range, missile, submarine, attack);
            if (airDefender) f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Air };
            f.Present(combat); Equal(sound, f.State.LastSound); Equal(argument, f.State.SoundArgument); Equal(explosion, f.State.ExplosionKind);
        }
    }

    private static void CombatOpeningAutoplayAndFailure()
    {
        var f = new OpeningFixture(); f.World.Multiplayer.Saved.AutoplayActive = true; var combat = f.Prepare() with { ShoreBombard = true };
        f.Present(combat); Equal(0, f.Views.Single().Notices.Count); Equal(true, f.Data.Unit(3).Visibility.Contains(2));
        Equal(combat.Origin, f.Data.Unit(3).Position); Equal(CombatSound.Infantry, f.State.LastSound);
        var broken = new OpeningFixture(); var missing = broken.Prepare() with { ImprovementNotice = BuildingIds.CityWalls };
        broken.World.Multiplayer.Saved.AutoplayActive = true;
        Throws<InvalidOperationException>(() => broken.Present(missing)); // Text is prepared even when its notice is suppressed.
        Equal(missing.Origin, broken.Data.Unit(3).Position); Equal(1, broken.Publications); Equal(0, broken.Views.Count);
    }

    private static void CombatOpeningRoundsResolutionJoin()
    {
        foreach (bool failedRenderer in new[] { false, true })
        {
            var f = new ResolutionFixture([CombatChoice("U06.round.attack", 8, 7), CombatChoice("U06.round.defense", 4, 0)]);
            f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            var combat = f.Prepare() with { AttackFirepower = 10 };
            var opening = new CombatOpening(f.World, f.Data.Rules, f.State, f.Data.Stacks, new(f.World, f.Data.Rules, f.Data.Stacks), () => { },
                _ => { if (failedRenderer) throw new InvalidOperationException("No renderer"); });
            var result = opening.Present(combat, 1, ""); Equal(failedRenderer, result.PresentationFailure is not null);
            Equal(combat.Origin, f.Data.Unit(3).Position); Equal(true, f.State.AnyViewer);
            using var rounds = RoundService(f.Combat, f.Life.Random, new()).Begin(combat, "fight"); Equal(10, rounds.Advance());
            using var resolution = f.Resolution.Begin(combat, rounds.Result != 0, "resolution"); f.Complete(resolution);
            Equal(3, f.State.KillCount); Equal(true, f.Data.Unit(3).Flags.Veteran); Equal(combat.Origin, f.Data.Unit(3).Position);
            Equal((UnitSlot?)null, f.State.AnimationDefender); Equal(false, f.Data.Unit(2).Token.Occupied);
        }
    }
}
