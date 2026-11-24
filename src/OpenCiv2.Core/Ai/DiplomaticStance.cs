using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core.Ai;

[Flags]
public enum AllianceReasons { None = 0, ExistingAlliedTarget = 1, DangerousEnemy = 2, NewCommonTarget = 4 }

/// <summary>A08 §4 ordered diplomatic evaluation, consuming real V07 race, C08 wonders, U01 spatial
/// queries, V04 relations and A08/V02 government choice. A is the actor and B the evaluated counterpart.</summary>
public sealed class DiplomaticStance(GameState world, RuleState rules, GovernmentPolicy governments)
{
    private DiplomacyWorkspace E => world.Diplomacy;
    private int Rank(int c) => world.Globals.PowerRank[c];
    private int Strength(int c) => world.Civilizations[c].MilitaryStrength;
    private int Nuclear(int c) => world.Civilizations[c].UnitsActive[UnitTypeIds.NuclearMissile];
    private bool Wonder(int c, int w) => BuildingQueries.HasWonder(world, rules.Definitions, c, w);
    private int Distrust(int a, int b) => world.Civilizations[a].Reputation - world.Civilizations[b].ReputationCredit[a];

    /// <summary>A08 §6 security query. The judging civilization is C; this does not
    /// alter the evaluation workspace, treaties or random stream.</summary>
    public bool FeelsSecure(int c, int d)
    {
        MapKnowledge.RequireLocal(world);
        unchecked
        {
            var relation = world.Civilizations[c].Treaties[d];
            bool alliance = relation.Alliance;
            if (relation.Aggressor || relation.Vendetta)
            { return true; }
            for (int h = 1; h < EntityLimits.Civilizations; ++h)
            {
                if (!world.Globals.HumanPlayers.Contains(h))
                { continue; }
                if (DiplomaticAttitudes.Hostile(world, c, h) && Strength(d) < Strength(h)
                    && !world.Civilizations[h].Treaties[d].Alliance)
                { return false; }
                if (DiplomaticAttitudes.Hostile(world, d, h) && Strength(c) < Strength(h)
                    && !world.Civilizations[h].Treaties[c].Alliance)
                { return false; }
            }
            if (Nuclear(c) < Nuclear(d))
            { return false; }
            int security = 0;
            for (int k = 1; k < EntityLimits.Civilizations; ++k)
            {
                if (k == c || k == d)
                { continue; }
                if (DiplomaticAttitudes.Hostile(world, c, k))
                { ++security; }
                if (world.Globals.HumanPlayers.Contains(k) && world.Civilizations[c].Treaties[k].Alliance)
                {
                    if (world.Civilizations[d].Treaties[k].Alliance)
                    {
                        ++security;
                        if (Rank(k) > Rank(c) && Rank(k) > Rank(d))
                        { ++security; }
                    }
                    if (world.Civilizations[d].Treaties[k].War)
                    { --security; }
                }
                if (Rank(k) == 7 && world.Globals.Turn > 199 && world.Civilizations[d].Treaties[k].War)
                { --security; }
            }
            if (!DiplomaticAttitudes.Hostile(world, c, d))
            { ++security; }
            if (alliance)
            { ++security; }
            security -= rules.Leaders[world.Civilizations[c].Tribe].Attack;
            int ownAttack = 0, theirStrength = 1, sharedCities = 0;
            for (int r = 1; r < MapLimits.Regions - 1; ++r)
            {
                ref var actor = ref world.Civilizations[c];
                ref var other = ref world.Civilizations[d];
                int attack = actor.ContinentAttack[r], enemyAttack = other.ContinentAttack[r];
                int enemyMilitary = other.ContinentMilitary[r];
                if (actor.ContinentCities[r] != 0 && attack + actor.ContinentMilitary[r] < enemyAttack)
                { return false; }
                if (attack != 0 && enemyMilitary != 0)
                {
                    ownAttack += attack;
                    theirStrength += (enemyAttack >> 1) + enemyMilitary;
                    sharedCities += actor.ContinentCities[r];
                }
            }
            return !((ownAttack << 2) / theirStrength < security + 4 && sharedCities != 0);
        }
    }

    /// <summary>V05 §2 consumes the current evaluation attitude; War overrides all prior additions.</summary>
    public int Patience(int a, int b)
    {
        int result = E.Attitude < 25 ? 3 : 2;
        if (E.Attitude > 60)
        { --result; }
        if (Wonder(a, WonderIds.EiffelTower))
        { ++result; }
        var relation = world.Civilizations[b].Treaties[a];
        if (relation.Peace)
        { ++result; }
        if (relation.Alliance)
        { result += 2; }
        return relation.War ? 2 : result;
    }

    /// <summary>A08 §5 consumes live evaluation and preserves reasons through later rejection.
    /// This policy neither signs a treaty nor executes the negotiated price/response.</summary>
    public bool AllianceWilling(int a, int b, ref AllianceReasons reasons)
    {
        MapKnowledge.RequireLocal(world);
        unchecked
        {
            int allies = 0;
            for (int k = 1; k < EntityLimits.Civilizations; ++k)
            { if (world.Civilizations[a].Treaties[k].Alliance) { ++allies; } }
            int score = E.WarTarget is null ? 1 : 0;
            if (world.Globals.Difficulty < Difficulty.Prince)
            { --score; }
            if (Rank(a) < 5 && Rank(b) < 5)
            {
                if (world.Globals.Difficulty > Difficulty.Warlord && allies == 0)
                { --score; }
                --score;
            }
            if (Rank(a) < Rank(b))
            {
                if (Rank(b) > 5)
                { ++score; }
                if (Rank(b) > 6)
                { ++score; }
                if (Rank(a) < 6 && Strength(b) < Strength(a))
                { --score; }
            }
            if (E.TargetIsLeader)
            { ++score; }
            else if (Rank(a) - Rank(b) > 2)
            { --score; }
            if (E.Fear != 0)
            { --score; }
            bool agree = score < E.PeaceInclination;
            agree = world.Globals.Difficulty == Difficulty.Chieftain || DiplomaticAttitudes.Category(E.Attitude) < 4 && agree;
            if (Rank(a) == 7 && allies != 0)
            { agree = false; }
            if (Distrust(a, b) > (world.Globals.Difficulty == Difficulty.Chieftain ? 4 : 2))
            { agree = false; }
            if (Rank(a) < 4 && world.Globals.Turn > 20 * Rank(a))
            { agree = true; }
            if (E.WarTarget is > 0 and var enemy && !DiplomaticAttitudes.Hostile(world, a, enemy)
                && world.Civilizations[a].Treaties[enemy].Contact)
            {
                int limit = world.Civilizations[a].Treaties[enemy].Peace ? 3 * Strength(b) / 2 : 2 * Strength(b);
                if (world.Civilizations[b].Treaties[enemy].Fighting && limit <= Strength(enemy))
                { agree = true; reasons = AllianceReasons.DangerousEnemy; }
            }
            if ((E.WarTarget is null or < 1 || reasons == AllianceReasons.None) && Rank(a) < 7 && Rank(b) < 7
                && world.Globals.Turn > 99 && allies < 7 - Rank(a) && DiplomaticAttitudes.Category(E.Attitude) < 5)
            {
                int best = 8;
                int? target = null;
                for (int k = 1; k < EntityLimits.Civilizations; ++k)
                {
                    if (k == a || k == b || Rank(k) < Rank(a) || Rank(k) < Rank(b)
                        || !(world.Civilizations[a].Treaties[k].Contact || world.Civilizations[b].Treaties[k].Contact)
                        || world.Civilizations[a].Treaties[k].War || world.Civilizations[b].Treaties[k].Alliance)
                    { continue; }
                    int candidate = Rank(k) - (world.Civilizations[a].Treaties[k].Contact ? 1 : 0);
                    if (candidate < best)
                    { best = candidate; target = k; }
                }
                if (E.WarTarget is > 0 and var retained && Rank(retained) != 7)
                { target = retained; }
                if (target is > 0)
                { E.WarTarget = target; agree = true; reasons |= AllianceReasons.NewCommonTarget; }
            }
            if (world.Civilizations[b].Treaties[a].Vendetta || Distrust(a, b) > 4)
            { agree = false; }
            if (E.WarTarget is >= 0 and var allied && world.Civilizations[a].Treaties[allied].Alliance)
            { agree = true; reasons |= AllianceReasons.ExistingAlliedTarget; }
            if (E.Attitude > 75 || world.Civilizations[b].DiploRequestCount > 4 && world.Globals.Difficulty != Difficulty.Chieftain)
            { agree = false; }
            return agree;
        }
    }

    public void Evaluate(int a, int b, string call)
    {
        MapKnowledge.RequireLocal(world);
        ArgumentException.ThrowIfNullOrEmpty(call);
        unchecked
        {
            E.RaceThreat = SpaceshipQueries.RaceBehind(world, rules.Definitions, b);
            int wars = 0;
            for (int k = 1; k < EntityLimits.Civilizations; ++k)
            {
                if (world.Civilizations[a].Treaties[k].War)
                { ++wars; }
                if (world.Globals.HumanPlayers.Contains(k) && world.Civilizations[k].SpaceshipFlags.Launched
                    && world.Globals.Difficulty > Difficulty.King)
                { E.RaceThreat = true; }
            }
            E.OutranksTarget = Rank(a) < Rank(b);
            E.SuppressedAggression = false;
            if (E.OpeningEvaluationActive && !E.InitialEvaluationDone)
            { E.LastContact = world.Civilizations[a].LastContactTurn[b]; }
            E.Fear = E.Demand = 0;
            E.Aggressive = false;
            E.RequestMode = 1;
            for (int r = 1; r < MapLimits.Regions; ++r)
            {
                ref var actor = ref world.Civilizations[a];
                ref var other = ref world.Civilizations[b];
                int attackA = actor.ContinentAttack[r], attackB = other.ContinentAttack[r];
                int militaryB = other.ContinentMilitary[r];
                if (other.ContinentCities[r] > (other.CityCount > 1 ? 1 : 0) && militaryB < attackA)
                { E.Fear += (int)((uint)(attackA * (world.Globals.Difficulty == Difficulty.Chieftain ? 4 : 2)) / (uint)(militaryB + 1)); }
                else
                {
                    if (actor.ContinentMilitary[r] != 0 && militaryB != 0)
                    { E.Aggressive = true; }
                    if (actor.ContinentCities[r] == 0)
                    { E.Demand += other.ContinentCities[r] == 0 ? (attackB - attackA) / 4 : (militaryB - attackA) / 2; }
                    else
                    {
                        E.Demand += other.ContinentCities[r] < 2 && (world.Globals.Turn <= 200 - 50 * wars || Rank(a) > 6 || Rank(b) > 6)
                            ? attackB : attackB - attackA;
                    }
                }
            }
            if (Wonder(b, WonderIds.MarcoPolo))
            { E.Demand += E.Demand / 4; }
            E.TargetIsLeader = Rank(a) == 7 && world.Civilizations[a].CityCount > 4 && world.Civilizations[b].CityCount > 1
                && Nuclear(a) == 0 && world.Globals.Difficulty != Difficulty.Chieftain && world.Globals.Turn > 200;
            E.Attitude = DiplomaticAttitudes.Get(world, b, a);
            Modifiers(a, b);
            OtherCivilizations(a, b);
            Finish(a, b, call);
        }
    }

    private void Modifiers(int a, int b)
    {
        unchecked
        {
            ref var actor = ref world.Civilizations[a];
            ref var other = ref world.Civilizations[b];
            ref var relation = ref other.Treaties[a];
            if ((E.TargetIsLeader || relation.Vendetta && Strength(a) < 3 * Strength(b)) && Nuclear(a) == 0)
            {
                E.Demand = Arithmetic.ClampAB(E.Demand, 2 * Rank(b), 9999);
                if (!E.Aggressive)
                { E.Demand = Arithmetic.ClampAB(E.Demand, 0, (actor.Gold / 50) / (8 - Rank(b))); }
                E.Aggressive = true;
                E.Fear = 0;
                if (E.Attitude < 76)
                { E.Attitude = 75; }
            }
            if (E.RaceThreat && E.OutranksTarget)
            { E.Aggressive = true; }
            if (Nuclear(b) != 0)
            {
                relation.NuclearThreat = true;
                if (Nuclear(a) == 0)
                { E.Aggressive = true; E.Demand = Arithmetic.ClampAB(E.Demand, 100, 9999); }
            }
            if (Nuclear(a) != 0 && !relation.Alliance)
            {
                if (Nuclear(b) == 0)
                { E.Fear = Math.Max(4, (actor.Reputation >> 1) + 1); }
                else
                { E.Demand = Nuclear(b) * (E.Demand / 2) / Nuclear(a); }
            }
            E.Fear /= (actor.Reputation - 1) / 2 + 1;
            if (relation.Alliance)
            { E.Attitude -= 25; }
            if (relation.War)
            { E.Attitude += 25; }
            if (actor.Reputation != 0)
            { E.Attitude += 5 * (actor.Reputation - 1 - other.ReputationCredit[a]); }
            if (E.Attitude < 26)
            { E.Demand /= 2; E.Aggressive = false; }
            else if (DiplomaticAttitudes.Category(E.Attitude) < 4)
            { E.Demand = 2 * E.Demand / 3; }
            else if (E.Attitude > 74)
            { E.Demand = 3 * E.Demand / 2; E.Aggressive = true; }
            if (relation.Aggressor || world.Globals.Rules.Bloodlust && other.ScienceRate == 0)
            { E.Aggressive = true; }
            if (E.Fear != 0)
            { E.Aggressive = false; E.RequestMode = 0; }
            if (Wonder(a, WonderIds.GreatWall) || Wonder(a, WonderIds.UnitedNations))
            {
                E.TargetIsLeader = false;
                E.SuppressedAggression = E.Aggressive;
                E.Aggressive = false;
                E.Attitude -= 10;
                DiplomaticRelations.Clear(world, b, a, new() { Incident = true });
            }
            if (actor.Reputation != 0)
            { E.Demand += actor.Reputation * E.Demand / 2; }
            if (relation.Vendetta)
            { E.Demand *= 2; }
            else if (relation.Incident)
            { E.Demand /= 4; }
            E.NearbyUnits = 0;
            for (int i = 0; i < world.Entities.Cities.End; ++i)
            {
                ref var city = ref world.Entities.Cities.Storage[i];
                if (!city.Token.Occupied || city.Owner.Value != a || world.Map.UnitOwner(city.Position) is not null)
                { continue; }
                var nearest = UnitQueries.Nearest(world, city.Position, b);
                if (nearest.Slot is not null && nearest.Distance < 3)
                { E.Demand *= 2; ++E.NearbyUnits; break; }
            }
            E.Demand = 50 * Arithmetic.ClampAB(((int)world.Globals.Difficulty + 1) * E.Demand / 32, 0, 20);
            if (actor.Gold < E.Demand && E.Demand < 2 * actor.Gold && actor.Gold > 49)
            { E.Demand = actor.Gold / 50 * 50; }
            if (E.Demand == 0 || 3 * Strength(b) < Strength(a))
            { E.Aggressive = false; }
            if (other.AggressionCounter[a] > 9)
            { other.AggressionCounter[a] = 0; E.Aggressive = false; }
            if (E.Aggressive)
            { E.RequestMode = 2; }
            else if (relation.Alliance)
            { E.RequestMode = 0; }
            E.InitiallyAggressive = E.Aggressive;
        }
    }

    private void OtherCivilizations(int a, int b)
    {
        unchecked
        {
            E.PeaceInclination = world.Civilizations[b].Treaties[a].Vendetta ? -2 : 0;
            if (world.Globals.Rules.Bloodlust && world.Civilizations[b].ScienceRate == 0)
            { --E.PeaceInclination; }
            if (world.Civilizations[a].Reputation != 0)
            { E.PeaceInclination -= (world.Civilizations[a].Reputation - 1) / 2; }
            E.WeakestEnemy = E.StrongestEnemy = E.WarTarget = null;
            int maximum = 0, minimum = 9999, aggressors = 0;
            for (int k = 1; k < EntityLimits.Civilizations; ++k)
            {
                if (k == a || k == b)
                { continue; }
                var relation = world.Civilizations[b].Treaties[k];
                if (relation.Alliance)
                {
                    if (Rank(b) < Rank(a))
                    { --E.PeaceInclination; }
                    if (Rank(a) == 7 && world.Civilizations[a].CityCount > 4 && world.Globals.Turn > 200
                        && world.Globals.Difficulty != Difficulty.Chieftain && world.Civilizations[k].Treaties[a].Contact)
                    {
                        if (E.Fear == 0)
                        {
                            if (world.Civilizations[k].Treaties[a].War && !Wonder(a, WonderIds.GreatWall) && !Wonder(a, WonderIds.UnitedNations))
                            { E.Aggressive = true; }
                        }
                        else
                        { E.Fear = Arithmetic.ClampAB(E.Fear - 4, 0, 99); }
                    }
                }
                if (relation.Aggressor)
                { ++aggressors; }
                if (!DiplomaticAttitudes.Hostile(world, b, k))
                { continue; }
                if (4 * Strength(k) > Strength(b))
                { ++E.PeaceInclination; }
                if (Strength(k) > Strength(b))
                { ++E.PeaceInclination; }
                if (world.Civilizations[a].Treaties[k].Contact && !world.Civilizations[a].Treaties[k].War)
                { E.WarTarget = k; }
                if (relation.War && relation.Fighting)
                {
                    if (Strength(k) > maximum)
                    { maximum = Strength(k); E.StrongestEnemy = k; }
                    if (Strength(k) < minimum)
                    { minimum = Strength(k); E.WeakestEnemy = k; }
                }
            }
            if (aggressors == 0)
            { DiplomaticRelations.Clear(world, b, a, new() { Mercenary = true }); }
            E.PeaceInclination -= rules.Leaders[world.Civilizations[b].Tribe].Attack;
            if (Strength(a) < Strength(b))
            { --E.PeaceInclination; }
            if (E.PeaceInclination > (DiplomaticAttitudes.Hostile(world, b, a) ? 1 : 0) && !E.TargetIsLeader
                && !world.Civilizations[b].Treaties[a].Alliance && !world.Civilizations[b].Treaties[a].Aggressor)
            {
                E.Aggressive = false;
                if (((world.Globals.Turn + b + a) & 32) != 0)
                { E.Demand = 0; }
            }
        }
    }

    private void Finish(int a, int b, string call)
    {
        unchecked
        {
            E.CapturedCities = 0;
            foreach (ref readonly var city in world.Entities.Cities.Scanned)
            { if (city.Token.Occupied && city.Owner.Value == a && city.Founder.Value == b) { ++E.CapturedCities; } }
            if (!E.InitialEvaluationDone || Distrust(a, b) > 6)
            {
                var relation = world.Civilizations[b].Treaties[a];
                E.DeclareWar = (!relation.War || E.CapturedCities > 4) && !relation.Alliance
                    && (E.Aggressive || E.RaceThreat && world.Globals.Difficulty > Difficulty.Prince
                        || E.SuppressedAggression && Distrust(a, b) > 6 && relation.Vendetta)
                    && (Strength(b) < Strength(a) || relation.Vendetta || Distrust(a, b) > 6);
            }
            if (E.DeclareWar)
            { E.Aggressive = false; E.RequestMode = 0; if (E.Attitude > 29) { E.Attitude = 30; } }
            if (world.Globals.Status.Scenario && world.Scenario.Flags.HistoricalAi)
            {
                if (b == 6)
                {
                    if (a == 7)
                    { if (world.Civilizations[6].Treaties[7].Alliance) { E.Aggressive = true; } }
                    else
                    { E.Aggressive = a == 3; }
                }
                else if (b == 3 && a is 6 or 1)
                { E.Aggressive = true; }
            }
            if (E.OpeningEvaluationActive)
            {
                int requests = world.Civilizations[b].DiploRequestCount;
                int patience = Patience(a, b);
                if (requests < patience && world.Civilizations[b].Government > Government.Fundamentalism && E.Aggressive
                    && !world.Civilizations[b].Treaties[a].Alliance && !world.Civilizations[b].Treaties[a].CityCaptured
                    && !(world.Globals.Status.Scenario && world.Scenario.Flags.RevolutionRestriction))
                {
                    if (2 * Strength(b) < 3 * Strength(a) && !E.TargetIsLeader && !world.Civilizations[b].Treaties[a].Vendetta)
                    { E.Aggressive = false; }
                    else
                    { governments.Choose(b, true, call + "/government"); }
                }
            }
            if (E.Fear == 0 && E.Attitude < 2)
            { E.Attitude = 1; }
            var forward = world.Civilizations[a].Treaties[b];
            if (!forward.War && E.Attitude > 98)
            { E.Attitude = 99; }
            if ((forward.Alliance || E.RequestMode == 0) && E.Attitude > 73)
            { E.Attitude = 74; }
            if (forward.War && E.Attitude < 27)
            { E.Attitude = 26; }
        }
    }
}
