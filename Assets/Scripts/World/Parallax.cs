using UnityEngine;

namespace SnowBoarder
{
    /// <summary>
    /// Moves a background layer with a fraction of the camera movement.
    /// factor 1 = stuck to camera (infinitely far), 0 = static world object.
    /// Optionally repeats horizontally by wrapping its tiles.
    /// </summary>
    public class Parallax : MonoBehaviour
    {
        [SerializeField] Vector2 factor = new Vector2(0.9f, 0.9f);
        [SerializeField] float tileWidth = 0f; // > 0 enables horizontal wrapping of children

        Transform cam;
        Vector3 startPos, camStart;

        void Start()
        {
            cam = Camera.main.transform;
            startPos = transform.position;
            camStart = cam.position;
        }

        void LateUpdate()
        {
            Vector3 d = cam.position - camStart;
            transform.position = startPos + new Vector3(d.x * factor.x, d.y * factor.y, 0f);

            if (tileWidth <= 0f) return;
            foreach (Transform child in transform)
            {
                float dx = child.position.x - cam.position.x;
                int count = transform.childCount;
                if (dx < -tileWidth * count * 0.5f) child.position += Vector3.right * tileWidth * count;
                else if (dx > tileWidth * count * 0.5f) child.position -= Vector3.right * tileWidth * count;
            }
        }
    }
}
