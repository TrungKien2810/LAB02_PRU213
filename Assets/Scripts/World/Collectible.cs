using UnityEngine;

namespace SnowBoarder
{
    /// <summary>
    /// Snowflake pickup. Bobs and spins; flies towards the player when the magnet is active.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Collectible : MonoBehaviour
    {
        [SerializeField] int points = 50;
        [SerializeField] float magnetRadius = 9f;
        [SerializeField] GameObject collectEffectPrefab;

        Vector3 basePos;
        float phase;
        bool collected;
        bool magnetized;

        void Start()
        {
            basePos = transform.position;
            phase = Random.value * 10f;
        }

        void Update()
        {
            if (collected) return;
            var gm = GameManager.Instance;
            var player = gm ? gm.Player : null;

            if (player && player.HasMagnet && !player.IsCrashed &&
                (magnetized || Vector2.Distance(player.transform.position, transform.position) < magnetRadius))
            {
                magnetized = true;
                transform.position = Vector3.MoveTowards(transform.position, player.transform.position,
                    (player.Speed + 15f) * Time.deltaTime);
            }
            else
            {
                magnetized = false;
                transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 3f + phase) * 0.2f;
            }
            transform.Rotate(0, 0, 90f * Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || !other.GetComponentInParent<PlayerController>()) return;
            collected = true;
            GameManager.Instance?.CollectSnowflake(points);
            if (collectEffectPrefab) Destroy(Instantiate(collectEffectPrefab, transform.position, Quaternion.identity), 2f);
            Destroy(gameObject);
        }
    }
}
