using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
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
    // U02/W03/V04/S02/V03: the root owns gameplay; fixtures supply authored state and presentation.
    private sealed class GameplayFixture
    {
        public SinglePlayerMovementFixture F { get; }
        public SinglePlayerGameplay Services { get; }
        public SelectionEffects Selection { get; } = new();
        public RevelationFixture Data => F.Data;
        public GameState World => F.World;
        public GameplayFixture(List<Choice>? choices = null, WonderBuildRestrictions? restrictions = null,
            RevelationFixture? data = null, SinglePlayerGameplayEffects? presentation = null)
        {
            F = new(choices, data);
            var life = F.Life; var events = life.Diplomacy.Events; var grant = life.Diplomacy.Grant;
            var audience = events.Contact.F; var conversation = audience.C;
            var demands = conversation.F; var treaties = demands.F; var theft = audience.T.T;
            var combat = F.Combat.F; var capture = combat.F.Drop.Capture;
            var mission = F.Missions.F; var move = F.F;
            Services = new(World, Data.Process, life.Random,
                new(NamingCatalog(["First", "Second"]), new(), new(), GameplayContent.Builtin.Values,
                    new(World.Map), events.Media, lines => { events.Texts.Add(lines); return lines; },
                    () => { using var reader = mission.Text.Reader(); return ClassicCityMissionOptions.Read(reader, "missions", " (Spy authored)"); },
                    () => { using var reader = mission.Revolt.Text.Reader(); return ClassicRevoltOptions.Read(reader, "revolt"); }, "Food",
                    WonderRestrictions: restrictions),
                presentation ?? new(grant, grant.G, grant,
                    new(treaties.WarEffects, events.Contact, audience.T, audience, treaties, demands, conversation, conversation,
                        demands, conversation, audience, theft, _ => { }, treaties.Agreements.Add,
                        conversation.Notices.Add, events.Contact.Notices.Add, theft.Taken.Add),
                    new(Data, Data, life.Life, life.Life, life.Life, life.War, life.Incidents.Add, life.Removed.Add,
                        life.Life.RedrawTile, life.Life.RedrawMaps),
                    new(capture, capture, capture, combat.F.Drop, combat.F, combat.F, new RoundEffects(), combat.F,
                        combat, combat.F, _ => { }, _ => { }, _ => { }, () => { }),
                    new(mission, mission.Capture.Theft.Damage, mission.Capture.Theft, mission.Revolt,
                        F.Missions.UnitEffects, F.Missions.Devices.Add, () => ++mission.Stops),
                    new(move, move.Land, move.Land, move.Foreign, move.Foreign, move.Finish, move.City, move.Relocation,
                        move.Finish, move.Finish, move.Finish, () => throw new InvalidOperationException("Unexpected city limit")),
                    Selection, new(_ => { }, _ => { }, (_, _) => { }), _ => { }, capture.Upgrades.Add,
                    _ => { }, _ => { }, () => ++events.Suppressed), "gameplay");
        }
        public MovementDispatcher.Operation Begin()
        {
            var from = Data.Unit(0).Position; var to = new MapPoint(from.X + 2, from.Y);
            F.F.Relocation.Step = F.F.Relocation.Step with { From = from, To = to, Direction = 1 };
            F.F.Land.Step = F.F.Land.Step with { From = from, To = to, Direction = 1 };
            return Services.Movement.Dispatcher.Begin(new(0), 1, new(true, true), F.F.Inputs, "move");
        }
    }

    private static void SinglePlayerGameplayMovementContact()
    {
        var f = new GameplayFixture(); f.Data.AddUnit(2, 2, 4, new(21, 13));
        f.World.Multiplayer.LocalHuman = 2; f.World.Events.Loaded = true;
        var permission = NegotiationEvent(f.World, 1, 2, deny: true);
        permission.Values.Actions = new() { Text = true, JustOnce = true };
        permission.SetString(Event.TextBegin, new("Root movement contact"));
        using var operation = f.Begin(); var pending = operation.Resume();
        Equal(true, pending is MovementDecision.Finish { Decision: MovementCompletionDecision.Contact });
        Equal(new MapPoint(20, 12), f.Data.Unit(0).Position); Equal(pending, operation.Resume());
        Equal(false, permission.Values.Actions.Consumed); Equal((byte)0, f.Data.Unit(0).LastDirection);
        operation.Resume(new(0)); f.F.Complete(operation);
        Equal(true, permission.Values.Actions.Consumed); Equal((byte)1, f.Data.Unit(0).LastDirection);
        Equal("Root movement contact", f.F.Life.Diplomacy.Events.Texts.Single()[0]);
    }

    private static void SinglePlayerGameplayScenarioCycle()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var f = new GameplayFixture(); var events = f.F.Life.Diplomacy.Events;
            var entry = ScenarioNestedCreation(events); int gold = f.World.Civilizations[1].Gold;
            using var operation = f.Services.Lifecycle.Events.Begin(entry, "scenario");
            var pending = operation.Resume();
            Equal(true, pending is LifecycleContact { Decision: DiplomaticContactDecision.Scenario { Value: ResearchGrantDecision.Government } });
            int unit = f.Services.Stacks.FirstAt(new(20, 12)); var token = f.Data.Unit(unit).Token;
            Equal(pending, operation.Resume()); Equal(gold, f.World.Civilizations[1].Gold);
            if (dispose)
            {
                operation.Dispose(); Throws<ObjectDisposedException>(() => operation.Resume(new(0)));
                Equal(false, entry.Values.Actions.Consumed);
            }
            else
            {
                Equal(true, operation.Resume(new(0)) is ScenarioText);
                Equal(gold + 7, f.World.Civilizations[1].Gold);
                Equal(null, operation.Resume(new(0))); Equal(true, entry.Values.Actions.Consumed);
                Equal(true, f.World.Civilizations[1].Advances.Contains(AdvanceIds.Monarchy));
            }
            Equal(token, f.Data.Unit(unit).Token); f.F.Consumed();
        }
    }

    private static void SinglePlayerGameplayCapture()
    {
        foreach (bool scenario in new[] { false, true })
        {
            var f = new GameplayFixture([CaptureParity(0) with { Key = new("C09.capture.building-parity", "move/0/finish/capture") }],
                scenario ? new(true, false) : null);
            f.F.AtBirch(); var entry = f.F.Combat.Event(EventTrigger.CityTaken);
            if (scenario)
            {
                f.World.Globals.Status.Scenario = true;
                f.F.F.Capture.City.Production = -40;
                f.Data.Rules.Units[61] = f.Data.Rules.Units[4] with
                { Role = RuleRole.Defend, Defense = 1, CostRows = 2, Prerequisite = AdvanceReference.None };
            }
            using var operation = f.Begin();
            Equal(true, operation.Resume() is MovementDecision.Finish { Decision: MovementCompletionDecision.Capture { Decision: CityCaptureDecision.Lifecycle } });
            Equal(new CivilizationSlot(1), f.F.F.Capture.City.Owner);
            Equal(true, operation.Resume(new(0)) is MovementDecision.Finish);
            Equal(true, operation.Resume(new(0)) is MovementDecision.Finish { Decision: MovementCompletionDecision.Capture { Decision: CityCaptureDecision.Window } });
            operation.Resume(new(0)); f.F.Complete(operation);
            Equal(true, entry.Values.Actions.Consumed); Equal((byte)1, f.Data.Unit(0).LastDirection);
            if (scenario) Equal((sbyte)61, f.F.F.Capture.City.Production);
        }
    }

    // C06 automatic selection must enter A02, including its actual C03 calculation.
    private static void SinglePlayerGameplayProduction()
    {
        var f = new GameplayFixture();
        f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        ref var city = ref f.World.Entities.Cities.Storage[0];
        Equal(new CivilizationSlot(2), city.Owner);
        city.Production = 99; city.ShieldBox = 17;
        f.World.Civilizations[2].Government = Government.Despotism;
        for (int i = 0; i < RuleLimits.UnitTypes; ++i)
        { f.Data.Rules.Units[i] = f.Data.Rules.Units[i] with { Prerequisite = AdvanceReference.Never }; }
        foreach (int type in new[] { 3, 61 })
        {
            f.Data.Rules.Units[type] = new()
            {
                Role = RuleRole.Defend,
                Domain = RuleDomain.Land,
                Attack = 1,
                Defense = 1,
                Movement = 3,
                HitPoints = 10,
                Firepower = 1,
                CostRows = 2,
                Prerequisite = AdvanceReference.None
            };
        }
        f.Services.Production.Set(new(0), 99);
        Equal((sbyte)61, city.Production); Equal((short)17, city.ShieldBox);
        Equal((byte)1, f.World.Civilizations[2].UnitsInProduction[61]);
        f.Data.Rules.Units[61] = f.Data.Rules.Units[61] with { Abilities = new() { Paradrop = true } };
        f.Services.Production.Set(new(0), 99);
        Equal((sbyte)3, city.Production); Equal((byte)0, f.World.Civilizations[2].UnitsInProduction[61]);
        Equal((byte)1, f.World.Civilizations[2].UnitsInProduction[3]); f.F.Consumed();
    }

    // U01/V01: basic deletion's required owner check must never be a presentation callback.
    private static void SinglePlayerGameplayDeletion()
    {
        var f = new GameplayFixture(); f.World.Globals.HumanPlayers = CivilizationSet.Only(1);
        f.Data.Unit(0).Type = new(0); f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Role = RuleRole.Settle };
        f.World.Civilizations[1].CityCount = 0; f.World.Civilizations[1].UnitsActive[0] = 1;
        f.Data.AddUnit(2, 1, 4, new(22, 12)); f.World.Civilizations[1].SpaceshipParts[0] = 3;
        f.Services.Deletion.Erase(0, true);
        Equal(false, f.Data.Unit(0).Token.Occupied); Equal(false, f.Data.Unit(2).Token.Occupied);
        Equal(EndReason.Defeat, f.World.PendingEndReason); Equal((short)0, f.World.Civilizations[1].SpaceshipParts[0]);
        Equal(false, f.World.UnitDeletion.Active); Equal(0, f.F.Life.Life.Checks); f.F.Consumed();
    }
}
