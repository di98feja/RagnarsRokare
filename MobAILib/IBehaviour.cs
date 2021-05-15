using Stateless;

namespace RagnarsRokare.MobAI
{
    public interface IBehaviour
    {
        void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState);
        void Update(MobAIBase instance, float dt);
        string StartState { get; }
    }
}
