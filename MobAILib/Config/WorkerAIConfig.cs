using System;

namespace RagnarsRokare.MobAI
{
    public class WorkerAIConfig : MobAIBaseConfig
    {
        public int FeedDuration = 1000;
        public string[] IncludedContainers = new string[] { "piece_chest_wood" };
        public int TimeBeforeAssignmentCanBeRepeated = 120;
        public int TimeLimitOnAssignment = 60;

        [Obsolete]
        public int MaxContainersInMemory = 3;
        [Obsolete]
        public int AssignmentSearchRadius = 30;
        [Obsolete]
        public int ItemSearchRadius = 10;
        [Obsolete]
        public int ContainerSearchRadius = 10;
    }
}
