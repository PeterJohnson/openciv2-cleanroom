using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.Units;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static PillageCommand PlayerPillage(PillageFixture f) => new(f.World, f.Data.Rules,
        new(f.World, f.Data.Rules, _ => { }), War(f.Data, new()), f.Pillage);

    // W04 player choices retain their feature set; a single Farmland choice leaves automatic selection.
    private static void PillageCommandChoices()
    {
        foreach (bool multiple in new[] { false, true })
        {
            var f = new PillageFixture(); f.Features(MapFeatures.Irrigation | MapFeatures.Mine | (multiple ? MapFeatures.Road : MapFeatures.None));
            using var op = PlayerPillage(f).Begin(new(0)); var pending = op.Resume();
            if (multiple)
            {
                var menu = (PillageDecision.Menu)pending!;
                Sequence(new[] { PillageFeature.Farmland, PillageFeature.Road }, menu.Choices.Select(c => c.Feature));
                Equal(1, menu.Owner); Equal(menu, op.Resume()); op.Resume(0);
            }
            Equal(PillageOutcome.Applied, op.Result);
            Equal(MapFeatures.Unit | (multiple ? MapFeatures.Road : MapFeatures.Irrigation), f.World.Map.TileAt(f.Point).Features);
        }
        foreach (bool airbase in new[] { false, true })
        {
            var f = new PillageFixture(); f.Features(MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Fortification
                | MapFeatures.Road | MapFeatures.Railroad | (airbase ? MapFeatures.CitySite : MapFeatures.None));
            using var op = PlayerPillage(f).Begin(new(0)); var menu = (PillageDecision.Menu)op.Resume()!;
            Sequence(new[] { PillageFeature.Farmland, airbase ? PillageFeature.Airbase : PillageFeature.Fortress, PillageFeature.Railroad }, menu.Choices.Select(c => c.Feature));
            op.Resume(1); Equal(PillageOutcome.Applied, op.Result);
            Equal(MapFeatures.Unit | MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Road | MapFeatures.Railroad, f.World.Map.TileAt(f.Point).Features);
        }
        var cancelled = new PillageFixture(); cancelled.Features(MapFeatures.Mine | MapFeatures.Road);
        using var cancel = PlayerPillage(cancelled).Begin(new(0)); cancel.Resume(); cancel.Resume(-1);
        Equal(PillageOutcome.Cancelled, cancel.Result); Equal((byte)0, cancelled.Data.Unit(0).MovesUsed); Equal(0, cancelled.Redraws.Count);
    }

    private static void PillageCommandAdmission()
    {
        foreach (int variant in Enumerable.Range(0, 6))
        {
            var f = new PillageFixture(); f.Features(MapFeatures.Road);
            switch (variant)
            {
                case 1: f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = 0 }; break;
                case 2: f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Domain = RuleDomain.Sea }; break;
                case 3: f.Data.AddCity(0, 1, f.Point); break;
                case 4: f.Features(MapFeatures.Railroad); break;
                case 5: f.Data.Rules.Units[0] = f.Data.Rules.Units[0] with { Attack = 255 }; break;
            }
            bool available = variant is 0 or 5;
            Equal(available, PillageCommand.Available(f.World, f.Data.Rules, new(0)));
            using var op = PlayerPillage(f).Begin(new(0)); Equal(null, op.Resume());
            Equal(available ? PillageOutcome.Applied : PillageOutcome.Unavailable, op.Result);
            Equal((byte)(available ? 6 : 0), f.Data.Unit(0).MovesUsed);
        }
    }

    private static void PillageCommandDeclaration()
    {
        foreach (int citySlot in new[] { 0, 1 })
        {
            var f = new PillageFixture(); f.Features(MapFeatures.Mine); f.Data.AddCity(citySlot, 2, new(14, 10));
            f.World.Globals.Difficulty = Difficulty.Prince;
            using var op = PlayerPillage(f).Begin(new(0)); var warning = (PillageDecision.Attack)op.Resume()!;
            Equal(2, warning.Warning.Victim.Civilization); Equal(null, op.Resume(1)); Equal(PillageOutcome.Applied, op.Result);
            Equal(true, f.World.Civilizations[1].Treaties[2].War);
            Equal((byte)citySlot, f.World.Civilizations[1].Reputation);
            Equal((sbyte)-citySlot, f.World.Civilizations[1].ReputationCredit[2]);
            Equal(citySlot == 1, f.World.Civilizations[1].Treaties[2].Aggressor);
        }
        var ai = new PillageFixture(); ai.Features(MapFeatures.Mine); ai.Data.AddCity(1, 2, new(14, 10));
        ai.World.Globals.HumanPlayers = CivilizationSet.Only(2);
        using var retained = PlayerPillage(ai).Begin(new(0));
        Equal(1, ((PillageDecision.Attack)retained.Resume()!).Warning.Actor.Civilization);
        retained.Resume(0); Equal(PillageOutcome.Cancelled, retained.Result);
        Equal(false, ai.World.Civilizations[1].Treaties[2].War); Equal((byte)0, ai.Data.Unit(0).MovesUsed);
    }

    private static void PillageCommandRetainedMenuAndCore()
    {
        foreach (bool acceptCore in new[] { false, true })
        {
            var f = new PillageFixture(); f.Features(MapFeatures.Mine | MapFeatures.Road);
            f.Data.AddCity(1, 2, new(12, 10)); f.Data.AddCity(0, 3, new(30, 10));
            f.World.Civilizations[1].Treaties[3].Peace = true;
            using var op = PlayerPillage(f).Begin(new(0)); Equal(true, op.Resume() is PillageDecision.Menu);
            f.Data.Stacks.Relocate(0, new(28, 10), false);
            f.World.Map.TileAt(new(28, 10)).Features |= MapFeatures.Mine | MapFeatures.Irrigation | MapFeatures.Road;
            var first = (PillageDecision.Attack)op.Resume(0)!;
            Equal(2, first.Warning.Victim.Civilization); // Wrapper still searches its captured origin.
            var second = (PillageDecision.Attack)op.Resume(1)!;
            Equal(3, second.Warning.Victim.Civilization); Equal(true, f.World.Civilizations[1].Treaties[2].War);
            op.Resume(acceptCore ? 1 : 0);
            Equal(acceptCore ? PillageOutcome.Applied : PillageOutcome.Cancelled, op.Result);
            Equal(true, f.World.Civilizations[1].Treaties[2].War); // Cancellation does not undo the earlier declaration.
            Equal(MapFeatures.Mine | MapFeatures.Road, f.World.Map.TileAt(f.Point).Features);
            Equal(MapFeatures.Unit | MapFeatures.Irrigation | MapFeatures.Road | (acceptCore ? MapFeatures.None : MapFeatures.Mine),
                f.World.Map.TileAt(new(28, 10)).Features);
            Equal((byte)(acceptCore ? 6 : 0), f.Data.Unit(0).MovesUsed);
        }
        var changed = new PillageFixture(); changed.Features(MapFeatures.Mine | MapFeatures.Road);
        using var selection = PlayerPillage(changed).Begin(new(0)); selection.Resume();
        changed.Features(MapFeatures.Irrigation | MapFeatures.Road); selection.Resume(0);
        Equal(PillageOutcome.Applied, selection.Result); // Retained Mine choice clears nothing; it does not fall back to Irrigation.
        Equal(MapFeatures.Unit | MapFeatures.Irrigation | MapFeatures.Road, changed.World.Map.TileAt(changed.Point).Features);
        Equal((byte)6, changed.Data.Unit(0).MovesUsed);
    }

    private static void PillageCommandSessionAndFailure()
    {
        foreach (var kind in new[] { OperationInputKind.Cancel, OperationInputKind.Timeout, OperationInputKind.Choice })
        {
            var f = new PillageFixture(); f.Features(MapFeatures.Mine | MapFeatures.Road); f.Data.AddCity(1, 2, new(14, 10));
            var random = WarRandom();
            var context = new OperationContext(f.World, f.Data.Process, random, new PresentationRandom(1, "pillage-command"), new(default, new(3)), default, (_, _) => { }, () => { });
            using var session = new PillageCommandOperation(PlayerPillage(f).Begin(new(0)), c => new((ulong)c));
            var menu = session.Resume(context, new()); Equal(CommandStatus.AwaitingInteraction, menu.Status);
            Equal(new ParticipantId(1), menu.Interaction!.Recipient); Sequence(new[] { 0, 1, -1 }, menu.Interaction.Choices);
            var result = session.Resume(context, new(kind, 0));
            if (kind == OperationInputKind.Choice)
            {
                Equal(CommandStatus.AwaitingInteraction, result.Status); Equal(new ParticipantId(1), result.Interaction!.Recipient);
                Sequence(new[] { 0, 1 }, result.Interaction.Choices);
                result = session.Resume(context, new(OperationInputKind.Choice, 1));
            }
            Equal(CommandStatus.Completed, result.Status);
            Equal(new CommandValue.Text(kind == OperationInputKind.Choice ? "Applied" : "Cancelled"), result.Result!.Single().Value);
            random.RequireChoicesConsumed();
        }
        var invalid = new PillageFixture(); invalid.Features(MapFeatures.Mine | MapFeatures.Road);
        using var badChoice = PlayerPillage(invalid).Begin(new(0)); badChoice.Resume();
        Throws<ArgumentOutOfRangeException>(() => badChoice.Resume(7)); Throws<InvalidOperationException>(() => badChoice.Resume(0));
        Equal((byte)0, invalid.Data.Unit(0).MovesUsed);
        var cosmetic = new PillageFixture { FailRedraw = true }; cosmetic.Features(MapFeatures.Mine);
        using var draw = PlayerPillage(cosmetic).Begin(new(0)); draw.Resume(); Equal(PillageOutcome.Applied, draw.Result); Equal(1, draw.PresentationFailures.Count);
        var abandoned = new PillageFixture(); abandoned.Features(MapFeatures.Mine | MapFeatures.Road);
        var disposed = PlayerPillage(abandoned).Begin(new(0)); disposed.Resume(); disposed.Dispose();
        Throws<ObjectDisposedException>(() => disposed.Resume(0)); Equal((byte)0, abandoned.Data.Unit(0).MovesUsed);
    }
}
