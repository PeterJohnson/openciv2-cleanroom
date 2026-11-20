using System.Numerics;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

public sealed record NewGameServices(Func<int, int> CreateCivilization, Action BalanceStarts)
{
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(CreateCivilization);
        ArgumentNullException.ThrowIfNull(BalanceStarts);
    }
}
public static class NewGameInitialization
{
    /// <summary>F05 §5 inner initialization after this attempt's fresh boundary, content, wizard and W02.
    /// Required civilization creation/balancing remain supplied children. Earlier effects survive failure.</summary>
    public static void Initialize(SessionRuntime runtime, GameplayRandom random, string call, NewGameServices services)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        using var pin = runtime.PinWorld();
        var world = runtime.World;
        var process = runtime.Process;
        ref var globals = ref world.Globals;
        void Publish()
        {
            if (runtime.Active)
            {
                runtime.Publish();
            }
        }
        try
        {
            world.Scenario.Reset();
            world.Eliminations.Count = 0;
            world.Eliminations.Turns = default;
            world.Eliminations.Killers = default;
            world.Eliminations.Names = new(Enumerable.Repeat(OwnedList<byte>.Empty, 12));
            world.CivilizationColorFlags = default;
            foreach (var leader in process.Rules.Leaders)
            {
                leader.TimesUsed = 0;
                leader.CityNameIndex = 0;
            }
            globals.Turn = 0;
            globals.Year = -4000;
            globals.FirstLaunchTurn = -1;
            globals.GameOptions = process.OrdinaryPreferences.GameOptions & ~0x8000U;
            if (globals.Difficulty == Difficulty.Chieftain)
            {
                globals.GameOptions |= 0x300;
            }
            else
            {
                globals.GameOptions &= ~0x100U;
            }

            globals.TransientFlags = globals.ShownEventFlags = 0;
            globals.MessageOptions = process.OrdinaryPreferences.DisabledMessages;
            world.MovementMode = MovementMode.MoveUnits;
            world.Entities.Units.SetEnd(0);
            world.Entities.Cities.SetEnd(0);
            globals.UnitEnd = globals.CityEnd = 0;
            globals.ActiveUnit = new(0);
            globals.TurnCiv = 0;
            globals.ViewCiv = -1;
            globals.RevealMap = 0;
            globals.WarmingCounter = globals.GlobalWarmings = 0;
            globals.WarmingPollution = globals.PollutedTiles = 0;
            globals.FirstDiscoverer = default;
            globals.AdvanceKnownBy = default;
            for (int i = 0; i < 28; ++i)
            {
                globals.WonderCity[i] = -1;
            }

            for (int slot = 0; slot < 8; ++slot)
            {
                ref var civ = ref world.Civilizations[slot];
                civ.SpaceshipFlags = 0;
                civ.SpaceshipParts = default;
                civ.SpaceshipPopulation = 0;
                civ.SpaceshipLaunchYear = civ.SpaceshipArrivalYear = 0;
                civ.CityCount = 0;
                world.UnitTotals[slot] = 0;
                civ.MilitaryCount = 0;
                civ.MilitaryStrength = 0;
                civ.TotalAttack = 0;
                civ.TotalCitySize = 0;
                civ.ShipCount = 0;
                civ.PopulationMilestone = 0;
                civ.Researching = 0;
                civ.ContinentMilitary = default;
                civ.ContinentAttack = default;
                civ.ContinentCities = default;
                civ.ContinentCitySize = default;
            }
            globals.NextHistoryTurn = (short)random.PrimaryModulo(new ChoiceKey("F05.new-game.history", call), 50);
            world.PowerHistory = default;
            for (int slot = 0; slot < 8; ++slot)
            {
                Publish();
                _ = services.CreateCivilization(slot);
            }
            globals.CivsAtStart = globals.CivsAlive;
            Publish();
            services.BalanceStarts();
            ref var preferences = ref process.OrdinaryPreferences;
            if (preferences.Science + preferences.Tax > 10 || preferences.Science > 6 || preferences.Tax > 6)
            {
                preferences.Science = 6;
                preferences.Tax = 4;
            }
            if (BitOperations.PopCount(globals.HumanPlayers) == 1)
            {
                if (globals.PlayerCiv is < 0 or >= 8)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(runtime),
                        "New-game player has no civilization backing");
                }

                ref var civ = ref world.Civilizations[globals.PlayerCiv];
                civ.ScienceRate = unchecked((byte)preferences.Science);
                civ.TaxRate = unchecked((byte)preferences.Tax);
            }
            else
            {
                for (int slot = 1; slot < 8; ++slot)
                {
                    if ((globals.HumanPlayers & (1 << slot)) != 0)
                    {
                        world.Civilizations[slot].ScienceRate = 6;
                        world.Civilizations[slot].TaxRate = 4;
                    }
                }
            }

            if (globals.Difficulty == Difficulty.Chieftain)
            {
                for (int slot = 1; slot < 8; ++slot)
                {
                    if ((globals.HumanPlayers & (1 << slot)) != 0)
                    {
                        world.Civilizations[slot].Gold = 50;
                    }
                }
            }

            Publish();
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
