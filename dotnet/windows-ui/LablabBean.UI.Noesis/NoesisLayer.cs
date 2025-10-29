using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoesisGUI.MonoGameWrapper;
using NoesisGUI.MonoGameWrapper.Providers;

namespace LablabBean.UI.Noesis
{
    public class NoesisLayer : IDisposable
    {
        private readonly GameWindow _gameWindow;
        private readonly GraphicsDeviceManager _graphics;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Func<Viewport> _getViewport;

        private NoesisWrapper? _wrapper;
        private NoesisProviderManager? _providers;
        private bool _enabled;

        public NoesisLayer(GameWindow gameWindow,
                           GraphicsDeviceManager graphics,
                           GraphicsDevice graphicsDevice,
                           Func<Viewport> getViewport)
        {
            _gameWindow = gameWindow ?? throw new ArgumentNullException(nameof(gameWindow));
            _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _getViewport = getViewport ?? throw new ArgumentNullException(nameof(getViewport));
        }

        public bool IsEnabled => _enabled && _wrapper is not null;

        public void Initialize(string? licenseName,
                               string? licenseKey,
                               string rootXamlPath,
                               string? themeXamlPath = null,
                               string? contentRoot = null)
        {
            if (string.IsNullOrWhiteSpace(licenseName) || string.IsNullOrWhiteSpace(licenseKey))
            {
                // License missing; disable overlay gracefully
                _enabled = false;
                return;
            }

            // Normalize paths
            rootXamlPath = Path.GetFullPath(rootXamlPath);
            if (!File.Exists(rootXamlPath))
                throw new FileNotFoundException("Root XAML not found", rootXamlPath);
            if (!string.IsNullOrEmpty(themeXamlPath))
                themeXamlPath = Path.GetFullPath(themeXamlPath);

            // Providers: simple folder-based providers rooted at contentRoot or file folders
            var xamlDir = Path.GetDirectoryName(rootXamlPath)!;
            var fontDir = contentRoot ?? xamlDir;
            var texDir = contentRoot ?? xamlDir;

            var xamlProvider = new FolderXamlProvider(xamlDir);
            var fontProvider = new FolderFontProvider(fontDir);
            var texProvider = new FolderTextureProvider(texDir);
            _providers = new NoesisProviderManager(xamlProvider, fontProvider, texProvider);

            // Build config and wrapper
            var config = new NoesisConfig(
                _gameWindow,
                _graphics,
                _providers,
                rootXamlPath,
                themeXamlPath!,
                TimeSpan.Zero,
                _getViewport,
                onErrorMessageReceived: null,
                onDevLogMessageReceived: null,
                onUnhandledException: null,
                isEnableDirectionalNavigation: true,
                isAcceptingMouseMiddleButtonInput: false);

            // Pull input timing from Windows settings for better UX
            config.SetupInputFromWindows();

            NoesisWrapper.Init(licenseName, licenseKey);
            _wrapper = new NoesisWrapper(config);
            _enabled = true;
        }

        public void UpdateInput(GameTime gameTime, bool isActive)
        {
            if (!IsEnabled) return;
            _wrapper!.UpdateInput(gameTime, isActive);
        }

        public void Update(GameTime gameTime)
        {
            if (!IsEnabled) return;
            _wrapper!.Update(gameTime);
        }

        public void PreRender()
        {
            if (!IsEnabled) return;
            _wrapper!.PreRender();
        }

        public void Render()
        {
            if (!IsEnabled) return;
            _wrapper!.Render();
        }

        public void Dispose()
        {
            try
            {
                _wrapper?.Dispose();
                _providers?.Dispose();
            }
            finally
            {
                _wrapper = null;
                _providers = null;
                _enabled = false;
            }
        }
    }
}
