using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.Session;

/// <summary>F04 §9 host file operations. Directory changes select provider state, not process cwd.</summary>
public sealed record ClassicAutosaveServices(IFileStorage Storage, Func<string, string, bool> Rename,
    Action<string> ChangeDirectory, Func<string> Label);

/// <summary>The authored desktop profile imports files from memory and saves in this host directory.</summary>
public sealed record LocalAutosaveTarget(string Directory, ClassicAutosaveServices Services);

/// <summary>F04 §9, synchronous on the trusted session lane after upkeep and before movement.</summary>
public static class ClassicAutosave
{
    public static void Run(OperationContext context, ClassicFileState files, ClassicAutosaveServices services, int civilization)
    {
        var world = context.World;
        if (!world.Globals.GameOptions.Autosave)
        { return; }
        if (world.Globals.Multiplayer != GameMode.SinglePlayer || world.Production.CivilizationTurnInProgress
            || world.PlayerPhaseActive)
        { throw new InvalidOperationException("Autosave requires completed single-player upkeep before movement"); }

        void Directory(string path)
        {
            files.Text.Working = path;
            services.ChangeDirectory(path);
        }
        try
        {
            Directory(files.ScenarioDirectory);
            if (civilization != world.Multiplayer.LocalHuman)
            { return; }
            files.Extension = ClassicGameFiles.SelectExtension(false, world.Globals.Multiplayer, files.Extension);
            byte[] name = ClassicText.EncodeField(OutcomeText.Leader(context, civilization).Split('\0')[0]);
            string stem = ClassicText.DecodeField(name.AsSpan(0, Math.Min(2, name.Length))).Trim(' ', '\t')
                + "_" + services.Label();
            string current = stem + files.Extension, backup = stem + "2" + files.Extension;
            if (world.Globals.Turn % 8 is >= 0 and <= 3)
            {
                _ = services.Storage.Remove(backup);
                _ = services.Rename(current, backup);
            }
            _ = ClassicGameFiles.SaveUpkeep(context, files, current, services.Storage);
        }
        finally
        { Directory(files.Text.Home); }
    }
}
