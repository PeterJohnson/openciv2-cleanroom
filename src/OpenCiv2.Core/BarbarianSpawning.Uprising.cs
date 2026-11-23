using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed partial class BarbarianSpawning
{
    private void Uprising(Operation operation, CitySlot selected, string call)
    {
        int treasuryRoll = Roll("treasury", call, 100);
        if (treasuryRoll >= CityCaptureQueries.TreasuryShare(world, selected))
        { return; }
        MapPoint point = default;
        int attempts = 0;
        while (true)
        {
            int x = world.Map.Geometry.WrapX(world.Entities.Cities.At(selected).Position.X + 2 * Roll("land-x", call, 13, attempts) - 6,
                world.Globals.Rules.FlatWorld);
            int centerY = world.Entities.Cities.At(selected).Position.Y;
            int y = centerY + Roll("land-y", call, 13, attempts) - 6;
            point = new((x & ~1) + (y & 1), y);
            bool inBounds = world.Map.Geometry.InBounds(point);
            int distance = 0;
            if (inBounds)
            {
                var nearest = CityQueries.Nearest(world, point);
                if (nearest.Slot is not { } city)
                { attempts = 200; break; }
                selected = city;
                distance = nearest.Distance;
            }
            ++attempts;
            if (attempts >= world.Globals.BarbarianActivity * 50)
            { break; }
            if (inBounds && distance >= 5 && SpatialControl.AdjacentNonalliedUnit(world, point, 0, true) is null
                && !world.Map.TileAt(point).Ocean && world.Map.UnitOwner(point) is null
                && world.Map.TileAt(point).Region == world.Map.TileAt(world.Entities.Cities.At(selected).Position).Region)
            { break; }
        }
        if (attempts >= world.Globals.BarbarianActivity * 50)
        { return; }
        int size = world.Entities.Cities.At(selected).Size;
        int count = size <= 1 ? 0 : Roll("size", call, size);
        if (world.Globals.BarbarianActivity == 3 && world.Globals.Turn >= 150)
        {
            int surge = (byte)world.Globals.Difficulty >= 9 ? 0 : Roll("surge", call, 10 - (byte)world.Globals.Difficulty);
            if (surge == 0)
            {
                int half = size / 2;
                count += (half <= 1 ? 0 : Roll("surge-bonus", call, half)) + half;
            }
        }
        int n = Arithmetic.ClampAB(count, 2, 99);
        int main = UnitTypeIds.Chariot, alternate = UnitTypeIds.Horsemen;
        if (Roll("mounted", call, 3) == 0)
        { (main, alternate) = (alternate, main); }
        var kind = n > 10 && n > world.Entities.Cities.At(selected).Size / 2
            ? BarbarianUprisingKind.LargeUprising : BarbarianUprisingKind.Uprising;
        if (Known(AdvanceIds.Gunpowder))
        {
            kind = BarbarianUprisingKind.Gunpowder;
            if (Expanded)
            { main = UnitTypeIds.Archers; alternate = UnitTypeIds.Elephant; }
        }
        if (Known(AdvanceIds.Industrialization))
        {
            kind = BarbarianUprisingKind.Industrial;
            main = UnitTypeIds.Cannon;
            alternate = UnitTypeIds.Musketeers;
            if (Known(ArmorPrerequisite))
            { kind = BarbarianUprisingKind.Modern; alternate = UnitTypeIds.Riflemen; }
            if (Expanded)
            {
                main = UnitTypeIds.Artillery;
                if (Known(AdvanceIds.GuerrillaWarfare))
                {
                    if (world.Civilizations[world.Globals.BestHumanRank].Government < Government.Republic
                        || world.Entities.Cities.At(selected).Owner != world.Entities.Cities.At(selected).Founder)
                    { main = UnitTypeIds.Cavalry; alternate = UnitTypeIds.Partisans; }
                    else
                    { alternate = UnitTypeIds.Fanatics; kind = BarbarianUprisingKind.Fanatic; }
                }
            }
        }
        UnitSlot? final = null;
        for (int remaining = n; remaining >= 0; --remaining)
        {
            int type = remaining == n ? UnitTypeIds.Diplomat : (remaining & 4) != 0 ? main : alternate;
            final = units.Create(new((byte)type), new(0), point);
            if (final is { } created)
            {
                ref var unit = ref world.Entities.Units.At(created);
                if (type == UnitTypeIds.Fanatics)
                { unit.Flags.Veteran = true; }
                unit.Visibility = unit.Visibility.Union(world.Map.TileAt(point).RevealedTo);
            }
        }
        if (final is { } last && !world.Multiplayer.Saved.AutoplayActive
            && world.Entities.Cities.At(selected).Owner.Value == world.Multiplayer.LocalHuman
            && world.Map.TileAt(point).RevealedTo.Contains(world.Multiplayer.LocalHuman))
        {
            var notice = new BarbarianUprisingNotice(world.Multiplayer.LocalHuman, kind,
                world.Entities.Cities.At(selected).Name, new(last, world.Entities.Units.At(last).Token), point);
            operation.Present(() => effects.Notice(notice));
        }
        if (world.Globals.HumanPlayers.Contains(world.Entities.Cities.At(selected).Owner.Value))
        { operation.Present(() => effects.Redraw(point)); }
    }
}
