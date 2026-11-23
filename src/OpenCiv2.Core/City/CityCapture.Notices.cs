using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.City;

public sealed partial class CityCapture
{
    private CaptureNotice Notice(Operation op, CaptureNoticeKind kind, CitySlot? city, int n, int o, int viewer,
        int? improvement = null, bool scoped = false) => new(kind, city, n, o, viewer, op.Name,
            world.Names[n].Tribe, kind is CaptureNoticeKind.LoserAlly or CaptureNoticeKind.AbandonedWonder or CaptureNoticeKind.StillOneWonder or CaptureNoticeKind.StillWonders
                ? world.Names[o].Tribe : world.Names[o].Adjective,
            op.Number, op.Restored, improvement, improvement is { } i ? rules.Improvements[i].Identity.Name : null, scoped);

    private IEnumerable<CityCaptureDecision?> Palace(Operation op, CitySlot city, int n, int o, MapPoint center, CitySlot? replacement)
    {
        int bestScore = -999;
        CitySlot? best = null;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var candidate = ref world.Entities.Cities.Storage[i];
            if (i == city.Value || !candidate.Token.Occupied || unchecked((sbyte)candidate.Owner.Value) != o
                || candidate.Size < City(city).Size / 2 || candidate.Size <= 7)
            { continue; }
            var slot = new CitySlot((ushort)i);
            int score = unchecked(3 * candidate.Size - world.Map.Geometry.WeightedDistance(candidate.Position, center, world.Globals.Rules.FlatWorld));
            score = unchecked(score + 4 * stacks.Count(stacks.FirstAt(candidate.Position)));
            if (Facility(slot, BuildingIds.SamBattery))
            { score = unchecked(score * 3) / 2; }
            if (Facility(slot, BuildingIds.CityWalls) || Wonder(o, WonderIds.GreatWall))
            { score = unchecked(score * 2); }
            if (Facility(slot, BuildingIds.SdiDefense))
            { score = unchecked(score * 3); }
            if (world.Map.TileAt(candidate.Position).Region != world.Map.TileAt(center).Region)
            { score /= 2; }
            if (score > bestScore)
            { bestScore = score; best = slot; }
        }
        int decision = 0;
        if (world.Civilizations[o].Gold >= 1000 && best is { Value: > 0 } destination && replacement is null)
        {
            op.Number = 1000;
            if (Human(o) && Local == o && !Autoplay)
            {
                yield return new CityCaptureDecision.Palace(new(o, destination, City(destination).Name, world.Names[o].Adjective, 1000));
                decision = op.TakeReply()!.Value.Selected;
            }
            else if (world.Globals.Difficulty > Difficulty.Prince && (world.Civilizations[o].SpaceshipFlags.Exists
                || world.Civilizations[o].CityCount > 11 || world.Civilizations[o].Gold > 1999))
            { decision = 1; }
        }
        if (decision == 1)
        {
            world.Civilizations[o].Gold = unchecked(world.Civilizations[o].Gold - 1000);
            BuildingQueries.SetBuilding(world, best!.Value, BuildingIds.Palace, true);
            effects.Notice(Notice(op, CaptureNoticeKind.PalaceEscape, best, n, o, Local) with { CityName = City(best.Value).Name });
            if (!Human(o))
            { services.Orders.Set(best.Value, 99); }
        }
        else
        {
            SpaceshipLifecycle.Lost(world, o, effects.SpaceshipLost);
            if (replacement is { } capital)
            { City(capital).Production = -BuildingIds.Palace; }
        }
    }

    private void Abandoned(Operation op, CitySlot city, int n, int o)
    {
        int item = -City(city).Production, count = 0;
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var other = ref world.Entities.Cities.Storage[i];
            if (i != city.Value && other.Token.Occupied && unchecked((sbyte)other.Owner.Value) == o && other.Production == City(city).Production)
            { ++count; }
        }
        op.Number = count;
        effects.Notice(Notice(op, count == 0 ? CaptureNoticeKind.AbandonedWonder : count == 1 ? CaptureNoticeKind.StillOneWonder : CaptureNoticeKind.StillWonders,
            city, n, o, Local, item));
    }
    private void BuiltWonders(Operation op, CitySlot city, int n, int o)
    {
        for (int wonder = 0; wonder < RuleLimits.Wonders; ++wonder)
        {
            if (BuildingQueries.ActiveCity(world, rules, wonder) != city.Value)
            { continue; }
            bool scoped = wonder <= WonderIds.GreatWall || wonder is WonderIds.StatueOfLiberty or WonderIds.EiffelTower || wonder >= WonderIds.Manhattan;
            if (Local == n && !Autoplay)
            { effects.Notice(Notice(op, CaptureNoticeKind.CapturedWonder, city, n, o, Local, 39 + wonder, scoped)); }
            else if (Local == o && !Autoplay)
            { effects.Notice(Notice(op, CaptureNoticeKind.LostWonder, city, n, o, Local, 39 + wonder, scoped)); }
            if (wonder == WonderIds.EiffelTower)
            { services.Wonders.ApplyEiffel(n); }
        }
    }
    private void Announce(Operation op, int n, int o, MapPoint center, int plunder)
    {
        if ((Human(n) || Human(o)) && !Autoplay)
        {
            op.Number = plunder;
            if (Local == n || Local == o)
            {
                effects.EnsureVisible(center, n);
                effects.Redraw(center);
                effects.Notice(Notice(op, CaptureNoticeKind.Capture, op.RemainingCity, n, o, Local));
            }
            return;
        }
        if (Autoplay)
        { return; }
        for (int viewer = 1; viewer < EntityLimits.Civilizations; ++viewer)
        {
            if (!world.Globals.CivsAlive.Contains(viewer) || !Human(viewer))
            { continue; }
            if (Local == viewer)
            { effects.Redraw(center); }
            CaptureNoticeKind? kind = null;
            if (world.Civilizations[viewer].Treaties[n].Alliance || world.Civilizations[n].Treaties[viewer].Mercenary && world.Civilizations[n].Treaties[o].Aggressor)
            { kind = CaptureNoticeKind.WinnerAlly; }
            else if (world.Civilizations[viewer].Treaties[o].Alliance || world.Civilizations[o].Treaties[viewer].Mercenary && world.Civilizations[o].Treaties[n].Aggressor)
            { kind = CaptureNoticeKind.LoserAlly; }
            else if (world.Civilizations[viewer].Treaties[n].Embassy || Wonder(viewer, WonderIds.UnitedNations) || Wonder(viewer, WonderIds.MarcoPolo)
                || world.Civilizations[viewer].Treaties[o].Embassy || Wonder(viewer, WonderIds.UnitedNations) || Wonder(viewer, WonderIds.MarcoPolo))
            { kind = CaptureNoticeKind.General; }
            if (kind is { } notice && Local == viewer)
            { effects.Notice(Notice(op, notice, op.RemainingCity, n, o, viewer)); }
        }
    }
}
