using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;

namespace OpenCiv2.Formats.Classic;

public sealed record EventParseResult(bool Success, string Code = "");

/// <summary>S02 §§3–6 classic EVENTS grammar over the F03 reader. Appends in place;
/// malformed helper input retains earlier fields and events. Presentation of diagnostics
/// and the caller's directory/error notice belong to the load adapter. DEBUG is cosmetic.
/// Bare THEN is rejected before action parsing; original uninitialized reads are not admitted.</summary>
public sealed class ClassicEventParser(TextReader reader, Func<int, string> civilizationName,
    Func<int, string> unitName, Func<string, string, bool> equal)
{
    public ClassicEventParser(TextReader reader, GameState world, RuleTables rules, Func<string, string, bool> equal)
        : this(reader, i => world.Names[i].Tribe, i => RuleText.Display(rules.Units[i].Identity.Name), equal) { }

    private enum Flag
    {
        KilledUnit, Attacker, Defender, City, TalkerClass, ListenerClass, Technology,
        Terrain, Unit, Owner, Home, Veteran, Destination, MoveCount, MoneyReceiver,
        Amount, AggressionWho, AggressionWhom, Destroy, GiftReceiver, GiftTechnology
    }
    private sealed class ParseFailure(string code) : Exception(code);
    private readonly HashSet<Flag> seen = [];
    private Event? current;
    private string Line => ClassicText.DecodeWindows1252(reader.Line);
    private Event Entry => current ?? throw new ParseFailure("events-missing-if");

    public EventParseResult Read(EventCollection events, string filename = "EVENTS", string section = "BEGINEVENTS")
    {
        ArgumentNullException.ThrowIfNull(events);
        current = null;
        seen.Clear();
        int phase = 0;
        try
        {
            if (!reader.Open(filename, ClassicText.EncodeWindows1252(section)))
            { return new(false, "events-section-unavailable"); }
            while (reader.NextLine())
            {
                if (Line.StartsWith('@'))
                {
                    string directive = Line[1..];
                    if (equal(directive, "IF"))
                    { current = events.Append(); seen.Clear(); phase = 1; }
                    else if (equal(directive, "THEN"))
                    { _ = Entry; phase = 2; }
                    else if (equal(directive, "ENDIF"))
                    { phase = 0; }
                    else if (equal(directive, "ENDEVENTS"))
                    { return new(true); }
                    continue;
                }
                if (phase == 1)
                { Trigger(); }
                else if (phase == 2)
                { Action(); }
            }
            return new(true);
        }
        catch (ParseFailure error) { return new(false, error.Message); }
    }

    /// <summary>Catalog adapter for ClassicLoadDialogServices.LoadEvents. The caller owns
    /// reader lifetime and error presentation. Ordinary saved event state bypasses this hook.</summary>
    public OwnedList<EventInput> ReadCatalog(string filename = "EVENTS", string section = "BEGINEVENTS")
    {
        var events = new EventCollection();
        var result = Read(events, filename, section);
        if (!result.Success)
        { throw new InvalidDataException(result.Code); }
        return new(Enumerable.Range(0, events.Count).Select(i =>
        {
            var entry = events.At(i);
            return new EventInput(entry.Values, new(entry.Strings.ToArray()
                .Select(value => value.Present ? value.Value : null)));
        }));
    }

    private bool Is(string value) => equal(Line, value);
    private bool Field(string key, out string value)
    {
        string line = Line;
        bool matches = line.Length >= key.Length && equal(line[..key.Length], key);
        value = matches ? line[key.Length..] : "";
        return matches;
    }
    private static bool Digit(string value) => value.Length != 0 && value[0] is >= '0' and <= '9';
    private static bool Signed(string value) => Digit(value) || value.Length != 0 && value[0] is '+' or '-';
    private static int Integer(string value) => TextReader.ParseInteger(ClassicText.EncodeWindows1252(value));
    private void Next(bool parameter = true)
    {
        if (!reader.NextLine())
        { throw new ParseFailure("events-incomplete-helper"); }
        if (parameter && Line.StartsWith('@'))
        { throw new ParseFailure("events-directive-in-helper"); }
    }
    private bool Have(params Flag[] flags) => flags.All(seen.Contains);
    private void Parameters(Func<bool> complete, Action read)
    {
        while (!complete())
        { Next(); read(); }
    }
    private int Civilization(string name)
    {
        if (equal(name, "Anybody"))
        { return -2; }
        if (equal(name, "TriggerAttacker"))
        { return -3; }
        if (equal(name, "TriggerDefender") || equal(name, "TriggerReceiver"))
        { return -4; }
        for (int i = 0; i < EntityLimits.Civilizations; ++i)
        { if (equal(name, civilizationName(i))) { return i; } }
        return -1;
    }
    private int Unit(string name)
    {
        if (equal(name, "AnyUnit"))
        { return -2; }
        for (int i = 0; i < RuleLimits.UnitTypes; ++i)
        { if (equal(name, unitName(i))) { return i; } }
        return -1;
    }
    private void Named(string key, Flag flag, int? text, Action<int> store,
        bool unit = false, bool definite = false, bool twice = false)
    {
        if (!Field(key, out string value))
        { return; }
        int selector = unit ? Unit(value) : Civilization(value);
        if (selector == -1 || definite && selector == -2)
        { return; }
        if (text is { } field)
        { Entry.SetString(field, new(value)); }
        store(twice ? Civilization(value) : selector);
        seen.Add(flag);
    }
    private void TextField(string key, Flag flag, int field)
    {
        if (!Field(key, out string value))
        { return; }
        Entry.SetString(field, new(value));
        seen.Add(flag);
    }
    private void Number(string key, Flag flag, Action<int> store, bool signed = false)
    {
        if (!Field(key, out string value) || !(signed ? Signed(value) : Digit(value)))
        { return; }
        store(Integer(value));
        seen.Add(flag);
    }
    private void Control(string key, Flag flag, Action<int> store)
    {
        if (!Field(key, out string value))
        { return; }
        int control = equal(value, "Human") ? 1 : equal(value, "Computer") ? 2 : equal(value, "HumanOrComputer") ? 4 : 0;
        if (control == 0)
        { return; }
        store(control);
        seen.Add(flag);
    }
    private void Attacker(string key = "Attacker=", bool twice = false) =>
        Named(key, Flag.Attacker, 2, value => Entry.Values.WhoCiv = value, twice: twice);
    private void Defender(string key = "Defender=", bool twice = false) =>
        Named(key, Flag.Defender, 3, value => Entry.Values.WhomCiv = value, twice: twice);

    private void Trigger()
    {
        // These comparisons intentionally re-read the reader after each preceding helper.
        if (Is("UnitKilled"))
        {
            Parameters(() => Have(Flag.KilledUnit, Flag.Attacker, Flag.Defender), () =>
            {
                Named("Unit=", Flag.KilledUnit, 0, v => Entry.Values.UnitType = v, unit: true, definite: true);
                Attacker();
                Defender();
            });
            Entry.Values.Trigger = EventTrigger.UnitKilled;
        }
        if (Is("Negotiation"))
        {
            Parameters(() => Have(Flag.Attacker, Flag.Defender, Flag.TalkerClass, Flag.ListenerClass), () =>
            {
                Attacker("Talker=");
                Defender("Listener=");
                Control("TalkerType=", Flag.TalkerClass, v => Entry.Values.TalkerType = v);
                Control("ListenerType=", Flag.ListenerClass, v => Entry.Values.ListenerType = v);
            });
            Entry.Values.Trigger = EventTrigger.Negotiation;
            Entry.Values.DenyNegotiation = true;
        }
        if (Is("NoSchism"))
        {
            Parameters(() => Have(Flag.Defender), () => Defender());
            Entry.Values.Trigger = EventTrigger.NoSchism;
        }
        else if (Is("ReceivedTechnology"))
        {
            Parameters(() => Have(Flag.Defender, Flag.Technology), () =>
            {
                Defender("Receiver=", twice: true);
                Number("Technology=", Flag.Technology, v => Entry.Values.Technology = v);
            });
            Entry.Values.Trigger = EventTrigger.ReceivedTechnology;
        }
        else if (Is("CityTaken"))
        {
            Parameters(() => Have(Flag.City, Flag.Attacker, Flag.Defender), () =>
            { TextField("City=", Flag.City, 1); Attacker(twice: true); Defender(); });
            Entry.Values.Trigger = EventTrigger.CityTaken;
        }
        else if (Is("Turn"))
        { NumericTrigger(EventTrigger.Turn, "Turn="); }
        else if (Is("TurnInterval"))
        { NumericTrigger(EventTrigger.TurnInterval, "Interval="); }
        else if (Is("RandomTurn"))
        { NumericTrigger(EventTrigger.RandomTurn, "Denominator="); }
        else if (Is("ScenarioLoaded"))
        { Entry.Values.Trigger = EventTrigger.ScenarioLoaded; }
    }

    private void NumericTrigger(EventTrigger kind, string key)
    {
        do
        {
            Next();
            if (Field(key, out string value))
            {
                if (kind == EventTrigger.Turn && equal(value, "Every"))
                { Entry.Values.Turn = -1; Entry.Values.Trigger = kind; }
                else if (kind == EventTrigger.RandomTurn ? Digit(value) : Signed(value))
                {
                    int number = Integer(value);
                    if (kind == EventTrigger.RandomTurn)
                    {
                        Entry.Values.Denominator = number;
                        if (number is < 1 or > 1000)
                        { throw new ParseFailure("events-invalid-denominator"); }
                    }
                    else
                    { Entry.Values.Turn = number; }
                    Entry.Values.Trigger = kind;
                }
            }
            else if (kind == EventTrigger.Turn)
            { continue; }
            if (Entry.Values.Trigger == kind)
            { return; }
        } while (true);
    }

    private void Action()
    {
        if (Is("Text"))
        { Text(); }
        else if (Is("ChangeTerrain"))
        { Terrain(); }
        else if (Is("CreateUnit"))
        { Create(); }
        else if (Is("ChangeMoney"))
        {
            Parameters(() => Have(Flag.MoneyReceiver, Flag.Amount), () =>
            {
                Named("Receiver=", Flag.MoneyReceiver, 33, v => Entry.Values.MoneyReceiverCiv = v);
                Number("Amount=", Flag.Amount, v => Entry.Values.MoneyAmount = v, signed: true);
            });
            Entry.Values.Actions.ChangeMoney = true;
        }
        else if (Is("JustOnce"))
        { Entry.Values.Actions.JustOnce = true; }
        else if (Is("DontPlayWonders"))
        { Entry.Values.Actions.SuppressWonderMovies = true; }
        else if (Is("MakeAggression"))
        {
            Parameters(() => Have(Flag.AggressionWho, Flag.AggressionWhom), () =>
            {
                Named("Who=", Flag.AggressionWho, 27, v => Entry.Values.AggressionWhoCiv = v);
                Named("Whom=", Flag.AggressionWhom, 26, v => Entry.Values.AggressionWhomCiv = v);
            });
            Entry.Values.Actions.MakeAggression = true;
        }
        else if (Is("DestroyACivilization"))
        {
            Parameters(() => Have(Flag.Destroy), () => Named("Whom=", Flag.Destroy, null, v => Entry.Values.DestroyCiv = v));
            Entry.Values.Actions.DestroyCivilization = true;
        }
        else if (Is("GiveTechnology"))
        {
            Parameters(() => Have(Flag.GiftReceiver, Flag.GiftTechnology), () =>
            {
                Named("Receiver=", Flag.GiftReceiver, null, v => Entry.Values.GiveTechCiv = v);
                Number("Technology=", Flag.GiftTechnology, v => Entry.Values.GiveTech = v);
            });
            Entry.Values.Actions.GiveTechnology = true;
        }
        else if (Is("MoveUnit"))
        { Move(); }
        else if (Is("PlayCDTrack"))
        { Next(false); Entry.Values.CdTrack = reader.NextInteger(); if (Entry.Values.CdTrack > 0) { Entry.Values.Actions.PlayCdTrack = true; } }
        else if (Is("PlayWaveFile"))
        { Next(false); Entry.SetString(34, new(Line)); Entry.Values.Actions.PlayWaveFile = true; }
    }

    private void Text()
    {
        int count = 0;
        Next(false);
        while (!Is("ENDTEXT"))
        {
            if (count < 20)
            { Entry.SetString(Event.TextBegin + count++, new(Line)); }
            Next();
        }
        Entry.Values.Actions.Text = true;
    }
    // F06 event coordinates are i32. Unit storage narrowing belongs to action execution.
    private MapPoint Point() => new(reader.NextInteger(), reader.NextInteger());
    private void Rectangle(Action<int, MapPoint> store, ref int count)
    {
        if (!Is("MAPRECT"))
        { return; }
        Next(false);
        for (int i = 0; i < 4; ++i)
        { store(i, Point()); count = unchecked(count + 1); }
    }
    private void Terrain()
    {
        int count = 0;
        Parameters(() => Have(Flag.Terrain) && count == 4, () =>
        {
            Number("TerrainType=", Flag.Terrain, v => Entry.Values.TerrainType = v);
            Rectangle((i, point) => Entry.Values.TerrainRectangle[i] = point, ref count);
        });
        Entry.Values.Actions.ChangeTerrain = true;
    }
    private void Create()
    {
        int count = 0;
        Parameters(() => Have(Flag.Unit, Flag.Owner, Flag.Home, Flag.Veteran) && count != 0, () =>
        {
            Named("Unit=", Flag.Unit, 29, v => Entry.Values.CreateUnitType = v, unit: true);
            Named("Owner=", Flag.Owner, 28, v => Entry.Values.CreateOwnerCiv = v);
            TextField("HomeCity=", Flag.Home, 30);
            if (Field("Veteran=", out string value))
            {
                if (equal(value, "Yes") || equal(value, "True"))
                { Entry.Values.CreateVeteran = true; seen.Add(Flag.Veteran); }
                else if (equal(value, "No") || equal(value, "False"))
                { Entry.Values.CreateVeteran = false; seen.Add(Flag.Veteran); }
            }
            if (!Is("LOCATIONS"))
            { return; }
            for (int i = 0; i < 10; ++i)
            {
                Next(false);
                if (Signed(Line))
                { Entry.Values.CreateLocations[i] = Point(); count = unchecked(count + 1); }
                else if (Is("ENDLOCATIONS"))
                { break; }
            }
        });
        Entry.Values.CreateLocationCount = count;
        Entry.Values.Actions.CreateUnit = true;
    }
    private void Move()
    {
        int count = 0;
        Parameters(() => Have(Flag.Unit, Flag.Owner, Flag.Destination, Flag.MoveCount) && count == 4, () =>
        {
            Named("Unit=", Flag.Unit, 25, v => Entry.Values.MoveUnitType = v, unit: true);
            Named("Owner=", Flag.Owner, 24, v => Entry.Values.MoveOwnerCiv = v);
            if (Field("NumberToMove=", out string value))
            {
                if (equal(value, "All"))
                { Entry.Values.MoveCount = -2; seen.Add(Flag.MoveCount); }
                else if (Digit(value))
                { Entry.Values.MoveCount = Integer(value); seen.Add(Flag.MoveCount); }
            }
            if (Is("MOVETO"))
            {
                Next(false);
                if (Signed(Line))
                { Entry.Values.MoveTo = Point(); seen.Add(Flag.Destination); }
            }
            else
            { Rectangle((i, point) => Entry.Values.MoveRectangle[i] = point, ref count); }
        });
        Entry.Values.Actions.MoveUnit = true;
    }
}
