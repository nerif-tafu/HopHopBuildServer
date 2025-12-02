using System;
using System.Collections.Generic;
using UnityEngine;
using Carbon.Base;
using ProtoBuf;

namespace Carbon.Plugins
{
    [Info("FreeMovement", "HopHopBuildServer", "1.0.0")]
    [Description("Free movement plugin with no-clip toggle, teleport to look direction, and marker teleportation")]
    public class FreeMovement : CarbonPlugin
    {
        private Dictionary<ulong, bool> _noClipStates = new Dictionary<ulong, bool>();

        private void Init()
        {
            Puts("FreeMovement plugin loaded!");
            Puts("Type /keybinds in chat to see setup instructions");
        }

        [ConsoleCommand("freemovement.toggle")]
        private void ToggleNoClipCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            ToggleNoClip(player);
        }

        [ConsoleCommand("freemovement.tp")]
        private void TeleportCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            TeleportToLookDirection(player);
        }

        private void ToggleNoClip(BasePlayer player)
        {
            if (player == null) return;

            if (_noClipStates.ContainsKey(player.userID))
            {
                _noClipStates[player.userID] = !_noClipStates[player.userID];
            }
            else
            {
                _noClipStates[player.userID] = true;
            }

            player.SendConsoleCommand("noclip");
        }

        private void TeleportToLookDirection(BasePlayer player)
        {
            if (player == null) return;

            RaycastHit hit;
            Ray ray = player.eyes.HeadRay();
            
            if (Physics.Raycast(ray, out hit, 1000f))
            {
                Vector3 targetPosition = hit.point;
                targetPosition.y += 0.5f;
                
                player.Teleport(targetPosition);
            }
            else
            {
                Vector3 targetPosition = ray.origin + ray.direction * 100f;
                player.Teleport(targetPosition);
            }
        }

        private void OnMapMarkerAdded(BasePlayer basePlayer, MapNote mapNote)
        {
            if (basePlayer == null || mapNote == null) return;

            Vector3 markerPosition = mapNote.worldPosition;
            
            float groundY = GetGroundPosition(markerPosition);
            markerPosition.y = groundY;

            basePlayer.Teleport(markerPosition);
            basePlayer.RemoveFromTriggers();
            basePlayer.ForceUpdateTriggers();
        }

        private float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hitInfo;

            if (Physics.Raycast(
                new Vector3(pos.x, pos.y + 200f, pos.z),
                Vector3.down,
                out hitInfo,
                float.MaxValue,
                (Rust.Layers.Mask.Vehicle_Large | Rust.Layers.Solid | Rust.Layers.Mask.Water)))
            {
                var cargoShip = hitInfo.GetEntity() as CargoShip;
                if (cargoShip != null)
                {
                    return hitInfo.point.y;
                }

                return Mathf.Max(hitInfo.point.y, y);
            }

            return y;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null && _noClipStates.ContainsKey(player.userID))
            {
                _noClipStates.Remove(player.userID);
            }
        }

        private void Unload()
        {
            _noClipStates.Clear();
        }
    }
}

