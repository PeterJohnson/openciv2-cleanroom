using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using OpenCiv2.Contracts;

namespace OpenCiv2.App;

/// <summary>Read-only plot and accessible exact values from a prepared session report.</summary>
internal sealed class PowerHistoryView : StackPanel
{
    private static readonly string[] Colors = ["#555555", "#775b34", "#275aa0", "#26734b", "#a13337", "#724c99", "#8a650e", "#15747c"];
    private PowerHistoryReport? shown;
    public PowerHistoryView() { Spacing = 8; IsVisible = false; }
    public void Show(PowerHistoryReport? report)
    {
        if (ReferenceEquals(report, shown))
        { return; }
        shown = report;
        Children.Clear();
        IsVisible = report is not null;
        if (report is null)
        { return; }
        Children.Add(new Plot(report) { Height = 220, MinWidth = 250 });
        foreach (var series in report.Series)
        {
            Children.Add(new TextBlock { Text = $"● {series.Name} ({series.Civilization})", Foreground = Brush.Parse(Colors[series.Civilization]) });
        }
        var lines = new List<string> { "Turn · Date · " + string.Join(" · ", report.Series.Select(s => s.Name)) };
        for (int i = 0; i < report.Dates.Count; ++i)
        {
            var date = report.Dates[i];
            lines.Add($"{date.Turn} · {Date(report, date.Year)} · " + string.Join(" · ", report.Series.Select(s => s.Values[i])));
        }
        Children.Add(new Expander { Header = "History values", Content = new TextBlock { Text = string.Join("\n", lines), TextWrapping = TextWrapping.Wrap } });
        AutomationProperties.SetName(this, "Power history chart with exact values");
    }
    private static string Date(PowerHistoryReport report, int value) => report.Monthly
        ? $"{Math.Abs(value % 12) + 1}/{Math.Abs(value / 12)}" : $"{Math.Abs(value)} {(value < 0 ? "BC" : "AD")}";
    private sealed class Plot(PowerHistoryReport report) : Control
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            Rect plot = new(32, 8, Math.Max(1, Bounds.Width - 42), Math.Max(1, Bounds.Height - 40));
            context.DrawRectangle(Brush.Parse("#fffdf5"), new Pen(Brush.Parse("#b9ad92")), plot);
            void Label(string value, double x, double y) => context.DrawText(new FormattedText(value,
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 11, Brushes.Black), new(x, y));
            Label(report.Maximum.ToString(CultureInfo.InvariantCulture), 0, plot.Top);
            Label("0", 10, plot.Bottom - 12);
            if (report.Dates.Count == 0)
            { return; }
            Label(Date(report, report.Dates[0].Year), plot.Left, plot.Bottom + 5);
            if (report.Dates.Count > 1)
            { Label(Date(report, report.Dates[^1].Year), plot.Right - 70, plot.Bottom + 5); }
            foreach (var series in report.Series)
            {
                var pen = new Pen(Brush.Parse(Colors[series.Civilization]), 2);
                Point? previous = null;
                for (int i = 0; i < series.Values.Count; ++i)
                {
                    Point point = new(plot.Left + plot.Width * i / Math.Max(1, report.Dates.Count - 1), plot.Bottom - plot.Height * series.Values[i] / report.Maximum);
                    if (previous is { } old)
                    { context.DrawLine(pen, old, point); }
                    else
                    { context.DrawEllipse(pen.Brush, null, point, 2, 2); }
                    previous = point;
                }
            }
        }
    }
}
