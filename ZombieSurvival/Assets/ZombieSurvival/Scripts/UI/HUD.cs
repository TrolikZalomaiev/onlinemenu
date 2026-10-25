using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieSurvival
{
    /// <summary>
    /// Временный интерфейс на OnGUI: здоровье, патроны, время суток, инвентарь, подсказки.
    /// Позже заменим на нормальный UI Toolkit / uGUI.
    /// </summary>
    public class HUD : NetworkBehaviour
    {
        Health health;
        Weapon weapon;
        PlayerInventory inventory;
        PlayerInteractor interactor;
        BuildingSystem building;
        bool showHelp = true;

        GUIStyle big, small, center;

        void Awake()
        {
            health = GetComponent<Health>();
            weapon = GetComponent<Weapon>();
            inventory = GetComponent<PlayerInventory>();
            interactor = GetComponent<PlayerInteractor>();
            building = GetComponent<BuildingSystem>();
        }

        void Update()
        {
            if (IsOwner && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) showHelp = !showHelp;
        }

        void EnsureStyles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            center = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (!IsOwner || !IsSpawned) return;
            EnsureStyles();

            float sw = Screen.width, sh = Screen.height;

            // Прицел
            GUI.Label(new Rect(sw / 2f - 10f, sh / 2f - 14f, 20f, 28f), "+", center);

            // Время суток
            var cycle = DayNightCycle.Instance;
            if (cycle != null)
            {
                float hours = cycle.TimeOfDay.Value * 24f;
                int hh = Mathf.FloorToInt(hours), mm = Mathf.FloorToInt((hours - hh) * 60f);
                int left = Mathf.CeilToInt(cycle.SecondsToNextPhase());
                string phase = cycle.IsNight ? $"НОЧЬ — рассвет через {left / 60}:{left % 60:00}" : $"День — ночь через {left / 60}:{left % 60:00}";
                GUI.Label(new Rect(sw / 2f - 250f, 10f, 500f, 30f), $"День {cycle.Day.Value} · {hh:00}:{mm:00} · {phase}", center);
            }

            // Здоровье
            float hpFraction = health.Max > 0f ? health.Current.Value / health.Max : 0f;
            GUI.Box(new Rect(20f, sh - 50f, 260f, 26f), GUIContent.none);
            var old = GUI.color;
            GUI.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.3f, 0.85f, 0.3f), hpFraction);
            GUI.DrawTexture(new Rect(23f, sh - 47f, 254f * hpFraction, 20f), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(28f, sh - 49f, 250f, 24f), $"HP {Mathf.CeilToInt(health.Current.Value)}", small);

            // Патроны / строительство
            if (building != null && building.BuildMode)
            {
                var b = building.Selected;
                GUI.Label(new Rect(sw - 360f, sh - 80f, 340f, 40f), $"Стройка: {b.name}", big);
                GUI.Label(new Rect(sw - 360f, sh - 44f, 340f, 30f),
                    $"{PlayerInventory.DisplayName(b.costItem)}: {inventory.Count(b.costItem)} (нужно {b.cost})", small);
            }
            else if (weapon != null)
            {
                string ammo = weapon.Reloading ? "перезарядка..." : $"{weapon.InMagazine} / {inventory.Count(ItemType.Ammo)}";
                GUI.Label(new Rect(sw - 260f, sh - 60f, 240f, 40f), ammo, big);
            }

            // Инвентарь
            GUILayout.BeginArea(new Rect(20f, 20f, 220f, 220f), GUI.skin.box);
            GUILayout.Label("Инвентарь", small);
            foreach (var pair in inventory.Items)
                if (pair.Value > 0) GUILayout.Label($"{PlayerInventory.DisplayName(pair.Key)}: {pair.Value}");
            GUILayout.EndArea();

            // Подсказка взаимодействия
            if (interactor != null && interactor.Target != null)
                GUI.Label(new Rect(sw / 2f - 200f, sh / 2f + 30f, 400f, 30f), $"[E] Обыскать: {interactor.Target.DisplayName}", center);

            if (showHelp)
            {
                GUILayout.BeginArea(new Rect(20f, 250f, 300f, 240f), GUI.skin.box);
                GUILayout.Label("WASD — ходить, Shift — бег, Space — прыжок");
                GUILayout.Label("ЛКМ — стрелять, R — перезарядка");
                GUILayout.Label("E — обыскать / собрать");
                GUILayout.Label("Tab — крафт, H — аптечка");
                GUILayout.Label("B — стройка, 1/2/3 — блок");
                GUILayout.Label("ЛКМ — поставить, ПКМ — разобрать");
                GUILayout.Label("Esc — курсор, F1 — скрыть подсказку");
                GUILayout.EndArea();
            }
        }
    }
}
