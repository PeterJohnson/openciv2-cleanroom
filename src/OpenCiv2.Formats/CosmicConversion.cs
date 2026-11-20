using OpenCiv2.Core.Rules;

namespace OpenCiv2.Formats;

/// <summary>F03 §4 ordered COSMIC fields and shared classic/native input normalization.</summary>
public static class CosmicConversion
{
    public const int Count = 22;
    public static CosmicRules Decode(IReadOnlyList<int> values)
    {
        if (values.Count != Count)
        {
            throw new ArgumentException("Expected 22 cosmic operands", nameof(values));
        }

        byte Clamp(int index, int low, int high) => (byte)Math.Clamp(values[index], low, high);
        byte foodRows = Clamp(3, 4, 20);
        if (foodRows % 2 != 0)
        {
            ++foodRows;
        }

        return new()
        {
            RoadMovementMultiplier = Clamp(0, 1, 10),
            TriremeLossDenominator = Clamp(1, 1, 100),
            FoodPerCitizen = Clamp(2, 0, 10),
            FoodBoxRows = foodRows,
            ShieldBoxRows = Clamp(4, 4, 20),
            SettlerFoodLowGovernment = Clamp(5, 0, 10),
            SettlerFoodHighGovernment = Clamp(6, 0, 10),
            UnhappinessCitySize = Clamp(7, 4, 12),
            UnhappinessEmpireSize = Clamp(8, 10, 100),
            AqueductSize = Clamp(9, 4, 50),
            SewerSize = Clamp(10, 4, 50),
            TechnologyCostParadigm = Clamp(11, 3, 10),
            TransformationWork = Clamp(12, 5, 100),
            MonarchyFreeSupport = Clamp(13, 0, 8),
            CommunismFreeSupport = Clamp(14, 0, 8),
            FundamentalismFreeSupport = Clamp(15, 0, 8),
            CommunismPalaceDistance = Clamp(16, 1, 20),
            FundamentalismScienceLossPercent = Clamp(17, 0, 100),
            ProductionSwitchLossPercent = Clamp(18, 0, 100),
            ParadropRange = Clamp(19, 4, 100),
            SpaceshipMassThrustParadigm = Clamp(20, 25, 200),
            FundamentalismScienceCapTenths = Clamp(21, 0, 10),
        };
    }
    public static byte[] Encode(CosmicRules value) =>
    [
        value.RoadMovementMultiplier,
        value.TriremeLossDenominator,
        value.FoodPerCitizen,
        value.FoodBoxRows,
        value.ShieldBoxRows,
        value.SettlerFoodLowGovernment,
        value.SettlerFoodHighGovernment,
        value.UnhappinessCitySize,
        value.UnhappinessEmpireSize,
        value.AqueductSize,
        value.SewerSize,
        value.TechnologyCostParadigm,
        value.TransformationWork,
        value.MonarchyFreeSupport,
        value.CommunismFreeSupport,
        value.FundamentalismFreeSupport,
        value.CommunismPalaceDistance,
        value.FundamentalismScienceLossPercent,
        value.ProductionSwitchLossPercent,
        value.ParadropRange,
        value.SpaceshipMassThrustParadigm,
        value.FundamentalismScienceCapTenths,
    ];
}
