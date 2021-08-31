namespace RagnarsRokare.MobAI
{
    public interface IFightBehaviour : IBehaviour
    {
        string SuccessState { get; set; }
        string FailState { get; set; }
        float MobilityLevel { get; set; }
        float AgressionLevel { get; set; }
        float AwarenessLevel { get; set; }
    }
}
