using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum ResearchUnlockKind { Improvement, Unit }
public sealed record ResearchUnlock(ResearchUnlockKind Kind, int Identity, string Name);
public sealed record AdvanceAcquisitionNotice(int Civilization, int Advance, int Source, string CivilizationName,
    string AdvanceName, string? SourceName, bool IncludeImage, OwnedList<ResearchUnlock> Unlocks);
public sealed record BarracksSaleNotice(int Civilization, int Advance, int Count, int Value, bool IncludeImages);
public sealed record GoldenAgeNotice(int Civilization, CityTarget City, string Adjective, string CityName);
public sealed record WonderExpiryNotice(int Advance, int Wonder, CitySlot City, int Owner, bool IncludeImages);

/// <summary>V03 semantic notices and required emissary decision. Diplomacy reset is an owning child,
/// not a fabricated audience; the grant does not implement full V04/V05 negotiations.</summary>
public interface IResearchGrantEffects
{
    bool DiplomacyActive { get; }
    void Acquired(AdvanceAcquisitionNotice notice);
    bool ReceiveEmissary(int civilization);
    void EmissaryKnowledge(int civilization, int advance);
    void ResetDiplomacy();
    void Encyclopedia(int advance);
    void BarracksSold(BarracksSaleNotice notice);
    void GoldenAge(GoldenAgeNotice notice);
    void WonderExpired(WonderExpiryNotice notice);
    void RefreshStatus();
}

public abstract record ResearchGrantDecision
{
    public sealed record Government(ResearchGovernmentChoice Choice) : ResearchGrantDecision;
    public sealed record Emissary(EmissaryDecision Decision) : ResearchGrantDecision;
}

/// <summary>V03 §10 locally authorized grants and first-discovery effects. Composes actual C08
/// Leonardo/ownership/storage and recursively completes Great Library grants. Government/tutorial
/// prompts remain an explicit owning child; network authority and publication are not supplied here.</summary>
public sealed class ResearchGrants(GameState world, RuleTables rules, GameplayRandom random, WonderEffects wonders,
    Action<int, int> governmentPrompts, IResearchGrantEffects effects)
{
    private readonly ResearchGovernmentPrompts? retainedGovernments;
    private readonly EmissaryReception? retainedEmissary;
    private GameState World => world;

    /// <summary>Production retained composition. Use Begin so real government and emissary choices
    /// can suspend without repeating the stored advance or its earlier effects.</summary>
    public ResearchGrants(GameState world, RuleTables rules, GameplayRandom random, WonderEffects wonders,
        ResearchGovernmentPrompts governments, EmissaryReception emissary, IResearchGrantEffects effects)
        : this(world, rules, random, wonders, governments.Run, effects)
    { retainedGovernments = governments; retainedEmissary = emissary; }

    public Operation Begin(int civilization, int advance, int source, bool skipPedia, string call)
    {
        if (retainedGovernments is null || retainedEmissary is null)
        { throw new InvalidOperationException("Retained research grants require government and emissary services"); }
        return new(this, civilization, advance, source, skipPedia, call, true);
    }
    public sealed class Operation : IDisposable
    {
        private readonly ResearchGrants parent;
        private readonly IEnumerator<ResearchGrantDecision?> steps;
        private readonly List<Exception> failures = [];
        private int? reply;
        private bool faulted, disposed;
        internal bool Retained { get; }
        public bool Completed { get; private set; }
        public ResearchGrantDecision? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(ResearchGrants parent, int civilization, int advance, int source, bool skipPedia, string call, bool retained)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            Retained = retained;
            steps = parent.Execute(this, civilization, advance, source, skipPedia, call).GetEnumerator();
        }
        internal int? TakeReply() { var answer = reply; reply = null; return answer; }
        internal void Report(IEnumerable<Exception> errors) => failures.AddRange(errors);
        internal void Report(Exception? error)
        {
            if (error is not null)
            {
                failures.Add(error);
            }
        }
        public ResearchGrantDecision? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("A failed research grant cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                { return Pending; }
                reply = Pending is null ? null : answer;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                Completed = true;
                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }

    private bool Human(int c) => world.Globals.HumanPlayers.Contains(c & 31);
    private bool Knows(int c, int a) => CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, a);
    private bool Wonder(int c, int w) => BuildingQueries.HasWonder(world, rules, c, w);

    /// <summary>Synchronous compatibility entry for callers supplying synchronous decision adapters.
    /// Retained service graphs must use Begin; they cannot silently answer a human prompt.</summary>
    public void Grant(int civilization, int advance, int source, bool skipPedia, string call)
    {
        if (retainedGovernments is not null)
        { throw new InvalidOperationException("Use Begin for retained research grants"); }
        using var operation = new Operation(this, civilization, advance, source, skipPedia, call, false);
        while (!operation.Completed)
        {
            if (operation.Resume() is not null)
            { throw new InvalidOperationException("A synchronous grant cannot suspend for a decision"); }
        }
    }
    private IEnumerable<ResearchGrantDecision?> Execute(Operation operation, int civilization, int advance, int source, bool skipPedia, string call)
    {
        bool first = Research.StoreAdvance(world, civilization, advance, source);
        if (world.Globals.Turn == 0)
        {
            yield break;
        }

        world.Civilizations[civilization].NumAdvances = unchecked((byte)(world.Civilizations[civilization].NumAdvances + 1));
        if (ShouldAnnounce(civilization, source))
        {
            Announce(civilization, advance, source);
        }
        else if (!world.Multiplayer.Saved.AutoplayActive && !Human(civilization) && first
            && random.SecondaryRange(new ChoiceKey("V03.emissary", call), 0, 2) == 0
            && world.Civilizations[civilization].Treaties[world.Multiplayer.LocalHuman].Contact
            && !world.Civilizations[civilization].Treaties[world.Multiplayer.LocalHuman].Alliance
            && !world.Civilizations[world.Multiplayer.LocalHuman].Treaties[civilization].Embassy
            && !Wonder(world.Multiplayer.LocalHuman, WonderIds.UnitedNations)
            && !Wonder(world.Multiplayer.LocalHuman, WonderIds.MarcoPolo))
        {
            if (operation.Retained)
            {
                using var reception = retainedEmissary!.Begin(world.Multiplayer.LocalHuman, civilization, null, call + "/emissary");
                while (reception.Accepted is null)
                {
                    if (reception.Resume(operation.TakeReply()) is { } question)
                    { yield return new ResearchGrantDecision.Emissary(question); }
                }
                operation.Report(reception.PresentationFailures);
                if (reception.Accepted == true)
                { effects.EmissaryKnowledge(civilization, advance); }
                operation.Report(retainedEmissary.Reset());
            }
            else
            {
                if (effects.ReceiveEmissary(civilization))
                { effects.EmissaryKnowledge(civilization, advance); }
                effects.ResetDiplomacy();
            }
        }

        if (Human(civilization) && civilization == world.Multiplayer.LocalHuman && source != 0 && !world.Multiplayer.Saved.AutoplayActive)
        {
            if (!effects.DiplomacyActive && world.Globals.GameOptions.AdvanceCivilopedia && !skipPedia)
            {
                effects.Encyclopedia(advance);
            }

            if (operation.Retained)
            {
                using var government = retainedGovernments!.Begin(civilization, advance);
                while (!government.Completed)
                {
                    if (government.Resume(operation.TakeReply()) is { } question)
                    { yield return new ResearchGrantDecision.Government(question); }
                }
            }
            else
            { governmentPrompts(civilization, advance); }
        }

        int mobile = (int)RuleNormalization.FollowDisabled((AdvanceReference)AdvanceIds.MobileWarfare, rules.Advances);
        if (advance == AdvanceIds.Gunpowder || advance == mobile)
        {
            SellBarracks(civilization, advance);
        }

        if (first)
        {
            if (advance == AdvanceIds.Philosophy)
            {
                world.Civilizations[civilization].Flags.PendingFreeAdvance = true;
                if (!world.Multiplayer.Saved.AutoplayActive && (world.Multiplayer.LocalHuman != civilization || source != 0))
                {
                    ShowGoldenAge(civilization, call + "/philosophy");
                }
            }
            world.Globals.FirstDiscoverer[advance] = unchecked((byte)(world.Globals.FirstDiscoverer[advance] | (byte)civilization));
            if (!world.Multiplayer.Saved.AutoplayActive)
            {
                for (int w = 0; w < RuleLimits.Wonders; ++w)
                {
                    if ((int)rules.WonderObsolescence[w] == advance && world.Globals.WonderCity[w] >= 0)
                    {
                        var slot = new CitySlot((ushort)world.Globals.WonderCity[w]);
                        int owner = unchecked((sbyte)world.Entities.Cities.At(slot).Owner.Value);
                        if (Human(owner))
                        {
                            effects.WonderExpired(new(advance, w, slot, owner, !effects.DiplomacyActive));
                        }
                    }
                }
            }
        }
        wonders.UpgradeForLeonardo(civilization);
        if (BuildingQueries.ActiveOwner(world, rules, WonderIds.GreatLibrary) is > 0 and var library && !Knows(library, advance))
        {
            int count = 0;
            for (int c = 1; c < EntityLimits.Civilizations; ++c)
            {
                if (Knows(c, advance))
                {
                    ++count;
                }
            }

            if (count > 1)
            {
                using var child = new Operation(this, library, advance, -2, false, call + "/library/" + library, operation.Retained);
                while (!child.Completed)
                {
                    if (child.Resume(operation.TakeReply()) is { } question)
                    { yield return question; }
                    else if (!child.Completed)
                    { yield return null; }
                }
                operation.Report(child.PresentationFailures);
            }
        }
    }

    private bool ShouldAnnounce(int c, int source)
    {
        if (world.Multiplayer.Saved.AutoplayActive)
        {
            return false;
        }

        int local = world.Multiplayer.LocalHuman;
        if (local == c)
        {
            return source < 0 || source == c && !world.Globals.GameOptions.AdvanceCivilopedia;
        }

        bool observed = world.Civilizations[local].Treaties[c].Embassy || Wonder(local, WonderIds.UnitedNations)
            || Wonder(local, WonderIds.MarcoPolo) || world.Civilizations[local].Treaties[c].Alliance || world.Globals.RevealMap != 0;
        return observed && world.Globals.CivsAlive.Contains(c & 31);
    }

    private void Announce(int c, int advance, int source)
    {
        var unlocks = new List<ResearchUnlock>();
        if (c != world.Multiplayer.LocalHuman || !Human(world.Multiplayer.LocalHuman))
        {
            for (int i = 0; i < RuleLimits.Improvements; ++i)
            {
                if ((int)rules.Improvements[i].Prerequisite == advance)
                {
                    unlocks.Add(new(ResearchUnlockKind.Improvement, i, rules.Improvements[i].Identity.Name));
                }
            }

            for (int i = 0; i < RuleLimits.UnitTypes; ++i)
            {
                if ((int)rules.Units[i].Prerequisite == advance)
                {
                    unlocks.Add(new(ResearchUnlockKind.Unit, i, rules.Units[i].Identity.Name));
                }
            }
        }
        string? from = source == 0 || source == c ? null : source < 0
            ? rules.Improvements[39 + WonderIds.GreatLibrary].Identity.Name : world.Names[source].Tribe;
        effects.Acquired(new(c, advance, source, world.Names[c].Tribe, rules.Advances[advance].Identity.Name, from,
            !effects.DiplomacyActive, new(unlocks)));
    }

    private void SellBarracks(int c, int advance)
    {
        int count = 0;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (!city.Token.Occupied || unchecked((sbyte)city.Owner.Value) != unchecked((byte)c))
            {
                continue;
            }

            var slot = new CitySlot((ushort)i);
            if (BuildingQueries.HasBuilding(world, slot, BuildingIds.Barracks))
            {
                ++count;
            }

            BuildingQueries.SetBuilding(world, slot, BuildingIds.Barracks, false);
        }
        if (count == 0)
        {
            return;
        }

        int value = unchecked(rules.Improvements[BuildingIds.Barracks].CostRows * rules.Cosmic.ShieldBoxRows * count);
        if (Human(c))
        {
            effects.BarracksSold(new(c, advance, count, value, !effects.DiplomacyActive));
        }

        world.Civilizations[c].Gold = unchecked(world.Civilizations[c].Gold + value);
        if (c == world.Multiplayer.LocalHuman)
        {
            effects.RefreshStatus();
        }
    }

    /// <summary>V03 first-discovery/completion notice selection; this does not change city happiness.</summary>
    public void ShowGoldenAge(int c, string call)
    {
        MapKnowledge.RequireLocal(world);
        string adjective = world.Names[c].Adjective;
        CitySlot? selected = null;
        int best = 0;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (!city.Token.Occupied || unchecked((sbyte)city.Owner.Value) != c)
            {
                continue;
            }

            var slot = new CitySlot((ushort)i);
            int weight = city.Size;
            if (BuildingQueries.HasBuilding(world, slot, BuildingIds.Palace))
            {
                weight *= 2;
            }

            int score = weight <= 1 ? 1 : random.PrimaryModulo(new ChoiceKey("V03.golden-age", call, i), weight) + 1;
            if (score >= best)
            { best = score; selected = slot; }
        }
        if (selected is { } chosen)
        {
            ref var city = ref world.Entities.Cities.At(chosen);
            effects.GoldenAge(new(c, new(chosen, city.Token), adjective, city.Name));
        }
    }
}
