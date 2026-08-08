using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;

namespace LiveCaptionsTranslator
{
    public partial class App : System.Windows.Application
    {
        private const string TrayEnabledPngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAACUAAAAjCAIAAACcpVRJAAACrklEQVR4AeyTX0xScRTHE1m0bgpLH0wfTJ2yfMkt55ZSm9kDLVNxavWmRi+ktAhRYdbKJEHUBcZLpL5VYmpmE7ccW4EtZ5u92JCF+QD5AA3Im9H409nYfODCzx/D+QT77uz8vvd3usufeswNth9w5SNEOHewvydvfeSfnmZxnPBPYY1+83t+DQ0ONTY344t/kf15ejvUOKN72NiloE7yemrQ77Pha+7Ymuntn3mCIikTxFhYMNtv3qGVoMxQKaTRqt9tDvYbiOV1OagGm4/f7fb6/1MsoXsRtxmFG9eXqR719NVdqIN99ykxn8m/wHz7o5VRwUlNTd/2oCS6PxWSNj41Lu6UXKiu7Orv6+uThdoWFRfqJydaW1otVVUqF8trV62E/VsTlSSSd2dk5AypVxbkKwS2BZkQDHel0urSrGxLhbSHnPEfcITYY5uGIEBaPOEqwi4pWv65Oz0zBLkCyufkDmmZlnWCz2TNvple+rASDwaVPS65fLvARwuKlpKTQaDSnM3J98vPyoDVJkhAxhcWDb4LXz8zMjGhq29gAhyAIiJjC4pF/SMv6esnpEl5dPXwrJLm5JwGwtfXTYrHU1fJKz5TCAMrPlmcczwAfISwe1CuVCofD3iEWmz+atU+17W3tYMKfTN4vh0T9RG36YFINqLjcS3BECJfn9ribW5rlj+VGo7Ff0S+TScNNrVZrY1PD6Njo+8VFSafk5asXYT9WxOVBve+fb+7dnKxHNvt2FnJwwvJ4Pbrnunv3e0xmUyAQCJuxIooX1yJEAOh0OoNxJMKEI4rHq+UV5BfApXgFOyVsF7JYTGohikccI7Qj2ob6hpzsHHwVnyoeHhzmcrlUGDgoHjxOS08TiUT6CT2+dM90ZWVlUBtVe/Ci1iRiJnmJTI9am5wndSaJOP8BAAD//1xa62sAAAAGSURBVAMAtkKaJT3PlVEAAAAASUVORK5CYII=";

        private const string TrayDisabledPngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAADcAAAAcCAIAAABK0rDkAAAC6UlEQVR4AeyWX0xSURjAgaBMwtzC4XLL0YNvvthyDiShB19LJ7y6yuZ6cUjGH7E/orVyzIaXcLqZYlItXqqX0BwPxQu00ZBHntKA+afNEuXPEPrs6h0Yf8+9s9y8++Ce833f+c5v3/nOPYcR3gz//8KgHYbniJK6VTrK5T/PpdVq7bnTEwptUkeSKxLiivMqK50up1qjOhhQRMpLIpG2t8/91Y2Drq//VKqUUpk0t3Tc7HC6XLmSlsWGSAkpnLHM0Ol0z4IHQJlMZq9Gy+Pxssyyqw4EAorb3R9stt1+wS9Eyvn5j4uL30YMmK5/gAA1YkbrG2sOeff2vVAgxLARyH3BhDuOiJTLK8vcM9wLdXUSsZgAhQTvhMz+Y7GYEsnleDwejUaye2WwIFKmRgLQe3fvEzWaagLu2bk5zGj0er3b24lUU1FtCigBxTxthln3gfr9fqmsrV/34NXrl523Om2zRZcjxMSFAsqJ5xPfl5aePB56OPgIr1HghsxhRgy2l+WFxfHJMTY6BhWJT4nwT5YyEom6vriam5tFjY2w9ESNhsNhtUqj1fbx+XwGg1FbW1tefhqBDx9ClhKiJBKJiooKaICkgsLnSSgQgJK8UEAJqQoGgwRKKmgoRM0RSpaypORE/cV6u93+2eFIJpM+n29ldZVyULKUkMKO6zf45/kqtVIoErZfa5+anATlH1AdsZlAQ0YooGSfYo+Pjg/rh2VtMv2QvlvejQNJxBJiM5FcegoogYl1nNXQ0CCXywUCAbRpe4+kmJNpb1CGNyIlm81e+7HmdrszhExX7QMVNYpqamo4nLJ0rzw9RMqWKy3V56q75F3SfLc1cDCZnjGPMfGTiU6jGZ4aSktP5uFKNyNSQi2ajKbWq63p0bL2uFxu1dkqz4JHpVFFIsVdNSAoIiWM5JRxFApFjnva3yZ8MwHo1lYYIhQu6JSFz0F4Qo0O6AZ/bWzEYjFCWUjjQCkBSNzUND1lLvZM/w0AAP//rVQmRQAAAAZJREFUAwDXZigcCOevOQAAAABJRU5ErkJggg==";

        private Forms.NotifyIcon? _trayIcon;
        private Icon? _trayEnabledIcon;
        private Icon? _trayDisabledIcon;
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
            InitializeTrayIcon();
        }

        private static void OnFrameworkElementLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
                ChineseUiLocalizer.ApplyElement(element);
        }

        private void InitializeTrayIcon()
        {
            _trayEnabledIcon = CreateWhiteTrayIcon(TrayEnabledPngBase64);
            _trayDisabledIcon = CreateWhiteTrayIcon(TrayDisabledPngBase64);
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

        private static Icon CreateWhiteTrayIcon(string pngBase64)
        {
            using var stream = new MemoryStream(Convert.FromBase64String(pngBase64));
            using var source = new Bitmap(stream);
            using var mask = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    int luminance = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                    int alpha = luminance >= 246 ? 0 : Math.Min(255, (246 - luminance) * 2);
                    mask.SetPixel(x, y, System.Drawing.Color.FromArgb(alpha, 255, 255, 255));
                }
            }

            using var canvas = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(canvas))
            {
                graphics.Clear(System.Drawing.Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                const int maxWidth = 28;
                const int maxHeight = 26;
                double scale = Math.Min((double)maxWidth / mask.Width, (double)maxHeight / mask.Height);
                int width = Math.Max(1, (int)Math.Round(mask.Width * scale));
                int height = Math.Max(1, (int)Math.Round(mask.Height * scale));
                int left = (32 - width) / 2;
                int top = (32 - height) / 2;
                graphics.DrawImage(mask, new Rectangle(left, top, width, height));
            }

            var handle = canvas.GetHicon();
            try
            {
                using var temporary = Icon.FromHandle(handle);
                return (Icon)temporary.Clone();
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
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5f, System.Drawing.FontStyle.Regular),
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
                Size = new System.Drawing.Size(245, 38),
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.Transparent,
                Padding = new Forms.Padding(9, 0, 12, 0),
                CheckOnClick = false
            };
        }

        private static void ApplyRoundedRegion(Forms.Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0)
                return;

            using var path = CreateRoundedRectanglePath(
                new Rectangle(0, 0, control.Width, control.Height), radius);
            control.Region?.Dispose();
            control.Region = new Region(path);
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
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
            private static readonly System.Drawing.Color Background = System.Drawing.Color.FromArgb(45, 45, 45);
            private static readonly System.Drawing.Color Hover = System.Drawing.Color.FromArgb(61, 61, 61);
            private static readonly System.Drawing.Color Border = System.Drawing.Color.FromArgb(82, 82, 82);

            public override System.Drawing.Color ToolStripDropDownBackground => Background;
            public override System.Drawing.Color ImageMarginGradientBegin => Background;
            public override System.Drawing.Color ImageMarginGradientMiddle => Background;
            public override System.Drawing.Color ImageMarginGradientEnd => Background;
            public override System.Drawing.Color MenuItemSelected => Hover;
            public override System.Drawing.Color MenuItemSelectedGradientBegin => Hover;
            public override System.Drawing.Color MenuItemSelectedGradientEnd => Hover;
            public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.Transparent;
            public override System.Drawing.Color MenuBorder => Border;
            public override System.Drawing.Color SeparatorDark => Border;
            public override System.Drawing.Color SeparatorLight => Border;
        }

        private sealed class DarkTrayMenuRenderer : Forms.ToolStripProfessionalRenderer
        {
            public DarkTrayMenuRenderer() : base(new DarkTrayMenuColorTable())
            {
                RoundedEdges = true;
            }

            protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = System.Drawing.Color.White;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
            {
                var graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(3, 1, Math.Max(1, e.Item.Width - 6), Math.Max(1, e.Item.Height - 2));
                var fill = e.Item.Selected
                    ? System.Drawing.Color.FromArgb(61, 61, 61)
                    : System.Drawing.Color.FromArgb(45, 45, 45);

                using var brush = new SolidBrush(fill);
                using var path = CreateRoundedRectanglePath(rect, 5);
                graphics.FillPath(brush, path);
            }

            protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
            {
                if (e.Item is not Forms.ToolStripMenuItem item || !item.Checked)
                    return;

                var graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(System.Drawing.Color.White, 1.8f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                int x = e.ImageRectangle.Left + 3;
                int y = e.ImageRectangle.Top + e.ImageRectangle.Height / 2;
                graphics.DrawLines(pen, new System.Drawing.Point[]
                {
                    new System.Drawing.Point(x, y),
                    new System.Drawing.Point(x + 4, y + 4),
                    new System.Drawing.Point(x + 11, y - 4)
                });
            }

            protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, Math.Max(1, e.ToolStrip.Width - 1), Math.Max(1, e.ToolStrip.Height - 1));
                using var pen = new Pen(System.Drawing.Color.FromArgb(82, 82, 82), 1f);
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
