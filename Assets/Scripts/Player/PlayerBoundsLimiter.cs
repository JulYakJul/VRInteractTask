using UnityEngine;

namespace VRInteract.Player {
    /// <summary>
    /// Ограничение позиции объекта по плоскости XZ.
    /// </summary>
    public class PlayerBoundsLimiter : MonoBehaviour {
        [Header("Границы перемещения по X и Z")]
        [SerializeField]
        private Vector2 minXZ = new Vector2(-2f, -2f);

        [SerializeField]
        private Vector2 maxXZ = new Vector2(2f, 2f);

        [Header("Ограничивать только глобальные координаты")]
        [SerializeField]
        private bool useLocalSpace = false;

        private void LateUpdate() {
            // LateUpdate чтобы учесть все перемещения за кадр
            if (useLocalSpace) {
                var localPos = transform.localPosition;
                localPos.x = Mathf.Clamp(localPos.x, minXZ.x, maxXZ.x);
                localPos.z = Mathf.Clamp(localPos.z, minXZ.y, maxXZ.y);
                transform.localPosition = localPos;
            } else {
                var worldPos = transform.position;
                worldPos.x = Mathf.Clamp(worldPos.x, minXZ.x, maxXZ.x);
                worldPos.z = Mathf.Clamp(worldPos.z, minXZ.y, maxXZ.y);
                transform.position = worldPos;
            }
        }

        public void SetBounds(Vector2 min, Vector2 max) {
            minXZ = min;
            maxXZ = max;
        }
    }
}

