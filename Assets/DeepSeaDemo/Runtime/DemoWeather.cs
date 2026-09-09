using UniStorm;
using UnityEngine;

namespace DeepSeaDemo
{
    public sealed class DemoWeather : MonoBehaviour
    {
        public DemoConfig config;
        public UniStormSystem system;
        public WeatherType overcast, thunderstorm;
        public Transform head;
        public GameObject weatherRoot;
        public bool Storm { get; private set; }
        bool softLightning;
        void Start()
        {
            if (!config.weatherReady)
            {
                if (weatherRoot != null) weatherRoot.SetActive(false);
                Debug.LogWarning("[DeepSeaDemo] WEATHER NOT ACCEPTED: " + config.weatherStatus);
                return;
            }
            SetStorm(Storm);
        }
        public void SetStorm(bool storm)
        {
            Storm = storm;
            if (!config.weatherReady || system == null) return;
            system.TimeFlow = UniStormSystem.EnableFeature.Disabled;
            system.WeatherGeneration = UniStormSystem.EnableFeature.Disabled;
            var target = storm ? thunderstorm : overcast;
            if (target != null) system.ChangeWeather(target);
        }
        public void ToggleSoftLightning()
        {
            softLightning = !softLightning;
            if (system != null) system.LightningStrikes = softLightning ? UniStormSystem.EnableFeature.Disabled : UniStormSystem.EnableFeature.Enabled;
        }
    }
}
