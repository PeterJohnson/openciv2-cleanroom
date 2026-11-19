using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

/// <summary>A conservative change set computed only from two already disclosed observations.
/// Captured list identities prevent a copied record's stale metadata from certifying other data.
/// No references to observations are retained, so metadata cannot chain snapshot lifetimes.</summary>
public sealed class MapObservationDelta
{
    private readonly ClientIdentity identity;
    private readonly ulong from, to;
    private readonly int width, height;
    private readonly bool flat;
    private readonly OwnedList<ObservedTile> oldTiles, newTiles;
    private readonly OwnedList<ObservedMapUnit> oldUnits, newUnits;
    private readonly OwnedList<ObservedMapCity> oldCities, newCities;
    private readonly MapAnnotations? oldAnnotations, newAnnotations;
    public OwnedList<int> ChangedIndices { get; }
    public bool AnnotationsChanged { get; }
    public bool CitiesChanged { get; }

    private MapObservationDelta(MapObservation before, MapObservation after, IEnumerable<int> indices, bool citiesChanged)
    {
        identity = before.Identity;
        from = before.Revision; to = after.Revision;
        width = after.Width; height = after.Height; flat = after.Flat;
        oldTiles = before.Tiles; newTiles = after.Tiles;
        oldUnits = before.Units; newUnits = after.Units;
        oldCities = before.Cities; newCities = after.Cities;
        oldAnnotations = before.Annotations; newAnnotations = after.Annotations;
        ChangedIndices = new(indices.Order());
        AnnotationsChanged = !Equals(oldAnnotations, newAnnotations);
        CitiesChanged = citiesChanged;
    }

    public bool Matches(MapObservation before, MapObservation after) =>
        before.Identity == identity && after.Identity == identity && before.Revision == from && after.Revision == to &&
        from != ulong.MaxValue && to == from + 1 && before.Width == width && after.Width == width &&
        before.Height == height && after.Height == height && before.Flat == flat && after.Flat == flat &&
        ReferenceEquals(before.Tiles, oldTiles) && ReferenceEquals(after.Tiles, newTiles) &&
        ReferenceEquals(before.Units, oldUnits) && ReferenceEquals(after.Units, newUnits) &&
        ReferenceEquals(before.Cities, oldCities) && ReferenceEquals(after.Cities, newCities) &&
        ReferenceEquals(before.Annotations, oldAnnotations) && ReferenceEquals(after.Annotations, newAnnotations);

    public static MapObservation Attach(MapObservation? before, MapObservation after)
    {
        after = after with { Delta = null };
        if (before is null || before.Identity != after.Identity || before.Revision == ulong.MaxValue ||
            after.Revision != before.Revision + 1 || before.Width != after.Width || before.Height != after.Height ||
            before.Flat != after.Flat || before.Tiles.Count != after.Tiles.Count) return after;
        var changed = new HashSet<int>();
        if (!ReferenceEquals(before.Tiles, after.Tiles))
            for (int i = 0; i < after.Tiles.Count; i++)
                if (before.Tiles[i] != after.Tiles[i]) changed.Add(i);
        // Ordering is observable for stacks; comparing corresponding entries also
        // catches reorderings, removals and insertions without private entity data.
        for (int i = 0; i < Math.Max(before.Units.Count, after.Units.Count); i++)
        {
            if (i < before.Units.Count && i < after.Units.Count && before.Units[i] == after.Units[i]) continue;
            if (i < before.Units.Count) Add(before.Units[i].Position);
            if (i < after.Units.Count) Add(after.Units[i].Position);
        }
        bool citiesChanged = false;
        for (int i = 0; i < Math.Max(before.Cities.Count, after.Cities.Count); i++)
        {
            if (i < before.Cities.Count && i < after.Cities.Count && before.Cities[i] == after.Cities[i]) continue;
            citiesChanged = true;
            if (i < before.Cities.Count) Add(before.Cities[i].Position);
            if (i < after.Cities.Count) Add(after.Cities[i].Position);
        }
        return after with { Delta = new(before, after, changed, citiesChanged) };

        void Add(MapPoint point)
        {
            if (point.X >= 0 && point.X < after.Width && point.Y >= 0 && point.Y < after.Height && ((point.X ^ point.Y) & 1) == 0)
                changed.Add(point.Y * (after.Width / 2) + point.X / 2);
        }
    }
}
