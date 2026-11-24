using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // Specification-derived F04 §6 pass boundaries with actual A08 adjustments
    // and C01 transfer/removal/creation. These do not define the open city formulas.
    private static void CityPassEntryAndEmptyCompletion()
    {
        var f = new AssessmentFixture();
        f.Civ.BestShieldSurplus = 123;
        f.World.Civilizations[2].BestShieldSurplus = 456;
        for (int g = 0; g < RuleLimits.Governments; ++g) { f.Civ.GovernmentScore[g] = 12; }
        for (int w = 0; w < RuleLimits.Wonders; ++w)
        {
            f.World.Production.WonderNotices[1][w].Started = true;
            f.World.Production.WonderNotices[2][w].Started = true;
        }
        f.World.Globals.TransientFlags.AttitudeDirty = true;
        var pass = new CityPass(f.World, f.Policy).Begin(1);
        Equal((short)0, f.Civ.BestShieldSurplus); Equal((short)456, f.World.Civilizations[2].BestShieldSurplus);
        for (int g = 0; g < RuleLimits.Governments; ++g) { Equal((short)0, f.Civ.GovernmentScore[g]); }
        for (int w = 0; w < RuleLimits.Wonders; ++w)
        {
            Equal(false, f.World.Production.WonderNotices[1][w].Started);
            Equal(true, f.World.Production.WonderNotices[2][w].Started);
        }
        Equal(false, f.World.Globals.TransientFlags.AttitudeDirty);
        Equal((CitySlot?)null, pass.NextCity());
        f.Civ.GovernmentScore[(int)Government.Anarchy] = 30000;
        f.Civ.GovernmentScore[(int)Government.Despotism] = 2000;
        f.Civ.ScienceRate = 3; f.Civ.TaxRate = 4;
        f.Data.Process.Rules.Leaders[0].Attack = -1;
        f.World.Globals.BestHumanRank = 7;
        f.World.Civilizations[7].Gold = 100;
        f.World.Civilizations[7].SubversionCount = 2;
        f.World.Civilizations[7].UnitsActive[UnitTypeIds.Diplomat] = 3;
        f.World.Production.WonderNotices[1][27].Started = true;
        pass.Finish(); pass.Finish();
        Equal(true, pass.Finished); Equal((CitySlot?)null, pass.NextCity());
        Equal(unchecked((short)34000), f.Civ.GovernmentScore[(int)Government.Anarchy]);
        Equal((short)20, f.Civ.GovernmentScore[(int)Government.Democracy]);
        Equal((byte)3, f.Civ.ScienceRate); Equal((byte)4, f.Civ.TaxRate);
        Equal(false, f.World.Production.WonderNotices[1][27].Started);
        f.Random.RequireChoicesConsumed();
    }

    private static void CityPassLiveDescendingTraversal()
    {
        var f = new AssessmentFixture();
        f.Data.AddCity(0, 1, new(4, 10));
        f.Data.AddCity(1, 2, new(10, 10));
        f.Data.AddCity(2, 1, new(16, 10));
        f.Data.AddCity(3, 1, new(22, 10));
        f.Data.AddCity(4, 2, new(28, 10));
        for (int i = 0; i < 5; ++i) { f.World.Entities.Cities.Storage[i].Flags.Airlifted = true; }
        var pass = new CityPass(f.World, f.Policy).Begin(1);
        Equal<CitySlot?>(new(3), pass.NextCity());
        Equal(false, f.World.Entities.Cities.Storage[3].Flags.Airlifted);
        Equal(true, f.World.Entities.Cities.Storage[4].Flags.Airlifted);
        // Actual children may transfer lower cities and create a higher slot.
        CityTransfer.Transfer(f.World, new(2), 1, 2);
        CityTransfer.Transfer(f.World, new(1), 2, 1);
        Equal<CitySlot?>(new(5), CityCreator(f.Data).Create(new(1), new(32, 10)));
        f.World.Entities.Cities.Storage[5].Flags.Airlifted = true;
        new CityRemoval(f.World, f.Rules, f.Data.Stacks, f.Lifecycle.Loss, _ => { }).Delete(0);
        pass.CompleteCity(1);
        Equal<CitySlot?>(new(1), pass.NextCity()); pass.CompleteCity(1);
        Equal((CitySlot?)null, pass.NextCity()); pass.Finish();
        Equal(true, f.World.Entities.Cities.Storage[2].Flags.Airlifted);
        Equal(false, f.World.Entities.Cities.Storage[1].Flags.Airlifted);
        Equal(true, f.World.Entities.Cities.Storage[5].Flags.Airlifted);
        f.Random.RequireChoicesConsumed();
    }

    private static void CityPassRememberedSizes()
    {
        foreach (short apollo in new short[] { -1, -2, 0 })
        {
            var f = new AssessmentFixture(); f.Data.AddCity(0, 1, new(10, 10), 7);
            f.Civ.Treaties[3].Alliance = true;
            f.World.Civilizations[2].Treaties[4].Alliance = true;
            f.World.Globals.CivsAlive = CivilizationSet.Only(1).With(2);
            f.World.Globals.WonderCity[WonderIds.Apollo] = apollo;
            var pass = new CityPass(f.World, f.Policy).Begin(1);
            Equal<CitySlot?>(new(0), pass.NextCity());
            CityTransfer.Transfer(f.World, new(0), 1, 2);
            pass.CompleteCity(0);
            ref var city = ref f.World.Entities.Cities.Storage[0];
            Equal(default(CivilizationSet), city.KnownTo);
            Equal((byte)0, city.RememberedSize[0]);
            for (int observer = 1; observer < EntityLimits.Civilizations; ++observer)
            { Equal((byte)(observer == 3 || apollo != -1 ? 7 : 0), city.RememberedSize[observer]); }
            Equal((CitySlot?)null, pass.NextCity()); pass.Finish();
            f.Random.RequireChoicesConsumed();
        }
    }

    private static void CityPassRemovedResultAndReuse()
    {
        var f = new AssessmentFixture(); f.Data.AddCity(0, 1, new(10, 10), 9);
        f.World.Entities.Cities.Storage[0].Flags.Disorder = true;
        f.Civ.Treaties[3].Alliance = true;
        f.Ai(); f.Civ.ScienceRate = f.Civ.TaxRate = 3;
        var pass = new CityPass(f.World, f.Policy).Begin(1);
        Equal<CitySlot?>(new(0), pass.NextCity());
        new CityRemoval(f.World, f.Rules, f.Data.Stacks, f.Lifecycle.Loss, _ => { }).Delete(0);
        pass.CompleteCity(null);
        Equal((byte)0, f.World.Entities.Cities.Storage[0].RememberedSize[3]);
        Equal((CitySlot?)null, pass.NextCity()); pass.Finish();
        Equal(3, 10 - f.Civ.ScienceRate - f.Civ.TaxRate); // Removed size/disorder did not contribute.

        f = new(); f.Data.AddCity(0, 1, new(10, 10)); f.Data.AddCity(1, 1, new(20, 10));
        pass = new CityPass(f.World, f.Policy).Begin(1);
        Equal<CitySlot?>(new(1), pass.NextCity());
        new CityRemoval(f.World, f.Rules, f.Data.Stacks, f.Lifecycle.Loss, _ => { }).Delete(0);
        Equal<CitySlot?>(new(0), CityCreator(f.Data).Create(new(1), new(12, 10)));
        pass.CompleteCity(1);
        Equal<CitySlot?>(new(0), pass.NextCity());
        Equal(new MapPoint(12, 10), f.World.Entities.Cities.Storage[0].Position);
        pass.CompleteCity(1); Equal((CitySlot?)null, pass.NextCity()); pass.Finish();
        f.Random.RequireChoicesConsumed();
    }

    private static void CityPassAiSummaryRates()
    {
        foreach (var (size, balance, disorder, expectedLuxury) in new[]
        {
            (5, 0, false, 3), (6, 0, false, 4), (6, 1, false, 3), (6, -1, true, 4)
        })
        {
            var f = new AssessmentFixture(); f.Ai(); f.Civ.ScienceRate = f.Civ.TaxRate = 3;
            f.Data.AddCity(0, 1, new(10, 10), size);
            f.World.Entities.Cities.Storage[0].Flags.Disorder = disorder;
            var pass = new CityPass(f.World, f.Policy).Begin(1);
            Equal<CitySlot?>(new(0), pass.NextCity()); pass.CompleteCity(balance);
            Equal((CitySlot?)null, pass.NextCity()); pass.Finish();
            Equal(expectedLuxury, 10 - f.Civ.ScienceRate - f.Civ.TaxRate);
            f.Random.RequireChoicesConsumed();
        }
    }

    private static void CityPassAdmissionAndRequiredFailure()
    {
        var f = new AssessmentFixture(); f.Civ.BestShieldSurplus = 25;
        var service = new CityPass(f.World, f.Policy);
        Throws<ArgumentOutOfRangeException>(() => service.Begin(-1));
        Throws<ArgumentOutOfRangeException>(() => service.Begin(8));
        f.World.Globals.Multiplayer = GameMode.Tcp;
        Throws<InvalidOperationException>(() => service.Begin(1));
        Equal((short)25, f.Civ.BestShieldSurplus);
        f.World.Globals.Multiplayer = GameMode.SinglePlayer;
        f.Data.AddCity(0, 1, new(10, 10));
        var pass = service.Begin(1);
        Throws<InvalidOperationException>(() => pass.CompleteCity(0));
        Throws<InvalidOperationException>(() => pass.Finish());
        Equal<CitySlot?>(new(0), pass.NextCity());
        Throws<InvalidOperationException>(() => pass.NextCity());
        pass.CompleteCity(1); Equal((CitySlot?)null, pass.NextCity());
        f.Civ.GovernmentScore[(int)Government.Despotism] = 7;
        f.World.Globals.BestHumanRank = 255; // Required A08 query cannot resolve this invalid imported index.
        Throws<IndexOutOfRangeException>(() => pass.Finish());
        Equal(false, pass.Finished);
        Equal((short)14, f.Civ.GovernmentScore[(int)Government.Anarchy]);
        f.World.Globals.BestHumanRank = 0;
        Throws<InvalidOperationException>(() => pass.Finish());
        Equal((short)14, f.Civ.GovernmentScore[(int)Government.Anarchy]);
        f.Random.RequireChoicesConsumed();
    }
}
