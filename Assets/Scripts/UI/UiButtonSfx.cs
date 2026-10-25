using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SnowBoarder
{
    /// <summary>
    /// Gives a UI control an audible hover cue, so the pointer gets the same immediate
    /// feedback that the click sound already provides.
    /// Attached to every Selectable on a screen by <see cref="AttachAll"/>.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UiButtonSfx : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        /// <summary>
        /// One gesture fires both OnPointerEnter and OnSelect (highlight, then selection on
        /// press). A short shared gap collapses that pair into a single tick while still
        /// letting each newly entered control speak when the pointer sweeps a row.
        /// </summary>
        const float MinGap = 0.06f;

        const float HoverVolume = 0.6f; // a touch under the click, so the click still lands

        static float lastTickTime = -1f;

        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData eventData) => Tick();

        /// <summary>Keyboard / gamepad navigation moves the selection: same cue.</summary>
        public void OnSelect(BaseEventData eventData) => Tick();

        void Tick()
        {
            if (selectable && !selectable.IsInteractable()) return; // locked mountains stay silent

            float now = Time.unscaledTime; // menus can be open while the game is paused
            if (now - lastTickTime < MinGap) return;
            lastTickTime = now;

            AudioManager.PlaySfx(Sfx.Hover, HoverVolume);
        }

        /// <summary>
        /// Adds the hover cue to every <see cref="Selectable"/> under <paramref name="root"/>,
        /// inactive panels included. Called by the UI controllers so scenes that were generated
        /// before this component existed still get hover audio without being rebuilt.
        /// </summary>
        public static void AttachAll(Transform root)
        {
            if (!root) return;
            foreach (var s in root.GetComponentsInChildren<Selectable>(true))
                if (!s.GetComponent<UiButtonSfx>()) s.gameObject.AddComponent<UiButtonSfx>();
        }
    }
}
