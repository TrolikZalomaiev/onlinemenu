using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieSurvival
{
    /// <summary>
    /// Днём по карте бродит немного зомби. С наступлением ночи приходит орда,
    /// которая растёт с каждым днём и с количеством игроков.
    /// </summary>
    public class ZombieSpawner : NetworkBehaviour
    {
        [SerializeField] NetworkObject[] zombiePrefabs;

        [Header("Орда")]
        [SerializeField] int hordeBase = 6;
        [SerializeField] int hordePerDay = 4;
        [SerializeField] float hordePerExtraPlayer = 0.5f;
        [SerializeField] float hordeSpawnInterval = 1.2f;

        [Header("Днём")]
        [SerializeField] int dayWanderers = 5;
        [SerializeField] float daySpawnInterval = 8f;

        [Header("Общее")]
        [SerializeField] int maxAlive = 60;
        [SerializeField] float minDistance = 28f;
        [SerializeField] float maxDistance = 50f;

        readonly List<NetworkObject> alive = new List<NetworkObject>();
        bool wasNight;
        int hordeRemaining;
        float nextSpawnTime;

        public int Alive => alive.Count;
        public int HordeRemaining => hordeRemaining;

        void Update()
        {
            if (!IsServer || !IsSpawned || DayNightCycle.Instance == null || zombiePrefabs == null || zombiePrefabs.Length == 0)
                return;
            if (PlayerController.All.Count == 0) return;

            alive.RemoveAll(z => z == null || !z.IsSpawned);

            var cycle = DayNightCycle.Instance;
            bool night = cycle.IsNight;
            if (night && !wasNight)
            {
                int players = PlayerController.All.Count;
                float scale = 1f + (players - 1) * hordePerExtraPlayer;
                hordeRemaining = Mathf.RoundToInt((hordeBase + hordePerDay * (cycle.Day.Value - 1)) * scale);
            }
            if (!night) hordeRemaining = 0;
            wasNight = night;

            if (Time.time < nextSpawnTime || alive.Count >= maxAlive) return;

            if (night && hordeRemaining > 0)
            {
                if (TrySpawn()) hordeRemaining--;
                nextSpawnTime = Time.time + hordeSpawnInterval;
            }
            else if (!night && alive.Count < dayWanderers)
            {
                TrySpawn();
                nextSpawnTime = Time.time + daySpawnInterval;
            }
        }

        bool TrySpawn()
        {
            var players = PlayerController.All;
            var anchor = players[Random.Range(0, players.Count)];

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float distance = Random.Range(minDistance, maxDistance);
                Vector3 candidate = anchor.transform.position + new Vector3(dir.x, 0f, dir.y) * distance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas)) continue;
                if (IsTooCloseToPlayers(hit.position)) continue;

                var prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];
                var zombie = Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                zombie.Spawn(true);
                alive.Add(zombie);
                return true;
            }
            return false;
        }

        bool IsTooCloseToPlayers(Vector3 position)
        {
            foreach (var p in PlayerController.All)
                if (p != null && Vector3.Distance(p.transform.position, position) < minDistance * 0.8f)
                    return true;
            return false;
        }
    }
}
