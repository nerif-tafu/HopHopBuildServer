using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    [Info("settings", "nerif-tafu", "1.0.0")]
    [Description("Simple UI toggle example")]
    public class settings : CarbonPlugin
   {
        private const string UiParent = "settings_ui";
        private readonly HashSet<ulong> _uiOpen = new HashSet<ulong>();

        #region UI Logic

        private bool HasUI(BasePlayer player) => _uiOpen.Contains(player.userID);
        private void MarkUIOpen(BasePlayer player) => _uiOpen.Add(player.userID);
        private void MarkUIClose(BasePlayer player) => _uiOpen.Remove(player.userID);

        private void CreateUI(BasePlayer player)
        {
            DestroyUI(player); // Clean start

            var cui = new CuiElementContainer();

            // Root Panel (parent)
            cui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.85" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true,
            }, "Overlay", UiParent);

            // Sidebar
            cui.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.25 1" }
            }, UiParent, "settings_sidebar");

            // Example Nav Buttons
            AddNavButton(cui, "settings_sidebar", "GENERAL", 0.85f);
            AddNavButton(cui, "settings_sidebar", "SPAWN", 0.75f);
            AddNavButton(cui, "settings_sidebar", "PERMISSIONS", 0.65f);
            AddNavButton(cui, "settings_sidebar", "BACK", 0.15f);

            // Right Content Area
            cui.Add(new CuiPanel
            {
                Image = { Color = "1 0.5 1 0.2" },
                RectTransform = { AnchorMin = "0.25 0", AnchorMax = "1 1" }
            }, UiParent, "settings_content");

            CuiHelper.AddUi(player, cui);
            MarkUIOpen(player);
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiParent);
            MarkUIClose(player);
        }

        private void AddNavButton(CuiElementContainer cui, string parent, string text, float yAnchor)
        {
            string name = $"btn_{text.ToLower()}";
            cui.Add(new CuiButton
            {
                Button = { Color = "0.8 0.2 0.2 1", Command = "", Close = "" },
                RectTransform = { AnchorMin = $"0 {yAnchor}", AnchorMax = $"1 {yAnchor + 0.08f}" },
                Text = { Text = text, FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, parent, name);
        }

        #endregion

        #region Hooks

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            if (input.WasJustPressed(BUTTON.FIRE_THIRD))
            {
                Puts(HasUI(player));
                if (HasUI(player))
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
                    DestroyUI(player);
                }
                else
                {
                    CreateUI(player);
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
                }
                    
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null) MarkUIClose(player);
        }

        #endregion
    }
}