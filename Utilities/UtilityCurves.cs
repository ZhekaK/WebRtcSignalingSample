using UnityEngine;

public static class UtilityCurves
{
    /// <summary> Get WorldSpace position on cubic Bezier Curve </summary>
    public static Vector3 GetCurveWSPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return (oneMinusT * oneMinusT * oneMinusT * p0) + (3f * oneMinusT * oneMinusT * t * p1) + (3f * oneMinusT * t * t * p2) + (t * t * t * p3);
    }

    /// <summary> Get world space position on square Bezier Curve </summary>
    public static Vector3 GetCurveWSPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return (oneMinusT * oneMinusT * p0) + (2f * oneMinusT * t * p1) + (t * t * p2);
    }

    /// <summary> Get tangent direction on cubic Bezier Curve </summary>
    public static Vector3 GetCurveDirection(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return (3f * oneMinusT * oneMinusT * (p1 - p0)) + (6f * oneMinusT * t * (p2 - p1)) + (3f * t * t * (p3 - p2));
    }

    /// <summary> Get tangent direction on square Bezier Curve </summary>
    public static Vector3 GetCurveDirection(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        t = Mathf.Clamp01(t);
        return (2f * (1f - t) * (p1 - p0)) + (2f * t * (p2 - p1));
    }

    /// <summary> (High cost function!) Get length cubic Bezier Curve </summary>
    public static float GetCurveLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float turnSize, float accuracy = 0.01f)
    {
        float length = 0f;
        float samples = turnSize / accuracy;

        Vector3 prevPoint = p1;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / samples;
            Vector3 point = GetCurveWSPoint(p0, p1, p2, p3, t);
            length += Vector3.Distance(prevPoint, point);
            prevPoint = point;
        }

        return length;
    }

    /// <summary> (High cost function!) Get length square Bezier Curve </summary>
    public static float GetCurveLength(Vector3 p0, Vector3 p1, Vector3 p2, float turnSize, float accuracy = 0.01f)
    {
        float length = 0f;
        float samples = turnSize / accuracy;

        Vector3 prevPoint = p1;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / samples;
            Vector3 point = GetCurveWSPoint(p0, p1, p2, t);
            length += Vector3.Distance(prevPoint, point);
            prevPoint = point;
        }

        return length;
    }
}
