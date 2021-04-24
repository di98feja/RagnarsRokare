using System.Collections.Generic;

namespace RagnarsRokare.MobAI
{
    public class MobInfo
    {
        public string Name { get; set; }
        public IEnumerable<ItemDrop> PreTameConsumables { get; set; }
        public IEnumerable<ItemDrop> PostTameConsumables { get; set; }
        public float FeedDuration { get; set; }
        public float TamingTime { get; set; }
    }
}
