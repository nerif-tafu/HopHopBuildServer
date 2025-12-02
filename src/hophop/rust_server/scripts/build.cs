using Carbon.Components;
using static Carbon.Components.CUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Carbon.Base;
using ProtoBuf;
using Facepunch;

namespace Carbon.Plugins
{
    [Info("Build", "HopHopBuildServer", "1.0.0")]
    [Description("Build helper with infinite resources, grade selection, entity deletion, and instant crafting")]
    public class Build : CarbonPlugin
    {
        private Dictionary<ulong, BuildSettings> _playerSettings = new Dictionary<ulong, BuildSettings>();
        private HashSet<ulong> _uiOpen = new HashSet<ulong>();
        private HashSet<ulong> _activeInfinitePlayers = new HashSet<ulong>();
        private Dictionary<ulong, string> _activePanel = new Dictionary<ulong, string>(); // Track which panel is active for each player
        private Dictionary<ulong, string> _lastActivePanel = new Dictionary<ulong, string>(); // Remember last active panel when UI closes
        private Dictionary<ulong, int> _savesPage = new Dictionary<ulong, int>(); // Track current page for saves panel

        private const int _itemStartPosition = 24;
        private readonly string[] _resourceItemShortnames = { "wood", "stones", "metal.fragments", "metal.refined" };
        private readonly List<Item> _resourceItems = new List<Item>();
        
        private const string _savesDirectory = "build_saves";
        private Dictionary<ulong, List<BaseEntity>> _lastPastedEntities = new Dictionary<ulong, List<BaseEntity>>();
        private HashSet<ulong> _operationInProgress = new HashSet<ulong>(); // Track players with ongoing save/load operations

        private class BuildSettings
        {
            public bool InfiniteResources { get; set; } = false;
            public BuildingGrade.Enum BuildGrade { get; set; } = BuildingGrade.Enum.Twigs;
            public ulong BuildSkin { get; set; } = 0;
            public bool InstantCrafting { get; set; } = false;
        }

        private class BlockInfo
        {
            public string Name { get; set; }
            public string ImageUrl { get; set; }
            public ulong SkinId { get; set; }

            public BlockInfo(string name, string imageUrl, ulong skinId)
            {
                Name = name;
                ImageUrl = imageUrl;
                SkinId = skinId;
            }
        }

        private Dictionary<int, List<BlockInfo>> BuildingImages = new Dictionary<int, List<BlockInfo>>
        {
            [0] = new List<BlockInfo>
            {
                new("Wood", "https://i.ibb.co/yqsWpbp/wood.png", 0),
                new("Frontier", "https://i.ibb.co/b2bZFXj/frontier.png", 10232),
                new("Gingerbread", "https://i.ibb.co/Tw67yBM/gingerbread.png", 2)
            },
            [1] = new List<BlockInfo>
            {
                new("Stone", "https://i.ibb.co/jw9FJFP/stone.png", 0),
                new("Adobe", "https://i.ibb.co/Ky1MBJ7/adobe.png", 10220),
                new("Brick", "https://i.ibb.co/vjqh3Hj/brick.png", 10223),
                new("Brutalist", "https://i.ibb.co/86bpvS2/brutalist.png", 10225)
            },
            [2] = new List<BlockInfo>
            {
                new("Metal", "https://i.ibb.co/M9RPSZ2/metal.png", 0),
                new("Container", "https://i.ibb.co/YWzfwS4/container.png", 10221)
            },
            [3] = new List<BlockInfo>
            {
                new("TopTier", "https://i.ibb.co/T0Nwfvp/toptire.png", 0)
            }
        };

        private void Init()
        {
            Puts("Build plugin loaded!");
            InitializeResourceItems();
            EnsureSavesDirectory();
        }
        
        private void EnsureSavesDirectory()
        {
            // Carbon stores data in rust_server/carbon/data
            string dataDir = Path.Combine(ConVar.Server.rootFolder, "carbon", "data");
            string savesPath = Path.Combine(dataDir, _savesDirectory);
            if (!Directory.Exists(savesPath))
            {
                Directory.CreateDirectory(savesPath);
            }
        }
        
        private string GetSavesDirectory()
        {
            string dataDir = Path.Combine(ConVar.Server.rootFolder, "carbon", "data");
            return Path.Combine(dataDir, _savesDirectory);
        }

        private void InitializeResourceItems()
        {
            _resourceItems.Clear();
            int position = _itemStartPosition;
            foreach (string shortname in _resourceItemShortnames)
            {
                Item item = ItemManager.CreateByName(shortname, 10000);
                if (item == null)
                {
                    continue;
                }
                item.position = position++;
                _resourceItems.Add(item);
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null) return;

            // Open GUI on FIRE_THIRD
            if (input.WasJustPressed(BUTTON.FIRE_THIRD))
            {
                ToggleGUI(player);
            }

            // Kill entity on R (RELOAD) when hammer or building plan is equipped
            if (input.WasJustPressed(BUTTON.RELOAD))
            {
                var activeItem = player.GetActiveItem();
                if (activeItem != null)
                {
                    var itemName = activeItem.info.shortname;
                    if (itemName == "hammer" || itemName == "planbuilding")
                    {
                        KillEntityLookingAt(player);
                    }
                }
            }
        }

        private void ToggleGUI(BasePlayer player)
        {
            if (player == null) return;

            // Check if GUI is already open
            if (_uiOpen.Contains(player.userID))
            {
                CloseGUI(player);
            }
            else
            {
                OpenGUI(player);
            }
        }

        private void OpenGUI(BasePlayer player)
        {
            if (!_playerSettings.ContainsKey(player.userID))
            {
                _playerSettings[player.userID] = new BuildSettings();
            }

            var settings = _playerSettings[player.userID];

            using var cui = new CUI(CuiHandler);

            // Main parent
            cui.v2.CreateParent(CUI.ClientPanels.Overlay, LuiPosition.Full, "BuildMain").SetDestroy("BuildMain");
            cui.v2.CreateUrlImage("BuildMain", LuiPosition.Full, LuiOffset.None, "https://i.imgur.com/Fm7P2r6.png", "1 1 1 1", "build-background");
            
            // Enable cursor and keyboard, disable movement
            cui.v2.CreateEmptyContainer("BuildMain", "build-cursor-container", true)
                .AddCursor()
                .AddKeyboard();
            
            // Sidebar
            cui.v2.CreatePanel("BuildMain", new LuiPosition(0, 0, 0.25f, 1), LuiOffset.None, "0.2 0.2 0.2 0.9", "build-sidebar");
            cui.v2.CreatePanel("build-sidebar", new LuiPosition(0, 0.8f, 1, 1), new LuiOffset(0, 0, 0, 1), "1 1 1 0", "build-header");
            cui.v2.CreatePanel("build-sidebar", new LuiPosition(0, 0, 1, 0.8f), LuiOffset.None, "1 1 1 0", "build-nav");
            cui.v2.CreatePanel("build-nav", LuiPosition.Full, new LuiOffset(74, 66, 0, 0), "1 1 1 0", "build-nav__padding");

            // Navigation items
            int currentY = 0;
            int? lastGroup = null;
            
            // Determine which panel to show
            string panelToShow = _lastActivePanel.ContainsKey(player.userID) ? _lastActivePanel[player.userID] : "BUILD";
            
            var navItems = new[]
            {
                new { Text = "BUILD", Command = "build.nav.build", Group = 1 },
                new { Text = "SAVES", Command = "build.nav.saves", Group = 1 },
                new { Text = "SKINS", Command = "build.nav.skins", Group = 2 },
                new { Text = "SPAWNABLE", Command = "build.nav.spawnable", Group = 3 },
                new { Text = "INDUSTRIAL", Command = "build.nav.industrial", Group = 3 },
                new { Text = "CLOSE", Command = "build.close", Group = 4 },
                new { Text = "SETTINGS", Command = "build.settings", Group = 4 }
            };

            foreach (var item in navItems.OrderByDescending(x => x.Group))
            {
                bool isSelected = item.Text.Equals(panelToShow, StringComparison.OrdinalIgnoreCase);
                currentY = CreateNavItem(cui, "build-nav__padding", item.Text, item.Command, currentY, item.Group, lastGroup, isSelected);
                lastGroup = item.Group;
            }

            // Content area container
            cui.v2.CreatePanel("BuildMain", new LuiPosition(0.25f, 0, 1, 1), LuiOffset.None, "0 0 0 0.1", "build-content-container");
            
            // Show last active panel or BUILD by default - only create the active panel
            ShowPanel(cui, player, panelToShow);

            cui.v2.SendUi(player);
            _uiOpen.Add(player.userID);
        }

        private int CreateNavItem(CUI cui, string parent, string text, string command, int currentY, int group, int? lastGroup, bool isSelected = false)
        {
            const int NavItemHeight = 38;
            const int NavItemSpacing = 42;
            const int NavGroupSpacing = 74;
            const string DefaultTextColor = "0.5 0.5 0.5 1";
            const string SelectedTextColor = "0.8 0.8 0.8 1";
            const string DefaultBackgroundColor = "1 1 1 0";

            if (lastGroup.HasValue)
                currentY += (lastGroup.Value != group) ? NavGroupSpacing : NavItemSpacing;

            int yMin = currentY;
            int yMax = yMin + NavItemHeight;
            string buttonName = $"button_{command.Replace(".", "_")}";
            string textName = $"{buttonName}__text";
            string textColor = isSelected ? SelectedTextColor : DefaultTextColor;

            cui.v2.CreateButton(
                parent,
                new LuiPosition(0, 0, 1, 0),
                new LuiOffset(0, yMin, 0, yMax),
                command,
                DefaultBackgroundColor,
                false,
                buttonName
            );

            cui.v2.CreateText(
                buttonName,
                LuiPosition.Full,
                LuiOffset.None,
                28,
                textColor,
                text,
                TextAnchor.MiddleLeft,
                textName
            ).SetTextFont(CUI.Handler.FontTypes.RobotoCondensedBold);

            return currentY;
        }

        private void CreateBuildPanel(CUI cui, BuildSettings settings)
        {
            // BUILD Panel
            cui.v2.CreatePanel("build-content-container", LuiPosition.Full, LuiOffset.None, "1 1 1 0", "panel-build");
            
            // Title
            cui.v2.CreateText("panel-build", new LuiPosition(0, 0.85f, 1, 1), LuiOffset.None, 24, "1 1 1 1", "Build Settings", TextAnchor.MiddleCenter, "BuildTitle")
                .SetTextFont(CUI.Handler.FontTypes.RobotoCondensedBold);

            // Infinite Resources Toggle
            string infiniteColor = settings.InfiniteResources ? "0.2 0.8 0.2 1" : "0.5 0.5 0.5 1";
            cui.v2.CreateButton("panel-build", new LuiPosition(0.1f, 0.65f, 0.9f, 0.75f), LuiOffset.None, "build.toggle.infinite", infiniteColor, false, "InfiniteButton");
            cui.v2.CreateText("InfiniteButton", LuiPosition.Full, LuiOffset.None, 18, "1 1 1 1", $"Infinite Resources: {(settings.InfiniteResources ? "ON" : "OFF")}", TextAnchor.MiddleCenter, "InfiniteText");

            // Build Grade Selection
            cui.v2.CreateText("panel-build", new LuiPosition(0.1f, 0.5f, 0.9f, 0.6f), LuiOffset.None, 18, "1 1 1 1", $"Build Grade: {settings.BuildGrade}", TextAnchor.MiddleLeft, "GradeLabel");

            // Grade buttons - map BuildingGrade.Enum to our grade index
            int gradeIndex = GetGradeIndex(settings.BuildGrade);
            CreateGradeButton(cui, "panel-build", "Twigs", BuildingGrade.Enum.Twigs, settings.BuildGrade, 0.1f, 0.4f, 0.25f, 0.48f);
            CreateGradeButton(cui, "panel-build", "Wood", BuildingGrade.Enum.Wood, settings.BuildGrade, 0.27f, 0.4f, 0.42f, 0.48f);
            CreateGradeButton(cui, "panel-build", "Stone", BuildingGrade.Enum.Stone, settings.BuildGrade, 0.44f, 0.4f, 0.59f, 0.48f);
            CreateGradeButton(cui, "panel-build", "Metal", BuildingGrade.Enum.Metal, settings.BuildGrade, 0.61f, 0.4f, 0.76f, 0.48f);
            CreateGradeButton(cui, "panel-build", "HQM", BuildingGrade.Enum.TopTier, settings.BuildGrade, 0.78f, 0.4f, 0.9f, 0.48f);

            // Skin Selection (only show if grade has skins available)
            if (BuildingImages.ContainsKey(gradeIndex) && BuildingImages[gradeIndex].Count > 1)
            {
                cui.v2.CreateText("panel-build", new LuiPosition(0.1f, 0.3f, 0.9f, 0.38f), LuiOffset.None, 18, "1 1 1 1", "Build Skin:", TextAnchor.MiddleLeft, "SkinLabel");
                
                // Create skin buttons
                var skins = BuildingImages[gradeIndex];
                float buttonWidth = 0.8f / skins.Count;
                float startX = 0.1f;
                
                for (int i = 0; i < skins.Count; i++)
                {
                    var skin = skins[i];
                    bool isSelected = settings.BuildSkin == skin.SkinId;
                    float xMin = startX + (i * buttonWidth);
                    float xMax = xMin + buttonWidth - 0.02f;
                    CreateSkinButton(cui, "panel-build", skin.Name, skin.SkinId, settings.BuildSkin, xMin, 0.2f, xMax, 0.28f);
                }
            }
        }

        private int GetGradeIndex(BuildingGrade.Enum grade)
        {
            switch (grade)
            {
                case BuildingGrade.Enum.Twigs:
                    return -1; // No skins for twigs
                case BuildingGrade.Enum.Wood:
                    return 0;
                case BuildingGrade.Enum.Stone:
                    return 1;
                case BuildingGrade.Enum.Metal:
                    return 2;
                case BuildingGrade.Enum.TopTier:
                    return 3;
                default:
                    return 0;
            }
        }

        private void CreateSkinButton(CUI cui, string parent, string label, ulong skinId, ulong currentSkinId, float xMin, float yMin, float xMax, float yMax)
        {
            string color = skinId == currentSkinId ? "0.2 0.5 0.8 1" : "0.3 0.3 0.3 1";
            string command = $"build.setskin {skinId}";
            cui.v2.CreateButton(parent, new LuiPosition(xMin, yMin, xMax, yMax), LuiOffset.None, command, color, false, $"SkinButton_{skinId}");
            cui.v2.CreateText($"SkinButton_{skinId}", LuiPosition.Full, LuiOffset.None, 12, "1 1 1 1", label, TextAnchor.MiddleCenter, $"SkinText_{skinId}");
        }

        private void CreateSavesPanel(CUI cui, BasePlayer player)
        {
            if (player == null) return;
            
            const int itemsPerPage = 7; // Number of saves to show per page
            
            cui.v2.CreatePanel("build-content-container", LuiPosition.Full, LuiOffset.None, "1 1 1 0", "panel-saves");
            cui.v2.CreateText("panel-saves", new LuiPosition(0, 0.85f, 1, 1), LuiOffset.None, 24, "1 1 1 1", "Saves", TextAnchor.MiddleCenter, "SavesTitle")
                .SetTextFont(CUI.Handler.FontTypes.RobotoCondensedBold);
            
            // Save input field and button
            cui.v2.CreateText("panel-saves", new LuiPosition(0.1f, 0.75f, 0.4f, 0.8f), LuiOffset.None, 16, "1 1 1 1", "Save Name:", TextAnchor.MiddleLeft, "SaveNameLabel");
            cui.v2.CreateInput("panel-saves", new LuiPosition(0.1f, 0.7f, 0.4f, 0.75f), LuiOffset.None, "0.2 0.2 0.2 0.8", "", 16, "build.save", 50, true, CUI.Handler.FontTypes.RobotoCondensedBold, TextAnchor.MiddleLeft, "SaveNameInput");
            cui.v2.CreateButton("panel-saves", new LuiPosition(0.42f, 0.7f, 0.6f, 0.75f), LuiOffset.None, "build.save.current", "0.2 0.8 0.2 1", false, "SaveButton");
            cui.v2.CreateText("SaveButton", LuiPosition.Full, LuiOffset.None, 16, "1 1 1 1", "Save Buildings", TextAnchor.MiddleCenter, "SaveButtonText");
            
            // Load saved builds list
            cui.v2.CreateText("panel-saves", new LuiPosition(0.1f, 0.65f, 0.9f, 0.7f), LuiOffset.None, 18, "1 1 1 1", "Saved Builds:", TextAnchor.MiddleLeft, "SavedBuildsLabel");
            
            // Get list of saved builds
            var savedBuilds = GetSavedBuilds();
            
            // Initialize page if needed
            if (!_savesPage.ContainsKey(player.userID))
            {
                _savesPage[player.userID] = 0;
            }
            
            int currentPage = _savesPage[player.userID];
            int totalPages = (int)Math.Ceiling(savedBuilds.Count / (double)itemsPerPage);
            
            // Ensure current page is valid
            if (currentPage >= totalPages && totalPages > 0)
            {
                currentPage = totalPages - 1;
                _savesPage[player.userID] = currentPage;
            }
            if (currentPage < 0)
            {
                currentPage = 0;
                _savesPage[player.userID] = 0;
            }
            
            // Calculate which items to show
            int startIndex = currentPage * itemsPerPage;
            int endIndex = Math.Min(startIndex + itemsPerPage, savedBuilds.Count);
            
            float yPos = 0.6f;
            float itemHeight = 0.05f;
            float spacing = 0.01f;
            
            // Display saves for current page
            for (int i = startIndex; i < endIndex; i++)
            {
                if (yPos < 0.15f) break; // Stop if we run out of space (leave room for pagination)
                
                var saveName = savedBuilds[i];
                
                // Save name and buttons
                cui.v2.CreatePanel("panel-saves", new LuiPosition(0.1f, yPos - itemHeight, 0.9f, yPos), LuiOffset.None, "0.15 0.15 0.15 0.8", $"SaveItem_{saveName}");
                cui.v2.CreateText($"SaveItem_{saveName}", new LuiPosition(0.02f, 0, 0.5f, 1), LuiOffset.None, 14, "1 1 1 1", saveName, TextAnchor.MiddleLeft, $"SaveName_{saveName}");
                
                // Load button
                cui.v2.CreateButton($"SaveItem_{saveName}", new LuiPosition(0.52f, 0.1f, 0.7f, 0.9f), LuiOffset.None, $"build.load {saveName}", "0.2 0.5 0.8 1", false, $"LoadButton_{saveName}");
                cui.v2.CreateText($"LoadButton_{saveName}", LuiPosition.Full, LuiOffset.None, 12, "1 1 1 1", "LOAD", TextAnchor.MiddleCenter, $"LoadText_{saveName}");
                
                // Delete button
                cui.v2.CreateButton($"SaveItem_{saveName}", new LuiPosition(0.72f, 0.1f, 0.9f, 0.9f), LuiOffset.None, $"build.delete {saveName}", "0.8 0.2 0.2 1", false, $"DeleteButton_{saveName}");
                cui.v2.CreateText($"DeleteButton_{saveName}", LuiPosition.Full, LuiOffset.None, 12, "1 1 1 1", "DELETE", TextAnchor.MiddleCenter, $"DeleteText_{saveName}");
                
                yPos -= (itemHeight + spacing);
            }
            
            // Pagination controls
            if (totalPages > 1)
            {
                // Page info
                cui.v2.CreateText("panel-saves", new LuiPosition(0.1f, 0.12f, 0.5f, 0.16f), LuiOffset.None, 14, "0.8 0.8 0.8 1", $"Page {currentPage + 1} of {totalPages}", TextAnchor.MiddleLeft, "PageInfo");
                
                // Previous button
                string prevColor = currentPage > 0 ? "0.3 0.5 0.8 1" : "0.2 0.2 0.2 0.5";
                cui.v2.CreateButton("panel-saves", new LuiPosition(0.52f, 0.12f, 0.65f, 0.16f), LuiOffset.None, "build.saves.page.prev", prevColor, false, "PrevPageButton");
                cui.v2.CreateText("PrevPageButton", LuiPosition.Full, LuiOffset.None, 14, "1 1 1 1", "Previous", TextAnchor.MiddleCenter, "PrevPageText");
                
                // Next button
                string nextColor = currentPage < totalPages - 1 ? "0.3 0.5 0.8 1" : "0.2 0.2 0.2 0.5";
                cui.v2.CreateButton("panel-saves", new LuiPosition(0.67f, 0.12f, 0.8f, 0.16f), LuiOffset.None, "build.saves.page.next", nextColor, false, "NextPageButton");
                cui.v2.CreateText("NextPageButton", LuiPosition.Full, LuiOffset.None, 14, "1 1 1 1", "Next", TextAnchor.MiddleCenter, "NextPageText");
            }
            
            if (savedBuilds.Count == 0)
            {
                cui.v2.CreateText("panel-saves", new LuiPosition(0.1f, 0.4f, 0.9f, 0.5f), LuiOffset.None, 16, "0.6 0.6 0.6 1", "No saved builds. Save one using the input above!", TextAnchor.MiddleCenter, "NoSavesText");
            }
        }
        
        private List<string> GetSavedBuilds()
        {
            var saves = new List<string>();
            
            try
            {
                // Get files from Carbon's data directory
                string dataDir = Path.Combine(ConVar.Server.rootFolder, "carbon", "data", _savesDirectory);
                
                if (Directory.Exists(dataDir))
                {
                    var files = Directory.GetFiles(dataDir, "*.json");
                    foreach (var file in files)
                    {
                        saves.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error getting saved builds: {ex}");
            }
            
            saves.Sort();
            return saves;
        }

        private void CreateSkinsPanel(CUI cui)
        {
            cui.v2.CreatePanel("build-content-container", LuiPosition.Full, LuiOffset.None, "1 1 1 0", "panel-skins");
            cui.v2.CreateText("panel-skins", new LuiPosition(0, 0.85f, 1, 1), LuiOffset.None, 24, "1 1 1 1", "Skins", TextAnchor.MiddleCenter, "SkinsTitle")
                .SetTextFont(CUI.Handler.FontTypes.RobotoCondensedBold);
            cui.v2.CreateText("panel-skins", new LuiPosition(0.1f, 0.5f, 0.9f, 0.7f), LuiOffset.None, 18, "0.8 0.8 0.8 1", "Skins panel - Coming soon", TextAnchor.MiddleCenter, "SkinsText");
        }

        private void CreateSpawnablePanel(CUI cui)
        {
            cui.v2.CreatePanel("build-content-container", LuiPosition.Full, LuiOffset.None, "1 1 1 0", "panel-spawnable");
            cui.v2.CreateText("panel-spawnable", new LuiPosition(0, 0.85f, 1, 1), LuiOffset.None, 24, "1 1 1 1", "Spawnable", TextAnchor.MiddleCenter, "SpawnableTitle")
                .SetTextFont(CUI.Handler.FontTypes.RobotoCondensedBold);
            cui.v2.CreateText("panel-spawnable", new LuiPosition(0.1f, 0.5f, 0.9f, 0.7f), LuiOffset.None, 18, "0.8 0.8 0.8 1", "Spawnable panel - Coming soon", TextAnchor.MiddleCenter, "SpawnableText");
        }

        private void CreateIndustrialPanel(CUI cui)
        {
            cui.v2.CreatePanel("build-content-container", LuiPosition.Full, LuiOffset.None, "1 1 1 0", "panel-industrial");
            cui.v2.CreateText("panel-industrial", new LuiPosition(0, 0.85f, 1, 1), LuiOffset.None, 24, "1 1 1 1", "Industrial", TextAnchor.MiddleCenter, "IndustrialTitle")
                .SetTextFont(CUI.Handler.FontTypes.RobotoCondensedBold);
            cui.v2.CreateText("panel-industrial", new LuiPosition(0.1f, 0.5f, 0.9f, 0.9f), LuiOffset.None, 18, "0.8 0.8 0.8 1", "Industrial panel - Coming soon", TextAnchor.MiddleCenter, "IndustrialText");
        }

        private void CreateSettingsPanel(CUI cui, BasePlayer player)
        {
            if (player == null) return;
            
            if (!_playerSettings.ContainsKey(player.userID))
            {
                _playerSettings[player.userID] = new BuildSettings();
            }
            var settings = _playerSettings[player.userID];
            
            cui.v2.CreatePanel("build-content-container", LuiPosition.Full, LuiOffset.None, "1 1 1 0", "panel-settings");
            cui.v2.CreateText("panel-settings", new LuiPosition(0, 0.85f, 1, 1), LuiOffset.None, 24, "1 1 1 1", "Settings", TextAnchor.MiddleCenter, "SettingsTitle")
                .SetTextFont(CUI.Handler.FontTypes.RobotoCondensedBold);
            
            // Instant Crafting Toggle
            string instantCraftingColor = settings.InstantCrafting ? "0.2 0.8 0.2 1" : "0.5 0.5 0.5 1";
            cui.v2.CreateButton("panel-settings", new LuiPosition(0.1f, 0.65f, 0.9f, 0.75f), LuiOffset.None, "build.toggle.instantcrafting", instantCraftingColor, false, "InstantCraftingButton");
            cui.v2.CreateText("InstantCraftingButton", LuiPosition.Full, LuiOffset.None, 18, "1 1 1 1", $"Instant Crafting: {(settings.InstantCrafting ? "ON" : "OFF")}", TextAnchor.MiddleCenter, "InstantCraftingText");
        }

        private void ShowPanel(CUI cui, BasePlayer player, string panelName)
        {
            if (player == null) return;

            // Destroy the content container to remove all existing panels
            CUIStatics.Destroy("build-content-container", player);
            
            // Recreate content area container
            cui.v2.CreatePanel("BuildMain", new LuiPosition(0.25f, 0, 1, 1), LuiOffset.None, "0 0 0 0.1", "build-content-container");
            
            // Get settings for BUILD panel
            if (!_playerSettings.ContainsKey(player.userID))
            {
                _playerSettings[player.userID] = new BuildSettings();
            }
            var settings = _playerSettings[player.userID];

            // Create only the active panel
            string panelId = $"panel-{panelName.ToLowerInvariant()}";
            if (panelId == "panel-build")
            {
                CreateBuildPanel(cui, settings);
            }
            else if (panelId == "panel-saves")
            {
                CreateSavesPanel(cui, player);
            }
            else if (panelId == "panel-skins")
            {
                CreateSkinsPanel(cui);
            }
            else if (panelId == "panel-spawnable")
            {
                CreateSpawnablePanel(cui);
            }
            else if (panelId == "panel-industrial")
            {
                CreateIndustrialPanel(cui);
            }
            else if (panelId == "panel-settings")
            {
                CreateSettingsPanel(cui, player);
            }

            _activePanel[player.userID] = panelName;
            _lastActivePanel[player.userID] = panelName; // Remember for next time
            UpdateNavSelection(player, panelName);
            cui.v2.SendUi(player);
        }

        private void CreateGradeButton(CUI cui, string parent, string label, BuildingGrade.Enum grade, BuildingGrade.Enum currentGrade, float xMin, float yMin, float xMax, float yMax)
        {
            string color = grade == currentGrade ? "0.2 0.5 0.8 1" : "0.3 0.3 0.3 1";
            string command = $"build.setgrade {(int)grade}";
            cui.v2.CreateButton(parent, new LuiPosition(xMin, yMin, xMax, yMax), LuiOffset.None, command, color, false, $"GradeButton_{grade}");
            cui.v2.CreateText($"GradeButton_{grade}", LuiPosition.Full, LuiOffset.None, 14, "1 1 1 1", label, TextAnchor.MiddleCenter, $"GradeText_{grade}");
        }

        private void UpdateGUI(BasePlayer player)
        {
            if (player == null || !_uiOpen.Contains(player.userID)) return;
            if (!_playerSettings.ContainsKey(player.userID)) return;

            var settings = _playerSettings[player.userID];

            using var cui = new CUI(CuiHandler);

            // Make sure BUILD panel is visible
            if (!_activePanel.ContainsKey(player.userID) || _activePanel[player.userID] != "BUILD")
            {
                ShowPanel(cui, player, "BUILD");
            }

            // Update infinite resources button color and text
            string infiniteColor = settings.InfiniteResources ? "0.2 0.8 0.2 1" : "0.5 0.5 0.5 1";
            cui.v2.UpdateColor("InfiniteButton", infiniteColor);
            cui.v2.UpdateText("InfiniteText", $"Infinite Resources: {(settings.InfiniteResources ? "ON" : "OFF")}", 18, "1 1 1 1");

            // Update grade label
            cui.v2.UpdateText("GradeLabel", $"Build Grade: {settings.BuildGrade}", 18, "1 1 1 1");

            // Update grade buttons
            UpdateGradeButton(cui, BuildingGrade.Enum.Twigs, settings.BuildGrade);
            UpdateGradeButton(cui, BuildingGrade.Enum.Wood, settings.BuildGrade);
            UpdateGradeButton(cui, BuildingGrade.Enum.Stone, settings.BuildGrade);
            UpdateGradeButton(cui, BuildingGrade.Enum.Metal, settings.BuildGrade);
            UpdateGradeButton(cui, BuildingGrade.Enum.TopTier, settings.BuildGrade);

            cui.v2.SendUi(player);
        }

        private void UpdateGradeButton(CUI cui, BuildingGrade.Enum grade, BuildingGrade.Enum currentGrade)
        {
            string color = grade == currentGrade ? "0.2 0.5 0.8 1" : "0.3 0.3 0.3 1";
            cui.v2.UpdateColor($"GradeButton_{grade}", color);
        }

        private void CloseGUI(BasePlayer player)
        {
            // Remember the last active panel before closing
            if (player != null && _activePanel.ContainsKey(player.userID))
            {
                _lastActivePanel[player.userID] = _activePanel[player.userID];
            }
            
            CUIStatics.Destroy("BuildMain", player);
            _uiOpen.Remove(player.userID);
            _activePanel.Remove(player.userID);
        }

        [ConsoleCommand("build.settings")]
        private void SettingsCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "SETTINGS");
            }
        }

        [ConsoleCommand("build.toggle.instantcrafting")]
        private void ToggleInstantCraftingCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;

            if (!_playerSettings.ContainsKey(player.userID))
            {
                _playerSettings[player.userID] = new BuildSettings();
            }

            _playerSettings[player.userID].InstantCrafting = !_playerSettings[player.userID].InstantCrafting;
            
            // Refresh settings panel
            using var cui = new CUI(CuiHandler);
            ShowPanel(cui, player, "SETTINGS");
        }

        [ConsoleCommand("build.nav.build")]
        private void NavBuildCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "BUILD");
            }
        }

        [ConsoleCommand("build.nav.saves")]
        private void NavSavesCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "SAVES");
            }
        }

        [ConsoleCommand("build.saves.page.prev")]
        private void SavesPagePrevCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            
            if (!_savesPage.ContainsKey(player.userID))
            {
                _savesPage[player.userID] = 0;
            }
            
            if (_savesPage[player.userID] > 0)
            {
                _savesPage[player.userID]--;
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "SAVES");
            }
        }

        [ConsoleCommand("build.saves.page.next")]
        private void SavesPageNextCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            
            var savedBuilds = GetSavedBuilds();
            const int itemsPerPage = 7;
            int totalPages = (int)Math.Ceiling(savedBuilds.Count / (double)itemsPerPage);
            
            if (!_savesPage.ContainsKey(player.userID))
            {
                _savesPage[player.userID] = 0;
            }
            
            if (_savesPage[player.userID] < totalPages - 1)
            {
                _savesPage[player.userID]++;
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "SAVES");
            }
        }

        [ConsoleCommand("build.nav.skins")]
        private void NavSkinsCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "SKINS");
            }
        }

        [ConsoleCommand("build.nav.spawnable")]
        private void NavSpawnableCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "SPAWNABLE");
            }
        }

        [ConsoleCommand("build.nav.industrial")]
        private void NavIndustrialCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "INDUSTRIAL");
            }
        }

        private void UpdateNavSelection(BasePlayer player, string selected)
        {
            if (player == null || !_uiOpen.Contains(player.userID)) return;

            using var cui = new CUI(CuiHandler);

            var navItems = new[]
            {
                new { Text = "BUILD", Command = "build.nav.build" },
                new { Text = "SAVES", Command = "build.nav.saves" },
                new { Text = "SKINS", Command = "build.nav.skins" },
                new { Text = "SPAWNABLE", Command = "build.nav.spawnable" },
                new { Text = "INDUSTRIAL", Command = "build.nav.industrial" },
                new { Text = "CLOSE", Command = "build.close" },
                new { Text = "SETTINGS", Command = "build.settings" }
            };

            const string DefaultTextColor = "0.5 0.5 0.5 1";
            const string SelectedTextColor = "0.8 0.8 0.8 1";

            foreach (var item in navItems)
            {
                bool isSelected = item.Text.Equals(selected, StringComparison.OrdinalIgnoreCase);
                string textColor = isSelected ? SelectedTextColor : DefaultTextColor;
                string buttonName = $"button_{item.Command.Replace(".", "_")}";
                string textName = $"{buttonName}__text";

                cui.v2.UpdateText(textName, item.Text, 28, textColor);
            }

            cui.v2.SendUi(player);
        }

        [ConsoleCommand("build.close")]
        private void CloseGUICommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player != null)
            {
                CloseGUI(player);
            }
        }

        [ConsoleCommand("build.toggle.infinite")]
        private void ToggleInfiniteCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;

            if (!_playerSettings.ContainsKey(player.userID))
            {
                _playerSettings[player.userID] = new BuildSettings();
            }

            _playerSettings[player.userID].InfiniteResources = !_playerSettings[player.userID].InfiniteResources;
            
            // Update active infinite players set
            if (_playerSettings[player.userID].InfiniteResources)
            {
                _activeInfinitePlayers.Add(player.userID);
            }
            else
            {
                _activeInfinitePlayers.Remove(player.userID);
            }
            
            // Refresh inventory to update hidden resources
            if (player.inventory != null)
            {
                player.inventory.SendUpdatedInventory(PlayerInventory.Type.Main, player.inventory.containerMain);
            }
            
            UpdateGUI(player); // Update GUI instead of recreating
        }

        [ConsoleCommand("build.setgrade")]
        private void SetGradeCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;

            if (arg.Args == null || arg.Args.Length == 0) return;

            if (int.TryParse(arg.Args[0], out int gradeInt))
            {
                if (!_playerSettings.ContainsKey(player.userID))
                {
                    _playerSettings[player.userID] = new BuildSettings();
                }

                _playerSettings[player.userID].BuildGrade = (BuildingGrade.Enum)gradeInt;
                
                // Reset skin to default (0) when grade changes
                int gradeIndex = GetGradeIndex((BuildingGrade.Enum)gradeInt);
                if (BuildingImages.ContainsKey(gradeIndex) && BuildingImages[gradeIndex].Count > 0)
                {
                    _playerSettings[player.userID].BuildSkin = BuildingImages[gradeIndex][0].SkinId;
                }
                else
                {
                    _playerSettings[player.userID].BuildSkin = 0;
                }
                
                // Recreate the panel to show new skin options
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "BUILD");
            }
        }

        [ConsoleCommand("build.setskin")]
        private void SetSkinCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;

            if (arg.Args == null || arg.Args.Length == 0) return;

            if (ulong.TryParse(arg.Args[0], out ulong skinId))
            {
                if (!_playerSettings.ContainsKey(player.userID))
                {
                    _playerSettings[player.userID] = new BuildSettings();
                }

                _playerSettings[player.userID].BuildSkin = skinId;
                
                // Update the GUI to reflect the new skin selection
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "BUILD");
            }
        }

        private void KillEntityLookingAt(BasePlayer player)
        {
            if (player == null) return;

            RaycastHit hit;
            Ray ray = player.eyes.HeadRay();

            if (Physics.Raycast(ray, out hit, 50f))
            {
                var entity = hit.GetEntity();
                if (entity != null && !(entity is BasePlayer))
                {
                    entity.Kill();
                }
            }
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null) return;
            
            var buildingBlock = go.GetComponent<BuildingBlock>();
            if (buildingBlock == null) return;
            
            var player = plan.GetOwnerPlayer();
            if (player == null || !_playerSettings.ContainsKey(player.userID)) return;

            var settings = _playerSettings[player.userID];
            
            // Set the grade and skin after a short delay to ensure the block is fully initialized
            timer.Once(0.1f, () =>
            {
                if (buildingBlock != null && !buildingBlock.IsDestroyed)
                {
                    SetBuildingBlockGrade(buildingBlock, settings.BuildGrade, settings.BuildSkin);
                }
            });
        }

        private object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (player == null || !_playerSettings.ContainsKey(player.userID)) return null;

            var settings = _playerSettings[player.userID];
            
            // Override the grade and skin using the helper method
            if (block != null)
            {
                SetBuildingBlockGrade(block, settings.BuildGrade, settings.BuildSkin);
            }

            return null;
        }

        private void SetBuildingBlockGrade(BuildingBlock buildingBlock, BuildingGrade.Enum targetGrade, ulong skinId = 0)
        {
            if (buildingBlock == null || buildingBlock.IsDestroyed) return;
            
            buildingBlock.skinID = skinId; // Use the selected skin
            buildingBlock.SetGrade(targetGrade);
            buildingBlock.SetHealthToMax();
            buildingBlock.StartBeingRotatable();
            buildingBlock.SendNetworkUpdate();
            buildingBlock.UpdateSkin();
            buildingBlock.ResetUpkeepTime();
            buildingBlock.UpdateSurroundingEntities();
            BuildingManager.server.GetBuilding(buildingBlock.buildingID)?.Dirty();
            if (targetGrade > BuildingGrade.Enum.Twigs)
            {
                Effect.server.Run("assets/bundled/prefabs/fx/build/promote_" + targetGrade.ToString().ToLower() + ".prefab", buildingBlock, 0u, Vector3.zero, Vector3.zero);
            }
        }

        private void OnEntitySaved(BasePlayer player, BaseNetworkable.SaveInfo saveInfo)
        {
            if (player == null || !_activeInfinitePlayers.Contains(player.userID)) return;
            if (saveInfo.msg == null) return;

            if (saveInfo.msg.basePlayer != null && saveInfo.msg.basePlayer.inventory != null && saveInfo.msg.basePlayer.inventory.invMain != null)
            {
                AddItems(saveInfo.msg.basePlayer.inventory.invMain);
            }
        }

        private void OnInventoryNetworkUpdate(PlayerInventory inventory, ItemContainer container, ProtoBuf.UpdateItemContainer updatedItemContainer, PlayerInventory.Type inventoryType, bool sendToEveryone)
        {
            if (inventory?.baseEntity == null) return;
            
            var player = inventory.baseEntity as BasePlayer;
            if (player == null || !_activeInfinitePlayers.Contains(player.userID)) return;

            if (inventoryType == PlayerInventory.Type.Main && updatedItemContainer?.container != null && updatedItemContainer.container.Count > 0)
            {
                AddItems(updatedItemContainer.container[0]);
            }
        }

        private void AddItems(ProtoBuf.ItemContainer containerData)
        {
            if (containerData == null) return;

            List<Item> items = GetItems();
            
            foreach (Item item in items)
            {
                containerData.contents.Add(item.Save());
            }
            
            containerData.slots = _itemStartPosition + items.Count;
        }

        private List<Item> GetItems()
        {
            if (_resourceItems.Count > 0)
            {
                return _resourceItems;
            }

            InitializeResourceItems();
            return _resourceItems;
        }

        private object CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (player == null || !_playerSettings.ContainsKey(player.userID)) return null;

            var settings = _playerSettings[player.userID];
            
            // Allow upgrade if infinite resources is enabled
            if (settings.InfiniteResources)
            {
                return true;
            }

            return null;
        }

        private object CanAffordToPlace(BasePlayer player, Planner planner, Construction construction)
        {
            if (player == null || !_playerSettings.ContainsKey(player.userID)) return null;

            var settings = _playerSettings[player.userID];
            
            // Allow placement if infinite resources is enabled
            if (settings.InfiniteResources)
            {
                return true;
            }

            return null;
        }

        private object OnPayForPlacement(BasePlayer player, Planner planner, Construction construction)
        {
            if (player == null || !_playerSettings.ContainsKey(player.userID)) return null;

            var settings = _playerSettings[player.userID];
            
            // Skip payment if infinite resources is enabled
            if (settings.InfiniteResources)
            {
                return true;
            }

            return null;
        }

        private object OnPayForUpgrade(BasePlayer player, BuildingBlock block, ConstructionGrade gradeTarget)
        {
            if (player == null || !_playerSettings.ContainsKey(player.userID)) return null;

            var settings = _playerSettings[player.userID];
            
            // Skip payment if infinite resources is enabled
            if (settings.InfiniteResources)
            {
                return true;
            }

            return null;
        }

        private object OnItemCraft(ItemCraftTask task, BasePlayer player)
        {
            if (player == null || task == null) return null;
            
            // Only apply instant crafting if enabled for this player
            if (!_playerSettings.ContainsKey(player.userID) || !_playerSettings[player.userID].InstantCrafting)
            {
                return null;
            }

            // Remove crafting time - try different property names
            try
            {
                var timeProp = task.GetType().GetProperty("time", BindingFlags.Public | BindingFlags.Instance);
                if (timeProp != null)
                {
                    timeProp.SetValue(task, 0f);
                }
                else
                {
                    // Try duration property
                    var durationProp = task.GetType().GetProperty("duration", BindingFlags.Public | BindingFlags.Instance);
                    if (durationProp != null)
                    {
                        durationProp.SetValue(task, 0f);
                    }
                    else
                    {
                        // Try finishTime property
                        var finishTimeProp = task.GetType().GetProperty("finishTime", BindingFlags.Public | BindingFlags.Instance);
                        if (finishTimeProp != null)
                        {
                            finishTimeProp.SetValue(task, UnityEngine.Time.realtimeSinceStartup);
                        }
                    }
                }
            }
            catch
            {
                // If we can't set the time, try to complete the task immediately
                if (task.blueprint != null && player != null)
                {
                    // Force complete the craft
                    task.cancelled = false;
                }
            }
            
            return null;
        }

        [ConsoleCommand("build.save")]
        private void SaveCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(player, "Usage: Enter a save name in the input field and click Save Buildings");
                return;
            }
            
            string saveName = arg.Args[0];
            if (string.IsNullOrWhiteSpace(saveName))
            {
                SendReply(player, "Save name cannot be empty!");
                return;
            }
            
            // Sanitize filename
            saveName = string.Join("_", saveName.Split(Path.GetInvalidFileNameChars()));
            
            SaveBuildings(player, saveName);
        }
        
        [ConsoleCommand("build.save.current")]
        private void SaveCurrentCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            
            // Get save name from input field if available
            // For now, use a timestamp-based name
            string saveName = $"save_{DateTime.Now:yyyyMMdd_HHmmss}";
            SaveBuildings(player, saveName);
        }
        
        [ConsoleCommand("build.load")]
        private void LoadCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(player, "Usage: build.load <save_name>");
                return;
            }
            
            string saveName = arg.Args[0];
            LoadBuildings(player, saveName);
        }
        
        [ConsoleCommand("build.delete")]
        private void DeleteCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(player, "Usage: build.delete <save_name>");
                return;
            }
            
            string saveName = arg.Args[0];
            DeleteSave(saveName);
            
            // Refresh the saves panel (reset to page 0 after delete)
            if (_uiOpen.Contains(player.userID) && _activePanel.ContainsKey(player.userID) && _activePanel[player.userID] == "SAVES")
            {
                _savesPage[player.userID] = 0; // Reset to first page
                using var cui = new CUI(CuiHandler);
                ShowPanel(cui, player, "SAVES");
            }
        }
        
        private void SaveBuildings(BasePlayer player, string saveName)
        {
            if (player == null) return;
            
            // Check if an operation is already in progress for this player
            if (_operationInProgress.Contains(player.userID))
            {
                SendReply(player, "A save or load operation is already in progress. Please wait for it to complete.");
                return;
            }
            
            _operationInProgress.Add(player.userID);
            
            try
            {
                SendReply(player, "Collecting all buildings on the map... This may take a moment.");
            
            var entitiesList = new List<EntitySaveData>();
            var processedEntities = new HashSet<BaseEntity>();
            
            // Iterate through all server entities
            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                // Cast to BaseEntity
                var entity = networkable as BaseEntity;
                if (entity == null || !entity.IsValid() || entity.IsDestroyed)
                    continue;
                
                // Skip if already processed
                if (processedEntities.Contains(entity))
                    continue;
                
                // Skip entities with parents (they'll be saved as children)
                if (entity.HasParent())
                    continue;
                
                // Only save building blocks
                var buildingBlock = entity as BuildingBlock;
                if (buildingBlock == null)
                    continue;
                
                processedEntities.Add(entity);
                
                // Save entity data (use absolute positions, not relative)
                var entityData = new EntitySaveData
                {
                    PrefabName = entity.PrefabName,
                    Position = new SerializableVector3(entity.transform.position),
                    Rotation = new SerializableVector3(entity.transform.rotation.eulerAngles),
                    SkinId = entity.skinID.ToString(),
                    OwnerId = entity.OwnerID.ToString(),
                    Grade = (int)buildingBlock.grade,
                    CustomColour = buildingBlock.customColour,
                    Health = buildingBlock.health,
                    MaxHealth = buildingBlock.MaxHealth(),
                    Inventory = new ItemSaveData[0] // Initialize empty, will be set below if needed
                };
                
                // Save inventory data
                var container = entity as IItemContainerEntity;
                if (container != null && container.inventory != null)
                {
                    entityData.Inventory = ExtractInventoryData(container.inventory).ToArray();
                }
                
                entitiesList.Add(entityData);
            }
            
                // Save to file
                var saveData = new SaveData
                {
                    SaveName = saveName,
                    SourcePosition = new SerializableVector3(Vector3.zero), // Not used anymore, kept for compatibility
                    SourceRotation = new SerializableVector3(Vector3.zero), // Not used anymore, kept for compatibility
                    Entities = entitiesList.ToArray()
                };
                
                // Save to JSON file manually
                string savesPath = GetSavesDirectory();
                string filePath = Path.Combine(savesPath, $"{saveName}.json");
                string json = SerializeToJson(saveData);
                File.WriteAllText(filePath, json);
                SendReply(player, $"Successfully saved {entitiesList.Count} entities from the entire map as '{saveName}'!");
                
                // Refresh saves panel if open (reset to page 0 after save/delete)
                if (_uiOpen.Contains(player.userID) && _activePanel.ContainsKey(player.userID) && _activePanel[player.userID] == "SAVES")
                {
                    _savesPage[player.userID] = 0; // Reset to first page
                    using var cui = new CUI(CuiHandler);
                    ShowPanel(cui, player, "SAVES");
                }
            }
            catch (Exception ex)
            {
                SendReply(player, $"Error saving: {ex.Message}");
                PrintError($"Error saving build '{saveName}': {ex}");
            }
            finally
            {
                _operationInProgress.Remove(player.userID);
            }
        }
        
        private void LoadBuildings(BasePlayer player, string saveName)
        {
            if (player == null) return;
            
            // Check if an operation is already in progress for this player
            if (_operationInProgress.Contains(player.userID))
            {
                SendReply(player, "A save or load operation is already in progress. Please wait for it to complete.");
                return;
            }
            
            _operationInProgress.Add(player.userID);
            
            try
            {
                // Delete all existing building entities before loading
                SendReply(player, "Deleting all existing buildings...");
                int deletedCount = DeleteAllBuildings();
                SendReply(player, $"Deleted {deletedCount} existing buildings.");
                
                // Load from JSON file
                string savesPath = GetSavesDirectory();
                string filePath = Path.Combine(savesPath, $"{saveName}.json");
                
                if (!File.Exists(filePath))
                {
                    SendReply(player, $"Save '{saveName}' not found!");
                    return;
                }
                
                string json = File.ReadAllText(filePath);
                
                if (string.IsNullOrEmpty(json))
                {
                    SendReply(player, $"Save '{saveName}' is empty!");
                    return;
                }
                
                var saveData = DeserializeFromJson<SaveData>(json);
                
                if (saveData == null)
                {
                    PrintError($"Failed to deserialize save '{saveName}'. JSON length: {json.Length}");
                    PrintError($"First 500 chars: {json.Substring(0, Math.Min(500, json.Length))}");
                    SendReply(player, $"Save '{saveName}' is corrupted! Check server console for details.");
                    return;
                }
                
                if (saveData == null || saveData.Entities == null || saveData.Entities.Length == 0)
                {
                    SendReply(player, "Save file is empty or corrupted!");
                    return;
                }
                
                SendReply(player, $"Loading {saveData.Entities.Length} buildings at their original positions...");
                
                var pastedEntities = new List<BaseEntity>();
                var buildingId = BuildingManager.server.NewBuildingID();
                
                foreach (var entityData in saveData.Entities)
                {
                    // Use original absolute position
                    var worldPos = entityData.Position.ToVector3();
                    
                    // Use original rotation
                    var rot = Quaternion.Euler(entityData.Rotation.ToVector3());
                    
                    // Create entity
                    var entity = GameManager.server.CreateEntity(entityData.PrefabName, worldPos, rot);
                    if (entity == null) continue;
                    
                    if (ulong.TryParse(entityData.SkinId, out ulong skinId))
                        entity.skinID = skinId;
                    entity.OwnerID = player.userID;
                    
                    // Calculate health ratio from saved values (before we modify the entity)
                    float healthRatio = 1f;
                    if (entityData.Health > 0 && entityData.MaxHealth > 0)
                    {
                        healthRatio = entityData.Health / entityData.MaxHealth;
                        healthRatio = Mathf.Clamp01(healthRatio); // Ensure ratio is between 0 and 1
                    }
                    
                    // Apply building block data
                    var bb = entity as BuildingBlock;
                    if (bb != null)
                    {
                        bb.blockDefinition = PrefabAttribute.server.Find<Construction>(bb.prefabID);
                        
                        // Spawn as twig first
                        bb.SetGrade(BuildingGrade.Enum.Twigs);
                        bb.SetHealthToMax();
                    }
                    
                    // Attach to building
                    var decayEntity = entity as DecayEntity;
                    if (decayEntity != null)
                    {
                        decayEntity.AttachToBuilding(buildingId);
                    }
                    
                    // Spawn the entity first
                    entity.Spawn();
                    
                    // Now upgrade to the correct grade after spawning (this preserves health better)
                    if (bb != null)
                    {
                        // Upgrade to the correct grade
                        if (entityData.Grade != (int)BuildingGrade.Enum.Twigs)
                        {
                            bb.SetGrade((BuildingGrade.Enum)entityData.Grade);
                        }
                        
                        // Restore health using the ratio multiplied by the current max health
                        bb.health = healthRatio * bb.MaxHealth();
                        
                        if (entityData.CustomColour != 0)
                            bb.SetCustomColour(entityData.CustomColour);
                        bb.UpdateSkin();
                        bb.SendNetworkUpdate();
                    }
                    
                    // Restore inventory
                    if (entityData.Inventory != null && entityData.Inventory.Length > 0 && entity is IItemContainerEntity container && container.inventory != null)
                    {
                        RestoreInventory(container.inventory, entityData.Inventory.ToList());
                    }
                    
                    pastedEntities.Add(entity);
                }
                
                _lastPastedEntities[player.userID] = pastedEntities;
                SendReply(player, $"Successfully loaded {pastedEntities.Count} entities from '{saveName}'!");
            }
            catch (Exception ex)
            {
                SendReply(player, $"Error loading save: {ex.Message}");
                PrintError($"Error loading build '{saveName}': {ex}");
            }
            finally
            {
                _operationInProgress.Remove(player.userID);
            }
        }
        
        [ConsoleCommand("build.undo")]
        private void UndoCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            
            if (!_lastPastedEntities.ContainsKey(player.userID) || _lastPastedEntities[player.userID].Count == 0)
            {
                SendReply(player, "No pasted buildings to undo!");
                return;
            }
            
            var entities = _lastPastedEntities[player.userID];
            int count = entities.Count;
            
            foreach (var entity in entities)
            {
                if (entity != null && entity.IsValid() && !entity.IsDestroyed)
                {
                    entity.Kill();
                }
            }
            
            _lastPastedEntities[player.userID].Clear();
            SendReply(player, $"Removed {count} entities!");
        }
        
        private int DeleteAllBuildings()
        {
            int count = 0;
            var entitiesToDelete = new List<BaseEntity>();
            
            // Collect all building blocks
            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var entity = networkable as BaseEntity;
                if (entity == null || !entity.IsValid() || entity.IsDestroyed)
                    continue;
                
                var buildingBlock = entity as BuildingBlock;
                if (buildingBlock != null)
                {
                    entitiesToDelete.Add(entity);
                }
            }
            
            // Delete all collected building blocks
            foreach (var entity in entitiesToDelete)
            {
                if (entity != null && entity.IsValid() && !entity.IsDestroyed)
                {
                    entity.Kill();
                    count++;
                }
            }
            
            return count;
        }
        
        private void DeleteSave(string saveName)
        {
            try
            {
                // Delete from Carbon's data directory
                string dataDir = Path.Combine(ConVar.Server.rootFolder, "carbon", "data", _savesDirectory);
                string filePath = Path.Combine(dataDir, $"{saveName}.json");
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Puts($"Deleted save '{saveName}'");
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error deleting save '{saveName}': {ex}");
            }
        }
        
        private Vector3 NormalizePosition(Vector3 initialPos, Vector3 currentPos, float diffRot)
        {
            return currentPos - initialPos;
        }
        
        private List<ItemSaveData> ExtractInventoryData(ItemContainer inventory)
        {
            var items = new List<ItemSaveData>();
            
            if (inventory?.itemList == null) return items;
            
            foreach (var item in inventory.itemList)
            {
                items.Add(new ItemSaveData
                {
                    ItemId = item.info.itemid,
                    Amount = item.amount,
                    SkinId = item.skin.ToString(),
                    Position = item.position,
                    Condition = item.condition,
                    MaxCondition = item.maxCondition
                });
            }
            
            return items;
        }
        
        private string SerializeToJson<T>(T obj)
        {
            // Simple JSON serialization for our data structures
            if (obj == null) return "null";
            
            if (obj is string str)
                return $"\"{str.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
            
            if (obj is bool b)
                return b ? "true" : "false";
            
            if (obj is int || obj is float || obj is uint || obj is ulong)
                return obj.ToString();
            
            if (obj is SerializableVector3 vec)
                return $"{{\"x\":{vec.x},\"y\":{vec.y},\"z\":{vec.z}}}";
            
            if (obj is System.Array arr)
            {
                var items = new List<string>();
                foreach (var item in arr)
                    items.Add(SerializeToJson(item));
                return $"[{string.Join(",", items)}]";
            }
            
            // Handle objects
            var type = obj.GetType();
            var props = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var jsonProps = new List<string>();
            
            foreach (var prop in props)
            {
                var value = prop.GetValue(obj);
                var propName = prop.Name;
                var propValue = SerializeToJson(value);
                jsonProps.Add($"\"{propName}\":{propValue}");
            }
            
            return $"{{{string.Join(",", jsonProps)}}}";
        }
        
        private T DeserializeFromJson<T>(string json) where T : class, new()
        {
            try
            {
                // Simple JSON deserialization - this is a basic implementation
                var obj = new T();
                var type = typeof(T);
                
                // Remove whitespace
                json = json.Trim();
                if (!json.StartsWith("{") || !json.EndsWith("}"))
                {
                    PrintError($"JSON doesn't start/end with braces. Starts with: {json.Substring(0, Math.Min(50, json.Length))}");
                    return null;
                }
                
                json = json.Substring(1, json.Length - 2); // Remove { }
                
                var props = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                
                foreach (var prop in props)
                {
                    // Try multiple patterns to find the property
                    var patterns = new[]
                    {
                        $"\"{prop.Name}\":",
                        $"\"{prop.Name}\" :",
                        $"\"{prop.Name}\": ",
                        $"\"{prop.Name}\" : "
                    };
                    
                    int startIdx = -1;
                    string matchedPattern = null;
                    
                    foreach (var pattern in patterns)
                    {
                        startIdx = json.IndexOf(pattern);
                        if (startIdx != -1)
                        {
                            matchedPattern = pattern;
                            break;
                        }
                    }
                    
                    if (startIdx == -1) 
                    {
                        // Field not found, might be optional - that's okay
                        continue;
                    }
                    
                    startIdx += matchedPattern.Length;
                    
                    // Skip whitespace
                    while (startIdx < json.Length && char.IsWhiteSpace(json[startIdx]))
                        startIdx++;
                    
                    var valueStr = ExtractJsonValue(json, startIdx);
                    
                    if (valueStr != null)
                    {
                        var value = ParseJsonValue(valueStr, prop.FieldType);
                        if (value != null)
                        {
                            prop.SetValue(obj, value);
                        }
                        else
                        {
                            PrintError($"Failed to parse value for field '{prop.Name}' (type: {prop.FieldType.Name}): {valueStr.Substring(0, Math.Min(100, valueStr.Length))}");
                        }
                    }
                    else
                    {
                        PrintError($"Failed to extract value for field '{prop.Name}'");
                    }
                }
                
                return obj;
            }
            catch (Exception ex)
            {
                PrintError($"DeserializeFromJson error: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
        
        private string ExtractJsonValue(string json, int startIdx)
        {
            if (startIdx >= json.Length) return null;
            
            json = json.Substring(startIdx).Trim();
            if (json.Length == 0) return null;
            
            if (json.StartsWith("\""))
            {
                // String value - handle escaped quotes
                int i = 1;
                while (i < json.Length)
                {
                    if (json[i] == '"' && json[i - 1] != '\\')
                    {
                        // Found unescaped quote
                        return json.Substring(1, i - 1).Replace("\\\"", "\"").Replace("\\\\", "\\");
                    }
                    i++;
                }
                return null; // No closing quote found
            }
            else if (json.StartsWith("{"))
            {
                // Object value
                int depth = 0;
                bool inString = false;
                int i = 0;
                for (; i < json.Length; i++)
                {
                    if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                        inString = !inString;
                    
                    if (!inString)
                    {
                        if (json[i] == '{') depth++;
                        if (json[i] == '}') depth--;
                        if (depth == 0) break;
                    }
                }
                if (i >= json.Length) return null;
                return json.Substring(0, i + 1);
            }
            else if (json.StartsWith("["))
            {
                // Array value
                int depth = 0;
                bool inString = false;
                int i = 0;
                for (; i < json.Length; i++)
                {
                    if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                        inString = !inString;
                    
                    if (!inString)
                    {
                        if (json[i] == '[') depth++;
                        if (json[i] == ']') depth--;
                        if (depth == 0) break;
                    }
                }
                if (i >= json.Length) return null;
                return json.Substring(0, i + 1);
            }
            else
            {
                // Number or boolean
                var endIdx = json.IndexOfAny(new[] { ',', '}', ']' });
                if (endIdx == -1) return json.Trim();
                return json.Substring(0, endIdx).Trim();
            }
        }
        
        private object ParseJsonValue(string valueStr, System.Type targetType)
        {
            if (targetType == typeof(string))
                return valueStr;
            
            if (targetType == typeof(int) && int.TryParse(valueStr, out int intVal))
                return intVal;
            
            if (targetType == typeof(float) && float.TryParse(valueStr, out float floatVal))
                return floatVal;
            
            if (targetType == typeof(uint) && uint.TryParse(valueStr, out uint uintVal))
                return uintVal;
            
            if (targetType == typeof(ulong) && ulong.TryParse(valueStr, out ulong ulongVal))
                return ulongVal;
            
            if (targetType == typeof(bool))
            {
                if (valueStr == "true") return true;
                if (valueStr == "false") return false;
            }
            
            if (targetType == typeof(SerializableVector3))
            {
                try
                {
                    // Manually parse SerializableVector3 to avoid reflection issues
                    valueStr = valueStr.Trim();
                    if (!valueStr.StartsWith("{") || !valueStr.EndsWith("}"))
                        return new SerializableVector3();
                    
                    var vec = new SerializableVector3();
                    var innerJson = valueStr.Substring(1, valueStr.Length - 2);
                    
                    // Parse x, y, z using regex
                    var xMatch = Regex.Match(innerJson, @"""x"":\s*([+-]?[0-9]*\.?[0-9]+(?:[eE][+-]?[0-9]+)?)");
                    var yMatch = Regex.Match(innerJson, @"""y"":\s*([+-]?[0-9]*\.?[0-9]+(?:[eE][+-]?[0-9]+)?)");
                    var zMatch = Regex.Match(innerJson, @"""z"":\s*([+-]?[0-9]*\.?[0-9]+(?:[eE][+-]?[0-9]+)?)");
                    
                    if (xMatch.Success && float.TryParse(xMatch.Groups[1].Value, out float x))
                        vec.x = x;
                    if (yMatch.Success && float.TryParse(yMatch.Groups[1].Value, out float y))
                        vec.y = y;
                    if (zMatch.Success && float.TryParse(zMatch.Groups[1].Value, out float z))
                        vec.z = z;
                    
                    return vec;
                }
                catch (Exception ex)
                {
                    PrintError($"Error deserializing SerializableVector3: {ex.Message}");
                    return new SerializableVector3();
                }
            }
            
            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();
                valueStr = valueStr.Trim();
                if (!valueStr.StartsWith("[") || !valueStr.EndsWith("]"))
                {
                    PrintError($"Array value doesn't start/end with brackets: {valueStr.Substring(0, Math.Min(100, valueStr.Length))}");
                    return null;
                }
                
                // Handle empty array
                if (valueStr == "[]")
                {
                    return System.Array.CreateInstance(elementType, 0);
                }
                
                valueStr = valueStr.Substring(1, valueStr.Length - 2);
                var items = new List<object>();
                var parts = SplitJsonArray(valueStr);
                
                foreach (var part in parts)
                {
                    if (string.IsNullOrWhiteSpace(part)) continue;
                    var item = ParseJsonValue(part, elementType);
                    if (item != null) items.Add(item);
                }
                
                var array = System.Array.CreateInstance(elementType, items.Count);
                for (int i = 0; i < items.Count; i++)
                    array.SetValue(items[i], i);
                return array;
            }
            
            if (targetType.IsClass && targetType != typeof(string))
            {
                try
                {
                    // Use reflection to call the generic DeserializeFromJson method
                    var method = typeof(Build).GetMethod("DeserializeFromJson", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                    if (method == null)
                    {
                        PrintError($"Could not find DeserializeFromJson method for type {targetType.Name}");
                        return null;
                    }
                    
                    var genericMethod = method.MakeGenericMethod(targetType);
                    if (genericMethod == null)
                    {
                        PrintError($"Could not create generic method for type {targetType.Name}");
                        return null;
                    }
                    
                    var result = genericMethod.Invoke(this, new[] { valueStr });
                    return result;
                }
                catch (Exception ex)
                {
                    PrintError($"Error deserializing nested object of type {targetType?.Name ?? "unknown"}: {ex.Message}\nStack: {ex.StackTrace}");
                    return null;
                }
            }
            
            return null;
        }
        
        private List<string> SplitJsonArray(string json)
        {
            var items = new List<string>();
            int depth = 0;
            int start = 0;
            bool inString = false;
            
            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                    inString = !inString;
                
                if (!inString)
                {
                    if (json[i] == '{' || json[i] == '[') depth++;
                    if (json[i] == '}' || json[i] == ']') depth--;
                    
                    if (depth == 0 && json[i] == ',')
                    {
                        items.Add(json.Substring(start, i - start).Trim());
                        start = i + 1;
                    }
                }
            }
            
            if (start < json.Length)
                items.Add(json.Substring(start).Trim());
            
            return items;
        }
        
        private void RestoreInventory(ItemContainer inventory, List<ItemSaveData> items)
        {
            if (inventory == null || items == null) return;
            
            inventory.Clear();
            
            foreach (var itemData in items)
            {
                ulong skinId = 0;
                if (!string.IsNullOrEmpty(itemData.SkinId) && ulong.TryParse(itemData.SkinId, out ulong parsedSkinId))
                    skinId = parsedSkinId;
                
                var item = ItemManager.CreateByItemID(itemData.ItemId, itemData.Amount, skinId);
                if (item != null)
                {
                    if (item.hasCondition)
                    {
                        item.maxCondition = itemData.MaxCondition;
                        item.condition = itemData.Condition;
                    }
                    item.position = itemData.Position;
                    inventory.Insert(item);
                }
            }
        }
        
        [Serializable]
        private class SaveData
        {
            public string SaveName;
            public SerializableVector3 SourcePosition;
            public SerializableVector3 SourceRotation;
            public EntitySaveData[] Entities;
        }
        
        [Serializable]
        private class EntitySaveData
        {
            public string PrefabName;
            public SerializableVector3 Position;
            public SerializableVector3 Rotation;
            public string SkinId;
            public string OwnerId;
            public int Grade;
            public uint CustomColour;
            public float Health;
            public float MaxHealth;
            public ItemSaveData[] Inventory;
        }
        
        [Serializable]
        private class ItemSaveData
        {
            public int ItemId;
            public int Amount;
            public string SkinId;
            public int Position;
            public float Condition;
            public float MaxCondition;
        }
        
        [Serializable]
        private class SerializableVector3
        {
            public float x;
            public float y;
            public float z;
            
            public SerializableVector3() { }
            
            public SerializableVector3(Vector3 v)
            {
                x = v.x;
                y = v.y;
                z = v.z;
            }
            
            public Vector3 ToVector3()
            {
                return new Vector3(x, y, z);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null)
            {
                if (_playerSettings.ContainsKey(player.userID))
                {
                    _playerSettings.Remove(player.userID);
                }
                _uiOpen.Remove(player.userID);
                _activeInfinitePlayers.Remove(player.userID);
                _activePanel.Remove(player.userID);
                _lastActivePanel.Remove(player.userID);
                _operationInProgress.Remove(player.userID);
                _savesPage.Remove(player.userID);
            }
        }

        private void Unload()
        {
            // Clean up hidden resources for all active players
            foreach (var playerId in _activeInfinitePlayers)
            {
                var player = BasePlayer.FindByID(playerId);
                if (player != null && player.inventory != null)
                {
                    player.inventory.SendUpdatedInventory(PlayerInventory.Type.Main, player.inventory.containerMain);
                }
            }
        }
    }
}

