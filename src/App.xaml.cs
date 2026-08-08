using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Media = System.Windows.Media;
using Imaging = System.Windows.Media.Imaging;

using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace LiveCaptionsTranslator
{
    public partial class App : System.Windows.Application
    {
        private Forms.NotifyIcon? _trayIcon;
        private Drawing.Icon? _trayEnabledIcon;
        private Drawing.Icon? _trayDisabledIcon;
        private System.Windows.Controls.ContextMenu? _trayMenu;
        private System.Windows.Controls.MenuItem? _enableMenuItem;
        private SymbolIcon? _enableCheckIcon;
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

            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnFrameworkElementLoaded));

            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
            _mainWindow.Closing += MainWindow_Closing;

            InitializeTrayIconSafely();
        }

        private static void OnFrameworkElementLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
                ChineseUiLocalizer.ApplyElement(element);
        }

        private void InitializeTrayIconSafely()
        {
            try
            {
                // Use the FILLED Fluent System Icons from the same WPF-UI package as the app.
                // They remain pure white for a dark taskbar, but are visibly heavier than Regular.
                _trayEnabledIcon = CreateFluentTrayIcon(SymbolRegular.ClosedCaption24, filled: true);
                _trayDisabledIcon = CreateFluentTrayIcon(SymbolRegular.ClosedCaptionOff24, filled: true);
            }
            catch
            {
                _trayEnabledIcon = CreateFallbackCaptionIcon(false);
                _trayDisabledIcon = CreateFallbackCaptionIcon(true);
            }

            try
            {
                _trayMenu = BuildTrayMenu();
                _trayIcon = new Forms.NotifyIcon
                {
                    Visible = true,
                    Icon = _trayDisabledIcon,
                    Text = "字幕已关闭，点击开启字幕"
                };

                _trayIcon.MouseUp += (_, args) =>
                {
                    if (args.Button == Forms.MouseButtons.Left)
                    {
                        Dispatcher.BeginInvoke(ToggleTranslation);
                    }
                    else if (args.Button == Forms.MouseButtons.Right)
                    {
                        Dispatcher.BeginInvoke(ShowTrayMenu);
                    }
                };
            }
            catch
            {
                _mainWindow?.Show();
                _mainWindow?.Activate();
            }
        }

        private static Drawing.Icon CreateFluentTrayIcon(SymbolRegular symbol, bool filled)
        {
            const int size = 32;

            string resourceKey = filled ? "FluentSystemIconsFilled" : "FluentSystemIcons";
            var fontFamily = Current.TryFindResource(resourceKey) as Media.FontFamily;
            if (fontFamily == null)
                throw new InvalidOperationException($"{resourceKey} font resource was not found.");

            string glyphText = filled ? symbol.Swap().GetString() : symbol.GetString();

            var glyph = new System.Windows.Controls.TextBlock
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

            var encoder = new Imaging.PngBitmapEncoder();
            encoder.Frames.Add(Imaging.BitmapFrame.Create(render));

            using var pngStream = new MemoryStream();
            encoder.Save(pngStream);
            pngStream.Position = 0;

            using var bitmap = new Drawing.Bitmap(pngStream);
            return IconFromBitmap(bitmap);
        }

        private static Drawing.Icon CreateFallbackCaptionIcon(bool off)
        {
            using var bitmap = new Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Drawing.Graphics.FromImage(bitmap))
            {
                graphics.Clear(Drawing.Color.Transparent);
                graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;

                using var pen = new Drawing.Pen(Drawing.Color.White, 3.0f)
                {
                    StartCap = Drawing2D.LineCap.Round,
                    EndCap = Drawing2D.LineCap.Round,
                    LineJoin = Drawing2D.LineJoin.Round
                };

                var rect = new Drawing.RectangleF(2.5f, 5.5f, 27f, 21f);
                using (var path = new Drawing2D.GraphicsPath())
                {
                    const float radius = 4f;
                    const float d = radius * 2f;
                    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    graphics.DrawPath(pen, path);
                }

                using var font = new Drawing.Font("Segoe UI", 10.5f, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
                using var brush = new Drawing.SolidBrush(Drawing.Color.White);
                using var format = new Drawing.StringFormat
                {
                    Alignment = Drawing.StringAlignment.Center,
                    LineAlignment = Drawing.StringAlignment.Center
                };
                graphics.DrawString("CC", font, brush, new Drawing.RectangleF(3, 6, 26, 20), format);

                if (off)
                    graphics.DrawLine(pen, 5, 27, 27, 5);
            }

            return IconFromBitmap(bitmap);
        }

        private static Drawing.Icon IconFromBitmap(Drawing.Bitmap bitmap)
        {
            var handle = bitmap.GetHicon();
            try
            {
                using var temporary = Drawing.Icon.FromHandle(handle);
                return (Drawing.Icon)temporary.Clone();
            }
            finally
            {
                NativeMethods.DestroyIcon(handle);
            }
        }

        private System.Windows.Controls.ContextMenu BuildTrayMenu()
        {
            // WPF-UI's ControlsDictionary supplies the real Fluent ContextMenu/MenuItem
            // templates: rounded corners, shadow, modern hover states and DPI-aware rendering.
            var menu = new System.Windows.Controls.ContextMenu
            {
                MinWidth = 185,
                FontSize = 14,
                FontFamily = new Media.FontFamily("Segoe UI Variable Text"),
                Placement = PlacementMode.MousePoint,
                StaysOpen = false
            };

            _enableCheckIcon = new SymbolIcon(SymbolRegular.Checkmark20, 16)
            {
                Visibility = Visibility.Hidden
            };

            _enableMenuItem = new System.Windows.Controls.MenuItem
            {
                Header = "启用字幕",
                Icon = _enableCheckIcon,
                FontWeight = FontWeights.SemiBold
            };
            _enableMenuItem.Click += (_, _) => ToggleTranslation();

            var settingsItem = new System.Windows.Controls.MenuItem
            {
                Header = "设置",
                Icon = CreateEmptyMenuIcon()
            };
            settingsItem.Click += (_, _) => ShowSettings();

            var exitItem = new System.Windows.Controls.MenuItem
            {
                Header = "退出",
                Icon = CreateEmptyMenuIcon()
            };
            exitItem.Click += (_, _) => ExitApplication();

            menu.Items.Add(_enableMenuItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(exitItem);

            menu.Opened += (_, _) => UpdateTrayMenuState();
            return menu;
        }

        private static FrameworkElement CreateEmptyMenuIcon()
        {
            return new Border
            {
                Width = 20,
                Height = 20,
                Background = Media.Brushes.Transparent
            };
        }

        private void ShowTrayMenu()
        {
            if (_trayMenu == null)
                return;

            UpdateTrayMenuState();
            _trayMenu.Placement = PlacementMode.MousePoint;
            _trayMenu.IsOpen = true;
        }

        private void UpdateTrayMenuState()
        {
            if (_enableCheckIcon != null)
                _enableCheckIcon.Visibility = _translationEnabled ? Visibility.Visible : Visibility.Hidden;
        }

        private void ToggleTranslation()
        {
            _translationEnabled = !_translationEnabled;
            Translator.LogOnlyFlag = !_translationEnabled;
            Translator.ClearContexts();

            if (_mainWindow != null)
                _mainWindow.SetOverlayEnabled(_translationEnabled);

            UpdateTrayMenuState();

            if (_trayIcon != null)
            {
                _trayIcon.Icon = _translationEnabled ? _trayEnabledIcon : _trayDisabledIcon;
                _trayIcon.Text = _translationEnabled
                    ? "字幕已开启，点击关闭字幕"
                    : "字幕已关闭，点击开启字幕";
            }
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

            if (_trayMenu != null)
            {
                _trayMenu.IsOpen = false;
                _trayMenu = null;
            }

            _trayIcon?.Dispose();
            _trayIcon = null;
            _trayEnabledIcon?.Dispose();
            _trayEnabledIcon = null;
            _trayDisabledIcon?.Dispose();
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

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool DestroyIcon(IntPtr hIcon);
        }
    }
}
