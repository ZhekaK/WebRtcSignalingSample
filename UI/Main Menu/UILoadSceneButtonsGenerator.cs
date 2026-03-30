using UnityEngine;

public class UILoadSceneButtonsGenerator : MonoBehaviour
{
    [SerializeField] private LoadSceneButton _loadSceneButtonPrefab;
    [SerializeField] private Transform _contentTransform;


    private void Awake()
    {
        if (!_loadSceneButtonPrefab)
        {
            Debug.LogError($"<color=red>Link to {nameof(_loadSceneButtonPrefab)} not found</color>", this);
            return;
        }

        if (!_contentTransform)
        {
            Debug.LogError($"<color=red>Link to {nameof(_contentTransform)} not found</color>", this);
            return;
        }

        GenerateButtons();
    }

    private void GenerateButtons()
    {
        LoadSceneButton loadSceneButton;
        foreach (AirportsData.Airport airport in AirportsManager.Instance.AirportsData.Airports)
        {
            if (airport.ignore) continue;

            string sceneName = AirportsData.GetSceneName(airport);

            if (!AirportsManager.IsSceneValid(sceneName)) continue;

            loadSceneButton = Instantiate(_loadSceneButtonPrefab, _contentTransform);
            loadSceneButton.InitializeButton(sceneName, airport.buttonName);
        }
    }
}