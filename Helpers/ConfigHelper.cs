using securemapbooke.Models;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace securemapbooke.Helpers
{
    public static class ConfigHelper
    {
        public static ModConfig LoadConfig(ModHelper modHelper, string modFolder)
        {
            var mapbook = modHelper.GetJsonDataFromFile<ModConfig>(modFolder, "config/config.json");
            var barter = modHelper.GetJsonDataFromFile<BarterConfig>(modFolder, "config/barter.json");
            var containers = modHelper.GetJsonDataFromFile<ContainersConfig>(modFolder, "config/containers.json");
            var locales = modHelper.GetJsonDataFromFile<LocalesConfig>(modFolder, "config/locales.json");

            return new ModConfig
            {
                EnableDebugging = mapbook.EnableDebugging,
                CloneId = mapbook.CloneId,
                ParentId = mapbook.ParentId,
                HandbookParentId = mapbook.HandbookParentId,
                MapbookItemId = mapbook.MapbookItemId,
                TraderId = mapbook.TraderId,
                Price = mapbook.Price,
                LoyaltyLevelBuy = mapbook.LoyaltyLevelBuy,
                LoyaltyLevelBarter = barter.LoyaltyLevelBarter,
                BarterItems = barter.BarterItems,
                Size = mapbook.Size,
                AllowInsurance = mapbook.AllowInsurance,
                AllowInSecureContainers = mapbook.AllowInSecureContainers,
                AllowInSpecialSlots = mapbook.AllowInSpecialSlots,
                SpecialSlotsList = containers.SpecialSlotsList,
                SecureContainers = containers.SecureContainers,
                OrganizationalPouch = containers.OrganizationalPouch,
                Maps = mapbook.Maps,
                Locales = locales.Locales
            };

        }
    }
}
