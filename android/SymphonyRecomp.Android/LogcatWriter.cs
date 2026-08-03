using System.Text;
using Android.Util;

namespace SymphonyRecomp.Android;

// The runtime reports everything through Console.WriteLine / Console.Error, and
// on .NET for Android that goes nowhere visible by default. With no UI, logcat is
// the only channel a startup failure has, so both streams get routed here.
internal sealed class LogcatWriter : TextWriter
{
    const string Tag = "SymphonyRecomp";

    readonly LogPriority _priority;
    readonly StringBuilder _line = new();

    LogcatWriter(LogPriority priority) => _priority = priority;

    public override Encoding Encoding => Encoding.UTF8;

    public static void Install()
    {
        Console.SetOut(new LogcatWriter(LogPriority.Info));
        Console.SetError(new LogcatWriter(LogPriority.Error));
    }

    // Buffered to a newline: logcat is line-oriented and the runtime writes
    // plenty of partial lines via Write().
    public override void Write(char value)
    {
        if (value == '\n') Flush();
        else if (value != '\r') _line.Append(value);
    }

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        foreach (var c in value) Write(c);
    }

    public override void WriteLine(string? value)
    {
        Write(value);
        Flush();
    }

    public override void Flush()
    {
        if (_line.Length == 0) return;
        Log.WriteLine(_priority, Tag, _line.ToString());
        _line.Clear();
    }
}
