using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum CivilizationNoticeKind { Population, Funds, Rates }
public sealed record CivilizationNotice(CivilizationNoticeKind Kind, int Population = -1);

/// <summary>V02 §11 treasury boundaries, population queries and ordered post-assessment notices.</summary>
public static class CivilizationFinances
{
    public static int NormalizeTreasury(GameState world, int owner)
    {
        MapKnowledge.RequireLocal(world);
        ref int gold = ref world.Civilizations[owner].Gold;
        if (gold > 30000)
        {
            gold = 30000;
        }

        if (gold < -16384)
        {
            gold = 30000;
        }

        if (gold < 0)
        {
            gold = 0;
        }

        return gold;
    }

    public static int CityPopulation(in State.City city)
    {
        int size = city.Size;
        return size <= 0 ? 1 : Math.Max(1, size * (size + 1) / 2);
    }

    public static int Population(GameState world, int owner)
    {
        int total = 0;
        foreach (ref var city in world.Entities.Cities.Scanned)
        {
            if (city.Token.Occupied && unchecked((sbyte)city.Owner.Value) == owner)
            {
                total = unchecked(total + CityPopulation(city));
            }
        }
        if (total < 0 || total > 32000)
        {
            total = 32000;
        }

        return Math.Max(1, total);
    }

    /// <summary>Acknowledge each notice only after its actual presentation or rate child finishes.
    /// Funds gates are deliberately read after the population acknowledgment.</summary>
    public sealed class Operation(GameState world, int owner, int startGold)
    {
        private int stage;
        private CivilizationNotice? pending;
        public bool Completed => stage == 3 && pending is null;

        public CivilizationNotice? Next()
        {
            MapKnowledge.RequireLocal(world);
            if (pending is not null)
            {
                return pending;
            }

            ref var civ = ref world.Civilizations[owner];
            if (stage == 0)
            {
                int population = Population(world, owner), old = civ.PopulationMilestone, notice = -1;
                if (population < 100 && civ.PopulationMilestone * 10 < population)
                {
                    notice = population / 10 * 10;
                    civ.PopulationMilestone = unchecked((short)(population / 10));
                }
                else if (population >= 100 && (civ.PopulationMilestone - 9) * 100 < population)
                {
                    notice = population / 100 * 100;
                    civ.PopulationMilestone = unchecked((short)(population / 100 + 9));
                }
                stage = 1;
                if (old < civ.PopulationMilestone && HumanPresentation)
                {
                    return pending = new(CivilizationNoticeKind.Population, notice);
                }
            }
            if (stage == 1)
            {
                stage = 3;
                if (HumanPresentation && world.TurnLoopActive && civ.CityCount > 1 && civ.Gold < 100
                    && unchecked((civ.Gold - startGold) * 10 + civ.Gold) < 0)
                {
                    return pending = new(CivilizationNoticeKind.Funds);
                }
            }
            return null;
        }

        private bool HumanPresentation => world.Globals.HumanPlayers.Contains(owner) && !world.Multiplayer.Saved.AutoplayActive;

        public void Reply(int value)
        {
            var notice = pending ?? throw new InvalidOperationException("No financial notice is pending");
            pending = notice.Kind == CivilizationNoticeKind.Funds && value != 0
                ? new(CivilizationNoticeKind.Rates) : null;
        }
    }
}
