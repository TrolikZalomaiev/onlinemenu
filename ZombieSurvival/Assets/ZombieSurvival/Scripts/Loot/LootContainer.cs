using System;
using Unity.Netcode;
using UnityEngine;

namespace ZombieSurvival
{
    /// <summary>
    /// Ящик / бочка / дерево / камень, который можно обыскать по E.
    /// Добычу выдаёт сервер, после чего контейнер пуст до respawnSeconds.
    /// </summary>
    public class LootContainer : NetworkBehaviour
    {
        [Serializable]
        public struct LootEntry
        {
            public ItemType item;
            public int min;
            public int max;
            [Range(0f, 1f)] public float chance;
        }

        [SerializeField] string displayName = "Ящик";
        [SerializeField] LootEntry[] loot =
        {
            new LootEntry { item = ItemType.Scrap, min = 1, max = 4, chance = 0.9f },
            new LootEntry { item = ItemType.Gunpowder, min = 1, max = 3, chance = 0.6f },
            new LootEntry { item = ItemType.Ammo, min = 5, max = 15, chance = 0.4f },
            new LootEntry { item = ItemType.Medkit, min = 1, max = 1, chance = 0.15f },
        };
        [SerializeField] float respawnSeconds = 600f;
        [SerializeField] float maxUseDistance = 5f;
        [Tooltip("Спрятать модель, пока контейнер пуст (например, для веток / камней).")]
        [SerializeField] GameObject hideWhenLooted;

        public readonly NetworkVariable<bool> Looted = new NetworkVariable<bool>();

        float respawnAt;

        public string DisplayName => displayName;
        public bool IsAvailable => IsSpawned && !Looted.Value;

        public override void OnNetworkSpawn()
        {
            Looted.OnValueChanged += OnLootedChanged;
            OnLootedChanged(false, Looted.Value);
        }

        public override void OnNetworkDespawn()
        {
            Looted.OnValueChanged -= OnLootedChanged;
        }

        void OnLootedChanged(bool previous, bool looted)
        {
            if (hideWhenLooted != null) hideWhenLooted.SetActive(!looted);
        }

        [Rpc(SendTo.Server)]
        public void LootRpc(RpcParams rpcParams = default)
        {
            if (Looted.Value) return;

            ulong sender = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(sender, out var client) || client.PlayerObject == null)
                return;

            var playerObject = client.PlayerObject;
            if (Vector3.Distance(playerObject.transform.position, transform.position) > maxUseDistance)
                return;

            var inventory = playerObject.GetComponent<PlayerInventory>();
            foreach (var entry in loot)
            {
                if (UnityEngine.Random.value > entry.chance) continue;
                int amount = UnityEngine.Random.Range(entry.min, entry.max + 1);
                if (amount > 0) inventory.GiveRpc(entry.item, amount);
            }

            Looted.Value = true;
            respawnAt = Time.time + respawnSeconds;
        }

        void Update()
        {
            if (IsServer && Looted.Value && Time.time >= respawnAt)
                Looted.Value = false;
        }
    }
}
