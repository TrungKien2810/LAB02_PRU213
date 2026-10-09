using UnityEngine;

namespace SnowBoarder
{
    /// <summary>Respawn point after a crash.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] Vector3 respawnOffset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] SpriteRenderer flag;
        bool reached;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (reached || !other.GetComponentInParent<PlayerController>()) return;
            reached = true;
            if (flag) flag.color = new Color(0.4f, 1f, 0.5f);
            GameManager.Instance?.SetCheckpoint(transform.position + respawnOffset);
        }
    }
}
