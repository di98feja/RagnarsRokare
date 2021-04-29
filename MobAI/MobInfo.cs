using System;
using System.Collections.Generic;

namespace RagnarsRokare.MobAI
{
    public class MobInfo
    {
        public string Name { get; set; }
        public Type AIType { get; set; }
        public IEnumerable<ItemDrop> PreTameConsumables { get; set; }
        public IEnumerable<ItemDrop> PostTameConsumables { get; set; }
        public float PreTameFeedDuration { get; set; }
        public float PostTameFeedDuration { get; set; }
        public float TamingTime { get; set; }
    }
}
