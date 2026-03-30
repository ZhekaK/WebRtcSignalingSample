using System.Collections.Generic;
using UnityEngine;

namespace Movement.Trajectories
{
    public static class UtilityTrajectory
    {
        /// <summary> Generate trajectory with target parameters </summary>
        public static void GenerateTrajectory(Trajectory trajectory, float minTurnsOffset)
        {
            if (!trajectory || !trajectory.IsValid()) return;

            var turns = trajectory.Turns;

            for (int i = 0; i < turns.Count; i++)
            {
                Vector3 startPos = turns[i].Apex;
                Vector3 endPos = turns[i].Apex;

                if (i > 0)
                {
                    Vector3 toPrev = (turns[i - 1].Apex - turns[i].Apex).normalized;
                    float maxDistPrev = Vector3.Distance(turns[i].Apex, turns[i - 1].Apex) - minTurnsOffset;
                    startPos = turns[i].Apex + toPrev * Mathf.Clamp(turns[i].Size, 0, maxDistPrev);
                }

                if (i < turns.Count - 1)
                {
                    Vector3 toNext = (turns[i + 1].Apex - turns[i].Apex).normalized;
                    float maxDistNext = Vector3.Distance(turns[i].Apex, turns[i + 1].Apex) - minTurnsOffset;
                    endPos = turns[i].Apex + toNext * Mathf.Clamp(turns[i].Size, 0, maxDistNext);
                }

                turns[i].Start = startPos;
                turns[i].End = endPos;
            }
        }

        /// <summary> Get trajectory all path with steps </summary>
        public static List<Vector3> GetTrajectoryPath(Trajectory trajectory, int steps = 10)
        {
            List<Vector3> path = new();

            if (!trajectory.IsValid()) return path;

            path.Add(trajectory.Turns[0].Apex);
            for (int i = 1; i < trajectory.Turns.Count; i++)
            {
                path.Add(trajectory.Turns[i].Start);
                path.AddRange(GenerateTurnPath(trajectory.Turns[i], steps));
            }
            path.Add(trajectory.Turns[^1].Apex);

            return path;
        }

        /// <summary> Get trajectory turn path with steps </summary>
        public static List<Vector3> GenerateTurnPath(Trajectory.Turn turn, int steps = 10)
        {
            List<Vector3> path = new();

            for (int step = 0; step <= steps; step++)
                path.Add(UtilityCurves.GetCurveWSPoint(turn.Start, turn.Apex, turn.End, step / (float)steps));

            return path;
        }
    }
}