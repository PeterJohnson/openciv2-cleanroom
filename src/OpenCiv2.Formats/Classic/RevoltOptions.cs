using OpenCiv2.Core;

namespace OpenCiv2.Formats.Classic;

/// <summary>V06 DissidentOptions: all four lines are read even when a payment option will not be offered.
/// The owning content adapter supplies the filename and classic search directories.</summary>
public static class ClassicRevoltOptions
{
    public static RevoltOptionText? Read(TextReader reader, string filename)
    {
        try
        {
            if (!reader.Open(filename, "DISSIDENTOPTIONS"u8.ToArray()))
            { return null; }
            string Line()
            {
                if (!reader.NextLine())
                { throw new InvalidDataException("DissidentOptions requires four backed text lines"); }
                return ClassicText.DecodeWindows1252(reader.Line, "DissidentOptions");
            }
            var result = new RevoltOptionText(Line(), Line(), Line(), Line());
            reader.Close();
            return result;
        }
        catch
        {
            try
            { reader.Close(); }
            catch { }
            throw;
        }
    }
}
