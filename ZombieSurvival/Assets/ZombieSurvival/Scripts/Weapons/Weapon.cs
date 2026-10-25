using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieSurvival
{
    /// <summary>
    /// Хитскан-оружие. ЛКМ — стрельба, R — перезарядка патронами из инвентаря.
    /// Попадание определяет стрелок, урон наносит сервер.
    /// </summary>
    public class Weapon : NetworkBehaviour
    {
        [SerializeField] float damage = 34f;
        [SerializeField] float headshotMultiplier = 2f;
        [SerializeField] float shotsPerSecond = 6f;
        [SerializeField] float range = 120f;
        [SerializeField] int magazineSize = 15;
        [SerializeField] float reloadTime = 1.6f;
        [SerializeField] Transform muzzle;
        [SerializeField] Transform model;

        PlayerController player;
        PlayerInventory inventory;
        BuildingSystem building;
        Health ownHealth;

        float nextShotTime;
        float reloadEndTime;
        float recoil;
        Vector3 modelRestPosition;
        static Material tracerMaterial;

        public int InMagazine { get; private set; }
        public int MagazineSize => magazineSize;
        public bool Reloading => reloadEndTime > 0f;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
            building = GetComponent<BuildingSystem>();
            ownHealth = GetComponent<Health>();
            if (model != null) modelRestPosition = model.localPosition;
        }

        public override void OnNetworkSpawn()
        {
            InMagazine = magazineSize;
        }

        void Update()
        {
            AnimateRecoil();
            if (!IsOwner) return;

            if (Reloading && Time.time >= reloadEndTime) FinishReload();

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || !player.HasControl) return;
            if (building != null && building.BuildMode) return;

            if (keyboard.rKey.wasPressedThisFrame) StartReload();

            if (mouse.leftButton.isPressed && !Reloading && Time.time >= nextShotTime)
            {
                if (InMagazine > 0) Shoot();
                else StartReload();
            }
        }

        void StartReload()
        {
            if (Reloading || InMagazine >= magazineSize || !inventory.Has(ItemType.Ammo, 1)) return;
            reloadEndTime = Time.time + reloadTime;
        }

        void FinishReload()
        {
            reloadEndTime = 0f;
            int take = Mathf.Min(magazineSize - InMagazine, inventory.Count(ItemType.Ammo));
            if (take > 0 && inventory.Remove(ItemType.Ammo, take)) InMagazine += take;
        }

        void Shoot()
        {
            nextShotTime = Time.time + 1f / shotsPerSecond;
            InMagazine--;
            recoil = 1f;

            Ray ray = player.Camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 end = ray.origin + ray.direction * range;

            if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var target = hit.collider.GetComponentInParent<Health>();
                if (target != null && target != ownHealth && target.IsSpawned)
                {
                    bool headshot = hit.point.y > target.transform.position.y + 1.45f;
                    target.DamageRpc(headshot ? damage * headshotMultiplier : damage);
                }
            }

            Vector3 start = muzzle != null ? muzzle.position : ray.origin;
            ShotEffectRpc(start, end);
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
            line.startWidth = 0.03f;
            line.endWidth = 0.01f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(go, 0.04f);
        }

        void AnimateRecoil()
        {
            if (model == null) return;
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
            float reloadDip = Reloading ? 0.12f : 0f;
            model.localPosition = modelRestPosition + new Vector3(0f, -reloadDip, -0.06f * recoil);
        }
    }
}
