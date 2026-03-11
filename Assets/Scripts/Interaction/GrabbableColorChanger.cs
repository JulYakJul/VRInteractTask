using UnityEngine;

namespace VRInteract.Interaction {
    /// <summary>
    /// Изменение цвета объекта при захвате и возвращаение исходного цвета при отпускании.
    /// Подключается к событиям Grabbable (On Grab / On Release).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class GrabbableColorChanger : MonoBehaviour {
        [SerializeField]
        private Color highlightColor = Color.green;

        private Renderer _renderer;
        private Color _originalColor;
        private bool _initialized;

        private void Awake() {
            _renderer = GetComponent<Renderer>();

            // Сохранение исходного цвета материала
            if (_renderer != null && _renderer.material != null) {
                _originalColor = _renderer.material.color;
                _initialized = true;
            }
        }

        /// <summary>
        /// Хватание. Событие On Grab компонента Grabbable.
        /// </summary>
        public void OnGrab() {
            if (!_initialized) {
                return;
            }

            _renderer.material.color = highlightColor;
        }

        /// <summary>
        /// Отпускание. событие On Release компонента Grabbable.
        /// </summary>
        public void OnRelease() {
            if (!_initialized) {
                return;
            }

            _renderer.material.color = _originalColor;
        }
    }
}

