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
        private Popup? _trayPopup;
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
                _trayPopup = BuildTrayPopup();
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

        private Popup BuildTrayPopup()
        {
            var itemStyle = (Style)FindResource("TrayPopupItemStyle");

            var panel = new StackPanel
            {
                Width = 116
            };

            _enableCheckIcon = new SymbolIcon(SymbolRegular.Checkmark20, 11)
            {
                Visibility = Visibility.Hidden,
                Foreground = Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var enableItem = CreateTrayPopupItem("启用字幕", _enableCheckIcon, itemStyle);
            enableItem.Click += (_, _) =>
            {
                CloseTrayPopup();
                ToggleTranslation();
            };

            var settingsItem = CreateTrayPopupItem("设置", CreateEmptyMenuIcon(), itemStyle);
            settingsItem.Click += (_, _) =>
            {
                CloseTrayPopup();
                ShowSettings();
            };

            var exitItem = CreateTrayPopupItem("退出", CreateEmptyMenuIcon(), itemStyle);
            exitItem.Click += (_, _) =>
            {
                CloseTrayPopup();
                ExitApplication();
            };

            panel.Children.Add(enableItem);
            panel.Children.Add(settingsItem);
            panel.Children.Add(exitItem);

            var popupBorder = new Border
            {
                Background = new Media.SolidColorBrush(Media.Color.FromRgb(44, 44, 44)),
                BorderBrush = new Media.SolidColorBrush(Media.Color.FromRgb(71, 71, 71)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(3),
                Child = panel,
                SnapsToDevicePixels = true
            };

            return new Popup
            {
                AllowsTransparency = true,
                StaysOpen = false,
                Placement = PlacementMode.MousePoint,
                Child = popupBorder
            };
        }

        private static System.Windows.Controls.Button CreateTrayPopupItem(string text, FrameworkElement icon, Style style)
        {
            var grid = new Grid
            {
                Height = 22
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            icon.Width = 12;
            icon.Height = 12;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(icon, 0);

            var label = new System.Windows.Controls.TextBlock
            {
                Text = text,
                FontFamily = new Media.FontFamily("Microsoft YaHei"),
                FontSize = 11.0,
                FontWeight = FontWeights.Normal,
                Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(245, 245, 245)),
                Margin = new Thickness(3, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 1);

            grid.Children.Add(icon);
            grid.Children.Add(label);

            return new System.Windows.Controls.Button
            {
                Style = style,
                Content = grid
            };
        }

        private static FrameworkElement CreateEmptyMenuIcon()
        {
            return new Border
            {
                Width = 12,
                Height = 12,
                Background = Media.Brushes.Transparent
            };
        }

        private void ShowTrayMenu()
        {
            if (_trayPopup == null)
                return;

            UpdateTrayMenuState();
            _trayPopup.IsOpen = false;
            _trayPopup.Placement = PlacementMode.MousePoint;
            _trayPopup.IsOpen = true;
        }

        private void CloseTrayPopup()
        {
            if (_trayPopup != null)
                _trayPopup.IsOpen = false;
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

            CloseTrayPopup();
            _trayPopup = null;

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
