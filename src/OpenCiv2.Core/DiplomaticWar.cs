using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum DiplomaticWarNoticeKind { AllianceCancelled, AllyHelps, AllyActivated, JoinedWar }
public sealed record DiplomaticParty(int Civilization, string Plural, string Adjective);
public sealed record DiplomaticWarNotice(DiplomaticWarNoticeKind Kind, DiplomaticParty First,
    DiplomaticParty Second, DiplomaticParty? Third = null, int? Participant = null);

/// <summary>Semantic presentation only. Treaty policy, cargo and unit relocation execute in Core.</summary>
public interface IDiplomaticWarEffects
{
    void Notice(DiplomaticWarNotice notice);
    void RedrawMaps(int localHuman);
    void MoveCursor(MapPoint position);
}

/// <summary>V04 §§6–8 local expulsion, alliance cancellation, declaration/reputation and obligations.
/// Network requests/publication and negotiation are separate session operations.</summary>
public sealed class DiplomaticWar(GameState world, RuleTables rules, StackOperations stacks, UnitCargo cargo,
    GameplayRandom random, IDiplomaticWarEffects effects)
{
    private void RequireLocal()
    {
        if (world.Globals.Multiplayer >= GameMode.Tcp)
        { throw new InvalidOperationException("Network diplomacy requires the owning session publication service"); }
    }
    private bool Human(int owner) => world.Globals.HumanPlayers.Contains(owner);
    private bool Wonder(int owner, int wonder) => BuildingQueries.HasWonder(world, rules, owner, wonder);
    private DiplomaticParty Party(int owner) => new(owner, world.Names[owner].Tribe, world.Names[owner].Adjective);
    private void AddReputation(int owner) => world.Civilizations[owner].Reputation = unchecked((byte)(world.Civilizations[owner].Reputation + 1));
    private void AddCredit(int owner, int other, int delta) => world.Civilizations[owner].ReputationCredit[other]
        = unchecked((sbyte)(world.Civilizations[owner].ReputationCredit[other] + delta));

    public void Expel(int intruder, int territoryOwner)
    {
        RequireLocal();
        for (int slot = 0; slot < world.Entities.Units.End; ++slot)
        {
            ref var unit = ref world.Entities.Units.Storage[slot];
            if (!unit.Token.Occupied || unit.Owner.Value != intruder)
            { continue; }
            var origin = unit.Position;
            var nearest = CityQueries.Nearest(world, origin, intruder, territoryOwner).Slot;
            if (nearest is not { } city || world.Entities.Cities.At(city).Owner.Value != territoryOwner)
            { continue; }
            var tile = world.Map.TileAt(origin);
            CitySlot? destination = null;
            if (!tile.Ocean)
            {
                if (world.Civilizations[territoryOwner].ContinentCities[tile.Region] == 0
                    || rules.Units[unit.Type.Value].Role == RuleRole.Trade)
                { continue; }
                destination = CityQueries.Nearest(world, origin, intruder).Slot;
            }
            else
            {
                int distance = 9999;
                for (int i = 0; i < world.Entities.Cities.End; ++i)
                {
                    ref var candidate = ref world.Entities.Cities.Storage[i];
                    var candidateSlot = new CitySlot((ushort)i);
                    if (!candidate.Token.Occupied || candidate.Owner.Value != intruder
                        || !CityQueries.TouchesBody(world, candidateSlot, tile.Region))
                    { continue; }
                    int score = world.Map.Geometry.StepDistance(origin, candidate.Position, world.Globals.Rules.FlatWorld);
                    if (score < distance)
                    { distance = score; destination = candidateSlot; }
                }
            }
            if (destination is not { } target || unit.Owner != world.Entities.Cities.At(target).Owner)
            { continue; }
            cargo.Lift(slot, false);
            stacks.RelocateStack(slot, world.Entities.Cities.At(target).Position, true);
            if (world.MovementMode == MovementMode.MoveUnits && world.Globals.ActiveUnit == new UnitSlot((ushort)slot))
            { effects.MoveCursor(world.Entities.Cities.At(target).Position); }
            unit.Orders = UnitOrder.None;
        }
    }

    public void CancelAlliance(int first, int second)
    {
        RequireLocal();
        DiplomaticRelations.Clear(world, first, second, new() { Alliance = true });
        Expel(first, second);
        Expel(second, first);
        int local = world.Multiplayer.LocalHuman;
        effects.RedrawMaps(local);
        int? participant = local == first || local == second ? local : null;
        if (participant is null && !Observed(first, second))
        { return; }
        if (!world.Multiplayer.Saved.AutoplayActive)
        {
            effects.Notice(new(DiplomaticWarNoticeKind.AllianceCancelled, Party(first), Party(second), Participant: participant));
        }
    }

    private bool Observed(int first, int second)
    {
        int local = world.Multiplayer.LocalHuman;
        return world.Civilizations[local].Treaties[first].Embassy || Wonder(local, WonderIds.UnitedNations)
            || Wonder(local, WonderIds.MarcoPolo) || world.Civilizations[local].Treaties[second].Embassy
            || Wonder(local, WonderIds.UnitedNations) || Wonder(local, WonderIds.MarcoPolo) || world.Globals.RevealMap != 0;
    }

    public void Declare(int aggressor, int victim, int mediator = -1)
    {
        RequireLocal();
        if (mediator >= 0)
        { AddCredit(mediator, aggressor, 1); }
        var relation = world.Civilizations[aggressor].Treaties[victim];
        if (relation.Alliance)
        {
            if (world.Globals.Difficulty != Difficulty.Chieftain && !Wonder(aggressor, WonderIds.EiffelTower))
            { AddReputation(aggressor); }
            if (mediator < 0)
            {
                AddReputation(aggressor);
                if (Human(aggressor))
                { AddCredit(aggressor, victim, -1); }
            }
            if (mediator >= 0)
            { DiplomaticAttitudes.Adjust(world, mediator, aggressor, -25); }
            CancelAlliance(aggressor, victim);
            if (Human(aggressor))
            { world.Civilizations[victim].Treaties[aggressor].Vendetta = true; }
            return;
        }
        if (!relation.Ceasefire && !relation.Peace)
        {
            if (mediator >= 0)
            { DiplomaticAttitudes.Adjust(world, mediator, aggressor, -5); }
        }
        else
        {
            if (relation.Ceasefire && relation.Peace)
            { TreatyPenalty(aggressor, victim, mediator); }
            TreatyPenalty(aggressor, victim, mediator);
            if (mediator >= 0)
            { DiplomaticAttitudes.Adjust(world, mediator, aggressor, -15); }
        }
        if (Human(aggressor))
        { world.Civilizations[victim].Treaties[aggressor].Vendetta = true; }
        DiplomaticRelations.Set(world, aggressor, victim, new() { War = true });
        if (Human(aggressor))
        {
            world.Civilizations[aggressor].Treaties[victim].Aggressor = true;
            world.Civilizations[aggressor].Treaties[victim].ExtendedAggressor = true;
            world.Civilizations[aggressor].LastContactTurn[victim] = world.Globals.Turn;
            ActivateAllies(victim, aggressor);
        }
    }

    private void TreatyPenalty(int aggressor, int victim, int mediator)
    {
        if (world.Globals.PowerRank[aggressor] == 7 && world.Civilizations[aggressor].CityCount > 4 && mediator < 0)
        { AddReputation(aggressor); }
        if (world.Globals.Difficulty != Difficulty.Chieftain && !Wonder(aggressor, WonderIds.EiffelTower))
        { AddReputation(aggressor); }
        if (mediator < 0 && Human(aggressor))
        { AddCredit(aggressor, victim, -1); }
    }

    public bool Force(int aggressor, int victim)
    {
        RequireLocal();
        DiplomaticRelations.Clear(world, aggressor, victim, new() { HostileIntent = true, BrokenTreaty = true });
        var reverse = world.Civilizations[victim].Treaties[aggressor];
        if ((reverse.Ceasefire || reverse.Peace) && (Human(aggressor) || Human(victim)))
        { Declare(aggressor, victim); }
        DiplomaticRelations.Clear(world, aggressor, victim, new() { Peace = true, Alliance = true });
        if (world.Civilizations[aggressor].Treaties[victim].Contact)
        {
            DiplomaticRelations.Set(world, aggressor, victim, new() { War = true });
            DiplomaticAttitudes.Set(world, aggressor, victim, 100);
            DiplomaticAttitudes.Set(world, victim, aggressor, 100);
            if (!world.Civilizations[aggressor].Treaties[victim].SneakAttack && !world.Civilizations[victim].Treaties[aggressor].SneakAttack)
            {
                world.Civilizations[aggressor].Treaties[victim].SneakAttack = true;
                return true;
            }
        }
        return false;
    }

    public void ActivateAllies(int defender, int enemy)
    {
        RequireLocal();
        if (!world.Civilizations[defender].Treaties[enemy].Contact)
        { return; }
        for (int ally = 1; ally < EntityLimits.Civilizations; ++ally)
        {
            var relation = world.Civilizations[ally].Treaties[enemy];
            if (ally == defender || ally == enemy || !world.Civilizations[defender].Treaties[ally].Alliance
                || !relation.Contact || relation.Alliance || relation.War)
            { continue; }
            if (!Human(enemy))
            {
                if (!Human(defender))
                { continue; }
                if (defender == world.Multiplayer.LocalHuman)
                { effects.Notice(new(DiplomaticWarNoticeKind.AllyHelps, Party(defender), Party(enemy), Party(ally), defender)); }
                DiplomaticRelations.Set(world, ally, enemy, new() { Contact = true, RecentContact = true, War = true });
                world.Civilizations[ally].Treaties[enemy].Aggressor = true;
                world.Civilizations[ally].Treaties[enemy].ExtendedAggressor = true;
            }
            else
            {
                if (!Human(defender) && enemy == world.Multiplayer.LocalHuman)
                { effects.Notice(new(DiplomaticWarNoticeKind.AllyActivated, Party(defender), Party(enemy), Party(ally), enemy)); }
                DiplomaticAttitudes.Adjust(world, ally, enemy, 100);
                DiplomaticRelations.Set(world, ally, enemy, new() { Contact = true, RecentContact = true, War = true });
                world.Civilizations[ally].Treaties[enemy].Aggressor = true;
                world.Civilizations[ally].Treaties[enemy].ExtendedAggressor = true;
                world.Civilizations[defender].Treaties[enemy].Aggressor = true;
                world.Civilizations[defender].Treaties[enemy].ExtendedAggressor = true;
                world.Civilizations[enemy].LastContactTurn[ally] = world.Globals.Turn;
            }
        }
    }

    public bool JoinAllyWar(int actor, int enemy, int ally, string call)
    {
        RequireLocal();
        ArgumentException.ThrowIfNullOrEmpty(call);
        var relation = world.Civilizations[actor].Treaties[enemy];
        if (relation.Alliance || relation.War)
        { return false; }
        if (!world.Civilizations[ally].Treaties[enemy].War)
        {
            if (relation.Vendetta && world.Civilizations[ally].Treaties[enemy].Vendetta)
            {
                world.Civilizations[actor].Treaties[enemy].HostileIntent = true;
                world.Civilizations[ally].Treaties[enemy].HostileIntent = true;
            }
            return false;
        }
        if (Human(enemy) && !relation.Vendetta)
        {
            if (world.Civilizations[enemy].LastContactTurn[actor] - world.Globals.Turn < 6)
            { return false; }
            if (world.Globals.PowerRank[enemy] < 7 && random.PrimaryModulo(new ChoiceKey("V04.join-ally-war", call), 3) != 0)
            { return false; }
        }
        if (Human(enemy))
        {
            world.Civilizations[enemy].LastContactTurn[actor] = world.Globals.Turn;
            world.Civilizations[enemy].LastContactTurn[ally] = world.Globals.Turn;
        }
        effects.Notice(new(DiplomaticWarNoticeKind.JoinedWar, Party(actor), Party(enemy), Party(ally)));
        DiplomaticRelations.Set(world, actor, enemy, new() { War = true });
        return true;
    }
}
