using System;
using System.Collections.Generic;
using System.Linq;
using Carbon.Core;
using Oxide.Game.Rust.Libraries;
using Oxide.Core;
using UnityEngine;
using System.IO;

namespace Carbon.Plugins
{
    [Info("Fill Box", "Assistant", "1.0.0")]
    [Description("Allows players to fill containers with random items")]
    public class fill_box : CarbonPlugin
    {
        private static class UI
        {
            public static class Colors
            {
                public const string
                    CommandColor = "#87CEEB",  // Light Sky Blue - Commands and syntax
                    ErrorColor = "#FF6B6B",    // Soft Red - Errors and warnings
                    SuccessColor = "#98FB98",   // Pale Green - Success messages
                    InputColor = "#FFE66D",     // Light Yellow - User input
                    CategoryColor = "#DDA0DD";  // Plum - Categories
                
                public static string Colorize(string text, string color) => $"<color={color}>{text}</color>";
                public static string FormatSuccess(string text) => Colorize($"✓ {text}", SuccessColor);
                public static string FormatError(string text) => Colorize(text, ErrorColor);
                public static string FormatCommand(string text) => Colorize(text, CommandColor);
                public static string FormatInput(string text) => Colorize(text, InputColor);
                public static string FormatCategory(string text) => Colorize(text, CategoryColor);
            }
        }

        private ItemDefinition[] _allDefinitions;
        private Dictionary<string, List<ItemDefinition>> _categoryItems;
        private Dictionary<StorageContainer, BoxHistory> _containerHistory = new Dictionary<StorageContainer, BoxHistory>();
        private Dictionary<StorageContainer, BaseEntity> _alwaysIndicators = new Dictionary<StorageContainer, BaseEntity>();
        private Dictionary<BasePlayer, HashSet<StorageContainer>> _selectedBoxes = new Dictionary<BasePlayer, HashSet<StorageContainer>>();
        private Dictionary<StorageContainer, BaseEntity> _selectionIndicators = new Dictionary<StorageContainer, BaseEntity>();

        private class BoxHistory
        {
            public string Command { get; set; }  // "random", "category", or "clear"
            public string Category { get; set; }  // Only for category command
            public bool IsPermanent { get; set; }
        }

        private class Category
        {
            public string Id { get; set; }          // e.g., "ammunition"
            public string Name { get; set; }        // e.g., "Ammunition"
            public int? SkinId { get; set; }        // Reference to _skins list
        }

        private class Skin
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public ulong WorkshopId { get; set; }
        }

        private readonly List<Skin> _skins = new List<Skin>
        {
            new Skin { Id = 0, Name = "Neon Ammo Storage", WorkshopId = 2502887845 },
            new Skin { Id = 1, Name = "Neon Armor Storage", WorkshopId = 2530697138 },
            new Skin { Id = 2, Name = "Neon Boom Storage", WorkshopId = 2458681561 },
            new Skin { Id = 3, Name = "Neon Charcoal Storage", WorkshopId = 2565285602 },
            new Skin { Id = 4, Name = "Neon Clothes Storage", WorkshopId = 2579209697 },
            new Skin { Id = 5, Name = "Neon Comps Storage", WorkshopId = 2537853096 },
            new Skin { Id = 6, Name = "Neon Drop Box Storage", WorkshopId = 2569839654 },
            new Skin { Id = 7, Name = "Neon Elec Storage", WorkshopId = 2581449517 },
            new Skin { Id = 8, Name = "Neon Food Storage", WorkshopId = 2473566198 },
            new Skin { Id = 9, Name = "Neon Frags Storage", WorkshopId = 2509860100 },
            new Skin { Id = 10, Name = "Neon Gun Storage", WorkshopId = 2489929747 },
            new Skin { Id = 11, Name = "Neon Meds Storage", WorkshopId = 2498742171 },
            new Skin { Id = 12, Name = "Neon Ore Storage", WorkshopId = 2446262619 },
            new Skin { Id = 13, Name = "Neon Scrap Storage", WorkshopId = 2424873646 },
            new Skin { Id = 14, Name = "Neon Stone Storage", WorkshopId = 2538049119 },
            new Skin { Id = 15, Name = "Neon Sulfur Storage", WorkshopId = 2517917450 },
            new Skin { Id = 16, Name = "Neon Tools Storage", WorkshopId = 2551641004 },
            new Skin { Id = 17, Name = "Neon Drop Box Storage", WorkshopId = 2569839654 },
            new Skin { Id = 18, Name = "Recyclables Box", WorkshopId = 878850459 }
        };

        private readonly List<Category> _categories = new List<Category>
        {
            new Category { Id = "ammunition", Name = "Ammunition", SkinId = 0 },  // Neon Ammo Storage
            new Category { Id = "attire", Name = "Attire", SkinId = 4 },         // Neon Clothes Storage
            new Category { Id = "component", Name = "Components", SkinId = 5 },   // Neon Comps Storage
            new Category { Id = "construction", Name = "Construction", SkinId = 7 }, // Neon Elec Storage
            new Category { Id = "electrical", Name = "Electrical", SkinId = 7 },  // Neon Elec Storage
            new Category { Id = "food", Name = "Food", SkinId = 8 },             // Neon Food Storage
            new Category { Id = "medical", Name = "Medical", SkinId = 11 },      // Neon Meds Storage
            new Category { Id = "resources", Name = "Resources", SkinId = 12 },   // Neon Ore Storage
            new Category { Id = "tool", Name = "Tools", SkinId = 16 },           // Neon Tools Storage
            new Category { Id = "weapon", Name = "Weapons", SkinId = 10 },       // Neon Gun Storage
            // Categories that share the electrical storage skin
            new Category { Id = "misc", Name = "Misc", SkinId = 7 },            // Neon Elec Storage
            new Category { Id = "fun", Name = "Fun", SkinId = 7 },              // Neon Elec Storage
            new Category { Id = "items", Name = "Items", SkinId = 7 },          // Neon Elec Storage
            new Category { Id = "traps", Name = "Traps", SkinId = 7 }           // Neon Elec Storage
        };

        // Add this class to store the box state
        private class BoxData
        {
            public Dictionary<string, BoxState> Boxes { get; set; } = new Dictionary<string, BoxState>();
        }

        private class BoxState
        {
            public string Command { get; set; }
            public string Category { get; set; }
            public bool IsPermanent { get; set; }
            public Vector3 Position { get; set; }
            public string PrefabName { get; set; }
        }

        private BoxData _data;

        // Add these classes for JSON structure
        private class DebugItemInfo
        {
            public string TargetCategory { get; set; }
            public int MaxAmountInOutput { get; set; }
            public int BufferAmount { get; set; }
            public int MinAmountInInput { get; set; }
            public bool IsBlueprint { get; set; }
            public int BufferTransferRemaining { get; set; }
            public string TargetItemName { get; set; }
        }

        private class DebugCategoryInfo
        {
            public string Category { get; set; }
            public List<DebugItemInfo> ItemIDs { get; set; }
        }

        private List<DebugCategoryInfo> _debugData;

        private void LoadData()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<BoxData>("fill_box") ?? new BoxData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("fill_box", _data);
        }

        private void LoadDebugData()
        {
            _debugData = Interface.Oxide.DataFileSystem.ReadObject<List<DebugCategoryInfo>>("fill_box_debug") ?? new List<DebugCategoryInfo>();
        }

        private void SaveDebugData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("fill_box_debug", _debugData);
        }

        private void OnServerInitialized()
        {
            LoadData();
            LoadDebugData();
            
            // Cache all item definitions that are obtainable
            _allDefinitions = ItemManager.itemList
                .Where(item => IsItemObtainable(item))
                .ToArray();
            
            // Initialize categories
            _categoryItems = new Dictionary<string, List<ItemDefinition>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ammunition"] = new List<ItemDefinition>(),
                ["attire"] = new List<ItemDefinition>(),
                ["component"] = new List<ItemDefinition>(),
                ["construction"] = new List<ItemDefinition>(),
                ["electrical"] = new List<ItemDefinition>(),
                ["food"] = new List<ItemDefinition>(),
                ["fun"] = new List<ItemDefinition>(),
                ["items"] = new List<ItemDefinition>(),
                ["medical"] = new List<ItemDefinition>(),
                ["misc"] = new List<ItemDefinition>(),
                ["resources"] = new List<ItemDefinition>(),
                ["tool"] = new List<ItemDefinition>(),
                ["traps"] = new List<ItemDefinition>(),
                ["weapon"] = new List<ItemDefinition>()
            };

            // Categorize obtainable items
            foreach (var def in _allDefinitions)
            {
                var category = GetItemCategory(def);
                if (_categoryItems.ContainsKey(category))
                {
                    _categoryItems[category].Add(def);
                }
            }

            // Register commands
            cmd.AddChatCommand("box", this, nameof(OnBoxCommand));

            // Restore box states
            foreach (var kvp in _data.Boxes.ToList())
            {
                var state = kvp.Value;
                var container = BaseNetworkable.serverEntities.OfType<StorageContainer>()
                    .FirstOrDefault(c => 
                        c.PrefabName == state.PrefabName && 
                        c.transform.position == state.Position);

                if (container != null)
                {
                    _containerHistory[container] = new BoxHistory
                    {
                        Command = state.Command,
                        Category = state.Category,
                        IsPermanent = state.IsPermanent
                    };

                    if (state.IsPermanent)
                    {
                        UpdateAlwaysIndicator(container, true);
                        
                        // Reapply skin based on command
                        switch (state.Command)
                        {
                            case "random":
                                if (container.ShortPrefabName == "box.wooden.large")
                                {
                                    var skin = _skins.FirstOrDefault(s => s.Id == 17);
                                    if (skin != null)
                                    {
                                        container.skinID = skin.WorkshopId;
                                        container.SendNetworkUpdate();
                                    }
                                }
                                break;
                            case "category":
                                ApplyCategorySkin(container, state.Category);
                                break;
                            case "clear":
                                if (container.ShortPrefabName == "box.wooden.large")
                                {
                                    var skin = _skins.FirstOrDefault(s => s.Id == 18);
                                    if (skin != null)
                                    {
                                        container.skinID = skin.WorkshopId;
                                        container.SendNetworkUpdate();
                                    }
                                }
                                break;
                        }
                    }
                }
                else
                {
                    // Box no longer exists, remove from data
                    _data.Boxes.Remove(kvp.Key);
                    SaveData();
                }
            }
        }

        private (string match, List<string> ambiguous) MatchCommand(string input, IEnumerable<string> validCommands)
        {
            if (string.IsNullOrEmpty(input)) 
                return (null, null);

            // Convert to lowercase for case-insensitive matching
            input = input.ToLower();

            // Check for exact match first
            var exactMatch = validCommands.FirstOrDefault(cmd => 
                cmd.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null) 
                return (exactMatch, null);

            // Find all commands that start with the input
            var matches = validCommands.Where(cmd => 
                cmd.StartsWith(input, StringComparison.OrdinalIgnoreCase)).ToList();

            // Return the match if there's exactly one, otherwise return ambiguous matches
            return matches.Count == 1 
                ? (matches[0], null) 
                : (null, matches);
        }

        private void OnBoxCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, $"Usage: /box {UI.Colors.FormatCommand("clear")}|{UI.Colors.FormatCommand("random")}|{UI.Colors.FormatCommand("category")}|{UI.Colors.FormatCommand("bulk")}|{UI.Colors.FormatCommand("debug")}|{UI.Colors.FormatCommand("skin")}|{UI.Colors.FormatCommand("everything")} [category] [--always]");
                SendReply(player, $"Use {UI.Colors.FormatCommand("/box stop")} to disable always mode");
                return;
            }

            // Parse flags with partial matching
            var validFlags = new[] { "--always" };
            bool isAlways = args.Any(arg => 
                validFlags.Any(flag => flag.StartsWith(arg, StringComparison.OrdinalIgnoreCase)));

            var validCommands = new[] { "clear", "random", "category", "stop", "bulk", "debug", "skin", "everything" };
            var (action, ambiguousCommands) = MatchCommand(args[0], validCommands);

            // Add skin command handling
            if (action == "skin")
            {
                HandleSkinCommand(player, args);
                return;
            }

            // Add debug command handling
            if (action == "debug")
            {
                HandleDebugCommand(player, args);
                return;
            }

            // Handle bulk mode
            if (action == "bulk")
            {
                HandleBulkMode(player);
                return;
            }

            // Handle everything command
            if (action == "everything")
            {
                HandleEverythingCommand(player);
                return;
            }

            // Handle bulk operations
            if (_selectedBoxes.TryGetValue(player, out var selectedContainers) && selectedContainers.Count > 0)
            {
                foreach (var targetContainer in selectedContainers)
                {
                    // Run the command on each selected box
                    ExecuteBoxCommand(player, targetContainer, action, args, isAlways);
                }

                // Don't exit bulk mode after command
                SendReply(player, UI.Colors.FormatSuccess($"Command executed on {selectedContainers.Count} boxes."));
                return;
            }

            if (action == null)
            {
                if (ambiguousCommands?.Count > 0)
                {
                    SendReply(player, $"{UI.Colors.FormatError("Ambiguous command '")}"+
                        $"{UI.Colors.FormatInput(args[0])}{UI.Colors.FormatError("'. Could match: ")}" + 
                        string.Join(", ", ambiguousCommands.Select(c => UI.Colors.FormatCommand(c))));
                }
                else
                {
                    SendReply(player, $"{UI.Colors.FormatError("Invalid action!")} Use: " + 
                        string.Join(", ", validCommands.Select(c => UI.Colors.FormatCommand(c))));
                }
                return;
            }

            var container = GetLookingAtContainer(player);
            if (container == null)
            {
                SendReply(player, $"{UI.Colors.FormatError("Error:")} You must be looking at a container!");
                return;
            }

            // Handle the stop command first
            if (action == "stop")
            {
                if (_containerHistory.Remove(container))
                {
                    UpdateAlwaysIndicator(container, false);
                    RemoveBoxState(container);
                    // Clear the skin when stopping always mode
                    if (container.ShortPrefabName == "box.wooden.large")
                    {
                        container.skinID = 0;
                        container.SendNetworkUpdate();
                    }
                    SendReply(player, UI.Colors.FormatSuccess("Container is no longer in always mode."));
                }
                else
                {
                    SendReply(player, UI.Colors.FormatError("Container was not in always mode."));
                }
                return;
            }

            // Check for conflicting always states - but only when setting a new always state
            if (isAlways && _containerHistory.TryGetValue(container, out var existing))
            {
                // Don't block clear operations on containers with always mode
                if (action != "clear")
                {
                    SendReply(player, UI.Colors.FormatError($"This container is already set to always {existing.Command}!"));
                    SendReply(player, $"Use {UI.Colors.FormatCommand("/box stop")} first to change it.");
                    return;
                }
            }

            switch (action)
            {
                case "clear":
                    ClearContainer(container);
                    if (isAlways)
                    {
                        _containerHistory[container] = new BoxHistory 
                        { 
                            Command = "clear",
                            IsPermanent = true
                        };

                        // Apply skin before clearing
                        if (container.ShortPrefabName == "box.wooden.large")
                        {
                            var skin = _skins.FirstOrDefault(s => s.Id == 18);
                            if (skin != null)
                            {
                                container.skinID = skin.WorkshopId;
                                container.SendNetworkUpdate();
                            }
                        }

                        UpdateAlwaysIndicator(container, true);
                        SaveBoxState(container, "clear");
                        SendReply(player, UI.Colors.FormatSuccess("Container cleared! Will always stay empty."));
                    }
                    else
                    {
                        // Don't modify the container history at all for non-always clear
                        SendReply(player, UI.Colors.FormatSuccess("Container cleared!"));

                        // If container has an always mode, trigger a refill after clearing
                        if (_containerHistory.TryGetValue(container, out var history) && history.IsPermanent)
                        {
                            NextTick(() => 
                            {
                                switch (history.Command)
                                {
                                    case "random":
                                        RefillContainerRandom(container);
                                        break;
                                    case "category":
                                        RefillContainerCategory(container, history.Category);
                                        break;
                                }
                            });
                        }
                    }
                    break;

                case "random":
                    if (isAlways)
                    {
                        _containerHistory[container] = new BoxHistory 
                        { 
                            Command = "random",
                            IsPermanent = true
                        };
                        // Apply skin before filling
                        if (container.ShortPrefabName == "box.wooden.large")
                        {
                            var skin = _skins.FirstOrDefault(s => s.Id == 17);
                            if (skin != null)
                            {
                                container.skinID = skin.WorkshopId;
                                container.SendNetworkUpdate();
                            }
                        }
                        UpdateAlwaysIndicator(container, true);
                        FillContainerRandom(container);
                        SaveBoxState(container, "random");
                        SendReply(player, UI.Colors.FormatSuccess("Container filled with random items! Will always stay full."));
                    }
                    else
                    {
                        _containerHistory.Remove(container);
                        UpdateAlwaysIndicator(container, false);
                        FillContainerRandom(container);
                        SendReply(player, UI.Colors.FormatSuccess("Container filled with random items!"));
                    }
                    break;

                case "category":
                    if (args.Length < 2)
                    {
                        SendReply(player, $"Usage: /box category {UI.Colors.FormatCommand("<category>")}");
                        SendReply(player, "Available categories:\n" + 
                            string.Join(", ", _categoryItems.Keys.Select(c => UI.Colors.FormatCategory(c))));
                        return;
                    }

                    var (categoryMatch, ambiguousCategories) = MatchCommand(args[1], _categoryItems.Keys);
                    if (categoryMatch == null)
                    {
                        if (ambiguousCategories?.Count > 0)
                        {
                            SendReply(player, $"{UI.Colors.FormatError("Ambiguous category '")}"+
                                $"{UI.Colors.FormatInput(args[1])}{UI.Colors.FormatError("'. Could match: ")}" +
                                string.Join(", ", ambiguousCategories.Select(c => UI.Colors.FormatCategory(c))));
                        }
                        else
                        {
                            SendReply(player, $"{UI.Colors.FormatError("Invalid category!")} Available:\n" +
                                string.Join(", ", _categoryItems.Keys.Select(c => UI.Colors.FormatCategory(c))));
                        }
                        return;
                    }

                    if (isAlways)
                    {
                        _containerHistory[container] = new BoxHistory 
                        { 
                            Command = "category",
                            Category = categoryMatch,
                            IsPermanent = true
                        };

                        // Apply skin before filling
                        if (container.ShortPrefabName == "box.wooden.large")
                        {
                            var category = _categories.FirstOrDefault(c => 
                                c.Id.Equals(categoryMatch, StringComparison.OrdinalIgnoreCase));

                            if (category?.SkinId != null)
                            {
                                var skin = _skins.FirstOrDefault(s => s.Id == category.SkinId);
                                if (skin != null)
                                {
                                    container.skinID = skin.WorkshopId;
                                    container.SendNetworkUpdate();
                                }
                            }
                        }

                        UpdateAlwaysIndicator(container, true);
                        FillContainerCategory(container, categoryMatch);
                        SaveBoxState(container, "category", categoryMatch);
                        SendReply(player, UI.Colors.FormatSuccess($"Container filled with {categoryMatch} items! Will always stay full."));
                    }
                    else
                    {
                        _containerHistory.Remove(container);
                        UpdateAlwaysIndicator(container, false);
                        FillContainerCategory(container, categoryMatch);
                        SendReply(player, UI.Colors.FormatSuccess($"Container filled with {categoryMatch} items!"));
                    }
                    break;
            }
        }

        private StorageContainer GetLookingAtContainer(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 3f))
            {
                var container = hit.GetEntity() as StorageContainer;
                return container;
            }
            return null;
        }

        private void ClearContainer(StorageContainer container)
        {
            container.inventory.Clear();
        }

        private void FillContainerRandom(StorageContainer container)
        {
            // Only apply skin if container has always mode
            if (_containerHistory.TryGetValue(container, out var history) && history.IsPermanent)
            {
                if (container.ShortPrefabName == "box.wooden.large")
                {
                    // Apply XPOINT Misc skin (ID 17)
                    var skin = _skins.FirstOrDefault(s => s.Id == 17);
                    if (skin != null)
                    {
                        container.skinID = skin.WorkshopId;
                        container.SendNetworkUpdate();
                    }
                }
            }

            var slots = container.inventory.capacity;
            int attempts = 0;
            int maxAttempts = slots * 2;

            while (container.inventory.itemList.Count < slots && attempts < maxAttempts)
            {
                var def = _allDefinitions[UnityEngine.Random.Range(0, _allDefinitions.Length)];
                var amount = UnityEngine.Random.Range(1, def.stackable + 1);
                
                var item = ItemManager.Create(def, amount);
                if (!item.MoveToContainer(container.inventory))
                {
                    item.Remove();
                }
                attempts++;
            }
        }

        private void FillContainerCategory(StorageContainer container, string category)
        {
            var items = _categoryItems[category];
            if (items.Count == 0) return;

            // Only apply skin if container has always mode
            if (_containerHistory.TryGetValue(container, out var history) && history.IsPermanent)
            {
                ApplyCategorySkin(container, category);
            }
            
            var slots = container.inventory.capacity;
            int attempts = 0;
            int maxAttempts = slots * 2;

            while (container.inventory.itemList.Count < slots && attempts < maxAttempts)
            {
                var def = items[UnityEngine.Random.Range(0, items.Count)];
                var amount = UnityEngine.Random.Range(1, def.stackable + 1);
                
                var item = ItemManager.Create(def, amount);
                if (!item.MoveToContainer(container.inventory))
                {
                    item.Remove();
                }
                attempts++;
            }
        }

        private string GetItemCategory(ItemDefinition item)
        {
            if (item == null) return "misc";

            // Map item categories to our defined categories
            if (item.category == ItemCategory.Ammunition) return "ammunition";
            if (item.category == ItemCategory.Attire) return "attire";
            if (item.category == ItemCategory.Component) return "component";
            if (item.category == ItemCategory.Construction) return "construction";
            if (item.category == ItemCategory.Electrical) return "electrical";
            if (item.category == ItemCategory.Food) return "food";
            if (item.category == ItemCategory.Fun) return "fun";
            if (item.category == ItemCategory.Items) return "items";
            if (item.category == ItemCategory.Medical) return "medical";
            if (item.category == ItemCategory.Misc) return "misc";
            if (item.category == ItemCategory.Resources) return "resources";
            if (item.category == ItemCategory.Tool) return "tool";
            if (item.category == ItemCategory.Traps) return "traps";
            if (item.category == ItemCategory.Weapon) return "weapon";
            
            return "misc";
        }

        private bool IsItemObtainable(ItemDefinition item)
        {
            if (item == null) return false;
            if (item.hidden) return false;

            return true;
        }

        private void RefillContainerRandom(StorageContainer container)
        {
            var slots = container.inventory.capacity;
            int attempts = 0;
            int maxAttempts = slots * 2;

            while (container.inventory.itemList.Count < slots && attempts < maxAttempts)
            {
                var def = _allDefinitions[UnityEngine.Random.Range(0, _allDefinitions.Length)];
                var amount = UnityEngine.Random.Range(1, def.stackable + 1);
                
                var item = ItemManager.Create(def, amount);
                if (!item.MoveToContainer(container.inventory))
                {
                    item.Remove();
                }
                attempts++;
            }
        }

        private void RefillContainerCategory(StorageContainer container, string category)
        {
            var items = _categoryItems[category];
            if (items.Count == 0) return;

            var slots = container.inventory.capacity;
            int attempts = 0;
            int maxAttempts = slots * 2;

            while (container.inventory.itemList.Count < slots && attempts < maxAttempts)
            {
                var def = items[UnityEngine.Random.Range(0, items.Count)];
                var amount = UnityEngine.Random.Range(1, def.stackable + 1);
                
                var item = ItemManager.Create(def, amount);
                if (!item.MoveToContainer(container.inventory))
                {
                    item.Remove();
                }
                attempts++;
            }
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            var storageContainer = container.entityOwner as StorageContainer;
            if (storageContainer == null) return;

            if (_containerHistory.TryGetValue(storageContainer, out var history) && 
                history.IsPermanent && 
                history.Command == "clear")
            {
                // Wait a tick to let the item addition complete
                NextTick(() => 
                {
                    if (container.itemList.Count > 0)
                    {
                        ClearContainer(storageContainer);
                    }
                });
            }
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            var storageContainer = container.entityOwner as StorageContainer;
            if (storageContainer == null) return;

            if (_containerHistory.TryGetValue(storageContainer, out var history) && history.IsPermanent)
            {
                // Wait a tick to let the item removal complete
                NextTick(() => 
                {
                    if (container.itemList.Count < container.capacity)
                    {
                        switch (history.Command)
                        {
                            case "random":
                                RefillContainerRandom(storageContainer);
                                break;
                            case "category":
                                RefillContainerCategory(storageContainer, history.Category);
                                break;
                        }
                    }
                });
            }
        }

        // Add this method to create/update indicators
        private void UpdateAlwaysIndicator(StorageContainer container, bool shouldHave)
        {
            // Remove existing indicator if there is one
            if (_alwaysIndicators.TryGetValue(container, out var existing))
            {
                existing.Kill();
                _alwaysIndicators.Remove(container);
            }

            if (shouldHave)
            {
                // Get the appropriate prefab based on the container's mode
                string prefabPath = "assets/bundled/prefabs/modding/events/twitch/br_sphere.prefab";
                
                if (_containerHistory.TryGetValue(container, out var history))
                {
                    switch (history.Command)
                    {
                        case "random":
                            prefabPath = "assets/bundled/prefabs/modding/events/twitch/br_sphere_purple.prefab";
                            break;
                        case "category":
                            prefabPath = "assets/bundled/prefabs/modding/events/twitch/br_sphere_green.prefab";
                            break;
                        case "clear":
                            prefabPath = "assets/bundled/prefabs/modding/events/twitch/br_sphere_red.prefab";
                            break;
                    }
                }

                // Create new indicator
                var pos = container.transform.position + new Vector3(0f, 0.8f, 0f);
                var indicator = GameManager.server.CreateEntity(prefabPath, pos) as BaseEntity;
                if (indicator != null)
                {
                    indicator.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    indicator.Spawn();
                    indicator.SetFlag(BaseEntity.Flags.Locked, true);
                    _alwaysIndicators[container] = indicator;
                }
            }
        }

        // Add cleanup when containers are destroyed
        private void OnEntityKill(StorageContainer container)
        {
            if (container != null)
            {
                if (_alwaysIndicators.TryGetValue(container, out var indicator))
                {
                    indicator.Kill();
                    _alwaysIndicators.Remove(container);
                }
                _containerHistory.Remove(container);
            }
        }

        // Add cleanup on plugin unload
        private void Unload()
        {
            // Clean up all indicators
            foreach (var indicator in _alwaysIndicators.Values)
            {
                if (indicator != null)
                {
                    indicator.Kill();
                }
            }
            _alwaysIndicators.Clear();

            // Clean up selection indicators
            foreach (var indicator in _selectionIndicators.Values)
            {
                if (indicator != null) indicator.Kill();
            }
            _selectionIndicators.Clear();
        }

        private void ApplyCategorySkin(StorageContainer container, string categoryId)
        {
            // Only apply skins to large wooden boxes
            if (container.ShortPrefabName != "box.wooden.large") 
            {
                return;
            }

            var category = _categories.FirstOrDefault(c => 
                c.Id.Equals(categoryId, StringComparison.OrdinalIgnoreCase));

            if (category?.SkinId != null)
            {
                var skin = _skins.FirstOrDefault(s => s.Id == category.SkinId);
                if (skin != null)
                {
                    container.skinID = skin.WorkshopId;
                    container.SendNetworkUpdate();
                }
            }
        }

        // Update box state when setting always mode
        private void SaveBoxState(StorageContainer container, string command, string category = null)
        {
            var key = $"{container.PrefabName}_{container.transform.position}";
            _data.Boxes[key] = new BoxState
            {
                Command = command,
                Category = category,
                IsPermanent = true,
                Position = container.transform.position,
                PrefabName = container.PrefabName
            };
            SaveData();
        }

        // Remove box state when stopping always mode
        private void RemoveBoxState(StorageContainer container)
        {
            var key = $"{container.PrefabName}_{container.transform.position}";
            if (_data.Boxes.Remove(key))
            {
                SaveData();
            }
        }

        // Add this method to handle box selection
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                if (!_selectedBoxes.ContainsKey(player))
                    return;

                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 3f))
                {
                    var container = hit.GetEntity() as StorageContainer;
                    if (container != null)
                    {
                        var selected = _selectedBoxes[player];
                        if (selected.Contains(container))
                        {
                            // Deselect
                            selected.Remove(container);
                            if (_selectionIndicators.TryGetValue(container, out var indicator))
                            {
                                indicator.Kill();
                                _selectionIndicators.Remove(container);
                            }

                            // Show updated selection count if in everything mode
                            if (selected.Count > 0)
                            {
                                int totalItems = _allDefinitions.Length;
                                int itemsPerBox = 48;
                                int boxesNeeded = (int)Math.Ceiling((double)totalItems / itemsPerBox);
                                int totalSlotsNeeded = totalItems;
                                int selectedSlots = selected.Count * itemsPerBox;

                                if (boxesNeeded > selected.Count)
                                {
                                    SendReply(player, UI.Colors.FormatError($"Need {boxesNeeded - selected.Count} more boxes ({totalSlotsNeeded - selectedSlots} more slots) to store all items."));
                                }
                                else
                                {
                                    SendReply(player, UI.Colors.FormatSuccess($"Selected {selected.Count} boxes ({selectedSlots} slots). Ready to distribute items!"));
                                }
                            }
                        }
                        else
                        {
                            // Select
                            selected.Add(container);
                            var pos = container.transform.position + new Vector3(0f, 0.8f, 0f);
                            var indicator = GameManager.server.CreateEntity(
                                "assets/bundled/prefabs/modding/cinematic/cinelights/cinelight_point_green.prefab", 
                                pos) as BaseEntity;
                            
                            if (indicator != null)
                            {
                                indicator.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                                indicator.Spawn();
                                indicator.SetFlag(BaseEntity.Flags.Locked, true);
                                _selectionIndicators[container] = indicator;
                            }

                            // Show updated selection count if in everything mode
                            int totalItems = _allDefinitions.Length;
                            int itemsPerBox = 30;
                            int boxesNeeded = (int)Math.Ceiling((double)totalItems / itemsPerBox);
                            int totalSlotsNeeded = totalItems;
                            int selectedSlots = selected.Count * itemsPerBox;

                            if (boxesNeeded > selected.Count)
                            {
                                SendReply(player, UI.Colors.FormatError($"Need {boxesNeeded - selected.Count} more boxes ({totalSlotsNeeded - selectedSlots} more slots) to store all items."));
                            }
                            else
                            {
                                SendReply(player, UI.Colors.FormatSuccess($"Selected {selected.Count} boxes ({selectedSlots} slots). Ready to distribute items!"));
                            }
                        }
                    }
                }
            }
        }

        // Add this helper method to execute commands on containers
        private void ExecuteBoxCommand(BasePlayer player, StorageContainer targetContainer, string action, string[] args, bool isAlways)
        {
            switch (action)
            {
                case "stop":
                    if (_containerHistory.Remove(targetContainer))
                    {
                        UpdateAlwaysIndicator(targetContainer, false);
                        RemoveBoxState(targetContainer);
                        // Clear the skin when stopping always mode
                        if (targetContainer.ShortPrefabName == "box.wooden.large")
                        {
                            targetContainer.skinID = 0;
                            targetContainer.SendNetworkUpdate();
                        }
                    }
                    break;

                case "clear":
                    ClearContainer(targetContainer);
                    if (isAlways)
                    {
                        _containerHistory[targetContainer] = new BoxHistory 
                        { 
                            Command = "clear",
                            IsPermanent = true
                        };

                        if (targetContainer.ShortPrefabName == "box.wooden.large")
                        {
                            var skin = _skins.FirstOrDefault(s => s.Id == 18);
                            if (skin != null)
                            {
                                targetContainer.skinID = skin.WorkshopId;
                                targetContainer.SendNetworkUpdate();
                            }
                        }

                        UpdateAlwaysIndicator(targetContainer, true);
                        SaveBoxState(targetContainer, "clear");
                    }
                    break;

                case "random":
                    if (isAlways)
                    {
                        _containerHistory[targetContainer] = new BoxHistory 
                        { 
                            Command = "random",
                            IsPermanent = true
                        };
                        if (targetContainer.ShortPrefabName == "box.wooden.large")
                        {
                            var skin = _skins.FirstOrDefault(s => s.Id == 17);
                            if (skin != null)
                            {
                                targetContainer.skinID = skin.WorkshopId;
                                targetContainer.SendNetworkUpdate();
                            }
                        }
                        UpdateAlwaysIndicator(targetContainer, true);
                        FillContainerRandom(targetContainer);
                        SaveBoxState(targetContainer, "random");
                    }
                    else
                    {
                        _containerHistory.Remove(targetContainer);
                        UpdateAlwaysIndicator(targetContainer, false);
                        FillContainerRandom(targetContainer);
                    }
                    break;

                case "category":
                    if (args.Length < 2) return;

                    var (categoryMatch, _) = MatchCommand(args[1], _categoryItems.Keys);
                    if (categoryMatch == null) return;

                    if (isAlways)
                    {
                        _containerHistory[targetContainer] = new BoxHistory 
                        { 
                            Command = "category",
                            Category = categoryMatch,
                            IsPermanent = true
                        };

                        if (targetContainer.ShortPrefabName == "box.wooden.large")
                        {
                            var category = _categories.FirstOrDefault(c => 
                                c.Id.Equals(categoryMatch, StringComparison.OrdinalIgnoreCase));

                            if (category?.SkinId != null)
                            {
                                var skin = _skins.FirstOrDefault(s => s.Id == category.SkinId);
                                if (skin != null)
                                {
                                    targetContainer.skinID = skin.WorkshopId;
                                    targetContainer.SendNetworkUpdate();
                                }
                            }
                        }

                        UpdateAlwaysIndicator(targetContainer, true);
                        FillContainerCategory(targetContainer, categoryMatch);
                        SaveBoxState(targetContainer, "category", categoryMatch);
                    }
                    else
                    {
                        _containerHistory.Remove(targetContainer);
                        UpdateAlwaysIndicator(targetContainer, false);
                        FillContainerCategory(targetContainer, categoryMatch);
                    }
                    break;
            }
        }

        private void HandleBulkMode(BasePlayer player)
        {
            if (_selectedBoxes.ContainsKey(player))
            {
                // Exit bulk mode
                var selected = _selectedBoxes[player];
                _selectedBoxes.Remove(player);

                // Clean up indicators
                foreach (var selectedContainer in selected)
                {
                    if (_selectionIndicators.TryGetValue(selectedContainer, out var indicator))
                    {
                        indicator.Kill();
                        _selectionIndicators.Remove(selectedContainer);
                    }
                }

                SendReply(player, UI.Colors.FormatSuccess($"Exited bulk mode. Selected {selected.Count} boxes."));
                return;
            }
            else
            {
                // Enter bulk mode
                _selectedBoxes[player] = new HashSet<StorageContainer>();
                SendReply(player, UI.Colors.FormatSuccess("Entered bulk mode. Left click boxes to select/deselect them."));
                SendReply(player, "Run /box bulk again when done selecting.");
                return;
            }
        }

        private void HandleSkinCommand(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                SendReply(player, $"Usage: /box skin {UI.Colors.FormatCommand("<skin_name>")}");
                SendReply(player, "Available skins:\n" + 
                    string.Join("\n", _skins.Select(s => UI.Colors.FormatInput(s.Name))));
                return;
            }

            var targetContainer = GetLookingAtContainer(player);
            if (targetContainer == null)
            {
                SendReply(player, $"{UI.Colors.FormatError("Error:")} You must be looking at a container!");
                return;
            }

            if (targetContainer.ShortPrefabName != "box.wooden.large")
            {
                SendReply(player, $"{UI.Colors.FormatError("Error:")} Skins can only be applied to large wooden boxes!");
                return;
            }

            // Get the skin name from all remaining args to support spaces
            string skinName = string.Join(" ", args.Skip(1));

            // Match skin name using our partial matching logic
            var (skinMatch, ambiguousSkins) = MatchCommand(
                skinName, 
                _skins.Select(s => s.Name)
            );

            if (skinMatch == null)
            {
                if (ambiguousSkins?.Count > 0)
                {
                    SendReply(player, $"{UI.Colors.FormatError("Ambiguous skin name '")}"+
                        $"{UI.Colors.FormatInput(skinName)}{UI.Colors.FormatError("'. Could match: ")}" +
                        string.Join(", ", ambiguousSkins.Select(s => UI.Colors.FormatInput(s))));
                }
                else
                {
                    SendReply(player, $"{UI.Colors.FormatError("Invalid skin name!")} Available:\n" +
                        string.Join("\n", _skins.Select(s => UI.Colors.FormatInput(s.Name))));
                }
                return;
            }

            // Find and apply the skin
            var skin = _skins.FirstOrDefault(s => s.Name == skinMatch);
            if (skin != null)
            {
                targetContainer.skinID = skin.WorkshopId;
                targetContainer.SendNetworkUpdate();
                SendReply(player, UI.Colors.FormatSuccess($"Applied skin: {skin.Name}"));
            }
        }

        private void HandleEverythingCommand(BasePlayer player)
        {
            // Calculate total items
            int totalItems = _allDefinitions.Length;

            if (_selectedBoxes.ContainsKey(player))
            {
                var selectedBoxes = _selectedBoxes[player];
                
                // Calculate total available slots
                int totalAvailableSlots = selectedBoxes.Sum(container => container.inventory.capacity);
                
                // Check if we have enough slots
                if (totalAvailableSlots < totalItems)
                {
                    int slotsNeeded = totalItems - totalAvailableSlots;
                    SendReply(player, $"{UI.Colors.FormatError("Error:")} Need {slotsNeeded} more slots to store all items!");
                    SendReply(player, $"Current capacity: {totalAvailableSlots} slots, Need: {totalItems} slots");
                    return;
                }

                // Convert to list to maintain order
                var boxList = selectedBoxes.ToList();
                int currentBox = 0;
                int currentSlot = 0;

                // Distribute items across boxes
                foreach (var itemDef in _allDefinitions)
                {
                    if (currentBox >= boxList.Count) break;

                    var container = boxList[currentBox];
                    var item = ItemManager.CreateByName(itemDef.shortname, 1);
                    if (item != null)
                    {
                        // Move to next box if current is full
                        if (currentSlot >= container.inventory.capacity)
                        {
                            currentBox++;
                            currentSlot = 0;
                            if (currentBox >= boxList.Count) break;
                            container = boxList[currentBox];
                        }

                        if (!item.MoveToContainer(container.inventory))
                        {
                            item.Remove();
                        }
                        else
                        {
                            currentSlot++;
                        }
                    }
                }

                // Clean up selection
                _selectedBoxes.Remove(player);
                foreach (var container in selectedBoxes)
                {
                    if (_selectionIndicators.TryGetValue(container, out var indicator))
                    {
                        indicator.Kill();
                        _selectionIndicators.Remove(container);
                    }
                }

                SendReply(player, UI.Colors.FormatSuccess($"Distributed {totalItems} items across {currentBox + 1} boxes!"));
                return;
            }
            else
            {
                // Enter selection mode
                _selectedBoxes[player] = new HashSet<StorageContainer>();
                SendReply(player, UI.Colors.FormatSuccess($"Please select boxes to store {totalItems} items."));
                SendReply(player, "Left click boxes to select/deselect them.");
                SendReply(player, "Run /box everything again when done selecting.");
                return;
            }
        }

        private void HandleDebugCommand(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                SendReply(player, $"Usage: /box debug {UI.Colors.FormatCommand("<category_name>")}");
                SendReply(player, "Example: /box debug \"High Grade Weapons\"");
                return;
            }

            var targetContainer = GetLookingAtContainer(player);
            if (targetContainer == null)
            {
                SendReply(player, $"{UI.Colors.FormatError("Error:")} You must be looking at a container!");
                return;
            }

            // Get the category name from all remaining args to support spaces
            string categoryName = string.Join(" ", args.Skip(1));

            // Get all items from the container
            var items = new List<DebugItemInfo>();
            
            foreach (var item in targetContainer.inventory.itemList)
            {
                items.Add(new DebugItemInfo 
                { 
                    TargetCategory = null,
                    MaxAmountInOutput = 0,
                    BufferAmount = 0,
                    MinAmountInInput = 0,
                    IsBlueprint = false,
                    BufferTransferRemaining = 0,
                    TargetItemName = item.info.shortname
                });
            }

            // Remove existing category data if it exists
            _debugData.RemoveAll(d => d.Category == categoryName);

            // Add new category data
            _debugData.Add(new DebugCategoryInfo 
            { 
                Category = categoryName,
                ItemIDs = items
            });

            SaveDebugData();

            SendReply(player, UI.Colors.FormatSuccess($"Saved {items.Count} items from container under category '{categoryName}' to debug data."));
        }
    }
}