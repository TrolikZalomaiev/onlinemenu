using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieSurvival
{
    /// <summary>Tab — меню крафта (патроны, аптечки, порох).</summary>
    public class CraftingSystem : NetworkBehaviour
    {
        [Serializable]
        public struct Ingredient
        {
            public ItemType item;
            public int amount;
        }

        [Serializable]
        public class Recipe
        {
            public string name;
            public ItemType result;
            public int amount = 1;
            public Ingredient[] ingredients;
        }

        [SerializeField] List<Recipe> recipes = new List<Recipe>
        {
            new Recipe
            {
                name = "Патроны x10", result = ItemType.Ammo, amount = 10,
                ingredients = new[] { new Ingredient { item = ItemType.Scrap, amount = 2 }, new Ingredient { item = ItemType.Gunpowder, amount = 1 } }
            },
            new Recipe
            {
                name = "Порох x2", result = ItemType.Gunpowder, amount = 2,
                ingredients = new[] { new Ingredient { item = ItemType.Stone, amount = 3 }, new Ingredient { item = ItemType.Wood, amount = 1 } }
            },
            new Recipe
            {
                name = "Аптечка", result = ItemType.Medkit, amount = 1,
                ingredients = new[] { new Ingredient { item = ItemType.Scrap, amount = 3 }, new Ingredient { item = ItemType.Wood, amount = 2 } }
            },
        };

        PlayerController player;
        PlayerInventory inventory;
        Vector2 scroll;

        public bool MenuOpen { get; private set; }

        void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
        }

        void Update()
        {
            if (!IsOwner) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.tabKey.wasPressedThisFrame) SetMenuOpen(!MenuOpen);
        }

        void SetMenuOpen(bool open)
        {
            MenuOpen = open;
            player.InputBlocked = open;
            PlayerController.SetCursorLocked(!open);
        }

        public bool CanCraft(Recipe recipe)
        {
            foreach (var ing in recipe.ingredients)
                if (!inventory.Has(ing.item, ing.amount)) return false;
            return true;
        }

        public bool Craft(Recipe recipe)
        {
            if (!CanCraft(recipe)) return false;
            foreach (var ing in recipe.ingredients) inventory.Remove(ing.item, ing.amount);
            inventory.Add(recipe.result, recipe.amount);
            return true;
        }

        void OnGUI()
        {
            if (!IsOwner || !MenuOpen) return;

            const float w = 380f, h = 320f;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUILayout.BeginArea(rect, "Крафт (Tab — закрыть)", GUI.skin.window);
            scroll = GUILayout.BeginScrollView(scroll);

            foreach (var recipe in recipes)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label(recipe.name);
                var parts = new List<string>();
                foreach (var ing in recipe.ingredients)
                    parts.Add($"{PlayerInventory.DisplayName(ing.item)} {inventory.Count(ing.item)}/{ing.amount}");
                GUILayout.Label(string.Join(", ", parts));
                GUILayout.EndVertical();

                GUI.enabled = CanCraft(recipe);
                if (GUILayout.Button("Создать", GUILayout.Width(90f), GUILayout.Height(40f))) Craft(recipe);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
