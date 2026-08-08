using System.ComponentModel;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;

namespace LiveCaptionsTranslator
{
    public partial class App : Application
    {
        private Forms.NotifyIcon? _trayIcon;
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
            Icon? icon = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
                    icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            }
            catch
            {
                // Ignore and fall back to a system icon below.
            }

            _trayIcon = new Forms.NotifyIcon
            {
                Visible = true,
                Icon = icon ?? SystemIcons.Application,
                Text = "实时字幕翻译：已关闭",
                ContextMenuStrip = BuildTrayMenu()
            };

            _trayIcon.MouseClick += (_, args) =>
            {
                if (args.Button == Forms.MouseButtons.Left)
                    ToggleTranslation();
            };
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
                _trayIcon.Text = _translationEnabled ? "实时字幕翻译：已开启" : "实时字幕翻译：已关闭";
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
