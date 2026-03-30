using System;
using System.Collections.Generic;
using UnityEngine;

public class UIGroupController : MonoBehaviour
{
    public static event Action<CanvasGroup> OnChangeActiveGroup;

    [field: Header("- - Управляемые группы канваса:")]
    [field: SerializeField] public List<CanvasGroup> CanvasGroups { get; private set; } = new();


    /// <summary> Установить активным передаваемый CanvasGroup, остальные неактивными </summary> 
    public void SetActiveGroup(CanvasGroup group)
    {
        if (!group || !CanvasGroups.Contains(group)) return;

        for (int i = 0; i < CanvasGroups.Count; i++)
        {
            if (CanvasGroups[i] == group)
            {
                SetStateGroup(CanvasGroups[i], true);
                OnChangeActiveGroup?.Invoke(CanvasGroups[i]);
            }
            else
            {
                SetStateGroup(CanvasGroups[i], false);
            }
        }
    }

    private void SetStateGroup(CanvasGroup group, bool state)
    {
        if (!group) return;

        group.alpha = state ? 1 : 0;
        group.blocksRaycasts = state;
        group.interactable = state;
    }

#if UNITY_EDITOR
    [Header("- - Управление в Editor:")]
    public bool UseChangeActiveGroup = false;


    private void OnValidate()
    {
        CheckGroupsValid();
    }

    private void CheckGroupsValid()
    {
        for (int i = 0; i < CanvasGroups.Count; i++)
            if (!CanvasGroups[i])
                Debug.LogWarning($"<color=orange>Не все CanvasGroup валидны. Элемент под номерм {i} не валиден!</color>", this);
    }
#endif
}