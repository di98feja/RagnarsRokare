using RagnarsRokare.MobAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlaveGreylings
{
    public static class MobManager
    {
        public static Dictionary<string, MobAIBase> Mobs = new Dictionary<string, MobAIBase>();

        public static bool IsControlledMob(string id)
        {
            return Mobs.ContainsKey(id);
        }
    }
}
