public enum MovementType
{
    /// <summary> No movement </summary>
    None,
    /// <summary> Movement via received geo coordinates </summary>
    GeoReciever,
    /// <summary> Movement via specified geo coordinates </summary>
    GeoDebug,
    /// <summary> Movement along a trajectory </summary>
    Trajectory,
    /// <summary> Free camera movement </summary>
    FreeCamera
}
