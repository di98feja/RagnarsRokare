using RagnarsRokare.MobAI;

namespace RagnarsRokare.SlaveGreylings
{
    class MobInfoTest : IControllableMob
    {
        public MobInfo GetMobInfo()
        {
            return new MobInfo
            {
                AIType = this.GetType(),
                Name = "MobInfoTest"
            };
        }
    }
}
