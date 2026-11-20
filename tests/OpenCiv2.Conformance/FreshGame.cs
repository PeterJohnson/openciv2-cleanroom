using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.Conformance;

public static partial class Adapter
{
    private static byte[] InputBytes(Input input, int count) { byte[] bytes = Unhex(input.Word()); Require(bytes.Length == count, "Invalid fresh-game byte array"); return bytes; }
    private static string InputText(Input input, int limit)
    {
        string word = input.Word();
        byte[] bytes = word == "-" ? [] : Unhex(word);
        Require(bytes.Length <= limit, "Fresh-game text exceeds harness bound");
        return new UTF8Encoding(false, true).GetString(bytes);
    }
    private static JsonNode FreshGame(Input input)
    {
        var process = new ProcessContext(); using var runtime = new SessionRuntime(process, new(1)); var world = runtime.World;
        process.Tokens.NextUnit = input.Read<uint>(); process.Tokens.NextCity = input.Read<uint>(); world.Entities.Units.SetEnd(input.Count(2048)); world.Entities.Cities.SetEnd(input.Count(256));
        byte[] damage = InputBytes(input, 2048); for (int i = 0; i < damage.Length; ++i) world.Entities.Units.Storage[i].Damage = damage[i];
        foreach (ref var city in world.Entities.Cities.Storage) city.ShieldBox = input.Read<short>(); byte[] future = InputBytes(input, 8); for (int i = 0; i < 8; ++i) world.Civilizations[i].FutureTechs = future[i];
        world.Globals.PeaceTurns = input.Read<short>(); byte[] goals = InputBytes(input, 512); int cursor = 0;
        foreach (ref var civilization in world.Civilizations.AsSpan())
        {
            foreach (ref var goal in (Span<CivilizationGoal>)civilization.Goals) goal = goal with { Role = unchecked((sbyte)goals[cursor++]) };
            foreach (ref var goal in (Span<CivilizationGoal>)civilization.StandingGoals) goal = goal with { Role = unchecked((sbyte)goals[cursor++]) };
        }
        byte[] names = InputBytes(input, 21); for (int i = 0; i < 21; ++i) process.Rules.Leaders[i].CityNameIndex = names[i]; world.Multiplayer.Saved.ResumeEvent = input.Read<short>(); world.Multiplayer.Saved.Passwords = new(Enumerable.Range(0, 8).Select(_ => InputText(input, 1024)));
        if (input.Boolean()) world.Map.Allocate(new(8, 6)); process.Fallback.Value.Region = input.Read<byte>(); process.TextParameters.Line = InputText(input, 65536);
        uint importedOptions = input.Read<uint>(); process.OrdinaryPreferences.GameOptions = StateConversion.DecodeGameOptions(importedOptions); process.MultiplayerPreferences.Timer = input.Read<short>(); process.Rules.DoubleMovementApplied = input.Boolean(); IPlayerClient? old = null;
        if (input.Boolean()) { runtime.BeginClientSession(new(11, 22)); old = runtime.Bind(runtime.AddParticipant(new(1)), ControllerKind.Scripted); }
        input.End();
        runtime.PrepareNewGame(); world = runtime.World; bool valid = false; if (old is not null) { try { _ = old.Identity; valid = true; } catch (AccessException) { } }
        var roles = new List<byte>(); foreach (ref var civilization in world.Civilizations.AsSpan()) { foreach (var goal in (Span<CivilizationGoal>)civilization.Goals) roles.Add(unchecked((byte)goal.Role)); foreach (var goal in (Span<CivilizationGoal>)civilization.StandingGoals) roles.Add(unchecked((byte)goal.Role)); }
        var state = O(("F02.nextUnitToken", S("u32", process.Tokens.NextUnit)), ("F02.nextCityToken", S("u32", process.Tokens.NextCity)), ("F02.unitEnd", S("u32", (uint)world.Entities.Units.End)), ("F02.cityEnd", S("u32", (uint)world.Entities.Cities.End)),
            ("F02.unitDamage", Bytes(world.Entities.Units.Storage.ToArray().Select(u => u.Damage).ToArray())), ("F02.cityShields", A(world.Entities.Cities.Storage.ToArray(), c => S("i16", c.ShieldBox))), ("F02.futureTechnologies", Bytes(world.Civilizations.Select(c => c.FutureTechs).ToArray())),
            ("F02.peaceTurns", S("i16", world.Globals.PeaceTurns)), ("F02.goalRoles", Bytes(roles.ToArray())), ("F02.leaderCityNameCursors", Bytes(process.Rules.Leaders.Select(l => l.CityNameIndex).ToArray())), ("F02.resumeEvent", S("i16", world.Multiplayer.Saved.ResumeEvent)),
            ("F02.passwords", A(world.Multiplayer.Saved.Passwords, T)), ("F02.mapAllocated", B(world.Map.Allocated)), ("F02.fallbackRegion", S("u8", process.Fallback.Value.Region)), ("F02.textLine", T(process.TextParameters.Line)),
            ("F02.preferenceGameOptions", S("u32", (StateConversion.EncodeGameOptions(process.OrdinaryPreferences.GameOptions) | (importedOptions & ~StateConversion.GameOptionsMask)))), ("F02.preferenceTimer", S("i16", process.MultiplayerPreferences.Timer)), ("F02.doubleMovementApplied", B(process.Rules.DoubleMovementApplied)), ("F02.previousClientValid", B(valid)));
        return Returned("F02.prepareNewGame", new JsonObject(), Null(), state);
    }
}
