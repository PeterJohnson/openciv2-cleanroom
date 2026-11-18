using OpenCiv2.Base;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void ContentIdentityAndNames()
    {
        Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", ContentIdentity.Sha256([]));
        Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ContentIdentity.Sha256("abc"u8));
        Equal("cdc76e5c9914fb9281a1c7e284d73e67f1809a48a497200e046d39ccc7112cd0", ContentIdentity.Sha256(Enumerable.Repeat((byte)'a', 1000000).ToArray()));
        Equal("000f10ff", ContentIdentity.HexBytes([0, 15, 16, 255]));
        Equal(true, TextComparison.AsciiNameEqual("Name"u8, "nAME"u8));
        Equal(true, TextComparison.AsciiNameEqual([0xff, (byte)'a'], [0xff, (byte)'A']));
        Equal(false, TextComparison.AsciiNameEqual([0xff], []));
        Equal(false, TextComparison.AsciiNameEqual([(byte)'a', 0xff], [(byte)'b', 0xfe]));
        Throws<ComparisonUnavailableException>(() => TextComparison.AsciiNameEqual([0xff], [0xfe]));
        Throws<ComparisonUnavailableException>(() => TextComparison.AsciiNameEqual([0xff], [(byte)'a']));
    }

    private static void PollOnlyCancellation()
    {
        StopToken empty = default;
        Equal(false, empty.StopRequested); empty.CheckStop();
        var source = new CancellationSource();
        StopToken first = source.Token, second = source.Token;
        Equal(false, first.StopRequested);
        Equal(true, source.RequestStop()); Equal(false, source.RequestStop());
        Equal(true, first.StopRequested); Equal(true, second.StopRequested);
        Throws<CancelledException>(first.CheckStop);
        Equal(false, empty.StopRequested);
    }
}
