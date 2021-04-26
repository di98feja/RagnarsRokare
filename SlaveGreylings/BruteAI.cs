using RagnarsRokare.MobAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlaveGreylings
{
    public class BruteAI : MobAIBase, IControllableMob
    {
        public override void Follow(Player player)
        {
            NView.InvokeRPC(ZNetView.Everybody, Constants.Z_MobCommand, player.GetZDOID(), "Follow");
        }

        public MobInfo GetMobInfo()
        {
            return new MobInfo
            {
                FeedDuration = 100,
                TamingTime = 1000,
                Name = "Brute",
                PreTameConsumables = new List<ItemDrop> { ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Dandelion").Single() },
                PostTameConsumables = new List<ItemDrop> { ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Dandelion").Single() }
            };
        }

        protected override void RPC_MobCommand(long sender, ZDOID playerId, string command)
        {
            Player player = GetPlayer(playerId);
            if (!(player == null) && command == "Follow")
            {
                {
                    (Instance as MonsterAI).ResetPatrolPoint();
                    (Instance as MonsterAI).SetFollowTarget(player.gameObject);
                }
            }
        }
    }
}
