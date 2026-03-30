using UnityEngine;

[System.AttributeUsage(System.AttributeTargets.Method)]
public class InspectorButtonAttribute : System.Attribute
{
    public readonly string ButtonName;

    public InspectorButtonAttribute(string methodName = null)
    {
        ButtonName = methodName;
    }
}