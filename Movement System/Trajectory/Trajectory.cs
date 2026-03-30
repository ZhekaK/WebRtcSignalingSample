using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movement.Trajectories
{
    public class Trajectory : MonoBehaviour
    {
        [field: SerializeField] public string Name { get; private set; }

        [Serializable]
        public class Turn
        {
            /// <summary> Turn-start world space position </summary>
            [DisableEdit] public Vector3 Start;
            /// <summary> Turn-apex world space position </summary>
            public Vector3 Apex;
            /// <summary> Turn-end world space position </summary>
            [DisableEdit] public Vector3 End;
            /// <summary> Turn size (symmetric) </summary>
            [Min(1)] public float Size = 1;
            /// <summary> Speed on turn </summary>
            [Min(1)] public float Speed = 1;
        }
        [field: SerializeField] public List<Turn> Turns { get; private set; } = new();

        public bool IsValid() => Turns.Count >= 2;


#if UNITY_EDITOR

        private void OnValidate()
        {
            this.ValidateTurnsSizes();
            UtilityTrajectory.GenerateTrajectory(this, 1);
        }

#endif
    }
}