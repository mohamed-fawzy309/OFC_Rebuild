namespace Football.Core
{
    public interface IStateMachine
    {
        IState CurrentState { get; }
        void ChangeState(IState newState);
        void Tick(float deltaTime);
    }
}
