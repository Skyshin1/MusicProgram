using System.Collections;
using AbstractOcclusion.WebGpuWater;
using UniStorm;
using UniStorm.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonicWorld.Weather
{
    /// <summary>
    /// Applies the scene's fixed-storm contract before UniStorm initialises and keeps the shared
    /// environment state coherent afterwards. This intentionally leaves underwater fog, caustics
    /// and god rays to WebGpuWater instead of stacking UniStorm's legacy camera effects on XR.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class LockedThunderstormEnvironment : MonoBehaviour
    {
        [SerializeField] UniStormSystem uniStormSystem;
        [SerializeField] WeatherType thunderstormWeather;
        [SerializeField] Transform playerTransform;
        [SerializeField] Camera playerCamera;
        [SerializeField] Material skyboxMaterial;
        [SerializeField] Light uniStormSun;
        [SerializeField] WaterVolume waterVolume;
        [SerializeField] Light[] legacyDirectionalLights = new Light[0];
        [SerializeField] bool disableBuiltInFog = true;

        public void Configure(
            UniStormSystem system,
            WeatherType weather,
            Transform player,
            Camera camera,
            Material skybox,
            Light stormSun,
            WaterVolume water,
            Light[] legacyLights)
        {
            uniStormSystem = system;
            thunderstormWeather = weather;
            playerTransform = player;
            playerCamera = camera;
            skyboxMaterial = skybox;
            uniStormSun = stormSun;
            waterVolume = water;
            legacyDirectionalLights = legacyLights ?? new Light[0];
        }

        void Awake()
        {
            ApplyUniStormContract();
            ApplySharedEnvironment();
        }

        IEnumerator Start()
        {
            // UniStorm creates/configures its camera effects and assigns RenderSettings during Start.
            // Re-apply one frame later so the project-owned XR fog and sky contract wins.
            yield return null;
            ApplySharedEnvironment();
            DisableLegacyCameraEffects();
            DynamicGI.UpdateEnvironment();
        }

        void LateUpdate()
        {
            if (uniStormSystem != null)
            {
                uniStormSystem.TimeFlow = UniStormSystem.EnableFeature.Disabled;
                uniStormSystem.WeatherGeneration = UniStormSystem.EnableFeature.Disabled;

                if (thunderstormWeather != null &&
                    uniStormSystem.CurrentWeatherType != thunderstormWeather)
                    uniStormSystem.ChangeWeather(thunderstormWeather);
            }

            ApplySharedEnvironment();
        }

        void ApplyUniStormContract()
        {
            if (uniStormSystem == null)
                return;

            uniStormSystem.PlatformType = UniStormSystem.PlatformTypeEnum.VR;
            uniStormSystem.PlayerTransform = playerTransform;
            uniStormSystem.PlayerCamera = playerCamera;
            uniStormSystem.GetPlayerAtRuntime = UniStormSystem.EnableFeature.Disabled;
            uniStormSystem.UseRuntimeDelay = UniStormSystem.EnableFeature.Disabled;
            uniStormSystem.StartingHour = 17;
            uniStormSystem.StartingMinute = 0;
            uniStormSystem.Hour = 17;
            uniStormSystem.Minute = 0;
            uniStormSystem.TimeFlow = UniStormSystem.EnableFeature.Disabled;
            uniStormSystem.RealWorldTime = UniStormSystem.EnableFeature.Disabled;
            uniStormSystem.WeatherGeneration = UniStormSystem.EnableFeature.Disabled;
            uniStormSystem.CloudShadows = UniStormSystem.EnableFeature.Disabled;
            uniStormSystem.SunShaftsEffect = UniStormSystem.EnableFeature.Disabled;
            uniStormSystem.MoonShaftsEffect = UniStormSystem.EnableFeature.Disabled;
            uniStormSystem.UseUniStormMenu = UniStormSystem.EnableFeature.Disabled;
            uniStormSystem.FogType = UniStormSystem.FogTypeEnum.UnityFog;
            uniStormSystem.LightningStrikes = UniStormSystem.EnableFeature.Enabled;

            if (thunderstormWeather != null)
            {
                uniStormSystem.CurrentWeatherType = thunderstormWeather;
                uniStormSystem.NextWeatherType = thunderstormWeather;
            }
        }

        void ApplySharedEnvironment()
        {
            if (skyboxMaterial != null && RenderSettings.skybox != skyboxMaterial)
                RenderSettings.skybox = skyboxMaterial;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            if (uniStormSun == null && uniStormSystem != null)
                uniStormSun = uniStormSystem.m_SunLight;
            if (uniStormSun != null)
                RenderSettings.sun = uniStormSun;

            if (disableBuiltInFog)
                RenderSettings.fog = false;

            for (int i = 0; i < legacyDirectionalLights.Length; i++)
            {
                Light light = legacyDirectionalLights[i];
                if (light != null && light != uniStormSun)
                    light.enabled = false;
            }
        }

        void DisableLegacyCameraEffects()
        {
            if (playerCamera == null)
                return;

            UniStormAtmosphericFog[] fogEffects =
                playerCamera.GetComponents<UniStormAtmosphericFog>();
            for (int i = 0; i < fogEffects.Length; i++)
                fogEffects[i].enabled = false;

            UniStormSunShafts[] shaftEffects = playerCamera.GetComponents<UniStormSunShafts>();
            for (int i = 0; i < shaftEffects.Length; i++)
                shaftEffects[i].enabled = false;
        }
    }
}
