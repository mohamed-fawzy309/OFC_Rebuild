namespace Football.Core
{
    public interface IStateMachine<TStateId> where TStateId : System.Enum
    {
        TStateId CurrentStateId { get; }
        IState CurrentState { get; }
        void ChangeState(TStateId stateId);
        void RegisterState(TStateId stateId, IState state);
        void Tick(float deltaTime);
    }
}
