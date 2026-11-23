using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum ResearchAbility { Transform, Fortress, Airlift, Railroad, Farmland, Paradrop }
public sealed record ResearchCompletionNotice(int Civilization, int Advance, string Adjective, string Name, bool HasInvention, int? FutureNumber);
public sealed record ResearchAbilityNotice(int Civilization, int Advance, ResearchAbility Ability, int? Range = null);
public interface IResearchCompletionEffects
{
    int NoticeMode { get; }
    CitySlot? SelectedCity { get; }
    void Completed(ResearchCompletionNotice notice);
    void Encyclopedia(int advance);
    void Ability(ResearchAbilityNotice notice);
    void FirstCivilization(int civilization);
}

public abstract record ResearchDecision
{
    public sealed record Grant(ResearchGrantDecision Decision) : ResearchDecision;
}

/// <summary>A session-owned research operation with retained selection and acquisition decisions.
/// The owner admits replies and protects the world between calls.</summary>
public abstract class ResearchOperation : IDisposable
{
    public bool Finished { get; protected set; }
    public int? Result { get; protected set; }
    private bool faulted, disposed;
    private readonly List<Exception> presentationFailures = [];
    public IReadOnlyList<Exception> PresentationFailures => presentationFailures.AsReadOnly();
    protected void Report(IEnumerable<Exception> failures) => presentationFailures.AddRange(failures);
    public ResearchDecision? Resume(ResearchReply? reply = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        {
            throw new InvalidOperationException("A failed research operation cannot resume.");
        }

        try
        { return ResumeCore(reply); }
        catch { faulted = true; Release(); throw; }
    }
    protected abstract ResearchDecision? ResumeCore(ResearchReply? reply);
    protected virtual void Release() { }
    public void Dispose() { disposed = true; Release(); }
}

/// <summary>V03 §§8–9 full accumulation and completion, with real grant, production reset, city
/// recalculation and government children. Player choices suspend before later city/turn effects.</summary>
public sealed class ResearchFlow(GameState world, RuleState ruleState, GameplayRandom random,
    ResearchGrants grants, ResearchGovernmentPrompts governmentPrompts, ProductionOrders production,
    CityEconomy economy, IResearchChoiceEffects choiceEffects, IResearchCompletionEffects effects)
{
    private GameState World => world;
    private ResearchGrants Grants => grants;
    private ResearchGovernmentPrompts GovernmentPrompts => governmentPrompts;
    private ProductionOrders Production => production;
    private CityEconomy Economy => economy;
    private IResearchCompletionEffects Effects => effects;
    private RuleTables Rules => ruleState.Definitions;
    private bool Human(int c) => world.Globals.HumanPlayers.Contains(c & 31);
    private ResearchSelection Choose(int c, string call) => new(world, ruleState, random, choiceEffects, c, 0, call);
    public ResearchOperation Complete(int c, int source, string call) => new Completion(this, c, source, call);
    public ResearchOperation AddPoints(int c, int points, string call) => new Accumulation(this, c, points, call);

    private sealed class Completion(ResearchFlow flow, int c, int source, string call) : ResearchOperation
    {
        private enum Phase { Begin, InitialChoice, BeginGrant, Grant, FinalChoice, Government, Done }
        private Phase phase;
        private ResearchSelection? selection;
        private ResearchGrants.Operation? grant;
        private ResearchGovernmentPrompts.Operation? government;
        private int completed;

        protected override ResearchDecision? ResumeCore(ResearchReply? reply)
        {
            MapKnowledge.RequireLocal(flow.World);
            if (Finished)
            {
                return null;
            }

            while (true)
            {
                switch (phase)
                {
                    case Phase.Begin:
                        completed = flow.World.Civilizations[c].Researching;
                        if (completed < 0)
                        {
                            selection = flow.Choose(c, call + "/initial");
                            phase = Phase.InitialChoice;
                        }
                        else
                        {
                            phase = Phase.BeginGrant;
                        }

                        break;
                    case Phase.InitialChoice:
                        if (selection!.Resume(reply) is { } initial)
                        {
                            return initial;
                        }

                        reply = null;
                        completed = flow.World.Civilizations[c].Researching;
                        phase = completed < 0 ? Phase.Done : Phase.BeginGrant;
                        break;
                    case Phase.BeginGrant:
                        if (flow.Effects.NoticeMode != 2)
                        {
                            flow.World.Civilizations[c].ResearchProgress = 0;
                        }

                        flow.World.Civilizations[c].Researching = -1;
                        grant = flow.Grants.Begin(c, completed, source, false, call + "/grant");
                        phase = Phase.Grant;
                        break;
                    case Phase.Grant:
                        if (grant!.Resume(reply?.Selected) is { } acquisition)
                        { return new ResearchDecision.Grant(acquisition); }
                        reply = null;
                        if (!grant.Completed)
                        { return null; }
                        Report(grant.PresentationFailures);
                        grant.Dispose();
                        grant = null;
                        if (flow.World.Globals.Turn == 0 || !flow.Human(c))
                        {
                            var selected = flow.Effects.SelectedCity;
                            flow.Production.ResetCivilization(c);
                            if (flow.World.Globals.Turn != 0 && selected is { } slot && slot.Value < flow.World.Entities.Cities.End
                                && flow.World.Entities.Cities.At(slot).Token.Occupied && flow.World.Entities.Cities.At(slot).Owner.Value == c)
                            {
                                flow.Economy.Calculate(slot, false);
                            }
                        }
                        else if (c == flow.World.Multiplayer.LocalHuman && !flow.World.Multiplayer.Saved.AutoplayActive)
                        {
                            flow.LocalNotices(c, completed, call);
                            if (flow.Human(c) && flow.World.Globals.Turn != 0)
                            {
                                selection = flow.Choose(c, call + "/next");
                                phase = Phase.FinalChoice;
                                break;
                            }
                        }
                        phase = Phase.Done;
                        break;
                    case Phase.FinalChoice:
                        if (selection!.Resume(reply) is { } next)
                        {
                            return next;
                        }

                        reply = null;
                        government = flow.GovernmentPrompts.Begin(c, completed);
                        phase = Phase.Government;
                        break;
                    case Phase.Government:
                        if (government!.Resume(reply?.Selected) is { } change)
                        { return new ResearchDecision.Grant(new ResearchGrantDecision.Government(change)); }
                        reply = null;
                        government.Dispose();
                        government = null;
                        phase = Phase.Done;
                        break;
                    default:
                        Result = completed;
                        Finished = true;
                        return null;
                }
            }
        }
        protected override void Release() { grant?.Dispose(); government?.Dispose(); }
    }

    private void LocalNotices(int c, int completed, string call)
    {
        effects.Completed(new(c, completed, world.Names[c].Adjective, Rules.Advances[completed].Identity.Name,
            CivilizationKnowledge.KnowsAdvance(world.Civilizations, c, AdvanceIds.Invention),
            completed == CivilizationKnowledge.FutureTechnology ? world.Civilizations[c].FutureTechs : null));
        if (world.Globals.GameOptions.AdvanceCivilopedia)
        {
            effects.Encyclopedia(completed);
        }

        if (completed == AdvanceIds.Philosophy && world.Civilizations[c].Flags.PendingFreeAdvance
            && c == world.Multiplayer.LocalHuman && !world.Multiplayer.Saved.AutoplayActive)
        {
            grants.ShowGoldenAge(c, call + "/philosophy");
        }

        if ((int)Rules.Units[UnitTypeIds.Engineers].Prerequisite == completed)
        {
            effects.Ability(new(c, completed, ResearchAbility.Transform));
        }

        if (completed == AdvanceIds.Construction)
        {
            effects.Ability(new(c, completed, ResearchAbility.Fortress));
        }

        if ((int)Rules.Improvements[BuildingIds.Airport].Prerequisite == completed)
        {
            effects.Ability(new(c, completed, ResearchAbility.Airlift));
        }

        if (completed == AdvanceIds.Railroad)
        {
            effects.Ability(new(c, completed, ResearchAbility.Railroad));
        }

        if ((int)Rules.Improvements[BuildingIds.Supermarket].Prerequisite == completed)
        {
            effects.Ability(new(c, completed, ResearchAbility.Farmland));
        }

        if ((int)Rules.Units[UnitTypeIds.Paratroopers].Prerequisite == completed)
        {
            effects.Ability(new(c, completed, ResearchAbility.Paradrop, Rules.Cosmic.ParadropRange));
        }
    }

    private sealed class Accumulation(ResearchFlow flow, int c, int points, string call) : ResearchOperation
    {
        private enum Phase { Begin, Threshold, FreeCheck, Free, ChoiceCheck, Choice, Done }
        private Phase phase;
        private ResearchOperation? completion;
        private ResearchSelection? selection;

        protected override ResearchDecision? ResumeCore(ResearchReply? reply)
        {
            if (Finished)
            {
                return null;
            }

            while (true)
            {
                switch (phase)
                {
                    case Phase.Begin:
                        if (points <= 0)
                        { phase = Phase.Done; break; }
                        MapKnowledge.RequireLocal(flow.World);
                        flow.World.Civilizations[c].ResearchProgress = unchecked((short)(flow.World.Civilizations[c].ResearchProgress + (short)points));
                        if (flow.World.Civilizations[c].Researching < 0)
                        {
                            phase = Phase.ChoiceCheck;
                        }
                        else if (Research.Cost(flow.World, flow.Rules, c) <= flow.World.Civilizations[c].ResearchProgress)
                        {
                            completion = flow.Complete(c, 0, call + "/threshold");
                            phase = Phase.Threshold;
                        }
                        else
                        {
                            phase = Phase.FreeCheck;
                        }

                        break;
                    case Phase.Threshold:
                    case Phase.Free:
                        if (completion!.Resume(reply) is { } pending)
                        {
                            return pending;
                        }

                        reply = null;
                        if (!completion.Finished)
                        { return null; }
                        Report(completion.PresentationFailures);
                        completion.Dispose();
                        completion = null;
                        phase = phase == Phase.Threshold ? Phase.FreeCheck : Phase.ChoiceCheck;
                        break;
                    case Phase.FreeCheck:
                        if (flow.World.Civilizations[c].Flags.PendingFreeAdvance)
                        {
                            flow.World.Civilizations[c].Flags.PendingFreeAdvance = false;
                            completion = flow.Complete(c, 0, call + "/free");
                            phase = Phase.Free;
                        }
                        else
                        {
                            phase = Phase.ChoiceCheck;
                        }

                        break;
                    case Phase.ChoiceCheck:
                        if (flow.World.Civilizations[c].ResearchProgress != 0 && flow.World.Civilizations[c].Researching < 0)
                        {
                            if (flow.World.Globals.GameOptions.Tutorial && flow.World.Civilizations[c].NumAdvances < 2 && flow.Human(c))
                            {
                                flow.Effects.FirstCivilization(c);
                            }

                            selection = flow.Choose(c, call + "/choose");
                            phase = Phase.Choice;
                        }
                        else
                        {
                            phase = Phase.Done;
                        }

                        break;
                    case Phase.Choice:
                        if (selection!.Resume(reply) is { } choice)
                        {
                            return choice;
                        }

                        reply = null;
                        phase = Phase.Done;
                        break;
                    default:
                        Finished = true;
                        return null;
                }
            }
        }
        protected override void Release() => completion?.Dispose();
    }
}
