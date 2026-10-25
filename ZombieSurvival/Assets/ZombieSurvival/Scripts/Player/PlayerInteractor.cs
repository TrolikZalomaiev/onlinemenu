using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieSurvival
{
    /// <summary>E — обыскать / собрать, H — использовать аптечку.</summary>
    public class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] float reach = 3.5f;
        [SerializeField] float medkitHeal = 50f;

        PlayerController player;
        PlayerInventory inventory;
        Health health;

        public LootContainer Target { get; private set; }

        void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
            health = GetComponent<Health>();
        }

        void Update()
        {
            if (!IsOwner || player.Camera == null) return;

            Target = null;
            Ray ray = player.Camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Collide))
            {
                var loot = hit.collider.GetComponentInParent<LootContainer>();
                if (loot != null && loot.IsAvailable) Target = loot;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || !player.HasControl) return;

            if (Target != null && keyboard.eKey.wasPressedThisFrame)
                Target.LootRpc();

            if (keyboard.hKey.wasPressedThisFrame && health.Current.Value < health.Max && inventory.Remove(ItemType.Medkit, 1))
                health.HealRpc(medkitHeal);
        }
    }
}
