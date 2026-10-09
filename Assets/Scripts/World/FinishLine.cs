using UnityEngine;

namespace SnowBoarder
{
    [RequireComponent(typeof(Collider2D))]
    public class FinishLine : MonoBehaviour
    {
        [SerializeField] ParticleSystem finishEffect;
        bool done;

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (done || !player || player.IsCrashed) return;
            done = true;
            if (finishEffect) finishEffect.Play();
            GameManager.Instance?.OnFinishReached();
        }
    }
}
