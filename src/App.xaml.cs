using System.ComponentModel;
using System.IO;
using System.Windows;
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
        private Forms.ContextMenuStrip? _trayMenu;
        private Forms.ToolStripMenuItem? _enableMenuItem;
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
                    Text = "字幕已关闭，点击开启字幕",
                    ContextMenuStrip = _trayMenu
                };

                // Let NotifyIcon/ContextMenuStrip handle right-click natively. This gives the
                // expected outside-click dismissal and keeps the menu inside the work area.
                _trayIcon.MouseUp += (_, args) =>
                {
                    if (args.Button == Forms.MouseButtons.Left)
                        Dispatcher.BeginInvoke(ToggleTranslation);
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

        private Forms.ContextMenuStrip BuildTrayMenu()
        {
            var menu = new Forms.ContextMenuStrip
            {
                BackColor = Drawing.Color.FromArgb(44, 44, 44),
                ForeColor = Drawing.Color.FromArgb(245, 245, 245),
                Font = new Drawing.Font("Microsoft YaHei", 8.25f, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point),
                Renderer = new DarkTrayMenuRenderer(),
                ShowImageMargin = false,
                ShowCheckMargin = true,
                Padding = new Forms.Padding(2),
                DropShadowEnabled = true,
                AutoSize = true
            };

            menu.HandleCreated += (_, _) => ApplyModernMenuWindowStyle(menu.Handle);

            _enableMenuItem = CreateTrayMenuItem("启用字幕");
            _enableMenuItem.Click += (_, _) => ToggleTranslation();

            var settingsItem = CreateTrayMenuItem("设置");
            settingsItem.Click += (_, _) => ShowSettings();

            var exitItem = CreateTrayMenuItem("退出");
            exitItem.Click += (_, _) => ExitApplication();

            menu.Items.Add(_enableMenuItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(exitItem);

            menu.Opening += (_, _) =>
            {
                if (_enableMenuItem != null)
                    _enableMenuItem.Checked = _translationEnabled;
            };

            return menu;
        }

        private static Forms.ToolStripMenuItem CreateTrayMenuItem(string text)
        {
            return new Forms.ToolStripMenuItem(text)
            {
                AutoSize = false,
                Size = new Drawing.Size(116, 22),
                ForeColor = Drawing.Color.FromArgb(245, 245, 245),
                BackColor = Drawing.Color.Transparent,
                Padding = new Forms.Padding(2, 0, 4, 0),
                Margin = new Forms.Padding(0)
            };
        }

        private static void ApplyModernMenuWindowStyle(IntPtr handle)
        {
            try
            {
                int dark = 1;
                NativeMethods.DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));

                // DWMWCP_ROUND = 2 on Windows 11.
                int corners = 2;
                NativeMethods.DwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
            }
            catch
            {
                // Rendering remains dark even if DWM attributes are unavailable.
            }
        }

        private void UpdateTrayMenuState()
        {
            if (_enableMenuItem != null)
                _enableMenuItem.Checked = _translationEnabled;
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

            if (_trayIcon != null)
                _trayIcon.ContextMenuStrip = null;

            _trayMenu?.Dispose();
            _trayMenu = null;
            _enableMenuItem = null;

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

        private sealed class DarkTrayMenuRenderer : Forms.ToolStripProfessionalRenderer
        {
            public DarkTrayMenuRenderer() : base(new DarkTrayColorTable())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
            {
                var rect = new Drawing.Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                var color = e.Item.Selected
                    ? Drawing.Color.FromArgb(58, 58, 58)
                    : Drawing.Color.FromArgb(44, 44, 44);

                using var brush = new Drawing.SolidBrush(color);
                using var path = CreateRoundedRectangle(rect, 4);
                e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.Default;
            }

            protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
            {
                if (e.Item is not Forms.ToolStripMenuItem item || !item.Checked)
                    return;

                var bounds = e.ImageRectangle;
                int cx = bounds.Left + bounds.Width / 2;
                int cy = bounds.Top + bounds.Height / 2;
                using var pen = new Drawing.Pen(Drawing.Color.White, 1.5f)
                {
                    StartCap = Drawing2D.LineCap.Round,
                    EndCap = Drawing2D.LineCap.Round,
                    LineJoin = Drawing2D.LineJoin.Round
                };
                e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawLines(pen, new[]
                {
                    new Drawing.Point(cx - 4, cy),
                    new Drawing.Point(cx - 1, cy + 3),
                    new Drawing.Point(cx + 5, cy - 4)
                });
                e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.Default;
            }

            private static Drawing2D.GraphicsPath CreateRoundedRectangle(Drawing.Rectangle rect, int radius)
            {
                int diameter = radius * 2;
                var path = new Drawing2D.GraphicsPath();
                path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
                path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
                path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                return path;
            }
        }

        private sealed class DarkTrayColorTable : Forms.ProfessionalColorTable
        {
            private static readonly Drawing.Color Background = Drawing.Color.FromArgb(44, 44, 44);
            private static readonly Drawing.Color Selected = Drawing.Color.FromArgb(58, 58, 58);
            private static readonly Drawing.Color Border = Drawing.Color.FromArgb(71, 71, 71);

            public override Drawing.Color ToolStripDropDownBackground => Background;
            public override Drawing.Color ImageMarginGradientBegin => Background;
            public override Drawing.Color ImageMarginGradientMiddle => Background;
            public override Drawing.Color ImageMarginGradientEnd => Background;
            public override Drawing.Color MenuBorder => Border;
            public override Drawing.Color MenuItemBorder => Selected;
            public override Drawing.Color MenuItemSelected => Selected;
            public override Drawing.Color MenuItemSelectedGradientBegin => Selected;
            public override Drawing.Color MenuItemSelectedGradientEnd => Selected;
            public override Drawing.Color MenuItemPressedGradientBegin => Selected;
            public override Drawing.Color MenuItemPressedGradientMiddle => Selected;
            public override Drawing.Color MenuItemPressedGradientEnd => Selected;
            public override Drawing.Color SeparatorDark => Border;
            public override Drawing.Color SeparatorLight => Border;
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool DestroyIcon(IntPtr hIcon);

            [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
        }
    }
}
