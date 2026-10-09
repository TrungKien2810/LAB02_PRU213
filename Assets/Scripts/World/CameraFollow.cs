using UnityEngine;

namespace SnowBoarder
{
    /// <summary>
    /// Smooth follow camera with velocity look-ahead, speed-based zoom and screen shake.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        [SerializeField] PlayerController target;
        [SerializeField] Vector2 offset = new Vector2(5f, 2f);
        [SerializeField] float lookAhead = 0.35f;
        [SerializeField] float smoothTime = 0.25f;
        [SerializeField] float minSize = 9f;
        [SerializeField] float maxSize = 13f;

        Camera cam;
        Vector3 velocity;
        Vector3 shakeOffset;
        float shakeTime, shakeDuration, shakeStrength;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (target) transform.position = Desired();
        }

        Vector3 Desired()
        {
            Vector2 v = target.Body ? target.Body.linearVelocity : Vector2.zero;
            Vector2 ahead = Vector2.ClampMagnitude(v * lookAhead, 8f);
            Vector3 p = target.transform.position + (Vector3)(offset + ahead);
            p.z = -10f;
            return p;
        }

        void LateUpdate()
        {
            if (!target) return;
            Vector3 pos = Vector3.SmoothDamp(transform.position - shakeOffset, Desired(), ref velocity, smoothTime);

            float speed01 = Mathf.InverseLerp(5f, 25f, target.Speed);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, Mathf.Lerp(minSize, maxSize, speed01), Time.deltaTime * 1.5f);

            shakeOffset = Vector3.zero;
            if (shakeTime > 0f && GameSettings.ScreenShake)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float k = shakeTime / shakeDuration;
                shakeOffset = (Vector3)Random.insideUnitCircle * shakeStrength * k;
            }
            transform.position = pos + shakeOffset;
        }

        public void Shake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(strength, shakeTime > 0 ? shakeStrength : 0f);
            shakeDuration = duration;
            shakeTime = duration;
        }
    }
}
