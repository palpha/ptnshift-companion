namespace Core.Diagnostics;

public interface IDebugWriter
{
    event Action<string> DebugWritten;
    void Write(string message);
}

public class DebugWriter : IDebugWriter
{
    public event Action<string>? DebugWritten;

    public void Write(string message)
    {
        DebugWritten?.Invoke(message);
    }
}