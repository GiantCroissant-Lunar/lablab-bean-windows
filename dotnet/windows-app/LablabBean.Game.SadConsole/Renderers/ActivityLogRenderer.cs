using SadConsole;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;
using LablabBean.Contracts.Game.UI.Models;
using LablabBean.Contracts.Game.UI.Services;

namespace LablabBean.Game.SadConsole.Renderers;

/// <summary>
/// SadConsole renderer for ECS ActivityLog as a ControlsConsole panel.
/// </summary>
public class ActivityLogRenderer
{
    private readonly ControlsConsole _console;
    private readonly ListBox _listBox;
    private long _lastSequence = -1;
    private int _maxLines = 100;
    private bool _showTimestamps = true;
    private IActivityLog? _service;

    public ControlsConsole Console => _console;

    public ActivityLogRenderer(int width, int height)
    {
        _console = new ControlsConsole(width, height);

        _listBox = new ListBox(width - 2, height - 2)
        {
            Position = new Point(1, 1)
        };

        _console.Controls.Add(_listBox);

        // Draw border
        _console.Surface.DrawBox(new Rectangle(0, 0, width, height),
            ShapeParameters.CreateStyledBox(ICellSurface.ConnectedLineThin,
            new ColoredGlyph(Color.White, Color.Black)));
        var title = " Activity ";
        _console.Surface.Print((width - title.Length) / 2, 0, title, Color.Gray);
    }

    public void SetMaxLines(int max) => _maxLines = Math.Max(10, max);
    public void ShowTimestamps(bool show) => _showTimestamps = show;

    public void Bind(IActivityLog service)
    {
        _service = service;
        _service.Changed += OnServiceChanged;
        RefreshFromService();
    }

    private void OnServiceChanged(long sequence)
    {
        RefreshFromService();
    }

    private void RefreshFromService()
    {
        if (_service == null) return;
        var entries = _service.GetRecentEntries(_maxLines);
        var items = BuildItems(entries);
        _listBox.Items.Clear();
        foreach (var s in items) _listBox.Items.Add(s);
        if (_listBox.Items.Count > 0) _listBox.SelectedIndex = _listBox.Items.Count - 1;
    }

    private List<string> BuildItems(System.Collections.Generic.IReadOnlyList<ActivityEntryDto> entries)
    {
        var count = entries.Count;
        var start = Math.Max(0, count - _maxLines);
        var items = new List<string>(Math.Min(_maxLines, count));
        for (int i = start; i < count; i++)
        {
            var e = entries[i];
            var ts = _showTimestamps ? $"[{e.Timestamp:HH:mm}] " : string.Empty;
            items.Add($"{ts}{e.Icon} {e.Message}");
        }
        return items;
    }
}
