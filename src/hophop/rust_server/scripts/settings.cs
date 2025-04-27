using Carbon.Components;
using static Carbon.Components.CUI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Carbon.Modules;
using Carbon.Base;
using ImageDatabaseModule = Carbon.Modules.ImageDatabaseModule;

namespace Carbon.Plugins
{
    [Info("Settings", "nerif-tafu", "1.0.0")]
    [Description("Server settings management with CUI")]
    public class Settings : CarbonPlugin, IDisposable
    {
        private List<NavItem> navItems = new();
        private const string DefaultTextColor = "0.5 0.5 0.5 1";
        private const string DefaultBackgroundColor = "1 1 1 0";

        private void Init()
        {
            Puts("Plugin Loaded.");

            navItems = new()
            {
                new NavItem("BUILD", "build", 1),
                new NavItem("SAVES", "saves", 1),
                new NavItem("SKINS", "skins", 2),
                new NavItem("SPAWNABLE", "spawnable", 3),
                new NavItem("INDUSTRIAL", "industrial", 3),
                new NavItem("CLOSE", "close", 4),
                new NavItem("SETTINGS", "settings", 4),
            };

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null) continue;

                Puts($"Reinitializing UI for Player: {player.displayName}");
                CreateUI(player);
            }
        }

        private void CreateUI(BasePlayer player)
        {
            using var cui = new CUI(CuiHandler);

            cui.v2.CreateParent(CUI.ClientPanels.Overlay, LuiPosition.Full, "Main").SetDestroy("Main");
            cui.v2.CreateUrlImage("Main", LuiPosition.Full, LuiOffset.None, "https://i.imgur.com/Fm7P2r6.png", "1 1 1 1", "logo2");

            // Sidebar
            cui.v2.CreatePanel("Main", new LuiPosition(0, 0, 0.25f, 1), LuiOffset.None, "0 1 0 0", "settings-sidebar");
            cui.v2.CreatePanel("settings-sidebar", new LuiPosition(0, 0.8f, 1, 1), new LuiOffset(0, 0, 0, 1), "0 0 1 0", "settings-header");
            cui.v2.CreatePanel("settings-sidebar", new LuiPosition(0, 0, 1, 0.8f), LuiOffset.None, "0 0.5 1 0", "settings-nav");
            cui.v2.CreatePanel("settings-nav", LuiPosition.Full, new LuiOffset(74, 66, 0, 0), "0 1 1 0", "settings-nav__padding");

            int currentY = 0;
            int? lastGroup = null;

            foreach (var item in navItems.OrderByDescending(x => x.Group))
            {
                currentY = item.Create(cui, currentY, lastGroup);
                lastGroup = item.Group;
            }

            // Content area
            cui.v2.CreatePanel("Main", new LuiPosition(0.25f, 0, 1, 1), LuiOffset.None, "0 0 0 0.1", "settings-content");
            cui.v2.SendUi(player);
        }

        [ConsoleCommand("menuNav.select")]
        private void MenuNavSelect(ConsoleSystem.Arg args)
        {
            if (args.Connection?.player is not BasePlayer player || args.Args == null || args.Args.Length == 0)
                return;

            string selected = args.Args[0].ToUpperInvariant();
            using var cui = new CUI(CuiHandler);

            foreach (var item in navItems)
            {
                bool isSelected = item.Text.Equals(selected, StringComparison.OrdinalIgnoreCase);
                string textColor = isSelected ? "0.8 0.8 0.8 1" : DefaultTextColor;

                cui.v2.UpdateText(item.TextName, item.Text, 28, textColor);
            }

            cui.v2.SendUi(player);
            
            Puts($"Selected nav item: {selected}");
            // TODO: Show panel for each nav item chosen.
        }

        private class NavItem
        {
            public string Text { get; }
            public string Command { get; }
            public int Group { get; }
            public string BackgroundColor => DefaultBackgroundColor;
            public string TextColor => DefaultTextColor;

            private const int NavItemHeight = 38;
            private const int NavItemSpacing = 42;
            private const int NavGroupSpacing = 74;

            public string ButtonName => $"button_{Command.ToLower()}";
            public string TextName => $"{ButtonName}__text";

            public NavItem(string text, string command, int group)
            {
                Text = text;
                Command = $"menuNav.select {command}";
                Group = group;
            }

            public int Create(CUI cui, int currentY, int? lastGroup)
            {
                if (lastGroup.HasValue)
                    currentY += (lastGroup.Value != Group) ? NavGroupSpacing : NavItemSpacing;

                int yMin = currentY;
                int yMax = yMin + NavItemHeight;

                cui.v2.CreateButton(
                    "settings-nav__padding",
                    new LuiPosition(0, 0, 1, 0),
                    new LuiOffset(0, yMin, 0, yMax),
                    Command,
                    BackgroundColor,
                    false,
                    ButtonName
                );

                cui.v2.CreateText(
                    ButtonName,
                    LuiPosition.Full,
                    LuiOffset.None,
                    28,
                    TextColor,
                    Text,
                    TextAnchor.MiddleLeft,
                    TextName
                ).SetTextFont(Handler.FontTypes.RobotoCondensedBold);

                return currentY;
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.USE) || input.WasJustPressed(BUTTON.FIRE_THIRD))
            {
                using var cui = new CUI(CuiHandler);
                cui.v2.UpdateColor("settings-sidebar", "1 0 1 0.5");
                cui.v2.SendUi(player);
            }

            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                var imageDb = BaseModule.GetModule<ImageDatabaseModule>();
                Puts(imageDb.HasImage("logo2"));
            }

            if (input.WasJustPressed(BUTTON.RELOAD))
            {
                CUIStatics.Destroy("Main", player);
            }
        }
    }
}
