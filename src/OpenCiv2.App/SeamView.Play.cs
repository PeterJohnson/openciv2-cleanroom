using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OpenCiv2.Console;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.IO;
using OpenCiv2.Session;
using OpenCiv2.ViewModels;

namespace OpenCiv2.App;

public sealed partial class SeamView
{
    private const string PlaySave = "file.save-game", PlayReload = "file.reload-game", PlayCity = "city.open";
    private const string SaveKey = "file-alder-valley";
    private readonly LocalGame? game;
    private readonly Dictionary<string, Control> playControls = [];
    private readonly TextBlock turnLabel = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox cityName = new() { Text = "Alder Crossing", MaxLength = 15 };
    private readonly ComboBox terrainJob = new() { MinWidth = 135, MaxWidth = 175 };
    private readonly WrapPanel terrainWork = new();
    private readonly StackPanel playOrders = new() { Spacing = 5 };
    private readonly StackPanel cityBody = new() { Spacing = 9, Margin = new Thickness(20) };
    private readonly Grid cityOverlay = new() { IsVisible = false, Background = Brush.Parse("#b0172b33") };
    private readonly List<string> consoleHistory = [];
    private int historyPosition;
    private bool transferring;
    private CityTarget? openCity;
    private SessionView? cityRendered;

    private IEnumerable<ShellAction> PlayMenuActions()
    {
        if (game is null)
        {
            yield break;
        }

        var play = model.View.Play;
        bool ready = !Busy && play?.PlayerPhase == true;
        yield return new(PlaySave, "File", "Save game", "Primary+S", ready && SeamApp.Storage is not null);
        yield return new(PlayReload, "File", "Reload save", null, ready && SeamApp.Storage is not null);
        yield return new(PlayCity, "Orders", "Open city", null, ready && play?.City is not null);
        yield return new(LocalPlayCommands.Fortify, "Orders", "Fortify unit", null,
            ready && play?.Units.Any(u => u.Target == activeTarget && u.MovesLeft > 0) == true
            && model.View.Units?.Records.Any(u => new UnitTarget(u.Slot, u.Token) == activeTarget
                && u.Orders == UnitOrder.None) == true);
        yield return new(LocalPlayCommands.EndTurn, "Orders", "End turn", null, ready);
        var availableWork = play?.Units.FirstOrDefault(u => u.Target == activeTarget)?.Work;
        foreach (var job in LocalPlayCommands.WorkJobs)
        {
            bool available = availableWork?.Any(option => option.Id == job.Id) == true;
            yield return new(LocalPlayCommands.Work + "." + job.Id, "Orders", job.Name, null,
                ready && available, Hidden: !available);
        }
    }

    private Control BuildPlayOrders()
    {
        playOrders.Children.Add(turnLabel);
        var directions = new WrapPanel { ItemWidth = 43 };
        string[] labels = ["NE", "E", "SE", "S", "SW", "W", "NW", "N"];
        for (int i = 0; i < labels.Length; i++)
        {
            int direction = i;
            directions.Children.Add(PlayButton("move." + i, labels[i], () => Move(direction)));
        }

        playOrders.Children.Add(directions);
        var unitOrders = new WrapPanel();
        unitOrders.Children.Add(PlayButton("fortify", "Fortify", () => UnitCommand(LocalPlayCommands.Fortify)));
        unitOrders.Children.Add(PlayButton("end", "End turn", () => PlayCommand(LocalPlayCommands.EndTurn)));
        playOrders.Children.Add(unitOrders);
        AutomationProperties.SetName(terrainJob, "Terrain work");
        playControls["work.job"] = terrainJob;
        terrainWork.Children.Add(terrainJob);
        terrainWork.Children.Add(PlayButton("work", "Work", () =>
        {
            if (terrainJob.SelectedItem is ComboBoxItem { Tag: int job })
            {
                UnitCommand(LocalPlayCommands.Work, new("job", new CommandValue.Integer(job)));
            }
        }));
        playOrders.Children.Add(terrainWork);
        AutomationProperties.SetName(cityName, "New city name");
        playControls["name"] = cityName;
        playOrders.Children.Add(cityName);
        playOrders.Children.Add(PlayButton("found", "Found city", () =>
            UnitCommand(LocalPlayCommands.Found, new("name", new CommandValue.Text(cityName.Text ?? "")))));
        var files = new WrapPanel();
        files.Children.Add(PlayButton("city", "Open city", OpenOwnedCity));
        files.Children.Add(PlayButton("save", "Save game", () => _ = Transfer(false)));
        files.Children.Add(PlayButton("load", "Reload save", () => _ = Transfer(true)));
        playOrders.Children.Add(files);
        var research = new WrapPanel { Name = "ResearchChoices" };
        playOrders.Children.Add(research);
        return playOrders;
    }

    private Button PlayButton(string id, string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 4, 3) };
        AutomationProperties.SetAutomationId(button, "play." + id);
        button.Click += (_, _) => action();
        playControls[id] = button;
        return button;
    }

    private void BuildCityOverlay()
    {
        cityOverlay.Children.Add(new Border
        {
            Background = Brush.Parse("#f9f6ed"),
            BorderBrush = Brush.Parse("#a58557"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            MaxWidth = 660,
            MaxHeight = 580,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new ScrollViewer { Content = cityBody }
        });
        cityOverlay.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                CloseCity();
                args.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        KeyboardNavigation.SetTabNavigation(cityBody, KeyboardNavigationMode.Cycle);
    }

    private void OpenOwnedCity()
    {
        if (model.View.Play?.City is { } city && !Busy)
        {
            openCity = city.Target;
            Refresh(force: true);
        }
    }

    private void CloseCity()
    {
        openCity = null;
        Refresh(force: true);
        map.Focus();
    }

    private void UnitCommand(string name, NamedValue? argument = null)
    {
        if (activeTarget is not { } target)
        {
            return;
        }

        NamedValue unit = new("unit", new CommandValue.Unit(target));
        PlayCommand(name, argument is null ? [unit] : [unit, argument]);
    }

    private void Move(int direction) => UnitCommand(LocalPlayCommands.Move, new("direction", new CommandValue.Integer(direction)));

    private void PlayCommand(string name, params NamedValue[] args)
    {
        if (Busy || transferring)
        {
            return;
        }

        parseError = "";
        dialog.RememberFocus(map);
        model.Submit(new(name, new(args)));
        Refresh(force: true);
    }

    private async Task Transfer(bool load)
    {
        if (game is null || SeamApp.Storage is not { } storage || Busy || transferring)
        {
            return;
        }

        transferring = true;
        Refresh(force: true);
        try
        {
            if (load)
            {
                byte[]? document = await storage.ReadAsync(SaveKey);
                if (document is null)
                {
                    parseError = "There is no saved game.";
                    return;
                }

                parseError = OpenClassicGame(StoredDocument.Decode(document)).Message;
            }
            else
            {
                var file = new StoredDocument("Alder Valley.SAV", game.SaveClassic());
                await storage.WriteAsync(SaveKey, file.Encode());
                parseError = "Classic save written.";
                model.Refresh();
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException)
        {
            parseError = error.Message;
        }
        finally
        {
            transferring = false;
            Refresh(force: true);
        }
    }

    private (bool Opened, string Message) OpenClassicGame(StoredDocument document)
    {
        switch (game!.LoadClassic(document.Bytes.ToArray()))
        {
            case LocalLoadResult.Loaded:
                model.Bind(game.Player);
                Refresh(force: true);
                parseError = "Classic save loaded.";
                return (true, parseError);
            case LocalLoadResult.Busy:
                return (false, "Finish the pending command before loading.");
            case LocalLoadResult.InvalidFile:
                return (false, "The classic save could not be read.");
            case LocalLoadResult.UnsupportedVersion:
                return (false, "This classic save version is unavailable.");
            default:
                return (false, "This mode opens Alder Valley saves.");
        }
    }

    private void RefreshPlay(SessionView view, SessionView? previous, bool newBinding)
    {
        if (view.Play is not { } play)
        {
            return;
        }

        if (newBinding)
        {
            openCity = null;
            cityRendered = null;
            if (previous is not null)
            {
                input.Clear();
                consoleHistory.Clear();
                historyPosition = 0;
            }
        }

        if (view.Command is { Status: CommandStatus.Completed } command
            && (command.Correlation != previous?.Command?.Correlation || previous.Command.Status != CommandStatus.Completed)
            && command.Committed.Any(effect => effect.Name == "city.founded"))
        {
            openCity = play.City?.Target;
        }

        string research = play.Research.FirstOrDefault(a => a.Id == play.Researching)?.Name ?? "No advance selected";
        turnLabel.Text = $"Turn {play.Turn} · {Math.Abs(play.Year)} {(play.Year < 0 ? "BC" : "AD")}\n"
            + $"{play.Gold} gold · {research} {play.ResearchProgress}/{play.ResearchCost}";
        var unit = play.Units.FirstOrDefault(u => u.Target == activeTarget);
        var own = unit is null ? null : view.Units?.Records.FirstOrDefault(
            u => u.Slot == unit.Target.Slot && u.Token == unit.Target.Token);
        for (int i = 0; i < MapLimits.Directions; i++)
        {
            playControls["move." + i].IsEnabled = unit is { MovesLeft: > 0 } && own?.Orders == UnitOrder.None;
        }

        playControls["found"].IsEnabled = unit?.CanFound == true;
        cityName.IsVisible = playControls["found"].IsVisible = unit?.CanFound == true;
        playControls["fortify"].IsEnabled = unit is { MovesLeft: > 0 } && own?.Orders == UnitOrder.None;
        var jobs = unit?.Work.ToArray() ?? [];
        var oldJobs = terrainJob.Items.Cast<ComboBoxItem>().ToArray();
        if (!oldJobs.Select(item => (int)item.Tag!).SequenceEqual(jobs.Select(job => job.Id)))
        {
            int? selected = (terrainJob.SelectedItem as ComboBoxItem)?.Tag as int?;
            var choices = jobs.Select(job => new ComboBoxItem { Content = job.Name, Tag = job.Id }).ToArray();
            terrainJob.ItemsSource = choices;
            terrainJob.SelectedItem = choices.FirstOrDefault(item => (int)item.Tag! == selected) ?? choices.FirstOrDefault();
        }
        terrainWork.IsVisible = jobs.Length != 0;
        playControls["work"].IsEnabled = jobs.Length != 0;
        playControls["city"].IsEnabled = play.City is not null;
        playControls["save"].IsEnabled = playControls["load"].IsEnabled = SeamApp.Storage is not null;

        var researchPanel = (WrapPanel)playOrders.Children[^1];
        if (previous?.Play?.Research.Equals(play.Research) != true || newBinding)
        {
            foreach (var key in playControls.Keys.Where(k => k.StartsWith("research.", StringComparison.Ordinal)).ToArray())
            {
                playControls.Remove(key);
            }

            researchPanel.Children.Clear();
            foreach (var advance in play.Research)
            {
                researchPanel.Children.Add(PlayButton("research." + advance.Id, advance.Name,
                    () => PlayCommand(LocalPlayCommands.Research, new NamedValue("advance", new CommandValue.Integer(advance.Id)))));
            }
        }

        RefreshCity(view);
    }

    private void RefreshCity(SessionView view)
    {
        bool visible = openCity is { } target && view.Play?.City is { } city && city.Target == target
            && view.Cities?.Records.Any(c => c.Slot == target.Slot && c.Token == target.Token) == true;
        if (!visible)
        {
            openCity = null;
            cityOverlay.IsVisible = false;
            return;
        }

        bool opening = !cityOverlay.IsVisible;
        cityOverlay.IsVisible = true;
        cityOverlay.IsEnabled = !Busy && !transferring;
        if (!opening && ReferenceEquals(view, cityRendered))
        {
            return;
        }

        cityRendered = view;
        var modelCity = view.Play!.City!;
        var record = view.Cities!.Records.Single(c => c.Slot == modelCity.Target.Slot && c.Token == modelCity.Target.Token);
        foreach (var key in playControls.Keys.Where(k => k.StartsWith("city.", StringComparison.Ordinal)).ToArray())
        {
            playControls.Remove(key);
        }

        cityBody.Children.Clear();
        var close = PlayButton("city.close", "Close city", CloseCity);
        cityBody.Children.Add(new Grid
        {
            Children = { new TextBlock { Text = record.Name, FontWeight = FontWeight.Bold, FontSize = 23 },
                new Border { Child = close, HorizontalAlignment = HorizontalAlignment.Right } }
        });
        cityBody.Children.Add(new TextBlock
        {
            Text = $"Population {record.Size} · {modelCity.Specialists} entertainers\n"
                + $"Food {modelCity.Food} / {modelCity.FoodRequired} used · Stock {record.FoodBox}/{modelCity.FoodCapacity}\n"
                + $"Shields {modelCity.Shields} · Support {modelCity.ShieldSupport} · Stock {record.ShieldBox}/{modelCity.ShieldCost}\n"
                + $"Science {modelCity.Science} · Tax {modelCity.Tax}",
            TextWrapping = TextWrapping.Wrap
        });
        var production = new WrapPanel();
        foreach (var option in modelCity.Production)
        {
            var button = PlayButton("city.production." + option.Id, $"Build {option.Name}", () =>
                CityCommand(LocalPlayCommands.Production, modelCity.Target, "type", option.Id));
            button.IsEnabled = option.Id != record.Production;
            production.Children.Add(button);
        }

        cityBody.Children.Add(production);
        cityBody.Children.Add(PlayButton("city.buy", "Buy production…", () =>
            PlayCommand(LocalPlayCommands.Buy, new NamedValue("city", new CommandValue.City(modelCity.Target)))));
        var facilities = new WrapPanel();
        foreach (var improvement in modelCity.Improvements)
        {
            var sell = PlayButton("city.sell." + improvement.Id, $"Sell {improvement.Name}…", () =>
                CityCommand(LocalPlayCommands.Sell, modelCity.Target, "improvement", improvement.Id));
            sell.IsEnabled = !modelCity.ImprovementSold && improvement.Id != 1;
            facilities.Children.Add(sell);
        }
        cityBody.Children.Add(facilities);
        cityBody.Children.Add(new TextBlock { Text = "Worked tiles · Food / Shields / Trade" });
        var workers = new WrapPanel { ItemWidth = 117 };
        foreach (var tile in modelCity.Tiles)
        {
            string label = tile.Index == 20 ? "Reset workers" :
                $"{(tile.Worked ? "✓ " : "")}{tile.Position.X},{tile.Position.Y}  {tile.Food}/{tile.Shields}/{tile.Trade}";
            workers.Children.Add(PlayButton("city.tile." + tile.Index, label, () =>
                CityCommand(LocalPlayCommands.Worker, modelCity.Target, "tile", tile.Index)));
        }

        cityBody.Children.Add(workers);
        close.Focus();
    }

    private void CityCommand(string command, CityTarget city, string key, int value) =>
        PlayCommand(command, new NamedValue("city", new CommandValue.City(city)),
            new NamedValue(key, new CommandValue.Integer(value)));

    private void RememberLine(string line)
    {
        if (consoleHistory.Count == 0 || consoleHistory[^1] != line)
        {
            consoleHistory.Add(line);
            if (consoleHistory.Count > 50)
            {
                consoleHistory.RemoveAt(0);
            }
        }

        historyPosition = consoleHistory.Count;
    }

    private bool ConsoleEditKey(Key key)
    {
        if (key == Key.Tab && !string.IsNullOrEmpty(input.Text))
        {
            var matches = CommandText.Complete(input.Text, model.View.Commands, model.View.Units, model.View.Cities);
            if (matches.Count == 1)
            {
                input.Text = matches[0];
                input.CaretIndex = input.Text.Length;
            }
            else if (matches.Count > 0)
            {
                parseError = string.Join("  ·  ", matches);
                Refresh(force: true);
            }

            return matches.Count > 0;
        }

        if (key is Key.Up or Key.Down && consoleHistory.Count > 0)
        {
            historyPosition = Math.Clamp(historyPosition + (key == Key.Up ? -1 : 1), 0, consoleHistory.Count);
            input.Text = historyPosition < consoleHistory.Count ? consoleHistory[historyPosition] : "";
            input.CaretIndex = input.Text.Length;
            return true;
        }

        return false;
    }

    private bool ConsoleQuery(string line)
    {
        var view = model.View;
        switch (line)
        {
            case "help":
                parseError = "Enter units, cities, map or research to inspect the game. Use save or reload for the classic slot.\n"
                    + "Enter tile x y to inspect one known tile.\n"
                    + "Tab completes a command or owned target.\n"
                    + "Alder Valley supports one city. Combat, AI, huts, terrain work, trade routes and multiplayer are unavailable.\n"
                    + string.Join("\n", view.Commands.Select(CommandText.Help));
                break;
            case "save":
            case "reload":
                _ = Transfer(line == "reload");
                break;
            case "units":
                parseError = string.Join("\n", view.Units?.Records.Select(u =>
                    $"Unit {u.Slot.Value}:{u.Token.Value} · ({u.Position.X},{u.Position.Y}) · {u.MovesUsed} spent") ?? []);
                break;
            case "cities":
                parseError = view.Cities?.Records.Count is > 0 ? string.Join("\n", view.Cities.Records.Select(c =>
                    $"{c.Name} {c.Slot.Value}:{c.Token.Value} · Population {c.Size}")) : "No cities.";
                break;
            case "map":
                parseError = $"{view.Map?.Tiles.Count(t => t.Terrain is not null) ?? 0} explored tiles.";
                break;
            case "research":
                parseError = string.Join("\n", view.Play?.Research.Select(a => $"{a.Id} · {a.Name}") ?? []);
                break;
            case "tile":
            case var _ when line.StartsWith("tile ", StringComparison.Ordinal):
                parseError = KnownTile(line);
                break;
            default:
                return false;
        }

        return true;
    }

    private string KnownTile(string line)
    {
        string[] words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length != 3 || !int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
            || !int.TryParse(words[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
        {
            return "Use tile x y.";
        }

        var point = new MapPoint(x, y);
        if (map.Scene is not { } scene || !scene.Contains(point))
        {
            return "Tile is outside the map.";
        }

        var tile = scene.Tile(point);
        if (tile.Terrain is not { } terrain)
        {
            return "Tile is unexplored.";
        }

        var improvements = tile.Improvements & ~(MapFeatures.Unit | MapFeatures.CitySite);
        return $"({x},{y}) · {(Terrain)terrain}{(tile.River ? " · River" : "")}" +
            (improvements == MapFeatures.None ? "" : $" · {improvements}");
    }

    private JsonObject? PlaySnapshot(SessionView view, Func<Control?, JsonObject> bounds)
    {
        if (view.Play is not { } play)
        {
            return null;
        }

        var city = view.Cities?.Records.FirstOrDefault();
        return new()
        {
            ["command"] = view.Command?.Correlation.Command.Value,
            ["turn"] = play.Turn,
            ["year"] = play.Year,
            ["phase"] = play.PlayerPhase,
            ["gold"] = play.Gold,
            ["research"] = play.Researching,
            ["progress"] = play.ResearchProgress,
            ["known"] = new JsonArray(play.Discovered.Select(a => (JsonNode?)JsonValue.Create(a.Id)).ToArray()),
            ["runtime"] = play.Observation.Runtime.Value,
            ["explored"] = view.Map?.Tiles.Count(t => t.Terrain is not null),
            ["cityOpen"] = cityOverlay.IsVisible,
            ["city"] = city is null ? null : new JsonObject
            {
                ["slot"] = city.Slot.Value,
                ["token"] = city.Token.Value,
                ["name"] = city.Name,
                ["x"] = city.Position.X,
                ["y"] = city.Position.Y,
                ["size"] = city.Size,
                ["production"] = city.Production,
                ["foodBox"] = city.FoodBox,
                ["shieldBox"] = city.ShieldBox,
                ["specialists"] = play.City?.Specialists,
                ["worked"] = new JsonArray(play.City?.Tiles.Where(t => t.Worked)
                    .Select(t => (JsonNode?)JsonValue.Create(t.Index)).ToArray() ?? [])
            },
            ["units"] = new JsonArray(view.Units?.Records.Select(u => (JsonNode)new JsonObject
            {
                ["slot"] = u.Slot.Value,
                ["token"] = u.Token.Value,
                ["type"] = u.Type.Value,
                ["x"] = u.Position.X,
                ["y"] = u.Position.Y,
                ["moves"] = u.MovesUsed,
                ["orders"] = (int)u.Orders
            }).ToArray() ?? []),
            ["effects"] = new JsonArray(view.Command?.Committed.Select(e => (JsonNode?)JsonValue.Create(e.Name)).ToArray() ?? []),
            ["unitRows"] = new JsonArray(view.Units?.Records.Select((_, i) => (JsonNode)bounds(
                units.ContainerFromIndex(i))).ToArray() ?? []),
            ["controls"] = new JsonObject(playControls.Where(pair => pair.Value.IsEffectivelyVisible).Select(pair =>
                new KeyValuePair<string, JsonNode?>(pair.Key, bounds(pair.Value))))
        };
    }
}
