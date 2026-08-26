using UnityEngine;

namespace Football.Core
{
    public abstract class StateBase : IState
    {
        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Tick(float deltaTime) { }
    }
}
