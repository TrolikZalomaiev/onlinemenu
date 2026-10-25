using Unity.Netcode;
using UnityEngine;

namespace ZombieSurvival
{
    /// <summary>
    /// Построенный игроком блок. Зомби ищут такие блоки, если путь к игроку перекрыт.
    /// На префабе также нужны Health, NetworkObject и NavMeshObstacle (Carve).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class BuildBlock : NetworkBehaviour
    {
        /// <summary>Заполняется сервером при постройке (для возврата ресурсов при разборе).</summary>
        public ItemType CostItem { get; set; }
        public int Cost { get; set; }

        [SerializeField] Renderer[] damageTintRenderers;
        [SerializeField] Color damagedColor = new Color(0.35f, 0.2f, 0.15f);

        Health health;
        MaterialPropertyBlock block;
        Color[] baseColors;

        void Awake()
        {
            health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            health.Current.OnValueChanged += OnHealthChanged;
        }

        public override void OnNetworkDespawn()
        {
            health.Current.OnValueChanged -= OnHealthChanged;
        }

        void OnHealthChanged(float previous, float current)
        {
            if (damageTintRenderers == null || damageTintRenderers.Length == 0) return;

            block ??= new MaterialPropertyBlock();
            if (baseColors == null)
            {
                baseColors = new Color[damageTintRenderers.Length];
                for (int i = 0; i < baseColors.Length; i++)
                    baseColors[i] = damageTintRenderers[i].sharedMaterial.color;
            }

            float t = 1f - Mathf.Clamp01(current / health.Max);
            for (int i = 0; i < damageTintRenderers.Length; i++)
            {
                damageTintRenderers[i].GetPropertyBlock(block);
                Color c = Color.Lerp(baseColors[i], damagedColor, t * 0.8f);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                damageTintRenderers[i].SetPropertyBlock(block);
            }
        }
    }
}
