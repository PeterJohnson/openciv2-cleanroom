using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using OpenCiv2.App;

internal static partial class Program
{
    private static ScopedMapPerformanceWorld? performanceWorld;

    // Test host only: authored engine state followed by the production scoped
    // projection, scene binding and real gallery input/rendering controls.
    [JSExport]
    public static void PerformanceSetup()
    {
        performanceWorld?.Dispose();
        var fixture = performanceWorld = new();
        var gallery = SeamApp.CurrentGallery!;
        int step = 0;
        gallery.ChangeObservation = previous =>
        {
            fixture.Change(++step);
            return fixture.Capture() with { Flat = previous.Flat };
        };
        gallery.SetObservation(fixture.Capture());
    }

    [JSExport]
    public static string PerformanceSnapshot()
    {
        var gallery = SeamApp.CurrentGallery!;
        var result = gallery.Snapshot();
        result["managedAllocatedBytes"] = GC.GetTotalAllocatedBytes(precise: true);
        result["managedHeapBytes"] = GC.GetTotalMemory(false);
        result["gcPauseMs"] = GC.GetTotalPauseDuration().TotalMilliseconds;
        result["gcCollections"] = JsonSerializer.SerializeToNode(new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) });
        result["observedUnits"] = gallery.Map.Scene!.Map.Units.Count;
        result["observedCities"] = gallery.Map.Scene.Map.Cities.Count;
        return result.ToJsonString();
    }
}
