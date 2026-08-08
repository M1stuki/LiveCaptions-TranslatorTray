using System.ComponentModel;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Media = System.Windows.Media;
using Imaging = System.Windows.Media.Imaging;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using PlacementMode = System.Windows.Controls.Primitives.PlacementMode;

using LiveCaptionsTranslator.utils;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace LiveCaptionsTranslator
{
    public partial class App : System.Windows.Application
    {
        private Forms.NotifyIcon? _trayIcon;
        private Drawing.Icon? _trayEnabledIcon;
        private Drawing.Icon? _trayDisabledIcon;
        private WpfContextMenu? _trayMenu;
        private WpfMenuItem? _enableMenuItem;
        private System.Windows.Interop.HwndSource? _trayMenuOwner;
        private MainWindow? _mainWindow;
        private bool _translationEnabled;
        private bool _translationToggleBusy;
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

            InitializeTrayIconSafely();
        }

        private void InitializeTrayIconSafely()
        {
            try
            {
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
                EnsureTrayMenuOwner();
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

        private void EnsureTrayMenuOwner()
        {
            if (_trayMenuOwner != null || _mainWindow == null)
                return;

            // WPF-UI's own tray implementation uses a tiny hidden HWND as the shell/menu
            // message owner, then foregrounds that HWND before opening the ContextMenu.
            // Reproduce that behavior while keeping the existing reliable WinForms NotifyIcon.
            var parentHandle = new System.Windows.Interop.WindowInteropHelper(_mainWindow).EnsureHandle();
            _trayMenuOwner = new System.Windows.Interop.HwndSource(
                0x0,
                0x04000000,
                0x00080000 | 0x00000020 | 0x00000008 | 0x08000000,
                0,
                0,
                0,
                0,
                "LiveCaptionsTranslator_TrayMenuOwner",
                parentHandle);
        }

        private static Drawing.Icon CreateFluentTrayIcon(SymbolRegular symbol, bool filled)
        {
            const int size = 32;

            string resourceKey = filled ? "FluentSystemIconsFilled" : "FluentSystemIcons";
            var fontFamily = Current.TryFindResource(resourceKey) as Media.FontFamily;
            if (fontFamily == null)
                throw new InvalidOperationException($"{resourceKey} font resource was not found.");

            string glyphText = filled ? symbol.Swap().GetString() : symbol.GetString();

            var glyph = new WpfTextBlock
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

        private WpfContextMenu BuildTrayMenu()
        {
            var menu = new WpfContextMenu
            {
                FontFamily = new Media.FontFamily("Microsoft YaHei UI"),
                FontSize = 11.0,
                MinWidth = 128,
                StaysOpen = false
            };

            ApplyDarkTrayMenuPalette(menu);

            _enableMenuItem = CreateTrayMenuItem("开启字幕");
            _enableMenuItem.Click += (_, _) => ToggleTranslation();

            var settingsItem = CreateTrayMenuItem("设置");
            settingsItem.Click += (_, _) => ShowSettings();

            var exitItem = CreateTrayMenuItem("退出");
            exitItem.Click += (_, _) => ExitApplication();

            menu.Items.Add(_enableMenuItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(exitItem);

            menu.Opened += (_, _) => UpdateTrayMenuState();
            return menu;
        }

        private static void ApplyDarkTrayMenuPalette(WpfContextMenu menu)
        {
            var background = new Media.SolidColorBrush(Media.Color.FromRgb(44, 44, 44));
            var foreground = new Media.SolidColorBrush(Media.Color.FromRgb(245, 245, 245));
            var border = new Media.SolidColorBrush(Media.Color.FromArgb(0x33, 0, 0, 0));
            var hover = new Media.SolidColorBrush(Media.Color.FromArgb(0x0F, 255, 255, 255));
            var pressed = new Media.SolidColorBrush(Media.Color.FromArgb(0x0A, 255, 255, 255));
            var pressedText = new Media.SolidColorBrush(Media.Color.FromArgb(0xC5, 255, 255, 255));

            menu.Background = background;
            menu.Foreground = foreground;
            menu.BorderBrush = border;

            menu.Resources["ContextMenuBackground"] = background;
            menu.Resources["ContextMenuForeground"] = foreground;
            menu.Resources["ContextMenuBorderBrush"] = border;
            menu.Resources["MenuBarItemBackgroundSelected"] = hover;
            menu.Resources["MenuBarItemBackgroundPressed"] = pressed;
            menu.Resources["MenuBarItemTextForegroundPressed"] = pressedText;
        }

        private static WpfMenuItem CreateTrayMenuItem(string text)
        {
            return new WpfMenuItem
            {
                Header = text,
                IsCheckable = false,
                StaysOpenOnClick = false,
                Style = (Style)Current.FindResource("TrayContextMenuItemStyle")
            };
        }

        private void ShowTrayMenu()
        {
            if (_trayMenu == null)
                return;

            EnsureTrayMenuOwner();
            if (_trayMenuOwner == null)
                return;

            UpdateTrayMenuState();

            if (_trayMenu.IsOpen)
                _trayMenu.IsOpen = false;

            // This mirrors WPF-UI.Tray's OpenMenu(): foreground the dedicated tray HWND,
            // use MousePoint placement, and do not bind the popup to the hidden main window.
            _ = NativeMethods.SetForegroundWindow(_trayMenuOwner.Handle);
            System.Windows.Controls.ContextMenuService.SetPlacement(_trayMenu, PlacementMode.MousePoint);
            _trayMenu.PlacementTarget = null;
            _trayMenu.IsOpen = true;
        }

        private void UpdateTrayMenuState()
        {
            if (_enableMenuItem != null)
            {
                _enableMenuItem.Header = _translationToggleBusy
                    ? "正在切换…"
                    : (_translationEnabled ? "关闭字幕" : "开启字幕");
            }
        }

        private async void ToggleTranslation()
        {
            if (_translationToggleBusy)
                return;

            _translationToggleBusy = true;
            UpdateTrayMenuState();

            try
            {
                if (_translationEnabled)
                {
                    _translationEnabled = false;
                    Translator.LogOnlyFlag = true;
                    _mainWindow?.SetOverlayEnabled(false);
                    await Task.Run(Translator.StopEngine);
                }
                else
                {
                    await Task.Run(Translator.StartEngine);
                    Translator.LogOnlyFlag = false;
                    _translationEnabled = true;
                    Translator.ClearContexts();
                    _mainWindow?.SetOverlayEnabled(true);
                }
            }
            catch (Exception ex)
            {
                _translationEnabled = false;
                Translator.LogOnlyFlag = true;
                _mainWindow?.SetOverlayEnabled(false);

                try
                {
                    await Task.Run(Translator.StopEngine);
                }
                catch
                {
                }

                Forms.MessageBox.Show(
                    $"无法启动字幕识别：\r\n{ex.Message}",
                    "LiveCaptions Translator",
                    Forms.MessageBoxButtons.OK,
                    Forms.MessageBoxIcon.Error);
            }
            finally
            {
                _translationToggleBusy = false;
                UpdateTrayMenuState();
                UpdateTrayIconState();
            }
        }

        private void UpdateTrayIconState()
        {
            if (_trayIcon == null)
                return;

            _trayIcon.Icon = _translationEnabled ? _trayEnabledIcon : _trayDisabledIcon;
            _trayIcon.Text = _translationEnabled
                ? "字幕已开启，点击关闭字幕"
                : "字幕已关闭，点击开启字幕";
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

            try
            {
                Translator.StopEngine();
            }
            catch
            {
            }

            if (_trayMenu != null)
                _trayMenu.IsOpen = false;
            _trayMenu = null;
            _enableMenuItem = null;

            _trayIcon?.Dispose();
            _trayIcon = null;
            _trayEnabledIcon?.Dispose();
            _trayEnabledIcon = null;
            _trayDisabledIcon?.Dispose();
            _trayDisabledIcon = null;
            _trayMenuOwner?.Dispose();
            _trayMenuOwner = null;
            Shutdown();
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            try
            {
                Translator.StopEngine();
            }
            catch
            {
            }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool DestroyIcon(IntPtr hIcon);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool SetForegroundWindow(IntPtr hWnd);
        }
    }
}
