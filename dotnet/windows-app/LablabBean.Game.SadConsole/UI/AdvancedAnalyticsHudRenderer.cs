using LablabBean.Reporting.Analytics;
using LablabBean.Reporting.Contracts.Models;
using SadConsole;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace LablabBean.Game.SadConsole.UI;

/// <summary>
/// Advanced analytics HUD renderer
/// Displays detailed breakdowns of items, enemies, time, and combat
/// </summary>
public class AdvancedAnalyticsHudRenderer
{
    private readonly ControlsConsole _console;
    private readonly AdvancedAnalyticsCollector _analytics;

    public ControlsConsole Console => _console;

    public AdvancedAnalyticsHudRenderer(int width, int height, AdvancedAnalyticsCollector analytics)
    {
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        _console = new ControlsConsole(width, height);
    }

    public void Render(int totalKills, int totalDeaths)
    {
        _console.Surface.Clear();

        // Draw border
        _console.Surface.DrawBox(
            new Rectangle(0, 0, _console.Width, _console.Height),
            ShapeParameters.CreateStyledBoxThin(Color.Gray)
        );

        // Title
        _console.Surface.Print(2, 0, " Advanced Analytics ", Color.Yellow);

        int y = 2;

        // Time Analytics Section
        var timeAnalytics = _analytics.GetTimeAnalytics();
        _console.Surface.Print(2, y++, "Time Analytics:", Color.Cyan);
        _console.Surface.Print(3, y++, $"Total: {timeAnalytics.TotalPlaytime:hh\\:mm\\:ss}", Color.White);
        _console.Surface.Print(3, y++, $"Avg/Level: {timeAnalytics.AverageTimePerLevel:mm\\:ss}", Color.White);
        _console.Surface.Print(3, y++, $"Avg/Dungeon: {timeAnalytics.AverageTimePerDungeon:mm\\:ss}", Color.White);
        y++;

        // Combat Statistics Section
        var combatStats = _analytics.GetCombatStatistics(totalKills, totalDeaths);
        _console.Surface.Print(2, y++, "Combat Stats:", Color.Cyan);
        _console.Surface.Print(3, y++, $"Damage Out: {combatStats.DamageDealt}", Color.Green);
        _console.Surface.Print(3, y++, $"Damage In: {combatStats.DamageTaken}", Color.Red);
        _console.Surface.Print(3, y++, $"Healing: {combatStats.HealingReceived}", Color.LightGreen);
        _console.Surface.Print(3, y++, $"Crits: {combatStats.CriticalHits}", Color.Yellow);
        _console.Surface.Print(3, y++, $"Dodges: {combatStats.PerfectDodges}", Color.Cyan);
        _console.Surface.Print(3, y++, $"Avg Hit: {combatStats.AverageDamagePerHit:F1}", Color.White);
        _console.Surface.Print(3, y++, $"Survival: {combatStats.SurvivalRate:F1}%",
            GetColorByPercentage(combatStats.SurvivalRate));
        y++;

        // Item Breakdown Section
        var itemBreakdown = _analytics.GetItemTypeBreakdown();
        _console.Surface.Print(2, y++, "Item Types:", Color.Cyan);
        foreach (var item in itemBreakdown.Take(3))
        {
            _console.Surface.Print(3, y++, $"{item.Type}: {item.Count} ({item.Percentage:F0}%)", Color.White);
        }
        y++;

        // Enemy Distribution Section
        var enemyDist = _analytics.GetEnemyTypeDistribution();
        _console.Surface.Print(2, y++, "Enemy Kills:", Color.Cyan);
        foreach (var enemy in enemyDist.Take(3))
        {
            _console.Surface.Print(3, y++, $"{enemy.Type}: {enemy.Kills} ({enemy.Percentage:F0}%)", Color.White);
        }
    }

    private Color GetColorByPercentage(double percentage)
    {
        if (percentage >= 75) return Color.Green;
        if (percentage >= 50) return Color.Yellow;
        return Color.Red;
    }
}
