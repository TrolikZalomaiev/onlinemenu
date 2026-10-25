using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ZombieSurvival
{
    /// <summary>
    /// Простое меню: «Создать игру» (хост) или «Подключиться» по IP.
    /// Для игры через интернет без проброса портов позже подключим Unity Relay.
    /// </summary>
    public class NetworkMenu : MonoBehaviour
    {
        [Tooltip("Все префабы с NetworkObject, которые спавнятся во время игры (зомби, блоки).")]
        [SerializeField] GameObject[] networkPrefabs;
        [SerializeField] string address = "127.0.0.1";
        [SerializeField] ushort port = 7777;

        string portText;
        bool prefabsRegistered;

        void Awake()
        {
            portText = port.ToString();
        }

        void RegisterPrefabs()
        {
            if (prefabsRegistered) return;
            prefabsRegistered = true;
            foreach (var prefab in networkPrefabs)
                if (prefab != null) NetworkManager.Singleton.AddNetworkPrefab(prefab);
        }

        void Configure(string listenAddress)
        {
            RegisterPrefabs();
            ushort.TryParse(portText, out port);
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(address.Trim(), port, listenAddress);
        }

        void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (!nm.IsClient && !nm.IsServer)
            {
                const float w = 320f, h = 250f;
                GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), "ZOMBIE SURVIVAL — кооп", GUI.skin.window);
                GUILayout.Space(8f);

                if (GUILayout.Button("Создать игру (хост)", GUILayout.Height(42f)))
                {
                    Configure("0.0.0.0");
                    nm.StartHost();
                }

                GUILayout.Space(12f);
                GUILayout.Label("IP друга:");
                address = GUILayout.TextField(address);
                GUILayout.Label("Порт:");
                portText = GUILayout.TextField(portText);

                if (GUILayout.Button("Подключиться", GUILayout.Height(36f)))
                {
                    Configure(null);
                    nm.StartClient();
                }
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(Screen.width - 210f, 10f, 200f, 70f));
            string mode = nm.IsHost ? "Хост" : nm.IsServer ? "Сервер" : "Клиент";
            GUILayout.Label($"{mode} · игроков: {PlayerController.All.Count}");
            if (Cursor.lockState != CursorLockMode.Locked && GUILayout.Button("Выйти в меню"))
                nm.Shutdown();
            GUILayout.EndArea();
        }
    }
}
