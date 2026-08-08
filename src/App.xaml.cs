using System.ComponentModel;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;

namespace LiveCaptionsTranslator
{
    public partial class App : System.Windows.Application
    {
        private Forms.NotifyIcon? _trayIcon;
        private Icon? _trayEnabledIcon;
        private Icon? _trayDisabledIcon;
        private MainWindow? _mainWindow;
        private bool _translationEnabled;
        private bool _exiting;

        public App()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Translator.Setting?.Save();

            // Start disabled: no translation API calls until the tray icon is enabled.
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
            Icon? appIcon = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
                    appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            }
            catch
            {
                // Ignore and fall back to a system icon below.
            }

            _trayEnabledIcon = appIcon != null ? (Icon)appIcon.Clone() : (Icon)SystemIcons.Application.Clone();
            _trayDisabledIcon = CreateDisabledIcon(_trayEnabledIcon);
            appIcon?.Dispose();

            _trayIcon = new Forms.NotifyIcon
            {
                Visible = true,
                Icon = _trayDisabledIcon,
                Text = "实时字幕翻译：已关闭",
                ContextMenuStrip = BuildTrayMenu()
            };

            _trayIcon.MouseClick += (_, args) =>
            {
                if (args.Button == Forms.MouseButtons.Left)
                    ToggleTranslation();
            };
        }

        private static Icon CreateDisabledIcon(Icon source)
        {
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.DrawIcon(source, new Rectangle(0, 0, 32, 32));
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var darkPen = new Pen(Color.FromArgb(210, 0, 0, 0), 5.2f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                using var whitePen = new Pen(Color.White, 3.1f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                graphics.DrawLine(darkPen, 6, 26, 26, 6);
                graphics.DrawLine(whitePen, 6, 26, 26, 6);
            }

            var handle = bitmap.GetHicon();
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
            var menu = new Forms.ContextMenuStrip();

            var toggleItem = new Forms.ToolStripMenuItem("开启/关闭字幕");
            toggleItem.Click += (_, _) => ToggleTranslation();

            var settingsItem = new Forms.ToolStripMenuItem("设置");
            settingsItem.Click += (_, _) => ShowSettings();

            var exitItem = new Forms.ToolStripMenuItem("退出");
            exitItem.Click += (_, _) => ExitApplication();

            menu.Items.Add(toggleItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);
            return menu;
        }

        private void ToggleTranslation()
        {
            _translationEnabled = !_translationEnabled;
            Translator.LogOnlyFlag = !_translationEnabled;
            Translator.ClearContexts();

            if (_mainWindow != null)
                _mainWindow.SetOverlayEnabled(_translationEnabled);

            if (_trayIcon != null)
            {
                _trayIcon.Icon = _translationEnabled ? _trayEnabledIcon : _trayDisabledIcon;
                _trayIcon.Text = _translationEnabled ? "实时字幕翻译：已开启" : "实时字幕翻译：已关闭";
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
