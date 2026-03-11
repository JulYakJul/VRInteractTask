using BNG;
using UnityEngine;

namespace VRInteract.Interaction {
    /// <summary>
    /// Хватание/отпускание по нажатию на кнопку джойстика.
    /// </summary>
    public class ThumbstickGrabToggle : MonoBehaviour {
        [SerializeField]
        private Grabber leftGrabber;

        [SerializeField]
        private Grabber rightGrabber;

        private void Awake() {
            if (leftGrabber == null || rightGrabber == null) {
                var grabbers = GetComponentsInChildren<Grabber>(true);
                foreach (var g in grabbers) {
                    if (g.HandSide == ControllerHand.Left && leftGrabber == null) {
                        leftGrabber = g;
                    }
                    else if (g.HandSide == ControllerHand.Right && rightGrabber == null) {
                        rightGrabber = g;
                    }
                }
            }
        }

        private void Update() {
            var input = InputBridge.Instance;
            if (input == null) {
                return;
            }

            if (input.LeftThumbstickDown && leftGrabber != null) {
                Toggle(leftGrabber);
            }

            if (input.RightThumbstickDown && rightGrabber != null) {
                Toggle(rightGrabber);
            }
        }

        private static void Toggle(Grabber grabber) {
            if (grabber.HoldingItem || grabber.RemoteGrabbingItem) {
                grabber.TryRelease();
            }
            else {
                grabber.TryGrab();
            }
        }
    }
}

