using OpenCiv2.Base;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>S02 media ports. The platform resolves classic paths and plays the selected supported media.</summary>
public interface IScenarioMedia
{
    string ScenarioDirectory { get; }
    string HomeDirectory { get; }
    bool WaveExists(string path);
    void PlayWave(string path);
    int CdTrackCount();
    void CloseCd();
    void PlayCdTrack(int track, bool forced);
}

public sealed record ScenarioText(int Recipient, OwnedList<string> Lines) : LifecycleDecision;

/// <summary>S02 §10 local presentation actions. prepareText owns P04 authored substitutions with
/// default dialog inputs; suppressWonders publishes the S01 presentation request, not a gameplay change.</summary>
public sealed class ScenarioPresentationActions(GameState world, IScenarioMedia media,
    Func<OwnedList<string>, OwnedList<string>> prepareText, Action suppressWonders)
{
    public const int WaveNameField = 34;
    public int Wave(Event entry)
    {
        MapKnowledge.RequireLocal(world);
        if (!world.Globals.GameOptions.Sound)
        { return 0; }
        if (entry.Strings[WaveNameField].Present)
        {
            string path = media.ScenarioDirectory + "\\SOUND\\" + entry.Strings[WaveNameField].Value;
            if (media.WaveExists(path))
            { media.PlayWave(path); }
            else
            {
                path = media.HomeDirectory + "\\SOUND\\" + entry.Strings[WaveNameField].Value;
                if (media.WaveExists(path))
                { media.PlayWave(path); }
            }
        }
        return 1;
    }
    public int Cd(Event entry)
    {
        MapKnowledge.RequireLocal(world);
        int count = 0;
        for (int attempt = 0; attempt < 10; ++attempt)
        {
            count = media.CdTrackCount();
            if (count is 10 or 12 or 18 or 24)
            { break; }
            media.CloseCd();
        }
        if (count is not (10 or 12 or 18 or 24))
        { return 0; }
        int track = entry.Values.CdTrack;
        if (track < 2 || track > count)
        { return 0; }
        media.PlayCdTrack(track, true);
        return 1;
    }
    public ScenarioText? Text(Event entry)
    {
        MapKnowledge.RequireLocal(world);
        var lines = new List<string>();
        for (int field = Event.TextBegin; field < Event.OtherBegin; ++field)
        {
            if (entry.Strings[field].Present)
            { lines.Add(entry.Strings[field].Value); }
        }
        var prepared = prepareText(new(lines));
        return world.Multiplayer.Saved.AutoplayActive ? null : new(world.Multiplayer.LocalHuman, prepared);
    }
    public void SuppressWonders() { MapKnowledge.RequireLocal(world); suppressWonders(); }
}
