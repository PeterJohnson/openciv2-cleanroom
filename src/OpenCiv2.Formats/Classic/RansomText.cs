using OpenCiv2.Contracts;
using OpenCiv2.Core;

namespace OpenCiv2.Formats.Classic;

/// <summary>W06 CITY/BARBARIANS variants using F03 scenario/install lookup and selected language.
/// Closes its stream on every exit. Malformed missing message content fails explicitly under P04.</summary>
public sealed class ClassicRansomText(TextReader reader) : IBarbarianRansomText
{
    public BarbarianRansomText Read(MapPoint city)
    {
        try
        {
            if (!reader.Open("CITY", "BARBARIANS"u8.ToArray()))
            { return new(false, null); }
            reader.NextLine();
            int count = TextReader.ParseDecimal(reader.Line);
            if (count <= 0)
            { return new(true, null); }
            int selected = unchecked(3 * city.Y + 5 * city.X) % count;
            for (int i = 0; i <= selected; ++i)
            {
                if (!reader.NextLine())
                { throw new InvalidDataException("CITY/BARBARIANS is missing the selected ransom message"); }
            }
            return new(true, ClassicText.DecodeTerminated(reader.Line.ToArray()));
        }
        finally { reader.Dispose(); }
    }
}
