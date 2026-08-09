using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;
using Wpf.Ui.Controls;

namespace LiveCaptionsTranslator
{
    public partial class SettingPage : Page
    {
        private static SettingWindow? SettingWindow;

        public SettingPage()
        {
            InitializeComponent();
            ConfigureStableInfoToolTips();
            ApplicationThemeManager.ApplySystemTheme();
            DataContext = Translator.Setting;

            Loaded += (s, e) =>
            {
                (App.Current.MainWindow as MainWindow)?.AutoHeightAdjust(maxHeight: (int)App.Current.MainWindow.MinHeight);
                RefreshLiveCaptionsButton();
                CheckForFirstUse();
            };

            TranslateAPIBox.ItemsSource = Translator.Setting?.Configs.Keys;
            TranslateAPIBox.SelectedIndex = 0;

            LoadAPISetting();
        }

        private void ConfigureStableInfoToolTips()
        {
            // These controls originally opened a WPF-UI Flyout on MouseEnter and closed it
            // immediately on MouseLeave. With per-monitor DPI popups the newly opened popup can
            // alter hit testing around the tiny 15-DIP trigger and cause an open/close loop.
            // Detach those hover handlers and reuse the exact existing Flyout content inside the
            // WPF ToolTip service, which owns hover timing and does not require manual Hide().
            LiveCaptionsInfo.MouseEnter -= LiveCaptionsInfo_MouseEnter;
            LiveCaptionsInfo.MouseLeave -= LiveCaptionsInfo_MouseLeave;
            FrequencyInfo.MouseEnter -= FrequencyInfo_MouseEnter;
            FrequencyInfo.MouseLeave -= FrequencyInfo_MouseLeave;
            TranslateAPIInfo.MouseEnter -= TranslateAPIInfo_MouseEnter;
            TranslateAPIInfo.MouseLeave -= TranslateAPIInfo_MouseLeave;
            TargetLangInfo.MouseEnter -= TargetLangInfo_MouseEnter;
            TargetLangInfo.MouseLeave -= TargetLangInfo_MouseLeave;
            CaptionLogMaxInfo.MouseEnter -= CaptionLogMaxInfo_MouseEnter;
            CaptionLogMaxInfo.MouseLeave -= CaptionLogMaxInfo_MouseLeave;
            ContextAwareInfo.MouseEnter -= ContextAwareInfo_MouseEnter;
            ContextAwareInfo.MouseLeave -= ContextAwareInfo_MouseLeave;

            ConvertFlyoutToToolTip(LiveCaptionsInfo, LiveCaptionsInfoFlyout);
            ConvertFlyoutToToolTip(FrequencyInfo, FrequencyInfoFlyout);
            ConvertFlyoutToToolTip(TranslateAPIInfo, TranslateAPIInfoFlyout);
            ConvertFlyoutToToolTip(TargetLangInfo, TargetLangInfoFlyout);
            ConvertFlyoutToToolTip(CaptionLogMaxInfo, CaptionLogMaxInfoFlyout);
            ConvertFlyoutToToolTip(ContextAwareInfo, ContextAwareInfoFlyout);
        }

        private static void ConvertFlyoutToToolTip(
            System.Windows.Controls.Button trigger,
            Wpf.Ui.Controls.Flyout flyout)
        {
            object? originalContent = flyout.Content;
            flyout.Content = null;
            flyout.Visibility = Visibility.Collapsed;

            UIElement contentElement = originalContent as UIElement
                ?? new System.Windows.Controls.TextBlock { Text = originalContent?.ToString() ?? string.Empty };

            var chrome = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Child = contentElement,
                SnapsToDevicePixels = true
            };

            var toolTip = new System.Windows.Controls.ToolTip
            {
                Content = chrome,
                Placement = PlacementMode.Top,
                VerticalOffset = -4,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                FontFamily = SystemFonts.MessageFontFamily,
                FontSize = SystemFonts.MessageFontSize,
                HasDropShadow = true,
                StaysOpen = true,
                IsHitTestVisible = false
            };

            TextOptions.SetTextFormattingMode(toolTip, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(toolTip, TextRenderingMode.Grayscale);
            TextOptions.SetTextHintingMode(toolTip, TextHintingMode.Animated);

            trigger.ToolTip = toolTip;
            ToolTipService.SetInitialShowDelay(trigger, 120);
            ToolTipService.SetBetweenShowDelay(trigger, 50);
            ToolTipService.SetShowDuration(trigger, 30000);
        }

        private void LiveCaptionsButton_click(object sender, RoutedEventArgs e)
        {
            // Keep the user's show/hide choice even while the recognition engine is stopped.
            // This prevents the tray StopEngine/StartEngine cycle from losing UI state.
            Translator.LiveCaptionsHidden = !Translator.LiveCaptionsHidden;
            RefreshLiveCaptionsButton();
        }

        private void RefreshLiveCaptionsButton()
        {
            // The text describes the action that will happen next.
            ButtonText.Text = Translator.LiveCaptionsHidden ? "显示" : "隐藏";
        }

        private void TranslateAPIBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAPISetting();
        }

        private void TargetLangBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetLangBox.SelectedItem != null)
                Translator.Setting.TargetLanguage = TargetLangBox.SelectedItem.ToString();
        }

        private void TargetLangBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Translator.Setting.TargetLanguage = TargetLangBox.Text;
        }

        private void APISettingButton_click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SettingWindow != null && SettingWindow.IsLoaded)
                {
                    SettingWindow.Activate();
                    return;
                }

                SettingWindow = new SettingWindow();
                SettingWindow.Closed += (_, _) => SettingWindow = null;
                SettingWindow.Show();
            }
            catch (Exception ex)
            {
                SettingWindow = null;
                System.Windows.MessageBox.Show(
                    $"无法打开 API 设置。\n\n{ex.Message}",
                    "API 设置",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void Contexts_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.DisplaySentences = Translator.Setting.NumContexts;
        }

        private void DisplaySentences_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.NumContexts = Translator.Setting.DisplaySentences;
            Translator.Caption.OnPropertyChanged("DisplayLogCards");
            Translator.Caption.OnPropertyChanged("OverlayPreviousTranslation");
        }

        private void LiveCaptionsInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Show();
        }

        private void LiveCaptionsInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Hide();
        }

        private void FrequencyInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Show();
        }

        private void FrequencyInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Hide();
        }

        private void TranslateAPIInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Show();
        }

        private void TranslateAPIInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Hide();
        }

        private void TargetLangInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Show();
        }

        private void TargetLangInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Hide();
        }

        private void CaptionLogMaxInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Show();
        }

        private void CaptionLogMaxInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Hide();
        }

        private void ContextAwareInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Show();
        }

        private void ContextAwareInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Hide();
        }

        private void CheckForFirstUse()
        {
            if (Translator.FirstUseFlag)
            {
                Translator.LiveCaptionsHidden = false;
                RefreshLiveCaptionsButton();
            }
        }

        public void LoadAPISetting()
        {
            var configType = Translator.Setting[Translator.Setting.ApiName].GetType();
            var languagesProp = configType.GetProperty(
                "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            while (configType != null && languagesProp == null)
            {
                configType = configType.BaseType;
                languagesProp = configType.GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);
            }
            if (languagesProp == null)
                languagesProp = typeof(TranslateAPIConfig).GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            var supportedLanguages = (Dictionary<string, string>)languagesProp.GetValue(null);
            TargetLangBox.ItemsSource = supportedLanguages.Keys;

            string targetLang = Translator.Setting.TargetLanguage;
            if (!supportedLanguages.ContainsKey(targetLang))
                supportedLanguages[targetLang] = targetLang;
            TargetLangBox.SelectedItem = targetLang;
        }
    }
}
