using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Automation;

using LiveCaptionsTranslator.apis;
using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator
{
    public static class Translator
    {
        private static AutomationElement? window = null;
        private static Caption? caption = null;
        private static Setting? setting = null;

        private static readonly ConcurrentQueue<string> pendingTextQueue = new();
        private static readonly TranslationTaskQueue translationTaskQueue = new();
        private static readonly object liveCaptionsLock = new();
        private static volatile bool engineEnabled = false;
        private static volatile bool liveCaptionsHidden = true;

        public static AutomationElement? Window
        {
            get => window;
            set => window = value;
        }
        public static Caption? Caption => caption;
        public static Setting? Setting => setting;
        public static bool EngineEnabled => engineEnabled;

        // This is deliberately independent from EngineEnabled. If the user chooses
        // "显示" for Windows Live Captions, stopping recognition may destroy the
        // process, but the preference survives and is re-applied on the next launch.
        public static bool LiveCaptionsHidden
        {
            get => liveCaptionsHidden;
            set
            {
                liveCaptionsHidden = value;
                ApplyLiveCaptionsVisibility();
            }
        }

        public static bool LogOnlyFlag { get; set; } = false;
        public static bool FirstUseFlag { get; set; } = false;

        public static event Action? TranslationLogged;

        static Translator()
        {
            // Loading the application must not start Windows Live Captions. Recognition is
            // expensive and should only run while the tray switch is enabled. The one
            // exception is first-use setup, where the original project needs the Live
            // Captions window so the user can configure its language.
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), models.Setting.FILENAME)))
                FirstUseFlag = true;

            caption = Caption.GetInstance();
            setting = Setting.Load();

            if (FirstUseFlag)
            {
                // Match the original first-run behavior: show Microsoft's Live Captions so
                // the recognition language can be configured, and remember that preference.
                liveCaptionsHidden = false;
                try
                {
                    window = LaunchAndPrepareLiveCaptions();
                }
                catch
                {
                    window = null;
                }
            }
        }

        public static void StartEngine()
        {
            lock (liveCaptionsLock)
            {
                if (engineEnabled && IsLiveCaptionsAlive(window))
                    return;

                if (!IsLiveCaptionsAlive(window))
                    window = LaunchAndPrepareLiveCaptions();
                else
                    ApplyLiveCaptionsVisibility();

                engineEnabled = true;
            }
        }

        public static void StopEngine()
        {
            AutomationElement? currentWindow;

            lock (liveCaptionsLock)
            {
                engineEnabled = false;
                currentWindow = window;
                window = null;
            }

            while (pendingTextQueue.TryDequeue(out _))
            {
            }

            translationTaskQueue.CancelAll();
            LiveCaptionsHandler.ResetCaptionCache();
            ClearContexts();

            if (caption != null)
            {
                caption.OriginalCaption = string.Empty;
                caption.DisplayOriginalCaption = string.Empty;
                caption.OverlayOriginalCaption = string.Empty;
                caption.TranslatedCaption = string.Empty;
                caption.DisplayTranslatedCaption = string.Empty;
                caption.OverlayNoticePrefix = string.Empty;
                caption.OverlayCurrentTranslation = string.Empty;
            }

            if (currentWindow != null)
            {
                try
                {
                    LiveCaptionsHandler.KillLiveCaptions(currentWindow);
                }
                catch
                {
                    // It may already have been closed by Windows or the user.
                }
            }
        }

        private static AutomationElement LaunchAndPrepareLiveCaptions()
        {
            var liveCaptionsWindow = LiveCaptionsHandler.LaunchLiveCaptions();
            LiveCaptionsHandler.FixLiveCaptions(liveCaptionsWindow);
            window = liveCaptionsWindow;
            ApplyLiveCaptionsVisibility();
            LiveCaptionsHandler.ResetCaptionCache();
            return liveCaptionsWindow;
        }

        public static void ApplyLiveCaptionsVisibility()
        {
            var currentWindow = window;
            if (!IsLiveCaptionsAlive(currentWindow))
                return;

            try
            {
                if (liveCaptionsHidden)
                    LiveCaptionsHandler.HideLiveCaptions(currentWindow!);
                else
                    LiveCaptionsHandler.RestoreLiveCaptions(currentWindow!);
            }
            catch (ElementNotAvailableException)
            {
                window = null;
                LiveCaptionsHandler.ResetCaptionCache();
            }
            catch (InvalidOperationException)
            {
                window = null;
                LiveCaptionsHandler.ResetCaptionCache();
            }
        }

        private static bool IsLiveCaptionsAlive(AutomationElement? candidate)
        {
            if (candidate == null)
                return false;

            try
            {
                _ = candidate.Current.ProcessId;
                return true;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static void RestartLiveCaptionsIfNeeded()
        {
            lock (liveCaptionsLock)
            {
                if (!engineEnabled || IsLiveCaptionsAlive(window))
                    return;

                window = LaunchAndPrepareLiveCaptions();
            }
        }

        public static void SyncLoop()
        {
            int idleCount = 0;
            int syncCount = 0;

            while (true)
            {
                if (!engineEnabled)
                {
                    Thread.Sleep(200);
                    continue;
                }

                var currentWindow = Window;
                if (currentWindow == null)
                {
                    Thread.Sleep(100);
                    continue;
                }

                string fullText = string.Empty;
                try
                {
                    var info = currentWindow.Current;
                    _ = info.Name;
                    fullText = LiveCaptionsHandler.GetCaptions(currentWindow);
                }
                catch (ElementNotAvailableException)
                {
                    Window = null;
                    LiveCaptionsHandler.ResetCaptionCache();
                    Thread.Sleep(100);
                    continue;
                }

                // The original loop immediately continued here. During silence that could
                // spin UI Automation as fast as the CPU allowed. Keep latency low while
                // speech is active, but back off when there is no caption text.
                if (string.IsNullOrEmpty(fullText))
                {
                    Thread.Sleep(50);
                    continue;
                }

                fullText = RegexPatterns.Acronym().Replace(fullText, "$1$2");
                fullText = RegexPatterns.AcronymWithWords().Replace(fullText, "$1 $2");
                fullText = RegexPatterns.PunctuationSpace().Replace(fullText, "$1 ");
                fullText = RegexPatterns.CJPunctuationSpace().Replace(fullText, "$1");
                fullText = TextUtil.ReplaceNewlines(fullText, TextUtil.MEDIUM_THRESHOLD);

                if (fullText.IndexOfAny(TextUtil.PUNC_EOS) == -1 && Caption.Contexts.Count > 0)
                    ClearContexts();

                int lastEOSIndex;
                if (Array.IndexOf(TextUtil.PUNC_EOS, fullText[^1]) != -1)
                    lastEOSIndex = fullText[0..^1].LastIndexOfAny(TextUtil.PUNC_EOS);
                else
                    lastEOSIndex = fullText.LastIndexOfAny(TextUtil.PUNC_EOS);
                string latestCaption = fullText.Substring(lastEOSIndex + 1);

                if (lastEOSIndex > 0 && Encoding.UTF8.GetByteCount(latestCaption) < TextUtil.SHORT_THRESHOLD)
                {
                    lastEOSIndex = fullText[0..lastEOSIndex].LastIndexOfAny(TextUtil.PUNC_EOS);
                    latestCaption = fullText.Substring(lastEOSIndex + 1);
                }

                Caption.OverlayOriginalCaption = latestCaption;
                for (int historyCount = Math.Min(Setting.DisplaySentences, Caption.Contexts.Count);
                     historyCount > 0 && lastEOSIndex > 0;
                     historyCount--)
                {
                    lastEOSIndex = fullText[0..lastEOSIndex].LastIndexOfAny(TextUtil.PUNC_EOS);
                    Caption.OverlayOriginalCaption = fullText.Substring(lastEOSIndex + 1);
                }

                if (string.CompareOrdinal(Caption.DisplayOriginalCaption, latestCaption) != 0)
                {
                    Caption.DisplayOriginalCaption = latestCaption;
                    Caption.DisplayOriginalCaption =
                        TextUtil.ShortenDisplaySentence(Caption.DisplayOriginalCaption, TextUtil.VERYLONG_THRESHOLD);
                }

                int lastEOS = latestCaption.LastIndexOfAny(TextUtil.PUNC_EOS);
                if (lastEOS != -1)
                    latestCaption = latestCaption.Substring(0, lastEOS + 1);

                if (string.CompareOrdinal(Caption.OriginalCaption, latestCaption) != 0)
                {
                    Caption.OriginalCaption = latestCaption;
                    idleCount = 0;

                    if (!string.IsNullOrEmpty(Caption.OriginalCaption))
                    {
                        if (Array.IndexOf(TextUtil.PUNC_EOS, Caption.OriginalCaption[^1]) != -1)
                        {
                            syncCount = 0;
                            pendingTextQueue.Enqueue(Caption.OriginalCaption);
                        }
                        else if (Encoding.UTF8.GetByteCount(Caption.OriginalCaption) >= TextUtil.SHORT_THRESHOLD)
                            syncCount++;
                    }
                }
                else
                {
                    idleCount++;
                }

                if (!string.IsNullOrEmpty(Caption.OriginalCaption) &&
                    (syncCount > Setting.MaxSyncInterval || idleCount == Setting.MaxIdleInterval))
                {
                    syncCount = 0;
                    pendingTextQueue.Enqueue(Caption.OriginalCaption);
                }

                Thread.Sleep(25);
            }
        }

        public static async Task TranslateLoop()
        {
            while (true)
            {
                if (!engineEnabled)
                {
                    await Task.Delay(200);
                    continue;
                }

                if (Window == null)
                {
                    Caption.DisplayTranslatedCaption = "[WARNING] LiveCaptions was unexpectedly closed, restarting...";
                    try
                    {
                        RestartLiveCaptionsIfNeeded();
                        Caption.DisplayTranslatedCaption = string.Empty;
                    }
                    catch
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                }

                if (pendingTextQueue.TryDequeue(out string? originalSnapshot) &&
                    !string.IsNullOrEmpty(originalSnapshot))
                {
                    if (LogOnlyFlag)
                    {
                        bool isOverwrite = await IsOverwrite(originalSnapshot);
                        await LogOnly(originalSnapshot, isOverwrite);
                    }
                    else
                    {
                        translationTaskQueue.Enqueue(token => Task.Run(
                            () => Translate(originalSnapshot, token), token), originalSnapshot);
                    }
                }

                await Task.Delay(40);
            }
        }

        public static async Task DisplayLoop()
        {
            bool idleDisplayCleared = false;

            while (true)
            {
                if (!engineEnabled)
                {
                    if (!idleDisplayCleared && Caption != null)
                    {
                        Caption.TranslatedCaption = string.Empty;
                        Caption.DisplayTranslatedCaption = string.Empty;
                        Caption.OverlayNoticePrefix = string.Empty;
                        Caption.OverlayCurrentTranslation = string.Empty;
                        idleDisplayCleared = true;
                    }

                    await Task.Delay(200);
                    continue;
                }

                idleDisplayCleared = false;
                var (translatedText, isChoke) = translationTaskQueue.Output;

                if (LogOnlyFlag)
                {
                    Caption.TranslatedCaption = string.Empty;
                    Caption.DisplayTranslatedCaption = "[Paused]";
                    Caption.OverlayNoticePrefix = "[Paused]";
                    Caption.OverlayCurrentTranslation = string.Empty;
                }
                else if (!string.IsNullOrEmpty(RegexPatterns.NoticePrefix().Replace(
                             translatedText, string.Empty).Trim()) &&
                         string.CompareOrdinal(Caption.TranslatedCaption, translatedText) != 0)
                {
                    Caption.TranslatedCaption = translatedText;
                    Caption.DisplayTranslatedCaption =
                        TextUtil.ShortenDisplaySentence(Caption.TranslatedCaption, TextUtil.VERYLONG_THRESHOLD);

                    if (Caption.TranslatedCaption.Contains("[ERROR]") || Caption.TranslatedCaption.Contains("[WARNING]"))
                        Caption.OverlayCurrentTranslation = Caption.TranslatedCaption;
                    else
                    {
                        var match = RegexPatterns.NoticePrefixAndTranslation().Match(Caption.TranslatedCaption);
                        Caption.OverlayNoticePrefix = match.Groups[1].Value.Trim();
                        Caption.OverlayCurrentTranslation = match.Groups[2].Value.Trim();
                    }
                }

                if (isChoke)
                    await Task.Delay(720);
                await Task.Delay(40);
            }
        }

        public static async Task<(string, bool)> Translate(string text, CancellationToken token = default)
        {
            string translatedText;
            bool isChoke = Array.IndexOf(TextUtil.PUNC_EOS, text[^1]) != -1;

            try
            {
                var sw = Setting.MainWindow.LatencyShow ? Stopwatch.StartNew() : null;

                if (Setting.ContextAware && !TranslateAPI.IsLLMBased)
                {
                    translatedText = await TranslateAPI.TranslateFunction($"{Caption.AwareContextsCaption} 🔤 {text} 🔤", token);
                    translatedText = RegexPatterns.TargetSentence().Match(translatedText).Groups[1].Value;
                }
                else
                {
                    translatedText = await TranslateAPI.TranslateFunction(text, token);
                    translatedText = translatedText.Replace("🔤", "");
                }

                if (sw != null)
                {
                    sw.Stop();
                    translatedText = $"[{sw.ElapsedMilliseconds,4} ms] " + translatedText;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ($"[ERROR] Translation Failed: {ex.Message}", isChoke);
            }

            return (translatedText, isChoke);
        }

        public static async Task Log(string originalText, string translatedText,
            bool isOverwrite = false, CancellationToken token = default)
        {
            string targetLanguage, apiName;
            if (Setting != null)
            {
                targetLanguage = Setting.TargetLanguage;
                apiName = Setting.ApiName;
            }
            else
            {
                targetLanguage = "N/A";
                apiName = "N/A";
            }

            try
            {
                if (isOverwrite)
                    await SQLiteHistoryLogger.DeleteLastTranslation(token);
                await SQLiteHistoryLogger.LogTranslation(originalText, translatedText, targetLanguage, apiName);
                TranslationLogged?.Invoke();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("[ERROR] Logging history failed.", ex.Message, SnackbarType.Error,
                    timeout: 2, closeButton: true);
            }
        }

        public static async Task LogOnly(string originalText,
            bool isOverwrite = false, CancellationToken token = default)
        {
            try
            {
                if (isOverwrite)
                    await SQLiteHistoryLogger.DeleteLastTranslation(token);
                await SQLiteHistoryLogger.LogTranslation(originalText, "N/A", "N/A", "LogOnly");
                TranslationLogged?.Invoke();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SnackbarHost.Show("[ERROR] Logging history failed.", ex.Message, SnackbarType.Error,
                    timeout: 2, closeButton: true);
            }
        }

        public static async Task AddContexts(CancellationToken token = default)
        {
            var lastLog = await SQLiteHistoryLogger.LoadLastTranslation(token);
            if (lastLog == null)
                return;

            if (Caption?.Contexts.Count >= Caption.MAX_CONTEXTS)
                Caption.Contexts.Dequeue();
            Caption?.Contexts.Enqueue(lastLog);

            Caption?.OnPropertyChanged("DisplayLogCards");
            Caption?.OnPropertyChanged("OverlayPreviousTranslation");
        }

        public static void ClearContexts()
        {
            Caption?.Contexts.Clear();

            Caption?.OnPropertyChanged("DisplayLogCards");
            Caption?.OnPropertyChanged("OverlayPreviousTranslation");
        }

        public static async Task<bool> IsOverwrite(string originalText, CancellationToken token = default)
        {
            string lastOriginalText = await SQLiteHistoryLogger.LoadLastSourceText(token);
            if (lastOriginalText == null)
                return false;

            int minLen = Math.Min(originalText.Length, lastOriginalText.Length);
            originalText = originalText.Substring(0, minLen);
            lastOriginalText = lastOriginalText.Substring(0, minLen);

            double similarity = TextUtil.Similarity(originalText, lastOriginalText);
            return similarity > TextUtil.SIM_THRESHOLD;
        }
    }
}
