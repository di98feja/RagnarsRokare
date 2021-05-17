using System;

namespace RagnarsRokare.MobAI
{
    public class FixerAIConfig : MobAIBaseConfig
    {
        public int PostTameFeedDuration = 1000;
        public int TimeLimitOnAssignment = 30;
        public string[] IncludedContainers = new string[] { "piece_chest_wood" };

        [Obsolete]
        public int AssignmentSearchRadius = 30;
        [Obsolete]
        public int ItemSearchRadius = 10;
        [Obsolete]
        public int ContainerSearchRadius = 10;
        [Obsolete]
        public int MaxContainersInMemory = 5;

    }
}
