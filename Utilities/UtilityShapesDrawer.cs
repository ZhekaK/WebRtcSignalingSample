#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class UtilityShapesDrawer
{
    /// <summary> Нарисовать капсулу с помощью класса Handles </summary>
    /// <param name="center">Центр объема капсулы</param>
    /// <param name="radius">Радиус сферической части капсулы</param>
    /// <param name="height">Общая высота капсулы</param>
    /// <param name="rotation">Ориентация капсулы</param>
    public static void DrawWireCapsule(Vector3 center, float radius, float height, Quaternion rotation)
    {
        height = Mathf.Max(height, radius * 2);

        CalculateLocalAxis(Vector3.up, rotation, out Vector3 up);
        CalculateLocalAxis(Vector3.forward, rotation, out Vector3 forward);
        CalculateLocalAxis(Vector3.right, rotation, out Vector3 right);

        Vector3 topPoint = center + up * (height / 2);
        Vector3 bottomPoint = center - up * (height / 2);

        Vector3 topSphereCenter = topPoint - up * radius;
        Vector3 bottomSphereCenter = bottomPoint + up * radius;

        Handles.DrawLine(bottomSphereCenter + forward * radius, topSphereCenter + forward * radius);
        Handles.DrawLine(bottomSphereCenter + (-forward) * radius, topSphereCenter + (-forward) * radius);
        Handles.DrawLine(bottomSphereCenter + right * radius, topSphereCenter + right * radius);
        Handles.DrawLine(bottomSphereCenter + (-right) * radius, topSphereCenter + (-right) * radius);

        DrawWireHemisphere(topSphereCenter, radius, rotation, true);
        DrawWireHemisphere(bottomSphereCenter, radius, rotation, false);
    }

    /// <summary> Нарисовать капсулу с помощью класса Handles </summary>
    /// <param name="center">Центр объема капсулы</param>
    /// <param name="radius">Радиус сферической части капсулы</param>
    /// <param name="height">Общая высота капсулы</param>
    /// <param name="rotation">Ориентация капсулы</param>
    /// <param name="color">Цвет капсулы</param>
    public static void DrawWireCapsule(Vector3 center, float radius, float height, Quaternion rotation, Color color)
    {
        using (new Handles.DrawingScope(color))
        {
            DrawWireCapsule(center, radius, height, rotation);
        }
    }

    /// <summary> Нарисовать капсулу и линию до земли с помощью класса Handles </summary>
    /// <param name="center">Центр объема капсулы</param>
    /// <param name="radius">Радиус сферической части капсулы</param>
    /// <param name="height">Общая высота капсулы</param>
    /// <param name="rotation">Ориентация капсулы</param>
    /// <param name="color">Цвет капсулы</param>
    /// <param name="groundPos">Позиция земли капсулы</param>
    public static void DrawWireCapsule(Vector3 center, float radius, float height, Quaternion rotation, Color color, Vector3 groundPos)
    {
        using (new Handles.DrawingScope(color))
        {
            DrawWireCapsule(center, radius, height, rotation);
            Handles.DrawLine(center - rotation * Vector3.up * (height / 2), groundPos);
        }
    }

    /// <summary> Нарисовать капсулу с помощью класса Handles </summary>
    /// <param name="bottomPoint">Центр нижней полусферы капсулы</param>
    /// <param name="upperPoint">Центр верхней полусферы капсулы</param>
    /// <param name="radius">Радиус капсулы</param>
    /// <param name="color">Цвет капсулы</param>
    public static void DrawWireCapsule(Vector3 bottomPoint, Vector3 upperPoint, float radius, Color color)
    {
        Vector3 center = (bottomPoint + upperPoint) * 0.5f;
        Vector3 direction = (upperPoint - bottomPoint);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        float height = direction.magnitude + radius * 2;

        using (new Handles.DrawingScope(color))
        {
            DrawWireCapsule(center, radius, height, rotation);
        }
    }

    /// <summary> Нарисовать каст капсулы с помощью класса Handles </summary>
    /// <param name="bottomPoint">Центр нижней полусферы капсулы</param>
    /// <param name="upperPoint">Центр верхней полусферы капсулы</param>
    /// <param name="radius">Радиус капсулы</param>
    /// <param name="color">Цвет капсулы</param>
    /// <param name="castVector">Локальный вектор каста</param>
    public static void DrawWireCapsule(Vector3 bottomPoint, Vector3 upperPoint, float radius, Color color, Vector3 castVector)
    {
        DrawWireCapsule(bottomPoint, upperPoint, radius, color);

        Vector3 newBottomPoint = bottomPoint + castVector;
        Vector3 newUpperPoint = upperPoint + castVector;
        DrawWireCapsule(newBottomPoint, newUpperPoint, radius, color);

        using (new Handles.DrawingScope(color))
        {
            Handles.DrawLine(bottomPoint, newBottomPoint);
            Handles.DrawLine(upperPoint, newUpperPoint);
        }
    }

    /// <summary> Нарисовать полусферу с помощью класса Handles </summary>
    /// <param name="center">Центр полусферы</param>
    /// <param name="radius">Радиус полусферы</param>
    /// <param name="rotation">Ориентация полусферы</param>
    public static void DrawWireHemisphere(Vector3 center, float radius, Quaternion rotation, bool isUpperHemisphere)
    {
        CalculateLocalAxis(Vector3.up, rotation, out Vector3 up);
        CalculateLocalAxis(Vector3.forward, rotation, out Vector3 forward);
        CalculateLocalAxis(Vector3.right, rotation, out Vector3 right);

        Handles.DrawWireDisc(center, up, radius);

        if (isUpperHemisphere)
        {
            Handles.DrawWireArc(center, forward, right, 180, radius);
            Handles.DrawWireArc(center, -right, forward, 180, radius);
        }
        else
        {
            Handles.DrawWireArc(center, forward, right, -180, radius);
            Handles.DrawWireArc(center, -right, forward, -180, radius);
        }
    }

    /// <summary> Нарисовать полусферу с помощью класса Handles </summary>
    /// <param name="center">Центр полусферы</param>
    /// <param name="radius">Радиус полусферы</param>
    /// <param name="rotation">Ориентация полусферы</param>
    /// <param name="color">Цвет полусферы</param>
    public static void DrawWireHemisphere(Vector3 center, float radius, Quaternion rotation, bool isUpperHemisphere, Color color)
    {
        using (new Handles.DrawingScope(color))
        {
            DrawWireHemisphere(center, radius, rotation, isUpperHemisphere);
        }
    }

    /// <summary> Нарисовать стрелку с помощью класса Handles </summary>
    /// <param name="position">Позиция стрелки (середина)</param>
    /// <param name="rotation">Ориентация стрелки</param>
    /// <param name="length">Общая длина стрелки</param>
    /// <param name="arrowheadSize">Размер наконечника (от 0 до 1, относительно длины)</param>
    /// <param name="color">Цвет стрелки</param>
    public static void DrawArrow(Vector3 position, Quaternion rotation, float length, float arrowheadSize, Color color)
    {
        using (new Handles.DrawingScope(color))
        {
            arrowheadSize = Mathf.Clamp01(arrowheadSize);

            Vector3 forward = rotation * Vector3.forward;
            Vector3 tail = position - forward * length * 0.5f;
            Vector3 tip = position + forward * length * 0.5f;
            float wingLength = length * arrowheadSize;

            Handles.DrawLine(tail, tip);

            Vector3 right = rotation * Vector3.right * wingLength;

            Handles.DrawLine(tip, tip - forward * wingLength + right);
            Handles.DrawLine(tip, tip - forward * wingLength - right);
            Handles.DrawLine(tip - forward * wingLength - right, tip - forward * wingLength + right);
        }
    }

    /// <summary> Нарисовать стрелку с помощью класса Handles </summary>
    /// <param name="start">Точка начала стрелки</param>
    /// <param name="end">Точка конца стрелки</param>
    /// <param name="arrowheadSize">Размер наконечника (от 0 до 1)</param>
    /// <param name="color">Цвет стрелки</param>
    public static void DrawArrow(Vector3 start, Vector3 end, float arrowheadSize, Color color)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;
        if (length == 0f) return;

        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 position = start + direction * 0.5f;

        DrawArrow(position, rotation, length, arrowheadSize, color);
    }

    /// <summary> Нарисовать сетку с помощью класса Handles </summary>
    /// <param name="position">Позиция центра сетки</param>
    /// <param name="cellSize">Размер одной ячейки</param>
    /// <param name="gridSize">Количество ячеек по X/Z</param>
    /// <param name="color">Цвет сетки</param>
    public static void DrawGrid(Vector3 position, float cellSize, int gridSize, Color color)
    {
        using (new Handles.DrawingScope(color))
        {
            float totalSize = cellSize * gridSize;
            Vector3 start = position - new Vector3(totalSize / 2f, 0, totalSize / 2f);

            // Горизонтальные линии
            for (int x = 0; x <= gridSize; x++)
            {
                Vector3 startX = start + Vector3.right * x * cellSize;
                Vector3 endX = startX + Vector3.forward * totalSize;
                Handles.DrawLine(startX, endX);
            }

            // Вертикальные линии
            for (int z = 0; z <= gridSize; z++)
            {
                Vector3 startZ = start + Vector3.forward * z * cellSize;
                Vector3 endZ = startZ + Vector3.right * totalSize;
                Handles.DrawLine(startZ, endZ);
            }
        }
    }

    private static void CalculateLocalAxis(Vector3 worldAxis, Quaternion rotation, out Vector3 localAxis)
    {
        localAxis = rotation * worldAxis;
    }
}
#endif