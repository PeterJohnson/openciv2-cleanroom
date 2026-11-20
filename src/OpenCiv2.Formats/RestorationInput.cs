using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;

namespace OpenCiv2.Formats;

/// <summary>F06 admitted stored values to owned semantic installation inputs; no world mutation or RNG.</summary>
public static class RestorationInput
{
    public static MapInput Map(RawMapFile source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new MapInput
        {
            Width = source.Header.Width,
            Height = source.Header.Height,
            Topology = source.Header.Topology,
            Seed = source.Header.Seed,
            Tiles = new(source.Tiles.Select(StateConversion.ToLive))
        };
        result.Validate(false);
        return result;
    }

    public static GameInput Game(StoredSave source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Names.Count != EntityLimits.Civilizations || source.Tribes.Count != RuleLimits.Tribes || source.Views.Count != EntityLimits.Civilizations
            || source.PowerHistory.Count != GameState.PowerHistorySamples)
        {
            throw new ArgumentException("Restoration input has incorrect fixed-group extents", nameof(source));
        }

        var civilizations = new OwnedList<Civilization>(source.Civilizations.Select(StateConversion.ToLive));
        var units = source.Units.Select(StateConversion.ToLive).ToArray();
        var cities = source.Cities.Select(StateConversion.ToLive).ToArray();
        if (source.Header.Version < 0x29)
        {
            for (int i = 0; i < units.Length; ++i)
            {
                units[i].Token = new(1);
            }

            for (int i = 0; i < cities.Length; ++i)
            {
                cities[i].Token = new(1);
            }
        }
        var leaders = new LeaderCustomization?[RuleLimits.Tribes];
        int index = 0;
        void SelectLeader(int civilization)
        {
            if (civilization < 0 || civilization >= civilizations.Count)
            {
                throw new ArgumentException("Saved leader has no civilization backing", nameof(source));
            }

            int tribe = civilizations[civilization].Tribe;
            if (tribe < 0 || tribe >= RuleLimits.Tribes)
            {
                throw new ArgumentException("Saved civilization has an invalid tribe", nameof(source));
            }

            if (index >= source.Leaders.Count)
            {
                throw new ArgumentException("Missing saved selected leader", nameof(source));
            }

            var saved = source.Leaders[index++];
            if (saved.Titles.Count != RuleLimits.Governments || saved.Titles.Any(row => row.Count != RuleLimits.LeaderGenders))
            {
                throw new ArgumentException("Saved leader requires seven title pairs", nameof(source));
            }

            leaders[tribe] = new(
                saved.Plural < 0,
                saved.Adjective < 0,
                new(saved.Titles.Select(row => new OwnedList<bool>(row.Select(value => value < 0)))));
        }
        if (source.Globals.Multiplayer == 0)
        {
            SelectLeader(source.Globals.PlayerCiv);
        }
        else
        {
            for (int civilization = 0; civilization < EntityLimits.Civilizations; ++civilization)
            {
                if ((source.Globals.HumanPlayers & (1U << civilization)) != 0)
                {
                    SelectLeader(civilization);
                }
            }
        }

        PowerHistoryArray<CivilizationArray<byte>> history = default;
        for (int i = 0; i < GameState.PowerHistorySamples; ++i)
        {
            if (source.PowerHistory[i].Count != EntityLimits.Civilizations)
            {
                throw new ArgumentException("History sample requires eight civilizations", nameof(source));
            }

            for (int j = 0; j < EntityLimits.Civilizations; ++j)
            {
                history[i][j] = source.PowerHistory[i][j];
            }
        }
        var map = new MapInput
        {
            Width = source.Map.Header.Width,
            Height = source.Map.Header.Height,
            Topology = source.Map.Header.Topology,
            Seed = source.Map.Header.Seed,
            Tiles = new(source.Map.Tiles.Select(StateConversion.ToLive)),
            Remembered = new(source.Map.Remembered.Select(layer =>
                new OwnedList<MapFeatures>(layer.Select(StateConversion.DecodeMapFeatures)))),
            Coarse = source.Map.Coarse,
            Regions = new(source.Map.Regions.Select(StateConversion.ToLive))
        };
        map.Validate(true);
        OwnedList<EventInput>? events = null;
        if (source.Events is { } storedEvents)
        {
            events = new(storedEvents.Select(entry =>
            {
                if (entry.Strings.Count != Event.StringCount)
                {
                    throw new ArgumentException("Event input requires 35 strings", nameof(source));
                }

                return new EventInput(StateConversion.ToLive(entry.Record),
                    new(entry.Strings.Select(text => text is null ? null : ClassicText.DecodeTerminated(text))));
            }));
        }
        return new()
        {
            Globals = StateConversion.ToLive(source.Globals),
            Names = new(source.Names.Select(StateConversion.ToLive)),
            Civilizations = civilizations,
            Map = map,
            Units = new(units),
            Cities = new(cities),
            Tribes = new(source.Tribes.Select(value =>
                new TribeInput(value.TimesUsed, value.CityNameIndex, value.Female != 0))),
            Leaders = new(leaders),
            PowerHistory = history,
            Layout = StateConversion.ToLive(source.Layout),
            Views = new(source.Views.Select(StateConversion.ToLive)),
            Scenario = source.Scenario is null ? null : StateConversion.ToLive(source.Scenario),
            Eliminations = StateConversion.ToLive(source.Eliminations),
            Multiplayer = source.Multiplayer is null ? null : StateConversion.ToLive(source.Multiplayer),
            Events = events,
            Cursor = new(source.CursorX, source.CursorY)
        };
    }
}
