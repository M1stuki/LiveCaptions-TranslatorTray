$ErrorActionPreference = 'Stop'

$path = Join-Path $PSScriptRoot '..\src\windows\SettingWindow.xaml'
$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

# Build-time only localization. Translate exact display literals instead of
# walking/modifying the WPF logical tree at runtime. Keeping the upstream
# initialization path intact avoids the API-settings crash.
$replacements = @(
    @('Title="API Setting"', 'Title="API 设置"'),
    @('>API Setting</ui:TextBlock>', '>API 设置</ui:TextBlock>'),
    @('Text="API Setting"', 'Text="API 设置"'),
    @('Text="Prompt"', 'Text="提示词"'),
    @('Text="Note 1:"', 'Text="说明 1："'),
    @('Text="&#x0A;Note 2:"', 'Text="&#x0A;说明 2："'),
    @('Text="The {0} in the prompt indicates the target language, so make sure your prompt includes {0}."', 'Text="提示词中的 {0} 代表目标语言，请确保提示词中包含 {0}。"'),
    @('Text="The source text is enclosed with 🔤."', 'Text="源文本会用 🔤 包围。"'),
    @('Text="Current Config: "', 'Text="当前配置："'),
    @('Content="New"', 'Content="新建"'),
    @('Content="Delete"', 'Content="删除"'),
    @('Text="You must keep at least one config."', 'Text="至少需要保留一个配置。"'),
    @('Text="Model Name"', 'Text="模型名称"'),
    @('Text="Temperature"', 'Text="温度"'),
    @('Text="API Url"', 'Text="API 地址"'),
    @('Text="API Url (Base)"', 'Text="API 基础地址"'),
    @('Text="API URL"', 'Text="API 地址"'),
    @('Text="API Key"', 'Text="API 密钥"'),
    @('Text="APP Key"', 'Text="应用密钥"'),
    @('Text="APP Secret"', 'Text="应用密钥"'),
    @('Text="APP ID"', 'Text="应用 ID"'),
    @('Text="Source Language"', 'Text="源语言"'),
    @('Content="Load Models"', 'Content="加载模型"'),
    @('Text="No need to explicitly add"', 'Text="无需手动添加"'),
    @('Text="suffix."', 'Text="后缀。"'),
    @('Text="Base URL ending with"', 'Text="基础地址以"'),
    @('Text=". Chat endpoint and models are appended automatically."', 'Text=" 结尾。聊天接口和模型接口会自动拼接。"'),
    @('Text="Use Full Url (typically ending with"', 'Text="请使用完整地址（通常以"'),
    @('Text=") instead of Base Url (typically ending with just"', 'Text="）而不是基础地址（通常仅以"'),
    @('Text=")."', 'Text="）结尾。"')
)

foreach ($pair in $replacements) {
    $text = $text.Replace([string]$pair[0], [string]$pair[1])
}

# Write UTF-8 without BOM; the WPF XAML compiler handles it consistently.
[System.IO.File]::WriteAllText(
    $path,
    $text,
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host 'API settings XAML localized to Simplified Chinese for this build.'
