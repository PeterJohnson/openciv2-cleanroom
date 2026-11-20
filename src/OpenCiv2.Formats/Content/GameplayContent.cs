using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Core;

namespace OpenCiv2.Formats.Content;

public sealed record GameplayBundle(GameplayValues Values, string Id, string Revision, string ManifestSha256,
    string SemanticSha256, bool CustomMode, bool ClassicValues, OwnedList<FileReceipt> Files);

public static class GameplayContent
{
    public const string Target = "classic-mge-5.4.0f";
    public const string ClassicSha256 = "d3276f6cfe815b9b8412e2930a4d3acd49b831bb11277b21779b53448356a940";

    private static Rational Ratio(ContentReader root)
    {
        root.Fields(["numerator", "denominator"]);
        int n = root.At("numerator").Integer(0), d = root.At("denominator").Integer(1);
        int a = n, b = d;
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        n /= a;
        d /= a;
        root.At("numerator").Set(n);
        root.At("denominator").Set(d);
        return new(n, d);
    }

    private static CalendarValues Calendar(ContentReader root)
    {
        root.Fields(["format", "profiles", "retirement"], true);
        var profiles = root.At("profiles");
        profiles.Fields(GameplayNames.CalendarProfiles);
        var values = new List<OwnedList<CalendarPeriod>>();
        foreach (string name in GameplayNames.CalendarProfiles)
        {
            var profile = profiles.At(name);
            profile.Fields(["periods"]);
            var periods = profile.At("periods");
            if (periods.ArraySize() != 6)
            {
                periods.Error("expected six ordered periods");
            }

            long end = 0;
            var rows = new List<CalendarPeriod>();
            for (int j = 0; j < 6; ++j)
            {
                var period = periods.Element(j);
                period.Fields(["startYear", "turnCount", "yearStep"]);
                var row = new CalendarPeriod(
                    period.At("startYear").Integer(),
                    period.At("turnCount").Integer(1),
                    period.At("yearStep").Integer(1));
                if (j != 0 && row.StartYear != end)
                {
                    period.Error("discontinuous calendar period");
                }

                end = row.StartYear + (long)row.TurnCount * row.YearStep;
                if (end < int.MinValue || end > int.MaxValue)
                {
                    period.Error("calendar endpoint outside authoring domain");
                }

                rows.Add(row);
            }
            values.Add(new(rows));
        }
        var retirement = root.At("retirement");
        retirement.Fields(["warningYear", "endYear", "scenarioWarningLeadTurns"]);
        int warning = retirement.At("warningYear").Integer(), endYear = retirement.At("endYear").Integer();
        int lead = retirement.At("scenarioWarningLeadTurns").Integer(0);
        if (warning >= endYear)
        {
            retirement.Error("warning must precede retirement year");
        }

        return new(new(values), warning, endYear, lead);
    }

    private static SpaceshipValues Spaceship(ContentReader root)
    {
        root.Fields(["format", "parts", "peoplePerHabitation"], true);
        var parts = root.At("parts");
        parts.Fields(GameplayNames.SpaceshipParts);
        var values = new List<SpaceshipPartValues>();
        for (int i = 0; i < 6; ++i)
        {
            var part = parts.At(GameplayNames.SpaceshipParts[i]);
            part.Fields(["group", "maximumCount", "massTons"]);
            part.At("group").Constant(i == 0 ? "structural" : i < 3 ? "component" : "module");
            int maximum = part.At("maximumCount").Integer(0, 32767), mass = part.At("massTons").Integer(100);
            if (mass % 100 != 0)
            {
                part.At("massTons").Error("mass must be divisible by 100 tons");
            }

            values.Add(
                new(
                    i == 0 ? SpaceshipGroup.Structural : i < 3 ? SpaceshipGroup.Component : SpaceshipGroup.Module,
                    maximum,
                    mass / 100));
        }
        return new(new(values), root.At("peoplePerHabitation").Integer(1));
    }

    private static OwnedList<GovernmentValues> Governments(ContentReader root)
    {
        root.Fields(["format", "governments"], true);
        var records = root.At("governments");
        records.Fields(GameplayNames.Governments);
        var values = new List<GovernmentValues>();
        for (int i = 0; i < 7; ++i)
        {
            var record = records.At(GameplayNames.Governments[i]);
            record.Fields(["maximumRateTenths", "requiredAdvance", "corruptionDivisor"]);
            int maximum = record.At("maximumRateTenths")
                .Integer(
                    0,
                    10), divisor = record.At("corruptionDivisor")
                .Integer(
                    1);
            var advance = record.At("requiredAdvance");
            string? id = null;
            if (i < 2)
            {
                if (advance.Value is not null)
                {
                    advance.Error("this government has no required advance");
                }
            }
            else
            {
                id = advance.Id();
            }

            values.Add(new(maximum, divisor, id));
        }
        return new(values);
    }

    private static OwnedList<DifficultyValues> Difficulties(ContentReader root)
    {
        root.Fields(["format", "difficulties"], true);
        var records = root.At("difficulties");
        records.Fields(GameplayNames.Difficulties);
        var values = new List<DifficultyValues>();
        foreach (string name in GameplayNames.Difficulties)
        {
            var record = records.At(name);
            record.Fields(["aiBoxFactor", "humanResearchMultiplier", "aiResearchMultiplier"]);
            values.Add(
                new(
                    record.At("aiBoxFactor").Integer(1),
                    record.At("humanResearchMultiplier").Integer(1),
                    record.At("aiResearchMultiplier").Integer(1)));
        }
        return new(values);
    }

    private static CombatValues Combat(ContentReader root)
    {
        root.Fields(
            [
                "format",
                "strengthBonuses",
                "defenseMultipliers",
                "antiAirMultipliers",
                "partisansAgainstZeroAttackMultiplier"
            ],
            true);
        var bonuses = root.At("strengthBonuses");
        bonuses.Fields(["veteran", "paradrop"]);
        var veteran = Ratio(bonuses.At("veteran"));
        var paradrop = Ratio(bonuses.At("paradrop"));
        var defense = root.At("defenseMultipliers");
        defense.Fields(["fortification", "fortress", "cityWalls"]);
        int Factor(string key)
        {
            var field = defense.At(key);
            var ratio = Ratio(field);
            long doubled = 2L * ratio.Numerator;
            if (doubled % ratio.Denominator != 0 || doubled / ratio.Denominator < 1
                || doubled / ratio.Denominator > int.MaxValue)
            {
                field.Error("doubled defense multiplier must be a positive authoring integer");
            }

            return (int)(doubled / ratio.Denominator);
        }
        int fortification = Factor("fortification"), fortress = Factor("fortress"), walls = Factor("cityWalls");
        var air = root.At("antiAirMultipliers");
        air.Fields(["ordinaryAircraft", "destroyedAfterAttack"]);
        return new(veteran, paradrop, fortification, fortress, walls, air.At("ordinaryAircraft").Integer(1),
            air.At("destroyedAfterAttack").Integer(1), root.At("partisansAgainstZeroAttackMultiplier").Integer(1));
    }

    private static ScoringValues Scoring(ContentReader root)
    {
        root.Fields(
            [
                "format",
                "citizens",
                "wonderPoints",
                "spaceshipPointsPerPopulationSuccess",
                "pollutionPointsPerTile",
                "peace",
                "futureTechnologyPoints",
                "barbarianActivity",
                "conquest",
                "objectiveValue",
                "scenario",
                "rating"
            ],
            true);
        var citizens = root.At("citizens");
        citizens.Fields(["perCitizen", "perHappyCitizen", "perUnhappyCitizen"]);
        int citizen = citizens.At("perCitizen")
            .Integer(), happy = citizens.At("perHappyCitizen")
            .Integer(), unhappy = citizens.At("perUnhappyCitizen")
            .Integer();
        int wonder = root.At("wonderPoints")
            .Integer(), spaceship = root.At("spaceshipPointsPerPopulationSuccess")
            .Integer(),
            pollution = root.At("pollutionPointsPerTile")
                .Integer(), technology = root.At("futureTechnologyPoints")
                .Integer();
        var peace = root.At("peace");
        peace.Fields(["firstEligibleTurn", "pointsPerTurn", "maximumPoints"]);
        var peaceValues = new PeaceScoring(
            peace.At("firstEligibleTurn").Integer(0),
            peace.At("pointsPerTurn").Integer(),
            peace.At("maximumPoints").Integer(0));
        var barbarian = root.At("barbarianActivity");
        barbarian.Fields(["pointsPerLevel", "offsetPoints"]);
        var barbarianValues = new BarbarianScoring(
            barbarian.At("pointsPerLevel").Integer(),
            barbarian.At("offsetPoints").Integer());
        var conquest = root.At("conquest");
        conquest.Fields(
            [
                "pointsPerEnemy",
                "basePoints",
                "turnReference",
                "pointsPerRemainingTurn",
                "noRestartMinimumTurn",
                "largeMapMultiplier",
                "smallMapMultiplier"
            ]);
        var conquestValues = new ConquestScoring(
            conquest.At("pointsPerEnemy").Integer(),
            conquest.At("basePoints").Integer(),
            conquest.At("turnReference").Integer(0),
            conquest.At("pointsPerRemainingTurn").Integer(),
            conquest.At("noRestartMinimumTurn").Integer(0),
            Ratio(conquest.At("largeMapMultiplier")),
            Ratio(conquest.At("smallMapMultiplier")));
        var objective = root.At("objectiveValue");
        objective.Fields(["city", "wonder"]);
        int objectiveCity = objective.At("city").Integer(0), objectiveWonder = objective.At("wonder").Integer(0);
        var scenario = root.At("scenario");
        scenario.Fields(["outcomePoints", "pointsPerObjective", "conquestMultiplier"]);
        var outcomes = scenario.At("outcomePoints");
        outcomes.Fields(GameplayNames.ScenarioOutcomes);
        var outcomeValues = new OwnedList<int>(
            GameplayNames.ScenarioOutcomes.Select(name => outcomes.At(name).Integer()));
        int points = scenario.At("pointsPerObjective")
            .Integer(), multiplier = scenario.At("conquestMultiplier")
            .Integer(
                0);
        var rating = root.At("rating");
        rating.Fields(["difficultyFactors"]);
        var factors = rating.At("difficultyFactors");
        factors.Fields(GameplayNames.Difficulties);
        var factorValues = new OwnedList<int>(GameplayNames.Difficulties.Select(name => factors.At(name).Integer(1)));
        return new(
            citizen,
            happy,
            unhappy,
            wonder,
            spaceship,
            pollution,
            technology,
            peaceValues,
            barbarianValues,
            conquestValues,
            objectiveCity,
            objectiveWonder,
            outcomeValues,
            points,
            multiplier,
            factorValues);
    }

    public static GameplayBundle Load(ReadOnlySpan<byte> manifest, ContentResolver resolve, bool requireClassic = false)
    {
        var root = new ContentReader(ContentJson.Parse(manifest, "gameplay.json"), "gameplay.json");
        root.Fields(["format", "id", "revision", "target", "mode", "components", "provenance"], true);
        root.At("format").Constant("openciv2-gameplay/1");
        root.At("target").Constant(Target);
        string id = root.At("id").Id(), revision = root.At("revision").Revision(), mode = root.At("mode").Text();
        if (mode is not ("classic" or "custom"))
        {
            root.At("mode").Error("expected classic or custom mode");
        }

        var provenance = root.At("provenance");
        provenance.Fields(["authors", "license", "notes"]);
        provenance.At("authors").Texts(true);
        provenance.At("license").Text();
        provenance.At("notes").Texts(false);
        var descriptors = root.At("components");
        descriptors.Fields(GameplayNames.Components);
        var files = new List<FileReceipt>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in GameplayNames.Components)
        {
            var descriptor = descriptors.At(name);
            descriptor.Fields(["path", "sha256"]);
            string path = descriptor.At("path").Text(), sha = descriptor.At("sha256").Text();
            ContentPaths.ValidateRelative(path);
            if (!paths.Add(path))
            {
                descriptor.Error("components must use distinct files");
            }

            descriptor.At("sha256").Sha256();
            files.Add(new(path, "", sha));
        }
        var values = new JsonObject();
        CalendarValues calendar = null!;
        SpaceshipValues spaceship = null!;
        CombatValues combat = null!;
        ScoringValues scoring = null!;
        OwnedList<GovernmentValues> governments = null!;
        OwnedList<DifficultyValues> difficulties = null!;
        for (int i = 0; i < files.Count; ++i)
        {
            var file = files[i];
            ContentSource source;
            try
            {
                source = resolve(file.Path);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                throw new ContentException("source-error", file.Path, error.Message);
            }
            if (source.Identity.Length == 0 || !identities.Add(source.Identity))
            {
                throw new ContentException("source-alias", file.Path, "missing or duplicate backing identity");
            }

            byte[] bytes = source.Bytes.ToArray();
            if (ContentIdentity.Sha256(bytes) != file.Sha256)
            {
                throw new ContentException("hash-mismatch", file.Path, "exact file hash mismatch");
            }

            files[i] = file with { Identity = source.Identity };
            var component = ContentJson.Parse(bytes, file.Path);
            var reader = new ContentReader(component, file.Path);
            string name = GameplayNames.Components[i];
            reader.At("format").Constant("openciv2-" + name + "/1");
            switch (i)
            {
                case 0:
                    calendar = Calendar(reader);
                    break;
                case 1:
                    spaceship = Spaceship(reader);
                    break;
                case 2:
                    governments = Governments(reader);
                    break;
                case 3:
                    difficulties = Difficulties(reader);
                    break;
                case 4:
                    combat = Combat(reader);
                    break;
                case 5:
                    scoring = Scoring(reader);
                    break;
            }
            ((JsonObject)component!).Remove("extensions");
            values[name] = component;
        }
        var envelope = new JsonObject
        {
            ["format"] = "openciv2-gameplay-values/1",
            ["target"] = Target,
            ["components"] = values
        };
        string semantic = ContentIdentity.Sha256(ContentJson.CanonicalBytes(envelope));
        bool classic = semantic == ClassicSha256;
        if ((mode != "custom" || requireClassic) && !classic)
        {
            throw new ContentException(
                "classic-values-required",
                "gameplay.json/mode",
                "changed gameplay operands require custom mode and fail the classic-values gate");
        }

        return new(
            new(calendar, spaceship, governments, difficulties, combat, scoring),
            id,
            revision,
            ContentIdentity.Sha256(manifest),
            semantic,
            mode == "custom",
            classic,
            new(files));
    }

    private static readonly Lazy<GameplayBundle> BuiltinValue = new(() =>
    {
        var bundle = Load(
            BuiltinBytes("gameplay.json"),
            path => new("builtin:" + Target + "/1/" + path, new(BuiltinBytes(path))),
            true);
        if (bundle.Id != Target || bundle.Revision != "1")
        {
            throw new ContentException(
                "invalid-content",
                "builtin gameplay",
                "registered builtin identity/revision mismatch");
        }

        return bundle;
    });
    public static GameplayBundle Builtin => BuiltinValue.Value;

    public static byte[] BuiltinBytes(string name)
    {
        if (name != "gameplay.json" && !GameplayNames.Components.Any(component => name == component + ".json"))
        {
            throw new ContentException("source-error", name, "unknown builtin source");
        }

        using var stream = typeof(GameplayContent).Assembly.GetManifestResourceStream("OpenCiv2.Gameplay." + name)
            ?? throw new InvalidOperationException("Missing builtin gameplay resource");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
