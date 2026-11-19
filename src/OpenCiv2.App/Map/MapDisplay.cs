namespace OpenCiv2.App.Map;

internal enum SentryAppearance { Silhouette, Greyscale }
internal enum MapColorCorrection { Normal, HighContrast, Greyscale }

/// <summary>P01/P02 local presentation preferences. They never enter the session.</summary>
internal readonly record struct MapDisplay(bool Bare = false, bool Grid = false, bool CityRadius = false,
    bool Ownership = false, bool ReducedMotion = false, bool FollowActive = false,
    SentryAppearance Sentry = SentryAppearance.Silhouette, MapColorCorrection Color = MapColorCorrection.Normal);
