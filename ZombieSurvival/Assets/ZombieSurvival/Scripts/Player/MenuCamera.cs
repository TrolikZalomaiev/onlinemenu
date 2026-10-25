using UnityEngine;

namespace ZombieSurvival
{
    /// <summary>Камера главного меню. Выключается, когда появляется свой игрок.</summary>
    public class MenuCamera : MonoBehaviour
    {
        [SerializeField] float orbitSpeed = 3f;

        void Update()
        {
            transform.RotateAround(Vector3.zero, Vector3.up, orbitSpeed * Time.deltaTime);
        }
    }
}
