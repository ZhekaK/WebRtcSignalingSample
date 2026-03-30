using UnityEngine;

namespace Multilayer.Setup.Module
{
    public class LightingSetupModule : BaseSetupModule
    {
        [SerializeField] private Light _sunLight;
        [SerializeField] private Light _moonLight;


        protected override bool IsModuleValid() => _sunLight && _moonLight;

        /// <summary> Setup the scene lighting for a visibility layer </summary>
        public void SetupLighting(RenderLayer layer, bool isLightingEnabled, LightShadows lightingShadows, params string[] lightingCullingLayersNames)
        {
            if (!IsModuleValid())
            {
                Debug.LogError($"<color=red>{GetType().Name} is not valid!</color>");
                return;
            }

            // Prepare:
            int layerMask = LayerMask.NameToLayer(layer.ToString());
            int cullingMask = LayerMask.GetMask(lightingCullingLayersNames);

            // Setup:
            _sunLight.enabled = isLightingEnabled;
            _moonLight.enabled = isLightingEnabled;

            _sunLight.shadows = lightingShadows;
            _moonLight.shadows = LightShadows.None;

            _sunLight.gameObject.layer = layerMask;
            _moonLight.gameObject.layer = layerMask;

            _sunLight.cullingMask = cullingMask;
            _moonLight.cullingMask = cullingMask;
        }

        public override void Dispose() { }
    }
}