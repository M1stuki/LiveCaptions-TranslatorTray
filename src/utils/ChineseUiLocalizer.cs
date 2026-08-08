using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace LiveCaptionsTranslator.Utils
{
    public static class ChineseUiLocalizer
    {
        private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
        {
            ["LiveCaptions Translator"] = "实时字幕翻译",
            ["Caption"] = "字幕",
            ["Setting"] = "设置",
            ["Settings"] = "设置",
            ["History"] = "历史记录",
            ["Info"] = "关于",
            ["Log Cards of Captions"] = "字幕历史卡片",
            ["Log Cards"] = "字幕历史卡片",
            ["Pause Translation (Log Only)"] = "暂停翻译",
            ["Overlay Window"] = "悬浮字幕窗口",
            ["Always on Top"] = "始终置顶",
            ["Click To Copy"] = "点击复制",
            ["Click to Copy, Ctrl+Scroll to Resize Font"] = "点击复制；Ctrl+滚轮调整字号",

            ["SettingPage"] = "设置",
            ["LiveCaptions"] = "Windows 实时字幕",
            ["Show"] = "显示",
            ["Hide"] = "隐藏",
            ["API Interval"] = "API 调用间隔",
            ["Translate API"] = "翻译 API",
            ["Target Language"] = "目标语言",
            ["API Setting"] = "API 设置",
            ["Open"] = "打开",
            ["Show Latency"] = "显示延迟",
            ["Off"] = "关",
            ["On"] = "开",
            ["Contexts"] = "上下文句数",
            ["Contexts:"] = "上下文句数：",
            ["Display Sentences"] = "悬浮字幕句数",
            ["Overlay Sentences:"] = "悬浮字幕句数：",
            ["Context Aware"] = "上下文翻译",

            ["Prompt"] = "提示词",
            ["Current Config: "] = "当前配置：",
            ["Current Config:"] = "当前配置：",
            ["New"] = "新建",
            ["Delete"] = "删除",
            ["Model Name"] = "模型名称",
            ["Temperature"] = "温度",
            ["API Url"] = "API 地址",
            ["API URL"] = "API 地址",
            ["API Key"] = "API 密钥",
            ["Secret Key"] = "密钥",
            ["App Key"] = "应用密钥",
            ["App ID"] = "应用 ID",
            ["Access Key"] = "访问密钥",
            ["Secret Access Key"] = "访问密钥密码",
            ["Region"] = "区域",
            ["Endpoint"] = "接口地址",
            ["Server URL"] = "服务器地址",
            ["Base URL"] = "基础地址",
            ["Host"] = "主机",
            ["Port"] = "端口",
            ["Token"] = "令牌",
            ["Account"] = "账号",
            ["Password"] = "密码",
            ["Client ID"] = "客户端 ID",
            ["Client Secret"] = "客户端密钥",
            ["Source Language"] = "源语言",
            ["Translation"] = "译文",
            ["Original"] = "原文",
            ["Original Text"] = "原文",
            ["Translated Text"] = "译文",
            ["Export"] = "导出",
            ["Clear"] = "清空",
            ["Search"] = "搜索",
            ["Save"] = "保存",
            ["Cancel"] = "取消",
            ["Close"] = "关闭",
            ["Update"] = "更新",
            ["Refresh"] = "刷新",
            ["Load Models"] = "加载模型",
            ["Ignore this version"] = "忽略此版本",
            ["New Version Available"] = "发现新版本",
            ["About"] = "关于",
            ["Version"] = "版本",
            ["Author"] = "作者",
            ["Source"] = "源代码",
            ["Previous"] = "上一页",
            ["Next"] = "下一页",

            ["Note 1:"] = "说明 1：",
            ["Note 2:"] = "说明 2：",
            ["Note:"] = "说明：",
            ["The source text is enclosed with 🔤."] = "源文本会用 🔤 包围。",
            ["You must keep at least one config."] = "至少需要保留一个配置。",
            ["Translate in context."] = "结合前文进行翻译。",
            ["It can improve translation accuracy, but will consume more tokens."] = "可以提高翻译准确性，但会消耗更多 Token。",
            ["Determines the frequency of translate API calls. The smaller it is, the more frequent API calls."] = "控制翻译 API 的调用频率。数值越小，调用越频繁。",
            ["The translate API is called once after the caption changes"] = "字幕内容每变化",
            ["times."] = "次后调用一次翻译 API。",
            ["Except for Google and Google2, all other APIs require configuring before they can be used."] = "除 Google 和 Google2 外，其他 API 使用前都需要先配置。",
            ["Determines the number of context sentences when"] = "决定启用上下文翻译时提供给模型的前文句数。",
            ["Determines the number of displayed cards when"] = "决定字幕历史卡片显示的句数，并限制悬浮字幕窗口最多显示的句数。",
            ["is enabled."] = "启用时。",
            ["is enabled, as well as the max number of sentences displayed in the Overlay Window."] = "启用时，同时也决定悬浮字幕窗口最多显示的句数。",
            ["Contexts must be"] = "上下文句数必须",
            ["greater than or equal"] = "大于或等于",
            ["Display Sentences. If not met, the program will automatically adjust them."] = "悬浮字幕句数；如果不满足，程序会自动调整。",
            ["No need to explicitly add"] = "无需手动添加",
            ["suffix."] = "后缀。",
            ["The {0} in the prompt indicates the target language, so make sure your prompt includes {0}."] = "提示词中的 {0} 代表目标语言，请确保提示词中包含 {0}。",
            ["After Windows 11 version 24H2, you can only change the"] = "Windows 11 24H2 之后，只能在实时字幕中修改",
            ["in LiveCaptions."] = "。",
            ["Please click"] = "请点击",
            ["to hide LiveCaptions instead of closing it directly."] = "来隐藏实时字幕，不要直接关闭它。",
            ["There isn’t the target language I expect!"] = "没有我想要的目标语言？",
            ["You can directly edit the content of this combobox to customize the language, and it is recommended to follow the"] = "可以直接编辑此下拉框内容来自定义语言，建议遵循",
            ["BCP 47 language tag."] = "BCP 47 语言标签。",
            ["Some of APIs (such as DeepL) needs another way to define target language, see their official docs for more details."] = "部分 API（例如 DeepL）使用不同的目标语言代码，详情请查看其官方文档。",
            ["No need to consider this for included target languages, since we've built in tag mappings. But if your expected language isn't in the list, keep this in mind."] = "列表中已有的语言已内置代码映射，无需处理；只有自定义列表外语言时才需要注意。",
            ["Please set the API URL first."] = "请先设置 API 地址。",
            ["No models found or unable to connect. Check that the server is running."] = "未找到模型或无法连接，请确认服务器正在运行。"
        };

        public static void Apply(DependencyObject root)
        {
            var visited = new HashSet<DependencyObject>();
            ApplyRecursive(root, visited);
        }

        private static void ApplyRecursive(DependencyObject root, HashSet<DependencyObject> visited)
        {
            if (!visited.Add(root))
                return;

            if (root is FrameworkElement element)
                ApplyElement(element);

            foreach (var child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyObject)
                    ApplyRecursive(dependencyObject, visited);
            }
        }

        public static void ApplyElement(FrameworkElement element)
        {
            if (element is Window window && !string.IsNullOrWhiteSpace(window.Title))
            {
                var translatedTitle = Translate(window.Title);
                if (translatedTitle != window.Title)
                    window.Title = translatedTitle;
            }

            if (element.ToolTip is string tooltip)
            {
                var translatedTooltip = Translate(tooltip);
                if (translatedTooltip != tooltip)
                    element.ToolTip = translatedTooltip;
            }

            if (element is TextBlock textBlock)
            {
                // Text= is common in WPF-UI TextBlock. Translate it directly first.
                if (!string.IsNullOrEmpty(textBlock.Text))
                {
                    var translatedText = Translate(textBlock.Text);
                    if (translatedText != textBlock.Text)
                        textBlock.Text = translatedText;
                }

                // Rich TextBlocks use individual Run elements; preserve their formatting.
                foreach (Inline inline in textBlock.Inlines)
                {
                    if (inline is Run run && !string.IsNullOrEmpty(run.Text))
                    {
                        var translated = Translate(run.Text);
                        if (translated != run.Text)
                            run.Text = translated;
                    }
                }
            }

            if (element is ContentControl contentControl && contentControl.Content is string content)
            {
                var translated = Translate(content);
                if (translated != content)
                    contentControl.Content = translated;
            }

            if (element is HeaderedContentControl headered && headered.Header is string header)
            {
                var translated = Translate(header);
                if (translated != header)
                    headered.Header = translated;
            }

            // WPF-UI ToggleSwitch exposes OnContent/OffContent rather than Content.
            TranslateStringProperty(element, "OnContent");
            TranslateStringProperty(element, "OffContent");
        }

        private static void TranslateStringProperty(FrameworkElement element, string propertyName)
        {
            try
            {
                var property = element.GetType().GetProperty(propertyName);
                if (property == null || !property.CanRead || !property.CanWrite)
                    return;

                var value = property.GetValue(element);
                if (value is string text)
                {
                    var translated = Translate(text);
                    if (translated != text)
                        property.SetValue(element, translated);
                }
            }
            catch
            {
                // Localization must never prevent the settings UI from opening.
            }
        }

        private static string Translate(string text)
        {
            if (Map.TryGetValue(text, out var translated))
                return translated;

            // Preserve accidental leading/trailing whitespace while translating the UI phrase itself.
            var trimmed = text.Trim();
            if (trimmed.Length != text.Length && Map.TryGetValue(trimmed, out translated))
            {
                var leading = text[..(text.Length - text.TrimStart().Length)];
                var trailing = text[(text.TrimEnd().Length)..];
                return leading + translated + trailing;
            }

            return text;
        }
    }
}
