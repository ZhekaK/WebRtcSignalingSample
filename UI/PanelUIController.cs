using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class PanelUIController : MonoBehaviour
{
    [Header("- - Дополнительные элементы:")]
    [SerializeField] private UIGroupController _groupController;
    [SerializeField] private Slider _sliderPages;
    [SerializeField] private Text _textPageName;

    [Header("- - Параметры контроллера страниц UI:")]
    [SerializeField, Min(0)] private int _indexMinControlPage;
    [SerializeField, Min(0)] private int _indexMaxControlPage;


    private void Awake()
    {
        InitializeSlider();

        SetActivePage(_indexMinControlPage);
    }

    private void InitializeSlider()
    {
        _sliderPages.minValue = 0;
        _sliderPages.maxValue = Mathf.Max(_groupController.CanvasGroups.Count - 1, 0);
        _sliderPages.value = _indexMinControlPage;

        _sliderPages.onValueChanged.AddListener(SetActivePage);
    }

    public void SetActivePage(float panelNumber)
    {
        _groupController.SetActiveGroup(_groupController.CanvasGroups[(int)panelNumber]);
        _textPageName.text = _groupController.CanvasGroups[(int)panelNumber].name.Replace("Panel_", "№");
    }
}
