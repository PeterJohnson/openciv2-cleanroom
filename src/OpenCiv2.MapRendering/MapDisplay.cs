namespace OpenCiv2.MapRendering;

public enum SentryAppearance { Greyscale, Silhouette }
public enum MapColorCorrection { Normal, HighContrast, Greyscale }

/// <summary>P01/P02 local presentation preferences. They never enter the session.</summary>
public readonly record struct MapDisplay(bool Bare = false, bool Grid = false, bool CityRadius = false,
    bool Ownership = false, bool ReducedMotion = false, bool FollowActive = false,
    SentryAppearance Sentry = SentryAppearance.Greyscale, MapColorCorrection Color = MapColorCorrection.Normal);
