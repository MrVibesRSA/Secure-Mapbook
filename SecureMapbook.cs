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
    public override SemanticVersioning.Version Version { get; init; } = new("1.5.4");
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

        if (config.AllowInSecureContainers)
            AllowInSecureContainers(config);

        if (config.AllowInSpecialSlots)
            AllowInSpecialSlots(config);

        if (!config.AllowInsurance)
            DisableMapsInsurance(config);

        logger.Success("[SecureMapbook] Successfully loaded.");
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
            // Prefab reference
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
                Parent = config.ParentId, //"55818b224bdc2dde698b456f"
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

    private void AllowInSecureContainers(ModConfig config)
    {
        var containerIds = config.SecureContainers.Values
            .Concat(config.OrganizationalPouch.Values);

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

    private void AllowInSpecialSlots(ModConfig config)
    {
        foreach (var slotId in config.SpecialSlotsList)
        {
            try
            {
                var slotContainer = databaseService.GetItems()[new MongoId(slotId)];
                
                if (slotContainer.Properties.Slots == null)
                    continue;

                var slots = slotContainer.Properties.Slots.ToList();

                foreach (var slot in slots)
                {
                    slot.Properties ??= new SlotProperties();

                    var filters = slot.Properties.Filters?.ToList() ?? new List<SlotFilter>();
                    if (!filters.Any())
                    {
                        filters.Add(new SlotFilter { Filter = new HashSet<MongoId>() });
                    }

                    filters[0].Filter.Add(new MongoId(config.MapbookItemId));

                    slot.Properties.Filters = filters;
                }

                slotContainer.Properties.Slots = slots;

                if (config.EnableDebugging)
                    logger.Info($"[SecureMapbook] Added Mapbook to special slot container {slotId}");
            }
            catch (Exception ex)
            {
                logger.Error($"[SecureMapbook] Failed to add Mapbook to special slot container {slotId}: {ex}");
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
}
