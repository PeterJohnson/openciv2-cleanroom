using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>Semantic local AI announcement. Presentation resolves names and the old ruler title
/// before the transition; Overthrown uses the old portrait, Changed the requested portrait.</summary>
public sealed record GovernmentChangeNotice(int Civilization, int OldGovernment, int NewGovernment, bool Overthrown);

/// <summary>V02 §§2–4 local government queries and transitions with actual C03 recalculation.
/// Restricted player selection and rate decisions are required continuations, not implicit choices.</summary>
public sealed class GovernmentChanges(GameState world, RuleState rules, StackOperations stacks,
    Action<int> selectGovernment, Action<int> showRates, Action<GovernmentChangeNotice> announce)
{
    private readonly CityEconomy economy = new(world, rules, stacks);
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    private int GovernmentOf(int owner) => unchecked((byte)world.Civilizations[owner].Government);
    private bool Wonder(int owner, int wonder) => BuildingQueries.HasWonder(world, rules.Definitions, owner, wonder);

    public int MaximumRate(int owner) => GovernmentOf(owner) switch { < 2 => 6, 2 => 7, < 6 => 8, _ => 10 };

    public bool Available(int owner, int government)
    {
        bool liberty = Wonder(owner, WonderIds.StatueOfLiberty);
        int advance = government switch
        {
            (int)Government.Monarchy => AdvanceIds.Monarchy,
            (int)Government.Communism => AdvanceIds.Communism,
            (int)Government.Fundamentalism => AdvanceIds.Fundamentalism,
            (int)Government.Republic => AdvanceIds.Republic,
            (int)Government.Democracy => AdvanceIds.Democracy,
            _ => -1
        };
        if (advance < 0)
        {
            return true;
        }

        bool known = CivilizationKnowledge.KnowsAdvance(world.Civilizations, owner, advance);
        return (known || liberty) && (government != (int)Government.Fundamentalism || rules.Definitions.Advances[advance].Enabled);
    }

    public void Set(int owner, int government)
    {
        MapKnowledge.RequireLocal(world);
        int old = GovernmentOf(owner);
        world.Civilizations[owner].Government = (Government)unchecked((byte)government);
        if (GovernmentOf(owner) != old)
        {
            if (GovernmentOf(owner) == (int)Government.Anarchy || !Human(owner))
            {
                for (int other = 0; other < EntityLimits.Civilizations; ++other)
                {
                    world.Civilizations[other].Treaties[owner].Vendetta = false;
                }
            }

            if (Human(owner))
            {
                world.Globals.TransientFlags.AttitudeDirty = false;
                for (int i = 0; i < world.Entities.Cities.End; ++i)
                {
                    ref var city = ref world.Entities.Cities.Storage[i];
                    if (!city.Token.Occupied || unchecked((sbyte)city.Owner.Value) != owner)
                    {
                        continue;
                    }

                    economy.Calculate(new((ushort)i));
                    if (city.Production == UnitTypeIds.Fanatics && GovernmentOf(owner) != (int)Government.Fundamentalism)
                    {
                        city.Production = UnitTypeIds.Riflemen;
                    }
                }
            }
        }
        if (Human(owner) && government > 0 && !world.Multiplayer.Saved.AutoplayActive)
        {
            showRates(owner);
        }
    }

    public void Change(int owner, int government)
    {
        MapKnowledge.RequireLocal(world);
        int old = GovernmentOf(owner);
        if (government != 0)
        {
            world.Civilizations[owner].Flags.GovernmentChanged = true;
        }

        if (Human(owner))
        {
            if (government != 0)
            {
                selectGovernment(owner);
                return;
            }
        }
        else if (old != government || world.Civilizations[owner].Flags.Revolution)
        {
            int observer = world.Multiplayer.LocalHuman;
            if (world.Civilizations[observer].Treaties[owner].Embassy
                || Wonder(observer, WonderIds.UnitedNations) || Wonder(observer, WonderIds.MarcoPolo)
                || world.Globals.RevealMap != 0)
            {
                if (!world.Multiplayer.Saved.AutoplayActive)
                {
                    bool overthrown = !world.Civilizations[owner].Flags.Revolution;
                    announce(new(owner, old, government, overthrown));
                    world.Civilizations[owner].Flags.Revolution = overthrown;
                }
                int rank = government < 4 ? government : unchecked(government - 1);
                world.Civilizations[owner].TaxRate = unchecked((byte)(4 - (rank >> 1)));
            }
        }
        Set(owner, government);
    }
}
