using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieSurvival
{
    /// <summary>
    /// B — режим строительства, 1/2/3 — тип блока, ЛКМ — поставить, ПКМ — разобрать (возврат половины ресурсов).
    /// Блоки стоят по сетке, ставит их сервер.
    /// </summary>
    public class BuildingSystem : NetworkBehaviour
    {
        [Serializable]
        public struct BlockType
        {
            public string name;
            public NetworkObject prefab;
            public ItemType costItem;
            public int cost;
        }

        [SerializeField] BlockType[] blocks;
        [SerializeField] float gridSize = 2f;
        [SerializeField] float maxDistance = 8f;
        [SerializeField] Material previewMaterial;
        [SerializeField] Color validColor = new Color(0.3f, 1f, 0.4f, 0.4f);
        [SerializeField] Color invalidColor = new Color(1f, 0.25f, 0.2f, 0.4f);

        PlayerController player;
        PlayerInventory inventory;
        GameObject preview;
        Material previewInstance;
        bool previewValid;

        public bool BuildMode { get; private set; }
        public int SelectedIndex { get; private set; }
        public BlockType Selected => blocks[SelectedIndex];
        public int BlockCount => blocks?.Length ?? 0;
        public BlockType GetBlock(int index) => blocks[index];

        void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
        }

        public override void OnNetworkDespawn()
        {
            if (preview != null) Destroy(preview);
        }

        void Update()
        {
            if (!IsOwner || BlockCount == 0) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (player.HasControl && keyboard.bKey.wasPressedThisFrame) BuildMode = !BuildMode;

            if (BuildMode && player.HasControl)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectedIndex = 0;
                if (keyboard.digit2Key.wasPressedThisFrame && BlockCount > 1) SelectedIndex = 1;
                if (keyboard.digit3Key.wasPressedThisFrame && BlockCount > 2) SelectedIndex = 2;
            }

            if (!BuildMode || !player.HasControl || !TryGetCell(out Vector3 cell, out RaycastHit hit))
            {
                SetPreviewVisible(false);
                return;
            }

            previewValid = IsCellFree(cell) && inventory.Has(Selected.costItem, Selected.cost);
            UpdatePreview(cell);

            if (mouse.leftButton.wasPressedThisFrame && previewValid && inventory.Remove(Selected.costItem, Selected.cost))
                PlaceBlockRpc(SelectedIndex, cell);

            if (mouse.rightButton.wasPressedThisFrame)
            {
                var block = hit.collider.GetComponentInParent<BuildBlock>();
                if (block != null) DemolishRpc(block.NetworkObject);
            }
        }

        bool TryGetCell(out Vector3 cell, out RaycastHit hit)
        {
            cell = default;
            Ray ray = player.Camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out hit, maxDistance, ~0, QueryTriggerInteraction.Ignore)) return false;

            Vector3 p = hit.point + hit.normal * (gridSize * 0.5f);
            cell = new Vector3(
                Mathf.Round(p.x / gridSize) * gridSize,
                Mathf.Floor(p.y / gridSize) * gridSize + gridSize * 0.5f,
                Mathf.Round(p.z / gridSize) * gridSize);
            return true;
        }

        bool IsCellFree(Vector3 cell)
        {
            return !Physics.CheckBox(cell, Vector3.one * (gridSize * 0.45f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        }

        void UpdatePreview(Vector3 cell)
        {
            if (preview == null)
            {
                preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                preview.name = "BuildPreview";
                Destroy(preview.GetComponent<Collider>());
                var r = preview.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (previewMaterial != null)
                {
                    previewInstance = new Material(previewMaterial);
                    r.sharedMaterial = previewInstance;
                }
                else
                {
                    previewInstance = r.material;
                }
            }

            SetPreviewVisible(true);
            preview.transform.SetPositionAndRotation(cell, Quaternion.identity);
            preview.transform.localScale = Vector3.one * (gridSize * 1.01f);
            previewInstance.color = previewValid ? validColor : invalidColor;
        }

        void SetPreviewVisible(bool visible)
        {
            if (preview != null && preview.activeSelf != visible) preview.SetActive(visible);
        }

        [Rpc(SendTo.Server)]
        void PlaceBlockRpc(int index, Vector3 cell)
        {
            if (index < 0 || index >= BlockCount) return;
            var type = blocks[index];

            bool ok = type.prefab != null
                      && Vector3.Distance(transform.position, cell) <= maxDistance + gridSize
                      && IsCellFree(cell);

            if (!ok)
            {
                // Вернуть ресурсы, если сервер отказал.
                inventory.GiveRpc(type.costItem, type.cost);
                return;
            }

            // Размер блока задан в самом префабе и должен совпадать с gridSize.
            var instance = Instantiate(type.prefab, cell, Quaternion.identity);
            if (instance.TryGetComponent(out BuildBlock block))
            {
                block.CostItem = type.costItem;
                block.Cost = type.cost;
            }
            instance.Spawn(true);
        }

        [Rpc(SendTo.Server)]
        void DemolishRpc(NetworkObjectReference target)
        {
            if (!target.TryGet(out NetworkObject obj) || !obj.TryGetComponent(out BuildBlock block)) return;
            if (Vector3.Distance(transform.position, obj.transform.position) > maxDistance + gridSize) return;

            int refund = block.Cost / 2;
            if (refund > 0) inventory.GiveRpc(block.CostItem, refund);
            obj.Despawn();
        }
    }
}
