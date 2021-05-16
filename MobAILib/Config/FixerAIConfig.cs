namespace RagnarsRokare.MobAI
{
    public class FixerAIConfig : MobAIBaseConfig
    {
        public int PostTameFeedDuration = 1000;
        public int AssignmentSearchRadius = 30;
        public int ItemSearchRadius = 10;
        public int ContainerSearchRadius = 10;
        public int MaxContainersInMemory = 5;
        public int TimeLimitOnAssignment = 30;
        public string[] IncludedContainers = new string[] { "piece_chest_wood" };
    }
}
