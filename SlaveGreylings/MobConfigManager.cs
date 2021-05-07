using System.Collections.Generic;

namespace RagnarsRokare.SlaveGreylings
{
    public class MobConfig
    {
        public IEnumerable<ItemDrop> PreTameConsumables { get; set; }
        public IEnumerable<ItemDrop> PostTameConsumables { get; set; }
        public float PreTameFeedDuration { get; set; }
        public float PostTameFeedDuration { get; set; }
        public float TamingTime { get; set; }
    }

    public static class MobConfigManager
    {
        public static bool IsControllableMob(string mobType)
        {
            if (mobType == "Greyling") return true;
            if (mobType == "Greydwarf_Elite") return true;
            return false;
        }

        public static MobConfig GetMobConfig(string mobType)
        {
            switch (mobType)
            {
                case "Greyling":
                    {
                        return new MobConfig
                        {
                            PostTameConsumables = GreylingsConfig.PostTameConsumables,
                            PostTameFeedDuration = GreylingsConfig.FeedDuration.Value,
                            PreTameConsumables = GreylingsConfig.PreTameConsumables,
                            PreTameFeedDuration = GreylingsConfig.FeedDuration.Value,
                            TamingTime = GreylingsConfig.TamingTime.Value
                        };
                    }
                case "Greydwarf_Elite":
                    {
                        return new MobConfig
                        {
                            PostTameConsumables = BruteConfig.PostTameConsumables,
                            PostTameFeedDuration = BruteConfig.PostTameFeedDuration.Value,
                            PreTameConsumables = BruteConfig.PreTameConsumables,
                            PreTameFeedDuration = BruteConfig.PreTameFeedDuration.Value,
                            TamingTime = BruteConfig.TamingTime.Value
                        };
                    }
                default:
                    return null;
            }
        }
    }
}
