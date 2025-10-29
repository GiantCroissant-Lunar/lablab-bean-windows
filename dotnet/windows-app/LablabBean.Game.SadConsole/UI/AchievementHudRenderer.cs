using LablabBean.Reporting.Analytics;
using LablabBean.Reporting.Contracts.Models;
using SadConsole;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace LablabBean.Game.SadConsole.UI;

/// <summary>
/// Achievement notification overlay
/// Displays achievement unlocks as temporary notifications
/// </summary>
public class AchievementNotificationOverlay
{
    private readonly ControlsConsole _console;
    private readonly Queue<AchievementNotification> _notifications;
    private AchievementNotification? _currentNotification;
    private double _displayTime;
    private const double NotificationDuration = 5.0; // seconds

    public ControlsConsole Console => _console;

    private class AchievementNotification
    {
        public AchievementDefinition Achievement { get; set; } = null!;
        public DateTime QueueTime { get; set; }
    }

    public AchievementNotificationOverlay(int width, int height)
    {
        _console = new ControlsConsole(width, height);
        _console.IsVisible = false;
        _notifications = new Queue<AchievementNotification>();
        _displayTime = 0;
    }

    /// <summary>
    /// Show achievement unlock notification
    /// </summary>
    public void ShowAchievement(AchievementDefinition achievement)
    {
        _notifications.Enqueue(new AchievementNotification
        {
            Achievement = achievement,
            QueueTime = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Update notification display
    /// </summary>
    public void Update(double deltaTime)
    {
        // If no current notification, try to show next one
        if (_currentNotification == null && _notifications.Count > 0)
        {
            _currentNotification = _notifications.Dequeue();
            _displayTime = 0;
            _console.IsVisible = true;
            RenderNotification(_currentNotification.Achievement);
        }

        // Update display time
        if (_currentNotification != null)
        {
            _displayTime += deltaTime;

            if (_displayTime >= NotificationDuration)
            {
                _currentNotification = null;
                _console.IsVisible = false;
                _console.Surface.Clear();
            }
        }
    }

    private void RenderNotification(AchievementDefinition achievement)
    {
        _console.Surface.Clear();

        // Calculate position (top-center)
        int boxWidth = 50;
        int boxHeight = 7;
        int x = (_console.Width - boxWidth) / 2;
        int y = 5;

        // Draw background box
        _console.Surface.DrawBox(
            new Rectangle(x, y, boxWidth, boxHeight),
            ShapeParameters.CreateStyledBoxFilled(
                ICellSurface.ConnectedLineThin,
                new ColoredGlyph(Color.Yellow, Color.Black),
                new ColoredGlyph(Color.White, new Color(20, 20, 40, 200))
            )
        );

        // Title
        _console.Surface.Print(x + 2, y, " ACHIEVEMENT UNLOCKED! ", Color.Yellow, Color.Black);

        // Icon and name
        string iconName = $"{achievement.Icon} {achievement.Name}";
        int nameX = x + (boxWidth - iconName.Length) / 2;
        _console.Surface.Print(nameX, y + 2, iconName, GetRarityColor(achievement.Rarity));

        // Description
        int descX = x + (boxWidth - achievement.Description.Length) / 2;
        _console.Surface.Print(descX, y + 3, achievement.Description, Color.LightGray);

        // Points
        string points = $"+{achievement.Points} points";
        int pointsX = x + (boxWidth - points.Length) / 2;
        _console.Surface.Print(pointsX, y + 5, points, Color.Gold);
    }

    private Color GetRarityColor(AchievementRarity rarity)
    {
        return rarity switch
        {
            AchievementRarity.Common => Color.White,
            AchievementRarity.Uncommon => Color.LightGreen,
            AchievementRarity.Rare => Color.LightBlue,
            AchievementRarity.Epic => Color.Purple,
            AchievementRarity.Legendary => Color.Orange,
            _ => Color.White
        };
    }
}

/// <summary>
/// Achievement progress HUD
/// Shows achievement list with progress bars
/// </summary>
public class AchievementProgressHud
{
    private readonly ControlsConsole _console;
    private readonly AchievementSystem _achievementSystem;

    public ControlsConsole Console => _console;

    public AchievementProgressHud(int width, int height, AchievementSystem achievementSystem)
    {
        _achievementSystem = achievementSystem ?? throw new ArgumentNullException(nameof(achievementSystem));
        _console = new ControlsConsole(width, height);
    }

    public void Render(Dictionary<string, double> metrics)
    {
        _console.Surface.Clear();

        // Draw border
        _console.Surface.DrawBox(
            new Rectangle(0, 0, _console.Width, _console.Height),
            ShapeParameters.CreateStyledBoxThin(Color.Gold)
        );

        // Title
        int totalPoints = _achievementSystem.GetTotalPoints();
        double completion = _achievementSystem.GetCompletionPercentage();
        string title = $" Achievements ({_achievementSystem.Unlocks.Count}/{_achievementSystem.AllAchievements.Count}) ";
        _console.Surface.Print(2, 0, title, Color.Yellow);

        int y = 2;

        // Progress summary
        _console.Surface.Print(2, y++, $"Total Points: {totalPoints}", Color.Gold);
        _console.Surface.Print(2, y++, $"Completion: {completion:F0}%", Color.LightGreen);
        y++;

        // Get progress for all achievements
        var progress = _achievementSystem.GetProgress(metrics);

        // Show recent unlocks
        var recentUnlocks = _achievementSystem.Unlocks
            .OrderByDescending(u => u.UnlockTime)
            .Take(3)
            .ToList();

        if (recentUnlocks.Any())
        {
            _console.Surface.Print(2, y++, "Recent Unlocks:", Color.Cyan);
            foreach (var unlock in recentUnlocks)
            {
                var achievement = _achievementSystem.AllAchievements
                    .FirstOrDefault(a => string.Equals(a.Id, unlock.AchievementId, StringComparison.Ordinal));

                if (achievement != null && y < _console.Height - 2)
                {
                    _console.Surface.Print(3, y++, $"{achievement.Icon} {achievement.Name}", Color.White);
                }
            }
            y++;
        }

        // Show in-progress achievements
        _console.Surface.Print(2, y++, "In Progress:", Color.Cyan);

        var inProgress = progress
            .Where(p => !p.IsUnlocked && p.ProgressPercentage > 0)
            .OrderByDescending(p => p.ProgressPercentage)
            .Take(5)
            .ToList();

        foreach (var p in inProgress)
        {
            if (y >= _console.Height - 2) break;

            var achievement = _achievementSystem.AllAchievements
                .FirstOrDefault(a => string.Equals(a.Id, p.AchievementId, StringComparison.Ordinal));

            if (achievement != null)
            {
                // Achievement name
                _console.Surface.Print(3, y, $"{achievement.Icon} {achievement.Name}", Color.White);
                y++;

                // Progress bar
                if (y < _console.Height - 2)
                {
                    DrawProgressBar(5, y, _console.Width - 7, p.ProgressPercentage);
                    y++;
                }
            }
        }
    }

    private void DrawProgressBar(int x, int y, int width, double percentage)
    {
        percentage = Math.Clamp(percentage, 0, 100);
        int filledWidth = (int)(width * percentage / 100);

        // Background
        for (int i = 0; i < width; i++)
        {
            _console.Surface.SetGlyph(x + i, y, '░', Color.DarkGray);
        }

        // Filled portion
        for (int i = 0; i < filledWidth; i++)
        {
            Color barColor = percentage >= 75 ? Color.Green : percentage >= 50 ? Color.Yellow : Color.Orange;
            _console.Surface.SetGlyph(x + i, y, '█', barColor);
        }

        // Percentage text
        string percentText = $"{percentage:F0}%";
        int textX = x + (width - percentText.Length) / 2;
        _console.Surface.Print(textX, y, percentText, Color.White);
    }
}
