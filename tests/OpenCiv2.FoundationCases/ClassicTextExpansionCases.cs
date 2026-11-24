using System.Globalization;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // P04 §2 and adopted Appendix B selector domains; S02 actual retained event execution.
    private static TextParameters ExpansionParameters()
    {
        var p = new TextParameters(); p.Strings[0] = "alpha"; p.Strings[1] = "%NUMBER0"; p.Strings[10] = "ten";
        p.Numbers[0] = 42; p.Numbers[1] = -12; p.Numbers[2] = 65535; p.Numbers[3] = -1; p.Numbers[4] = int.MinValue;
        return p;
    }
    private static void ClassicExpansionTokenRules()
    {
        var expansion = ClassicTextExpansion.Capture(ExpansionParameters(), "768");
        Equal("alpha0!|-121x", expansion.Expand("%STRING00!|%NUMBER01x"));
        Equal("alpha/ten/768", expansion.Expand("%string0/%String10/%STRING11"));
        Equal("number0/hex2", expansion.Expand("%number0/%hex2"));
        Equal("%NUMBER0", expansion.Expand("%%NUMBER0")); Equal("%NUMBER0", expansion.Expand("%STRING1"));
        Equal("amissingb", expansion.Expand("a%missing%b")); Equal("abc", expansion.Expand("abc%")); Equal("", expansion.Expand(""));
        Equal("-121", expansion.Expand("%NUMBER+1")); Equal("-121", expansion.Expand("%NUMBER 1"));
        Equal("42xFF", expansion.Expand("%NUMBER0xFF")); Equal("alpha", expansion.Expand("%STRING!"));
        Equal("9", expansion.Expand("%STRING09")); Equal("ten0", expansion.Expand("%STRING010"));
        Equal("42294967296", expansion.Expand("%NUMBER4294967296"));
        Equal("ffffx/ffff2/0000/002a", expansion.Expand("%HEX2x/%HEX02/%HEX5/%HEX0"));
        Equal("-2147483648", expansion.Expand("%NUMBER4")); Equal("café alpha", expansion.Expand("café %STRING0"));
    }
    private static void ClassicExpansionBoundaries()
    {
        var p = ExpansionParameters(); var expansion = ClassicTextExpansion.Capture(p);
        foreach (string source in new[] { "%STRING11", "%STRING12", "%STRING-1", "%NUMBER6", "%NUMBER-1", "%HEX6", "%HEX3", "%STRING", "%NUMBER", "%HEX", "embedded\0tail" })
        { Throws<InvalidDataException>(() => expansion.Expand(source)); }
        Equal("alpha", expansion.Expand("%STRING0")); // Failure does not corrupt a reusable captured context.
        p.Numbers[0] = 65536; Throws<InvalidDataException>(() => ClassicTextExpansion.Capture(p).Expand("%HEX0"));
        p.Strings[0] = "bad\0text"; Throws<InvalidDataException>(() => ClassicTextExpansion.Capture(p).Expand("%STRING0"));
        Throws<ArgumentException>(() => new ClassicTextExpansion(new([1]), new(new string[11])));
        Throws<ArgumentException>(() => new ClassicTextExpansion(new(new int[6]), new(["one"])));
        Equal("", ClassicTextExpansion.Capture(new()).Expand("%STRING0")); Equal("0", ClassicTextExpansion.Capture(new()).Expand("%NUMBER0"));
    }
    private static void ClassicExpansionCaptureAndCulture()
    {
        var p = ExpansionParameters(); var expansion = ClassicTextExpansion.Capture(p, "native profile");
        p.Strings[0] = "changed"; p.Numbers[1] = 17;
        Equal("alpha -12 native profile", expansion.Expand("%STRING0 %NUMBER1 %STRING11"));
        Equal("changed 17", ClassicTextExpansion.Capture(p).Expand("%STRING0 %NUMBER1"));
        p.Strings[0] = "東京 %NUMBER1";
        Equal("東京 %NUMBER1", ClassicTextExpansion.Capture(p).Expand("%STRING0"));
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone(); custom.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = custom; Equal("-12 -2147483648", expansion.Expand("%NUMBER1 %NUMBER4"));
        }
        finally { CultureInfo.CurrentCulture = saved; }
        var input = new OwnedList<string>(["%STRING0", "", "%NUMBER1", "%STRING1"]);
        Equal(new OwnedList<string>(["alpha", "", "-12", "%NUMBER0"]), expansion.Expand(input));
        Equal("%STRING0", input[0]); Equal("%NUMBER1", input[2]);
    }
    private static void ClassicScenarioTextRetainsPreparedContent()
    {
        var data = new RevelationFixture(); var p = data.Process.TextParameters; p.Strings[0] = "Before"; p.Numbers[1] = 7;
        var prepare = new ClassicScenarioText(data.Process); var f = new EventFixture(data, prepareText: prepare.Prepare);
        var e = f.Text("%STRING0: %NUMBER1"); e.SetString(6, new("after hole: %STRING0")); e.SetString(7, new(""));
        e.Values.Actions.JustOnce = true; e.Values.Actions.ChangeMoney = true; e.Values.MoneyReceiverCiv = 1; e.Values.MoneyAmount = 9;
        int gold = data.World.Civilizations[1].Gold;
        using var operation = f.Dispatcher.Begin(e, "expanded");
        var pending = operation.Resume() as ScenarioText; Equal(true, pending is not null);
        Equal(new OwnedList<string>(["Before: 7", "after hole: Before", ""]), pending!.Lines);
        Equal(gold + 9, data.World.Civilizations[1].Gold); Equal(false, e.Values.Actions.Consumed);
        p.Strings[0] = "After"; p.Numbers[1] = 13; e.Strings[4].Value = "changed event";
        Equal(pending, operation.Resume()); Equal("Before: 7", pending.Lines[0]);
        Equal(null, operation.Resume(new(0))); Equal(true, e.Values.Actions.Consumed);
        var later = f.Text("%STRING0 %NUMBER1"); using var next = f.Dispatcher.Begin(later, "next");
        Equal("After 13", ((ScenarioText)next.Resume()!).Lines[0]); Equal(null, next.Resume(new(0))); f.Consumed();
    }
    private static void ClassicScenarioTextFailureAndAutoplay()
    {
        foreach (bool autoplay in new[] { false, true })
        {
            var data = new RevelationFixture(); var prepare = new ClassicScenarioText(data.Process);
            var f = new EventFixture(data, prepareText: prepare.Prepare); f.World.Multiplayer.Saved.AutoplayActive = autoplay;
            var e = f.Text("%NUMBER6"); e.Values.Actions.ChangeMoney = true; e.Values.MoneyReceiverCiv = 1; e.Values.MoneyAmount = 9;
            e.Values.Actions.JustOnce = true; e.Values.Actions.SuppressWonderMovies = true; int gold = f.World.Civilizations[1].Gold;
            using var op = f.Dispatcher.Begin(e, "failure"); Throws<InvalidDataException>(() => op.Resume());
            Equal(gold + 9, f.World.Civilizations[1].Gold); Equal(false, e.Values.Actions.Consumed); Equal(0, f.Suppressed);
            e.Strings[4].Value = "fixed"; Throws<InvalidOperationException>(() => op.Resume());
        }
        var valid = new RevelationFixture(); valid.Process.TextParameters.Strings[0] = "Prepared";
        var prepared = new ClassicScenarioText(valid.Process); var automatic = new EventFixture(valid, prepareText: prepared.Prepare);
        automatic.World.Multiplayer.Saved.AutoplayActive = true; var text = automatic.Text("%STRING0"); text.Values.Actions.JustOnce = true;
        using var operation = automatic.Dispatcher.Begin(text, "autoplay"); Equal(null, operation.Resume()); Equal(1, operation.Result);
        Equal("Prepared", automatic.Texts.Single()[0]); Equal(true, text.Values.Actions.Consumed);
    }
    private static void ClassicScenarioTextProcessReplacement()
    {
        var process = new ProcessContext(); process.TextParameters.Strings[0] = "old";
        var prepare = new ClassicScenarioText(process, () => "900"); var source = new OwnedList<string>(["%STRING0/%NUMBER0/%STRING11"]);
        Equal("old/0/900", prepare.Prepare(source)[0]);
        var replacement = new ProcessContext(); replacement.TextParameters.Strings[0] = "loaded"; replacement.TextParameters.Numbers[0] = 12;
        process.SwapContents(replacement); Equal("loaded/12/900", prepare.Prepare(source)[0]);
        process.TextParameters.Clear(); Equal("/0/900", prepare.Prepare(source)[0]);
        Equal("old", replacement.TextParameters.Strings[0]);
    }
    private static void ClassicScenarioTextPrivateParsedEvent()
    {
        using var runtime = new SessionRuntime(new(), new(255));
        var data = new RevelationFixture(runtime.Process, runtime.World); data.Process.TextParameters.Strings[0] = "Private text";
        var prepare = new ClassicScenarioText(data.Process); var f = new EventFixture(data, prepareText: prepare.Prepare);
        var (parsed, _) = ParseEvents("@IF\nScenarioLoaded\n@THEN\nText\n%STRING0\nENDTEXT\nJustOnce\n@ENDIF\n", f.World.Events.Collection);
        Equal(true, parsed.Success); var e = f.World.Events.Collection.At(0);
        ParticipantId target = default;
        runtime.RegisterCommand(new(new(1, "test.expanded-event", "Expanded event", AccessClass.Player, new([]), new([new("result", ValueKind.Integer)])),
            (_, _, _) => new(), _ => new ScenarioEventOperation(f.Dispatcher.Begin(e, "private"), _ => target)));
        runtime.BeginClientSession(new()); var actor = SessionClient(runtime, 2); var recipient = SessionClient(runtime, 1); target = recipient.Identity.Participant;
        var command = actor.Submit(new("test.expanded-event", new([]))).Correlation.Command; runtime.AdvanceOne();
        Equal(null, RequiredResult(actor, command).Interaction); var prompt = RequiredInteraction(recipient, command);
        Equal("Private text", RequiredResult(recipient, command).Interaction!.Spec.Prompt);
        data.Process.TextParameters.Strings[0] = "changed"; Equal("Private text", RequiredResult(recipient, command).Interaction!.Spec.Prompt);
        Equal(false, actor.Reply(prompt, 0).Accepted); Equal(true, recipient.Reply(prompt, 0).Accepted); runtime.AdvanceOne();
        Equal(true, e.Values.Actions.Consumed); Equal(CommandStatus.Completed, RequiredResult(actor, command).Status); f.Consumed();
    }
}
