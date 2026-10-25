using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieSurvival
{
    /// <summary>
    /// Набор хитскан-оружия игрока. 1–6 / колесо мыши — смена оружия, ЛКМ — огонь,
    /// ПКМ — прицел (у снайперки), R — перезарядка. Новое оружие открывается, когда
    /// в инвентаре появляется его предмет (нашёл в сундуке). Попадание определяет стрелок, урон наносит сервер.
    /// </summary>
    public class Weapon : NetworkBehaviour
    {
        [Serializable]
        public class WeaponDefinition
        {
            public string name = "Пистолет";
            public bool startUnlocked;
            public ItemType unlockItem = ItemType.WeaponAK;
            public GameObject model;
            public Transform muzzle;
            public float damage = 30f;
            public float shotsPerSecond = 4f;
            public bool automatic;
            public int magazineSize = 12;
            public float reloadTime = 1.4f;
            [Tooltip("Дробинок за выстрел (дробовик > 1).")]
            public int pellets = 1;
            [Tooltip("Разброс в градусах.")]
            public float spread = 0.5f;
            public float range = 120f;
            [Tooltip("Поле зрения в прицеле по ПКМ, 0 — без прицела.")]
            public float zoomFov;
            public float recoilKick = 1f;
        }

        [SerializeField] List<WeaponDefinition> weapons = new List<WeaponDefinition>();
        [SerializeField] float headshotMultiplier = 2f;
        [SerializeField] Transform holder;
        [Tooltip("Сколько патронов даёт найденный дубликат уже открытого оружия.")]
        [SerializeField] int duplicateAmmo = 15;

        public readonly NetworkVariable<int> Current = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        PlayerController player;
        PlayerInventory inventory;
        BuildingSystem building;
        Health ownHealth;

        int[] magazines;
        bool[] unlocked;
        float nextShotTime;
        float reloadEndTime;
        float recoil;
        float baseFov = 75f;
        Vector3 holderRestPosition;
        static Material tracerMaterial;
        readonly Dictionary<Health, float> pendingDamage = new Dictionary<Health, float>();

        public int Count => weapons.Count;
        public int CurrentIndex => Mathf.Clamp(Current.Value, 0, Mathf.Max(0, weapons.Count - 1));
        public WeaponDefinition CurrentWeapon => weapons.Count > 0 ? weapons[CurrentIndex] : null;
        public WeaponDefinition Get(int index) => weapons[index];
        public int InMagazine => magazines != null && magazines.Length > 0 ? magazines[CurrentIndex] : 0;
        public bool Reloading => reloadEndTime > 0f;
        public bool Aiming { get; private set; }

        /// <summary>Сообщение о найденном оружии для HUD.</summary>
        public string Notice { get; private set; }
        public float NoticeTime { get; private set; } = -100f;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
            building = GetComponent<BuildingSystem>();
            ownHealth = GetComponent<Health>();
            if (holder != null) holderRestPosition = holder.localPosition;
        }

        public override void OnNetworkSpawn()
        {
            magazines = new int[weapons.Count];
            unlocked = new bool[weapons.Count];
            for (int i = 0; i < weapons.Count; i++)
            {
                magazines[i] = weapons[i].magazineSize;
                unlocked[i] = weapons[i].startUnlocked;
            }

            Current.OnValueChanged += OnCurrentChanged;
            ShowModel(CurrentIndex);

            if (IsOwner)
            {
                if (player.Camera != null) baseFov = player.Camera.fieldOfView;
                inventory.Changed += OnInventoryChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            Current.OnValueChanged -= OnCurrentChanged;
            if (IsOwner) inventory.Changed -= OnInventoryChanged;
        }

        void OnCurrentChanged(int previous, int current) => ShowModel(current);

        void ShowModel(int index)
        {
            for (int i = 0; i < weapons.Count; i++)
                if (weapons[i].model != null) weapons[i].model.SetActive(i == Mathf.Clamp(index, 0, weapons.Count - 1));
        }

        public bool IsUnlocked(int index) => unlocked != null && index >= 0 && index < unlocked.Length && unlocked[index];

        void OnInventoryChanged()
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                if (w.startUnlocked) continue;
                int count = inventory.Count(w.unlockItem);
                if (count <= 0) continue;

                if (!unlocked[i])
                {
                    unlocked[i] = true;
                    Notice = $"Найдено оружие: {w.name}  [{i + 1}]";
                    NoticeTime = Time.time;
                    Select(i);
                }

                // Оружие держим в одном экземпляре, лишние превращаем в патроны.
                if (count > 1 && inventory.Remove(w.unlockItem, count - 1))
                    inventory.Add(ItemType.Ammo, duplicateAmmo * (count - 1));
            }
        }

        void Select(int index)
        {
            if (index == CurrentIndex || !IsUnlocked(index)) return;
            reloadEndTime = 0f;
            Current.Value = index;
        }

        void Update()
        {
            AnimateRecoil();
            if (!IsOwner || weapons.Count == 0) return;

            if (Reloading && Time.time >= reloadEndTime) FinishReload();

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool canUse = keyboard != null && mouse != null && player.HasControl && (building == null || !building.BuildMode);

            UpdateZoom(canUse && mouse.rightButton.isPressed && CurrentWeapon.zoomFov > 0f);
            if (!canUse) return;

            if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
            if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
            if (keyboard.digit3Key.wasPressedThisFrame) Select(2);
            if (keyboard.digit4Key.wasPressedThisFrame) Select(3);
            if (keyboard.digit5Key.wasPressedThisFrame) Select(4);
            if (keyboard.digit6Key.wasPressedThisFrame) Select(5);

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) Cycle(scroll > 0f ? -1 : 1);

            if (keyboard.rKey.wasPressedThisFrame) StartReload();

            var w = CurrentWeapon;
            bool trigger = w.automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (trigger && !Reloading && Time.time >= nextShotTime)
            {
                if (magazines[CurrentIndex] > 0) Shoot(w);
                else StartReload();
            }
        }

        void Cycle(int direction)
        {
            for (int step = 1; step < weapons.Count; step++)
            {
                int i = (CurrentIndex + direction * step + weapons.Count * step) % weapons.Count;
                if (IsUnlocked(i))
                {
                    Select(i);
                    return;
                }
            }
        }

        void UpdateZoom(bool aim)
        {
            Aiming = aim;
            if (player.Camera == null) return;
            float target = aim ? CurrentWeapon.zoomFov : baseFov;
            player.Camera.fieldOfView = Mathf.Lerp(player.Camera.fieldOfView, target, Time.deltaTime * 14f);
            if (holder != null) holder.gameObject.SetActive(!(aim && player.Camera.fieldOfView < baseFov * 0.6f));
        }

        void StartReload()
        {
            var w = CurrentWeapon;
            if (Reloading || magazines[CurrentIndex] >= w.magazineSize || !inventory.Has(ItemType.Ammo, 1)) return;
            reloadEndTime = Time.time + w.reloadTime;
        }

        void FinishReload()
        {
            reloadEndTime = 0f;
            int i = CurrentIndex;
            int take = Mathf.Min(weapons[i].magazineSize - magazines[i], inventory.Count(ItemType.Ammo));
            if (take > 0 && inventory.Remove(ItemType.Ammo, take)) magazines[i] += take;
        }

        void Shoot(WeaponDefinition w)
        {
            nextShotTime = Time.time + 1f / w.shotsPerSecond;
            magazines[CurrentIndex]--;
            recoil = w.recoilKick;

            Transform cam = player.Camera.transform;
            Vector3 start = w.muzzle != null && !Aiming ? w.muzzle.position : cam.position;
            float spread = Aiming ? w.spread * 0.2f : w.spread;
            pendingDamage.Clear();

            for (int p = 0; p < Mathf.Max(1, w.pellets); p++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * spread;
                Vector3 dir = cam.rotation * (Quaternion.Euler(offset.y, offset.x, 0f) * Vector3.forward);
                Vector3 end = cam.position + dir * w.range;

                if (Physics.Raycast(cam.position, dir, out RaycastHit hit, w.range, ~0, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var target = hit.collider.GetComponentInParent<Health>();
                    if (target != null && target != ownHealth && target.IsSpawned)
                    {
                        bool headshot = hit.point.y > target.transform.position.y + 1.45f;
                        float dmg = headshot ? w.damage * headshotMultiplier : w.damage;
                        pendingDamage[target] = (pendingDamage.TryGetValue(target, out float d) ? d : 0f) + dmg;
                    }
                }

                ShotEffectRpc(start, end);
            }

            foreach (var pair in pendingDamage) pair.Key.DamageRpc(pair.Value);
        }

        [Rpc(SendTo.Everyone)]
        void ShotEffectRpc(Vector3 start, Vector3 end)
        {
            if (tracerMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                tracerMaterial = new Material(shader) { color = new Color(1f, 0.85f, 0.4f) };
            }

            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = tracerMaterial;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = 0.025f;
            line.endWidth = 0.008f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(go, 0.04f);
        }

        void AnimateRecoil()
        {
            if (holder == null) return;
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
            float reloadDip = Reloading ? 0.15f : 0f;
            holder.localPosition = holderRestPosition + new Vector3(0f, -reloadDip, -0.07f * recoil);
            holder.localRotation = Quaternion.Euler(-6f * recoil, 0f, 0f);
        }
    }
}
