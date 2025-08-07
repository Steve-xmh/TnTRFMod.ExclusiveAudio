namespace TnTRFMod.ExclusiveAudio;

public enum LogType
{
    Info,
    Warning,
    Error,
    Fatal,
    Message,
    Debug
}

internal class Logger
{
    public static void Info(object value)
    {
        Log(value);
    }

    public static void Warn(object value)
    {
        Log(value, LogType.Warning);
    }

    public static void Error(object value)
    {
        Log(value, LogType.Error);
    }

    public static void Message(object value)
    {
        Log(value, LogType.Message);
    }

    public static void Log(object value, LogType type = LogType.Info)
    {
        switch (type)
        {
            case LogType.Info:
                ExclusiveAudioPlugin.Log.LogInfo(value);
                break;
            case LogType.Warning:
                ExclusiveAudioPlugin.Log.LogWarning(value);
                break;
            case LogType.Error:
                ExclusiveAudioPlugin.Log.LogError(value);
                break;
            case LogType.Fatal:
                ExclusiveAudioPlugin.Log.LogFatal(value);
                break;
            case LogType.Message:
                ExclusiveAudioPlugin.Log.LogMessage(value);
                break;
            case LogType.Debug:
#if DEBUG
                ExclusiveAudioPlugin.Log.LogInfo(value);
#endif
                break;
        }

        Console.Out.Flush();
    }

    public static void Log(List<string> values, LogType type = LogType.Info)
    {
        if (values.Count == 0) return;
        var value = values[0];
        var numSpacing = "[Info   :".Length + Math.Max(ExclusiveAudioPlugin.ModName.Length, 10) + 2;
        var spacing = string.Empty;
        for (var i = 0; i < numSpacing; i++) spacing += " ";
        for (var i = 1; i < values.Count; i++)
        {
            value += "\n";
            value += spacing;
            value += values[i];
        }

        Log(value, type);
    }
}