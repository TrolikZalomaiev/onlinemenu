using UnityEngine;

namespace ZombieSurvival
{
    /// <summary>Точка появления игроков. Игроки появляются в случайном месте в радиусе.</summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] float radius = 4f;

        public static Vector3 GetSpawnPosition()
        {
            var point = FindAnyObjectByType<PlayerSpawnPoint>();
            Vector3 center = point != null ? point.transform.position : Vector3.zero;
            float r = point != null ? point.radius : 0f;

            Vector2 offset = Random.insideUnitCircle * r;
            Vector3 position = center + new Vector3(offset.x, 0f, offset.y);

            // Ставим на землю.
            if (Physics.Raycast(position + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point + Vector3.up * 0.1f;
            return position;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
