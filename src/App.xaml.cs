using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Media = System.Windows.Media;
using Imaging = System.Windows.Media.Imaging;

using LiveCaptionsTranslator.utils;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using TrayNotifyIcon = Wpf.Ui.Tray.Controls.NotifyIcon;

namespace LiveCaptionsTranslator
{
    public partial class App : System.Windows.Application
    {
        private TrayNotifyIcon? _trayIcon;
        private MenuItem? _enableMenuItem;
        private Media.ImageSource? _trayEnabledIcon;
        private Media.ImageSource? _trayDisabledIcon;
        private MainWindow? _mainWindow;
        private bool _translationEnabled;
        private bool _exiting;

        public App()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Translator.Setting?.Save();
            Translator.LogOnlyFlag = true;
            Task.Run(() => Translator.SyncLoop());
            Task.Run(() => Translator.TranslateLoop());
            Task.Run(() => Translator.DisplayLoop());
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
            _mainWindow.Closing += MainWindow_Closing;

            // WPF-UI.Tray needs a real HWND as its shell owner. EnsureHandle creates it
            // without showing the main window, so startup remains tray-only and flicker-free.
            _ = new WindowInteropHelper(_mainWindow).EnsureHandle();
            InitializeTrayIconSafely();
        }

        private void InitializeTrayIconSafely()
        {
            try
            {
                try
                {
                    _trayEnabledIcon = CreateFluentTrayImage(SymbolRegular.ClosedCaption24, filled: true);
                    _trayDisabledIcon = CreateFluentTrayImage(SymbolRegular.ClosedCaptionOff24, filled: true);
                }
                catch
                {
                    _trayEnabledIcon = CreateFallbackTrayImage();
                    _trayDisabledIcon = _trayEnabledIcon;
                }

                var menu = BuildTrayMenu();

                _trayIcon = new TrayNotifyIcon
                {
                    FocusOnLeftClick = false,
                    MenuOnRightClick = true,
                    MenuFontSize = 15.0,
                    Icon = _trayDisabledIcon,
                    TooltipText = "字幕已关闭，点击开启字幕",
                    Menu = menu
                };

                _trayIcon.LeftClick += (_, _) => Dispatcher.BeginInvoke(ToggleTranslation);

                if (!_trayIcon.Register())
                    throw new InvalidOperationException("WPF-UI tray icon registration failed.");
            }
            catch
            {
                // Keep the program usable even if the shell tray icon cannot be registered.
                _trayIcon?.Dispose();
                _trayIcon = null;
                _mainWindow?.Show();
                _mainWindow?.Activate();
            }
        }

        private static Media.ImageSource CreateFluentTrayImage(SymbolRegular symbol, bool filled)
        {
            const int size = 32;

            string resourceKey = filled ? "FluentSystemIconsFilled" : "FluentSystemIcons";
            var fontFamily = Current.TryFindResource(resourceKey) as Media.FontFamily;
            if (fontFamily == null)
                throw new InvalidOperationException($"{resourceKey} font resource was not found.");

            string glyphText = filled ? symbol.Swap().GetString() : symbol.GetString();

            var glyph = new TextBlock
            {
                Width = size,
                Height = size,
                Text = glyphText,
                FontFamily = fontFamily,
                FontSize = 30,
                Foreground = Media.Brushes.White,
                Background = Media.Brushes.Transparent,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                SnapsToDevicePixels = true
            };

            glyph.Measure(new System.Windows.Size(size, size));
            glyph.Arrange(new Rect(0, -1, size, size + 2));
            glyph.UpdateLayout();

            var render = new Imaging.RenderTargetBitmap(
                size,
                size,
                96,
                96,
                Media.PixelFormats.Pbgra32);
            render.Render(glyph);
            render.Freeze();
            return render;
        }

        private static Media.ImageSource CreateFallbackTrayImage()
        {
            var bitmap = new Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = Imaging.BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(
                "pack://application:,,,/src/LiveCaptions-Translator.ico",
                UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private ContextMenu BuildTrayMenu()
        {
            // WPF UI supplies the ContextMenu/MenuItem Fluent templates. In its current
            // template each item already has 4 px outer margin + 8 px content margin and
            // 6 px vertical content margin, so text no longer hugs the window edge.
            var menu = new ContextMenu
            {
                FontFamily = new Media.FontFamily("Microsoft YaHei UI"),
                FontSize = 15.0,
                MinWidth = 152
            };

            _enableMenuItem = new MenuItem
            {
                Header = "启用字幕",
                IsCheckable = true,
                StaysOpenOnClick = false
            };
            _enableMenuItem.Click += (_, _) => ToggleTranslation();

            var settingsItem = new MenuItem { Header = "设置" };
            settingsItem.Click += (_, _) => ShowSettings();

            var exitItem = new MenuItem { Header = "退出" };
            exitItem.Click += (_, _) => ExitApplication();

            menu.Items.Add(_enableMenuItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(exitItem);

            menu.Opened += (_, _) => UpdateTrayMenuState();
            return menu;
        }

        private void UpdateTrayMenuState()
        {
            if (_enableMenuItem != null)
                _enableMenuItem.IsChecked = _translationEnabled;

            if (_trayIcon != null)
            {
                _trayIcon.Icon = _translationEnabled ? _trayEnabledIcon : _trayDisabledIcon;
                _trayIcon.TooltipText = _translationEnabled
                    ? "字幕已开启，点击关闭字幕"
                    : "字幕已关闭，点击开启字幕";
            }
        }

        private void ToggleTranslation()
        {
            _translationEnabled = !_translationEnabled;
            Translator.LogOnlyFlag = !_translationEnabled;
            Translator.ClearContexts();

            if (_mainWindow != null)
                _mainWindow.SetOverlayEnabled(_translationEnabled);

            UpdateTrayMenuState();
        }

        private void ShowSettings()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.ShowSettingsPage();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_exiting)
                return;

            e.Cancel = true;
            _mainWindow?.Hide();
        }

        private void ExitApplication()
        {
            _exiting = true;

            if (_trayIcon != null)
            {
                _trayIcon.Menu = null;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _enableMenuItem = null;
            _trayEnabledIcon = null;
            _trayDisabledIcon = null;
            Shutdown();
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            if (Translator.Window != null)
            {
                LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);
                LiveCaptionsHandler.KillLiveCaptions(Translator.Window);
            }
        }
    }
}
