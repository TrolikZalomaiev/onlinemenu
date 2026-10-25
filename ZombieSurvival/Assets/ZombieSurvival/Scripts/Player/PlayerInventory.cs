using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZombieSurvival
{
    // Новые значения добавлять только в конец — номера сохранены в префабах.
    public enum ItemType { Scrap, Gunpowder, Wood, Stone, Ammo, Medkit, WeaponAK, WeaponM4, WeaponShotgun, WeaponSMG, WeaponSniper }

    /// <summary>
    /// Инвентарь хранится у владельца. Сервер выдаёт предметы через GiveRpc.
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        [Serializable]
        public struct Stack
        {
            public ItemType item;
            public int amount;
        }

        [SerializeField] Stack[] startingItems =
        {
            new Stack { item = ItemType.Ammo, amount = 30 },
            new Stack { item = ItemType.Wood, amount = 12 },
            new Stack { item = ItemType.Scrap, amount = 4 },
            new Stack { item = ItemType.Medkit, amount = 1 },
        };

        readonly Dictionary<ItemType, int> items = new Dictionary<ItemType, int>();

        public event Action Changed;
        public IReadOnlyDictionary<ItemType, int> Items => items;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            foreach (var s in startingItems) Add(s.item, s.amount);
        }

        public int Count(ItemType item) => items.TryGetValue(item, out int n) ? n : 0;
        public bool Has(ItemType item, int amount) => Count(item) >= amount;

        public void Add(ItemType item, int amount)
        {
            if (amount <= 0) return;
            items[item] = Count(item) + amount;
            Changed?.Invoke();
        }

        public bool Remove(ItemType item, int amount)
        {
            if (!Has(item, amount)) return false;
            items[item] = Count(item) - amount;
            Changed?.Invoke();
            return true;
        }

        [Rpc(SendTo.Owner)]
        public void GiveRpc(ItemType item, int amount) => Add(item, amount);

        public static string DisplayName(ItemType item)
        {
            switch (item)
            {
                case ItemType.Scrap: return "Металлолом";
                case ItemType.Gunpowder: return "Порох";
                case ItemType.Wood: return "Дерево";
                case ItemType.Stone: return "Камень";
                case ItemType.Ammo: return "Патроны";
                case ItemType.Medkit: return "Аптечка";
                case ItemType.WeaponAK: return "АК-47";
                case ItemType.WeaponM4: return "M4";
                case ItemType.WeaponShotgun: return "Дробовик";
                case ItemType.WeaponSMG: return "Пистолет-пулемёт";
                case ItemType.WeaponSniper: return "Снайперская винтовка";
                default: return item.ToString();
            }
        }
    }
}
