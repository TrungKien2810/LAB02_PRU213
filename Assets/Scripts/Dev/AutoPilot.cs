using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SnowBoarder.Dev
{
    // TEMPORARY playtest helper: simulates keyboard input. Delete before submission.
    public class AutoPilot : MonoBehaviour
    {
        public bool doFlips = true;
        public string log = "";
        PlayerController p;
        float lastJump;

        void Update()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            p = gm.Player;
            var keys = new System.Collections.Generic.List<Key>();
            if (p.IsTouchingGround)
            {
                keys.Add(Key.UpArrow);
                foreach (var o in FindObjectsByType<Obstacle>())
                {
                    float dx = o.transform.position.x - p.transform.position.x;
                    if (dx > 0f && dx < p.Speed * 0.28f + 1.5f && Time.time - lastJump > 0.5f) { keys.Add(Key.Space); lastJump = Time.time; break; }
                }
            }
            else
            {
                // align with an assumed landing slope of ~-20 degrees
                float rot = Mathf.DeltaAngle(0, p.Body.rotation);
                bool flipping = doFlips && p.AirTime > 0.1f && p.AirTime < 0.5f && p.Body.linearVelocity.y > 2f;
                if (flipping) keys.Add(Key.LeftArrow);
                else if (rot > -10f && !(doFlips && Mathf.Abs(p.Body.angularVelocity) > 200 && rot > 60f)) keys.Add(Key.RightArrow);
                else if (rot < -35f) keys.Add(Key.LeftArrow);
                if (p.AirTime > 0.4f) keys.Add(Key.LeftShift);
            }
            var state = new KeyboardState(keys.ToArray());
            InputSystem.QueueStateEvent(Keyboard.current, state);
        }
    }
}
