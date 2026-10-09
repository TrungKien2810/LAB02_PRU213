using UnityEngine;

namespace SnowBoarder
{
    /// <summary>Speed pad on the ground: kicks the boarder forward along the slope.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class BoostPad : MonoBehaviour
    {
        [SerializeField] float force = 10f;
        [SerializeField] float boostDuration = 1.5f;
        float cooldown;

        void Update()
        {
            if (cooldown > 0f) cooldown -= Time.deltaTime;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (cooldown > 0f || !player || player.IsCrashed) return;
            cooldown = 1f;
            player.BoostPadKick(force, boostDuration);
            AudioManager.PlaySfx(Sfx.Boost, 0.9f);
            CameraFollow.Instance?.Shake(0.1f, 0.2f);
        }
    }
}
