using System.Globalization;
using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Export.Sqlite;

public static partial class SqliteSnapshot
{
    private const string PlayerSchema = """
        PRAGMA application_id=1329803859;
        PRAGMA user_version=1;
        PRAGMA temp_store=MEMORY;
        BEGIN;
        CREATE TABLE metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
        CREATE TABLE player(turn INTEGER, year INTEGER, gold INTEGER, research INTEGER,
          progress INTEGER, threshold INTEGER, player_phase INTEGER);
        CREATE TABLE units(slot INTEGER PRIMARY KEY, token INTEGER, type INTEGER, x INTEGER, y INTEGER,
          moves INTEGER, damage INTEGER, orders INTEGER, home INTEGER);
        CREATE TABLE cities(slot INTEGER PRIMARY KEY, token INTEGER, name TEXT, x INTEGER, y INTEGER, size INTEGER,
          production INTEGER, food_stock INTEGER, shield_stock INTEGER, specialists INTEGER, science INTEGER);
        CREATE TABLE worked_tiles(city_slot INTEGER, radius_index INTEGER, x INTEGER, y INTEGER,
          PRIMARY KEY(city_slot,radius_index));
        CREATE TABLE tiles(tile_index INTEGER PRIMARY KEY, terrain INTEGER, river INTEGER, remembered_features INTEGER);
        CREATE TABLE advances(id INTEGER PRIMARY KEY, name TEXT);
        """;

    /// <summary>Exports a scoped capture of loaded or live local play. A source hash identifies classic input when present.</summary>
    public static byte[] CreatePlayer(LocalPlayerSnapshot snapshot, string? sourceHash = null, StopToken stop = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        int total = snapshot.Units.Records.Count + snapshot.Cities.Records.Count + snapshot.Map.Tiles.Count;
        return CreateImage(PlayerSchema, total, (db, checkpoint) => StorePlayer(db, snapshot, sourceHash, checkpoint), stop);
    }

    private static void StorePlayer(DatabaseHandle db, LocalPlayerSnapshot snapshot, string? sourceHash, Action<int> checkpoint)
    {
        var play = snapshot.Play;
        using var metadata = new Statement(db, "INSERT INTO metadata VALUES(?,?)");
        void Meta(string name, string value)
        {
            metadata.Bind(1, name);
            metadata.Bind(2, value);
            metadata.Run();
        }

        Meta("schema", "openciv2-player-analysis/1");
        Meta("view", sourceHash is null ? "live" : "loaded");
        Meta("rules", "alder-valley");
        Meta("access", "player");
        Meta("observer", play.Observation.Observer.Value.ToString(CultureInfo.InvariantCulture));
        Meta("runtime", play.Observation.Runtime.Value.ToString(CultureInfo.InvariantCulture));
        Meta("session", play.Observation.Session.Value.ToString(CultureInfo.InvariantCulture));
        Meta("revision", play.Observation.Revision.ToString(CultureInfo.InvariantCulture));
        Meta("width", snapshot.Map.Width.ToString(CultureInfo.InvariantCulture));
        Meta("height", snapshot.Map.Height.ToString(CultureInfo.InvariantCulture));
        if (sourceHash is not null)
        {
            Meta("source_sha256", sourceHash);
        }

        using var player = new Statement(db, "INSERT INTO player VALUES(?,?,?,?,?,?,?)");
        InsertIntegers(player, play.Turn, play.Year, play.Gold, play.Researching, play.ResearchProgress,
            play.ResearchCost, play.PlayerPhase ? 1 : 0);
        player.Run();
        using var units = new Statement(db, "INSERT INTO units VALUES(?,?,?,?,?,?,?,?,?)");
        using var cities = new Statement(db, "INSERT INTO cities VALUES(?,?,?,?,?,?,?,?,?,?,?)");
        using var work = new Statement(db, "INSERT INTO worked_tiles VALUES(?,?,?,?)");
        using var tiles = new Statement(db, "INSERT INTO tiles VALUES(?,?,?,?)");
        using var advances = new Statement(db, "INSERT INTO advances VALUES(?,?)");
        int count = 0;
        foreach (var unit in snapshot.Units.Records)
        {
            InsertIntegers(units, unit.Slot.Value, unit.Token.Value, unit.Type.Value,
                unit.Position.X, unit.Position.Y, unit.MovesUsed, unit.Damage, (int)unit.Orders);
            if (unit.Home is { } home)
            {
                units.Bind(9, home.Value);
            }

            units.Run();
            checkpoint(++count);
        }

        foreach (var city in snapshot.Cities.Records)
        {
            cities.Bind(1, city.Slot.Value);
            cities.Bind(2, city.Token.Value);
            cities.Bind(3, city.Name);
            cities.Bind(4, city.Position.X);
            cities.Bind(5, city.Position.Y);
            cities.Bind(6, city.Size);
            cities.Bind(7, city.Production);
            cities.Bind(8, city.FoodBox);
            cities.Bind(9, city.ShieldBox);
            if (play.City is { } output && output.Target == new CityTarget(city.Slot, city.Token))
            {
                cities.Bind(10, output.Specialists);
                cities.Bind(11, output.Science);
                foreach (var tile in output.Tiles.Where(t => t.Worked))
                {
                    InsertIntegers(work, city.Slot.Value, tile.Index, tile.Position.X, tile.Position.Y);
                    work.Run();
                }
            }

            cities.Run();
            checkpoint(++count);
        }

        for (int i = 0; i < snapshot.Map.Tiles.Count; i++)
        {
            var tile = snapshot.Map.Tiles[i];
            if (tile.Terrain is byte terrain)
            {
                InsertIntegers(tiles, i, terrain, tile.River ? 1 : 0, (int)tile.Improvements);
                tiles.Run();
            }

            checkpoint(++count);
        }

        foreach (var advance in play.Discovered)
        {
            advances.Bind(1, advance.Id);
            advances.Bind(2, advance.Name);
            advances.Run();
        }
    }

    private static void InsertIntegers(Statement statement, params long[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            statement.Bind(i + 1, values[i]);
        }
    }
}
