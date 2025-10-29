using Arch.Core;
using Arch.Core.Extensions;
using LablabBean.Game.Core.Components;
using LablabBean.Game.Core.Maps;
using SadConsole;
using SadRogue.Primitives;

namespace LablabBean.Game.SadConsole.Renderers;

/// <summary>
/// Renders the game world using SadConsole
/// </summary>
public class WorldRenderer
{
    private readonly ScreenSurface _surface;
    private Point _cameraOffset;

    public ScreenSurface Surface => _surface;

    public WorldRenderer(int width, int height)
    {
        _surface = new ScreenSurface(width, height);
        _cameraOffset = Point.None;
    }

    /// <summary>
    /// Renders the world
    /// </summary>
    public void Render(World world, DungeonMap map)
    {
        // Get player position for camera centering
        var playerPos = GetPlayerPosition(world);
        if (playerPos.HasValue)
        {
            // Center camera on player
            _cameraOffset = new Point(
                playerPos.Value.X - _surface.Width / 2,
                playerPos.Value.Y - _surface.Height / 2
            );

            // Clamp to map bounds
            _cameraOffset = new Point(
                Math.Max(0, Math.Min(_cameraOffset.X, map.Width - _surface.Width)),
                Math.Max(0, Math.Min(_cameraOffset.Y, map.Height - _surface.Height))
            );
        }

        // Clear surface
        _surface.Clear();

        // Render map
        RenderMap(map);

        // Render entities
        RenderEntities(world, map);
    }

    /// <summary>
    /// Renders the map tiles
    /// </summary>
    private void RenderMap(DungeonMap map)
    {
        for (int y = 0; y < _surface.Height; y++)
        {
            for (int x = 0; x < _surface.Width; x++)
            {
                var worldPos = new Point(x + _cameraOffset.X, y + _cameraOffset.Y);

                if (!map.IsInBounds(worldPos))
                    continue;

                int glyph;
                Color foreground;
                Color background = Color.Black;

                // Check if in FOV
                if (map.IsInFOV(worldPos))
                {
                    if (map.IsWalkable(worldPos))
                    {
                        glyph = '.';
                        foreground = Color.Gray;
                    }
                    else
                    {
                        glyph = '#';
                        foreground = Color.White;
                    }
                }
                else
                {
                    // Not visible
                    glyph = ' ';
                    foreground = Color.DarkGray;
                }

                _surface.SetGlyph(x, y, glyph, foreground, background);
            }
        }
    }

    /// <summary>
    /// Renders all visible entities
    /// </summary>
    private void RenderEntities(World world, DungeonMap map)
    {
        var query = new QueryDescription().WithAll<Position, Renderable, Visible>();

        // Collect entities to render
        var entitiesToRender = new List<(Point pos, char glyph, Color foreground, Color background, int zOrder)>();

        world.Query(in query, (Entity entity, ref Position pos, ref Renderable renderable, ref Visible visible) =>
        {
            if (!visible.IsVisible || !map.IsInFOV(pos.Point))
                return;

            // Convert to screen coordinates
            var screenPos = pos.Point - _cameraOffset;

            // Check if on screen
            if (screenPos.X >= 0 && screenPos.X < _surface.Width &&
                screenPos.Y >= 0 && screenPos.Y < _surface.Height)
            {
                entitiesToRender.Add((
                    screenPos,
                    renderable.Glyph,
                    renderable.Foreground,
                    renderable.Background,
                    renderable.ZOrder
                ));
            }
        });

        // Sort by Z-order
        entitiesToRender.Sort((a, b) => a.zOrder.CompareTo(b.zOrder));

        // Render entities
        foreach (var (pos, glyph, foreground, background, _) in entitiesToRender)
        {
            _surface.SetGlyph(pos.X, pos.Y, glyph, foreground, background);
        }
    }

    /// <summary>
    /// Gets player position
    /// </summary>
    private Point? GetPlayerPosition(World world)
    {
        var query = new QueryDescription().WithAll<Player, Position>();
        Point? result = null;

        world.Query(in query, (Entity entity, ref Player player, ref Position pos) =>
        {
            result = pos.Point;
        });

        return result;
    }
}
