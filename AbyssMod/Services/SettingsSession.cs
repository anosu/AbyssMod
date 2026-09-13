using System;

namespace AbyssMod.Services;

/// <summary>菜单草稿、批量保存和启动设置差异；不依赖 Unity UI，便于测试。</summary>
internal sealed class SettingsSession
{
    private readonly SettingsValues _startup = SettingsValues.Read();
    private SettingsValues _baseline;
    public SettingsValues Draft { get; set; }
    public string Status { get; private set; }
    public bool HasChanges => Draft != _baseline;
    public bool RestartRequired => RequiresRestart(SettingsValues.Read());

    public SettingsSession() => Begin();

    public void Begin()
    {
        Draft = _baseline = SettingsValues.Read();
        Status = RestartRequired
            ? "部分已保存设置与启动时不同，需要重启游戏生效。"
            : "修改后请点击「保存并应用」，关闭菜单不会自动保存。";
    }

    public void RestoreDefaults()
    {
        Draft = SettingsValues.Read(defaults: true);
        Status = "默认值已填入，尚未保存；点击「保存并应用」后生效。";
    }

    public void WarnUnsaved() => Status = "尚未保存。请先保存，或点击「放弃并关闭」。";

    public bool Apply()
    {
        var next = Draft.Normalize();
        string error = next.Validate();
        if (error != null)
        {
            Status = error;
            return false;
        }
        var previous = SettingsValues.Read();
        if (next == previous)
        {
            Draft = _baseline = previous;
            Status = "没有需要保存的修改。";
            return true;
        }

        bool saveOnSet = Plugin.ConfigFile.SaveOnConfigSet;
        bool suppressToasts = Config.SuppressSettingToasts;
        try
        {
            Plugin.ConfigFile.SaveOnConfigSet = false;
            Config.SuppressSettingToasts = true;
            next.Write();
            Plugin.ConfigFile.Save();
        }
        catch (Exception ex)
        {
            previous.Write();
            Logger.Error($"Settings menu save failed: {ex}");
            Status = "保存失败，未应用到当前游戏。请检查配置文件权限和日志后重试。";
            return false;
        }
        finally
        {
            Plugin.ConfigFile.SaveOnConfigSet = saveOnSet;
            Config.SuppressSettingToasts = suppressToasts;
        }

        Draft = _baseline = SettingsValues.Read();
        try
        {
            RuntimeSettings.ApplyChanges(previous, Draft);
            Status = RequiresRestart(Draft)
                ? "已保存。剧情、画面和声音设置已应用；资料、界面或资源设置需重启游戏。"
                : "已保存并应用。标注「下次载入」的选项会在对应场景重新载入时生效。";
        }
        catch (Exception ex)
        {
            Logger.Error($"Settings saved but runtime apply failed: {ex}");
            Status = "配置已保存，但即时应用失败；请重启游戏并查看日志。";
            return false;
        }
        return true;
    }

    public bool ReloadFromFile()
    {
        if (HasChanges)
        {
            WarnUnsaved();
            return false;
        }
        bool suppressToasts = Config.SuppressSettingToasts;
        try
        {
            Config.SuppressSettingToasts = true;
            RuntimeSettings.ReloadFromFile();
            Begin();
            Status = RestartRequired
                ? "已重读配置；资料、界面或资源设置仍需重启游戏生效。"
                : "已从配置文件重新读取并应用运行时设置。";
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Settings menu reload failed: {ex}");
            Status = "重读配置失败，请检查日志。";
            return false;
        }
        finally
        {
            Config.SuppressSettingToasts = suppressToasts;
        }
    }

    private bool RequiresRestart(SettingsValues value) =>
        value.Translation != Config.MasterDataTranslationEnabledAtStartup
        || value.UiTranslation != Config.UiTranslationEnabledAtStartup
        || value.Cdn != _startup.Cdn
        || value.Language != _startup.Language
        || value.CacheDirectory != _startup.CacheDirectory
        || value.FontBundlePath != _startup.FontBundlePath
        || value.PreferLocalFiles != _startup.PreferLocalFiles;
}
