using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Multilayer.Setup.Module
{
    public class GeometrySetupModule : BaseSetupModule
    {
        [SerializeField] private Transform _geometry;


        protected override bool IsModuleValid() => _geometry;

        /// <summary> Setup the scene geometry for a visibility layer </summary>
        public void SetupGeometry(RenderLayer layer, bool isVisible, ShadowCastingMode shadowCastingMode, bool receiveShadows)
        {
            if (!IsModuleValid())
            {
                Debug.LogError($"<color=red>{GetType().Name} is not valid!</color>");
                return;
            }

            // Prepare:
            int layerMask = LayerMask.NameToLayer(layer.ToString());

            // Setup:
            ApplySettingsRecursively(_geometry, layerMask, isVisible, shadowCastingMode, receiveShadows);
        }

        private void ApplySettingsRecursively(Transform current, int layerMask, bool isVisible, ShadowCastingMode shadowCastingMode, bool receiveShadows)
        {
            current.gameObject.layer = layerMask;
            if (current.gameObject.TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer.enabled = isVisible;
                meshRenderer.shadowCastingMode = shadowCastingMode;
                meshRenderer.receiveShadows = receiveShadows;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                ApplySettingsRecursively(current.GetChild(i), layerMask, isVisible, shadowCastingMode, receiveShadows);
            }
        }

        public override void Dispose() { }
    }
}