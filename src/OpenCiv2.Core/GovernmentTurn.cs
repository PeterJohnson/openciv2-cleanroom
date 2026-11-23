using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>V02 §6 resumable government prefix. Each requested change must finish its
/// selection/rate children before acknowledgment. The caller then runs V04 diplomacy maintenance.</summary>
public sealed class GovernmentTurn(GameState world, RuleTables rules, int owner, Action<int> clearStoredTurnFlag)
{
    private int stage;
    private int? pending;
    public bool Completed => stage == 4 && pending is null;

    public int? NextChange()
    {
        if (pending is not null)
        {
            return pending;
        }
        MapKnowledge.RequireLocal(world);
        while (stage != 4)
        {
            switch (stage)
            {
                case 0:
                    int government = unchecked((byte)world.Civilizations[owner].Government);
                    world.Civilizations[owner].Flags.GovernmentChanged = false;
                    // Reserved40 has no game meaning; its imported F06 bit belongs to the adapter.
                    clearStoredTurnFlag(owner);
                    stage = government == (int)Government.Anarchy ? 1 : 3;
                    break;
                case 1:
                    if (!Human || (world.Globals.Turn & 3) != 0)
                    {
                        stage = 3;
                        if ((world.Globals.Turn & 3) == 0 || BuildingQueries.HasWonder(world, rules, owner, WonderIds.StatueOfLiberty))
                        {
                            stage = 2;
                            return pending = 1;
                        }
                    }
                    else
                    {
                        stage = 3;
                        if (!world.Civilizations[owner].Flags.Revolution)
                        {
                            return pending = 1;
                        }
                        world.Civilizations[owner].Flags.Revolution = false;
                    }
                    break;
                case 2:
                    if (Human)
                    {
                        world.Civilizations[owner].Flags.Revolution = false;
                    }
                    stage = 3;
                    break;
                case 3:
                    stage = 4;
                    if (!Human && world.Civilizations[owner].Flags.Revolution)
                    {
                        return pending = unchecked((byte)world.Civilizations[owner].Government);
                    }
                    break;
            }
        }
        return null;
    }

    private bool Human => world.Globals.HumanPlayers.Contains(owner);

    public void ChangeCompleted()
    {
        if (pending is null)
        {
            throw new InvalidOperationException("No government change is pending");
        }
        pending = null;
    }
}
