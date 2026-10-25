using UnityEngine;

namespace ZombieSurvival
{
    /// <summary>
    /// Простая процедурная анимация (покачивание при ходьбе и рывок при атаке),
    /// пока нет настоящих анимаций. Работает на всех клиентах.
    /// </summary>
    public class ZombieVisual : MonoBehaviour
    {
        [SerializeField] Transform model;
        [SerializeField] float swayAngle = 8f;
        [SerializeField] float swayFrequency = 6f;

        Vector3 lastPosition;
        float phase;
        float attackTimer;
        Quaternion restRotation;

        void Awake()
        {
            if (model != null) restRotation = model.localRotation;
            lastPosition = transform.position;
            phase = Random.value * 10f;
        }

        public void PlayAttack() => attackTimer = 0.35f;

        void LateUpdate()
        {
            if (model == null) return;

            float speed = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPosition = transform.position;

            float moving = Mathf.Clamp01(speed / 3f);
            phase += Time.deltaTime * swayFrequency * (0.5f + moving);

            float roll = Mathf.Sin(phase) * swayAngle * moving;
            float lean = 10f + 8f * moving;
            if (attackTimer > 0f)
            {
                attackTimer -= Time.deltaTime;
                lean += Mathf.Sin(attackTimer / 0.35f * Mathf.PI) * 30f;
            }

            model.localRotation = restRotation * Quaternion.Euler(lean, 0f, roll);
        }
    }
}
