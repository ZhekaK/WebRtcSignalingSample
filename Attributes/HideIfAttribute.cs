using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class HideIfAttribute : PropertyAttribute
{
    public string ConditionPath;
    public object ComparisonValue;
    public bool Invert;

    public HideIfAttribute(string conditionPath, object comparisonValue, bool invert = false)
    {
        ConditionPath = conditionPath;
        ComparisonValue = comparisonValue;
        Invert = invert;
    }
}