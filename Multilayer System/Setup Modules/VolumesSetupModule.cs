using UnityEngine;
using UnityEngine.Rendering;

namespace Multilayer.Setup.Module
{
    public class VolumesSetupModule : BaseSetupModule
    {
        [SerializeField] private Volume _generalVolume;
        [SerializeField] private Volume _skyVolume;
        [SerializeField] private Volume _cloudsVolume;
        [SerializeField] private Volume _fogVolume;
        [SerializeField] private Volume _postProcessVolume;
        [SerializeField] private Volume _customVolume;


        protected override bool IsModuleValid() => _generalVolume && _skyVolume && _cloudsVolume && _fogVolume && _postProcessVolume && _customVolume;

        /// <summary> Setup the scene volumes for a visibility layer </summary>
        public void SetupVolumes(RenderLayer layer, bool useGeneralVolume, bool useSkyVolume, bool useCloudsVolume, bool useFogVolume, bool usePostProcessVolume, bool useCustomVolume,
            VolumeProfile generalVolume, VolumeProfile skyVolume, VolumeProfile cloudsVolume, VolumeProfile fogVolume, VolumeProfile postProcessVolume, VolumeProfile customVolume)
        {
            if (!IsModuleValid())
            {
                Debug.LogError($"<color=red>{GetType().Name} is not valid!</color>");
                return;
            }

            // Prepare:
            int layerMask = LayerMask.NameToLayer(layer.ToString());

            // Setup:
            _generalVolume.enabled = useGeneralVolume;
            _generalVolume.gameObject.layer = layerMask;
            _generalVolume.sharedProfile = generalVolume;
            _generalVolume.profile = generalVolume;

            _skyVolume.enabled = useSkyVolume;
            _skyVolume.gameObject.layer = layerMask;
            _skyVolume.sharedProfile = skyVolume;
            _skyVolume.profile = skyVolume;

            _cloudsVolume.enabled = useCloudsVolume;
            _cloudsVolume.gameObject.layer = layerMask;
            _cloudsVolume.sharedProfile = cloudsVolume;
            _cloudsVolume.profile = cloudsVolume;

            _fogVolume.enabled = useFogVolume;
            _fogVolume.gameObject.layer = layerMask;
            _fogVolume.sharedProfile = fogVolume;
            _fogVolume.profile = fogVolume;

            _postProcessVolume.enabled = usePostProcessVolume;
            _postProcessVolume.gameObject.layer = layerMask;
            _postProcessVolume.sharedProfile = postProcessVolume;
            _postProcessVolume.profile = postProcessVolume;

            _customVolume.enabled = useCustomVolume;
            _customVolume.gameObject.layer = layerMask;
            _customVolume.sharedProfile = customVolume;
            _customVolume.profile = customVolume;
        }

        public override void Dispose() { }
    }
}