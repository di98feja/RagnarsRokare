using Stateless;

namespace RagnarsRokare.MobAI
{
    public interface IDynamicBehaviour
    {
        void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState);
        void Update(MobAIBase instance, float dt);
        void Abort();
        string StartState { get; }
        string SuccessState { get; set; }
        string FailState { get; set; }

    }
}
