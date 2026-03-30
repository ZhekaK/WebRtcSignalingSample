using UnityEngine;
using UnityEngine.UI;

public class UIMainMenuScreen : MonoBehaviour
{
    [SerializeField] private string _softwareName = "Имитатор Окружения";
    
    [Header("- - UI Elements:")]
    [SerializeField] private Text _textProjectName;
    [SerializeField] private Text _textProjectVersion;
    [SerializeField] private Button _buttonQuit;

    private void Awake()
    {
#if APP_TYPE_INTEGRAL
        _textProjectName.text = $"{_softwareName}\nИнтеграл";
#elif APP_TYPE_SGS
        _textProjectName.text = $"{_softwareName}\nСГС";
#endif
        _textProjectVersion.text = Application.version;
        _buttonQuit.onClick.AddListener(ExitApplication);
    }

    private void ExitApplication()
    {
        Application.Quit();
    }
}