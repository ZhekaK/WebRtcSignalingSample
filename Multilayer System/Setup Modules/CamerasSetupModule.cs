using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using static UnityEngine.Rendering.HighDefinition.CustomPass;

namespace Multilayer.Setup.Module
{
    public class CamerasSetupModule : BaseSetupModule
    {
        [SerializeField] private Transform _camerasBlockTransform;

        private List<Camera> _renderCameras = new();
        private List<CustomPassVolume> _overlayPassVolumes = new();
        private List<DrawRenderersCustomPass> _overlayLayerPasses = new();

        protected override bool IsModuleValid() => _camerasBlockTransform;

        /// <summary> Setup the scene camera render for a visibility layer </summary>
        public void SetupRenderCameras(RenderLayer renderLayer,HDAdditionalCameraData.ClearColorMode clearColorMode,
                                       Color backgroundColor, CustomPassInjectionPoint passInjectionPoint,
                                       Shader overrideShader, string overrideShaderPassType,
                                       RenderQueueType renderQueueType)
        {
            if (!IsModuleValid()) return;

            // Overlay Camera Prefab
            GameObject renderCameraPrefab = Resources.Load<GameObject>("Multilayer System/Render Camera");
            if (!renderCameraPrefab)
            {
                Debug.LogError($"<color=red>Render Camera Prefab is not found!</color>");
                return;
            }

            GameObject renderCameraObject;
            int layerMask = LayerMask.NameToLayer(renderLayer.ToString());
            int layerMaskFlag = LayerMask.GetMask(renderLayer.ToString());
            foreach (DisplayData displayData in DisplaysManager.Instance.DisplaysDatas.Values)
            {
                if (!displayData.RenderingLayersDatas.TryGetValue(renderLayer, out RenderLayerData renderLayerData)) continue;
                if (!renderLayerData.Settings.ExistOnDisplay) continue;

                // Render Camera Object
                renderCameraObject = Instantiate(renderCameraPrefab, _camerasBlockTransform);
                if (renderCameraObject.TryGetComponent<Camera>(out Camera renderCamera))
                {
                    // Rendering
                    renderCamera.gameObject.name = $"{renderLayer} Render Camera - {displayData.Settings.Display}";
                    renderCamera.targetDisplay = (int)displayData.Settings.Display;
                    renderCamera.targetTexture = renderLayerData.RT;
                    renderCamera.gameObject.layer = layerMask;
                    renderCamera.cullingMask = layerMaskFlag;
                    renderCamera.depth = 0;

                    var HDdata = renderCamera.GetComponent<HDAdditionalCameraData>();
                    HDdata.volumeLayerMask = layerMaskFlag;
                    HDdata.clearColorMode = clearColorMode;
                    HDdata.backgroundColorHDR = backgroundColor;

                    // FOV and Rotation
                    if (DisplaysManager.Instance.DisplaysConfig.EmulateWindshields)
                    {
                        WindshieldsEmulationManager.Instance.AddCameraToSybsystem(renderCamera);
                    }
                    else
                    {
                        renderCamera.fieldOfView = displayData.Settings.FOV;
                        renderCamera.transform.eulerAngles = displayData.Settings.Rotation;
                    }

                    _renderCameras.Add(renderCamera);
                }
                else
                {
                    Debug.LogError($"<color=red>Camera on object - {renderCameraObject.name} is not found!</color>");
                }

                // Custom Pass Volume
                if (!overrideShader) continue;
                CustomPassVolume passVolume = renderCameraObject.GetComponentInChildren<CustomPassVolume>();
                if (passVolume)
                {
                    passVolume.injectionPoint = passInjectionPoint;
                    var layerPass = passVolume.AddPassOfType(typeof(DrawRenderersCustomPass)) as DrawRenderersCustomPass;
                    layerPass.name = $"{renderLayer} Render Layer Pass - {displayData.Settings.Display}";
                    layerPass.overrideMode = DrawRenderersCustomPass.OverrideMaterialMode.Shader;
                    layerPass.targetColorBuffer = CustomPass.TargetBuffer.Camera;
                    layerPass.targetDepthBuffer = CustomPass.TargetBuffer.Camera;
                    layerPass.overrideShader = overrideShader;
                    layerPass.overrideShaderPassName = overrideShaderPassType;
                    layerPass.renderQueueType = renderQueueType;
                    layerPass.layerMask = layerMaskFlag;

                    _overlayPassVolumes.Add(passVolume);
                    _overlayLayerPasses.Add(layerPass);
                }
                else
                {
                    Debug.LogError($"<color=red>{nameof(CustomPassVolume)} on object - {renderCameraObject.name} is not found!</color>");
                }
            }
        }

        public override void Dispose()
        {
            if (DisplaysManager.Instance.DisplaysConfig.EmulateWindshields)
                foreach (Camera renderCamera in _renderCameras)
                    WindshieldsEmulationManager.Instance.RemoveCameraToSybsystem(renderCamera);
        }
    }
}