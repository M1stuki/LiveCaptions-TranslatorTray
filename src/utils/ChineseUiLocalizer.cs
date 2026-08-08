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
            ["History"] = "历史记录",
            ["Info"] = "关于",
            ["Log Cards of Captions"] = "字幕历史卡片",
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
            ["Display Sentences"] = "悬浮字幕句数",
            ["Context Aware"] = "上下文翻译",

            ["Prompt"] = "提示词",
            ["Current Config: "] = "当前配置：",
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
            ["Ignore this version"] = "忽略此版本",
            ["New Version Available"] = "发现新版本",

            ["Note 1:"] = "说明 1：",
            ["Note 2:"] = "说明 2：",
            ["Note:"] = "说明：",
            ["The source text is enclosed with 🔤."] = "源文本会用 🔤 包围。",
            ["You must keep at least one config."] = "至少需要保留一个配置。",
            ["Translate in context."] = "结合前文进行翻译。",
            ["It can improve translation accuracy, but will consume more tokens."] = "可以提高翻译准确性，但会消耗更多 Token。",
            ["Determines the frequency of translate API calls. The smaller it is, the more frequent API calls."] = "控制翻译 API 的调用频率。数值越小，调用越频繁。",
            ["Except for Google and Google2, all other APIs require configuring before they can be used."] = "除 Google 和 Google2 外，其他 API 使用前都需要先配置。",
            ["Determines the number of context sentences when"] = "启用上下文翻译时，决定提供给模型的前文句数。",
            ["Determines the number of displayed cards when"] = "决定字幕历史卡片及悬浮窗口最多显示的句数。",
            ["greater than or equal"] = "大于或等于",
            ["No need to explicitly add"] = "无需手动添加",
            ["suffix."] = "后缀。",
            ["The {0} in the prompt indicates the target language, so make sure your prompt includes {0}."] = "提示词中的 {0} 代表目标语言，请确保提示词中包含 {0}。",
            ["After Windows 11 version 24H2, you can only change the"] = "Windows 11 24H2 之后，只能在实时字幕中修改",
            ["in LiveCaptions."] = "。",
            ["Please click"] = "请点击",
            ["to hide LiveCaptions instead of closing it directly."] = "来隐藏实时字幕，不要直接关闭它。"
        };

        public static void Apply(DependencyObject root)
        {
            if (root is FrameworkElement element)
                ApplyElement(element);

            foreach (var child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyObject)
                    Apply(dependencyObject);
            }
        }

        public static void ApplyElement(FrameworkElement element)
        {
            if (element is Window window && !string.IsNullOrWhiteSpace(window.Title))
                window.Title = Translate(window.Title);

            if (element.ToolTip is string tooltip)
                element.ToolTip = Translate(tooltip);

            if (element is TextBlock textBlock)
            {
                if (!string.IsNullOrWhiteSpace(textBlock.Text))
                    textBlock.Text = Translate(textBlock.Text);

                foreach (Inline inline in textBlock.Inlines)
                {
                    if (inline is Run run && !string.IsNullOrEmpty(run.Text))
                        run.Text = Translate(run.Text);
                }
            }

            if (element is ContentControl contentControl && contentControl.Content is string content)
                contentControl.Content = Translate(content);

            if (element is HeaderedContentControl headered && headered.Header is string header)
                headered.Header = Translate(header);
        }

        private static string Translate(string text)
        {
            if (Map.TryGetValue(text, out var translated))
                return translated;
            return text;
        }
    }
}
