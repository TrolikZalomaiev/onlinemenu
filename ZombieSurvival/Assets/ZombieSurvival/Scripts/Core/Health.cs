using System;
using Unity.Netcode;
using UnityEngine;

namespace ZombieSurvival
{
    public enum DeathBehavior { Despawn, Respawn }

    /// <summary>
    /// Здоровье для игроков, зомби и построек. Урон всегда считает сервер.
    /// </summary>
    public class Health : NetworkBehaviour
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] DeathBehavior onDeath = DeathBehavior.Despawn;

        public readonly NetworkVariable<float> Current = new NetworkVariable<float>();

        public float Max => maxHealth;
        public bool IsDead => Current.Value <= 0f;

        /// <summary>Вызывается только на сервере.</summary>
        public event Action<Health> Died;

        public override void OnNetworkSpawn()
        {
            if (IsServer) Current.Value = maxHealth;
        }

        /// <summary>Клиент просит сервер нанести урон (например, попал из оружия).</summary>
        [Rpc(SendTo.Server)]
        public void DamageRpc(float amount) => ApplyDamage(amount);

        [Rpc(SendTo.Server)]
        public void HealRpc(float amount)
        {
            if (IsDead) return;
            Current.Value = Mathf.Min(maxHealth, Current.Value + amount);
        }

        /// <summary>Только на сервере.</summary>
        public void ApplyDamage(float amount)
        {
            if (!IsServer || IsDead || amount <= 0f) return;

            Current.Value = Mathf.Max(0f, Current.Value - amount);
            if (Current.Value > 0f) return;

            Died?.Invoke(this);
            if (onDeath == DeathBehavior.Despawn)
            {
                NetworkObject.Despawn();
            }
            else
            {
                Current.Value = maxHealth;
                RespawnRpc();
            }
        }

        [Rpc(SendTo.Owner)]
        void RespawnRpc()
        {
            if (TryGetComponent(out PlayerController player)) player.Respawn();
        }
    }
}
