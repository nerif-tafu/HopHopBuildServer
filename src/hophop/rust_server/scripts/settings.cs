using Carbon.Components;
using static Carbon.Components.CUI;
using System;
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
        private void Init()
        {
            Puts("Plugin Loaded.");
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    Puts($"Reinitializing UI for Player: {player.displayName}");
                    using CUI _cui = new(CuiHandler);
                    _cui.v2.CreateParent(CUI.ClientPanels.Overlay, LuiPosition.Full, "Main").SetDestroy("Main");
                    _cui.v2.CreateUrlImage("Main", LuiPosition.Full, LuiOffset.None, "https://i.imgur.com/Fm7P2r6.png", "1 1 1 1", "logo2");

                    const int navHeight = 509;
                    const int navItemHeight = 37;

                    int CalcYMax(int yMin)
                    {
                        return -(navHeight - yMin - navItemHeight);
                    }
                    
                    // Settings navigation
                    _cui.v2.CreatePanel("Main", new LuiPosition(0, 0, .25f, 1), LuiOffset.None, "0 1 0 0", "settings-sidebar");
                    _cui.v2.CreatePanel("settings-sidebar", new LuiPosition(0, 0.8f, 1, 1), new LuiOffset(0, 0, 0, 1), "0 0 1 0", "settings-header");
                    _cui.v2.CreatePanel("settings-sidebar", new LuiPosition(0, 0, 1, 0.8f), LuiOffset.None, "0 0.5 1 0", "settings-nav");
                    _cui.v2.CreatePanel("settings-nav", new LuiPosition(0, 0, 1, 1), new LuiOffset(74, 66, 0, 0), "0 1 1 0", "settings-nav__padding");

                    // Distance between a nav item in a group is 42.
                    // Distance between a nav group is 74.
                    
                    _cui.v2.CreateButton("settings-nav__padding", new LuiPosition(0, 0, 1, 0),  new LuiOffset(0, 0, 0, 38), "testCommand1", "1 0 0 0", false, "button1");
                    _cui.v2.CreateText("button1", LuiPosition.Full, LuiOffset.None, 28, "0.4 0.4 0.4 1", "QUIT", TextAnchor.MiddleLeft, "button1__text").SetTextFont(Handler.FontTypes.RobotoCondensedBold);
                    
                    _cui.v2.CreateButton("settings-nav__padding", new LuiPosition(0, 0, 1, 1),  new LuiOffset(0, 42, 0, CalcYMax(42)), "testCommand2", "1 1 0 0.5", false, "button2");
                    _cui.v2.CreateText("button2", LuiPosition.Full, LuiOffset.None, 28, "0.8 0.8 0.8 1", "RESPAWN",TextAnchor.MiddleLeft, "button2__text").SetTextFont(Handler.FontTypes.RobotoCondensedBold);
                    
                    _cui.v2.CreateButton("settings-nav__padding", new LuiPosition(0, 0, 1, 1),  new LuiOffset(0, 116, 0, CalcYMax(116)), "testCommand3", "1 0 0 0.4", false, "button3");
                    _cui.v2.CreateText("button3", LuiPosition.Full, LuiOffset.None, 28, "0.8 0.8 0.8 1", "OPTIONS",TextAnchor.MiddleLeft, "button3__text").SetTextFont(Handler.FontTypes.RobotoCondensedBold);
                    
                    _cui.v2.CreateButton("settings-nav__padding", new LuiPosition(0, 0, 1, 1),  new LuiOffset(0, 190, 0, CalcYMax(190)), "testCommand4", "1 0 0 0.2", false, "button4");
                    _cui.v2.CreateText("button4", LuiPosition.Full, LuiOffset.None, 28, "0.8 0.8 0.8 1", "RUST+",TextAnchor.MiddleLeft, "button4__text").SetTextFont(Handler.FontTypes.RobotoCondensedBold);
                    
                    _cui.v2.CreateButton("settings-nav__padding", new LuiPosition(0, 0, 1, 1),  new LuiOffset(0, 232, 0, CalcYMax(232)), "testCommand5", "1 0 0 0.2", false, "button5");
                    _cui.v2.CreateText("button5", LuiPosition.Full, LuiOffset.None, 28, "0.8 0.8 0.8 1", "WORKSHOP",TextAnchor.MiddleLeft, "button5__text").SetTextFont(Handler.FontTypes.RobotoCondensedBold);
                    
                    _cui.v2.CreateButton("settings-nav__padding", new LuiPosition(0, 0, 1, 1),  new LuiOffset(0, 274, 0, CalcYMax(274)), "testCommand6", "1 0 0 0.2", false, "button6");
                    _cui.v2.CreateText("button6", LuiPosition.Full, LuiOffset.None, 28, "0.8 0.8 0.8 1", "ITEM STORE",TextAnchor.MiddleLeft, "button6__text").SetTextFont(Handler.FontTypes.RobotoCondensedBold);

                    // Settings content
                    _cui.v2.CreatePanel("Main", new LuiPosition(0.25f, 0, 1, 1), LuiOffset.None, "0 0 0 0.1", "settings-content");
                    _cui.v2.SendUi(player);
                }
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.USE))
            {
                Puts("Testing Use Button.");
                using CUI _cui = new(CuiHandler);
                _cui.v2.UpdateColor("settings-sidebar", "1 0 1 0.5");
                _cui.v2.SendUi(player);
            }

            if (input.WasJustPressed(BUTTON.FIRE_THIRD))
            {
                using CUI _cui = new(CuiHandler);
                _cui.v2.UpdateColor("settings-sidebar", "1 0 1 0.5");
                _cui.v2.SendUi(player);
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