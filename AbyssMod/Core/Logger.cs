namespace AbyssMod;

/// <summary>
/// 统一日志封装，避免各处直接引用 Plugin.Log。
/// </summary>
public static class Logger
{
    public static void Info(string msg) => Plugin.Log.LogInfo(msg);

    public static void Warn(string msg) => Plugin.Log.LogWarning(msg);

    public static void Error(string msg) => Plugin.Log.LogError(msg);
}
