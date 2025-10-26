namespace securemapbooke.Models
{
    public class ModConfig
    {
        public bool EnableDebugging { get; set; }
        public string MapbookItemId { get; set; } = string.Empty;
        public string CloneId { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
        public string HandbookParentId { get; set; } = string.Empty;
        public string TraderId { get; set; } = string.Empty;
        public int Price { get; set; }
        public int LoyaltyLevelBuy { get; set; }
        public int LoyaltyLevelBarter { get; set; }
        public List<BarterItem> BarterItems { get; set; } = new();
        public bool AllowInsurance { get; set; }
        public bool AllowInSecureContainers { get; set; }
        public bool AllowInSpecialSlots { get; set; }
        public List<string> SpecialSlotsList { get; set; } = new List<string>();
        public Dictionary<string, string> SecureContainers { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> OrganizationalPouch { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Maps { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, LocaleDetails> Locales { get; set; } = new();
        public ItemSize Size { get; set; } = new ItemSize();
    }

    public class LocalesConfig
    {
        public Dictionary<string, LocaleDetails> Locales { get; set; } = new();
    }

    public class BarterConfig
    {
        public List<BarterItem> BarterItems { get; set; } = new();
        public int LoyaltyLevelBarter { get; set; }
    }

    public class ContainersConfig
    {
        public List<string> SpecialSlotsList { get; set; } = new List<string>();
        public Dictionary<string, string> SecureContainers { get; set; } = new();
        public Dictionary<string, string> OrganizationalPouch { get; set; } = new();
    }
}
