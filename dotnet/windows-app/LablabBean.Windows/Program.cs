using LablabBean.Infrastructure.Extensions;
using LablabBean.Plugins.Core;
using LablabBean.Plugins.Contracts;
using LablabBean.Reactive.Extensions;
using LablabBean.Game.Core.Services;
using LablabBean.Game.Core.Systems;
using LablabBean.Game.Core.Worlds;
using LablabBean.Game.Core.Components;
using LablabBean.Game.Core.Maps;
using LablabBean.Game.SadConsole.Screens;
using LablabBean.Game.SadConsole.Services;
using LablabBean.Game.SadConsole;
using LablabBean.Windows;
using LablabBean.Reporting.Analytics;
using LablabBean.Reporting.Contracts;
using LablabBean.Plugins.Reporting.Html;
using LablabBean.Plugins.Reporting.Csv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SadConsole;
using SadConsole.Configuration;
using Serilog;
using System.Reflection;
#if WINDOWS_DX
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using LablabBean.UI.Noesis;
#endif

try
{
    // Build configuration
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    // Configure Serilog
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(configuration)
        .Enrich.FromLogContext()
        .WriteTo.File("logs/lablab-bean-windows-.log", rollingInterval: RollingInterval.Day)
        .CreateLogger();

    Log.Information("Starting Lablab Bean Windows application");

    // Build DI container
    var services = new ServiceCollection();

    // Add configuration to DI container
    services.AddSingleton<IConfiguration>(configuration);

    // Add logging services
    services.AddLogging(builder =>
    {
        builder.AddSerilog(dispose: true);
    });

    services.AddLablabBeanInfrastructure(configuration);
    services.AddLablabBeanReactive();

    // Add plugin system (note: requires manual start/stop since not using Generic Host)
    services.AddPluginSystem(configuration);

    // Add reporting services
    services.AddTransient<SessionStatisticsProvider>();
    services.AddTransient<PluginHealthProvider>();
    services.AddSingleton<AdvancedAnalyticsCollector>();
    services.AddSingleton<PersistenceService>();
    services.AddSingleton(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<AchievementSystem>>();
        var sessionId = Guid.NewGuid().ToString();
        return new AchievementSystem(logger, sessionId);
    });
    services.AddSingleton(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<LeaderboardSystem>>();
        var persistence = sp.GetRequiredService<PersistenceService>();
        return new LeaderboardSystem(logger, persistence);
    });
    services.AddSingleton<SessionMetricsCollector>();
    services.AddSingleton<ReportExportService>();

    // Register game services required by GameScreen
    services.AddSingleton<GameWorldManager>();
    services.AddSingleton<MovementSystem>();
    services.AddSingleton<CombatSystem>();
    services.AddSingleton<AISystem>();
    services.AddSingleton<ActorSystem>();
    services.AddSingleton<InventorySystem>();
    services.AddSingleton<ItemSpawnSystem>();
    services.AddSingleton<StatusEffectSystem>();
    services.AddSingleton<ActivityLogSystem>();
    services.AddSingleton<LablabBean.Contracts.UI.Services.IActivityLogService, LablabBean.Game.Core.Services.ActivityLogService>();
    // Bridge old UI ActivityLogService to new game-specific IActivityLog via adapter
    services.AddSingleton<LablabBean.Contracts.Game.UI.Services.IActivityLog>(sp =>
        new LablabBean.Contracts.Game.UI.Services.Adapters.ActivityLogAdapter(
            sp.GetRequiredService<LablabBean.Contracts.UI.Services.IActivityLogService>()));
    services.AddSingleton<LevelManager>();
    services.AddSingleton<GameStateManager>();

    var serviceProvider = services.BuildServiceProvider();

    // Start plugin system to load SadConsole plugins
    var pluginService = serviceProvider.GetServices<IHostedService>()
        .FirstOrDefault(s => s.GetType().Name.Contains("PluginLoader"));
    if (pluginService != null)
    {
        Log.Information("Starting plugin system");
        await pluginService.StartAsync(CancellationToken.None);
    }
    else
    {
        Log.Warning("Plugin loader service not found - plugins will not be loaded");
    }

    // Configure SadConsole
    var width = GameSettings.GAME_WIDTH;
    var height = GameSettings.GAME_HEIGHT;

    var builder = new Builder()
        .SetScreenSize(width, height)
        .IsStartingScreenFocused(true)
        .ConfigureFonts(true);

    // Start SadConsole with a callback to set up the screen after initialization
    Game.Create(builder);

#if WINDOWS_DX
    // Gate Noesis overlay behind config flag (disabled by default)
    var noesisEnabled = configuration.GetValue<bool>("Noesis:Enabled", false);
    if (noesisEnabled)
    {
        // Try to locate the underlying MonoGame Game and Graphics manager via reflection
        static T? FindInObjectGraph<T>(object root, int maxDepth = 3) where T : class
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var queue = new Queue<(object obj, int depth)>();
            queue.Enqueue((root, 0));
        visited.Add(root);
        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (current is T found)
                return found;
            if (depth >= maxDepth) continue;
            var type = current.GetType();
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var prop in type.GetProperties(bf))
            {
                if (!prop.CanRead) continue;
                object? val = null;
                try { val = prop.GetValue(current); } catch { }
                if (val == null || visited.Contains(val)) continue;
                visited.Add(val);
                queue.Enqueue((val, depth + 1));
            }
            foreach (var field in type.GetFields(bf))
            {
                object? val = null;
                try { val = field.GetValue(current); } catch { }
                if (val == null || visited.Contains(val)) continue;
                visited.Add(val);
                queue.Enqueue((val, depth + 1));
            }
        }
        return null;
    }

        // Resolve MonoGame objects
        var mgGame = FindInObjectGraph<Game>(Game.Instance) ?? throw new InvalidOperationException("MonoGame Game instance not found for WINDOWS_DX");
        var gdm = FindInObjectGraph<GraphicsDeviceManager>(mgGame) ?? FindInObjectGraph<GraphicsDeviceManager>(Game.Instance)
                  ?? throw new InvalidOperationException("GraphicsDeviceManager not found for WINDOWS_DX");
        var graphicsDevice = mgGame.GraphicsDevice ?? throw new InvalidOperationException("GraphicsDevice not available");
        var gameWindow = mgGame.Window ?? throw new InvalidOperationException("GameWindow not available");

        // Prepare Noesis layer
        var noesisLayer = new NoesisLayer(gameWindow, gdm, graphicsDevice, () => graphicsDevice.Viewport);
        var licenseName = Environment.GetEnvironmentVariable("NOESIS_LICENSE_NAME") ?? configuration["Noesis:LicenseName"];
        var licenseKey  = Environment.GetEnvironmentVariable("NOESIS_LICENSE_KEY")  ?? configuration["Noesis:LicenseKey"];
        var xamlRoot = Path.GetFullPath("dotnet/windows-ui/LablabBean.UI.Noesis/Assets/Root.xaml");
        var xamlTheme = Path.GetFullPath("dotnet/windows-ui/LablabBean.UI.Noesis/Assets/Theme.xaml");
        try
        {
            noesisLayer.Initialize(licenseName, licenseKey, xamlRoot, xamlTheme);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Noesis overlay initialization failed; continuing without overlay");
        }

        // Add a MonoGame component to drive Noesis update and render each frame
        if (noesisLayer.IsEnabled)
        {
            mgGame.Components.Add(new NoesisComponent(mgGame, noesisLayer));
            Log.Information("Noesis overlay enabled (WINDOWS_DX)");
        }

        // Local component type
        sealed class NoesisComponent : DrawableGameComponent
        {
            private readonly NoesisLayer _layer;
            public NoesisComponent(Game game, NoesisLayer layer) : base(game) { _layer = layer; }
            public override void Update(GameTime gameTime)
            {
                _layer.UpdateInput(gameTime, Game.IsActive);
                _layer.Update(gameTime);
                base.Update(gameTime);
            }
            public override void Draw(GameTime gameTime)
            {
                _layer.PreRender();
                _layer.Render();
                base.Draw(gameTime);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) _layer.Dispose();
                base.Dispose(disposing);
            }
        }
    }
#endif

    // SadConsole is now initialized, safe to create screens
    Game.Instance.Started += (sender, args) =>
    {
        // Try to get UI adapter from plugin system registry
        var pluginRegistry = serviceProvider.GetService<IPluginRegistry>();
        var serviceRegistry = serviceProvider.GetService<IRegistry>();
        SadConsoleUiAdapter? uiAdapter = null;

        if (serviceRegistry != null)
        {
            var uiServices = serviceRegistry.GetAll<SadConsoleUiAdapter>();
            uiAdapter = uiServices.FirstOrDefault();

            if (uiAdapter != null)
            {
                Log.Information("Using plugin-provided SadConsole UI adapter");
            }
        }

        if (uiAdapter != null)
        {
            // Get GameScreen from adapter and set it as the main screen
            var gameScreen = uiAdapter.GetGameScreen();
            if (gameScreen != null)
            {
                Game.Instance.Screen = gameScreen;
            }
            else
            {
                Log.Warning("UI adapter did not provide GameScreen, creating fallback");
                CreateFallbackGameScreen();
            }
        }
        else
        {
            Log.Warning("SadConsole UI plugin not loaded, creating fallback GameScreen");
            CreateFallbackGameScreen();
        }

        void CreateFallbackGameScreen()
        {
            var gameScreen = ActivatorUtilities.CreateInstance<GameScreen>(serviceProvider, width, height);
            gameScreen.Initialize();
            Game.Instance.Screen = gameScreen;
        }

        // Hook combat events for metrics collection
        var metricsCollector = serviceProvider.GetRequiredService<SessionMetricsCollector>();
        var advancedAnalytics = serviceProvider.GetRequiredService<AdvancedAnalyticsCollector>();
        var gameStateManager = serviceProvider.GetRequiredService<GameStateManager>();
        var combatSystem = gameStateManager.CombatSystem;
        var inventorySystem = gameStateManager.InventorySystem;
        var levelManager = gameStateManager.LevelManager;

        if (combatSystem != null)
        {
            combatSystem.OnEntityDied += entity =>
            {
                var world = gameStateManager.WorldManager.GetWorld(GameMode.Play);

                if (world.Has<Player>(entity))
                {
                    metricsCollector.TotalDeaths++;
                    Log.Information("Player died. Total deaths: {Deaths}", metricsCollector.TotalDeaths);
                }
                else if (world.Has<Enemy>(entity))
                {
                    metricsCollector.TotalKills++;

                    // Track enemy type (randomly assign for now)
                    var enemyTypes = Enum.GetValues<LablabBean.Reporting.Contracts.Models.EnemyType>();
                    var randomType = enemyTypes[Random.Shared.Next(enemyTypes.Length)];
                    advancedAnalytics.RecordEnemyKilled(randomType);

                    Log.Information("Enemy killed. Total kills: {Kills}", metricsCollector.TotalKills);
                }
            };

            // Hook damage tracking
            combatSystem.OnDamageDealt += (attacker, target, damage) =>
            {
                var world = gameStateManager.WorldManager.GetWorld(GameMode.Play);
                if (world.Has<Player>(attacker))
                {
                    bool isCritical = Random.Shared.Next(100) < 15; // 15% crit chance simulation
                    advancedAnalytics.RecordDamageDealt(damage, isCritical);
                }
                else if (world.Has<Player>(target))
                {
                    advancedAnalytics.RecordDamageTaken(damage);
                }
            };

            // Hook healing tracking
            combatSystem.OnHealed += (entity, healAmount) =>
            {
                var world = gameStateManager.WorldManager.GetWorld(GameMode.Play);
                if (world.Has<Player>(entity))
                {
                    advancedAnalytics.RecordHealing(healAmount);
                    Log.Information("Healing received: {Amount}", healAmount);
                }
            };

            // Hook dodge tracking
            combatSystem.OnAttackMissed += (attacker, target) =>
            {
                var world = gameStateManager.WorldManager.GetWorld(GameMode.Play);
                if (world.Has<Player>(target))
                {
                    advancedAnalytics.RecordPerfectDodge();
                    Log.Information("Perfect dodge!");
                }
            };
        }

        if (inventorySystem != null)
        {
            inventorySystem.OnItemPickedUp += (playerEntity, itemEntity) =>
            {
                metricsCollector.ItemsCollected++;

                // Track item type (randomly assign for now)
                var itemTypes = Enum.GetValues<LablabBean.Reporting.Contracts.Models.ItemType>();
                var randomType = itemTypes[Random.Shared.Next(itemTypes.Length)];
                advancedAnalytics.RecordItemCollected(randomType);

                Log.Information("Item collected. Total items: {Items}", metricsCollector.ItemsCollected);
            };
        }

        if (levelManager != null)
        {
            levelManager.OnLevelCompleted += levelNumber =>
            {
                metricsCollector.LevelsCompleted++;
                advancedAnalytics.EndLevel();

                // Start next level tracking
                advancedAnalytics.StartLevel();

                Log.Information("Level {Level} completed. Total levels: {Total}", levelNumber, metricsCollector.LevelsCompleted);
            };

            levelManager.OnNewDepthReached += depth =>
            {
                metricsCollector.MaxDepth = Math.Max(metricsCollector.MaxDepth, depth);
                Log.Information("New depth record: Level {Depth}", depth);
            };

            levelManager.OnDungeonCompleted += () =>
            {
                metricsCollector.DungeonsCompleted++;
                advancedAnalytics.EndDungeon();
                Log.Information("Dungeon completed! Total dungeons: {Count}", metricsCollector.DungeonsCompleted);
            };

            // Start initial level tracking
            advancedAnalytics.StartLevel();
            advancedAnalytics.StartDungeon();
        }

        Log.Information("Game metrics and advanced analytics collection initialized");
    };

    Game.Instance.Run();
    Game.Instance.Dispose();
#if WINDOWS_DX
    // NoesisLayer is disposed by the component; nothing else to do here
#endif

    // Export session report before exit
    var metricsCollector = serviceProvider.GetService<SessionMetricsCollector>();
    if (metricsCollector != null)
    {
        try
        {
            // Get version from assembly
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? assembly.GetName().Version?.ToString()
                       ?? "0.1.0-dev";

            var reportDir = Path.Combine("build", "_artifacts", version, "reports", "sessions");
            var reportPath = Path.Combine(reportDir, $"windows-session-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            await metricsCollector.ExportSessionReportAsync(reportPath, LablabBean.Reporting.Contracts.Models.ReportFormat.HTML);
            Log.Information("Session report exported to {Path}", reportPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to export session report");
        }
        finally
        {
            metricsCollector.Dispose();
        }
    }

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
