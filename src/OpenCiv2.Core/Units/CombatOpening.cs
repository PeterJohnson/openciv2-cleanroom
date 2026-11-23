using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

/// <summary>U06 Appendix A/P10 classic resource identities; stock playback is cosmetic.</summary>
public enum CombatSound
{
    AirCombat = 0, Catapult = 10, Cavalry = 12, DiveCrash = 23, DiveBomb = 24,
    Elephant = 25, Fire = 28, Helicopter = 33, Infantry = 34, LargeExplosion = 35,
    MachineGuns = 38, Missile = 41, SwordFight = 73, SwordHorse = 74, Torpedoes = 77,
    JetCrash = 79, JetBomb = 80, JetCombat = 82,
    CustomLand = 101, CustomShip = 102, CustomAir = 103,
    Extra1 = 125, Extra2 = 126, Extra3 = 127, Extra4 = 128, Extra5 = 129, Extra6 = 130, Extra7 = 131, Extra8 = 132
}
public sealed record CombatSoundCue(CombatSound Sound, int Argument);
public enum CombatOpeningNoticeKind { MissileAttack, PearlHarbor, Battery, BatteryWithSdi, Scramble, AmphibiousAttack }
public sealed record CombatOpeningNotice(CombatOpeningNoticeKind Kind, UnitTarget Unit, int? Improvement, OwnedList<string> Arguments);
/// <summary>One observer's semantic opening. The adapter owns focus, views, animation and optional
/// sound playback. A skipped animation must not skip the engine's visibility/cargo/placement effects.</summary>
public sealed record CombatOpeningPresentation(int Recipient, UnitTarget Attacker, UnitTarget Defender,
    MapPoint Origin, MapPoint Target, int Direction, OwnedList<CombatOpeningNotice> Notices, CombatSoundCue? Sound);
/// <summary>A renderer failure is reported to the caller for diagnostics and does not cancel combat.</summary>
public sealed record CombatOpeningResult(bool Visible, Exception? PresentationFailure = null);

/// <summary>U06 local viewer publication and pre-round opening using actual U01 cargo/placement
/// and C01 nearest-city queries. P10 permits batched presentation and omitted cosmetic delays.
/// The unresolved scenario visibility selector must be supplied explicitly when it is needed.</summary>
public sealed class CombatOpening(GameState world, RuleTables rules, CombatWorkspace state,
    StackOperations stacks, UnitCargo cargo, Action publish, Action<CombatOpeningPresentation> display)
{
    private ref Unit Unit(UnitSlot slot) => ref world.Entities.Units.At(slot);
    private UnitRule Rule(UnitSlot slot) => rules.Units[Unit(slot).Type.Value];
    private UnitTarget Target(UnitSlot slot) => new(slot, Unit(slot).Token);

    public bool PublishViewers(PreparedCombat combat, bool? scenarioShowsCombat = null)
    {
        MapKnowledge.RequireLocal(world);
        state.AnyViewer = false;
        for (int v = 1; v < EntityLimits.Civilizations; ++v)
        {
            state.Seen[v] = false;
            if (world.Multiplayer.LocalHuman != v || !world.Globals.CivsAlive.Contains(v) || !world.Globals.HumanPlayers.Contains(v))
            { continue; }
            bool seen = combat.AttackerOwner == v || combat.DefenderOwner == v || world.Globals.RevealMap != 0
                || Unit(combat.Attacker).Visibility.Contains(v) || Unit(combat.Defender).Visibility.Contains(v)
                || Unit(combat.Attacker).Owner.Value == v || Unit(combat.Defender).Owner.Value == v;
            if (!seen && world.Globals.Status.HasScenario)
            { seen = scenarioShowsCombat ?? throw new InvalidOperationException("U06 scenario combat visibility requires an explicit resolved selector"); }
            state.Seen[v] = seen;
            state.AnyViewer |= seen;
        }
        return state.AnyViewer;
    }

    /// <param name="incomingArgument0">The caller's retained text substitution for notices that do
    /// not replace argument zero. It is an explicit content input, not an emulated text buffer.</param>
    public CombatOpeningResult Present(PreparedCombat combat, int direction, string incomingArgument0, bool? scenarioShowsCombat = null)
    {
        ArgumentNullException.ThrowIfNull(incomingArgument0);
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, MapLimits.Directions);
        if (!PublishViewers(combat, scenarioShowsCombat))
        { return new(false); }
        MapKnowledge.MarkUnitSeen(world, combat.Attacker, combat.DefenderOwner);
        publish();
        cargo.Lift(combat.Attacker.Value, true);
        Exception? presentationFailure = null;
        try
        {
            var notices = Announcements(combat, incomingArgument0);
            state.LastSound = null;
            state.SoundArgument = null;
            state.ExplosionKind = 0;
            var sound = SelectSound(combat);
            if (sound is not null)
            { state.LastSound = sound.Sound; state.SoundArgument = sound.Argument; }
            state.AnimationDefender = combat.Defender;
            state.AnimationTarget = combat.Target;
            // Local play has at most one eligible viewer. The adapter may render any number of views.
            for (int v = 1; v < EntityLimits.Civilizations; ++v)
            {
                if (state.Seen[v] && world.Multiplayer.LocalHuman == v)
                {
                    var view = new CombatOpeningPresentation(v, Target(combat.Attacker), Target(combat.Defender), combat.Origin, combat.Target, direction, notices, sound);
                    try
                    { display(view); }
                    catch (Exception error) { presentationFailure = error; }
                }
            }
        }
        finally
        {
            // A failed or skipped renderer cannot strand the lifted attacker or its cargo off-map.
            stacks.RelocateStack(combat.Attacker.Value, combat.Origin, false);
        }
        return new(true, presentationFailure);
    }

    private OwnedList<CombatOpeningNotice> Announcements(PreparedCombat combat, string argument0)
    {
        List<CombatOpeningNotice> notices = [];
        void Add(CombatOpeningNoticeKind kind, UnitSlot unit, int? improvement, params string[] arguments)
        {
            if (!world.Multiplayer.Saved.AutoplayActive)
            { notices.Add(new(kind, Target(unit), improvement, new(arguments))); }
        }
        string CityName() => world.Entities.Cities.At(state.City ?? throw new InvalidOperationException("Combat facility notice requires its cached city")).Name;
        string Name(UnitSlot unit) => Rule(unit).Identity.Name;
        if (Rule(combat.Attacker).Abilities.DestroyedAfterAttack && unchecked((sbyte)Rule(combat.Attacker).Attack) < 99
            && world.Globals.HumanPlayers.Contains(combat.DefenderOwner)
            && CityQueries.Nearest(world, combat.Target).Slot is { } nearest)
        {
            argument0 = world.Entities.Cities.At(nearest).Name;
            Add(CombatOpeningNoticeKind.MissileAttack, combat.Attacker, null, argument0);
        }
        if (combat.ShoreBombard)
        {
            argument0 = world.Names[combat.AttackerOwner].Adjective;
            Add(CombatOpeningNoticeKind.PearlHarbor, combat.Attacker, null, argument0, Name(combat.Attacker), world.Names[combat.DefenderOwner].Adjective);
        }
        if (combat.ImprovementNotice is { } improvement)
        {
            string name = rules.Improvements[improvement].Identity.Name, city = CityName();
            if (combat.SdiBonus && improvement != BuildingIds.SdiDefense)
            { Add(CombatOpeningNoticeKind.BatteryWithSdi, combat.Attacker, improvement, argument0, name, city, rules.Improvements[BuildingIds.SdiDefense].Identity.Name); }
            else
            { Add(CombatOpeningNoticeKind.Battery, combat.Attacker, improvement, argument0, name, city); }
        }
        if (combat.Scramble)
        {
            argument0 = world.Names[combat.DefenderOwner].Adjective;
            Add(CombatOpeningNoticeKind.Scramble, combat.Defender, null, argument0, Name(combat.Defender), CityName());
        }
        if (Rule(combat.Attacker).Domain == RuleDomain.Land && world.Map.TileAt(combat.Origin).Ocean)
        { Add(CombatOpeningNoticeKind.AmphibiousAttack, combat.Attacker, null, argument0, world.Names[combat.AttackerOwner].Adjective, Name(combat.Attacker)); }
        return new(notices);
    }

    private CombatSoundCue? SelectSound(PreparedCombat combat)
    {
        int type = Unit(combat.Attacker).Type.Value;
        var rule = rules.Units[type];
        CombatSound sound;
        if (type is >= 54 and <= 61)
        { sound = (CombatSound)((int)CombatSound.Extra1 + type - 54); }
        else if (type is >= 51 and <= 53)
        { sound = (CombatSound)((int)CombatSound.CustomLand + type - 51); }
        else if (rule.Abilities.DestroyedAfterAttack)
        { return unchecked((sbyte)rule.Attack) < 99 ? new(CombatSound.Missile, 1) : null; }
        else if (rule.Domain == RuleDomain.Air)
        {
            if (Rule(combat.Defender).Domain == RuleDomain.Air)
            { return new(type < 30 ? CombatSound.AirCombat : CombatSound.JetCombat, 0); }
            sound = rule.Range == 0 ? CombatSound.Helicopter : type < 30 ? CombatSound.DiveBomb : CombatSound.JetBomb;
        }
        else if (rule.Domain == RuleDomain.Sea)
        {
            if (!rule.Abilities.Submarine)
            { state.ExplosionKind = type is >= 37 and <= 40 ? 46 : 6; return null; }
            sound = CombatSound.Torpedoes;
        }
        else
        {
            switch (type)
            {
                case 17:
                    sound = CombatSound.Elephant;
                    break;
                case 15 or 16 or 18 or 19:
                    sound = CombatSound.SwordHorse;
                    break;
                case 20 or 21:
                    sound = CombatSound.Cavalry;
                    break;
                case 7 or 11 or 10 or 9:
                    sound = CombatSound.Infantry;
                    break;
                case 8 or 13 or 12 or 14:
                    sound = CombatSound.MachineGuns;
                    break;
                case 23:
                    sound = CombatSound.Catapult;
                    break;
                case 22 or 24 or 25 or 26:
                    state.ExplosionKind = 40;
                    return type > 23 ? new(CombatSound.Fire, 1) : null;
                default:
                    sound = CombatSound.SwordFight;
                    break;
            }
        }
        return new(sound, 1);
    }
}
