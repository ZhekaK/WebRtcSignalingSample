using System.Collections.Generic;

namespace Movement.Controllers
{
    public abstract class AbstractMovementController
    {
        public string ID { get; protected set; }

        protected HashSet<MovementMover> _movers = new();
        protected UnityTransform _unityTransform;

        public bool IsEmpty => _movers.Count == 0;

        public virtual void Initialize() { }

        /// <summary> Subscribe MovementMover to current Controller </summary>
        public void SubscribeMover(MovementMover mover) { _movers.Add(mover); }

        /// <summary> Unsubscribe MovementMover from current Controller </summary>
        public void UnsubscribeMover(MovementMover mover) { _movers.Remove(mover); }

        /// <summary> Update all subscribed MovementMovers from calculated transform </summary>
        public virtual void UpdateMovers() { foreach (MovementMover mover in _movers) mover.UpdateTransform(_unityTransform); }

        /// <summary> Dispose current controller </summary>
        public virtual void Dispose() { }

        protected virtual void CalculatePosition() { }

        protected virtual void CalculateRotation() { }
    }
}