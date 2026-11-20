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
            Tiles = source.Tiles
        };
        result.Validate(false);
        return result;
    }

    public static GameInput Game(StoredSave source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Names.Count != 8 || source.Tribes.Count != 21 || source.Views.Count != 8
            || source.PowerHistory.Count != 150)
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
        var leaders = new LeaderCustomization?[21];
        int index = 0;
        void SelectLeader(int civilization)
        {
            if (civilization < 0 || civilization >= civilizations.Count)
            {
                throw new ArgumentException("Saved leader has no civilization backing", nameof(source));
            }

            int tribe = civilizations[civilization].Tribe;
            if (tribe < 0 || tribe >= 21)
            {
                throw new ArgumentException("Saved civilization has an invalid tribe", nameof(source));
            }

            if (index >= source.Leaders.Count)
            {
                throw new ArgumentException("Missing saved selected leader", nameof(source));
            }

            var saved = source.Leaders[index++];
            if (saved.Titles.Count != 7 || saved.Titles.Any(row => row.Count != 2))
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
            for (int civilization = 0; civilization < 8; ++civilization)
            {
                if ((source.Globals.HumanPlayers & (1U << civilization)) != 0)
                {
                    SelectLeader(civilization);
                }
            }
        }

        Fixed150<Fixed8<byte>> history = default;
        for (int i = 0; i < 150; ++i)
        {
            if (source.PowerHistory[i].Count != 8)
            {
                throw new ArgumentException("History sample requires eight civilizations", nameof(source));
            }

            for (int j = 0; j < 8; ++j)
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
            Tiles = source.Map.Tiles,
            Remembered = source.Map.Remembered,
            Coarse = source.Map.Coarse,
            Regions = source.Map.Regions
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

                return new EventInput(StateConversion.ToLive(entry.Record), entry.Strings);
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
            Tribes = new(
                source.Tribes.Select(value => new TribeInput(value.TimesUsed, value.CityNameIndex, value.Female != 0))),
            Leaders = new(leaders),
            CursorX = source.CursorX,
            CursorY = source.CursorY,
            PowerHistory = history,
            Layout = StateConversion.ToLive(source.Layout),
            Views = new(source.Views.Select(StateConversion.ToLive)),
            Scenario = source.Scenario is null ? null : StateConversion.ToLive(source.Scenario),
            Eliminations = StateConversion.ToLive(source.Eliminations),
            Multiplayer = source.Multiplayer is null ? null : StateConversion.ToLive(source.Multiplayer),
            Events = events,
        };
    }
}
