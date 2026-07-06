using UnityEngine;
using UnityEngine.InputSystem;

namespace ShortHikeStylePrototype
{
    [DisallowMultipleComponent]
    public sealed class ShortHikeOrbitCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 focusOffset = new Vector3(0f, 0.8f, 0f);
        [SerializeField] float distance = 13.5f;
        [SerializeField] float yaw = 38f;
        [SerializeField] float pitch = 37f;
        [SerializeField] float orbitSpeed = 74f;
        [SerializeField] float zoomSpeed = 5f;
        [SerializeField] Vector2 distanceRange = new Vector2(8f, 20f);

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        void LateUpdate()
        {
            Vector2 look = Vector2.zero;
            float scroll = 0f;

            if (Mouse.current != null)
            {
                if (Mouse.current.rightButton.isPressed)
                    look = Mouse.current.delta.ReadValue();

                scroll = Mouse.current.scroll.ReadValue().y;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.isPressed)
                    look.x -= orbitSpeed * Time.deltaTime;
                if (Keyboard.current.eKey.isPressed)
                    look.x += orbitSpeed * Time.deltaTime;
            }

            yaw += look.x * Time.deltaTime * orbitSpeed * 0.035f;
            pitch = Mathf.Clamp(pitch - look.y * Time.deltaTime * orbitSpeed * 0.025f, 18f, 68f);
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * Time.deltaTime, distanceRange.x, distanceRange.y);

            Vector3 focus = target != null ? target.position + focusOffset : focusOffset;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = focus - rotation * Vector3.forward * distance;
            transform.rotation = rotation;
        }
    }
}
