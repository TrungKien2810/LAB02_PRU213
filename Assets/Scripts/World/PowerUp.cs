using UnityEngine;

namespace SnowBoarder
{
    /// <summary>
    /// Trigger pickup: speed boost, invincibility shield, extra life, snowflake magnet or time bonus.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PowerUp : MonoBehaviour
    {
        [SerializeField] PowerUpType type;
        [SerializeField] float duration = 5f;
        [SerializeField] GameObject collectEffectPrefab;

        public PowerUpType Type => type;

        Vector3 basePos;

        void Start() => basePos = transform.position;

        void Update()
        {
            transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2.5f) * 0.3f;
            float s = 1f + Mathf.Sin(Time.time * 6f) * 0.06f;
            transform.localScale = new Vector3(s, s, 1f) * baseScale;
        }

        float baseScale = 1f;
        void Awake() => baseScale = transform.localScale.x;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.GetComponentInParent<PlayerController>()) return;
            GameManager.Instance?.CollectPowerUp(type, duration);
            if (collectEffectPrefab) Destroy(Instantiate(collectEffectPrefab, transform.position, Quaternion.identity), 2f);
            Destroy(gameObject);
        }
    }
}
