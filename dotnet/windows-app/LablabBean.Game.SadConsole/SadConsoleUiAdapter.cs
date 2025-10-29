using Arch.Core;
using LablabBean.Contracts.Game.Models;
using LablabBean.Contracts.Game.UI;
using LablabBean.Contracts.UI.Models;
using LablabBean.Contracts.UI.Services;
using LablabBean.Game.Core.Maps;
using LablabBean.Game.SadConsole.Screens;
using LablabBean.Rendering.Contracts;
using LablabBean.Game.Core.Components;
using CorePosition = LablabBean.Game.Core.Components.Position;
using ContractPosition = LablabBean.Contracts.Game.Models.Position;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using SadConsole;
using System.Runtime.InteropServices;

namespace LablabBean.Game.SadConsole;

/// <summary>
/// SadConsole adapter implementing IService and IDungeonCrawlerUI.
/// Wraps GameScreen and provides interface compliance for the plugin system.
/// </summary>
public partial class SadConsoleUiAdapter : IService, IDungeonCrawlerUI
{
    private readonly ISceneRenderer _sceneRenderer;
    private readonly ILogger<SadConsoleUiAdapter> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SadConsoleRenderStyles _styles;
    private GameScreen? _gameScreen;
    private World? _currentWorld;
    private DungeonMap? _currentMap;
    private bool _initialized;

    public SadConsoleUiAdapter(
        ISceneRenderer sceneRenderer,
        ILogger<SadConsoleUiAdapter> logger,
        IServiceProvider serviceProvider,
        SadConsoleRenderStyles? styles = null)
    {
        _sceneRenderer = sceneRenderer ?? throw new ArgumentNullException(nameof(sceneRenderer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _styles = styles ?? SadConsoleRenderStyles.Default();
    }

    #region IService Implementation

    public Task InitializeAsync(UIInitOptions options, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing SadConsole UI Adapter");
        Initialize();
        return Task.CompletedTask;
    }

    public Task RenderViewportAsync(ViewportBounds viewport, IReadOnlyCollection<Contracts.Game.Models.EntitySnapshot> entities)
    {
        _logger.LogDebug("Render viewport: {EntityCount} entities", entities.Count);

        // Build a TileBuffer snapshot and hand to renderer (kept simple for now)
        try
        {
            if (_currentWorld != null && _currentMap != null && _gameScreen != null)
            {
                var width = _gameScreen.WorldSurface.Width;
                var height = _gameScreen.WorldSurface.Height;
                if (TryBuildGlyphArray(_currentWorld, _currentMap, width, height, out var glyphs, out var camX, out var camY))
                {
                    var buffer = new TileBuffer(width, height, glyphMode: true);

                    // entity overlays in viewport space
                    uint[,] entFg = new uint[height, width];
                    uint[,] entBg = new uint[height, width];
                    int[,] entZ = new int[height, width];
                    for (int yy = 0; yy < height; yy++) for (int xx = 0; xx < width; xx++) entZ[yy, xx] = int.MinValue;

                    var query = new QueryDescription().WithAll<CorePosition, Renderable, Visible>();
                    _currentWorld.Query(in query, (Entity e, ref CorePosition pos, ref Renderable renderable, ref Visible vis) =>
                    {
                        if (!vis.IsVisible) return;
                        if (!_currentMap.IsInFOV(pos.Point)) return;
                        int vx = pos.Point.X - camX; int vy = pos.Point.Y - camY;
                        if (vx < 0 || vy < 0 || vx >= width || vy >= height) return;
                        if (renderable.ZOrder <= entZ[vy, vx]) return;
                        entZ[vy, vx] = renderable.ZOrder;
                        entFg[vy, vx] = ToArgb(renderable.Foreground);
                        entBg[vy, vx] = ToArgb(renderable.Background);
                    });

                    if (buffer.Glyphs != null)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                var ch = glyphs[y, x];
                                ColorRef fg, bg;
                                if (entZ[y, x] != int.MinValue)
                                {
                                    fg = new ColorRef(0, entFg[y, x]);
                                    bg = new ColorRef(0, entBg[y, x]);
                                }
                                else
                                {
                                    var style = _styles.LookupForGlyph(ch);
                                    fg = new ColorRef(0, style.ForegroundArgb);
                                    bg = new ColorRef(0, style.BackgroundArgb);
                                }
                                buffer.Glyphs[y, x] = new Glyph(ch, fg, bg);
                            }
                        }
                    }

                    _ = _sceneRenderer.RenderAsync(buffer, CancellationToken.None);
                }
            }
        }
        catch { }

        return Task.CompletedTask;
    }

    public Task UpdateDisplayAsync()
    {
        _logger.LogDebug("Display update requested");
        // SadConsole handles this automatically through its game loop
        return Task.CompletedTask;
    }

    public Task HandleInputAsync(InputCommand command)
    {
        _logger.LogDebug("Input command: {Command}", command);
        // TODO: Route input to GameScreen
        return Task.CompletedTask;
    }

    public ViewportBounds GetViewport()
    {
        // TODO: Get actual viewport from GameScreen
        return new ViewportBounds(new ContractPosition(0, 0), 80, 50);
    }

    public void SetViewportCenter(ContractPosition centerPosition)
    {
        _logger.LogDebug("Set viewport center: ({X}, {Y})", centerPosition.X, centerPosition.Y);
        // TODO: Update GameScreen camera
    }

    public void Initialize()
    {
        if (_initialized)
        {
            _logger.LogWarning("SadConsoleUiAdapter already initialized");
            return;
        }

        _logger.LogInformation("Creating SadConsole GameScreen");

        // Create GameScreen with proper dependencies from DI
        // Default dimensions - should match host configuration
        const int width = 120;
        const int height = 40;

        _gameScreen = ActivatorUtilities.CreateInstance<GameScreen>(_serviceProvider, width, height);
        _gameScreen.Initialize();

        // Plugin-only binding policy: renderer target is bound by the UI plugin, not here.

        _initialized = true;
        _logger.LogInformation("SadConsole UI adapter initialized with GameScreen ({Width}x{Height})", width, height);
    }

    public GameScreen? GetGameScreen()
    {
        return _gameScreen;
    }

    #endregion

    #region IDungeonCrawlerUI Implementation

    public void ToggleHud()
    {
        _logger.LogInformation("HUD toggle requested");
        // TODO: Toggle HUD visibility in GameScreen
    }

    public void ShowDialogue(string speaker, string text, string[]? choices = null)
    {
        _logger.LogInformation("Dialogue: {Speaker} - {Text}", speaker, text);
        // TODO: Show dialogue overlay in GameScreen
    }

    public void HideDialogue()
    {
        _logger.LogDebug("Hide dialogue requested");
        // TODO: Hide dialogue in GameScreen
    }

    public void ShowQuests()
    {
        _logger.LogInformation("Show quests requested");
        // TODO: Show quest panel in GameScreen
    }

    public void HideQuests()
    {
        _logger.LogDebug("Hide quests requested");
        // TODO: Hide quest panel in GameScreen
    }

    public void ShowInventory()
    {
        _logger.LogInformation("Show inventory requested");
        // TODO: Show inventory panel in GameScreen
    }

    public void HideInventory()
    {
        _logger.LogDebug("Hide inventory requested");
        // TODO: Hide inventory panel in GameScreen
    }

    public void UpdateCameraFollow(int entityX, int entityY)
    {
        _logger.LogDebug("Camera follow: ({X}, {Y})", entityX, entityY);
        // TODO: Update camera in GameScreen
    }

    public void SetCameraFollow(int entityId)
    {
        _logger.LogDebug("Set camera follow: entity {EntityId}", entityId);
        // TODO: Set camera to follow specific entity
    }

    public void UpdatePlayerStats(int health, int maxHealth, int mana, int maxMana, int level, int experience)
    {
        _logger.LogDebug("Update player stats: HP {Health}/{MaxHealth}, MP {Mana}/{MaxMana}, Lvl {Level}, XP {Experience}",
            health, maxHealth, mana, maxMana, level, experience);
        // TODO: Update HUD with player stats
    }

    #endregion

    #region Game State Management

    public void SetWorldContext(World world, DungeonMap map)
    {
        _logger.LogInformation("Setting world context");
        _currentWorld = world;
        _currentMap = map;

        // TODO: Update GameScreen with new world/map
    }

    #endregion

    private static uint ToArgb(SadRogue.Primitives.Color c)
        => (0xFFu << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
}

// SadConsoleRenderStyles - moved to file-scoped namespace
public sealed class SadConsoleRenderStyles
{
    public List<uint>? Palette { get; set; }
    public Style Floor { get; set; } = new Style('.', 0xFFC0C0C0, 0xFF000000);
    public Style Wall { get; set; } = new Style('#', 0xFF808080, 0xFF000000);
    public Style FloorExplored { get; set; } = new Style('·', 0xFF404040, 0xFF000000);
    public Style WallExplored { get; set; } = new Style('▒', 0xFF606060, 0xFF000000);
    public Style EntityDefault { get; set; } = new Style('@', 0xFFFFFFFF, 0xFF000000);

    public static SadConsoleRenderStyles Default()
    {
        return new SadConsoleRenderStyles
        {
            Palette = new List<uint>
            {
                0xFF000000, 0xFFFF0000, 0xFF00FF00, 0xFFFFFF00,
                0xFF0000FF, 0xFFFF00FF, 0xFF00FFFF, 0xFFB0B0B0,
                0xFF505050, 0xFFFF8080, 0xFF80FF80, 0xFFFFFF80,
                0xFF8080FF, 0xFFFF80FF, 0xFF80FFFF, 0xFFFFFFFF
            }
        };
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly record struct Style(char Glyph, uint ForegroundArgb, uint BackgroundArgb)
    {
        public char Glyph { get; init; } = Glyph;
        public uint ForegroundArgb { get; init; } = ForegroundArgb;
        public uint BackgroundArgb { get; init; } = BackgroundArgb;
    }

    public Style LookupForGlyph(char glyph)
    {
        return glyph switch
        {
            '.' => Floor,
            '#' => Wall,
            '·' => FloorExplored,
            '▒' => WallExplored,
            _ => EntityDefault
        };
    }
}

partial class SadConsoleUiAdapter
{
    private bool TryBuildGlyphArray(World world, DungeonMap map, int viewWidth, int viewHeight, out char[,] buffer, out int camX, out int camY)
    {
        buffer = default!; camX = 0; camY = 0;
        if (viewWidth <= 0 || viewHeight <= 0) return false;

        // Find player position
        SadRogue.Primitives.Point? player = null;
        var q = new QueryDescription().WithAll<Player, CorePosition>();
        world.Query(in q, (Entity e, ref Player p, ref CorePosition pos) => { player = pos.Point; });
        if (player == null) return false;

        camX = player.Value.X - viewWidth / 2;
        camY = player.Value.Y - viewHeight / 2;
        camX = Math.Max(0, Math.Min(camX, map.Width - viewWidth));
        camY = Math.Max(0, Math.Min(camY, map.Height - viewHeight));

        buffer = new char[viewHeight, viewWidth];
        for (int y = 0; y < viewHeight && y + camY < map.Height; y++)
        {
            for (int x = 0; x < viewWidth && x + camX < map.Width; x++)
            {
                var wp = new SadRogue.Primitives.Point(x + camX, y + camY);
                char glyph = ' ';
                if (map.IsInFOV(wp))
                {
                    glyph = map.IsWalkable(wp) ? '.' : '#';
                }
                else if (map.FogOfWar.IsExplored(wp))
                {
                    glyph = map.IsWalkable(wp) ? '·' : '▒';
                }
                buffer[y, x] = glyph;
            }
        }
        return true;
    }
}
