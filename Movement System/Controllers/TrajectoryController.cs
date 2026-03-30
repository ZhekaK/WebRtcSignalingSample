using Movement.Trajectories;
using UnityEngine;

namespace Movement.Controllers
{
    public class TrajectoryController : AbstractMovementController
    {
        private enum MoveState { OnLine, OnTurn }

        private readonly Trajectory _traj;

        private MoveState _state = MoveState.OnLine;
        private bool _isOver = false;
        private int _turnIndex;
        private float _posOnTurn;
        private float _turnLength; // Current Turn Lenght (m)
        private float _speed; // Speed (m/s)
        public float Speed
        {
            get => _speed * 3600f / 1000f; // m/s to km/h (For Inspector)
            set => _speed = Mathf.Clamp(value * 1000f / 3600f, 1, float.MaxValue); // km/h to m/s (For Work)
        }

        public TrajectoryController(MovementMover mover)
        {
            ID = mover.ID;
            _traj = mover.Trajectory;

            Initialize();
        }

        private bool IsValid => _traj && _traj.IsValid();

        private bool IsPositionsEquals(Vector3 pos1, Vector3 pos2) => Vector3.Distance(pos1, pos2) == 0;


        public override void Initialize()
        {
            if (!IsValid) return;

            _state = MoveState.OnLine;
            _isOver = false;
            _turnIndex = 1;
            _posOnTurn = 0;
            _turnLength = UtilityCurves.GetCurveLength(_traj.Turns[_turnIndex].Start, _traj.Turns[_turnIndex].Apex, _traj.Turns[_turnIndex].End, _traj.Turns[_turnIndex].Size);
            Speed = _traj.Turns[0].Speed;

            _unityTransform.position = _traj.Turns[0].Apex;
            _unityTransform.rotation = Quaternion.LookRotation(_traj.Turns[1].Apex - _traj.Turns[0].Apex);
        }

        public override void UpdateMovers()
        {
            if (!IsValid) return;

            if (_isOver) return;

            CalculateRotation();
            CalculatePosition();
            CalculateMoveState();
            _isOver = IsPositionsEquals(_unityTransform.position, _traj.Turns[^1].End);

            base.UpdateMovers();
        }

        protected override void CalculatePosition()
        {
            if (_state == MoveState.OnLine)
            {
                Speed = GetSpeed();
                _unityTransform.position = Vector3.MoveTowards(_unityTransform.position, _traj.Turns[_turnIndex].Start, _speed * Time.deltaTime);
            }

            if (_state == MoveState.OnTurn)
            {
                _posOnTurn = _posOnTurn + _speed * Time.deltaTime / _turnLength;
                _unityTransform.position = UtilityCurves.GetCurveWSPoint(_traj.Turns[_turnIndex].Start, _traj.Turns[_turnIndex].Apex, _traj.Turns[_turnIndex].End, _posOnTurn);
            }
        }

        protected override void CalculateRotation()
        {
            Vector3 direction = Vector3.zero;

            if (_state == MoveState.OnLine)
                direction = (_traj.Turns[_turnIndex].Start - _unityTransform.position).normalized;

            if (_state == MoveState.OnTurn)
                direction = UtilityCurves.GetCurveDirection(_traj.Turns[_turnIndex].Start, _traj.Turns[_turnIndex].Apex, _traj.Turns[_turnIndex].End, _posOnTurn);

            _unityTransform.rotation = Quaternion.LookRotation(direction);
        }

        private void CalculateMoveState()
        {
            if (_state == MoveState.OnTurn)
            {
                if (IsPositionsEquals(_unityTransform.position, _traj.Turns[_turnIndex].End))
                {
                    _posOnTurn = 0;
                    _turnIndex = Mathf.Clamp(_turnIndex + 1, 0, _traj.Turns.Count - 1);
                    _turnLength = UtilityCurves.GetCurveLength(_traj.Turns[_turnIndex].Start, _traj.Turns[_turnIndex].Apex, _traj.Turns[_turnIndex].End, _traj.Turns[_turnIndex].Size);
                    _state = MoveState.OnLine;
                }
            }

            if (_state == MoveState.OnLine)
            {
                if (IsPositionsEquals(_unityTransform.position, _traj.Turns[_turnIndex].Start))
                {
                    _state = MoveState.OnTurn;
                }
            }
        }

        private float GetSpeed()
        {
            float lineTraveledDist = Vector3.Distance(_unityTransform.position, _traj.Turns[_turnIndex - 1].End);
            float lineFullDist = Vector3.Distance(_traj.Turns[_turnIndex - 1].Start, _traj.Turns[_turnIndex - 1].End);

            return Mathf.Lerp(_traj.Turns[_turnIndex - 1].Speed, _traj.Turns[_turnIndex].Speed, lineTraveledDist / lineFullDist);
        }
    }
}