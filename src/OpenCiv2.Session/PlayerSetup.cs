using System.Globalization;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

/// <summary>F05 §4 player wizard. Accepted edits and per-site choice ordinals survive retries.
/// Does not save preferences, generate terrain, create civilizations or apply acceleration.</summary>
public static class PlayerSetup
{
    private const int MinimumOpponents = 2;
    private const int MaximumOpponents = EntityLimits.PlayerCivilizations - 1;

    private enum Step { Difficulty, Opponents, Barbarians, Rules, Gender, Tribe, Names, CityStyle }

    private static void RestoreNames(RuleState rules)
    {
        foreach (var leader in rules.Leaders)
        {
            leader.LeaderName = leader.Names[leader.Female ? 1 : 0];
            leader.Plural = leader.Plural with { Custom = false };
            leader.Adjective = leader.Adjective with { Custom = false };
            foreach (var pair in leader.Titles)
            {
                for (int i = 0; i < RuleLimits.LeaderGenders; ++i)
                {
                    pair[i] = pair[i] with { Custom = false };
                }
            }
        }
    }

    private static string Name(string text)
    {
        if (text.EnumerateRunes().Count() > 23 || text.Contains('\0'))
        {
            throw new ArgumentOutOfRangeException(nameof(text), "Setup names require at most 23 characters without NUL");
        }

        return text;
    }

    private static void Title(ref CivilizationNames names, int index, string text)
    {
        text = Name(text);
        var titles = names.Titles.ToArray();
        titles[index] = text;
        names.Titles = new(titles);
    }

    private static OwnedList<SetupTribeOption> Tribes(RuleState rules, short? color = null) =>
        new(rules.Leaders.Select((leader, i) => (leader, i))
        .Where(item => color is null || item.leader.Color == color)
            .Select(
                item => new SetupTribeOption(item.i, item.leader.Plural.Text, item.leader.Color)));

    private static void Row(int value, int maximum)
    {
        if (value > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Setup response is outside the specified choices");
        }
    }

    public static PlayerSetupResult Select(
        SessionRuntime runtime,
        GameplayRandom random,
        string call,
        WorldSource source,
        CalendarValues calendar,
        IPlayerSetupInteraction services)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(services);
        using var pin = runtime.PinWorld();
        var world = runtime.World;
        var rules = runtime.Process.Rules;
        ref var globals = ref world.Globals;
        ref var preferences = ref runtime.Process.OrdinaryPreferences;
        bool pickOpponents = false;
        int acceleration = 0;
        ulong[] draws = new ulong[3];
        void Publish()
        {
            if (runtime.Active)
            {
                runtime.Publish();
            }
        }
        T Invoke<T>(Func<T> callback)
        {
            Publish();
            return callback();
        }
        int Sample(int site, int modulus)
        {
            string name = site switch
            {
                0 => "F05.setup.opponent-count",
                1 => "F05.setup.barbarians",
                _ => "F05.setup.opponent-slot"
            };
            return random.PrimaryModulo(
                new ChoiceKey(name, call + "/" + (draws[site]++).ToString(CultureInfo.InvariantCulture)),
                modulus);
        }
        PlayerSetupResult Finish(bool accepted = false)
        {
            Publish();
            return new(accepted, pickOpponents, acceleration);
        }
        try
        {
            RestoreNames(rules);
            var step = Step.Difficulty;
            int gender = 0, selectedTribe = 0, human = 0;
            bool custom = false;
            for (; ; )
            {
                switch (step)
                {
                    case Step.Difficulty:
                        {
                            Publish();
                            int choice = services.ChooseDifficulty(preferences.Difficulty);
                            Row(choice, RuleLimits.Difficulties - 1);
                            if (choice < 0)
                            {
                                return Finish();
                            }

                            globals.Difficulty = preferences.Difficulty = (Difficulty)choice;
                            step = Step.Opponents;
                            break;
                        }
                    case Step.Opponents:
                        {
                            Publish();
                            var choice = services.ChooseOpponents(preferences.Opponents);
                            if (choice is SetupNumericChoice.Selected ordinary)
                            {
                                Row(ordinary.Row, MaximumOpponents - MinimumOpponents);
                                if (ordinary.Row < 0)
                                {
                                    step = Step.Difficulty;
                                    break;
                                }
                                globals.NumEnemies = (byte)(MaximumOpponents - ordinary.Row);
                                preferences.Opponents = (short)ordinary.Row;
                            }
                            else if (choice is SetupNumericChoice.Random)
                            {
                                globals.NumEnemies = (byte)(
                                    Sample(0, MaximumOpponents - MinimumOpponents + 1) + MinimumOpponents);
                            }
                            else
                            {
                                throw new ArgumentException("Missing opponent choice");
                            }

                            step = Step.Barbarians;
                            break;
                        }
                    case Step.Barbarians:
                        {
                            Publish();
                            var choice = services.ChooseBarbarians(preferences.Barbarians);
                            if (choice is SetupNumericChoice.Selected ordinary)
                            {
                                Row(ordinary.Row, 3);
                                if (ordinary.Row < 0)
                                {
                                    step = Step.Difficulty;
                                    break;
                                }
                                globals.BarbarianActivity = (byte)ordinary.Row;
                                preferences.Barbarians = (short)ordinary.Row;
                            }
                            else if (choice is SetupNumericChoice.Random)
                            {
                                int level = (int)globals.Difficulty;
                                Row(level, RuleLimits.Difficulties - 1);
                                globals.BarbarianActivity = unchecked((byte)(level < 2
                                    ? Sample(1, 2) + level
                                    : level < 5 ? Sample(1, 3) + 1 : Sample(1, 2) + 2));
                            }
                            else
                            {
                                throw new ArgumentException("Missing barbarian choice");
                            }

                            step = Step.Rules;
                            break;
                        }
                    case Step.Rules:
                        {
                            Publish();
                            int choice = services.ChooseRules(preferences.RulesChoice);
                            Row(choice, 1);
                            if (choice < 0)
                            {
                                return Finish();
                            }

                            preferences.RulesChoice = (RuleSelection)choice;
                            if (choice == 0)
                            {
                                ClassicConfigurationMetadata.ResetRules(runtime.Process, runtime.World, preferences: false);
                                globals.Rules = GameRules.Normal;
                                step = Step.Gender;
                                break;
                            }
                            var remembered = preferences.Rules;
                            bool flat = source == WorldSource.Generated
                                ? remembered.FlatWorld
                                : source == WorldSource.PremadeFlat;
                            var defaults = new SetupAdvancedChoices(
                                !remembered.NormalCombat,
                                flat,
                                false,
                                preferences.AcceleratedStart != 0,
                                remembered.Bloodlust,
                                remembered.NoRestart);
                            var checks = Invoke(() => services.ChooseAdvanced(defaults)).Checks;
                            ClassicConfigurationMetadata.ResetRules(runtime.Process, runtime.World, preferences: true);
                            globals.Rules = default;
                            if (!checks.SimplifiedCombat)
                            {
                                globals.Rules.NormalCombat = true;
                            }

                            if (checks.FlatWorld)
                            {
                                globals.Rules.FlatWorld = true;
                            }

                            pickOpponents = checks.PickOpponents;
                            acceleration = checks.AcceleratedStartup ? 1 : 0;
                            if (checks.Bloodlust)
                            {
                                globals.Rules.Bloodlust = true;
                            }

                            if (checks.NoRestart)
                            {
                                globals.Rules.NoRestart = true;
                            }

                            preferences.Rules = source == WorldSource.Generated
                                ? globals.Rules
                                : globals.Rules with { FlatWorld = remembered.FlatWorld };
                            if (acceleration != 0)
                            {
                                var years = SetupCalendar.PrepareAccelerationYears(runtime, calendar);
                                Publish();
                                acceleration = services.ChooseAcceleration(years, preferences.AcceleratedStart);
                                Row(acceleration, 2);
                                if (acceleration < 0)
                                {
                                    break;
                                }
                            }
                            preferences.AcceleratedStart = (short)acceleration;
                            step = Step.Gender;
                            break;
                        }
                    case Step.Gender:
                        RestoreNames(rules);
                        Publish();
                        gender = services.ChooseGender(preferences.Gender);
                        Row(gender, RuleLimits.LeaderGenders - 1);
                        if (gender < 0)
                        {
                            step = Step.Difficulty;
                            break;
                        }
                        preferences.Gender = (Gender)gender;
                        step = Step.Tribe;
                        break;
                    case Step.Tribe:
                        {
                            var choices = Tribes(rules);
                            Publish();
                            var choice = services.ChooseTribe(choices, preferences.Tribe);
                            Row(choice.Tribe, RuleLimits.Tribes - 1);
                            if (choice.Tribe < 0)
                            {
                                step = Step.Gender;
                                break;
                            }
                            selectedTribe = choice.Tribe;
                            custom = choice.Custom;
                            preferences.Tribe = (short)selectedTribe;
                            var leader = rules.Leaders[selectedTribe];
                            human = leader.Color;
                            if (human is < 1 or >= EntityLimits.Civilizations)
                            {
                                throw new ArgumentOutOfRangeException(
                                    nameof(services),
                                    "Setup human color has no civilization backing");
                            }

                            globals.PlayerCiv = (sbyte)human;
                            world.Multiplayer.LocalHuman = human;
                            globals.HumanPlayers = CivilizationSet.Only(human);
                            ref var civ = ref world.Civilizations[human];
                            civ.Tribe = (short)selectedTribe;
                            civ.LeaderPortrait = (byte)(selectedTribe + RuleLimits.Tribes * gender);
                            leader.Female = gender != 0;
                            leader.LeaderName = leader.Names[gender];
                            ref var names = ref world.Names[human];
                            names.Leader = Name(leader.LeaderName.Text);
                            leader.LeaderName = leader.LeaderName with { Custom = true };
                            if (custom)
                            {
                                names.Tribe = Name(leader.Plural.Text);
                                leader.Plural = leader.Plural with { Custom = true };
                                names.Adjective = Name(leader.Adjective.Text);
                                leader.Adjective = leader.Adjective with { Custom = true };
                                for (int g = 0; g < RuleLimits.Governments; ++g)
                                {
                                    Title(ref names, g, leader.Titles[g][gender].Text);
                                    leader.Titles[g][gender] = leader.Titles[g][gender] with { Custom = true };
                                }
                            }
                            step = Step.Names;
                            break;
                        }
                    case Step.Names:
                        {
                            ref var names = ref world.Names[human];
                            var current = new SetupNames(
                                names.Leader,
                                custom ? names.Tribe : string.Empty,
                                custom ? names.Adjective : string.Empty);
                            var response = Invoke(() => services.ChooseNames(custom, current));
                            if (response.Action == SetupNameAction.Unavailable)
                            {
                                return Finish();
                            }

                            if (response.Action == SetupNameAction.Cancel)
                            {
                                step = Step.Gender;
                                break;
                            }
                            if ((!custom && response.Action != SetupNameAction.Accept)
                                || response.Action is not (SetupNameAction.Accept or SetupNameAction.Titles
                                    or SetupNameAction.Portrait))
                            {
                                throw new ArgumentException("Unsupported name editor action");
                            }

                            names.Leader = Name(response.Names.Leader);
                            if (custom)
                            {
                                names.Tribe = Name(response.Names.Plural);
                                names.Adjective = Name(response.Names.Adjective);
                            }
                            if (response.Action == SetupNameAction.Titles)
                            {
                                var titles = new GovernmentTitles(names.Titles);
                                var edited = Invoke(() => services.ChooseTitles(titles));
                                if (edited.Status == SetupTitleStatus.Unavailable)
                                {
                                    return Finish();
                                }

                                if (edited.Status == SetupTitleStatus.Accept)
                                {
                                    for (int g = 0; g < RuleLimits.Governments; ++g)
                                    {
                                        Title(ref names, g, edited.Titles.Values[g]);
                                    }
                                }
                                else if (edited.Status != SetupTitleStatus.Cancel)
                                {
                                    throw new ArgumentException("Unsupported title editor result");
                                }
                            }
                            else if (response.Action == SetupNameAction.Portrait)
                            {
                                Publish();
                                services.ChoosePortrait(new((byte)human));
                            }
                            else
                            {
                                step = Step.CityStyle;
                            }

                            break;
                        }
                    case Step.CityStyle:
                        {
                            ref var names = ref world.Names[human];
                            Publish();
                            int style = services.ChooseCityStyle(
                                rules.Leaders[selectedTribe].CityStyle,
                                names.CityStyle);
                            Row(style, 3);
                            if (style < 0)
                            {
                                step = Step.Gender;
                                break;
                            }
                            names.CityStyle = (short)style;
                            globals.CivsAlive = globals.HumanPlayers.With(0);
                            for (int admitted = 0; admitted < globals.NumEnemies;)
                            {
                                int civilization = Sample(2, EntityLimits.Civilizations);
                                if (!globals.CivsAlive.Contains(civilization))
                                {
                                    globals.CivsAlive = globals.CivsAlive.With(civilization);
                                    ++admitted;
                                }
                            }
                            bool cancelled = false;
                            if (pickOpponents)
                            {
                                for (byte slot = 1; slot < EntityLimits.Civilizations; ++slot)
                                {

                                    if (!globals.CivsAlive.Contains(slot) || globals.HumanPlayers.Contains(slot))
                                    {
                                        continue;
                                    }

                                    var choices = Tribes(rules, slot);
                                    var choice = Invoke(() => services.ChooseOpponentTribe(new(slot), choices));
                                    if (choice is SetupNumericChoice.Selected selected)
                                    {
                                        if (selected.Row < 0)
                                        {
                                            cancelled = true;
                                            break;
                                        }
                                        if (!choices.Any(option => option.Tribe == selected.Row))
                                        {
                                            throw new ArgumentOutOfRangeException(
                                                nameof(services),
                                                "Opponent tribe is outside the offered color");
                                        }

                                        world.PendingLeaderOverrides[slot] = (sbyte)selected.Row;
                                    }
                                    else if (choice is not SetupNumericChoice.Random)
                                    {
                                        throw new ArgumentException("Missing opponent tribe choice");
                                    }
                                }
                            }

                            if (cancelled)
                            {
                                break;
                            }

                            return Finish(true);
                        }
                }
            }
        }
        catch
        {
            try
            {
                Publish();
            }
            catch { }

            throw;
        }
    }
}
