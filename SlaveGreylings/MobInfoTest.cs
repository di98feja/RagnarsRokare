using RagnarsRokare.MobAI;

namespace RagnarsRokare.SlaveGreylings
{
    class MobInfoTest : IControllableMob
    {
        public MobAIInfo GetMobAIInfo()
        {
            return new MobAIInfo
            {
                AIType = this.GetType(),
                Name = "MobInfoTest"
            };
        }
    }
}
