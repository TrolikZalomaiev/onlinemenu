using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ZombieSurvival
{
    /// <summary>
    /// Движение от первого лица: WASD, Shift — бег, Space — прыжок, мышь — обзор.
    /// Управляет только владелец, позиция синхронизируется через OwnerNetworkTransform.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        public static readonly List<PlayerController> All = new List<PlayerController>();
        public static PlayerController Local { get; private set; }

        [SerializeField] Transform head;
        [SerializeField] Camera playerCamera;
        [SerializeField] Transform bodyVisual;

        [Header("Движение")]
        [SerializeField] float walkSpeed = 4.5f;
        [SerializeField] float sprintSpeed = 7.5f;
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float mouseSensitivity = 0.12f;

        CharacterController controller;
        float pitch;
        float verticalVelocity;

        /// <summary>Открыто меню (крафт и т.п.) — персонаж не двигается.</summary>
        public bool InputBlocked { get; set; }
        public bool HasControl => !InputBlocked && Cursor.lockState == CursorLockMode.Locked;
        public Camera Camera => playerCamera;
        public Transform Head => head;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);

            bool mine = IsOwner;
            if (playerCamera != null)
            {
                playerCamera.enabled = mine;
                if (playerCamera.TryGetComponent(out AudioListener listener)) listener.enabled = mine;
            }

            if (!mine) return;

            Local = this;
            var menuCamera = FindAnyObjectByType<MenuCamera>();
            if (menuCamera != null) menuCamera.gameObject.SetActive(false);

            // Своё тело не рисуем (камера внутри головы), но тень оставляем.
            if (bodyVisual != null)
            {
                foreach (var r in bodyVisual.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            Respawn();
            SetCursorLocked(true);
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this)
            {
                Local = null;
                SetCursorLocked(false);
                var menuCamera = FindAnyObjectByType<MenuCamera>(FindObjectsInactive.Include);
                if (menuCamera != null) menuCamera.gameObject.SetActive(true);
            }
        }

        /// <summary>Вызывается у владельца: при входе и после смерти.</summary>
        public void Respawn()
        {
            Vector3 position = PlayerSpawnPoint.GetSpawnPosition();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalVelocity = 0f;
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (!IsOwner) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;

            if (!InputBlocked && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
            else if (!InputBlocked && mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);

            bool control = HasControl;

            if (control && mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            Vector2 input = Vector2.zero;
            if (control)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
            }

            Vector3 move = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f);
            float speed = control && keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (control && controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((move * speed + Vector3.up * verticalVelocity) * Time.deltaTime);

            if (transform.position.y < -50f) Respawn();
        }
    }
}
