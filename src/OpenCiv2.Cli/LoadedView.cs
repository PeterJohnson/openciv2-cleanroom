using System.Security.Cryptography;
using System.Text.Json.Nodes;
using OpenCiv2.Contracts;
using OpenCiv2.IO;
using OpenCiv2.Session;

/// <summary>Read-only loaded inspection under an explicitly selected local rules profile.</summary>
internal static class LoadedView
{
    internal static int Run(string path, bool json, Func<LocalPlayerSnapshot, string, byte[]>? export)
    {
        using var game = new LocalGame();
        using var input = File.OpenRead(path);
        if (input.Length > StoredDocument.MaximumBytes)
        {
            throw new IOException("Loaded inspection accepts files up to 32 MiB");
        }

        var bytes = new byte[(int)input.Length];
        input.ReadExactly(bytes);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var status = game.LoadClassic(bytes);
        if (status != LocalLoadResult.Loaded)
        {
            if (json)
            {
                Cli.Write(new JsonObject
                {
                    ["view"] = "loaded",
                    ["rules"] = "alder-valley",
                    ["sourceSha256"] = hash,
                    ["status"] = status.ToString()
                }.ToJsonString() + "\n");
            }
            else
            {
                Console.Error.WriteLine($"Loaded view unavailable ({status}).");
            }

            return status == LocalLoadResult.InvalidFile ? 4 : 3;
        }

        var snapshot = LocalPlayerSnapshot.Capture(game.Player);
        if (export is not null)
        {
            Cli.Write([.. export(snapshot, hash), (byte)'\n']);
        }
        else
        {
            Cli.Write(json ? ToJson(snapshot, hash) + "\n" :
                FormattableString.Invariant(
                    $"Alder Valley loaded player view. Turn {snapshot.Play.Turn}, year {snapshot.Play.Year}.\n")
                + $"{snapshot.Cities.Records.Count} owned cities, {snapshot.Units.Records.Count} owned units.\n"
                + "Loaded with renewed entity identities, rebuilt census and resumed player phase.\n");
        }

        return 0;
    }

    private static string ToJson(LocalPlayerSnapshot snapshot, string hash)
    {
        var play = snapshot.Play;
        var map = snapshot.Map;
        return new JsonObject
        {
            ["schema"] = "openciv2-loaded-player/1",
            ["view"] = "loaded",
            ["rules"] = "alder-valley",
            ["sourceSha256"] = hash,
            ["status"] = "Loaded",
            ["entry"] = "single-player ordinary resume",
            ["gameplaySeed"] = LocalGame.GameplaySeed,
            ["presentationSeed"] = LocalGame.PresentationSeed,
            ["transformations"] = new JsonArray("renew entity identities", "restore ownership and unit census",
                "reset classic passwords", "recalculate city output", "resume the player phase"),
            ["observer"] = play.Observation.Observer.Value,
            ["runtime"] = play.Observation.Runtime.Value,
            ["session"] = play.Observation.Session.Value,
            ["revision"] = play.Observation.Revision,
            ["turn"] = play.Turn,
            ["year"] = play.Year,
            ["gold"] = play.Gold,
            ["research"] = play.Researching,
            ["progress"] = play.ResearchProgress,
            ["known"] = new JsonArray(play.Discovered.Select(a => (JsonNode?)JsonValue.Create(a.Id)).ToArray()),
            ["units"] = new JsonArray(snapshot.Units.Records.Select(u => (JsonNode)new JsonObject
            {
                ["slot"] = u.Slot.Value,
                ["token"] = u.Token.Value,
                ["type"] = u.Type.Value,
                ["x"] = u.Position.X,
                ["y"] = u.Position.Y,
                ["moves"] = u.MovesUsed,
                ["orders"] = (int)u.Orders,
                ["home"] = u.Home?.Value
            }).ToArray()),
            ["cities"] = new JsonArray(snapshot.Cities.Records.Select(c => (JsonNode)new JsonObject
            {
                ["slot"] = c.Slot.Value,
                ["token"] = c.Token.Value,
                ["name"] = c.Name,
                ["x"] = c.Position.X,
                ["y"] = c.Position.Y,
                ["size"] = c.Size,
                ["production"] = c.Production,
                ["foodBox"] = c.FoodBox,
                ["shieldBox"] = c.ShieldBox,
                ["science"] = play.City?.Science,
                ["specialists"] = play.City?.Specialists,
                ["worked"] = new JsonArray(play.City?.Tiles.Where(t => t.Worked)
                    .Select(t => (JsonNode?)JsonValue.Create(t.Index)).ToArray() ?? [])
            }).ToArray()),
            ["tiles"] = new JsonArray(map.Tiles.Select((tile, i) => (tile, i))
                .Where(item => item.tile.Terrain is not null).Select(item => (JsonNode)new JsonObject
                {
                    ["index"] = item.i,
                    ["terrain"] = item.tile.Terrain,
                    ["river"] = item.tile.River,
                    ["rememberedFeatures"] = (int)item.tile.Improvements
                }).ToArray())
        }.ToJsonString();
    }
}
