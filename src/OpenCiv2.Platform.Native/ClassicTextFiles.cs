using OpenCiv2.Base;
using OpenCiv2.Formats.Classic;
using OpenCiv2.IO.Native;

namespace OpenCiv2.Platform.Native;

/// <summary>Native F03 text adapter. Physical seeks, CRLF translation and byte windows; no locale decoding.</summary>
public sealed class ClassicTextFiles(ClassicStorage storage)
{
    public OpenedText Open(string directory, string name)
    {
        var stream = storage.OpenReadStream(directory, name, out string path);
        return stream is null ? new() : new(ClassicStorage.ClassicPath(path), new NativeText(stream));
    }

    private sealed class NativeText(FileStream stream) : ITextStream
    {
        private bool closed;

        private void RequireOpen() => ObjectDisposedException.ThrowIf(closed, this);

        public TextRead Read()
        {
            RequireOpen();
            var bytes = new List<byte>();
            try
            {
                while (bytes.Count < 254)
                {
                    int c = stream.ReadByte();
                    if (c < 0)
                    {
                        break;
                    }

                    if (c is 0 or 26)
                    {
                        stream.Seek(-1, SeekOrigin.Current);
                        throw new TextException(
                            TextFault.UnsupportedControl,
                            "NUL/control-Z needs supplied classic text semantics");
                    }
                    if (c == 13)
                    {
                        int next = stream.ReadByte();
                        if (next == 10)
                        {
                            c = 10;
                        }
                        else if (next >= 0)
                        {
                            stream.Seek(-1, SeekOrigin.Current);
                        }
                    }
                    bytes.Add((byte)c);
                    if (c == 10)
                    {
                        break;
                    }
                }
            }
            catch (IOException)
            {
                return new(TextReadStatus.Failure, OwnedList<byte>.Empty);
            }
            return new(bytes.Count == 0 ? TextReadStatus.End : TextReadStatus.Line, new(bytes));
        }

        public bool Seek(long physicalOffset)
        {
            RequireOpen();
            if (physicalOffset < 0)
            {
                return false;
            }

            try
            {
                stream.Seek(physicalOffset, SeekOrigin.Begin);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public void Close()
        {
            if (closed)
            {
                return;
            }

            closed = true;
            stream.Dispose();
        }
    }
}
