using UnityEngine;

namespace SnowBoarder
{
    /// <summary>
    /// Procedural animation for the two-part boarder sprite:
    /// lean while steering, crouch when braking/tucking, stretch for grabs,
    /// squash on landing and a ragdoll-ish tumble on crash.
    /// </summary>
    public class BoarderAnimator : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] Transform visualRoot;
        [SerializeField] Transform top;

        Vector3 topBasePos;
        Quaternion topBaseRot;
        float squash;
        float crouch, lean, grab;

        void Awake()
        {
            topBasePos = top.localPosition;
            topBaseRot = top.localRotation;
            player.Landed += (_, air, _, _) => squash = Mathf.Clamp(air * 0.25f, 0.08f, 0.25f);
            player.Jumped += () => squash = -0.12f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            bool crashed = player.IsCrashed;

            float targetCrouch = player.IsBraking ? 1f : (player.IsTouchingGround && player.Speed > 14f ? 0.5f : 0f);
            crouch = Mathf.MoveTowards(crouch, crashed ? 0f : targetCrouch, dt * 5f);
            lean = Mathf.MoveTowards(lean, crashed ? 0f : player.Steer, dt * 6f);
            grab = Mathf.MoveTowards(grab, player.IsGrabbing ? 1f : 0f, dt * 8f);

            // upper body
            Vector3 pos = topBasePos + new Vector3(0.05f * lean, -0.25f * crouch - 0.2f * grab, 0f);
            float rot = lean * 12f + grab * -25f;
            if (crashed) rot = Mathf.Sin(Time.time * 20f) * 30f;
            top.localPosition = pos;
            top.localRotation = topBaseRot * Quaternion.Euler(0, 0, rot);

            // squash & stretch on whole body
            squash = Mathf.MoveTowards(squash, 0f, dt * 1.2f);
            float s = squash;
            visualRoot.localScale = new Vector3(1f + s, 1f - s, 1f);
        }
    }
}
