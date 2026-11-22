using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.City;

public enum CityMoodNoticeKind { Disorder, Restored, Revolt, Celebration, CelebrationEnded }

/// <summary>C04 semantic information. Ruler-government snapshots let presentation resolve its title;
/// native text buffers, sounds and popup choreography are not simulation state (X01).</summary>
public sealed record CityMoodNotice(CitySlot City, int Owner, CityMoodNoticeKind Kind, Government Government);

public interface ICityMoodEffects
{
    void MoodNotice(CityMoodNotice notice);
    void RedrawCity(CitySlot city);
}

/// <summary>C04 city-turn disorder, tax and celebration, after the caller's second full C03 calculation.
/// AI production reconsideration remains a required C06 continuation.</summary>
public sealed class CityMoodTurn(GameState world, RuleTables rules, GovernmentChanges governments,
    Action<CitySlot> reconsiderProduction, ICityMoodEffects effects)
{
    private CityCalculationWorkspace Work => world.Calculation;
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    private int GovernmentOf(int owner) => unchecked((byte)world.Civilizations[owner].Government);

    public void Run(CitySlot slot, int foodSurplus)
    {
        ref var city = ref world.Entities.Cities.At(slot);
        int owner = unchecked((sbyte)city.Owner.Value);
        if (owner == 0)
        {
            return;
        }

        MapKnowledge.RequireLocal(world);
        int margin = unchecked(world.Mood.Happy - world.Mood.Unhappy);
        if (margin < 0)
        {
            if (world.Multiplayer.LocalHuman == owner && !world.Multiplayer.Saved.AutoplayActive
                && !world.Globals.MessageOptions.Disorder)
            {
                Notice(CityMoodNoticeKind.Disorder);
            }

            if (!city.Flags.Disorder)
            {
                city.Flags.Disorder = city.Flags.MoodOnEdge = true;
                if (!Human(owner))
                {
                    reconsiderProduction(slot);
                }

                effects.RedrawCity(slot);
            }
            else
            {
                if (!Human(owner))
                {
                    reconsiderProduction(slot);
                }

                if (city.Flags.SecondDisorderTurn)
                {
                    city.Flags.LongDisorder = true;
                }

                city.Flags.SecondDisorderTurn = true;
                if (Human(owner) && GovernmentOf(owner) == (int)Government.Democracy
                    && (!world.Globals.Status.Scenario || !world.Scenario.Flags.GovernmentLock))
                {
                    Notice(CityMoodNoticeKind.Revolt);
                    world.Civilizations[owner].Flags.Revolution = false;
                    governments.Change(owner, (int)Government.Anarchy);
                    world.Civilizations[owner].Flags.Revolution = true;
                }
            }
        }
        else
        {
            city.Flags.MoodOnEdge = world.Mood.Happy == world.Mood.Unhappy
                && unchecked(Work.Shields - Work.ShieldSupport) > 0 && foodSurplus >= 0;
            if (city.Flags.Disorder)
            {
                city.Flags.Disorder = city.Flags.SecondDisorderTurn = city.Flags.LongDisorder = false;
                if (!Human(owner))
                {
                    reconsiderProduction(slot);
                }
                else if (!world.Globals.MessageOptions.OrderRestored)
                {
                    Notice(CityMoodNoticeKind.Restored);
                }

                effects.RedrawCity(slot);
            }
            if (GovernmentOf(owner) != (int)Government.Anarchy)
            {
                world.Civilizations[owner].Gold = unchecked(world.Civilizations[owner].Gold + Work.Tax);
            }
        }

        bool qualifies = world.Mood.Unhappy == 0 && city.Size > 2
            && ((city.Size + 1) >> 1) <= world.Mood.Happy && GovernmentOf(owner) != (int)Government.Anarchy;
        if (qualifies)
        {
            if (!city.Flags.Celebrating)
            {
                if (Human(owner) && !world.Globals.MessageOptions.Celebration)
                {
                    Notice(CityMoodNoticeKind.Celebration);
                }

                city.Flags.Celebrating = true;
            }
            else if (GovernmentOf(owner) > (int)Government.Fundamentalism
                && unchecked(city.Size * rules.Cosmic.FoodPerCitizen + Work.SupportedSettlers * Work.SettlerFood) < Work.Food
                && new CitySupport(world, rules).GrowthFacilityNeeded(slot) is null)
            {
                city.Size = unchecked((sbyte)(city.Size + 1));
            }
        }
        else if (city.Flags.Celebrating)
        {
            if (Human(owner) && GovernmentOf(owner) != (int)Government.Anarchy && !city.Flags.Disorder
                && !world.Globals.MessageOptions.Celebration)
            {
                Notice(CityMoodNoticeKind.CelebrationEnded);
            }

            city.Flags.Celebrating = false;
        }

        void Notice(CityMoodNoticeKind kind) => effects.MoodNotice(new(slot, owner, kind, (Government)GovernmentOf(owner)));
    }
}
