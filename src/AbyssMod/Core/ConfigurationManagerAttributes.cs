namespace AbyssMod;

// ConfigurationManager 按类型名和字段名读取 ConfigDescription.Tags，无需引用其程序集。
// 仅保留本项目使用的字段，声明与目标分支的模板一致：
// https://github.com/anosu/BepInEx.ConfigurationManager/blob/master/ConfigurationManagerAttributes.cs
internal sealed class ConfigurationManagerAttributes
{
    public string DispName;
    public int? Order;
    public bool? IsAdvanced;
    public bool? ShowRangeAsPercent;
}
