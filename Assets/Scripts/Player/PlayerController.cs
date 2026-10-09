using UnityEngine;
using UnityEngine.InputSystem;

namespace SnowBoarder
{
    public enum PowerUpType { SpeedBoost, Invincibility, ExtraLife, Magnet, TimeBonus }

    /// <summary>
    /// Physics based snowboarder.
    ///  - Gravity + a low-friction PhysicsMaterial2D let the slope angle drive acceleration (momentum).
    ///  - Extra snow friction / air drag are applied in FixedUpdate so speed depends on the slope.
    ///  - Left/Right rotate the boarder (lean on ground, spin in the air for flips).
    ///  - Up pushes (accelerate), Down brakes / tucks, Space jumps, Shift grabs in the air.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] float snowFriction = 0.6f;      // m/s^2 resistance while carving
        [SerializeField] float brakeFriction = 9f;       // m/s^2 when holding brake
        [SerializeField] float airDrag = 0.006f;         // quadratic drag coefficient
        [SerializeField] float groundSpinFactor = 0.35f; // spin multiplier while grounded
        [SerializeField] float spinAcceleration = 1800f; // deg/s^2
        [SerializeField] float coyoteTime = 0.12f;

        [Header("References")]
        [SerializeField] SpriteRenderer topRenderer;
        [SerializeField] SpriteRenderer bottomRenderer;
        [SerializeField] GameObject shieldVisual;
        [SerializeField] ParticleSystem snowSpray;
        [SerializeField] ParticleSystem crashEffect;
        [SerializeField] TrailRenderer boostTrail;

        public Rigidbody2D Body { get; private set; }
        public CharacterData Stats { get; private set; }
        public bool IsGrounded => groundContacts > 0 || Time.time - lastGroundedTime < coyoteTime;
        public bool IsTouchingGround => groundContacts > 0;
        public float Speed => Body.linearVelocity.magnitude;
        public float SpeedKmh => Speed * 3.6f;
        public bool IsCrashed { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool IsInvincible => invincibleTimer > 0f || respawnGraceTimer > 0f;
        public bool IsBoosting => boostTimer > 0f;
        public bool IsGrabbing { get; private set; }
        public bool IsBraking { get; private set; }
        public float Steer { get; private set; }
        public float InvincibleTimeLeft => invincibleTimer;
        public float BoostTimeLeft => boostTimer;
        public float MagnetTimeLeft => magnetTimer;
        public float AirTime => IsTouchingGround ? 0f : Time.time - leftGroundTime;
        public Vector2 GroundNormal { get; private set; } = Vector2.up;

        /// <summary>Raised on landing: (flips, airTime, grabTime, cleanLanding).</summary>
        public event System.Action<int, float, float, bool> Landed;
        public event System.Action Jumped;

        int groundContacts;
        float lastGroundedTime;
        float leftGroundTime;
        float airRotation;      // accumulated degrees while airborne
        float grabTime;
        float lastAngle;
        float boostTimer, invincibleTimer, magnetTimer, respawnGraceTimer;
        float slowTimer;
        bool jumpQueued;
        bool spawnDrop = true; // first touchdown after (re)spawning is not a trick landing

        void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            leftGroundTime = Time.time;
            Stats = CharacterData.Current;
            if (topRenderer) topRenderer.color = Stats.JacketTint;
            if (bottomRenderer) bottomRenderer.color = CharacterData.BoardColors[GameSettings.BoardIndex];
            if (shieldVisual) shieldVisual.SetActive(false);
            if (boostTrail) boostTrail.emitting = false;
            lastAngle = Body.rotation;
        }

        void Update()
        {
            if (IsFrozen || IsCrashed) { Steer = 0f; IsBraking = false; IsGrabbing = false; return; }

            var kb = Keyboard.current;
            float steer = 0f;
            bool accel = false, brake = false, grab = false;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) steer += 1f;   // counter-clockwise
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) steer -= 1f; // clockwise
                accel = kb.upArrowKey.isPressed || kb.wKey.isPressed;
                brake = kb.downArrowKey.isPressed || kb.sKey.isPressed;
                grab = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (kb.spaceKey.wasPressedThisFrame) jumpQueued = true;
            }
            Steer = steer;
            accelHeld = accel;
            IsBraking = brake && IsTouchingGround;
            IsGrabbing = grab && !IsTouchingGround && AirTime > 0.1f;
            if (IsGrabbing) grabTime += Time.deltaTime;

            TickTimers(Time.deltaTime);
        }

        bool accelHeld;

        void FixedUpdate()
        {
            if (IsFrozen || IsCrashed) return;

            float dt = Time.fixedDeltaTime;
            Vector2 v = Body.linearVelocity;

            // --- rotation / tricks ---
            // steer towards a target spin rate: responsive, but keeps momentum when released mid-air
            float spin = Stats.SpinSpeed * (IsTouchingGround ? groundSpinFactor : 1f);
            if (!Mathf.Approximately(Steer, 0f))
                Body.angularVelocity = Mathf.MoveTowards(Body.angularVelocity, Steer * spin, spinAcceleration * dt);
            else if (!IsTouchingGround)
                Body.angularVelocity = Mathf.MoveTowards(Body.angularVelocity, 0f, spinAcceleration * 0.15f * dt);

            float angle = Body.rotation;
            if (!IsTouchingGround) airRotation += Mathf.DeltaAngle(lastAngle, angle);
            lastAngle = angle;

            // --- ground movement ---
            if (IsTouchingGround)
            {
                Vector2 tangent = new Vector2(GroundNormal.y, -GroundNormal.x); // downhill-right direction
                float along = Vector2.Dot(v, tangent);

                // snow friction proportional to the normal force (steeper slope => less normal force)
                float normalFactor = Mathf.Clamp01(Vector2.Dot(GroundNormal, Vector2.up));
                float friction = (IsBraking ? brakeFriction : snowFriction) * normalFactor;
                if (Mathf.Abs(along) > 0.05f)
                    Body.AddForce(-Mathf.Sign(along) * friction * Body.mass * tangent);

                float maxSpeed = CurrentMaxSpeed;
                if (accelHeld && along < maxSpeed)
                    Body.AddForce(tangent * Stats.Acceleration * Body.mass * (slowTimer > 0 ? 0.4f : 1f));
                if (IsBoosting && along < maxSpeed)
                    Body.AddForce(tangent * Stats.Acceleration * 1.2f * Body.mass);

                // gently align board with slope (grip), unless the player is steering
                if (Mathf.Approximately(Steer, 0f))
                {
                    float target = Mathf.Atan2(GroundNormal.y, GroundNormal.x) * Mathf.Rad2Deg - 90f;
                    float delta = Mathf.DeltaAngle(Body.rotation, target);
                    Body.angularVelocity = Mathf.Lerp(Body.angularVelocity, delta * 6f * Stats.Grip, 0.2f);
                }
            }

            // jump
            if (jumpQueued)
            {
                jumpQueued = false;
                if (IsGrounded)
                {
                    Vector2 dir = ((Vector2)transform.up + Vector2.up).normalized;
                    Body.linearVelocity = new Vector2(v.x, Mathf.Max(v.y, 0f));
                    Body.AddForce(dir * Stats.JumpForce * Body.mass, ForceMode2D.Impulse);
                    lastGroundedTime = -10f;
                    AudioManager.PlaySfx(Sfx.Jump, 0.6f);
                    Jumped?.Invoke();
                }
            }

            // air drag (quadratic) keeps a natural terminal velocity
            v = Body.linearVelocity;
            Body.AddForce(-v * v.magnitude * airDrag * Body.mass);

            // hard cap
            float cap = CurrentMaxSpeed * 1.25f;
            if (v.magnitude > cap) Body.linearVelocity = v.normalized * cap;

            // effects
            if (snowSpray)
            {
                var em = snowSpray.emission;
                em.rateOverTime = IsTouchingGround ? Mathf.Lerp(0f, IsBraking ? 120f : 60f, Speed / 15f) : 0f;
            }
            AudioManager.Instance?.SetWind(Speed / CurrentMaxSpeed);
        }

        public float CurrentMaxSpeed => Stats.MaxSpeed * (IsBoosting ? 1.4f : 1f) * (slowTimer > 0f ? 0.6f : 1f);

        void TickTimers(float dt)
        {
            if (boostTimer > 0f) { boostTimer -= dt; if (boostTimer <= 0f && boostTrail) boostTrail.emitting = false; }
            if (magnetTimer > 0f) magnetTimer -= dt;
            if (slowTimer > 0f) slowTimer -= dt;
            if (respawnGraceTimer > 0f) respawnGraceTimer -= dt;
            if (invincibleTimer > 0f) invincibleTimer -= dt;
            if (shieldVisual) shieldVisual.SetActive(invincibleTimer > 0f);

            // blink while respawn-protected
            bool blink = respawnGraceTimer > 0f && Mathf.Repeat(Time.time * 10f, 1f) > 0.5f;
            if (topRenderer) topRenderer.enabled = !blink;
            if (bottomRenderer) bottomRenderer.enabled = !blink;
        }

        // ---------------- ground detection ----------------
        void OnCollisionEnter2D(Collision2D c)
        {
            if (!c.collider.GetComponent<Ground>()) return;
            bool wasAirborne = groundContacts == 0;
            groundContacts++;
            UpdateNormal(c);
            if (wasAirborne) OnLand();
        }

        void OnCollisionStay2D(Collision2D c)
        {
            if (c.collider.GetComponent<Ground>()) UpdateNormal(c);
        }

        void OnCollisionExit2D(Collision2D c)
        {
            if (!c.collider.GetComponent<Ground>()) return;
            groundContacts = Mathf.Max(0, groundContacts - 1);
            if (groundContacts == 0)
            {
                leftGroundTime = Time.time;
                lastGroundedTime = Time.time;
                airRotation = 0f;
                grabTime = 0f;
                lastAngle = Body.rotation;
            }
        }

        void UpdateNormal(Collision2D c)
        {
            if (c.contactCount == 0) return;
            Vector2 n = Vector2.zero;
            for (int i = 0; i < c.contactCount; i++) n += c.GetContact(i).normal;
            GroundNormal = n.normalized;
            lastGroundedTime = Time.time;
        }

        void OnLand()
        {
            if (spawnDrop) { spawnDrop = false; airRotation = 0f; grabTime = 0f; return; }
            float air = Time.time - leftGroundTime;
            if (air < 0.25f || IsCrashed || IsFrozen) return;

            int flips = Mathf.FloorToInt((Mathf.Abs(airRotation) + 60f) / 360f); // 60 deg tolerance
            bool clean = Vector2.Dot(transform.up, GroundNormal) > 0.55f;
            if (flips > 0 && airRotation < 0) flips = -flips; // negative = frontflip (clockwise)
            AudioManager.PlaySfx(Sfx.Land, Mathf.Clamp01(air));
            Landed?.Invoke(flips, air, grabTime, clean);
            airRotation = 0f;
            grabTime = 0f;
        }

        // ---------------- gameplay API ----------------
        public void Crash()
        {
            if (IsCrashed || IsFrozen) return;
            IsCrashed = true;
            if (crashEffect) crashEffect.Play();
            if (snowSpray) { var em = snowSpray.emission; em.rateOverTime = 0f; }
            AudioManager.PlaySfx(Sfx.Crash);
            AudioManager.Instance?.SetWind(0f);
            boostTimer = 0f;
            if (boostTrail) boostTrail.emitting = false;
            GameManager.Instance?.OnPlayerCrashed();
        }

        /// <summary>Bump into a small obstacle: lose speed instead of crashing.</summary>
        public void HitObstacle(float speedLoss)
        {
            Body.linearVelocity *= 1f - speedLoss;
            slowTimer = 1.2f;
            AudioManager.PlaySfx(Sfx.Hit);
            CameraFollow.Instance?.Shake(0.25f, 0.3f);
        }

        public void Freeze(bool frozen)
        {
            IsFrozen = frozen;
            Body.simulated = !frozen;
            if (frozen) AudioManager.Instance?.SetWind(0f);
        }

        public void Respawn(Vector3 position)
        {
            transform.position = position;
            Body.rotation = 0f;
            transform.rotation = Quaternion.identity;
            Body.linearVelocity = Vector2.right * 4f;
            Body.angularVelocity = 0f;
            Body.simulated = true;
            IsCrashed = false;
            IsFrozen = false;
            groundContacts = 0;
            airRotation = 0f;
            lastAngle = 0f;
            respawnGraceTimer = 2f;
            spawnDrop = true;
            leftGroundTime = Time.time;
        }

        public void ApplyPowerUp(PowerUpType type, float duration)
        {
            switch (type)
            {
                case PowerUpType.SpeedBoost:
                    boostTimer = duration;
                    if (boostTrail) boostTrail.emitting = true;
                    Body.AddForce((Vector2)transform.right * 8f * Body.mass, ForceMode2D.Impulse);
                    break;
                case PowerUpType.Invincibility:
                    invincibleTimer = duration;
                    break;
                case PowerUpType.Magnet:
                    magnetTimer = duration;
                    break;
            }
        }

        public void BoostPadKick(float force, float duration)
        {
            Vector2 dir = new Vector2(GroundNormal.y, -GroundNormal.x);
            Body.AddForce(dir * force * Body.mass, ForceMode2D.Impulse);
            boostTimer = Mathf.Max(boostTimer, duration);
            if (boostTrail) boostTrail.emitting = true;
        }

        public bool HasMagnet => magnetTimer > 0f;
    }
}
