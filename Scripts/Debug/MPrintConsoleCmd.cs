namespace BetterSovereignBlade.Scripts.Debug;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;


public sealed class MPrintConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "mprint";
    public override string Args => "<text...>";
    public override string Description => "Print mod message to in-game console.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false; // mod 环境下可用

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length == 0)
        {
            return new CmdResult(success: false, "Usage: mprint <text...>");
        }

        string message = string.Join(" ", args)
            .Replace("\\n", "\n")
            .Replace("[", "[lb]")  // 防止 BBCode 注入
            .Replace("]", "[rb]");

        return new CmdResult(success: true, $"[color=#4DB8FF][BetterSovereignBlade][/color] {message}");
    }
}