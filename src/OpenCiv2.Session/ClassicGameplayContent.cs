using OpenCiv2.Base;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using ClassicReader = OpenCiv2.Formats.Classic.TextReader;

namespace OpenCiv2.Session;

/// <summary>F03/C01/V06 classic resources for the local gameplay graph. The host selects
/// directories, language, labels, media and map compatibility. Rebind after world/rule
/// replacement; retained mission operations own when their option text is read.</summary>
public sealed class ClassicGameplayContent(TextWorkspace workspace, TextDirectories directories,
    TextFiles files, TextCase comparison)
{
    private ClassicReader Reader() => new(workspace, directories, files, comparison);

    public SinglePlayerGameplayContent Load(RuleState rules, CityPreferences preferences,
        StartingPositions starts, GameplayValues values, MapCompatibility mapCompatibility,
        IScenarioMedia media, Func<OwnedList<string>, OwnedList<string>> prepareScenarioText,
        string foodName, string spyAnnotation, string missionFilename = "GAME",
        IReadOnlyDictionary<int, string>? extraCommodityNames = null,
        IReadOnlyDictionary<int, CivilizationSet>? extraAdvancePresence = null,
        WonderBuildRestrictions? wonderRestrictions = null)
    {
        NameCatalog names;
        using (var reader = Reader())
        {
            names = ClassicCityContent.LoadNames(reader, directories, comparison);
            preferences.Apply(ClassicCityContent.LoadPreferences(reader, rules, comparison));
        }

        CityMissionText? Missions()
        {
            using var reader = Reader();
            return ClassicCityMissionOptions.Read(reader, missionFilename, spyAnnotation);
        }
        RevoltOptionText? Revolt()
        {
            using var reader = Reader();
            return ClassicRevoltOptions.Read(reader, missionFilename);
        }

        return new(names, preferences, starts, values, mapCompatibility, media, prepareScenarioText,
            Missions, Revolt, foodName, extraCommodityNames, extraAdvancePresence, wonderRestrictions);
    }
}
