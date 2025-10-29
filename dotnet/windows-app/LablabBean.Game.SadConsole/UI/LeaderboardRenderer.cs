using System;
using System.Collections.Generic;
using System.Linq;
using LablabBean.Reporting.Analytics;
using LablabBean.Reporting.Contracts.Models;
using SadConsole;
using SadRogue.Primitives;

namespace LablabBean.Game.SadConsole.UI;

/// <summary>
/// Renders leaderboards and player stats overlay
/// </summary>
public class LeaderboardRenderer
{
    private readonly LeaderboardSystem _leaderboardSystem;
    private readonly int _width;
    private readonly int _height;
    private LeaderboardCategory _currentCategory = LeaderboardCategory.TotalScore;
    private readonly List<LeaderboardCategory> _categories;
    private int _selectedCategoryIndex = 0;

    public bool IsVisible { get; set; }

    public LeaderboardRenderer(LeaderboardSystem leaderboardSystem, int width, int height)
    {
        _leaderboardSystem = leaderboardSystem;
        _width = width;
        _height = height;
        _categories = Enum.GetValues(typeof(LeaderboardCategory)).Cast<LeaderboardCategory>().ToList();
    }

    /// <summary>
    /// Render leaderboard overlay
    /// </summary>
    public void Render(IScreenSurface surface)
    {
        if (!IsVisible) return;

        var console = surface as ScreenSurface;
        if (console == null) return;

        int startX = 5;
        int startY = 3;
        int panelWidth = _width - 10;
        int panelHeight = _height - 6;

        // Draw background panel
        DrawPanel(console, startX, startY, panelWidth, panelHeight, Color.Black, Color.Yellow);

        // Header
        var title = "🏆 LEADERBOARDS 🏆";
        console.Print(startX + (panelWidth - title.Length) / 2, startY + 1, title, Color.Yellow);

        // Category selector
        DrawCategorySelector(console, startX + 2, startY + 3, panelWidth - 4);

        // Leaderboard content
        DrawLeaderboardContent(console, startX + 2, startY + 7, panelWidth - 4, panelHeight - 10);

        // Player stats summary
        DrawPlayerStats(console, startX + 2, startY + panelHeight - 3);

        // Footer
        var footer = "← → Change Category | L Close | ESC Exit";
        console.Print(startX + (panelWidth - footer.Length) / 2, startY + panelHeight - 1, footer, Color.Gray);
    }

    /// <summary>
    /// Draw category selector tabs
    /// </summary>
    private void DrawCategorySelector(ScreenSurface console, int x, int y, int width)
    {
        var categoryNames = new Dictionary<LeaderboardCategory, string>
        {
            [LeaderboardCategory.TotalScore] = "Total Score",
            [LeaderboardCategory.HighestKills] = "Kills",
            [LeaderboardCategory.BestKDRatio] = "K/D Ratio",
            [LeaderboardCategory.MostLevelsCompleted] = "Levels",
            [LeaderboardCategory.FastestCompletion] = "Speed",
            [LeaderboardCategory.MostItemsCollected] = "Items",
            [LeaderboardCategory.DeepestDungeon] = "Depth",
            [LeaderboardCategory.AchievementPoints] = "Achievements"
        };

        int currentX = x;

        for (int i = 0; i < _categories.Count; i++)
        {
            var category = _categories[i];
            var name = categoryNames.GetValueOrDefault(category, category.ToString());
            var isSelected = i == _selectedCategoryIndex;

            var bgColor = isSelected ? Color.Yellow : Color.DarkGray;
            var fgColor = isSelected ? Color.Black : Color.White;

            if (currentX + name.Length + 4 > x + width)
                break; // No more space

            console.Print(currentX, y, $" {name} ", fgColor, bgColor);
            currentX += name.Length + 3;
        }

        _currentCategory = _categories[_selectedCategoryIndex];
    }

    /// <summary>
    /// Draw leaderboard entries
    /// </summary>
    private void DrawLeaderboardContent(ScreenSurface console, int x, int y, int width, int maxRows)
    {
        var entries = _leaderboardSystem.GetLeaderboard(_currentCategory, maxRows);
        var playerProfile = _leaderboardSystem.GetPlayerProfile();
        var playerName = playerProfile.PlayerName;

        if (!entries.Any())
        {
            console.Print(x + width / 2 - 10, y + maxRows / 2, "No entries yet!", Color.Gray);
            return;
        }

        // Header
        console.Print(x, y, "Rank", Color.Yellow);
        console.Print(x + 6, y, "Player", Color.Yellow);
        console.Print(x + 26, y, "Score", Color.Yellow);
        console.Print(x + 38, y, "Details", Color.Yellow);

        // Entries
        for (int i = 0; i < Math.Min(entries.Count, maxRows - 2); i++)
        {
            var entry = entries[i];
            int rowY = y + i + 2;

            var isPlayer = string.Equals(entry.PlayerName, playerName, StringComparison.Ordinal);
            var rankColor = GetRankColor(entry.Rank);
            var textColor = isPlayer ? Color.Cyan : Color.White;

            // Rank with medal
            var rankText = entry.Rank switch
            {
                1 => "🥇 #1",
                2 => "🥈 #2",
                3 => "🥉 #3",
                _ => $"  #{entry.Rank}"
            };
            console.Print(x, rowY, rankText, rankColor);

            // Player name (truncate if too long)
            var displayName = entry.PlayerName.Length > 18
                ? entry.PlayerName.Substring(0, 15) + "..."
                : entry.PlayerName;
            console.Print(x + 6, rowY, displayName, textColor);

            // Score
            var scoreText = FormatScore(entry.Score, _currentCategory);
            console.Print(x + 26, rowY, scoreText, textColor);

            // Details
            var details = GetEntryDetails(entry);
            if (details.Length > width - 40)
                details = details.Substring(0, width - 43) + "...";
            console.Print(x + 38, rowY, details, Color.Gray);
        }
    }

    /// <summary>
    /// Draw player stats summary
    /// </summary>
    private void DrawPlayerStats(ScreenSurface console, int x, int y)
    {
        var profile = _leaderboardSystem.GetPlayerProfile();
        var playerBests = _leaderboardSystem.GetPlayerBestEntries(profile.PlayerName);

        var stats = $"Player: {profile.PlayerName} | " +
                   $"Sessions: {profile.TotalSessions} | " +
                   $"Total Kills: {profile.TotalKills} | " +
                   $"Achievements: {profile.UnlockedAchievements.Count} ({profile.TotalAchievementPoints} pts) | " +
                   $"Playtime: {FormatTimespan(profile.TotalPlaytime)}";

        console.Print(x, y, stats, Color.Cyan);

        // Show rank in current category
        var rank = _leaderboardSystem.GetPlayerRank(_currentCategory, profile.PlayerName);
        if (rank.HasValue)
        {
            console.Print(x, y + 1, $"Your Rank in {_currentCategory}: #{rank.Value}", Color.Yellow);
        }
        else
        {
            console.Print(x, y + 1, $"Not ranked in {_currentCategory} yet", Color.Gray);
        }
    }

    /// <summary>
    /// Handle category navigation
    /// </summary>
    public void NextCategory()
    {
        _selectedCategoryIndex = (_selectedCategoryIndex + 1) % _categories.Count;
    }

    public void PreviousCategory()
    {
        _selectedCategoryIndex--;
        if (_selectedCategoryIndex < 0)
            _selectedCategoryIndex = _categories.Count - 1;
    }

    /// <summary>
    /// Get rank color based on position
    /// </summary>
    private Color GetRankColor(int rank)
    {
        return rank switch
        {
            1 => Color.Gold,
            2 => Color.Silver,
            3 => new Color(205, 127, 50), // Bronze
            <= 10 => Color.Yellow,
            _ => Color.White
        };
    }

    /// <summary>
    /// Format score based on category
    /// </summary>
    private string FormatScore(long score, LeaderboardCategory category)
    {
        return category switch
        {
            LeaderboardCategory.BestKDRatio => $"{score / 100.0:F2}",
            LeaderboardCategory.FastestCompletion => FormatTime(10000 - score),
            _ => score.ToString("N0")
        };
    }

    /// <summary>
    /// Get entry details summary
    /// </summary>
    private string GetEntryDetails(LeaderboardEntry entry)
    {
        var stats = entry.Stats;

        return _currentCategory switch
        {
            LeaderboardCategory.TotalScore =>
                $"K: {stats.GetValueOrDefault("Kills", 0)} D: {stats.GetValueOrDefault("Deaths", 0)} L: {stats.GetValueOrDefault("Levels", 0)}",

            LeaderboardCategory.HighestKills =>
                $"D: {stats.GetValueOrDefault("Deaths", 0)} K/D: {stats.GetValueOrDefault("KDRatio", 0):F2}",

            LeaderboardCategory.BestKDRatio =>
                $"K: {stats.GetValueOrDefault("Kills", 0)} D: {stats.GetValueOrDefault("Deaths", 0)}",

            LeaderboardCategory.FastestCompletion =>
                $"Avg: {stats.GetValueOrDefault("AvgTimePerLevel", "N/A")} L: {stats.GetValueOrDefault("Levels", 0)}",

            _ => $"{stats.GetValueOrDefault("Playtime", "00:00:00")}"
        };
    }

    /// <summary>
    /// Format timespan
    /// </summary>
    private string FormatTimespan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    /// <summary>
    /// Format time in seconds
    /// </summary>
    private string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
    }

    /// <summary>
    /// Draw bordered panel
    /// </summary>
    private void DrawPanel(ScreenSurface console, int x, int y, int width, int height, Color bgColor, Color borderColor)
    {
        // Fill background
        for (int py = y; py < y + height; py++)
        {
            for (int px = x; px < x + width; px++)
            {
                if (px >= 0 && px < console.Width && py >= 0 && py < console.Height)
                {
                    console.SetGlyph(px, py, ' ', bgColor, bgColor);
                }
            }
        }

        // Draw border
        DrawBox(console, x, y, width, height, borderColor);
    }

    /// <summary>
    /// Draw box border
    /// </summary>
    private void DrawBox(ScreenSurface console, int x, int y, int width, int height, Color color)
    {
        // Corners
        console.SetGlyph(x, y, '╔', color);
        console.SetGlyph(x + width - 1, y, '╗', color);
        console.SetGlyph(x, y + height - 1, '╚', color);
        console.SetGlyph(x + width - 1, y + height - 1, '╝', color);

        // Horizontal lines
        for (int px = x + 1; px < x + width - 1; px++)
        {
            console.SetGlyph(px, y, '═', color);
            console.SetGlyph(px, y + height - 1, '═', color);
        }

        // Vertical lines
        for (int py = y + 1; py < y + height - 1; py++)
        {
            console.SetGlyph(x, py, '║', color);
            console.SetGlyph(x + width - 1, py, '║', color);
        }
    }
}
