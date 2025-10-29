using LablabBean.Game.Core.Services;
using LablabBean.Game.Core.Worlds;
using LablabBean.Game.Core.Systems;
using LablabBean.Game.Core.Components;
using LablabBean.Game.SadConsole.Renderers;
using LablabBean.Game.SadConsole.UI;
using LablabBean.Reporting.Analytics;
using LablabBean.Game.SadConsole.Services;
using Microsoft.Extensions.Logging;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using Arch.Core;

namespace LablabBean.Game.SadConsole.Screens;

/// <summary>
/// Main game screen for SadConsole
/// Combines world rendering, HUD, and session stats
/// </summary>
public class GameScreen : ScreenObject
{
    private readonly ILogger<GameScreen> _logger;
    private readonly GameStateManager _gameStateManager;
    private readonly SessionMetricsCollector _metricsCollector;
    private readonly ReportExportService _reportExportService;
    private readonly WorldRenderer _worldRenderer;
    private readonly HudRenderer _hudRenderer;
    private readonly SessionStatsHudRenderer _sessionStatsHudRenderer;
    private readonly AdvancedAnalyticsHudRenderer? _advancedAnalyticsHudRenderer;
    private readonly AchievementNotificationOverlay? _achievementNotifications;
    private readonly AchievementProgressHud? _achievementProgressHud;
    private readonly LeaderboardRenderer? _leaderboardRenderer;
    private readonly NotificationOverlay _notificationOverlay;
    private bool _isInitialized;
    private bool _showSessionStats = true;
    private bool _showAdvancedAnalytics = false;
    private bool _showAchievements = false;
    private bool _showLeaderboard = false;

    public ScreenSurface WorldSurface => _worldRenderer.Surface;

    public GameScreen(
        ILogger<GameScreen> logger,
        GameStateManager gameStateManager,
        SessionMetricsCollector metricsCollector,
        ReportExportService reportExportService,
        AdvancedAnalyticsCollector? advancedAnalytics,
        AchievementSystem? achievementSystem,
        LeaderboardSystem? leaderboardSystem,
        int width,
        int height)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gameStateManager = gameStateManager ?? throw new ArgumentNullException(nameof(gameStateManager));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));

        // Create renderers
        int playerHudWidth = 30;
        int sessionStatsWidth = 25;
        int worldWidth = width - playerHudWidth - sessionStatsWidth;

        _worldRenderer = new WorldRenderer(worldWidth, height);
        _hudRenderer = new HudRenderer(playerHudWidth, height);
        _sessionStatsHudRenderer = new SessionStatsHudRenderer(sessionStatsWidth, 17, metricsCollector);
        _notificationOverlay = new NotificationOverlay(width, height);

        // Create advanced analytics renderer if collector available
        if (advancedAnalytics != null)
        {
            _advancedAnalyticsHudRenderer = new AdvancedAnalyticsHudRenderer(sessionStatsWidth, 30, advancedAnalytics);
            _advancedAnalyticsHudRenderer.Console.Position = new Point(worldWidth + playerHudWidth, 18);
            _advancedAnalyticsHudRenderer.Console.IsVisible = false;
        }

        // Create achievement UI if system available
        if (achievementSystem != null)
        {
            _achievementNotifications = new AchievementNotificationOverlay(width, height);
            _achievementProgressHud = new AchievementProgressHud(sessionStatsWidth, height, achievementSystem);
            _achievementProgressHud.Console.Position = new Point(worldWidth + playerHudWidth, 0);
            _achievementProgressHud.Console.IsVisible = false;

            // Subscribe to achievement unlocks
            achievementSystem.OnAchievementUnlocked += achievement =>
            {
                _achievementNotifications?.ShowAchievement(achievement);
                _hudRenderer.AddMessage($"🏆 {achievement.Name} unlocked! (+{achievement.Points}pts)");
            };
        }

        // Create leaderboard UI if system available
        if (leaderboardSystem != null)
        {
            _leaderboardRenderer = new LeaderboardRenderer(leaderboardSystem, width, height);
        }

        // Position world renderer
        _worldRenderer.Surface.Position = new Point(0, 0);

        // Position player HUD on the right
        _hudRenderer.Console.Position = new Point(worldWidth, 0);

        // Position session stats HUD on the far right
        _sessionStatsHudRenderer.Console.Position = new Point(worldWidth + playerHudWidth, 0);

        // Add to children
        Children.Add(_worldRenderer.Surface);
        Children.Add(_hudRenderer.Console);
        Children.Add(_sessionStatsHudRenderer.Console);
        if (_advancedAnalyticsHudRenderer != null)
        {
            Children.Add(_advancedAnalyticsHudRenderer.Console);
        }
        if (_achievementProgressHud != null)
        {
            Children.Add(_achievementProgressHud.Console);
        }
        if (_achievementNotifications != null)
        {
            Children.Add(_achievementNotifications.Console);
        }
        Children.Add(_notificationOverlay.Console);

        UseMouse = true;
        UseKeyboard = true;
    }

    /// <summary>
    /// Initializes a new game
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
            return;

        _logger.LogInformation("Initializing game screen");

        // Initialize the game
        _gameStateManager.InitializeNewGame(80, 40);

        _hudRenderer.AddMessage("Welcome to the Dungeon!");
        _hudRenderer.AddMessage("Use arrow keys or WASD to move.");
        _hudRenderer.AddMessage("Press 'E' to switch to edit mode.");
        _hudRenderer.AddMessage("Press 'R' to export report.");
        _hudRenderer.AddMessage("Press 'T' to toggle stats.");
        if (_advancedAnalyticsHudRenderer != null)
        {
            _hudRenderer.AddMessage("Press 'A' to toggle analytics.");
        }
        if (_achievementProgressHud != null)
        {
            _hudRenderer.AddMessage("Press 'C' to view achievements.");
        }
        if (_leaderboardRenderer != null)
        {
            _hudRenderer.AddMessage("Press 'L' to view leaderboards.");
        }
        _hudRenderer.AddMessage("Press 'ESC' to quit.");

        _isInitialized = true;

        // Initial render
        Render();
    }

    /// <summary>
    /// Renders the game
    /// </summary>
    private void Render()
    {
        if (!_isInitialized || _gameStateManager.CurrentMap == null)
            return;

        // Don't render world when leaderboard is shown
        if (_showLeaderboard)
            return;

        // Render world
        _worldRenderer.Render(_gameStateManager.WorldManager.CurrentWorld, _gameStateManager.CurrentMap);

        // Render HUD
        _hudRenderer.Update(_gameStateManager.WorldManager.CurrentWorld);
    }

    /// <summary>
    /// Updates the game state
    /// </summary>
    public override void Update(TimeSpan delta)
    {
        base.Update(delta);

        if (_isInitialized)
        {
            // Update leaderboard rendering if visible
            if (_showLeaderboard && _leaderboardRenderer != null)
            {
                _leaderboardRenderer.Render(_worldRenderer.Surface);
            }

            _gameStateManager.Update();
            _sessionStatsHudRenderer.Update();

            if (_advancedAnalyticsHudRenderer != null && _showAdvancedAnalytics)
            {
                _advancedAnalyticsHudRenderer.Render(_metricsCollector.TotalKills, _metricsCollector.TotalDeaths);
            }

            if (_achievementNotifications != null)
            {
                _achievementNotifications.Update(delta.TotalSeconds);
            }

            if (_achievementProgressHud != null && _showAchievements)
            {
                var combatStats = _metricsCollector.AdvancedAnalytics.GetCombatStatistics(_metricsCollector.TotalKills, _metricsCollector.TotalDeaths);
                var timeAnalytics = _metricsCollector.AdvancedAnalytics.GetTimeAnalytics();

                var metrics = new Dictionary<string, double>
(StringComparer.Ordinal)
                {
                    ["TotalKills"] = _metricsCollector.TotalKills,
                    ["TotalDeaths"] = _metricsCollector.TotalDeaths,
                    ["KDRatio"] = _metricsCollector.KDRatio,
                    ["ItemsCollected"] = _metricsCollector.ItemsCollected,
                    ["LevelsCompleted"] = _metricsCollector.LevelsCompleted,
                    ["MaxDepth"] = _metricsCollector.MaxDepth,
                    ["DungeonsCompleted"] = _metricsCollector.DungeonsCompleted,
                    ["DamageDealt"] = combatStats.DamageDealt,
                    ["DamageTaken"] = combatStats.DamageTaken,
                    ["HealingReceived"] = combatStats.HealingReceived,
                    ["CriticalHits"] = combatStats.CriticalHits,
                    ["PerfectDodges"] = combatStats.PerfectDodges,
                    ["TotalPlaytimeMinutes"] = timeAnalytics.TotalPlaytime.TotalMinutes,
                    ["AvgTimePerLevelSeconds"] = timeAnalytics.AverageTimePerLevel.TotalSeconds
                };

                _achievementProgressHud.Render(metrics);
            }

            // Check for new achievement unlocks periodically
            if (_metricsCollector.AchievementSystem != null && DateTime.UtcNow.Second % 2 == 0)
            {
                _metricsCollector.CheckAchievements();
            }

            _notificationOverlay.Update();
        }
    }

    /// <summary>
    /// Handles keyboard input
    /// </summary>
    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        if (!_isInitialized)
            return false;

        bool actionTaken = false;

        // Movement
        if (keyboard.IsKeyPressed(Keys.Up) || keyboard.IsKeyPressed(Keys.W))
        {
            actionTaken = _gameStateManager.HandlePlayerMove(0, -1);
        }
        else if (keyboard.IsKeyPressed(Keys.Down) || keyboard.IsKeyPressed(Keys.S))
        {
            actionTaken = _gameStateManager.HandlePlayerMove(0, 1);
        }
        else if (keyboard.IsKeyPressed(Keys.Left) || keyboard.IsKeyPressed(Keys.A))
        {
            actionTaken = _gameStateManager.HandlePlayerMove(-1, 0);
        }
        else if (keyboard.IsKeyPressed(Keys.Right) || keyboard.IsKeyPressed(Keys.D))
        {
            actionTaken = _gameStateManager.HandlePlayerMove(1, 0);
        }
        // Diagonal movement
        else if (keyboard.IsKeyPressed(Keys.Home))
        {
            actionTaken = _gameStateManager.HandlePlayerMove(-1, -1);
        }
        else if (keyboard.IsKeyPressed(Keys.PageUp))
        {
            actionTaken = _gameStateManager.HandlePlayerMove(1, -1);
        }
        else if (keyboard.IsKeyPressed(Keys.End))
        {
            actionTaken = _gameStateManager.HandlePlayerMove(-1, 1);
        }
        else if (keyboard.IsKeyPressed(Keys.PageDown))
        {
            actionTaken = _gameStateManager.HandlePlayerMove(1, 1);
        }
        // Mode switching
        else if (keyboard.IsKeyPressed(Keys.E))
        {
            ToggleMode();
            return true;
        }
        // Export report
        else if (keyboard.IsKeyPressed(Keys.R))
        {
            _ = ExportReportAsync();
            return true;
        }
        // Toggle session stats
        else if (keyboard.IsKeyPressed(Keys.T))
        {
            ToggleSessionStats();
            return true;
        }
        // Toggle advanced analytics
        else if (keyboard.IsKeyPressed(Keys.A))
        {
            ToggleAdvancedAnalytics();
            return true;
        }
        // Toggle achievements
        else if (keyboard.IsKeyPressed(Keys.C))
        {
            ToggleAchievements();
            return true;
        }
        // Toggle leaderboards
        else if (keyboard.IsKeyPressed(Keys.L))
        {
            ToggleLeaderboard();
            return true;
        }
        // Navigate leaderboard categories
        else if (_showLeaderboard && _leaderboardRenderer != null)
        {
            if (keyboard.IsKeyPressed(Keys.Left))
            {
                _leaderboardRenderer.PreviousCategory();
                return true;
            }
            else if (keyboard.IsKeyPressed(Keys.Right))
            {
                _leaderboardRenderer.NextCategory();
                return true;
            }
        }
        // Quit
        else if (keyboard.IsKeyPressed(Keys.Escape))
        {
            System.Environment.Exit(0);
            return true;
        }

        if (actionTaken)
        {
            Render();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Toggles between play and edit modes
    /// </summary>
    private void ToggleMode()
    {
        var newMode = _gameStateManager.CurrentMode == GameMode.Play
            ? GameMode.Edit
            : GameMode.Play;

        _gameStateManager.SwitchMode(newMode);
        _hudRenderer.AddMessage($"Switched to {newMode} mode");

        Render();
    }

    /// <summary>
    /// Exports current session report
    /// </summary>
    private async Task ExportReportAsync()
    {
        _hudRenderer.AddMessage("Exporting session report...");
        _notificationOverlay.ShowInfo("Exporting report...");

        try
        {
            var stats = _reportExportService.GetQuickStats();
            _logger.LogInformation("Exporting report: {Stats}", stats);

            // Export all formats
            var results = await _reportExportService.ExportAllFormatsAsync().ConfigureAwait(false);

            var successCount = results.Values.Count(p => p != null);
            if (successCount > 0)
            {
                _hudRenderer.AddMessage($"✓ Report exported ({successCount} formats)");
                _notificationOverlay.ShowSuccess($"Report exported! {stats}");

                foreach (var result in results.Where(r => r.Value != null))
                {
                    _logger.LogInformation("Exported {Format}: {Path}", result.Key, result.Value);
                }
            }
            else
            {
                _hudRenderer.AddMessage("✗ Report export failed");
                _notificationOverlay.ShowError("Failed to export report");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report");
            _hudRenderer.AddMessage($"✗ Error: {ex.Message}");
            _notificationOverlay.ShowError($"Export error: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggles session stats visibility
    /// </summary>
    private void ToggleSessionStats()
    {
        _showSessionStats = !_showSessionStats;
        _sessionStatsHudRenderer.Console.IsVisible = _showSessionStats;

        var status = _showSessionStats ? "shown" : "hidden";
        _hudRenderer.AddMessage($"Session stats {status}");
        _notificationOverlay.ShowInfo($"Session stats {status}", 2.0);
    }

    /// <summary>
    /// Toggles advanced analytics visibility
    /// </summary>
    private void ToggleAdvancedAnalytics()
    {
        if (_advancedAnalyticsHudRenderer == null)
        {
            _hudRenderer.AddMessage("Advanced analytics not available");
            _notificationOverlay.ShowError("Analytics not available", 2.0);
            return;
        }

        _showAdvancedAnalytics = !_showAdvancedAnalytics;
        _advancedAnalyticsHudRenderer.Console.IsVisible = _showAdvancedAnalytics;

        var status = _showAdvancedAnalytics ? "shown" : "hidden";
        _hudRenderer.AddMessage($"Advanced analytics {status}");
        _notificationOverlay.ShowInfo($"Analytics {status}", 2.0);
    }

    /// <summary>
    /// Toggles achievement progress visibility
    /// </summary>
    private void ToggleAchievements()
    {
        if (_achievementProgressHud == null)
        {
            _hudRenderer.AddMessage("Achievements not available");
            _notificationOverlay.ShowError("Achievements not available", 2.0);
            return;
        }

        _showAchievements = !_showAchievements;
        _achievementProgressHud.Console.IsVisible = _showAchievements;

        // Hide session stats and analytics when showing achievements
        if (_showAchievements)
        {
            _sessionStatsHudRenderer.Console.IsVisible = false;
            if (_advancedAnalyticsHudRenderer != null)
            {
                _advancedAnalyticsHudRenderer.Console.IsVisible = false;
            }
        }
        else
        {
            _sessionStatsHudRenderer.Console.IsVisible = _showSessionStats;
            if (_advancedAnalyticsHudRenderer != null)
            {
                _advancedAnalyticsHudRenderer.Console.IsVisible = _showAdvancedAnalytics;
            }
        }

        var status = _showAchievements ? "shown" : "hidden";
        _hudRenderer.AddMessage($"Achievements {status}");
        _notificationOverlay.ShowInfo($"Achievements {status}", 2.0);

        if (_showAchievements && _metricsCollector.AchievementSystem != null)
        {
            var totalPoints = _metricsCollector.AchievementSystem.GetTotalPoints();
            var unlocked = _metricsCollector.AchievementSystem.Unlocks.Count;
            var total = _metricsCollector.AchievementSystem.AllAchievements.Count;
            _hudRenderer.AddMessage($"🏆 {unlocked}/{total} unlocked ({totalPoints}pts)");
        }
    }

    /// <summary>
    /// Toggles leaderboard visibility
    /// </summary>
    private void ToggleLeaderboard()
    {
        if (_leaderboardRenderer == null)
        {
            _hudRenderer.AddMessage("Leaderboards not available");
            _notificationOverlay.ShowError("Leaderboards not available", 2.0);
            return;
        }

        _showLeaderboard = !_showLeaderboard;
        _leaderboardRenderer.IsVisible = _showLeaderboard;

        // Hide other panels when showing leaderboard
        if (_showLeaderboard)
        {
            _hudRenderer.Console.IsVisible = false;
            _sessionStatsHudRenderer.Console.IsVisible = false;
            if (_advancedAnalyticsHudRenderer != null)
            {
                _advancedAnalyticsHudRenderer.Console.IsVisible = false;
            }
            if (_achievementProgressHud != null)
            {
                _achievementProgressHud.Console.IsVisible = false;
            }
        }
        else
        {
            _hudRenderer.Console.IsVisible = true;
            _sessionStatsHudRenderer.Console.IsVisible = _showSessionStats;
            if (_advancedAnalyticsHudRenderer != null)
            {
                _advancedAnalyticsHudRenderer.Console.IsVisible = _showAdvancedAnalytics;
            }
            if (_achievementProgressHud != null)
            {
                _achievementProgressHud.Console.IsVisible = _showAchievements;
            }
            Render(); // Re-render world
        }

        var status = _showLeaderboard ? "shown" : "hidden";
        _notificationOverlay.ShowInfo($"Leaderboards {status}", 2.0);
    }
}
