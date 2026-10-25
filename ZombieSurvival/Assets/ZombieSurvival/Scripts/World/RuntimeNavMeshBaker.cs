using Unity.AI.Navigation;
using UnityEngine;

namespace ZombieSurvival
{
    /// <summary>
    /// Запекает NavMesh при запуске сцены, чтобы не хранить запечённые данные в репозитории.
    /// Постройки игроков вырезают себя из NavMesh через NavMeshObstacle (Carve).
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<NavMeshSurface>().BuildNavMesh();
        }
    }
}
