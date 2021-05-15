namespace RagnarsRokare.MobAI
{
    public class WorkerAIConfig
    {
        public int FeedDuration = 1000;
        public int AssignmentSearchRadius = 30;
        public int ItemSearchRadius = 10;
        public int ContainerSearchRadius = 10;
        public string[] IncludedContainers = new string[] { "piece_chest_wood" };
        public int MaxContainersInMemory = 3;
        public int TimeBeforeAssignmentCanBeRepeated = 120;
        public int TimeLimitOnAssignment = 60;
        public int AggressionLevel = 2; // 1-10
        public int MobilityLevel = 10; // 1-10
        public int AwarenessLevel = 10; // 1-10
    }
}
