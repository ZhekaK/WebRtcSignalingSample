using UnityEngine;

namespace Movement.Trajectories
{
    public static class TrajectoryExtensions
    {
        /// <summary> Add new Turn in trajectory </summary>
        public static void AddTurn(this Trajectory trajectory, Vector3 apexPosition)
        {
            Trajectory.Turn turn = new() { Apex = apexPosition };
            trajectory.Turns.Add(turn);
            trajectory.ValidateTurnsSizes();
        }

        /// <summary> Remove Turn with target index from trajectory </summary>
        public static void RemoveTurnAt(this Trajectory trajectory, int turnIndex)
        {
            if (!trajectory.IsIndexValid(turnIndex)) return;

            trajectory.Turns.RemoveAt(turnIndex);
            trajectory.ValidateTurnsSizes();
        }

        /// <summary> Set size Turn with target index </summary>
        public static void SetTurnSize(this Trajectory trajectory, int turnIndex, float size)
        {
            if (!trajectory.IsIndexValid(turnIndex)) return;

            trajectory.Turns[turnIndex].Size = Mathf.Clamp(size, 1, float.MaxValue);
            trajectory.ValidateTurnsSizes();
        }

        /// <summary> Set speed Turn with target index </summary>
        public static void SetTurnSpeed(this Trajectory trajectory, int turnIndex, float speed)
        {
            if (!trajectory.IsIndexValid(turnIndex)) return;

            trajectory.Turns[turnIndex].Speed = Mathf.Clamp(speed, 1, float.MaxValue);
        }

        /// <summary> Validate all Turns sizes in trajectory </summary>
        public static void ValidateTurnsSizes(this Trajectory trajectory)
        {
            if (!trajectory.IsValid()) return;

            for (int iteration = 0; iteration < 3; iteration++)
            {
                trajectory.Turns[0].Size = 1;
                Vector3 dirToNext0 = (trajectory.Turns[1].Apex - trajectory.Turns[0].Apex).normalized;
                trajectory.Turns[0].End = trajectory.Turns[0].Apex + dirToNext0 * trajectory.Turns[0].Size;
                trajectory.Turns[0].Start = trajectory.Turns[0].Apex;

                for (int i = 1; i < trajectory.Turns.Count - 1; i++)
                {
                    Vector3 dirToPrevI = (trajectory.Turns[i - 1].Apex - trajectory.Turns[i].Apex).normalized;
                    Vector3 dirToNextI = (trajectory.Turns[i + 1].Apex - trajectory.Turns[i].Apex).normalized;

                    float distToPrevBorder = Vector3.Distance(trajectory.Turns[i].Apex, trajectory.Turns[i - 1].End);
                    float distToNextBorder = Vector3.Distance(trajectory.Turns[i].Apex, trajectory.Turns[i + 1].Start);

                    float maxSizePrev = Mathf.Max(0, distToPrevBorder - 1);
                    float maxSizeNext = Mathf.Max(0, distToNextBorder - 1);
                    float maxAllowedSize = Mathf.Min(maxSizePrev, maxSizeNext);

                    trajectory.Turns[i].Size = Mathf.Clamp(trajectory.Turns[i].Size, 0, maxAllowedSize);

                    trajectory.Turns[i].Start = trajectory.Turns[i].Apex + dirToPrevI * trajectory.Turns[i].Size;
                    trajectory.Turns[i].End = trajectory.Turns[i].Apex + dirToNextI * trajectory.Turns[i].Size;
                }

                trajectory.Turns[^1].Size = 1;
                Vector3 dirToPrev1 = (trajectory.Turns[^2].Apex - trajectory.Turns[^1].Apex).normalized;
                trajectory.Turns[^1].Start = trajectory.Turns[^1].Apex + dirToPrev1 * trajectory.Turns[^1].Size;
                trajectory.Turns[^1].End = trajectory.Turns[^1].Apex;
            }
        }

        private static bool IsIndexValid(this Trajectory trajectory, int turnIndex) => turnIndex >= 0 && turnIndex < trajectory.Turns.Count;
    }
}