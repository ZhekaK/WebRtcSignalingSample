using UnityEngine;
using UnityEngine.UI;

public class LoadSceneButton : MonoBehaviour
{
    [Header("- - UI Elements:")]
    [SerializeField] private Button _buttonLoadScene;
    [SerializeField] private Text _textButton;
    [Space]
    [SerializeField, DisableEdit] private string _sceneName;

    /// <summary>
    /// Initialize buttons for all airports scenes
    /// </summary>
    /// <param name="sceneName"></param>
    /// <param name="buttonText"></param>
    public void InitializeButton(string sceneName, string buttonText)
    {
        _sceneName = sceneName;
        _textButton.text = buttonText;
        _buttonLoadScene.onClick.AddListener(LoadScene);
    }

    private void LoadScene()
    {
        AirportsManager.Instance.LoadSceneAsync(_sceneName);
    }
}