using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    /// <summary>Independent direct-client sequence for the native rate/persistence scenario.</summary>
    public static (byte[] Initial, byte[] Saved, byte[] Continued, byte[] Preferences) LocalRateReference()
    {
        var preferences = new BufferedPreferenceWriter();
        using var game = new LocalGame(null, preferences);
        var founder = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
        RunLocal(game, false, "found", LocalPlayCommands.Found,
            new("unit", new CommandValue.Unit(new(founder.Slot, founder.Token))), new("name", new CommandValue.Text("Rates")));
        byte[] initial = game.SaveClassic();
        // Import once, just as the native test does before issuing the command.
        Equal(LocalLoadResult.Loaded, game.LoadClassic(initial));
        RunLocal(game, false, "rates", LocalPlayCommands.Rates,
            new("tax", new CommandValue.Integer(6)), new("science", new CommandValue.Integer(4)));
        byte[] saved = game.SaveClassic();
        Equal(LocalLoadResult.Loaded, game.LoadClassic(saved));
        EndLocalTurn(game, false, 2);
        return (initial, saved, game.SaveClassic(), preferences.Capture());
    }

    private static CivilizationRates RatesFor(RevelationFixture f) => new(f.World, f.Process, f.Stacks);

    private static void RateNormalizationAndBalancing()
    {
        var f = WorkingFixture(); var rates = RatesFor(f); ref var civ = ref f.World.Civilizations[1];
        civ.Government = Government.Despotism;
        foreach (var (tax, science, expected) in new[]
        {
            (255, 255, new RateValues(6, 6, -2)), (0, 255, new(6, 6, -2)),
            (0, 0, new(4, 0, 6)), (4, 4, new(4, 4, 2)), (9, 0, new(6, 3, 1))
        })
        {
            civ.TaxRate = (byte)tax; civ.ScienceRate = (byte)science;
            f.World.Entities.Cities.Storage[0].Tax = 111;
            var normalized = rates.Prepare(1);
            Equal(expected, normalized.Selected); Equal(expected, normalized.Preferred);
            Equal((byte)expected.Tax, civ.TaxRate); Equal((byte)expected.Science, civ.ScienceRate);
            Equal(tax != expected.Tax || science != expected.Science, normalized.LimitWarning);
            Equal((short)111, f.World.Entities.Cities.Storage[0].Tax); // No implicit city calculation.
        }
        civ.TaxRate = civ.ScienceRate = 4;
        foreach (var (holds, expected) in new[]
        {
            (new RateHolds(0, 0, 0), new RateValues(6, 2, 2)),
            (new(0, 1, 0), new(6, 4, 0)), (new(0, 1, -2), new(4, 4, 2)),
            (new(5, 0, 0), new(6, 2, 2))
        })
        {
            var draft = rates.Prepare(1, holds); draft.Adjust(RateKind.Tax, 6);
            Equal(expected, draft.Selected); Equal((byte)4, civ.TaxRate);
        }
        var selected = rates.Prepare(1);
        selected.Adjust(RateKind.Tax, 6); selected.Adjust(RateKind.Tax, 4);
        Equal(new RateValues(4, 4, 2), selected.Selected); // Initial preference survives adjustments.
        selected.Adjust(RateKind.Tax, -8); Equal(new RateValues(0, 4, 6), selected.Selected);
        Equal(false, selected.LimitWarning);
        selected.Adjust(RateKind.Science, 99); Equal(true, selected.LimitWarning);
        Equal(new RateValues(0, 6, 4), selected.Selected);
        selected.Adjust(RateKind.Luxury, 6); Equal(new RateValues(0, 4, 6), selected.Selected);
        selected.Hold(RateKind.Science, 7); Equal(new RateHolds(0, 7, 0), selected.Holds);
        selected.Hold(RateKind.Tax, -3); Equal(new RateHolds(-3, 0, 0), selected.Holds);
        selected.Hold(RateKind.Science, 0); Equal(new RateHolds(-3, 0, 0), selected.Holds);
        Throws<ArgumentOutOfRangeException>(() => selected.Adjust((RateKind)(-1), 1));
        Throws<ArgumentOutOfRangeException>(() => selected.Hold((RateKind)3, 1));
        Equal(false, CivilizationRates.Allowed(f.World, 1, int.MaxValue, int.MinValue));
        Equal(false, CivilizationRates.Allowed(f.World, 1, 6, 6));
        Equal(false, CivilizationRates.Allowed(f.World, 1, 0, 0));
        Equal(true, CivilizationRates.Allowed(f.World, 1, 4, 0));
    }

    private static void RateCapDecisionOrder()
    {
        var f = WorkingFixture(); var rates = RatesFor(f); ref var civ = ref f.World.Civilizations[1];
        civ.Government = Government.Despotism; civ.TaxRate = 9; civ.ScienceRate = 0;
        int calls = 0;
        rates.EnforceMaximum(1, owner =>
        {
            Equal(1, owner); ++calls;
            f.World.Civilizations[owner].Government = Government.Democracy;
            f.World.Civilizations[owner].TaxRate = 0;
            f.World.Civilizations[owner].ScienceRate = (byte)(calls == 1 ? 0 : 8);
        });
        Equal(3, calls); // The opening cap6 persists, with live reads between actual decisions.
        civ.TaxRate = civ.ScienceRate = 6; civ.Government = Government.Despotism;
        rates.EnforceMaximum(1, _ => throw new Exception("Negative luxury does not exceed the cap"));
        f.World.Civilizations[2].TaxRate = 255;
        rates.EnforceMaximum(2, _ => throw new Exception("AI does not open player rates"));
        f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => rates.Prepare(1));
        Equal((byte)6, civ.TaxRate);
    }

    private static void RatePreviewRealDistribution()
    {
        var f = WorkingFixture(); var rates = RatesFor(f); ref var civ = ref f.World.Civilizations[1];
        civ.TaxRate = 6; civ.ScienceRate = 4; var draft = rates.Prepare(1);
        civ.TaxRate = civ.ScienceRate = 3; civ.Gold = 500;
        f.AddCity(1, 1, new(12, 6)); f.AddCity(2, 2, new(16, 6));
        f.World.Entities.Cities.Storage[0].Trade = 20;
        f.World.Entities.Cities.Storage[1].Trade = 10;
        f.World.Entities.Cities.Storage[1].Flags.Disorder = true;
        f.World.Entities.Cities.Storage[2].Tax = 777;
        f.Rules.Improvements[BuildingIds.Temple] = f.Rules.Improvements[BuildingIds.Temple] with { Upkeep = 2 };
        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Temple, true);
        BuildingQueries.SetBuilding(f.World, new(1), BuildingIds.Temple, true);
        Equal(new RatePreview(12, 4, 8), rates.Preview(1, draft));
        Equal((short)6, f.World.Entities.Cities.Storage[1].Tax);
        Equal((short)4, f.World.Entities.Cities.Storage[1].Science);
        Equal((short)777, f.World.Entities.Cities.Storage[2].Tax);
        Equal((byte)3, civ.TaxRate); Equal((byte)3, civ.ScienceRate); Equal(500, civ.Gold);
        civ.ResearchProgress = 32000;
        var report = rates.Report(1, new(12, 4, 0));
        Equal(1, report.Preview.Science);
        Equal(Math.Max(1, Research.Cost(f.World, f.Rules, 1)), report.DiscoveryTurns);

        BuildingQueries.SetBuilding(f.World, new(0), BuildingIds.Barracks, true);
        f.Rules.Advances[AdvanceIds.MobileWarfare] = f.Rules.Advances[AdvanceIds.MobileWarfare] with
        { Enabled = false, Prerequisites = new([(AdvanceReference)AdvanceIds.MobileWarfare, AdvanceReference.None]) };
        Throws<InvalidOperationException>(() => rates.Preview(1, draft));
        Equal((byte)6, civ.TaxRate); Equal((byte)4, civ.ScienceRate); // Real upkeep failure retains the prefix.
    }

    private static void RateAcceptanceAndPersistence()
    {
        var f = WorkingFixture(); var rates = RatesFor(f); ref var civ = ref f.World.Civilizations[1];
        civ.Government = Government.Despotism; civ.TaxRate = civ.ScienceRate = 3;
        f.World.Globals.Turn = 7; f.World.Globals.TransientFlags.AttitudeDirty = true;
        f.World.Entities.Cities.Storage[0].TotalFood = 199;
        f.AddCity(1, 2, new(12, 6)); f.World.Entities.Cities.Storage[1].TotalFood = 199;
        int writes = 0;
        Equal(false, rates.Accept(1, new(6, 4, 0), () =>
        {
            ++writes;
            Equal((short)6, f.Process.OrdinaryPreferences.Tax);
            Equal((short)4, f.Process.OrdinaryPreferences.Science);
            Equal((short)199, f.World.Entities.Cities.Storage[0].TotalFood);
            return false;
        }));
        Equal((byte)6, civ.TaxRate); Equal(false, f.World.Globals.TransientFlags.AttitudeDirty);
        Equal(true, f.World.Entities.Cities.Storage[0].TotalFood != 199);
        Equal((short)199, f.World.Entities.Cities.Storage[1].TotalFood);
        f.World.Entities.Cities.Storage[0].TotalFood = 199;
        Equal(true, rates.Accept(1, new(6, 4, 0), () => { ++writes; return true; }));
        Equal(2, writes); Equal((short)199, f.World.Entities.Cities.Storage[0].TotalFood);
        Equal(true, rates.Accept(1, new(262, 4, -256), () => true));
        Equal((byte)6, civ.TaxRate); Equal(true, f.World.Entities.Cities.Storage[0].TotalFood != 199);
        f.World.Globals.Turn = 11;
        Equal(true, rates.Accept(1, new(4, 6, 0), () => throw new Exception("Not a startup-rate save")));
        f.World.Globals.Turn = -1; civ.Government = Government.Monarchy;
        Equal(true, rates.Accept(1, new(6, 4, 0), () => throw new Exception("Not a startup government")));
        civ.Government = Government.Despotism;
        Throws<InvalidOperationException>(() => rates.Accept(1, new(4, 4, 2), () => throw new InvalidOperationException("output")));
        Equal((byte)4, civ.TaxRate); Equal((short)4, f.Process.OrdinaryPreferences.Tax);
    }

    private static void LocalPlayRatesAndPersistence()
    {
        byte[]? direct = null;
        foreach (bool console in new[] { false, true })
        {
            var preferences = new BufferedPreferenceWriter();
            using var game = new LocalGame(null, preferences);
            var founder = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
            RunLocal(game, console, $"city.found unit={founder.Slot.Value}:{founder.Token.Value} name=Rates", LocalPlayCommands.Found,
                new("unit", new CommandValue.Unit(new(founder.Slot, founder.Token))), new("name", new CommandValue.Text("Rates")));
            byte[] before = game.SaveClassic();
            var denied = SubmitLocal(game, console, "civilization.rates tax=6 science=6", LocalPlayCommands.Rates,
                new("tax", new CommandValue.Integer(6)), new("science", new CommandValue.Integer(6)));
            Equal(true, game.AdvanceOne()); Equal("rates_not_available", game.Player.Result(denied)!.Code);
            BytesEqual(before, game.SaveClassic()); Equal(0, preferences.Capture().Length);
            RunLocal(game, console, "civilization.rates tax=6 science=4", LocalPlayCommands.Rates,
                new("tax", new CommandValue.Integer(6)), new("science", new CommandValue.Integer(4)));
            var prefs = ClassicPreferences.DecodeOrdinary(preferences.Capture().AsSpan(0, ClassicPreferences.OrdinarySize));
            Equal((short)6, prefs.Tax); Equal((short)4, prefs.Science);
            Equal(LocalLoadResult.Loaded, game.LoadClassic(game.SaveClassic()));
            EndLocalTurn(game, console, 2);
            var saved = game.SaveClassic();
            Equal((byte)6, ClassicFiles.DecodeStoredSave(saved).Civilizations[1].TaxRate);
            Equal((byte)4, ClassicFiles.DecodeStoredSave(saved).Civilizations[1].ScienceRate);
            if (direct is null) direct = saved; else BytesEqual(direct, saved);
        }
        using var failed = new LocalGame(null, new FailedRatePreferences());
        var id = SubmitLocal(failed, false, "rates", LocalPlayCommands.Rates,
            new("tax", new CommandValue.Integer(6)), new("science", new CommandValue.Integer(4)));
        Equal(true, failed.AdvanceOne());
        Equal(CommandStatus.Failed, failed.Player.Result(id)!.Status);
        Equal("preferences_output_failed", failed.Player.Result(id)!.Code);
        Equal("civilization.rates", failed.Player.Result(id)!.Committed.Single().Name);
        Equal((byte)6, ClassicFiles.DecodeStoredSave(failed.SaveClassic()).Civilizations[1].TaxRate);

        var oldPreferences = new OpenCiv2.Core.State.ProcessContext();
        oldPreferences.OrdinaryPreferences.SetDefaults(1024, true);
        oldPreferences.MultiplayerPreferences.SetDefaults();
        oldPreferences.OrdinaryPreferences.Text = "Retain this";
        oldPreferences.OrdinaryPreferences.Opponents = 5;
        var retained = new BufferedPreferenceWriter();
        using var existing = new LocalGame(null, retained, PreferenceFiles.Capture(oldPreferences));
        foreach (int tax in new[] { 6, 5 })
        {
            Equal(LocalLoadResult.Loaded, existing.LoadClassic(existing.SaveClassic()));
            RunLocal(existing, false, "rates", LocalPlayCommands.Rates,
                new("tax", new CommandValue.Integer(tax)), new("science", new CommandValue.Integer(4)));
            var savedPreferences = ClassicPreferences.DecodeOrdinary(retained.Capture().AsSpan(0, ClassicPreferences.OrdinarySize));
            Equal("Retain this", StateConversion.ToLive(savedPreferences).Text); Equal((short)5, savedPreferences.Opponents);
            Equal((short)tax, savedPreferences.Tax);
        }
    }

    private sealed class FailedRatePreferences : IPreferenceWriter
    {
        public bool Write(PreferenceSnapshot values, Action? beforeIo = null) => false;
    }
}
