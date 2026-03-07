namespace BetterSovereignBlade.Scripts.Debug;

using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Debug;


public static class ModConsole
{
    private static readonly object _lock = new();
    private static readonly Queue<string> _pending = new();

    public static void Print(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (_lock)
        {
            if (!TryGetConsole(out NDevConsole? console))
            {
                _pending.Enqueue(text);
                return;
            }

            while (_pending.Count > 0)
            {
                Send(console, _pending.Dequeue());
            }

            Send(console, text);
        }
    }

    private static bool TryGetConsole(out NDevConsole? console)
    {
        try
        {
            console = NDevConsole.Instance;
            return GodotObject.IsInstanceValid(console);
        }
        catch
        {
            console = null;
            return false;
        }
    }

    private static void Send(NDevConsole console, string text)
    {
        string oneLine = text.Replace("\r", "").Replace("\n", " \\n ");
        _ = console.ProcessNetCommand(null, $"mprint {oneLine}");
    }
}