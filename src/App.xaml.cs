using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
                // Original WPF-UI Fluent System Icons, rendered white for dark taskbars.
                _trayEnabledIcon = CreateFluentTrayIcon(SymbolRegular.ClosedCaption24);
                _trayDisabledIcon = CreateFluentTrayIcon(SymbolRegular.ClosedCaptionOff24);
            }
            catch
            {
                // Do not fall back to the EXE icon. Keep a white caption-style symbol instead.
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

                _trayIcon.MouseClick += (_, args) =>
                {
                    if (args.Button == Forms.MouseButtons.Left)
                        ToggleTranslation();
                };
            }
            catch
            {
                // If NotifyIcon itself fails, keep the program usable instead of silently exiting.
                _mainWindow?.Show();
                _mainWindow?.Activate();
            }
        }

        private static Drawing.Icon CreateFluentTrayIcon(SymbolRegular symbol)
        {
            const int size = 32;

            var fontFamily = Current.TryFindResource("FluentSystemIcons") as Media.FontFamily;
            if (fontFamily == null)
                throw new InvalidOperationException("FluentSystemIcons font resource was not found.");

            var glyph = new TextBlock
            {
                Width = size,
                Height = size,
                Text = symbol.GetString(),
                FontFamily = fontFamily,
                FontSize = 25,
                Foreground = Media.Brushes.White,
                Background = Media.Brushes.Transparent,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                SnapsToDevicePixels = true
            };

            glyph.Measure(new System.Windows.Size(size, size));
            glyph.Arrange(new Rect(0, 0, size, size));
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

                using var pen = new Drawing.Pen(Drawing.Color.White, 2.2f)
                {
                    StartCap = Drawing2D.LineCap.Round,
                    EndCap = Drawing2D.LineCap.Round,
                    LineJoin = Drawing2D.LineJoin.Round
                };

                var rect = new Drawing.RectangleF(4.5f, 7.5f, 23f, 17f);
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

                using var font = new Drawing.Font("Segoe UI", 8.7f, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
                using var brush = new Drawing.SolidBrush(Drawing.Color.White);
                using var format = new Drawing.StringFormat
                {
                    Alignment = Drawing.StringAlignment.Center,
                    LineAlignment = Drawing.StringAlignment.Center
                };
                graphics.DrawString("CC", font, brush, new Drawing.RectangleF(5, 8, 22, 16), format);

                if (off)
                    graphics.DrawLine(pen, 7, 25, 25, 7);
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
                BackColor = Drawing.Color.FromArgb(45, 45, 45),
                ForeColor = Drawing.Color.White,
                Font = new Drawing.Font("Microsoft YaHei UI", 10.5f, Drawing.FontStyle.Regular),
                ShowImageMargin = false,
                ShowCheckMargin = true,
                Padding = new Forms.Padding(5, 7, 5, 7),
                Renderer = new DarkTrayMenuRenderer()
            };

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

            menu.Opened += (_, _) => ApplyRoundedRegion(menu, 7);
            menu.SizeChanged += (_, _) => ApplyRoundedRegion(menu, 7);
            return menu;
        }

        private static Forms.ToolStripMenuItem CreateTrayMenuItem(string text)
        {
            return new Forms.ToolStripMenuItem(text)
            {
                AutoSize = false,
                Size = new Drawing.Size(245, 38),
                ForeColor = Drawing.Color.White,
                BackColor = Drawing.Color.Transparent,
                Padding = new Forms.Padding(9, 0, 12, 0),
                CheckOnClick = false
            };
        }

        private static void ApplyRoundedRegion(Forms.Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0)
                return;

            using var path = CreateRoundedRectanglePath(
                new Drawing.Rectangle(0, 0, control.Width, control.Height), radius);
            control.Region?.Dispose();
            control.Region = new Drawing.Region(path);
        }

        private static Drawing2D.GraphicsPath CreateRoundedRectanglePath(Drawing.Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new Drawing2D.GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter - 1, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter - 1, bounds.Bottom - diameter - 1, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter - 1, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ToggleTranslation()
        {
            _translationEnabled = !_translationEnabled;
            Translator.LogOnlyFlag = !_translationEnabled;
            Translator.ClearContexts();

            if (_mainWindow != null)
                _mainWindow.SetOverlayEnabled(_translationEnabled);

            if (_enableMenuItem != null)
                _enableMenuItem.Checked = _translationEnabled;

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
            _trayIcon?.Dispose();
            _trayIcon = null;
            _trayMenu?.Dispose();
            _trayMenu = null;
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

        private sealed class DarkTrayMenuColorTable : Forms.ProfessionalColorTable
        {
            private static readonly Drawing.Color Background = Drawing.Color.FromArgb(45, 45, 45);
            private static readonly Drawing.Color Hover = Drawing.Color.FromArgb(61, 61, 61);
            private static readonly Drawing.Color Border = Drawing.Color.FromArgb(82, 82, 82);

            public override Drawing.Color ToolStripDropDownBackground => Background;
            public override Drawing.Color ImageMarginGradientBegin => Background;
            public override Drawing.Color ImageMarginGradientMiddle => Background;
            public override Drawing.Color ImageMarginGradientEnd => Background;
            public override Drawing.Color MenuItemSelected => Hover;
            public override Drawing.Color MenuItemSelectedGradientBegin => Hover;
            public override Drawing.Color MenuItemSelectedGradientEnd => Hover;
            public override Drawing.Color MenuItemBorder => Drawing.Color.Transparent;
            public override Drawing.Color MenuBorder => Border;
            public override Drawing.Color SeparatorDark => Border;
            public override Drawing.Color SeparatorLight => Border;
        }

        private sealed class DarkTrayMenuRenderer : Forms.ToolStripProfessionalRenderer
        {
            public DarkTrayMenuRenderer() : base(new DarkTrayMenuColorTable())
            {
                RoundedEdges = true;
            }

            protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = Drawing.Color.White;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
            {
                var graphics = e.Graphics;
                graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Drawing.Rectangle(3, 1, Math.Max(1, e.Item.Width - 6), Math.Max(1, e.Item.Height - 2));
                var fill = e.Item.Selected
                    ? Drawing.Color.FromArgb(61, 61, 61)
                    : Drawing.Color.FromArgb(45, 45, 45);

                using var brush = new Drawing.SolidBrush(fill);
                using var path = CreateRoundedRectanglePath(rect, 5);
                graphics.FillPath(brush, path);
            }

            protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
            {
                if (e.Item is not Forms.ToolStripMenuItem item || !item.Checked)
                    return;

                var graphics = e.Graphics;
                graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new Drawing.Pen(Drawing.Color.White, 1.8f)
                {
                    StartCap = Drawing2D.LineCap.Round,
                    EndCap = Drawing2D.LineCap.Round
                };

                int x = e.ImageRectangle.Left + 3;
                int y = e.ImageRectangle.Top + e.ImageRectangle.Height / 2;
                graphics.DrawLines(pen, new Drawing.Point[]
                {
                    new Drawing.Point(x, y),
                    new Drawing.Point(x + 4, y + 4),
                    new Drawing.Point(x + 11, y - 4)
                });
            }

            protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Drawing.Rectangle(0, 0, Math.Max(1, e.ToolStrip.Width - 1), Math.Max(1, e.ToolStrip.Height - 1));
                using var pen = new Drawing.Pen(Drawing.Color.FromArgb(82, 82, 82), 1f);
                using var path = CreateRoundedRectanglePath(rect, 7);
                e.Graphics.DrawPath(pen, path);
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
