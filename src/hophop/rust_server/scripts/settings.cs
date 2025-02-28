using System;
using System.Collections.Generic;
using System.Linq;
using Carbon.Core;
using Oxide.Game.Rust.Libraries;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("Settings", "Assistant", "1.0.0")]
    [Description("Server settings management")]
    public class settings : CarbonPlugin
    {
        private static class UI
        {
            public static class Colors
            {
                public const string
                    CommandColor = "#87CEEB",  // Light Sky Blue - Commands
                    ErrorColor = "#FF6B6B",    // Soft Red - Errors
                    SuccessColor = "#98FB98",  // Pale Green - Success
                    InputColor = "#FFE66D";    // Light Yellow - Input values
                
                public static string Colorize(string text, string color) => $"<color={color}>{text}</color>";
                public static string FormatSuccess(string text) => Colorize($"✓ {text}", SuccessColor);
                public static string FormatError(string text) => Colorize(text, ErrorColor);
                public static string FormatCommand(string text) => Colorize(text, CommandColor);
                public static string FormatInput(string text) => Colorize(text, InputColor);
            }
        }

        private class SettingsData
        {
            public bool GlobalPower { get; set; }
        }

        private SettingsData _settings;

        private void LoadData()
        {
            _settings = Interface.Oxide.DataFileSystem.ReadObject<SettingsData>("server_settings") ?? new SettingsData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("server_settings", _settings);
        }

        private void OnServerInitialized()
        {
            LoadData();
            cmd.AddChatCommand("settings", this, nameof(OnSettingsCommand));

            // Apply settings on startup
            if (_settings.GlobalPower)
            {
                SetGlobalPower(true);
            }
        }

        private void OnSettingsCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, $"Usage: /settings {UI.Colors.FormatCommand("<setting>")} {UI.Colors.FormatCommand("<value>")}");
                SendReply(player, "Available settings:");
                SendReply(player, $"  {UI.Colors.FormatCommand("global_power")} {UI.Colors.FormatInput("<true/false>")} - Powers all electrical devices");
                return;
            }

            string setting = args[0].ToLower();
            
            switch (setting)
            {
                case "global_power":
                    if (args.Length < 2)
                    {
                        SendReply(player, $"Current global_power: {UI.Colors.FormatInput(_settings.GlobalPower.ToString())}");
                        return;
                    }

                    if (!bool.TryParse(args[1], out bool powerValue))
                    {
                        SendReply(player, $"{UI.Colors.FormatError("Error:")} Value must be true or false");
                        return;
                    }

                    _settings.GlobalPower = powerValue;
                    SaveData();
                    SetGlobalPower(powerValue);

                    SendReply(player, UI.Colors.FormatSuccess($"Set global_power to {powerValue}"));
                    break;

                default:
                    SendReply(player, $"{UI.Colors.FormatError("Error:")} Unknown setting '{setting}'");
                    break;
            }
        }

        private void SetGlobalPower(bool enabled)
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var ioEntity = entity as IOEntity;
                if (ioEntity != null)
                {
                    if (enabled)
                    {
                        // Set power for input and mark dirty
                        ioEntity.SetFlag(BaseEntity.Flags.Reserved8, true); // Power flag
                        ioEntity.SetFlag(IOEntity.Flag_HasPower, true);
                        foreach (var input in ioEntity.inputs)
                        {
                            input.value = 100; // Maximum power
                        }
                        ioEntity.MarkDirty();
                        
                        // Some entities need extra handling
                        var autoTurret = ioEntity as AutoTurret;
                        if (autoTurret != null)
                        {
                            autoTurret.SetFlag(AutoTurret.Flag_HasPower, true);
                        }
                    }
                    else
                    {
                        // Remove power
                        ioEntity.SetFlag(BaseEntity.Flags.Reserved8, false);
                        ioEntity.SetFlag(IOEntity.Flag_HasPower, false);
                        foreach (var input in ioEntity.inputs)
                        {
                            input.value = 0;
                        }
                        ioEntity.MarkDirty();

                        var autoTurret = ioEntity as AutoTurret;
                        if (autoTurret != null)
                        {
                            autoTurret.SetFlag(AutoTurret.Flag_HasPower, false);
                        }
                    }
                }
            }
        }
    }
} 