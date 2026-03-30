using System;
using System.Linq;
using UnityEngine.UI;

public static class UtilityUserInterafaceHelper
{
    /// <summary> Initialize Dropdown options with all enum values and priority </summary>
    public static void InitializeDropdown(Dropdown dropdown, Type tagetEnum)
    {
        if (!dropdown) return;

        dropdown.ClearOptions();
        var options = Enum.GetNames(tagetEnum).Select(name => new Dropdown.OptionData(name)).ToList();
        dropdown.AddOptions(options);
        dropdown.RefreshShownValue();
        dropdown.value = 0;
    }
}