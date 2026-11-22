using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void UnitCreationPlacementAndHome()
    {
        var f = new RevelationFixture();
        MapPoint point = new(10, 10);
        f.AddCity(0, 1, new(6, 10));
        f.AddCity(1, 2, new(10, 6)); // Equal nearest distance: higher foreign slot wins.
        f.Unit(0) = new()
        {
            Damage = 5,
            MovesUsed = 99,
            AiTask = 7,
            Counter = 8,
            AuxiliaryTimer = 9,
            Visibility = CivilizationSet.Only(6),
            Flags = new() { Veteran = true, Aboard = true },
            Orders = UnitOrder.Sleeping,
            Home = new(99),
            Carrier = new(7),
            Previous = new(99),
            Next = new(98)
        };
        f.World.Entities.Units.SetEnd(1);
        Equal(new UnitSlot(0), f.Create(0, 1, point)!.Value);
        Equal((short)1, f.World.Civilizations[1].MilitaryCount);
        Equal((byte)1, f.World.Civilizations[1].UnitsActive[0]);
        Equal(1, f.World.UnitTotals[1]);
        Equal((uint)2, f.Process.Tokens.NextUnit);
        Equal((CitySlot?)null, f.Unit(0).Home);
        Equal((byte)0, f.Unit(0).MovesUsed);
        Equal((byte)0, f.Unit(0).Damage);
        Equal((byte)0x58, f.Unit(0).AiTask);
        Equal((byte)0, f.Unit(0).Counter);
        Equal((byte)0, f.Unit(0).AuxiliaryTimer);
        Equal(byte.MaxValue, f.Unit(0).LastDirection);
        Equal(default(UnitStatus), f.Unit(0).Flags);
        Equal(UnitOrder.None, f.Unit(0).Orders);
        Equal((UnitSlot?)null, f.Unit(0).Carrier);
        Equal(new MapPoint(-1, -1), f.Unit(0).Destination);
        Equal(point, f.Unit(0).Position);
        Equal(false, f.Unit(0).Visibility.Contains(6));
        Equal(true, f.World.Map.TileAt(point).RevealedTo.Contains(1));
        Equal(MapFeatures.Unit, f.World.Map.Remembered(1, point));
        Equal((byte?)1, f.World.Map.UnitOwner(point));
        Equal(0, f.Notices.Count);

        f.World.Entities.Cities.Storage[1].Owner = new(1);
        Equal(new UnitSlot(1), f.Create(0, 1, point)!.Value);
        Equal(new CitySlot(1), f.Unit(1).Home!.Value);
        Equal(2, f.Stacks.Count(0));
        Equal(new UnitSlot(1), f.Unit(0).Previous!.Value);
        Equal(new UnitSlot(0), f.Unit(1).Next!.Value);
        Equal(new UnitSlot(2), f.Create(0, 0, new(24, 10))!.Value);
        Equal((CitySlot?)null, f.Unit(2).Home);
    }

    private static void UnitCreationRetainedDamageAndWrap()
    {
        var f = new RevelationFixture();
        f.World.Globals.Multiplayer = GameMode.Tcp;
        f.World.Multiplayer.NetworkHost = true;
        f.World.Multiplayer.Saved.Simultaneous = true;
        f.Unit(0).Damage = 5;
        f.Process.Tokens.NextUnit = uint.MaxValue;
        f.World.Civilizations[1].MilitaryCount = short.MaxValue;
        f.World.Civilizations[1].UnitsActive[0] = byte.MaxValue;
        f.World.UnitTotals[1] = int.MaxValue;
        f.World.Entities.Units.SetEnd(1); // A hole ignores extension limits.
        f.OnReveal = slot =>
        {
            Equal((byte)3, f.Unit(slot).MovesUsed);
            Equal((byte)0, f.Unit(slot).Damage);
        };
        Equal(new UnitSlot(0), f.Create(0, 1, new(10, 10))!.Value);
        Equal(short.MinValue, f.World.Civilizations[1].MilitaryCount);
        Equal((byte)0, f.World.Civilizations[1].UnitsActive[0]);
        Equal(int.MinValue, f.World.UnitTotals[1]);
        Equal(uint.MaxValue, f.Unit(0).Token.Value);
        Equal((uint)0, f.Process.Tokens.NextUnit);
        Equal(1, f.Synchronizations);

        f.World.Globals.Multiplayer = GameMode.SinglePlayer;
        f.OnReveal = null;
        Equal(new UnitSlot(1), f.Create(0, 1, new(20, 10))!.Value);
        Equal((uint)0, f.Unit(1).Token.Value);
        Equal((uint)1, f.Process.Tokens.NextUnit);
        Equal((short)2, f.World.Globals.UnitEnd);
        Equal(true, f.World.Map.TileAt(new(20, 10)).RevealedTo.Contains(1));
        Equal((byte)0, f.Unit(1).MovesUsed);
    }

    private static void UnitCreationCapacityAndFailures()
    {
        var f = new RevelationFixture();
        f.World.Civilizations[1].MilitaryCount = 1949;
        f.Unit(0).Damage = 71;
        Equal((UnitSlot?)null, f.Create(0, 1, new(10, 10)));
        Equal(UnitCreationNotice.TooManyUnits, f.Notices.Single());
        Equal((uint)1, f.Process.Tokens.NextUnit);
        Equal((byte)71, f.Unit(0).Damage);
        Equal(0, f.World.UnitTotals[1]);
        Equal(0, f.World.Entities.Units.End);
        f.World.Civilizations[1].MilitaryCount = 0;
        f.OnReveal = slot => throw new InvalidOperationException("Revelation child failed");
        Throws<InvalidOperationException>(() => f.Create(0, 1, new(10, 10)));
        Equal((uint)2, f.Process.Tokens.NextUnit);
        Equal(true, f.Unit(0).Token.Occupied);
        Equal(new MapPoint(10, 10), f.Unit(0).Position);
        Equal(1, f.World.UnitTotals[1]);
        Equal(false, f.World.Map.TileAt(new(10, 10)).RevealedTo.Contains(1));
        f.OnReveal = slot => f.Unit(slot).Token = new(0);
        Equal(new UnitSlot(1), f.Create(0, 1, new(20, 10))!.Value);
        Equal(false, f.Unit(1).Token.Occupied);
    }

    private static void UnitCreationNotices()
    {
        foreach (var (type, expected) in new[]
        {
            (1, UnitCreationNotice.Ships), (2, UnitCreationNotice.Aircraft), (3, UnitCreationNotice.Caravan)
        })
        {
            var f = new RevelationFixture();
            f.World.Globals.Turn = 1;
            f.Create(type, 1, new(10, 10));
            Equal(0, f.Notices.Count);
            Equal(type == 1, f.World.Globals.ShownEventFlags.Ships);
            Equal(type == 2, f.World.Globals.ShownEventFlags.Aircraft);
            Equal(type == 3, f.World.Globals.ShownEventFlags.Caravan);
            f.World.Globals.GameOptions.Tutorial = true;
            f.Create(type, 1, new(10, 10));
            Equal(0, f.Notices.Count); // Disabled tutorials still consumed the marker.
            f.World.Globals.ShownEventFlags = default;
            f.OnNotice = notice =>
            {
                Equal(expected, notice);
                f.World.Globals.ShownEventFlags.Government = true;
            };
            f.Create(type, 1, new(10, 10));
            Equal(expected, f.Notices.Single());
            Equal(true, f.World.Globals.ShownEventFlags.Government);
        }

        var military = new RevelationFixture();
        military.World.Globals.Turn = 1;
        military.World.Globals.GameOptions.Tutorial = true;
        military.OnNotice = notice =>
        {
            if (notice == UnitCreationNotice.FirstMilitary)
            {
                military.World.Civilizations[1].MilitaryCount = 2;
            }
        };
        military.Create(0, 1, new(10, 10));
        Equal(2, military.Notices.Count);
        Equal(UnitCreationNotice.FirstMilitary, military.Notices[0]);
        Equal(UnitCreationNotice.SecondMilitary, military.Notices[1]);
    }
}
