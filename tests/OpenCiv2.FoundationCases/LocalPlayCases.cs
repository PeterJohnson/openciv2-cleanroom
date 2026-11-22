using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Console;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;
using OpenCiv2.Formats;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    /// <summary>Plays the direct-client reference used by the isolated native input scenario.</summary>
    public static (byte[] Saved, byte[] Continued) LocalPlayReference()
    {
        using var game = new LocalGame();
        byte[] saved = PlayAlderValley(game, false);
        ContinueAlderValley(game, saved, false);
        return (saved, game.SaveClassic());
    }

    private static void LocalPlayRoundTrip()
    {
        using var direct = new LocalGame();
        using var console = new LocalGame();
        var directSave = PlayAlderValley(direct, false);
        var consoleSave = PlayAlderValley(console, true);
        BytesEqual(directSave, consoleSave);

        ContinueAlderValley(direct, directSave, false);
        ContinueAlderValley(console, consoleSave, true);
        BytesEqual(direct.SaveClassic(), console.SaveClassic());
    }

    private static void LocalPlaySpecialistAdmission()
    {
        using var game = new LocalGame();
        var player = game.Player;
        var pioneer = player.OwnUnits().Records.Single(u => u.Type.Value == 0);
        RunLocal(game, false, "found test city", LocalPlayCommands.Found,
            new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))),
            new("name", new CommandValue.Text("Alder Crossing")));

        var stored = ClassicFiles.DecodeStoredSave(game.SaveClassic());
        var worked = default(CityWorkSet).With(LocalCityEconomy.Center);
        var cityView = player.ObserveLocalPlay()!.City!;
        foreach (var tile in cityView.Tiles.Where(tile => tile.Index != LocalCityEconomy.Center).Take(4))
        {
            worked = worked.With(tile.Index);
        }

        var specialists = new SpecialistArray<SpecialistKind>();
        specialists[0] = SpecialistKind.Entertainer;
        var city = stored.Cities.Single() with
        {
            Size = 5,
            WorkedTiles = StateConversion.EncodeCityWork(worked, specialists: 1),
            SpecialistKinds = StateConversion.EncodeSpecialists(specialists)
        };
        var civilizations = stored.Civilizations.ToArray();
        civilizations[1] = civilizations[1] with { TotalCitySize = 5 };
        stored = stored with { Civilizations = new(civilizations) };

        var before = LocalPlayerSnapshot.Capture(player);
        foreach (var (index, kind) in new[]
        {
            (0, SpecialistKind.Scientist),
            (0, SpecialistKind.TaxCollector),
            (CityLimits.Specialists - 1, SpecialistKind.Scientist)
        })
        {
            var unsupportedKinds = specialists;
            unsupportedKinds[index] = kind;
            var unsupported = city with { SpecialistKinds = StateConversion.EncodeSpecialists(unsupportedKinds) };
            Equal(LocalLoadResult.UnsupportedGame,
                game.LoadClassic(ClassicFiles.EncodeStoredSave(stored with { Cities = new([unsupported]) })));
            Equal(player.Identity, game.Player.Identity);
            Equal(before, LocalPlayerSnapshot.Capture(player));
        }

        Equal(LocalLoadResult.Loaded,
            game.LoadClassic(ClassicFiles.EncodeStoredSave(stored with { Cities = new([city]) })));
        Equal((sbyte)5, game.Player.OwnCities().Records.Single().Size);
        Equal(1, game.Player.ObserveLocalPlay()!.City!.Specialists);
        Equal(city.SpecialistKinds, ClassicFiles.DecodeStoredSave(game.SaveClassic()).Cities.Single().SpecialistKinds);
    }

    private static void LocalPlaySupportRetirement()
    {
        using var game = new LocalGame();
        var pioneer = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
        RunLocal(game, false, "found support city", LocalPlayCommands.Found,
            new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))), new("name", new CommandValue.Text("Alder Crossing")));
        var stored = ClassicFiles.DecodeStoredSave(game.SaveClassic());
        var prototype = stored.Units.Single(unit => unit.Token.Occupied);
        var units = Enumerable.Range(0, 12).Select(i => prototype with
        {
            Token = new((uint)(100 + i)),
            Home = new(0),
            Type = new(4),
            Previous = (short)(i - 1),
            Next = (short)(i == 11 ? -1 : i + 1)
        }).ToArray();
        stored = stored with { Units = new(units), Globals = stored.Globals with { UnitEnd = 12, ActiveUnit = 0 } };
        Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(stored)));
        Equal(12, game.Player.OwnUnits().Records.Count);
        var result = EndLocalTurn(game, false, 2);
        var retired = result.Committed.Where(e => e.Name == "unit.unsupported")
            .Select(e => ((CommandValue.Unit)CommandDescriptor.Value(e.Values, "unit")).Value.Slot.Value).ToArray();
        Equal(true, retired.Length > 0);
        Sequence(Enumerable.Range(0, retired.Length).Select(i => (ushort)i), retired); // Equal-distance candidates retire in slot order.
        Equal(12 - retired.Length, game.Player.OwnUnits().Records.Count);
        var remaining = game.Player.OwnUnits().Records.Select(unit => (unit.Slot, unit.Position, unit.Type, unit.Home)).ToArray();
        var before = game.Player.ObserveLocalPlay()!;
        var save = game.SaveClassic(); Equal(LocalLoadResult.Loaded, game.LoadClassic(save));
        Sequence(remaining, game.Player.OwnUnits().Records.Select(unit => (unit.Slot, unit.Position, unit.Type, unit.Home)));
        var after = game.Player.ObserveLocalPlay()!;
        Equal(before.City!.ShieldSupport, after.City!.ShieldSupport); Equal(before.Gold, after.Gold);
        Equal(before.ResearchProgress, after.ResearchProgress); Equal(before.City.Food, after.City.Food);
        EndLocalTurn(game, false, 3);
    }

    private static void LocalPlayExhaustedStep()
    {
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame();
            var player = game.Player;
            var units = player.ObserveLocalPlay()!.Units;
            var pioneer = units.Single(u => u.CanFound).Target;
            var rider = units.Single(u => !u.CanFound).Target;
            NamedValue unit = new("unit", new CommandValue.Unit(rider));
            string target = $"{rider.Slot.Value}:{rider.Token.Value}";
            RunLocal(game, console, $"unit.select unit={target}", LocalPlayCommands.Select, unit);
            RunLocal(game, console, $"unit.move unit={target} direction=1", LocalPlayCommands.Move,
                unit, new("direction", new CommandValue.Integer(1)));
            ExhaustOutrider(game, console, pioneer, rider);
        }
    }

    private static void LocalPlayFoodTurn()
    {
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame();
            var pioneer = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
            RunLocal(game, false, "found food city", LocalPlayCommands.Found,
                new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))), new("name", new CommandValue.Text("Alder Crossing")));
            var original = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            foreach (var (size, stock, expectedSize, effect) in new[]
            {
                (1, 12, 2, "city.population"), (2, -1, 1, "city.famine"), (8, 54, 8, "city.growth_halted")
            })
            {
                var civilizations = original.Civilizations.ToArray();
                civilizations[1] = civilizations[1] with { TotalCitySize = (short)size };
                var inputCity = original.Cities.Single() with { Size = (sbyte)size, FoodBox = (short)stock };
                if (stock < 0)
                {
                    // Keep a real deficit through the load-time C03 recalculation: only the center
                    // works, and both citizens are entertainers. Sufficient food would repair stock to0.
                    var specialists = new SpecialistArray<SpecialistKind>();
                    specialists[0] = specialists[1] = SpecialistKind.Entertainer;
                    inputCity = inputCity with
                    {
                        WorkedTiles = StateConversion.EncodeCityWork(default(CityWorkSet).With(20), specialists: (byte)size),
                        SpecialistKinds = StateConversion.EncodeSpecialists(specialists)
                    };
                }
                var stored = original with
                {
                    Civilizations = new(civilizations),
                    Cities = new([inputCity])
                };
                Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(stored)));
                var result = EndLocalTurn(game, console, 2);
                if (!result.Committed.Any(e => e.Name == effect))
                {
                    throw new InvalidOperationException($"Missing {effect} for size {size}, stock {stock}; effects: {string.Join(',', result.Committed.Select(e => e.Name))}");
                }
                Equal((sbyte)expectedSize, game.Player.OwnCities().Records.Single().Size);
                var city = ClassicFiles.DecodeStoredSave(game.SaveClassic()).Cities.Single();
                // Every branch resets stock before the real C03 collection; no granary in this authored profile.
                Equal((short)(city.TotalFood - expectedSize * 2), city.FoodBox);
                var saved = game.SaveClassic(); Equal(LocalLoadResult.Loaded, game.LoadClassic(saved));
                Equal(city.FoodBox, ClassicFiles.DecodeStoredSave(game.SaveClassic()).Cities.Single().FoodBox);
                Equal((sbyte)expectedSize, game.Player.OwnCities().Records.Single().Size);
                EndLocalTurn(game, console, 3);
            }
            var removal = original with { Cities = new([original.Cities.Single() with { FoodBox = -1 }]) };
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(removal)));
            // Every admitted center feeds size1 and there are no food routes or homed settlers.
            // C02 repairs this imported negative stock before the turn, without a false famine.
            Equal((short)0, ClassicFiles.DecodeStoredSave(game.SaveClassic()).Cities.Single().FoodBox);
            var repaired = EndLocalTurn(game, console, 2);
            Equal(false, repaired.Committed.Any(e => e.Name == "city.famine"));
            Equal((sbyte)1, game.Player.OwnCities().Records.Single().Size);
        }
    }

    private static void LocalPlayMoodTurn()
    {
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame();
            var pioneer = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
            RunLocal(game, console, $"city.found unit={pioneer.Slot.Value}:{pioneer.Token.Value} name=\"Alder Crossing\"", LocalPlayCommands.Found,
                new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))), new("name", new CommandValue.Text("Alder Crossing")));
            var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            // Imported celebration ends on an unqualified city; stale duration flags without Disorder survive.
            var city = saved.Cities.Single() with { Flags = saved.Cities.Single().Flags | 0x2U | 0x2000U | 0x100000U };
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(saved with { Cities = new([city]) })));
            int before = game.Player.ObserveLocalPlay()!.Gold;
            var result = EndLocalTurn(game, console, 2);
            Equal(true, result.Committed.Any(e => e.Name == "city.mood"));
            var after = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var flags = StateConversion.DecodeCityStatus(after.Cities.Single().Flags);
            Equal(false, flags.Celebrating || flags.Disorder);
            Equal(true, flags.SecondDisorderTurn && flags.LongDisorder);
            Equal(before + after.Cities.Single().Tax, game.Player.ObserveLocalPlay()!.Gold);
            var bytes = game.SaveClassic(); Equal(LocalLoadResult.Loaded, game.LoadClassic(bytes));
            Equal(after.Cities.Single().Flags, ClassicFiles.DecodeStoredSave(game.SaveClassic()).Cities.Single().Flags);
            var continuation = EndLocalTurn(game, console, 3);
            Equal(false, continuation.Committed.Any(e => e.Name == "city.mood"));
        }
    }

    private static void LocalPlayMaintenanceAndReputation()
    {
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame();
            var pioneer = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
            RunLocal(game, console, $"city.found unit={pioneer.Slot.Value}:{pioneer.Token.Value} name=\"Alder Crossing\"", LocalPlayCommands.Found,
                new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))), new("name", new CommandValue.Text("Alder Crossing")));
            var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var civilizations = saved.Civilizations.ToArray();
            for (int c = 0; c < civilizations.Length; ++c)
            {
                var credit = civilizations[c].ReputationCredit.ToArray(); credit[1] = (sbyte)(c % 2 == 0 ? -5 : 7); credit[2] = 7;
                civilizations[c] = civilizations[c] with { Reputation = 2, ReputationCredit = new(credit) };
            }
            civilizations[1] = civilizations[1] with { Gold = int.MaxValue };
            var input = saved with { Globals = saved.Globals with { Turn = 23 }, Civilizations = new(civilizations) };
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(input)));
            var before = game.Player.ObserveLocalPlay()!.City!;
            var result = EndLocalTurn(game, console, 24);
            var after = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            Equal(true, after.Cities.Single().Tax > 0); // Tax wraps the admitted treasury negative before C08.
            Equal(true, result.Committed.Any(e => e.Name == "city.improvement_lost"));
            Equal(0, after.Civilizations[1].Gold); Equal(default(ImprovementSet), StateConversion.ToLive(after.Cities.Single()).Improvements);
            Equal(saved.Civilizations[1].CapitalX, after.Civilizations[1].CapitalX);
            Equal(false, StateConversion.DecodeCityStatus(after.Cities.Single().Flags).ImprovementSold);
            Equal((short)(saved.Cities.Single().ShieldBox + Math.Max(0, before.Shields - before.ShieldSupport)), after.Cities.Single().ShieldBox);
            for (int c = 0; c < civilizations.Length; ++c)
            {
                Equal((byte)1, after.Civilizations[c].Reputation); // Includes inactive seats.
                Equal((sbyte)(c % 2 == 0 ? 0 : 1), after.Civilizations[c].ReputationCredit[1]);
                Equal((sbyte)7, after.Civilizations[c].ReputationCredit[2]);
            }
            var bytes = game.SaveClassic(); Equal(LocalLoadResult.Loaded, game.LoadClassic(bytes));
            Equal(false, EndLocalTurn(game, console, 25).Committed.Any(e => e.Name == "city.improvement_lost"));
            // Forced Palace loss extends the admitted profile only to no facilities.
            var city = StateConversion.ToLive(after.Cities.Single()); city.Improvements = city.Improvements.With(BuildingIds.Library);
            var unsupported = after with { Cities = new([StateConversion.ToStored(city)]) };
            Equal(LocalLoadResult.UnsupportedGame, game.LoadClassic(ClassicFiles.EncodeStoredSave(unsupported)));
        }
    }

    private static void LocalPlayResearchGrant()
    {
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame();
            var pioneer = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
            RunLocal(game, console, $"city.found unit={pioneer.Slot.Value}:{pioneer.Token.Value} name=\"Alder Crossing\"", LocalPlayCommands.Found,
                new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))), new("name", new CommandValue.Text("Alder Crossing")));
            var saved = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            var civilizations = saved.Civilizations.ToArray();
            civilizations[1] = civilizations[1] with { Researching = 0, ResearchProgress = 32000, NumAdvances = 255, ResearchCostAdvances = 7 };
            var first = saved.Globals.FirstDiscoverer.ToArray(); first[0] = 4;
            var known = saved.Globals.AdvanceKnownBy.ToArray(); known[0] = 0;
            var input = saved with { Civilizations = new(civilizations), Globals = saved.Globals with { FirstDiscoverer = new(first), AdvanceKnownBy = new(known) } };
            Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(input)));
            var result = EndLocalTurn(game, console, 2); Equal(true, result.Committed.Any(e => e.Name == "research.completed"));
            var after = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            Equal((byte)0, after.Civilizations[1].NumAdvances); Equal((byte)7, after.Civilizations[1].ResearchCostAdvances);
            Equal((byte)5, after.Globals.FirstDiscoverer[0]); Equal((byte)2, after.Globals.AdvanceKnownBy[0]);
            Equal((byte)0, after.Civilizations[1].AdvanceSource[0]); Equal((short)0, after.Civilizations[1].ResearchProgress);
            Equal(true, StateConversion.ToLive(after.Civilizations[1]).Advances.Contains(0));
            var bytes = game.SaveClassic(); Equal(LocalLoadResult.Loaded, game.LoadClassic(bytes));
            Equal((byte)5, ClassicFiles.DecodeStoredSave(game.SaveClassic()).Globals.FirstDiscoverer[0]);
            EndLocalTurn(game, console, 3);
        }
    }

    private static void LocalPlayProductionStocks()
    {
        foreach (bool console in new[] { false, true })
        {
            using var game = new LocalGame();
            var pioneer = game.Player.OwnUnits().Records.Single(u => u.Type.Value == 0);
            RunLocal(game, false, "found production city", LocalPlayCommands.Found,
                new("unit", new CommandValue.Unit(new(pioneer.Slot, pioneer.Token))), new("name", new CommandValue.Text("Alder Crossing")));
            var original = ClassicFiles.DecodeStoredSave(game.SaveClassic());
            foreach (var (stock, capacity) in new[] { (32767, false), (100, false), (100, true) })
            {
                var civilizations = original.Civilizations.ToArray();
                if (capacity) civilizations[1] = civilizations[1] with { MilitaryCount = 1949 };
                var input = original with
                {
                    Cities = new([original.Cities.Single() with { ShieldBox = (short)stock }]),
                    Civilizations = new(civilizations)
                };
                if (capacity)
                {
                    // U01 reuses holes before applying extension limits. Remove the founder's
                    // free slot so the retained high military count exercises actual refusal.
                    input = input with
                    {
                        Units = new([original.Units.Single(u => u.Token.Occupied) with { Previous = -1, Next = -1 }]),
                        Globals = input.Globals with { UnitEnd = 1, ActiveUnit = 0 }
                    };
                }
                Equal(LocalLoadResult.Loaded, game.LoadClassic(ClassicFiles.EncodeStoredSave(input)));
                var city = game.Player.OwnCities().Records.Single();
                foreach (int type in new[] { 4, 2, 4, (int)original.Cities.Single().Production })
                {
                    RunLocal(game, console, $"city.production city={city.Slot.Value}:{city.Token.Value} type={type}", LocalPlayCommands.Production,
                        new("city", new CommandValue.City(new(city.Slot, city.Token))), new("type", new CommandValue.Integer(type)));
                    var changed = ClassicFiles.DecodeStoredSave(game.SaveClassic());
                    Equal((short)stock, changed.Cities.Single().ShieldBox);
                    Equal((sbyte)type, changed.Cities.Single().Production);
                    Equal((byte)(type == 2 ? 1 : 0), changed.Civilizations[1].UnitsInProduction[2]);
                    Equal((byte)(type == 4 ? 1 : 0), changed.Civilizations[1].UnitsInProduction[4]);
                }
                var before = game.Player.ObserveLocalPlay()!;
                int net = Math.Max(0, before.City!.Shields - before.City.ShieldSupport);
                var result = EndLocalTurn(game, console, 2);
                var after = ClassicFiles.DecodeStoredSave(game.SaveClassic());
                bool built = stock == 100 && !capacity;
                Equal(built, result.Committed.Any(e => e.Name == "unit.produced"));
                Equal(capacity, result.Committed.Any(e => e.Name == "city.production_spent"));
                Equal(before.Units.Count + (built ? 1 : 0), game.Player.OwnUnits().Records.Count);
                int expected = stock == 32767 ? unchecked((short)(stock + net)) : capacity ? stock + net - before.City.ShieldCost : 0;
                Equal((short)expected, after.Cities.Single().ShieldBox);
                var saved = game.SaveClassic(); Equal(LocalLoadResult.Loaded, game.LoadClassic(saved));
                Equal((short)expected, ClassicFiles.DecodeStoredSave(game.SaveClassic()).Cities.Single().ShieldBox);
                EndLocalTurn(game, console, 3);
            }
        }
    }

    private static void ExhaustOutrider(LocalGame game, bool console, UnitTarget pioneer, UnitTarget rider)
    {
        var player = game.Player;
        NamedValue unit = new("unit", new CommandValue.Unit(rider));
        NamedValue south = new("direction", new CommandValue.Integer(3));
        string stepSouth = $"unit.move unit={rider.Slot.Value}:{rider.Token.Value} direction=3";
        RunLocal(game, console, stepSouth, LocalPlayCommands.Move, unit, south);
        Equal<UnitTarget?>(rider, player.ObserveMap()!.Annotations!.ActiveUnit);
        Equal(2, player.ObserveLocalPlay()!.Units.Single(u => u.Target == rider).MovesLeft);

        if (console)
        {
            var baseline = LocalPlayerSnapshot.Capture(player);
            for (int i = 0; i < 16; i++)
            {
                var observed = LocalPlayerSnapshot.Capture(player);
                Equal(baseline, observed);
                Equal(null, observed.Map.Tiles[0].Terrain);
                Equal(MapFeatures.None, observed.Map.Tiles[0].Improvements);
                Equal(2, CommandText.Complete("unit.move unit=", player.Commands(), observed.Units, observed.Cities).Count);
            }
        }

        var id = SubmitLocal(game, console, stepSouth, LocalPlayCommands.Move, unit, south);
        Equal(true, game.AdvanceOne());
        var result = player.Result(id)!;
        Equal(CommandStatus.Failed, result.Status);
        Equal("movement_spent", result.Code);
        var effect = result.Committed.Single();
        Equal("unit.movement_spent", effect.Name);
        Equal(unit, effect.Values.Single());
        Equal(new MapPoint(12, 10), player.OwnUnits().Records.Single(u => u.Slot == rider.Slot).Position);
        var play = player.ObserveLocalPlay()!;
        Equal(0, play.Units.Single(u => u.Target == rider).MovesLeft);
        Equal(3, play.Units.Single(u => u.Target == pioneer).MovesLeft);
        Equal<UnitTarget?>(pioneer, player.ObserveMap()!.Annotations!.ActiveUnit);
    }

    private static byte[] PlayAlderValley(LocalGame game, bool console)
    {
        var player = game.Player;
        var second = game.Bind(ControllerKind.Scripted);
        var settler = player.OwnUnits().Records.Single(u => u.Type.Value == 0);
        var rider = player.OwnUnits().Records.Single(u => u.Type.Value == 4);
        NamedValue settlerValue = new("unit", new CommandValue.Unit(new(settler.Slot, settler.Token)));
        NamedValue riderValue = new("unit", new CommandValue.Unit(new(rider.Slot, rider.Token)));
        string st = $"{settler.Slot.Value}:{settler.Token.Value}";
        string rt = $"{rider.Slot.Value}:{rider.Token.Value}";
        var initial = player.ObserveMap()!;
        var queued = player.Submit(new(LocalPlayCommands.Move, new([
            riderValue, new("direction", new CommandValue.Integer(1))])));
        Equal(LocalLoadResult.Busy, game.LoadClassic(ReadOnlyMemory<byte>.Empty));
        Equal(true, player.Cancel(queued.Correlation.Command).Accepted);
        Equal(true, game.AdvanceOne());
        Equal(CommandStatus.Cancelled, player.Result(queued.Correlation.Command)!.Status);
        Equal(rider, player.OwnUnits().Records.Single(u => u.Slot == rider.Slot));
        Equal(true, initial.Tiles.Any(t => t.Terrain is null));
        RunLocal(game, console, $"unit.select unit={rt}", LocalPlayCommands.Select, riderValue);
        RunLocal(game, console, $"unit.move unit={rt} direction=1", LocalPlayCommands.Move, riderValue,
            new("direction", new CommandValue.Integer(1)));
        var moved = player.OwnUnits().Records.Single(u => u.Slot == rider.Slot);
        Equal(new MapPoint(12, 8), moved.Position);
        Equal((byte)1, moved.MovesUsed);
        Equal(true, player.ObserveMap()!.Tiles.Count(t => t.Terrain is not null) >
            initial.Tiles.Count(t => t.Terrain is not null));
        Equal(null, player.ObserveMap()!.Tiles[0].Terrain);
        ExhaustOutrider(game, console, new(settler.Slot, settler.Token), new(rider.Slot, rider.Token));

        RunLocal(game, console, $"city.found unit={st} name=\"Alder Crossing\"", LocalPlayCommands.Found, settlerValue,
            new("name", new CommandValue.Text("Alder Crossing")));
        Equal("unit_not_available", player.Submit(new(LocalPlayCommands.Select, new([settlerValue]))).Code);
        Equal(0, CommandText.Complete($"unit.select unit={st}", player.Commands(), player.OwnUnits(), player.OwnCities()).Count);
        var city = player.OwnCities().Records.Single();
        var target = new CityTarget(city.Slot, city.Token);
        var cityValue = new NamedValue("city", new CommandValue.City(target));
        string ct = $"{city.Slot.Value}:{city.Token.Value}";
        Equal(new MapPoint(8, 8), city.Position);
        Equal((sbyte)1, city.Size);
        int worked = player.ObserveLocalPlay()!.City!.Tiles.Single(t => t.Worked && t.Index != 20).Index;
        RunLocal(game, console, $"city.worker city={ct} tile={worked}", LocalPlayCommands.Worker, cityValue,
            new("tile", new CommandValue.Integer(worked)));
        Equal(1, player.ObserveLocalPlay()!.City!.Specialists);
        RunLocal(game, console, $"city.worker city={ct} tile=1", LocalPlayCommands.Worker, cityValue,
            new("tile", new CommandValue.Integer(1)));
        Equal(0, player.ObserveLocalPlay()!.City!.Specialists);
        Equal(true, player.ObserveLocalPlay()!.City!.Tiles.Single(t => t.Index == 1).Worked);
        RunLocal(game, console, $"city.production city={ct} type=4", LocalPlayCommands.Production, cityValue,
            new("type", new CommandValue.Integer(4)));

        var pending = SubmitLocal(game, console, "turn.end", LocalPlayCommands.EndTurn);
        var confirm = AwaitLocal(game, pending);
        Equal(LocalLoadResult.Busy, game.LoadClassic(ReadOnlyMemory<byte>.Empty));
        Equal("decision_or_command_pending", second.Submit(new(LocalPlayCommands.Move,
            new([riderValue, new("direction", new CommandValue.Integer(1))]))).Code);
        Decide(player, console, confirm, null);
        game.AdvanceOne();
        Equal(CommandStatus.Cancelled, player.Result(pending)!.Status);
        Equal(1, player.ObserveLocalPlay()!.Turn);

        bool produced = false, discovered = false;
        for (int turn = 2; turn <= 7; turn++)
        {
            var update = EndLocalTurn(game, console, turn);
            produced |= update.Committed.Any(e => e.Name == "unit.produced");
            discovered |= update.Committed.Any(e => e.Name == "research.completed");
        }
        Equal(true, produced);
        Equal(true, discovered);
        Equal(true, player.OwnUnits().Records.Count >= 2);
        Equal((sbyte)4, player.OwnCities().Records.Single().Production);
        Equal(true, player.ObserveLocalPlay()!.Discovered.Any(a => a.Id == 0));

        RunLocal(game, console, $"unit.fortify unit={rt}", LocalPlayCommands.Fortify, riderValue);
        Equal(UnitOrder.Fortifying, player.OwnUnits().Records.Single(u => u.Slot == rider.Slot).Orders);
        var save = game.SaveClassic();
        var snapshot = LocalPlayerSnapshot.Capture(player);
        Equal(snapshot, LocalPlayerSnapshot.Capture(player));
        BytesEqual(save, game.SaveClassic());
        var stored = ClassicFiles.DecodeStoredSave(save);
        Equal((short)7, stored.Globals.Turn);
        Equal(1, stored.Cities.Count);
        return save;
    }

    private static void ContinueAlderValley(LocalGame game, byte[] save, bool console)
    {
        var retired = game.Player;
        var before = retired.OwnUnits();
        var beforeCity = retired.OwnCities().Records.Single();
        var beforePlay = retired.ObserveLocalPlay()!;
        Equal(LocalLoadResult.InvalidFile, game.LoadClassic(save.AsMemory(0, 20)));
        var stored = ClassicFiles.DecodeStoredSave(save);
        var harder = stored with { Globals = stored.Globals with { Difficulty = 1 } };
        Equal(LocalLoadResult.UnsupportedGame, game.LoadClassic(ClassicFiles.EncodeStoredSave(harder)));
        Equal(retired.Identity, game.Player.Identity);
        Equal(LocalLoadResult.Loaded, game.LoadClassic(save));
        var player = game.Player;
        Equal(true, before.Observation.Runtime != player.Identity.Runtime);
        try
        {
            retired.ObserveMap();
            throw new InvalidOperationException("The outgoing observer remained attached.");
        }
        catch (AccessException error)
        {
            Equal(AccessFailure.SessionEnded, error.Failure);
        }

        Equal(beforePlay.Turn, player.ObserveLocalPlay()!.Turn);
        Equal(beforePlay.ResearchProgress, player.ObserveLocalPlay()!.ResearchProgress);
        Equal(beforePlay.Gold, player.ObserveLocalPlay()!.Gold);
        var city = player.OwnCities().Records.Single();
        Equal(beforeCity with { Token = city.Token }, city);
        Equal(true, beforeCity.Token != city.Token);
        var units = player.OwnUnits().Records;
        Equal(before.Records.Count, units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            Equal(before.Records[i] with { Token = units[i].Token }, units[i]);
            Equal(true, before.Records[i].Token != units[i].Token);
        }

        var oldCity = new CityTarget(beforeCity.Slot, beforeCity.Token);
        Equal("city_not_available", player.Submit(new(LocalPlayCommands.Worker, new([
            new("city", new CommandValue.City(oldCity)), new("tile", new CommandValue.Integer(20))]))).Code);
        Equal(true, CommandText.Complete("city.worker city=", player.Commands(), player.OwnUnits(), player.OwnCities())
            .Single().EndsWith($"{city.Slot.Value}:{city.Token.Value}", StringComparison.Ordinal));
        EndLocalTurn(game, console, 8);
        Equal(UnitOrder.Fortified, player.OwnUnits().Records.Single(u => u.Slot.Value == 1).Orders);
    }

    private static CommandUpdate EndLocalTurn(LocalGame game, bool console, int turn)
    {
        var player = game.Player;
        var id = SubmitLocal(game, console, "turn.end", LocalPlayCommands.EndTurn);
        var confirmation = AwaitLocal(game, id);
        Decide(player, console, confirmation, 1);
        Equal(false, player.Reply(confirmation.Interaction!.Id, 1).Accepted);
        Equal(false, player.Cancel(id).Accepted);
        Equal(true, game.AdvanceOne());
        var update = player.Result(id)!;
        if (update.Interaction is { } research)
        {
            Equal(true, update.Committed.Any(e => e.Name == "turn.advanced"));
            Equal(turn, player.ObserveLocalPlay()!.Turn);
            Equal(false, player.ObserveLocalPlay()!.PlayerPhase);
            Equal(false, player.Cancel(id).Accepted);
            Equal(false, player.Reply(research.Id, -1).Accepted);
            Equal(LocalLoadResult.Busy, game.LoadClassic(ReadOnlyMemory<byte>.Empty));
            Decide(player, console, update, research.Spec.Choices[0]);
            Equal(true, game.AdvanceOne());
            update = player.Result(id)!;
        }

        Equal(CommandStatus.Completed, update.Status);
        Equal(turn, player.ObserveLocalPlay()!.Turn);
        Equal(true, player.ObserveLocalPlay()!.PlayerPhase);
        var delivered = player.Updates().Where(u => u.Correlation.Command == id)
            .SelectMany(u => u.Committed).ToArray();
        Equal(true, delivered.SequenceEqual(update.Committed));
        Equal(0, player.Updates().Count);
        return update;
    }

    private static CommandUpdate AwaitLocal(LocalGame game, CommandId id)
    {
        Equal(true, game.AdvanceOne());
        var update = game.Player.Result(id)!;
        Equal(CommandStatus.AwaitingInteraction, update.Status);
        return update;
    }

    private static void Decide(IPlayerClient player, bool console, CommandUpdate update, int? choice)
    {
        if (console)
        {
            choice = CommandText.Decision(choice is int value ? $"reply {value}" : "cancel");
        }

        var receipt = choice is int selected ? player.Reply(update.Interaction!.Id, selected)
            : player.Cancel(update.Correlation.Command);
        Equal(true, receipt.Accepted);
    }

    private static void RunLocal(LocalGame game, bool console, string line, string name, params NamedValue[] args)
    {
        var id = SubmitLocal(game, console, line, name, args);
        Equal(true, game.AdvanceOne());
        var result = game.Player.Result(id)!;
        if (result.Status != CommandStatus.Completed)
        {
            throw new InvalidOperationException($"{line}: {result.Status} {result.Code}");
        }
    }

    private static CommandId SubmitLocal(LocalGame game, bool console, string line, string name, params NamedValue[] args)
    {
        var request = console ? CommandText.Parse(line, game.Player.Commands()) : new(name, new(args));
        var result = game.Player.Submit(request);
        Equal(CommandStatus.Pending, result.Status);
        return result.Correlation.Command;
    }

    private static void LocalStepZones()
    {
        var world = new GameState(new());
        var rules = new RuleTables { Cosmic = new() { RoadMovementMultiplier = 3 } };
        rules.Units[0] = new() { Movement = 6, Attack = 1 };
        rules.Terrain[(int)Terrain.Grassland] = new() { MovementCost = 1 };
        world.Globals.Rules.FlatWorld = true;
        world.Map.Allocate(new(20, 16));
        for (int i = 0; i < world.Map.Geometry.Area; i++)
        {
            world.Map.Tiles[i] = new() { Terrain = Terrain.Grassland };
        }
        world.Entities.Units.SetEnd(2);
        var stacks = new StackOperations(world, new(500, 64));
        world.Entities.Units.Storage[0] = new() { Token = new(1), Owner = new(1), Position = new(-1, -1) };
        world.Entities.Units.Storage[1] = new() { Token = new(2), Owner = new(2), Position = new(-1, -1) };
        stacks.AttachAtFront(0, new(8, 8), false);
        stacks.AttachAtFront(1, new(9, 7), false);
        var random = new GameplayRandom(1, "local-step");
        var result = LandMovement.Step(world, rules, new(0), 1, random);
        Equal("zone_of_control", result.Code);
        Equal(true, result.Changed);
        Equal(new MapPoint(8, 8), world.Entities.Units.Storage[0].Position);
        Equal((byte)0, world.Entities.Units.Storage[0].MovesUsed);
        Equal(true, world.Entities.Units.Storage[1].Visibility.Contains(1));
        Equal(true, world.Map.TileAt(new(9, 7)).RevealedTo.Contains(1));
        world.Civilizations[1].Treaties[2].Alliance = true;
        Equal("moved", LandMovement.Step(world, rules, new(0), 1, random).Code);
        Equal((byte)3, world.Entities.Units.Storage[0].MovesUsed);
    }
}
