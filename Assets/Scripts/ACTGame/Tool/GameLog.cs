using UnityEngine;
using DeBug = UnityEngine.Debug;

public enum LogLevel
{
    Off = 0,
    Error = 1,
    Warn = 2,
    Info = 3,
    Debug = 4,
}

public static class LogConfig
{
    public static LogLevel GlobalLevel = LogLevel.Error;
    public static LogLevel OtherLevel = LogLevel.Error;
    public static LogLevel AssetBundleLevel = LogLevel.Warn;
    public static LogLevel CombatLevel = LogLevel.Info;
    public static LogLevel UILogLevel = LogLevel.Error;

    public static void ApplyReleasePreset()
    {
        GlobalLevel = LogLevel.Error;
        AssetBundleLevel = LogLevel.Error;
        CombatLevel = LogLevel.Error;
        UILogLevel = LogLevel.Error;
    }

    public static void ApplyDebugPreset()
    {
        GlobalLevel = LogLevel.Debug;
        AssetBundleLevel = LogLevel.Debug;
        CombatLevel = LogLevel.Info;
        UILogLevel = LogLevel.Warn;
    }
}

public static class GameLog
{
    private static bool IsEnabled(LogLevel moduleLevel, LogLevel msgLevel)
    {
        if (LogConfig.GlobalLevel == LogLevel.Off) return false;
        if (moduleLevel == LogLevel.Off) return false;
        if (msgLevel > LogConfig.GlobalLevel) return false;
        return msgLevel <= moduleLevel;
    }

    // AssetBundle 模块日志
    public static void ABInfo(string message)
    {
        if (!IsEnabled(LogConfig.AssetBundleLevel, LogLevel.Info)) return;
        DeBug.Log($"[AB] {message}");
    }

    public static void ABWarn(string message)
    {
        if (!IsEnabled(LogConfig.AssetBundleLevel, LogLevel.Warn)) return;
        DeBug.LogWarning($"[AB] {message}");
    }

    public static void ABError(string message)
    {
        if (!IsEnabled(LogConfig.AssetBundleLevel, LogLevel.Error)) return;
        DeBug.LogError($"[AB] {message}");
    }

    // Combat 模块
    public static void CombatDebug(string message)
    {
        if (!IsEnabled(LogConfig.CombatLevel, LogLevel.Debug)) return;
        DeBug.Log($"[Combat] {message}");
    }

    public static void CombatError(string message)
    {
        if (!IsEnabled(LogConfig.CombatLevel, LogLevel.Error)) return;
        DeBug.LogError($"[Combat] {message}");
    }

    // 通用日志接口，用于替代旧的 Log.Debug / Log.Error
    public static void Debug(string message)
    {
        // 默认归在 CombatLevel 控制下，你也可以改成单独的 EngineLevel
        CombatDebug(message);
    }

    public static void Error(string message)
    {
        CombatError(message);
    }

    public static void Error(System.Exception e)
    {
        if (!IsEnabled(LogConfig.CombatLevel, LogLevel.Error)) return;
        DeBug.LogError(e);
    }
}


