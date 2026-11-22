using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed record SpaceshipPartChoice(int Civilization, int Group, int FirstCount, int SecondCount, int ThirdCount);
public sealed record SpaceshipLaunchNotice(int Civilization, string Plural, int ArrivalYear);

/// <summary>Human construction choices and semantic launch presentation. The window receives a
/// negative civilization for an already-launched AI ship; it must not launch again.</summary>
public interface ISpaceshipConstructionEffects
{
    int ChoosePart(SpaceshipPartChoice choice);
    void NoFurther(int civilization, int part);
    void Launched(SpaceshipLaunchNotice notice);
    void ShowShip(int civilization);
}

/// <summary>V07 §§5–6 local assembly and launch, with actual performance/calendar and V04 relation
/// mutations. The A02/C06 city-selection policy and player presentation remain required children.</summary>
public sealed class SpaceshipConstruction(GameState world, RuleTables rules, SpaceshipValues values,
    CalendarValues calendar, Action<CitySlot> reconsiderProduction, ISpaceshipConstructionEffects effects)
{
    private readonly SpaceshipPerformance performance = new(world, rules, values, calendar);
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    private int Count(int owner, int part) => world.Civilizations[owner].SpaceshipParts[part];
    private bool Maxed(int owner, int part) => Count(owner, part) >= values.Parts[part].MaximumCount;

    public int AddPart(int owner, int group)
    {
        MapKnowledge.RequireLocal(world);
        int selected = -1;
        performance.Calculate(owner, false);
        if (world.Civilizations[owner].SpaceshipFlags.Launched)
        {
            return selected;
        }

        while (true)
        {
            switch (group)
            {
                case 0:
                    if (Maxed(owner, 0))
                    {
                        return selected;
                    }

                    selected = 0;
                    break;
                case 1:
                    if (Maxed(owner, 1) && Maxed(owner, 2))
                    {
                        return selected;
                    }

                    selected = Human(owner)
                        ? unchecked(effects.ChoosePart(new(owner, group, Count(owner, 1), Count(owner, 2), 0)) + 1)
                        : Count(owner, 1) < Count(owner, 2) ? 1 : 2;
                    break;
                default:
                    if (Maxed(owner, 3) && Maxed(owner, 4) && Maxed(owner, 5))
                    {
                        return selected;
                    }

                    if (Human(owner))
                    {
                        selected = unchecked(effects.ChoosePart(new(owner, 2, Count(owner, 3), Count(owner, 4), Count(owner, 5))) + 3);
                    }
                    else
                    {
                        int minimum = 999;
                        for (int p = 3; p < 6; ++p)
                        {
                            if (Count(owner, p) < minimum)
                            { minimum = Count(owner, p); selected = p; }
                        }
                    }
                    break;
            }
            if (!Human(owner))
            {
                break;
            }

            if (!Maxed(owner, selected))
            {
                break;
            }

            effects.NoFurther(owner, selected);
        }
        if (!world.Civilizations[owner].SpaceshipFlags.Exists)
        {
            world.Civilizations[owner].SpaceshipFlags.Exists = true;
            for (int human = 1; human < EntityLimits.Civilizations; ++human)
            {
                if (Human(human) && (human == owner || !world.Civilizations[human].SpaceshipFlags.Exists))
                {
                    for (int other = 1; other < EntityLimits.Civilizations; ++other)
                    {
                        world.Civilizations[human].LastContactTurn[other] = 0;
                    }
                }
            }
        }
        // A custom reply or the AI's retained minimum may leave no backed subtype. Stop before
        // the invalid count access, retaining the earlier first-ship/contact mutations.
        if (selected is < 0 or >= 6)
        {
            throw new ArgumentOutOfRangeException(nameof(group), "Selected spaceship part has no backing");
        }

        world.Civilizations[owner].SpaceshipParts[selected] = unchecked((short)(Count(owner, selected) + 1));
        performance.Calculate(owner, false);
        if (Human(owner))
        {
            return selected;
        }

        bool enginesFull = true;
        for (selected = 0; selected < 3; ++selected)
        {
            if (!Maxed(owner, selected))
            {
                enginesFull = false;
            }
        }

        if (world.Civilizations[owner].SpaceshipFlags.Launched || world.Spaceship.Success < 40)
        {
            return selected;
        }

        if (enginesFull)
        { Launch(owner); return selected; }
        int earliest = 0;
        for (int human = 1; human < EntityLimits.Civilizations; ++human)
        {
            if (Human(human) && world.Civilizations[human].SpaceshipFlags.Launched
                && (earliest == 0 || world.Civilizations[human].SpaceshipArrivalYear < world.Civilizations[earliest].SpaceshipArrivalYear))
            {
                earliest = human;
            }
        }

        if (earliest == 0 && world.Globals.Difficulty > Difficulty.King)
        {
            for (int other = 1; other < EntityLimits.Civilizations; ++other)
            {
                if (other != owner && !Human(other) && world.Civilizations[other].SpaceshipFlags.Launched
                    && world.Civilizations[other].SpaceshipArrivalYear < world.Civilizations[0].SpaceshipArrivalYear)
                {
                    return selected;
                }
            }
        }

        if (earliest != 0)
        {
            if (world.Civilizations[owner].SpaceshipArrivalYear < world.Civilizations[earliest].SpaceshipArrivalYear)
            {
                Launch(owner);
            }
        }
        else if (world.Spaceship.Success > 75)
        {
            Launch(owner);
        }
        else
        {
            for (int human = 1; human < EntityLimits.Civilizations; ++human)
            {
                if (Human(human) && (world.Globals.PowerRank[owner] < world.Globals.PowerRank[human]
                    || (world.Civilizations[human].SpaceshipFlags.Exists && world.Civilizations[human].Gold > 999)))
                { Launch(owner); break; }
            }
        }

        return selected;
    }

    public void Launch(int owner)
    {
        MapKnowledge.RequireLocal(world);
        world.Civilizations[owner].SpaceshipFlags.Launched = true;
        if (world.Globals.FirstLaunchTurn < 0)
        {
            world.Globals.FirstLaunchTurn = world.Globals.Turn;
        }

        var notice = new SpaceshipLaunchNotice(owner, world.Names[owner].Tribe, world.Civilizations[owner].SpaceshipArrivalYear);
        if (!Human(owner))
        {
            effects.Launched(notice);
            effects.ShowShip(unchecked(-owner));
        }
        if (!Human(owner))
        {
            for (int i = 0; i < world.Entities.Cities.End; ++i)
            {
                if (world.Entities.Cities.Storage[i].Token.Occupied && unchecked((sbyte)world.Entities.Cities.Storage[i].Owner.Value) == owner)
                {
                    reconsiderProduction(new((ushort)i));
                }
            }
        }
        else
        {
            for (int other = 1; other < EntityLimits.Civilizations; ++other)
            {
                if (other == owner || Human(other) || !world.Civilizations[other].SpaceshipFlags.Launched
                    || world.Civilizations[owner].SpaceshipArrivalYear >= world.Civilizations[other].SpaceshipArrivalYear)
                {
                    continue;
                }

                if (world.Globals.Difficulty < Difficulty.Emperor)
                {
                    if (world.Globals.Difficulty < Difficulty.Prince || world.Civilizations[owner].Reputation == 0)
                    {
                        DiplomaticRelations.Set(world, owner, other, new() { Incident = true });
                    }
                    else
                    {
                        world.Civilizations[other].Treaties[owner].HostileIntent = true;
                    }
                }
                else if (!world.Civilizations[owner].Treaties[other].Alliance)
                {
                    ref var reverse = ref world.Civilizations[other].Treaties[owner];
                    reverse.Ceasefire = reverse.Peace = reverse.HostileIntent = false;
                    reverse.BrokenTreaty = reverse.Aggressor = reverse.ExtendedAggressor = true;
                }
                else
                {
                    world.Civilizations[other].Treaties[owner].HostileIntent = true;
                }
            }
        }
    }
}
