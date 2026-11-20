using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Session;

public sealed record FileType(string Description, string Pattern);
public sealed record FileChoiceRequest(
    bool Save,
    bool RequireExisting,
    string Title,
    string InitialPath,
    string DefaultExtension,
    OwnedList<FileType> Types);
public enum FileChoiceStatus { Accepted, Cancelled, Failed }
public sealed record FileChoiceResult(
    FileChoiceStatus Status = FileChoiceStatus.Cancelled,
    string Path = "",
    string Error = "");
/// <summary>One owned interaction. Begin returns promptly; Poll makes bounded progress and consumes a result once.
/// Cancel dismisses presentation and must not throw, including after begin failure or completion.</summary>
public interface IFileChooser
{
    void Begin(FileChoiceRequest request);

    FileChoiceResult? Poll();

    void Cancel();
}
public static class ClassicFileChoice
{
    /// <summary>F06 §11 semantic types in original order; platform filter encoding is outside the session.</summary>
    public static OwnedList<FileType> Types(
        GameMode mode,
        bool scenario,
        bool rawMap,
        bool currentOnly,
        Func<int, string> label)
    {
        ArgumentNullException.ThrowIfNull(label);
        var result = new List<FileType>();
        void Append(int index, string extension) => result.Add(new(label(index), "*." + extension));
        if (scenario)
        {
            Append(415, "scn");
        }
        else if (rawMap)
        {
            Append(261, "mp");
        }
        else
        {
            switch (mode)
            {
                case GameMode.SinglePlayer:
                    Append(238, "sav");
                    break;
                case GameMode.Hotseat:
                    Append(855, "hot");
                    break;
                case GameMode.Email:
                    Append(858, "eml");
                    break;
                case >= GameMode.Tcp and <= GameMode.Serial:
                    Append(861, "net");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
            if (!currentOnly)
            {
                if (mode != GameMode.SinglePlayer)
                {
                    Append(238, "sav");
                }

                if (mode < GameMode.Tcp)
                {
                    Append(861, "net");
                }

                if (mode != GameMode.Hotseat)
                {
                    Append(855, "hot");
                }
            }
        }
        return new(result);
    }

    public static FileChoiceResult Validate(FileChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Status switch
        {
            FileChoiceStatus.Accepted => string.IsNullOrEmpty(result.Path) || result.Path.Contains('\0')
                ? new(FileChoiceStatus.Failed, Error: "Invalid selected path")
                : result,
            FileChoiceStatus.Cancelled or FileChoiceStatus.Failed => result with { Path = "" },
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    public static FileChoiceResult Choose(FileChoiceRequest request, Func<FileChoiceRequest, FileChoiceResult> choose)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(choose);
        return Validate(choose(request));
    }
}
