using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static Choice HistoryChoice(string call = "start/one", int code = 149) => Choice.Create(new("F05.new-game.history", call), new SamplingLaw.PrimaryModulo(50), ChoiceCode.Primary(code));
    private sealed class NewGameFixture : IDisposable
    {
        public readonly SessionRuntime Runtime = new(new(), new(91));
        public GameplayRandom Random;
        public NewGameServices Services;
        public List<int> Calls = [];
        public NewGameFixture(Choice[]? choices = null)
        {
            Random = new(new ControlledChoices(new(choices ?? [HistoryChoice()])), "setup"); var world = Runtime.World;
            world.Globals = StateConversion.ToLive(ClassicRecords.DecodeStoredGlobals(Enumerable.Repeat((byte)0xa5, 330).ToArray())); world.Globals.Difficulty = Difficulty.Prince; world.Globals.PlayerCiv = 3; world.Globals.HumanPlayers = StateConversion.DecodeCivilizations(2); world.Globals.CivsAlive = StateConversion.DecodeCivilizations(0x23);
            world.Scenario = StateConversion.ToLive(ClassicRecords.DecodeScenarioParameters(Enumerable.Repeat((byte)0x71, 100).ToArray())); world.Eliminations = StateConversion.ToLive(ClassicRecords.DecodeEliminationHistory(Enumerable.Repeat((byte)0x72, 338).ToArray()));
            for (int slot = 0; slot < 8; ++slot) { world.UnitTotals[slot] = -1; world.Civilizations[slot] = StateConversion.ToLive(ClassicRecords.DecodeStoredCivilization(Enumerable.Repeat((byte)(0xa5 + slot), 1428).ToArray())); }
            world.MovementMode = (MovementMode)(-19); for (int i = 0; i < 150; ++i) for (int j = 0; j < 8; ++j) world.PowerHistory[i][j] = 123;
            foreach (var leader in Runtime.Process.Rules.Leaders) { leader.Attack = -12; leader.TimesUsed = 33; leader.CityNameIndex = 66; leader.Female = true; leader.Color = 7; leader.CityStyle = 3; leader.LeaderName = leader.Plural = leader.Adjective = new(NameText("Retained 70000"), true); }
            Runtime.Process.OrdinaryPreferences.GameOptions = StateConversion.DecodeGameOptions(0x812387ff); Runtime.Process.OrdinaryPreferences.DisabledMessages = StateConversion.DecodeMessageSuppression(0xf123); Runtime.Process.OrdinaryPreferences.Science = 5; Runtime.Process.OrdinaryPreferences.Tax = 4;
            Services = new(slot => { Calls.Add(slot); return -1; }, () => Calls.Add(8));
        }
        public void Run(string call = "start/one") => NewGameInitialization.Initialize(Runtime, Random, call, Services);
        public void Dispose() => Runtime.Dispose();
    }
    private static void NewGameResetAndRetainedRecords()
    {
        using var f = new NewGameFixture(); var w = f.Runtime.World; var globals = w.Globals; var preferences = f.Runtime.Process.OrdinaryPreferences; var leaders = f.Runtime.Process.Rules.Leaders.Select(l => l.Snapshot()).ToArray();
        var civBytes = w.Civilizations.Select(c => ClassicRecords.Encode(StateConversion.ToStored(c))).ToArray();
        f.Run(); f.Random.RequireChoicesConsumed(); Equal(new OwnedList<int>(Enumerable.Range(0, 9)), new(f.Calls)); Equal(false, f.Runtime.Active);
        Equal(new OpenCiv2.Core.State.ScenarioParameters { TechnologyParadigm = 10 }, w.Scenario); Equal((short)0, w.Eliminations.Count); Equal(default(EliminationArray<short>), w.Eliminations.Turns); Equal(default(EliminationArray<byte>), w.Eliminations.Killers);
        for (int i = 0; i < 12; ++i) { Equal((byte)0x72, w.Eliminations.Portraits[i]); Equal(0, w.Eliminations.Names[i].Length); }
        Equal(default(CivilizationArray<int>), w.UnitTotals);
        for (int i = 0; i < 21; ++i) Equal(leaders[i] with { TimesUsed = 0, CityNameIndex = 0 }, f.Runtime.Process.Rules.Leaders[i].Snapshot());
        Equal((short)0, w.Globals.Turn); Equal((short)-4000, w.Globals.Year); Equal((short)-1, w.Globals.FirstLaunchTurn); Equal(StateConversion.DecodeGameOptions(0x812306ffU), w.Globals.GameOptions); Equal(StateConversion.DecodeMessageSuppression((ushort)0xf123), w.Globals.MessageOptions); Equal(StateConversion.DecodeTurnStatus((ushort)0), w.Globals.TransientFlags); Equal(StateConversion.DecodeTutorialProgress((ushort)0), w.Globals.ShownEventFlags); Equal((int)MovementMode.MoveUnits, (int)w.MovementMode); Equal(new UnitSlot(0), w.Globals.ActiveUnit); Equal((sbyte)0, w.Globals.TurnCiv); Equal((sbyte)-1, w.Globals.ViewCiv); Equal((byte)0, w.Globals.RevealMap);
        Equal((sbyte)0, w.Globals.WarmingCounter); Equal((sbyte)0, w.Globals.GlobalWarmings); Equal((short)0, w.Globals.WarmingPollution); Equal((short)0, w.Globals.PollutedTiles); Equal(default(AdvanceArray<byte>), w.Globals.FirstDiscoverer); Equal(default(AdvanceArray<CivilizationSet>), w.Globals.AdvanceKnownBy);
        for (int i = 0; i < 28; ++i) Equal((short)-1, w.Globals.WonderCity[i]); Equal((short)49, w.Globals.NextHistoryTurn); Equal(default(PowerHistoryArray<CivilizationArray<byte>>), w.PowerHistory); Equal(StateConversion.DecodeCivilizations((byte)0x23), w.Globals.CivsAtStart);
        // F06 independent byte extents expose retention outside the reset fields; comparison starts
        // from semantic encoding, so no original ABI padding becomes a live-state requirement.
        for (int slot = 0; slot < 8; ++slot)
        {
            byte[] expected = civBytes[slot]; foreach (var (start, end) in new[] { (0xa, 0xc), (0x66, 0x74), (0x192, 0x312), (0x400, 0x401), (0x402, 0x414) }) Array.Clear(expected, start, end - start);
            if (slot == 3) { expected[0x13] = 5; expected[0x14] = 4; }
            Equal(new OwnedList<byte>(expected), new(ClassicRecords.Encode(StateConversion.ToStored(w.Civilizations[slot]))));
        }
        Equal(globals.Rules, w.Globals.Rules); Equal(globals.Status, w.Globals.Status); Equal(globals.MovingUnit, w.Globals.MovingUnit); Equal((int)globals.Multiplayer, (int)w.Globals.Multiplayer); Equal(globals.SavedCurrentCiv, w.Globals.SavedCurrentCiv); Equal(globals.PeaceTurns, w.Globals.PeaceTurns); Equal(globals.NumAdvances, w.Globals.NumAdvances); Equal(globals.PowerRank, w.Globals.PowerRank); Equal(globals.CivByRank, w.Globals.CivByRank); Equal(preferences, f.Runtime.Process.OrdinaryPreferences);
    }
    private static void NewGameBoundsMapAndIdentityRetention()
    {
        using var f = new NewGameFixture([HistoryChoice(), HistoryChoice("start/two", 51)]); var w = f.Runtime.World; var unit = new OpenCiv2.Core.State.Unit { Token = new(123), Damage = 91 }; var city = new OpenCiv2.Core.State.City { Token = new(456), Size = 17 };
        w.Entities.Units.Storage.Fill(unit); w.Entities.Cities.Storage.Fill(city); w.Entities.Units.SetEnd(2048); w.Entities.Cities.SetEnd(256); f.Runtime.Process.Tokens.NextUnit = 765; f.Runtime.Process.Tokens.NextCity = 876; w.UnitStacks.Suppression = 77; w.UnitDeletion.Active = true; w.TurnLoopActive = true; w.Multiplayer.Saved.ResumeEvent = -7;
        w.Map.Allocate(new(10, 6)); var tile = StateConversion.ToLive(ClassicRecords.DecodeTile([0x73, 0x73, 0x73, 0x73, 0x73, 0x73])); w.Map.Tiles.Fill(tile); w.Map.Remembered(3, new(2, 2)) = MapFeatures.Irrigation | MapFeatures.Pollution; w.Map.CoarseGrid(0)[0] = 0xa7; f.Runtime.Process.Fallback.Value = tile;
        f.Run(); Equal(0, w.Entities.Units.End); Equal(0, w.Entities.Cities.End); Equal((short)0, w.Globals.UnitEnd); Equal((short)0, w.Globals.CityEnd); Equal(true, w.Entities.Units.Storage.ToArray().All(v => v == unit)); Equal(true, w.Entities.Cities.Storage.ToArray().All(v => v == city)); Equal(765U, f.Runtime.Process.Tokens.NextUnit); Equal(876U, f.Runtime.Process.Tokens.NextCity); Equal(77, w.UnitStacks.Suppression); Equal(true, w.UnitDeletion.Active); Equal(true, w.TurnLoopActive); Equal((sbyte)-7, w.Multiplayer.Saved.ResumeEvent);
        Equal(10, w.Map.Geometry.Width); Equal(true, w.Map.Tiles.ToArray().All(t => t == tile)); Equal(MapFeatures.Irrigation | MapFeatures.Pollution, w.Map.Remembered(3, new(2, 2))); Equal((byte)0xa7, w.Map.CoarseGrid(0)[0]!.Value); Equal(tile, f.Runtime.Process.Fallback.Value);
        w.Civilizations[3].FutureTechs = 19; w.Globals.PeaceTurns = 87; f.Runtime.Process.Rules.Leaders[3].CityNameIndex = 12; f.Run("start/two"); Equal((short)1, w.Globals.NextHistoryTurn); Equal((byte)19, w.Civilizations[3].FutureTechs); Equal((short)87, w.Globals.PeaceTurns); Equal((byte)0, f.Runtime.Process.Rules.Leaders[3].CityNameIndex); f.Random.RequireChoicesConsumed();
    }
    private static void NewGameLiveChildOrder()
    {
        using var f = new NewGameFixture(); var w = f.Runtime.World;
        f.Services = new(slot => { Equal((short)(slot == 0 ? 0 : 7), w.Globals.Turn); Equal((short)49, w.Globals.NextHistoryTurn); Equal(default(PowerHistoryArray<CivilizationArray<byte>>), w.PowerHistory); Equal(StateConversion.DecodeCivilizations((byte)0xa5), w.Globals.CivsAtStart); Equal((byte)0xa8, w.Civilizations[3].ScienceRate); w.Globals.Turn = 7; w.Globals.CivsAlive = StateConversion.DecodeCivilizations((byte)(1 << slot)); w.Civilizations[slot].Gold = slot + 100; f.Calls.Add(slot); return slot % 2 == 0 ? -1 : 0; }, () => { Equal(StateConversion.DecodeCivilizations((byte)0x80), w.Globals.CivsAtStart); Equal(107, w.Civilizations[7].Gold); w.Globals.CivsAlive = StateConversion.DecodeCivilizations(3); w.Globals.HumanPlayers = StateConversion.DecodeCivilizations(0x50); w.Globals.Difficulty = Difficulty.Chieftain; f.Runtime.Process.OrdinaryPreferences.Science = 7; f.Runtime.Process.OrdinaryPreferences.Tax = -100; f.Calls.Add(8); });
        f.Run(); Equal(new OwnedList<int>(Enumerable.Range(0, 9)), new(f.Calls)); Equal(StateConversion.DecodeCivilizations((byte)0x80), w.Globals.CivsAtStart); Equal(StateConversion.DecodeCivilizations((byte)3), w.Globals.CivsAlive); Equal((short)7, w.Globals.Turn); Equal((short)6, f.Runtime.Process.OrdinaryPreferences.Science); Equal((short)4, f.Runtime.Process.OrdinaryPreferences.Tax); Equal((byte)6, w.Civilizations[4].ScienceRate); Equal((byte)4, w.Civilizations[6].TaxRate); Equal(50, w.Civilizations[4].Gold); Equal(50, w.Civilizations[6].Gold); Equal(103, w.Civilizations[3].Gold); Equal((byte)0xa8, w.Civilizations[3].ScienceRate);
    }
    private static void NewGameOptionsRatesAndMasks()
    {
        foreach (int difficulty in new[] { 0, 1, 5, 255 }) foreach (uint options in new uint[] { 0, 0x100, 0x200, uint.MaxValue })
        { using var f = new NewGameFixture(); f.Runtime.World.Globals.Difficulty = (Difficulty)difficulty; f.Runtime.Process.OrdinaryPreferences.GameOptions = StateConversion.DecodeGameOptions(options); f.Run(); Equal(StateConversion.DecodeGameOptions(difficulty == 0 ? (options & 0xffff7fff) | 0x300 : options & 0xffff7eff), f.Runtime.World.Globals.GameOptions); Equal(StateConversion.DecodeGameOptions(options), f.Runtime.Process.OrdinaryPreferences.GameOptions); }
        foreach (var (science, tax, reset) in new (short, short, bool)[] { (6, 4, false), (5, 6, true), (7, -100, true), (-100, 7, true), (-1, -257, false), (short.MinValue, 6, false), (short.MaxValue, short.MaxValue, true) })
        {
            using var f = new NewGameFixture(); f.Runtime.Process.OrdinaryPreferences.Science = science; f.Runtime.Process.OrdinaryPreferences.Tax = tax; f.Run(); Equal(reset ? (short)6 : science, f.Runtime.Process.OrdinaryPreferences.Science); Equal(reset ? (short)4 : tax, f.Runtime.Process.OrdinaryPreferences.Tax); Equal(unchecked((byte)(reset ? 6 : science)), f.Runtime.World.Civilizations[3].ScienceRate); Equal(unchecked((byte)(reset ? 4 : tax)), f.Runtime.World.Civilizations[3].TaxRate);
        }
        foreach (byte mask in new byte[] { 0, 1, 2, 3, 0x82, 0xff })
        {
            using var f = new NewGameFixture(); var w = f.Runtime.World; w.Globals.HumanPlayers = StateConversion.DecodeCivilizations(mask); w.Globals.Difficulty = Difficulty.Chieftain; f.Run(); bool single = mask is 1 or 2;
            for (int slot = 0; slot < 8; ++slot) { bool human = slot != 0 && (mask & (1 << slot)) != 0; Equal((byte)(single && slot == 3 ? 5 : !single && human ? 6 : 0xa5 + slot), w.Civilizations[slot].ScienceRate); Equal(human, w.Civilizations[slot].Gold == 50); }
        }
    }
    private static void NewGameFailurePrefixes()
    {
        using (var f = new NewGameFixture([])) { var history = f.Runtime.World.PowerHistory; Throws<ChoiceException>(() => f.Run()); Equal((short)0, f.Runtime.World.Globals.Turn); Equal((short)-23131, f.Runtime.World.Globals.NextHistoryTurn); Equal(default(Fixed6<short>), f.Runtime.World.Civilizations[7].SpaceshipParts); Equal(history, f.Runtime.World.PowerHistory); Equal(StateConversion.DecodeCivilizations((byte)0xa5), f.Runtime.World.Globals.CivsAtStart); Equal(0, f.Calls.Count); }
        foreach (int stop in new[] { 0, 4, 7, 8 })
        {
            using var f = new NewGameFixture(); var w = f.Runtime.World; f.Runtime.BeginClientSession(new()); var client = SessionClient(f.Runtime); ulong revision = 0;
            f.Services = new(slot => { revision = client.OwnUnits().Observation.Revision; f.Calls.Add(slot); w.Civilizations[slot].Gold = slot; if (stop == slot) throw new IOException("create"); return -1; }, () => { if (stop == 8) throw new IOException("balance"); });
            Throws<IOException>(() => f.Run()); Equal(Math.Min(stop + 1, 8), f.Calls.Count); Equal(true, client.OwnUnits().Observation.Revision > revision); Equal(default(PowerHistoryArray<CivilizationArray<byte>>), w.PowerHistory); Equal(StateConversion.DecodeCivilizations((byte)(stop == 8 ? 0x23 : 0xa5)), w.Globals.CivsAtStart); Equal((byte)0xa8, w.Civilizations[3].ScienceRate); f.Random.RequireChoicesConsumed(); f.Runtime.PrepareNewGame();
        }
        foreach (sbyte player in new sbyte[] { -128, -1, 8, 127 })
        { using var f = new NewGameFixture(); f.Runtime.World.Globals.PlayerCiv = player; f.Runtime.Process.OrdinaryPreferences.Science = 7; Throws<ArgumentOutOfRangeException>(() => f.Run()); Equal(8, f.Calls[^1]); Equal((short)6, f.Runtime.Process.OrdinaryPreferences.Science); Equal((short)4, f.Runtime.Process.OrdinaryPreferences.Tax); Equal((byte)0xa8, f.Runtime.World.Civilizations[3].ScienceRate); }
        using var multiple = new NewGameFixture(); multiple.Runtime.World.Globals.PlayerCiv = -1; multiple.Runtime.World.Globals.HumanPlayers = StateConversion.DecodeCivilizations(6); multiple.Run();
    }
    private static void NewGamePublicationRandomAndAdmission()
    {
        var records = new[] { HistoryChoice() }.Concat(Enumerable.Range(0, 8).Select(i => Choice.Create(new("fixture.creation", i.ToString(System.Globalization.CultureInfo.InvariantCulture)), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(i)))).ToArray();
        using var f = new NewGameFixture(records); var w = f.Runtime.World; var before = w.Globals; var random = f.Random.Capture(); Throws<ArgumentException>(() => f.Run(""));
        foreach (var services in new NewGameServices[] { new(null!, () => { }), new(_ => 0, null!) }) Throws<ArgumentNullException>(() => NewGameInitialization.Initialize(f.Runtime, f.Random, "start/one", services));
        Equal(before, w.Globals); Equal(random, f.Random.Capture()); f.Runtime.BeginClientSession(new(11, 22)); var client = SessionClient(f.Runtime); var identity = client.Identity; ulong revision = client.OwnUnits().Observation.Revision; var game = f.Runtime.GameplayRandom.Capture(); var presentation = f.Runtime.PresentationRandom.Capture();
        w.Entities.Units.SetEnd(1); w.Entities.Units.Storage[0] = new() { Token = new(1), Owner = new(1) };
        f.Services = new(slot => { Equal(slot, f.Random.PrimaryCode(new ChoiceKey("fixture.creation", slot.ToString(System.Globalization.CultureInfo.InvariantCulture)))); var snapshot = client.OwnUnits(); Equal(true, snapshot.Observation.Revision > revision); revision = snapshot.Observation.Revision; if (slot == 0) { Equal(0, snapshot.Records.Count); w.Entities.Units.SetEnd(1); } else Equal((short)(slot - 1), snapshot.Records[0].Position.X); w.Entities.Units.Storage[0].Position = w.Entities.Units.Storage[0].Position with { X = (short)slot }; Throws<InvalidOperationException>(f.Runtime.PrepareNewGame); return 0; }, () => { Equal((short)7, client.OwnUnits().Records[0].Position.X); w.Entities.Units.Storage[0].Position = w.Entities.Units.Storage[0].Position with { X = 12 }; });
        f.Run(); Equal((short)12, client.OwnUnits().Records[0].Position.X); Equal(true, client.OwnUnits().Observation.Revision > revision); Equal(identity, client.Identity); Equal(game, f.Runtime.GameplayRandom.Capture()); Equal(presentation, f.Runtime.PresentationRandom.Capture()); f.Random.RequireChoicesConsumed(); Equal(new OwnedList<Choice>(records), f.Random.Capture().Trace!.Events);
    }
}
