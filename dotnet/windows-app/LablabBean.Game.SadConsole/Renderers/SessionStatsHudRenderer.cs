using LablabBean.Reporting.Analytics;
using SadConsole;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace LablabBean.Game.SadConsole.Renderers;

/// <summary>
/// Renders real-time session statistics HUD
/// Displays kills, deaths, items collected, levels completed, etc.
/// </summary>
public class SessionStatsHudRenderer
{
    private readonly ControlsConsole _console;
    private readonly Label _titleLabel;
    private readonly Label _combatStatsLabel;
    private readonly Label _progressStatsLabel;
    private readonly Label _kdRatioLabel;
    private readonly SessionMetricsCollector _metricsCollector;

    public ControlsConsole Console => _console;

    public SessionStatsHudRenderer(int width, int height, SessionMetricsCollector metricsCollector)
    {
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _console = new ControlsConsole(width, height);

        // Title
        _titleLabel = new Label(width - 2)
        {
            Position = new Point(1, 1),
            DisplayText = "=== SESSION STATS ===",
            TextColor = Color.Yellow
        };

        // Combat stats
        _combatStatsLabel = new Label(width - 2)
        {
            Position = new Point(1, 3),
            DisplayText = "Combat:\n  Kills: 0\n  Deaths: 0"
        };

        // Progress stats
        _progressStatsLabel = new Label(width - 2)
        {
            Position = new Point(1, 7),
            DisplayText = "Progress:\n  Items: 0\n  Levels: 0\n  Depth: 0\n  Dungeons: 0"
        };

        // K/D Ratio
        _kdRatioLabel = new Label(width - 2)
        {
            Position = new Point(1, 13),
            DisplayText = "K/D Ratio: 0.0",
            TextColor = Color.Cyan
        };

        _console.Controls.Add(_titleLabel);
        _console.Controls.Add(_combatStatsLabel);
        _console.Controls.Add(_progressStatsLabel);
        _console.Controls.Add(_kdRatioLabel);

        // Draw border
        _console.Surface.DrawBox(new Rectangle(0, 0, width, height),
            ShapeParameters.CreateStyledBox(ICellSurface.ConnectedLineThin,
            new ColoredGlyph(Color.Cyan, Color.Black)));

        // Initial update
        Update();
    }

    /// <summary>
    /// Updates the session stats display
    /// </summary>
    public void Update()
    {
        // Combat stats
        var kills = _metricsCollector.TotalKills;
        var deaths = _metricsCollector.TotalDeaths;

        _combatStatsLabel.DisplayText = $"Combat:\n" +
                                        $"  Kills: {kills}\n" +
                                        $"  Deaths: {deaths}";

        // Set color based on kill count
        _combatStatsLabel.TextColor = kills > 0 ? Color.LightGreen : Color.White;

        // Progress stats
        var items = _metricsCollector.ItemsCollected;
        var levels = _metricsCollector.LevelsCompleted;
        var depth = _metricsCollector.MaxDepth;
        var dungeons = _metricsCollector.DungeonsCompleted;

        _progressStatsLabel.DisplayText = $"Progress:\n" +
                                          $"  Items: {items}\n" +
                                          $"  Levels: {levels}\n" +
                                          $"  Depth: {depth}\n" +
                                          $"  Dungeons: {dungeons}";

        // K/D Ratio
        var kdRatio = deaths > 0 ? (double)kills / deaths : kills;
        var kdColor = kdRatio >= 2.0 ? Color.LightGreen :
                      kdRatio >= 1.0 ? Color.Yellow :
                      Color.Red;

        _kdRatioLabel.DisplayText = $"K/D Ratio: {kdRatio:F2}";
        _kdRatioLabel.TextColor = kdColor;
    }
}
