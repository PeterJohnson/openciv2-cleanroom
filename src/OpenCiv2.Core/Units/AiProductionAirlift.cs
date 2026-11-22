using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public sealed record AiAirliftNotice(CitySlot Destination, string Name, bool ShotDown);

/// <summary>U05 AI city-production airlift. A02 owns donor eligibility; this child preserves orders,
/// uses directed fighter-owner war and consumes one secondary draw per reachable fighter.</summary>
public sealed class AiProductionAirlift(GameState world, RuleTables rules, StackOperations stacks,
    UnitDeletion deletion, GameplayRandom random, Action<AiAirliftNotice> notice)
{
    public UnitSlot? Transfer(CitySlot origin, CitySlot destination, UnitSlot actor, int owner, string call)
    {
        MapKnowledge.RequireLocal(world);
        world.Entities.Cities.At(destination).Flags.Airlifted = true;
        world.Entities.Cities.At(origin).Flags.Airlifted = true;
        UnitMovement.Exhaust(world, rules, ref world.Entities.Units.At(actor));
        stacks.Relocate(actor.Value, world.Entities.Cities.At(destination).Position, true);
        UnitSlot? retained = actor;
        int watchers = 0;
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var fighter = ref world.Entities.Units.Storage[i];
            if (!fighter.Token.Occupied || rules.Units[fighter.Type.Value].Role != RuleRole.AirSuperiority
                || !world.Globals.HumanPlayers.Contains(fighter.Owner.Value) || !world.Civilizations[fighter.Owner.Value].Treaties[owner].War)
            {
                continue;
            }

            int distance = world.Map.Geometry.StepDistance(world.Entities.Cities.At(destination).Position, fighter.Position, world.Globals.Rules.FlatWorld);
            if (distance > Arithmetic.Divide32(UnitMovement.Allowance(world, rules, fighter), rules.Cosmic.RoadMovementMultiplier))
            {
                distance = world.Map.Geometry.StepDistance(world.Entities.Cities.At(origin).Position, fighter.Position, world.Globals.Rules.FlatWorld);
                if (distance > Arithmetic.Divide32(UnitMovement.Allowance(world, rules, fighter), rules.Cosmic.RoadMovementMultiplier))
                {
                    continue;
                }
            }
            if (random.SecondaryRange(new ChoiceKey("U05.ai-airlift", call, i), 0, fighter.Flags.Veteran ? 3 : 5) == 0)
            {
                deletion.Erase(actor.Value, true);
                retained = null;
                break;
            }
            watchers = unchecked(watchers + 1);
        }
        if (retained is null || watchers != 0)
        {
            notice(new(destination, world.Entities.Cities.At(destination).Name, retained is null));
        }

        return retained;
    }
}
