using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.Session;

/// <summary>F06 §6 successful restoration over owned inputs. The caller owns activity and input finalization.
/// Installation retains accepted prefixes on failure; it neither starts a session nor replaces its world.</summary>
public static class GameInstallation
{
    private static int Tribe(int civ, IReadOnlyList<Civilization> civilizations)
    {
        if (civ < 0 || civ >= civilizations.Count)
        {
            throw new ArgumentException("Leader selection has no civilization backing");
        }

        int tribe = civilizations[civ].Tribe;
        if (tribe < 0 || tribe >= RuleLimits.Tribes)
        {
            throw new ArgumentException("Leader selection has no tribe backing");
        }

        return tribe;
    }

    public static void Validate(GameInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        input.Map.Validate(true);
        if (input.Civilizations.Count != EntityLimits.Civilizations || input.Names.Count != EntityLimits.Civilizations || input.Views.Count != EntityLimits.Civilizations
            || input.Tribes.Count != RuleLimits.Tribes
            || input.Leaders.Count != RuleLimits.Tribes
            ||
            input.Globals.UnitEnd < 0 || input.Globals.CityEnd < 0 || input.Units.Count != input.Globals.UnitEnd
            || input.Cities.Count != input.Globals.CityEnd
            ||
            input.Units.Count > EntityLimits.Units || input.Cities.Count > EntityLimits.Cities)
        {
            throw new ArgumentException("Restoration input has inconsistent population or fixed record bounds");
        }

        for (int civ = 0; civ < EntityLimits.Civilizations; ++civ)
        {
            _ = Tribe(civ, input.Civilizations);
        }

        if (input.Names.Any(n => n.Titles.Count != RuleLimits.Governments) || input.Eliminations.Names.Count != OpenCiv2.Core.State.EliminationHistory.Capacity ||
            input.Leaders.Any(l => l is not null && (l.Titles.Count != RuleLimits.Governments || l.Titles.Any(pair => pair.Count != RuleLimits.LeaderGenders))) ||
            input.Multiplayer is { } multiplayer && multiplayer.Emails.Count != EntityLimits.Civilizations
            || input.Events is { } events && events.Any(e => e.Strings.Count != Event.StringCount))
        {
            throw new ArgumentException("Restoration input has inconsistent nested record bounds");
        }

        bool scenario = input.Globals.Status.HasScenario;
        if (scenario != input.Scenario.HasValue)
        {
            throw new ArgumentException("Scenario flags and restoration parameters disagree");
        }

        if (!scenario)
        {
            void Selected(int civ)
            {
                if (input.Leaders[Tribe(civ, input.Civilizations)] is null)
                {
                    throw new ArgumentException("Missing selected leader customization");
                }
            }
            if (input.Globals.Multiplayer == GameMode.SinglePlayer)
            {
                Selected(input.Globals.PlayerCiv);
            }
            else
            {
                for (int civ = 0; civ < EntityLimits.Civilizations; ++civ)
                {
                    if (input.Globals.HumanPlayers.Contains(civ))
                    {
                        Selected(civ);
                    }
                }
            }
        }
        else if (input.Names.Any(n => n.Tribe.Contains('\0')))
        {
            throw new ArgumentException("Scenario tribe name contains NUL");
        }
    }

    public static void RestoreLeaderNames(GameInput input, GameState world, RuleState rules)
    {
        foreach (var live in rules.Leaders)
        {
            live.LeaderName = live.Names[live.Female ? 1 : 0];
            live.Plural = live.Plural with { Custom = false };
            live.Adjective = live.Adjective with { Custom = false };
            for (int g = 0; g < RuleLimits.Governments; ++g)
            {
                for (int sex = 0; sex < RuleLimits.LeaderGenders; ++sex)
                {
                    live.Titles[g][sex] = live.Titles[g][sex] with { Custom = false };
                }
            }
        }
        if (!input.Globals.Status.HasScenario)
        {
            void Restore(int civ)
            {
                int selected = Tribe(civ, world.Civilizations);
                var live = rules.Leaders[selected];
                var saved = input.Leaders[selected]
                    ?? throw new ArgumentException("Missing selected leader customization");
                live.LeaderName = live.LeaderName with { Custom = !live.LeaderName.Custom };
                live.Plural = live.Plural with { Custom = live.Plural.Custom ^ saved.Plural };
                live.Adjective = live.Adjective with { Custom = live.Adjective.Custom ^ saved.Adjective };
                for (int g = 0; g < RuleLimits.Governments; ++g)
                {
                    for (int sex = 0; sex < RuleLimits.LeaderGenders; ++sex)
                    {
                        live.Titles[g][sex] = live.Titles[g][sex] with
                        {
                            Custom = live.Titles[g][sex].Custom ^ saved.Titles[g][sex]
                        };
                    }
                }
            }
            if (world.Globals.Multiplayer == GameMode.SinglePlayer)
            {
                Restore(world.Globals.PlayerCiv);
            }
            else
            {
                for (int civ = 0; civ < EntityLimits.Civilizations; ++civ)
                {
                    if (world.Globals.HumanPlayers.Contains(civ))
                    {
                        Restore(civ);
                    }
                }
            }

            return;
        }
        for (int civ = 1; civ < EntityLimits.Civilizations; ++civ)
        {
            int selected = Tribe(civ, world.Civilizations);
            var live = rules.Leaders[selected];
            live.Female = world.Civilizations[civ].Flags.FemaleLeader;
            world.Civilizations[civ].LeaderPortrait = (byte)(selected + (live.Female ? RuleLimits.Tribes : 0));
            if (world.Names[civ].Tribe.Equals(live.Plural.Text))
            {
                for (int g = 0; g < RuleLimits.Governments; ++g)
                {
                    for (int sex = 0; sex < RuleLimits.LeaderGenders; ++sex)
                    {
                        var titles = rules.Definitions.Governments[g].Titles;
                        live.Titles[g][sex] = new(RuleText.Display((sex == 0 ? titles.Male : titles.Female)));
                    }
                }
            }

            live.LeaderName = live.LeaderName with { Custom = !live.LeaderName.Custom };
            live.Plural = live.Plural with { Custom = !live.Plural.Custom };
            live.Adjective = live.Adjective with { Custom = !live.Adjective.Custom };
        }
    }

    public static void Install(
        GameInput input,
        SessionRuntime runtime,
        RuleInput rulesInput,
        Rectangle screen,
        string passwordResetCall)
    {
        Validate(input);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(rulesInput);
        var world = runtime.World;
        var process = runtime.Process;
        var entryMode = world.Globals.Multiplayer;
        var random = input.Multiplayer is null ? runtime.GameplayRandom : null;
        if (random is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(passwordResetCall);
        }

        input.Map.Install(world.Map, true);
        world.Globals = input.Globals;
        ref var globals = ref world.Globals;
        globals.NumEnemies = unchecked((byte)(globals.CivsAlive.Count - 1 - (globals.CivsAlive.Contains(0) ? 1 : 0)));
        bool scenario = globals.Status.HasScenario;
        if (globals.Multiplayer != GameMode.SinglePlayer)
        {
            globals.GameOptions.CheatMode = false;
        }

        if (scenario)
        {
            globals.Status.Scenario = true;
        }

        if (globals.Status.ScenarioExport)
        {
            globals.GameOptions.CheatMode = false;
            globals.RevealMap = 0;
            globals.Status.Cheated = false;
        }
        globals.Status.ScenarioExport = false;
        input.Names.ToArray().CopyTo(world.Names, 0);
        input.Civilizations.ToArray().CopyTo(world.Civilizations, 0);
        world.Entities.Units.SetEnd(input.Units.Count);
        world.Entities.Cities.SetEnd(input.Cities.Count);
        input.Units.ToArray().AsSpan().CopyTo(world.Entities.Units.Scanned);
        input.Cities.ToArray().AsSpan().CopyTo(world.Entities.Cities.Scanned);
        world.Entities.RenewLoadedLifetimes(process.Tokens, false);
        for (int i = 0; i < RuleLimits.Tribes; ++i)
        {
            var target = process.Rules.Leaders[i];
            target.TimesUsed = input.Tribes[i].TimesUsed;
            target.CityNameIndex = input.Tribes[i].CityNameIndex;
            target.Female = input.Tribes[i].Female;
        }
        world.PowerHistory = input.PowerHistory;
        world.Layout = input.Layout;
        input.Views.ToArray().CopyTo(world.Views, 0);
        if (input.Scenario is { } parameters)
        {
            world.Scenario = parameters;
        }
        else
        {
            world.Scenario.Reset();
        }

        world.Eliminations = input.Eliminations;
        ref var multiplayer = ref world.Multiplayer;
        ref var saved = ref multiplayer.Saved;
        ref var preferences = ref process.MultiplayerPreferences;
        multiplayer.LocalHuman = globals.SavedCurrentCiv;
        if (input.Multiplayer is null)
        {
            PasswordConversion.ResetPasswords(ref multiplayer, random!, passwordResetCall);
            saved.OpenGame = true;
            saved.KillOnRetirement = preferences.KillOnRetirement;
            saved.ShowHumanStarts = preferences.ShowHumanStarts;
            saved.ChatFromStart = preferences.ChatFromStart;
            saved.DoubleProduction = saved.DoubleMovement = false;
            preferences.Simultaneous = false;
            saved.ResumeCiv = unchecked((byte)multiplayer.LocalHuman);
        }
        else
        {
            saved = input.Multiplayer.Value;
            if (saved.ResumeCiv == 0)
            {
                saved.ResumeCiv = unchecked((byte)globals.TurnCiv);
                if (saved.ResumeCiv == 0)
                {
                    for (int civ = 1; civ < EntityLimits.Civilizations; ++civ)
                    {
                        if (globals.CivsAlive.Contains(civ))
                        {
                            saved.ResumeCiv = (byte)civ;
                            break;
                        }
                    }
                }
            }
            MultiplayerOperations.RefreshPasswordFlags(ref multiplayer);
        }
        multiplayer.ResumeCiv = saved.ResumeCiv;
        if (input.Events is { } events)
        {
            world.Events.Collection.Install(events);
        }
        else
        {
            world.Events.Collection.Reset();
        }

        world.Events.Loaded = input.Events is not null;
        RestoreLeaderNames(input, world, process.Rules);
        CivilizationCensus.RebuildLoadedUnits(world.Civilizations, world.Entities);
        world.Mood.City = null;
        var installer = new RuleInstaller(rulesInput, process.Rules);
        installer.Advances(true);
        installer.Improvements(true);
        installer.Units(true);
        var savedScreen = world.Layout.Rectangles[0];
        world.Layout.Valid = !scenario
            && unchecked(savedScreen.Right - savedScreen.Left) == unchecked(screen.Right - screen.Left)
            && unchecked(savedScreen.Bottom - savedScreen.Top) == unchecked(screen.Bottom - screen.Top);
        MultiplayerOperations.AdaptLoadedMode(entryMode, ref globals, ref multiplayer, ref preferences);
    }

    /// <summary>F06 §5: scenario export fills only names whose current rule reference is not custom.</summary>
    public static void PrepareScenarioNames(
        bool scenarioExport,
        GlobalValues globals,
        IReadOnlyList<Civilization> civilizations,
        Span<CivilizationNames> names,
        RuleState rules)
    {
        if (!scenarioExport && !globals.Status.Scenario)
        {
            return;
        }

        for (int civ = 1; civ < EntityLimits.Civilizations; ++civ)
        {
            var live = rules.Leaders[Tribe(civ, civilizations)];
            if (!live.Adjective.Custom)
            {
                names[civ].Adjective = live.Adjective.Text;
            }

            if (!live.Plural.Custom)
            {
                names[civ].Tribe = live.Plural.Text;
            }

            if (!live.LeaderName.Custom)
            {
                names[civ].Leader = live.LeaderName.Text;
            }
        }
    }
}
