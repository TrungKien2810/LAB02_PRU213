using UnityEngine;

namespace SnowBoarder
{
    public enum ObstacleKind { Rock, Tree }

    /// <summary>
    /// Obstacles on the slope.
    ///  Rock: bump = lose speed and break your combo (jump over it!).
    ///  Tree: hit = crash.
    /// With the invincibility shield both are smashed for bonus points.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Obstacle : MonoBehaviour
    {
        [SerializeField] ObstacleKind kind = ObstacleKind.Rock;
        [SerializeField] float speedLoss = 0.55f;
        [SerializeField] int smashPoints = 250;
        [SerializeField] GameObject smashEffectPrefab;

        bool used;

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (used || !player || player.IsCrashed || player.IsFrozen) return;
            used = true;

            if (player.IsInvincible)
            {
                if (smashEffectPrefab) Destroy(Instantiate(smashEffectPrefab, transform.position, Quaternion.identity), 2f);
                AudioManager.PlaySfx(Sfx.Smash, 0.9f);
                CameraFollow.Instance?.Shake(0.2f, 0.2f);
                GameManager.Instance?.OnObstacleSmashed(smashPoints);
                Destroy(gameObject);
                return;
            }

            if (kind == ObstacleKind.Tree)
            {
                player.Crash();
            }
            else
            {
                player.HitObstacle(speedLoss);
                GameManager.Instance?.OnObstacleBumped();
                // knock the rock aside visually
                GetComponent<SpriteRenderer>().color = new Color(1f, 0.7f, 0.7f);
            }
        }
    }
}
