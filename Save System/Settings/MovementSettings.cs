using System;

[Serializable]
public class MovementSettings
{
    public MovementType MovementType;

    public MovementSettings(MovementType type)
    {
        MovementType = type;
    }
}