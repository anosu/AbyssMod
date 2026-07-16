#if DEBUG
using Absf;
using Dmm.Games.Sdk;
using Dmm.Games.Sdk.Net.Unity;
using Dmm.Games.Sdk.Recibo.Api;
using HarmonyLib;
using Project;

namespace AbyssMod.Patches;

[HarmonyPatch]
public static class DebugPatch
{
    private static bool IsOffline => Config.OfflineStartup || Config.Offline.Value;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CommandLineArgs), nameof(CommandLineArgs.Parse))]
    public static bool SetLaunchArgs(CommandLineArgs __instance, ref bool __result)
    {
        if (IsOffline)
        {
            __instance.OpenId = "114514";
            __instance.AccessToken = "1919810";
            __result = true;
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RuntimeConfig), nameof(RuntimeConfig.GetApiUrl))]
    public static void SetApiUrl(ref string __result)
    {
        if (IsOffline)
            __result = Config.OfflineAPI.Value;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HttpClientUnityImpl), nameof(HttpClientUnityImpl.Request))]
    public static void SetDmmSdkUrl(ref string url)
    {
        if (IsOffline)
            url = url.Replace("https://", Config.DmmSdkAPI.Value);
    }
}
#endif
