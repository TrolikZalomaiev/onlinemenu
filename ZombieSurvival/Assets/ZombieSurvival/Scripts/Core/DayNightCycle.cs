using Unity.Netcode;
using UnityEngine;

namespace ZombieSurvival
{
    /// <summary>
    /// Смена дня и ночи. Время ведёт сервер, все клиенты только отрисовывают свет и туман.
    /// TimeOfDay: 0 = полночь, 0.25 = рассвет, 0.5 = полдень, 0.75 = закат.
    /// </summary>
    public class DayNightCycle : NetworkBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Header("Время")]
        [SerializeField] float cycleLengthSeconds = 900f;
        [SerializeField, Range(0f, 1f)] float startTime = 0.3f;
        [SerializeField, Range(0f, 1f)] float nightStart = 0.8f;
        [SerializeField, Range(0f, 1f)] float nightEnd = 0.2f;

        [Header("Свет")]
        [SerializeField] Light sun;
        [SerializeField] Color dayAmbient = new Color(0.62f, 0.66f, 0.72f);
        [SerializeField] Color nightAmbient = new Color(0.07f, 0.09f, 0.16f);
        [SerializeField] Color dayFog = new Color(0.66f, 0.79f, 0.88f);
        [SerializeField] Color sunsetFog = new Color(0.93f, 0.58f, 0.38f);
        [SerializeField] Color nightFog = new Color(0.04f, 0.05f, 0.09f);
        [SerializeField] Color daySun = new Color(1f, 0.96f, 0.86f);
        [SerializeField] Color sunsetSun = new Color(1f, 0.55f, 0.3f);

        public readonly NetworkVariable<float> TimeOfDay = new NetworkVariable<float>();
        public readonly NetworkVariable<int> Day = new NetworkVariable<int>(1);

        public bool IsNight => IsNightAt(TimeOfDay.Value);
        public float CycleLengthSeconds => cycleLengthSeconds;

        public bool IsNightAt(float t) => t >= nightStart || t < nightEnd;

        /// <summary>Сколько секунд реального времени осталось до смены фазы.</summary>
        public float SecondsToNextPhase()
        {
            float t = TimeOfDay.Value;
            float target = IsNight ? nightEnd : nightStart;
            float delta = Mathf.Repeat(target - t, 1f);
            return delta * cycleLengthSeconds;
        }

        void Awake()
        {
            Instance = this;
            ApplyVisuals(startTime);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) TimeOfDay.Value = startTime;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        void Update()
        {
            if (IsServer && IsSpawned)
            {
                float t = TimeOfDay.Value + Time.deltaTime / cycleLengthSeconds;
                if (t >= 1f)
                {
                    t -= 1f;
                    Day.Value++;
                }
                TimeOfDay.Value = t;
            }

            ApplyVisuals(IsSpawned ? TimeOfDay.Value : startTime);
        }

        void ApplyVisuals(float t)
        {
            // Высота солнца: 1 в полдень, -1 в полночь.
            float elevation = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);
            float dayFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.25f, elevation));
            float sunsetFactor = 1f - Mathf.Clamp01(Mathf.Abs(elevation) * 3f);

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler((t - 0.25f) * 360f, 160f, 0f);
                sun.intensity = Mathf.Lerp(0.03f, 1.3f, dayFactor);
                sun.color = Color.Lerp(daySun, sunsetSun, sunsetFactor);
                sun.shadowStrength = Mathf.Lerp(0.3f, 0.85f, dayFactor);
            }

            Color fog = Color.Lerp(nightFog, dayFog, dayFactor);
            fog = Color.Lerp(fog, sunsetFog, sunsetFactor * 0.6f * dayFactor + sunsetFactor * 0.25f);

            RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, dayFactor);
            RenderSettings.fogColor = fog;

            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = fog;
        }
    }
}
