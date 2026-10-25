using UnityEngine;

namespace SnowBoarder
{
    /// <summary>
    /// Sensor on the boarder's head. Touching the snow with your head = crash
    /// (unless the invincibility shield is active, which bounces you back up).
    ///
    /// The collider is a solid collider, not a trigger, on purpose. A trigger produces no
    /// collision response and no continuous collision detection, so at the speeds this game
    /// reaches the head could pass straight through the snow without ever registering the
    /// crash - and the boarder would then be under the surface with nothing to stand on.
    ///
    /// The contact is reported by <see cref="PlayerController"/>, which is guaranteed to
    /// receive every collision of the rigidbody; this component only owns the rule.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerHead : MonoBehaviour
    {
        [SerializeField] PlayerController player;

        PlayerController Player
        {
            get
            {
                if (!player) player = GetComponentInParent<PlayerController>();
                return player;
            }
        }

        /// <summary>The head has touched the snow.</summary>
        public void TouchedSnow()
        {
            var p = Player;
            if (!p || p.IsCrashed || p.IsFrozen) return;

            if (p.IsInvincible)
            {
                // The shield saves you: lift clear first, then straighten up. Snapping the
                // rotation alone would swing the board from above the body origin to below it,
                // i.e. straight down through the zero-thickness snow surface.
                p.Body.angularVelocity = 0f;
                p.Body.position += Vector2.up * 1.2f;
                p.Body.rotation = 0f;
                p.Body.AddForce(Vector2.up * 6f * p.Body.mass, ForceMode2D.Impulse);
                AudioManager.PlaySfx(Sfx.Hit, 0.6f);
                return;
            }
            p.Crash();
        }
    }
}
