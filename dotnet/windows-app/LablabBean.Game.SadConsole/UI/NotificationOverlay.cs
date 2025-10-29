using SadConsole;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace LablabBean.Game.SadConsole.UI;

/// <summary>
/// Displays temporary notification messages to the user
/// </summary>
public class NotificationOverlay
{
    private readonly ControlsConsole _console;
    private readonly Label _messageLabel;
    private DateTime? _hideTime;
    private bool _isVisible;

    public ControlsConsole Console => _console;
    public bool IsVisible => _isVisible;

    public NotificationOverlay(int screenWidth, int screenHeight)
    {
        int width = 60;
        int height = 5;

        _console = new ControlsConsole(width, height)
        {
            Position = new Point((screenWidth - width) / 2, screenHeight - height - 2),
            IsVisible = false
        };

        _messageLabel = new Label(width - 4)
        {
            Position = new Point(2, 2),
            DisplayText = "",
            TextColor = Color.White
        };

        _console.Controls.Add(_messageLabel);

        // Draw border
        _console.Surface.DrawBox(new Rectangle(0, 0, width, height),
            ShapeParameters.CreateStyledBox(ICellSurface.ConnectedLineThin,
            new ColoredGlyph(Color.Green, Color.Black)));

        // Fill background
        _console.Surface.Fill(Color.White, new Color(0, 40, 0), 0);
    }

    /// <summary>
    /// Shows a notification message
    /// </summary>
    /// <param name="message">Message to display</param>
    /// <param name="durationSeconds">How long to show (default 3 seconds)</param>
    /// <param name="color">Message color (default Green)</param>
    public void Show(string message, double durationSeconds = 3.0, Color? color = null)
    {
        _messageLabel.DisplayText = message;
        _messageLabel.TextColor = color ?? Color.LightGreen;
        _console.IsVisible = true;
        _isVisible = true;
        _hideTime = DateTime.UtcNow.AddSeconds(durationSeconds);
    }

    /// <summary>
    /// Shows a success notification
    /// </summary>
    public void ShowSuccess(string message, double durationSeconds = 3.0)
    {
        Show($"✓ {message}", durationSeconds, Color.LightGreen);
    }

    /// <summary>
    /// Shows an error notification
    /// </summary>
    public void ShowError(string message, double durationSeconds = 3.0)
    {
        Show($"✗ {message}", durationSeconds, Color.Red);
    }

    /// <summary>
    /// Shows an info notification
    /// </summary>
    public void ShowInfo(string message, double durationSeconds = 3.0)
    {
        Show($"ℹ {message}", durationSeconds, Color.Cyan);
    }

    /// <summary>
    /// Updates notification visibility
    /// </summary>
    public void Update()
    {
        if (_isVisible && _hideTime.HasValue && DateTime.UtcNow >= _hideTime.Value)
        {
            Hide();
        }
    }

    /// <summary>
    /// Hides the notification immediately
    /// </summary>
    public void Hide()
    {
        _console.IsVisible = false;
        _isVisible = false;
        _hideTime = null;
    }
}
