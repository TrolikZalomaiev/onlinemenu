using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieSurvival
{
    /// <summary>
    /// Днём зомби медленные и бродят, ночью быстрые и чуют игроков на любом расстоянии.
    /// Если путь к игроку перекрыт постройками — ломают ближайший блок.
    /// Логика работает только на сервере, клиенты получают позицию через NetworkTransform.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAI : NetworkBehaviour
    {
        [SerializeField] float daySpeed = 1.8f;
        [SerializeField] float nightSpeed = 4.2f;
        [SerializeField] float daySightRange = 25f;
        [SerializeField] float attackRange = 1.7f;
        [SerializeField] float playerDamage = 12f;
        [SerializeField] float blockDamage = 25f;
        [SerializeField] float attackCooldown = 1.1f;
        [SerializeField] float blockSearchRadius = 2.2f;
        [SerializeField] float wanderRadius = 15f;

        NavMeshAgent agent;
        PlayerController target;
        float nextAttackTime;
        float nextThinkTime;
        static readonly Collider[] overlap = new Collider[24];

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            agent.enabled = IsServer;
        }

        void Update()
        {
            if (!IsServer || !IsSpawned || !agent.isOnNavMesh) return;

            bool night = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            agent.speed = night ? nightSpeed : daySpeed;

            if (Time.time >= nextThinkTime)
            {
                nextThinkTime = Time.time + 0.4f;
                target = FindTarget(night ? float.PositiveInfinity : daySightRange);
                if (target != null) agent.SetDestination(target.transform.position);
                else Wander();
            }

            if (target == null) return;

            Vector3 toTarget = target.transform.position - transform.position;
            if (toTarget.magnitude <= attackRange)
            {
                FaceTowards(toTarget);
                TryAttack(target.GetComponent<Health>(), playerDamage);
                return;
            }

            // Путь перекрыт стеной или зомби застрял возле неё — ломаем.
            bool blocked = agent.pathStatus != NavMeshPathStatus.PathComplete
                           || (!agent.pathPending && agent.velocity.sqrMagnitude < 0.05f);
            if (blocked)
            {
                var block = FindNearbyBlock();
                if (block != null)
                {
                    FaceTowards(block.transform.position - transform.position);
                    TryAttack(block.GetComponent<Health>(), blockDamage);
                }
            }
        }

        PlayerController FindTarget(float maxDistance)
        {
            PlayerController best = null;
            float bestDistance = maxDistance;
            foreach (var p in PlayerController.All)
            {
                if (p == null) continue;
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }
            return best;
        }

        void Wander()
        {
            if (agent.pathPending || agent.remainingDistance > 1f) return;
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            if (NavMesh.SamplePosition(transform.position + new Vector3(r.x, 0f, r.y), out NavMeshHit hit, 4f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        BuildBlock FindNearbyBlock()
        {
            Vector3 center = transform.position + transform.forward * 0.8f + Vector3.up;
            int count = Physics.OverlapSphereNonAlloc(center, blockSearchRadius, overlap, ~0, QueryTriggerInteraction.Ignore);
            BuildBlock best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var block = overlap[i].GetComponentInParent<BuildBlock>();
                if (block == null) continue;
                float d = (block.transform.position - transform.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = block;
                }
            }
            return best;
        }

        void TryAttack(Health victim, float damage)
        {
            if (victim == null || Time.time < nextAttackTime) return;
            nextAttackTime = Time.time + attackCooldown;
            victim.ApplyDamage(damage);
            AttackEffectRpc();
        }

        void FaceTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
        }

        [Rpc(SendTo.Everyone)]
        void AttackEffectRpc()
        {
            if (TryGetComponent(out ZombieVisual visual)) visual.PlayAttack();
        }
    }
}
