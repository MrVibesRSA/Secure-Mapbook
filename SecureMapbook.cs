using Microsoft.Extensions.Logging;
using securemapbooke.Helpers;
using securemapbooke.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using System.Reflection;

namespace _securemapbook;

public record ModMetadata : AbstractModMetadata
{
    public override string Name { get; init; } = "SecureMapbook";
    public override string Author { get; init; } = "MrVibesRSA";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.5.5");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; } = true;
    public override string? License { get; init; } = "MIT";
    public override string ModGuid { get; init; } = "com.mrvibesrsa.securemapbook";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 99999)]
public class SecureMapbook(
ISptLogger<SecureMapbook> logger,
DatabaseService databaseService,
CustomItemService customItemService,
ModHelper modHelper

) : IOnLoad
{
    public Task OnLoad()
    {
        var modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = ConfigHelper.LoadConfig(modHelper, modFolder);

        CreateMapbookItem(customItemService, config);
        AddMapbookToMaps(config,databaseService);

        config.SpecialSlotsList = GetSpecialSlotIds(databaseService, config);
        config.SecureContainers = GetSecureContainerIds(databaseService, config);

        if (config.AllowInSecureContainers)
            AllowInSecureContainers(config);

        if (config.AllowInSpecialSlots)
            AllowInSpecialSlots(config);

        if (!config.AllowInsurance)
            DisableMapsInsurance(config);

        logger.Success(ValidateModChanges(config, databaseService, customItemService)); // validate mod changes
        return Task.CompletedTask;
    }

    private void CreateMapbookItem(CustomItemService customItemService, ModConfig config)
    {
        var localesForItem = config.Locales.ToDictionary(
            kvp => kvp.Key,
            kvp => new SPTarkov.Server.Core.Models.Spt.Mod.LocaleDetails
            {
                Name = kvp.Value.Name,
                ShortName = kvp.Value.ShortName,
                Description = kvp.Value.Description
            }
        );

        var mapbookDetails = new NewItemFromCloneDetails
        {
            ItemTplToClone = config.CloneId,
            NewId = config.MapbookItemId,
            ParentId = config.ParentId,
            FleaPriceRoubles = config.Price,
            HandbookPriceRoubles = config.Price,
            HandbookParentId = config.HandbookParentId,
            Locales = localesForItem,
            OverrideProperties = GetMapbookProperties(config)
        };

        if (config.EnableDebugging)
        {
            logger.Info($"[SecureMapbook] Created Mapbook with ID: {config.MapbookItemId}");
        }

        // Create the item via SPT service
        customItemService.CreateItemFromClone(mapbookDetails);
        AddToTraderAssort(new MongoId(config.TraderId), new MongoId(config.MapbookItemId), config);
    }

    private TemplateItemProperties GetMapbookProperties(ModConfig config)
    {
        return new TemplateItemProperties
        {
            Name = "Secure Mapbook",
            ShortName = "Mapbook",
            Description = "A meticulously crafted book designed for storing and organizing maps...",

            Prefab = new Prefab
            {
                Path = "assets/content/items/barter/item_mapbook/mapbook.bundle"
            },
            Width = config.Size.Width,
            Height = config.Size.Height,
            CanPutIntoDuringTheRaid = true,
            RaidModdable = true,
            InsuranceDisabled = !config.AllowInsurance,
            CanSellOnRagfair = false,
            ItemSound = "item_book",
            Grids = new List<Grid>(),
            Slots = GenerateMapSlots(config),
            ExaminedByDefault = false
        };
    }

    private List<Slot> GenerateMapSlots(ModConfig config)
    {
        var slots = new List<Slot>();
        var mapEntries = config.Maps;

        int i = 0;
        foreach (var kvp in mapEntries)
        {
            string mapName = kvp.Key;
            string mapId = kvp.Value;

            var slot = new Slot
            {
                Name = $"mod_mount_{(i + 1).ToString().PadLeft(2, '0')}",
                Id = $"5d235bb686f77443f433127{(char)('b' + i)}",
                Parent = "55818b224bdc2dde698b456f",
                Properties = new SlotProperties
                {
                    
                    Filters = new List<SlotFilter>
                    {
                        new SlotFilter
                        {
                             Filter = new HashSet<MongoId> { new MongoId(mapId) }
                        }
                    }
                },
                Required = false,
                MergeSlotWithChildren = false,
                Prototype = "55d4af244bdc2d962f8b4571"
            };

            slots.Add(slot);

            if (config.EnableDebugging)
            {
                logger.Info($"[SecureMapbook] Added slot for {mapName} ({mapId})");
            }

            i++;
        }

        return slots;
    }

    private void AddToTraderAssort(MongoId traderId, MongoId itemId, ModConfig config)
    {
        var assort = databaseService.GetTrader(traderId).Assort;

        // --- Level 1: Barter version ---
        if (config.BarterItems != null && config.BarterItems.Count > 0)
        {
            var barterAssortId = new MongoId(); // unique ID for barter entry

            var barterItem = new Item
            {
                Id = barterAssortId,
                Template = itemId,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = false,
                    StackObjectsCount = 1,
                    BuyRestrictionMax = 50,
                    BuyRestrictionCurrent = 0
                }
            };

            assort.Items.Add(barterItem);

            var barterList = new List<BarterScheme>();
            foreach (var item in config.BarterItems)
            {
                barterList.Add(new BarterScheme
                {
                    Template = item.ItemId,
                    Count = item.Count
                });
            }

            assort.BarterScheme[barterAssortId] = new List<List<BarterScheme>> { barterList };
            assort.LoyalLevelItems[barterAssortId] = config.LoyaltyLevelBarter;

            if (config.EnableDebugging)
            {
                logger.Info($"[SecureMapbook] Added barter version (Lvl {config.LoyaltyLevelBarter}) of {itemId} to trader {traderId}");
            }
        }

        // --- Level 2: Cash purchase version ---
        var cashAssortId = new MongoId(); // unique ID for cash entry

        var cashItem = new Item
        {
            Id = cashAssortId,
            Template = itemId,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                UnlimitedCount = false,
                StackObjectsCount = 1,
                BuyRestrictionMax = 50,
                BuyRestrictionCurrent = 0
            }
        };

        assort.Items.Add(cashItem);

        var roubleScheme = new List<BarterScheme>
        {
            new BarterScheme
            {
                Template = ItemTpl.MONEY_ROUBLES,
                Count = config.Price
            }
        };

        assort.BarterScheme[cashAssortId] = new List<List<BarterScheme>> { roubleScheme };
        assort.LoyalLevelItems[cashAssortId] = config.LoyaltyLevelBuy;

        if (config.EnableDebugging)
        {
            logger.Info($"[SecureMapbook] Added cash version (Lvl {config.LoyaltyLevelBuy}) of {itemId} to trader {traderId} for {config.Price} roubles");
        }
    }

    private void AddMapbookToMaps(ModConfig config, DatabaseService databaseService)
    {
        // Small spawn probability
        const float spawnChance = 1f;

        var locations = databaseService.GetLocations();

        foreach (var mapProperty in locations.GetType().GetProperties())
        {
            var mapName = mapProperty.Name.ToLower();
            var mapValue = mapProperty.GetValue(locations);

            if (mapValue == null) continue;

            var staticLootProp = mapValue.GetType().GetProperty("StaticLoot");
            if (staticLootProp == null) continue;

            var staticLoot = staticLootProp.GetValue(mapValue) as IDictionary<string, dynamic>;
            if (staticLoot == null) continue;

            foreach (var container in staticLoot.Values)
            {
                var itemDistProp = container.GetType().GetProperty("ItemDistribution");
                if (itemDistProp == null) continue;

                var itemDist = itemDistProp.GetValue(container) as IList<dynamic>;
                if (itemDist == null) continue;

                itemDist.Add(new
                {
                    tpl = config.MapbookItemId,
                    relativeProbability = spawnChance
                });
            }
        }
    }

    public Dictionary<string, string> GetSecureContainerIds(DatabaseService databaseService, ModConfig config)
    {
        var result = new Dictionary<string, string>();
        var allItems = databaseService.GetItems();

        foreach (var kvp in allItems)
        {
            var tpl = kvp.Value;
            if (tpl == null || tpl.Parent == null)
                continue;

            // Secure containers share the parent template ID below
            if (string.Equals(tpl.Parent, "5448bf274bdc2dfc2f8b456a", StringComparison.OrdinalIgnoreCase))
            {
                // Key = ID, Value = Name (or fallback to "Unknown")
                var name = tpl.Name ?? "Unknown";
                if (!result.ContainsKey(kvp.Key))
                    result.Add(kvp.Key, name);

                if (config.EnableDebugging)
                    logger.Info($"[SecureMapbook] Found secure container: {name} ({kvp.Key})");
            }
        }

        return result;
    }

    private void AllowInSecureContainers(ModConfig config)
    {
        var containerIds = config.SecureContainers.Keys.Concat(config.OrganizationalPouch.Keys);

        foreach (var containerId in containerIds)
        {
            try
            {
                var container = databaseService.GetItems()[new MongoId(containerId)];
                container.Properties ??= new TemplateItemProperties();

                var grids = container.Properties.Grids?.ToList() ?? new List<Grid>();
                if (!grids.Any())
                    continue;

                var firstGrid = grids[0];
                firstGrid.Properties ??= new GridProperties();

                var filters = firstGrid.Properties.Filters?.ToList() ?? new List<GridFilter>();
                if (!filters.Any())
                {
                    filters.Add(new GridFilter { Filter = new HashSet<MongoId>() });
                }

                filters[0].Filter.Add(new MongoId(config.MapbookItemId));

                firstGrid.Properties.Filters = filters;

                if (config.EnableDebugging)
                    logger.Info($"[SecureMapbook] Added Mapbook to container {containerId}");
            }
            catch (Exception ex)
            {
                logger.Error($"[SecureMapbook] Failed to add Mapbook to container {containerId}: {ex}");
            }
        }
    }

    public List<string> GetSpecialSlotIds(DatabaseService databaseService, ModConfig config)
    {
        var result = new List<string>();

        var allItems = databaseService.GetItems();
        foreach (var kvp in allItems)
        {
            var tpl = kvp.Value;
            if (tpl?.Properties?.Slots == null)
                continue;

            foreach (var slot in tpl.Properties.Slots)
            {
                if (slot?.Id == null)
                    continue;

                if (slot.Name.Contains("SpecialSlot", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(slot.Id);
                    if (config.EnableDebugging)
                        logger.Info($"[SecureMapbook] Found Special Slot, {slot.Name} ({slot.Id})");
                }
            }
        }

        return result.Distinct().ToList();
    }

    private void AllowInSpecialSlots(ModConfig config)
    {
        var allItems = databaseService.GetItems();

        foreach (var kvp in allItems)
        {
            var item = kvp.Value;
            if (item?.Properties?.Slots == null)
                continue;

            foreach (var slot in item.Properties.Slots)
            {
                if (config.SpecialSlotsList.Contains(slot.Id))
                {
                    slot.Properties ??= new SlotProperties();

                    // Safely convert Filters to a List
                    var filters = slot.Properties.Filters?.ToList() ?? new List<SlotFilter>();

                    if (!filters.Any())
                    {
                        filters.Add(new SlotFilter { Filter = new HashSet<MongoId>() });
                    }

                    filters[0].Filter.Add(new MongoId(config.MapbookItemId));

                    // Assign back as IEnumerable
                    slot.Properties.Filters = filters;

                    if (config.EnableDebugging)
                        logger.Info($"[SecureMapbook] Added Mapbook to special slot in container {kvp.Key}");
                }
            }
        }
    }

    private void DisableMapsInsurance(ModConfig config)
    {
        try
        {
            // Disable insurance for the Mapbook itself
            var mapbook = databaseService.GetItems()[new MongoId(config.MapbookItemId)];
            mapbook.Properties ??= new TemplateItemProperties();
            mapbook.Properties.InsuranceDisabled = true;

            if (config.EnableDebugging)
                logger.Info($"[SecureMapbook] Insurance disabled for Mapbook {config.MapbookItemId}");

            // Disable insurance for all maps listed in config.Maps
            foreach (var kvp in config.Maps)
            {
                string mapName = kvp.Key;
                string mapId = kvp.Value;

                try
                {
                    var mapItem = databaseService.GetItems()[new MongoId(mapId)];
                    mapItem.Properties ??= new TemplateItemProperties();
                    mapItem.Properties.InsuranceDisabled = true;

                    if (config.EnableDebugging)
                        logger.Info($"[SecureMapbook] Insurance disabled for map '{mapName}' ({mapId})");
                }
                catch (Exception innerEx)
                {
                    logger.Error($"[SecureMapbook] Failed to disable insurance for map '{mapName}' ({mapId}): {innerEx}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[SecureMapbook] Failed during DisableMapsInsurance process: {ex}");
        }
    }

    private string ValidateModChanges(ModConfig config, DatabaseService databaseService, CustomItemService customItemService)
    {
        bool success = true;

        try
        {
            // 1. Validate that the mapbook item exists
            var items = databaseService.GetItems();
            if (!items.ContainsKey(new MongoId(config.MapbookItemId)))
            {
                logger.Error($"[SecureMapbook] Mapbook item {config.MapbookItemId} not found in item database.");
                success = false;
            }

            // 2. Validate that the mapbook has the correct number of slots
            var mapbook = items.GetValueOrDefault(new MongoId(config.MapbookItemId));
            var slots = mapbook?.Properties?.Slots?.ToList();
            if (slots == null || slots.Count == 0)
            {
                logger.Warning($"[SecureMapbook] Mapbook item has no slots defined.");
                success = false;
            }

            // 3. Validate trader assort entries
            var trader = databaseService.GetTrader(new MongoId(config.TraderId));
            var foundTraderEntry = trader.Assort.Items.Any(i => i.Template == new MongoId(config.MapbookItemId));
            if (!foundTraderEntry)
            {
                logger.Error($"[SecureMapbook] ❌ Trader {config.TraderId} has no assort entry for the Mapbook.");
                success = false;
            }

            // 4. Validate secure container filters
            foreach (var kvp in config.SecureContainers)
            {
                var containerId = kvp.Key;
                var container = items.GetValueOrDefault(new MongoId(containerId));
                if (container?.Properties?.Grids == null)
                    continue;

                bool found = container.Properties.Grids
                    .Any(g => g.Properties?.Filters?.Any(f => f.Filter.Contains(new MongoId(config.MapbookItemId))) == true);

                if (!found)
                {
                    logger.Warning($"[SecureMapbook] Mapbook missing from container filters {kvp.Value} ({containerId})");
                    success = false;
                }
            }

            // 5. Validate insurance state
            bool insuranceCorrect = mapbook?.Properties?.InsuranceDisabled == !config.AllowInsurance;
            if (!insuranceCorrect)
            {
                logger.Warning($"[SecureMapbook] Insurance state mismatch for Mapbook {config.MapbookItemId}");
                success = false;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[SecureMapbook] ❌ Validation failed with exception: {ex}");
            success = false;
        }

        if (success)
            return "[SecureMapbook] loaded successfully!";
        else
            return "[SecureMapbook] Some mod config changes failed validation.";
    }

}
