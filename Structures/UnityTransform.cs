using System;
using UnityEngine;

[Serializable]
public struct UnityTransform
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 offset;

    public override string ToString()
    {;
        return $"-- Позиция --\n " +
            $"X: {position.x}\n " +
            $"Y: {position.y}\n " +
            $"Z: {position.z}\n " +
            $"- Углы --\n " +
            $"X: {rotation.eulerAngles.x}\n " +
            $"Y: {rotation.eulerAngles.y}\n " +
            $"Z: {rotation.eulerAngles.z}\n " +
            $"- Смещение --\n " +
            $"X: {offset.x}\n " +
            $"Y: {offset.y}\n " +
            $"Z: {offset.z}\n";
    }
}