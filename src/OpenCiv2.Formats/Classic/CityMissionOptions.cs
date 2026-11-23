using OpenCiv2.Core;

namespace OpenCiv2.Formats.Classic;

/// <summary>Reads all seven ordinary SpyOptions lines, including hidden choices.
/// The content owner supplies the filename and separately resolved Spy annotation.</summary>
public static class ClassicCityMissionOptions
{
    public static CityMissionText? Read(TextReader reader, string filename, string spyAnnotation)
    {
        try
        {
            if (!reader.Open(filename, "SPYOPTIONS"u8.ToArray()))
            { return null; }
            string Line()
            {
                if (!reader.NextLine())
                { throw new InvalidDataException("SpyOptions requires seven backed text lines"); }
                return ClassicText.DecodeWindows1252(reader.Line, "SpyOptions");
            }
            var result = new CityMissionText(Line(), Line(), Line(), Line(), Line(), Line(), Line(), spyAnnotation);
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
