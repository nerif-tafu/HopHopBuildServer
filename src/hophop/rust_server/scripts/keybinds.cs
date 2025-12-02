using Carbon.Base;

namespace Carbon.Plugins
{
    [Info("Keybinds", "HopHopBuildServer", "1.0.0")]
    [Description("Displays keybind instructions for server plugins")]
    public class Keybinds : CarbonPlugin
    {
        private void Init()
        {
            Puts("Keybinds plugin loaded!");
        }

        [ChatCommand("keybinds")]
        private void KeybindsCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            SendReply(player, "<color=yellow>=== Keybind Setup Instructions ===</color>");
            SendReply(player, "<color=white>1. Open console (F1)</color>");
            SendReply(player, "<color=white>2. Type: bind f freemovement.toggle</color>");
            SendReply(player, "<color=white>3. Type: bind x freemovement.tp</color>");
            SendReply(player, "<color=green>Done! Press F to toggle no-clip, X to teleport</color>");
        }
    }
}

