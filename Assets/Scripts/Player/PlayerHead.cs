using UnityEngine;

namespace SnowBoarder
{
    /// <summary>
    /// Trigger on the boarder's head. Touching the ground with your head = crash
    /// (unless the invincibility shield is active, which bounces you back up).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerHead : MonoBehaviour
    {
        [SerializeField] PlayerController player;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.GetComponent<Ground>() || player.IsCrashed || player.IsFrozen) return;

            if (player.IsInvincible)
            {
                // shield saves you: flip upright and pop up
                player.Body.angularVelocity = 0f;
                player.Body.rotation = 0f;
                player.Body.AddForce(Vector2.up * 6f * player.Body.mass, ForceMode2D.Impulse);
                AudioManager.PlaySfx(Sfx.Hit, 0.6f);
                return;
            }
            player.Crash();
        }
    }
}
