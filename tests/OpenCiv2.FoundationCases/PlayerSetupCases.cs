using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static OwnedList<byte> SetupText(string text) => new(ByteText(text));
    private static SetupNames SetupTextNames(string leader, string plural, string adjective) => new(SetupText(leader), SetupText(plural), SetupText(adjective));
    private static Choice PlayerChoice(string site, int ordinal, int modulus, int code) => Choice.Create(new("F05.setup." + site, "wizard/" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)), new SamplingLaw.PrimaryModulo(modulus), ChoiceCode.Primary(code));
    private static GameplayRandom PlayerChoices(params Choice[] choices) => new(new ControlledChoices(new(choices)), "setup");
    private static GameplayRandom PlayerSlots(params int[] codes) => PlayerChoices(codes.Select((code, i) => PlayerChoice("opponent-slot", i, 8, code)).ToArray());
    private sealed class PlayerFixture : IPlayerSetupInteraction, IDisposable
    {
        public readonly SessionRuntime Runtime = new(new(), new(98));
        public List<string> Visits = [];
        public Action<string>? Observe;
        public Func<Difficulty, int> Difficulty = _ => 2;
        public Func<short, SetupNumericChoice> Opponents = _ => new SetupNumericChoice.Selected(4), Barbarians = _ => new SetupNumericChoice.Selected(1);
        public Func<RuleSelection, int> Rules = _ => 0;
        public Func<Gender, int> Gender = _ => 1;
        public Func<SetupAdvancedChoices, SetupAdvancedResponse> Advanced = _ => throw new Exception("Unexpected advanced rules");
        public Func<AccelerationYears, short, int> Acceleration = (_, _) => throw new Exception("Unexpected acceleration");
        public Func<OwnedList<SetupTribeOption>, short, SetupTribeChoice> Tribe = (options, _) => { Equal(21, options.Count); Equal(new SetupTribeOption(3, SetupText("Tribe3"), 4), options[3]); return new(3); };
        public Func<bool, SetupNames, SetupNameResponse> Names = (_, current) => new(SetupNameAction.Accept, current);
        public Func<GovernmentTitles, SetupTitleResponse> Titles = _ => throw new Exception("Unexpected title editor");
        public Action<CivilizationSlot> Portrait = _ => throw new Exception("Unexpected portrait editor");
        public Func<short, short, int> Style = (_, _) => 2;
        public Func<CivilizationSlot, OwnedList<SetupTribeOption>, SetupNumericChoice> OpponentTribe = (_, _) => new SetupNumericChoice.Random();
        public PlayerFixture()
        {
            Runtime.Process.OrdinaryPreferences.SetDefaults(0, false); var rules = Runtime.Process.Rules; rules.Text.Initialize();
            NameReference Label(string text) => new(rules.Text.Store(ByteText(text)));
            for (int i = 0; i < 21; ++i)
            {
                var leader = rules.Leaders[i]; string n = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                leader.Names[0] = Label("Male" + n); leader.Names[1] = Label("Female" + n); leader.LeaderName = leader.Names[0]; leader.Plural = Label("Tribe" + n); leader.Adjective = Label("Adjective" + n); leader.Color = (short)(i % 7 + 1); leader.CityStyle = (short)(i % 4); leader.TimesUsed = 9; leader.CityNameIndex = 11;
                for (int g = 0; g < 7; ++g) { leader.Titles[g][0] = Label("M" + g + "/" + n); leader.Titles[g][1] = Label("F" + g + "/" + n); }
            }
        }
        private T Visit<T>(string name, Func<T> call) { Visits.Add(name); Observe?.Invoke(name); return call(); }
        public int ChooseDifficulty(Difficulty remembered) => Visit("difficulty", () => Difficulty(remembered));
        public SetupNumericChoice ChooseOpponents(short remembered) => Visit("opponents", () => Opponents(remembered));
        public SetupNumericChoice ChooseBarbarians(short remembered) => Visit("barbarians", () => Barbarians(remembered));
        public int ChooseRules(RuleSelection remembered) => Visit("rules", () => Rules(remembered));
        public int ChooseGender(Gender remembered) => Visit("gender", () => Gender(remembered));
        public SetupAdvancedResponse ChooseAdvanced(SetupAdvancedChoices defaults) => Visit("advanced", () => Advanced(defaults));
        public int ChooseAcceleration(AccelerationYears years, short remembered) => Visit("acceleration", () => Acceleration(years, remembered));
        public SetupTribeChoice ChooseTribe(OwnedList<SetupTribeOption> options, short remembered) => Visit("tribe", () => Tribe(options, remembered));
        public SetupNameResponse ChooseNames(bool custom, SetupNames current) => Visit(custom ? "custom-names" : "name", () => Names(custom, current));
        public SetupTitleResponse ChooseTitles(GovernmentTitles current) => Visit("titles", () => Titles(current));
        public void ChoosePortrait(CivilizationSlot civilization) => Visit("portrait", () => { Portrait(civilization); return true; });
        public int ChooseCityStyle(short tribeStyle, short currentOverride) => Visit("style", () => Style(tribeStyle, currentOverride));
        public SetupNumericChoice ChooseOpponentTribe(CivilizationSlot civ, OwnedList<SetupTribeOption> options) => Visit("opponent:" + civ.Value, () => OpponentTribe(civ, options));
        public void CustomRules(SetupAdvancedChoices checks) { Rules = _ => 1; Advanced = _ => new(-1, checks); }
        public void CustomTribe() => Tribe = (_, _) => new(3, true);
        public PlayerSetupResult Run(GameplayRandom random, WorldSource source = WorldSource.Generated) => PlayerSetup.Select(Runtime, random, "wizard", source, ClassicCalendar, this);
        public void Dispose() => Runtime.Dispose();
    }
    private static void PlayerSetupOrdinaryAndRestoredNames()
    {
        using var f = new PlayerFixture(); var w = f.Runtime.World; w.Globals.Turn = 71; w.Globals.Year = 1900; w.Globals.FirstLaunchTurn = 21; w.Scenario.StartYear = -100; w.Scenario.YearIncrement = -7; var scenario = w.Scenario; w.Civilizations[4].Flags = 0xabcd; w.Names[4].Tribe = new(Enumerable.Repeat((byte)255, 24)); w.Names[4].Leader = new(Enumerable.Repeat((byte)254, 24));
        var random = PlayerSlots(4, 0, 2, 2, 7); Equal(new PlayerSetupResult(true), f.Run(random)); random.RequireChoicesConsumed();
        Equal((ushort)0x3f, w.Globals.Rules); Equal(2, (int)w.Globals.Difficulty); Equal((byte)2, w.Globals.NumEnemies); Equal((byte)1, w.Globals.BarbarianActivity); Equal((sbyte)4, w.Globals.PlayerCiv); Equal((byte)0x10, w.Globals.HumanPlayers); Equal((byte)0x95, w.Globals.CivsAlive); Equal((short)71, w.Globals.Turn); Equal((short)1900, w.Globals.Year); Equal((short)21, w.Globals.FirstLaunchTurn); Equal(scenario, w.Scenario); Equal(4, w.Multiplayer.LocalHuman); Equal((short)3, w.Civilizations[4].Tribe); Equal((byte)24, w.Civilizations[4].LeaderPortrait); Equal((ushort)0xabcd, w.Civilizations[4].Flags); Equal((short)2, w.Names[4].CityStyle); Equal(SetupText("Female3"), w.Names[4].Leader); Equal(24, w.Names[4].Tribe.Count);
        var leader = f.Runtime.Process.Rules.Leaders[3]; Equal(leader.Names[1] with { Custom = true }, leader.LeaderName); Equal(true, leader.Female); Equal(false, leader.Plural.Custom); Equal((short)3, leader.CityStyle); Equal((byte)9, leader.TimesUsed); Equal((byte)11, leader.CityNameIndex);
        var p = f.Runtime.Process.OrdinaryPreferences; Equal(2, (int)p.Difficulty); Equal((short)4, p.Opponents); Equal((short)1, p.Barbarians); Equal(1, (int)p.Gender); Equal((short)3, p.Tribe); Equal(0, (int)p.RulesChoice); Equal(true, w.PendingLeaderOverrides.All(v => v == -1)); Equal(false, f.Runtime.Active);
        Equal(new OwnedList<string>(["difficulty", "opponents", "barbarians", "rules", "gender", "tribe", "name", "style"]), new(f.Visits));
        var globals = w.Globals; foreach (var l in f.Runtime.Process.Rules.Leaders) { l.Plural = l.Plural with { Custom = true }; l.Adjective = l.Adjective with { Custom = true }; foreach (var pair in l.Titles) for (int i = 0; i < 2; ++i) pair[i] = pair[i] with { Custom = true }; }
        f.Difficulty = _ => -42; Equal(false, f.Run(PlayerSlots()).Accepted); Equal(globals, w.Globals);
        foreach (var l in f.Runtime.Process.Rules.Leaders) { Equal(l.Names[l.Female ? 1 : 0], l.LeaderName); Equal(false, l.Plural.Custom); Equal(false, l.Adjective.Custom); Equal(true, l.Titles.All(pair => pair.All(n => !n.Custom))); }
    }
    private static void PlayerSetupCancellationRoutes()
    {
        foreach (string cancel in new[] { "opponents", "barbarians", "gender", "tribe", "style", "names" })
        {
            using var f = new PlayerFixture(); int difficulties = 0, genders = 0; f.Difficulty = remembered => { Equal(difficulties == 0 ? 0 : 5, (int)remembered); return ++difficulties == 1 ? 5 : -1; }; f.Gender = _ => ++genders == 1 && cancel != "gender" ? 1 : -1;
            if (cancel == "opponents") f.Opponents = _ => new SetupNumericChoice.Selected(-2); if (cancel == "barbarians") f.Barbarians = _ => new SetupNumericChoice.Selected(-3); if (cancel == "tribe") f.Tribe = (_, _) => new(-1); if (cancel == "style") f.Style = (_, _) => -1; if (cancel == "names") f.Names = (_, _) => new(SetupNameAction.Cancel, SetupTextNames("Discard", "Discard", "Discard"));
            var random = PlayerSlots(); Equal(false, f.Run(random).Accepted); random.RequireChoicesConsumed(); Equal(2, difficulties); Equal(5, (int)f.Runtime.World.Globals.Difficulty); Equal((byte)0, f.Runtime.World.Globals.CivsAlive); if (cancel != "opponents") Equal((short)4, f.Runtime.Process.OrdinaryPreferences.Opponents); if (cancel is "tribe" or "style" or "names") Equal(2, genders);
        }
        using var rules = new PlayerFixture(); rules.Rules = _ => -1; Equal(false, rules.Run(PlayerSlots()).Accepted); Equal((byte)1, rules.Runtime.World.Globals.BarbarianActivity); Equal((byte)0, rules.Runtime.World.Globals.HumanPlayers);
    }
    private static void PlayerSetupRandomBranchesAndRetries()
    {
        for (int level = 0; level < 6; ++level) foreach (int code in new[] { 0, 32767 })
        {
            using var f = new PlayerFixture(); f.Difficulty = _ => level; f.Opponents = _ => new SetupNumericChoice.Random(); f.Barbarians = _ => new SetupNumericChoice.Random(); f.Rules = _ => -1;
            var random = PlayerChoices(PlayerChoice("opponent-count", 0, 5, code), PlayerChoice("barbarians", 0, level is >= 2 and <= 4 ? 3 : 2, code)); Equal(false, f.Run(random).Accepted); random.RequireChoicesConsumed(); Equal((byte)(code % 5 + 2), f.Runtime.World.Globals.NumEnemies); int[] zero = [0, 1, 1, 1, 1, 2], maximum = [1, 2, 2, 2, 2, 3]; Equal((byte)(code == 0 ? zero[level] : maximum[level]), f.Runtime.World.Globals.BarbarianActivity); Equal((short)0, f.Runtime.Process.OrdinaryPreferences.Opponents); Equal((short)2, f.Runtime.Process.OrdinaryPreferences.Barbarians);
        }
        using var retry = new PlayerFixture(); int activities = 0; retry.Opponents = _ => new SetupNumericChoice.Random(); retry.Barbarians = _ => ++activities == 1 ? new SetupNumericChoice.Selected(-1) : new SetupNumericChoice.Random(); retry.Rules = _ => -1;
        var choices = PlayerChoices(PlayerChoice("opponent-count", 0, 5, 4), PlayerChoice("opponent-count", 1, 5, 5), PlayerChoice("barbarians", 0, 3, 2)); Equal(false, retry.Run(choices).Accepted); choices.RequireChoicesConsumed(); Equal((byte)2, retry.Runtime.World.Globals.NumEnemies); Equal((byte)3, retry.Runtime.World.Globals.BarbarianActivity);
    }
    private static void PlayerSetupAdvancedCombinations()
    {
        foreach (var source in new[] { WorldSource.Generated, WorldSource.PremadeCylindrical, WorldSource.PremadeFlat }) foreach (bool oldFlat in new[] { false, true }) for (int mask = 0; mask < 32; ++mask)
        {
            using var f = new PlayerFixture(); f.Runtime.Process.OrdinaryPreferences.Rules = (ushort)(oldFlat ? 0x819f : 0x19f); f.Runtime.Process.OrdinaryPreferences.AcceleratedStart = 2; var checks = new SetupAdvancedChoices((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, false, (mask & 8) != 0, (mask & 16) != 0); f.CustomRules(checks);
            f.Advanced = defaults => { Equal(new SetupAdvancedChoices(false, source == WorldSource.Generated ? oldFlat : source == WorldSource.PremadeFlat, false, true, true, true), defaults); return new(-1, checks); }; int levels = 0; f.Difficulty = _ => ++levels == 1 ? 2 : -1; f.Gender = _ => -1;
            var result = f.Run(PlayerSlots(), source); ushort active = (ushort)(0x2f | ((mask & 1) != 0 ? 0 : 0x10) | ((mask & 2) != 0 ? 0x8000 : 0) | ((mask & 8) != 0 ? 0x80 : 0) | ((mask & 16) != 0 ? 0x100 : 0));
            Equal(new PlayerSetupResult(false, (mask & 4) != 0), result); Equal(active, f.Runtime.World.Globals.Rules); Equal((ushort)(source == WorldSource.Generated ? active : (active & 0x7fff) | (oldFlat ? 0x8000 : 0)), f.Runtime.Process.OrdinaryPreferences.Rules); Equal((short)0, f.Runtime.Process.OrdinaryPreferences.AcceleratedStart);
        }
    }
    private static void PlayerSetupAccelerationRetention()
    {
        foreach (bool accepted in new[] { false, true })
        {
            using var f = new PlayerFixture(); f.Runtime.Process.OrdinaryPreferences.Rules = 0xbf; f.Runtime.Process.OrdinaryPreferences.AcceleratedStart = 2; f.Runtime.World.Globals.GameFlags = 4; f.Runtime.World.Globals.FirstLaunchTurn = 44; f.CustomRules(new(false, false, true, true, true)); int rules = 0, genders = 0;
            f.Rules = remembered => { Equal(rules == 0 ? 0 : 1, (int)remembered); return ++rules == 1 ? 1 : 0; }; f.Acceleration = (years, remembered) => { Equal(new AccelerationYears(4000, 3600, 3200), years); Equal((short)2, remembered); Equal((short)-1, f.Runtime.World.Globals.FirstLaunchTurn); return accepted ? 2 : -7; }; if (accepted) f.Gender = _ => ++genders == 1 ? -1 : 1;
            var random = PlayerSlots(4, 0, 2, 2, 7); Equal(new PlayerSetupResult(true, true, accepted ? 2 : -7), f.Run(random)); random.RequireChoicesConsumed(); Equal((short)2, f.Runtime.Process.OrdinaryPreferences.AcceleratedStart); Equal((ushort)0xbf, f.Runtime.Process.OrdinaryPreferences.Rules); Equal((ushort)0x3f, f.Runtime.World.Globals.Rules); Equal("opponent:2", f.Visits[^2]); Equal("opponent:7", f.Visits[^1]);
        }
        using var repeated = new PlayerFixture(); int advanced = 0; repeated.CustomRules(new()); repeated.Advanced = defaults => { Equal(false, defaults.PickOpponents); if (++advanced == 1) return new(0, new(false, true, true, true, true, true)); Equal(new SetupAdvancedChoices(false, true, false, false, true, true), defaults); return new(0, new(true)); }; repeated.Acceleration = (_, _) => -1; Equal(new PlayerSetupResult(true), repeated.Run(PlayerSlots(2, 7))); Equal((ushort)0x2f, repeated.Runtime.World.Globals.Rules); Equal(2, advanced);
    }
    private static void PlayerSetupNameChildren()
    {
        using var f = new PlayerFixture(); f.CustomTribe(); int names = 0, titles = 0;
        f.Names = (custom, current) =>
        {
            Equal(true, custom); var leader = f.Runtime.Process.Rules.Leaders[3]; Equal(true, leader.Plural.Custom); Equal(true, leader.Adjective.Custom); foreach (var pair in leader.Titles) { Equal(false, pair[0].Custom); Equal(true, pair[1].Custom); }
            if (++names == 1) { Equal(SetupTextNames("Female3", "Tribe3", "Adjective3"), current); return new(SetupNameAction.Titles, SetupTextNames("Probe", "People", "Descriptive")); }
            Equal(SetupTextNames("Probe", "People", "Descriptive"), current); return new(names == 2 ? SetupNameAction.Titles : names == 3 ? SetupNameAction.Portrait : SetupNameAction.Accept, current);
        };
        f.Titles = current => { Equal(SetupText("Probe"), f.Runtime.World.Names[4].Leader); Equal(SetupText("F0/3"), current.Values[0]); if (++titles == 1) return new(SetupTitleStatus.Cancel, new(Enumerable.Repeat(SetupText("Discard"), 7))); return new(SetupTitleStatus.Accept, new(new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six" }.Select(SetupText))); };
        f.Portrait = civ => { Equal(new CivilizationSlot(4), civ); Equal(SetupText("Six"), f.Runtime.World.Names[4].Titles[6]); f.Runtime.World.Civilizations[4].LeaderPortrait = 17; };
        Equal(true, f.Run(PlayerSlots(2, 7)).Accepted); Equal(4, names); Equal(2, titles); Equal((byte)17, f.Runtime.World.Civilizations[4].LeaderPortrait); Equal(SetupText("Zero"), f.Runtime.World.Names[4].Titles[0]);
    }
    private static void PlayerSetupNameCancellationAndUnavailable()
    {
        using (var f = new PlayerFixture())
        {
            f.CustomTribe(); int genders = 0, names = 0, difficulties = 0; f.Difficulty = _ => ++difficulties == 1 ? 2 : -1; f.Gender = _ => { if (++genders == 1) return 1; var leader = f.Runtime.Process.Rules.Leaders[3]; Equal(leader.Names[1], leader.LeaderName); Equal(false, leader.Plural.Custom); Equal(false, leader.Titles[3][1].Custom); Equal(SetupText("BeforeCancel"), f.Runtime.World.Names[4].Leader); return -1; };
            f.Names = (_, _) => ++names == 1 ? new(SetupNameAction.Titles, SetupTextNames("BeforeCancel", "People", "Descriptive")) : new(SetupNameAction.Cancel, SetupTextNames("Discard", "Discard", "Discard")); f.Titles = current => new(SetupTitleStatus.Cancel, current);
            Equal(false, f.Run(PlayerSlots()).Accepted); Equal(2, genders); Equal(SetupText("People"), f.Runtime.World.Names[4].Tribe); Equal((sbyte)4, f.Runtime.World.Globals.PlayerCiv);
        }
        foreach (bool titleFailure in new[] { false, true })
        {
            using var f = new PlayerFixture(); f.CustomTribe(); f.Names = (_, current) => new(titleFailure ? SetupNameAction.Titles : SetupNameAction.Unavailable, current with { Leader = SetupText("BeforeFailure") }); f.Titles = current => new(SetupTitleStatus.Unavailable, current);
            Equal(false, f.Run(PlayerSlots()).Accepted); Equal(SetupText(titleFailure ? "BeforeFailure" : "Female3"), f.Runtime.World.Names[4].Leader); Equal(true, f.Runtime.Process.Rules.Leaders[3].LeaderName.Custom); Equal((byte)0, f.Runtime.World.Globals.CivsAlive);
        }
    }
    private static void PlayerSetupNameBoundsAndPartialCopies()
    {
        using var f = new PlayerFixture(); f.CustomTribe(); var maximum = new OwnedList<byte>(Enumerable.Repeat((byte)255, 23)); f.Names = (_, current) => new(SetupNameAction.Accept, current with { Leader = maximum, Plural = new(Enumerable.Repeat((byte)'x', 24)) });
        Throws<ArgumentOutOfRangeException>(() => f.Run(PlayerSlots())); Equal(maximum, f.Runtime.World.Names[4].Leader); Equal(SetupText("Tribe3"), f.Runtime.World.Names[4].Tribe);
        f.Names = (_, current) => new(SetupNameAction.Accept, current with { Leader = new([65, 0, 66]) }); Throws<ArgumentOutOfRangeException>(() => f.Run(PlayerSlots())); Equal(SetupText("Female3"), f.Runtime.World.Names[4].Leader);
        f.Names = (_, current) => new(SetupNameAction.Titles, current); f.Titles = current => new(SetupTitleStatus.Accept, new(current.Values.Select((text, i) => i == 0 ? maximum : i == 1 ? new OwnedList<byte>([0]) : text)));
        Throws<ArgumentOutOfRangeException>(() => f.Run(PlayerSlots())); Equal(maximum, f.Runtime.World.Names[4].Titles[0]); Equal(SetupText("F1/3"), f.Runtime.World.Names[4].Titles[1]);
        // Initial copying also retains only the reached prefix, without marking the failed default.
        var leader = f.Runtime.Process.Rules.Leaders[3]; leader.Plural = new(f.Runtime.Process.Rules.Text.Store(new byte[24].Select(_ => (byte)'q').ToArray())); Throws<ArgumentOutOfRangeException>(() => f.Run(PlayerSlots())); Equal(true, leader.LeaderName.Custom); Equal(false, leader.Plural.Custom);
    }
    private static void PlayerSetupOpponentRetriesAndRoster()
    {
        using (var f = new PlayerFixture())
        {
            f.CustomRules(new(PickOpponents: true)); f.Runtime.World.PendingLeaderOverrides[6] = 12; int styles = 0, opponents = 0;
            f.Style = (preferred, current) => { Equal((short)3, preferred); if (++styles == 1) { Equal((short)0, current); return 3; } Equal((short)3, current); Equal((byte)0x95, f.Runtime.World.Globals.CivsAlive); Equal((sbyte)8, f.Runtime.World.PendingLeaderOverrides[2]); return 1; };
            f.OpponentTribe = (slot, options) => { Equal(3, options.Count); for (int i = 0; i < 3; ++i) { Equal((short)slot.Value, options[i].PreferredColor); Equal(slot.Value - 1 + 7 * i, options[i].Tribe); } ++opponents; Equal((byte)(opponents is 1 or 3 ? 2 : opponents == 2 ? 7 : 3), slot.Value); return opponents == 3 ? new SetupNumericChoice.Random() : new SetupNumericChoice.Selected(opponents == 1 ? 8 : opponents == 2 ? -1 : 9); };
            var random = PlayerSlots(2, 7, 0, 4, 2, 2, 3); Equal(true, f.Run(random).Accepted); random.RequireChoicesConsumed(); Equal(2, styles); Equal(4, opponents); Equal((byte)0x1d, f.Runtime.World.Globals.CivsAlive); Equal(new OwnedList<sbyte>([-1, -1, 8, 9, -1, -1, 12, -1]), new(f.Runtime.World.PendingLeaderOverrides));
        }
        using var full = new PlayerFixture(); full.Opponents = _ => new SetupNumericChoice.Selected(0); var draws = PlayerSlots(0, 4, 1, 1, 2, 3, 5, 6, 7); Equal(true, full.Run(draws).Accepted); draws.RequireChoicesConsumed(); Equal((byte)255, full.Runtime.World.Globals.CivsAlive);
        using var missing = new PlayerFixture(); var shortDraws = PlayerSlots(2, 2, 0); Throws<ChoiceException>(() => missing.Run(shortDraws)); shortDraws.RequireChoicesConsumed(); Equal((byte)0x15, missing.Runtime.World.Globals.CivsAlive); Equal((short)2, missing.Runtime.World.Names[4].CityStyle);
    }
    private static void PlayerSetupAdmissionAndPublication()
    {
        using (var f = new PlayerFixture()) { var globals = f.Runtime.World.Globals; Throws<ArgumentException>(() => PlayerSetup.Select(f.Runtime, PlayerSlots(), "", WorldSource.Generated, ClassicCalendar, f)); Equal(globals, f.Runtime.World.Globals); f.Barbarians = _ => new SetupNumericChoice.Selected(4); Throws<ArgumentOutOfRangeException>(() => f.Run(PlayerSlots())); Equal((short)4, f.Runtime.Process.OrdinaryPreferences.Opponents); Equal((byte)0, f.Runtime.World.Globals.BarbarianActivity); }
        using (var f = new PlayerFixture()) { f.Runtime.Process.Rules.Leaders[3].Color = 8; f.Tribe = (_, _) => new(3); Throws<ArgumentOutOfRangeException>(() => f.Run(PlayerSlots())); Equal((short)3, f.Runtime.Process.OrdinaryPreferences.Tribe); Equal((byte)0, f.Runtime.World.Globals.HumanPlayers); }
        using (var f = new PlayerFixture()) { f.CustomRules(new(PickOpponents: true)); f.OpponentTribe = (_, _) => new SetupNumericChoice.Selected(3); Throws<ArgumentOutOfRangeException>(() => f.Run(PlayerSlots(2, 7))); Equal((byte)0x95, f.Runtime.World.Globals.CivsAlive); Equal((sbyte)-1, f.Runtime.World.PendingLeaderOverrides[2]); }
        using var live = new PlayerFixture(); live.Runtime.BeginClientSession(new(10, 20)); var w = live.Runtime.World; w.Entities.Units.SetEnd(1); w.Entities.Units.Storage[0] = new() { Token = new(1), Owner = new(4) }; var client = SessionClient(live.Runtime, 4); ulong revision = client.OwnUnits().Observation.Revision; var game = live.Runtime.GameplayRandom.Capture(); var presentation = live.Runtime.PresentationRandom.Capture();
        live.Observe = _ => { Equal(true, client.OwnUnits().Observation.Revision > revision); revision = client.OwnUnits().Observation.Revision; Throws<InvalidOperationException>(live.Runtime.PrepareNewGame); }; live.Names = (_, current) => { w.Entities.Units.Storage[0].X = 123; return new(SetupNameAction.Accept, current); }; live.Style = (_, _) => { Equal((short)123, client.OwnUnits().Records[0].X); w.Entities.Units.Storage[0].X = 234; throw new IOException("interruption"); };
        Throws<IOException>(() => live.Run(PlayerSlots())); Equal((short)234, client.OwnUnits().Records[0].X); Equal(true, client.OwnUnits().Observation.Revision > revision); Equal(game, live.Runtime.GameplayRandom.Capture()); Equal(presentation, live.Runtime.PresentationRandom.Capture()); Equal(true, live.Runtime.Active); live.Runtime.PrepareNewGame();
    }
}
