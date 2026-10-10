using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Absf.Master;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Project.Master;

namespace AbyssMod.Patches;

/// <summary>
/// 在 MasterData 单表写入缓存后立即替换静态文本。
/// 剧情正文脚本不在 masterdata 内，仍由 TranslationPatch 处理。
/// </summary>
[HarmonyPatch]
internal static class MasterDataPatch
{
    private const string DownloadCallbackPrefix = "_DownloadFirstAsync_b__";
    private static readonly Dictionary<
        string,
        (Il2CppSystem.Type Type, IMasterLoadResult Result)
    > Pending = new(StringComparer.Ordinal);
    private static bool _pendingTranslationRunning;

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        Type[] parameterTypes =
        {
            typeof(Il2CppSystem.Type),
            typeof(Il2CppSystem.Object),
            typeof(bool),
        };
        var method = AccessTools
            .GetDeclaredMethods(typeof(MasterDataStore))
            .SingleOrDefault(candidate =>
                candidate.Name.StartsWith(DownloadCallbackPrefix, StringComparison.Ordinal)
                && candidate.ReturnType == typeof(void)
                && candidate
                    .GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(parameterTypes)
            );

        return method
            ?? throw new MissingMethodException(
                typeof(MasterDataStore).FullName,
                $"{DownloadCallbackPrefix}*(Type, Object, Boolean)"
            );
    }

    [HarmonyPostfix]
    public static void TranslateCachedTable(
        MasterDataStore __instance,
        Il2CppSystem.Type elementType
    )
    {
        try
        {
            bool ready = Plugin.Trans.MasterDataTranslationReady;
            if (ready && !Plugin.Trans.HasMasterDataTranslation(elementType))
                return;

            var result = __instance._caches[elementType];
            if (ready)
            {
                Translate(elementType, result);
                return;
            }

            Pending[elementType.Name] = (elementType, result);
            if (!_pendingTranslationRunning)
            {
                _pendingTranslationRunning = true;
                try
                {
                    Plugin.Instance.StartCoroutine(TranslatePendingWhenReady().WrapToIl2Cpp());
                }
                catch
                {
                    _pendingTranslationRunning = false;
                    throw;
                }
            }
            else
                _ = Plugin.Trans.EnsureStaticTranslationsLoadedAsync();
        }
        catch (Exception e)
        {
            Logger.Error($"[MasterDataTranslation] failed [{elementType?.Name}]: {e}");
        }
    }

    private static IEnumerator TranslatePendingWhenReady()
    {
        Task loadTask;
        try
        {
            loadTask = Plugin.Trans.EnsureStaticTranslationsLoadedAsync();
        }
        catch (Exception e)
        {
            Logger.Error($"[MasterDataTranslation] deferred load failed: {e}");
            _pendingTranslationRunning = false;
            yield break;
        }

        while (!loadTask.IsCompleted)
            yield return null;

        try
        {
            try
            {
                loadTask.GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Logger.Error($"[MasterDataTranslation] deferred translation load failed: {e}");
            }

            if (!Plugin.Trans.MasterDataTranslationReady)
            {
                Logger.Warn(
                    $"[MasterDataTranslation] plan unavailable; waiting with cached tables: {Pending.Count}"
                );
                while (Pending.Count > 0 && !Plugin.Trans.MasterDataTranslationReady)
                    yield return null;
            }

            if (Pending.Count == 0)
                yield break;

            foreach (var (type, result) in Pending.Values)
                Translate(type, result);
            Pending.Clear();
        }
        finally
        {
            _pendingTranslationRunning = false;
        }
    }

    private static void Translate(Il2CppSystem.Type type, IMasterLoadResult result)
    {
        if (!Plugin.Trans.TryTranslateMasterDataCache(type, result, out int translatedEntries))
            return;

        if (translatedEntries > 0)
            Logger.Info($"[MDT] Translated {type.Name}: {translatedEntries} entries");
    }

    internal static void Reset()
    {
        Pending.Clear();
        _pendingTranslationRunning = false;
    }
}
