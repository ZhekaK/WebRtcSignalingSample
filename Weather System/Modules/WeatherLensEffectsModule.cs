using UnityEngine;

public class WeatherLensEffectsModule : BaseWeatherModule
{
    [Header("- - Названия Render Feature эффекта камеры:")]
    [SerializeField] private string _lensEffectFeatureName = "FullScreenLensEffect";

    //[Header("- - Материалы тумана:")]
    //[SerializeField, DisableEdit] private FullScreenPassRendererFeature _lensEffectFeature;

    public override void Initialize()
    {
        //// 1. Ищем ссылку на UniversalRenderPipelineAsset:
        //UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        //if (!pipeline)
        //{
        //    IsModuleValid = false;
        //    Debug.LogWarning("<color=orange>Система WeatherLensEffect невалидна (не найден UniversalRenderPipelineAsset)!</color>");
        //    return;
        //}

        //// 2. Ищем ссылку на UniversalRendererData:
        //var renderData = pipeline.rendererDataList[0] as High;
        //if (!renderData)
        //{
        //    IsModuleValid = false;
        //    Debug.LogWarning("<color=orange>Система WeatherLensEffect невалидна (не найден UniversalRendererData)!</color>");
        //    return;
        //}

        //// 3. Ищем ссылки на Fog Render Features:
        //_lensEffectFeature = renderData.rendererFeatures.Where((f) => f.name == _lensEffectFeatureName).FirstOrDefault() as FullScreenPassRendererFeature;

        //// 4. Проверяем кешированные ссылки Fog Materials:
        //if (!_lensEffectFeature)
        //{
        //    IsModuleValid = false;
        //    Debug.LogWarning("<color=orange>Система WeatherLensEffect невалидна (не найден FullScreenRenderFeature)!</color>");
        //    return;
        //}

        //IsSystemValid = true;
    }

    public override void SetWeatherProfile(WeatherProfile newProfile)
    {
        //if (!IsModuleValid) return;

        //if (!newProfile.LensEffectMat)
        //{
        //    IsModuleValid = false;
        //    Debug.LogWarning("<color=orange>Система WeatherLensEffect невалидна (не найден LensEffectMat)!</color>");
        //    return;
        //}

        //_lensEffectFeature.passMaterial = newProfile.LensEffectMat;
    }
}