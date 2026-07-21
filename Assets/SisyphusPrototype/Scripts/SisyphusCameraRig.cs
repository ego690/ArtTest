using UnityEngine;
using UnityEngine.InputSystem;

namespace SisyphusPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class SisyphusCameraRig : MonoBehaviour
    {
        static readonly float[] RoundDistances = { 24f, 18f, 12f, 7.2f, 6.6f };
        static readonly float[] RoundFovs = { 48f, 46f, 43f, 49f, 49f };
        static readonly float[] RoundFocusWeights = { 0.52f, 0.60f, 0.72f, 0.88f, 0.92f };
        static readonly float[] RoundPitches = { 31f, 28f, 24f, 18f, 18f };

        [SerializeField] Transform player;
        [SerializeField] Transform boulder;
        [SerializeField] Vector3 focusOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] float yaw = 45f;
        [SerializeField, Min(0f)] float orbitSpeed = 0.14f;
        [SerializeField, Min(0f)] float followSharpness = 7f;
        [SerializeField, Min(0f)] float rotationSharpness = 9f;
        [SerializeField, Min(0f)] float collisionRadius = 0.55f;
        [SerializeField] LayerMask obstructionMask = ~(1 << 2);

        Camera targetCamera;
        Vector3 smoothedFocus;
        Vector3 smoothedPosition;
        float desiredDistance = RoundDistances[0];
        float desiredFov = RoundFovs[0];
        float desiredPitch = RoundPitches[0];
        float desiredFocusWeight = RoundFocusWeights[0];
        bool initialized;

        public void Configure(Transform playerTarget, Transform boulderTarget)
        {
            player = playerTarget;
            boulder = boulderTarget;
        }

        public void SetRound(int round)
        {
            int index = Mathf.Clamp(round - 1, 0, RoundDistances.Length - 1);
            desiredDistance = RoundDistances[index];
            desiredFov = RoundFovs[index];
            desiredPitch = RoundPitches[index];
            desiredFocusWeight = RoundFocusWeights[index];
        }

        void Awake()
        {
            targetCamera = GetComponent<Camera>();
            SetRound(1);
        }

        void LateUpdate()
        {
            if (player == null && boulder == null)
                return;

            ReadOrbitInput();
            Vector3 playerPosition = player != null ? player.position : boulder.position;
            Vector3 boulderPosition = boulder != null ? boulder.position : playerPosition;
            Vector3 targetFocus = Vector3.Lerp(playerPosition, boulderPosition, desiredFocusWeight) + focusOffset;

            float positionT = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            if (!initialized)
            {
                smoothedFocus = targetFocus;
                smoothedPosition = transform.position;
                initialized = true;
            }
            smoothedFocus = Vector3.Lerp(smoothedFocus, targetFocus, positionT);

            Quaternion orbit = Quaternion.Euler(desiredPitch, yaw, 0f);
            Vector3 backwards = orbit * Vector3.back;
            float cameraDistance = ResolveCameraDistance(smoothedFocus, backwards, desiredDistance);
            Vector3 targetPosition = smoothedFocus + backwards * cameraDistance;
            smoothedPosition = Vector3.Lerp(smoothedPosition, targetPosition, positionT);

            Quaternion targetRotation = Quaternion.LookRotation(smoothedFocus - smoothedPosition, Vector3.up);
            float rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.position = smoothedPosition;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationT);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, desiredFov, positionT);
        }

        void ReadOrbitInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
                yaw += mouse.delta.ReadValue().x * orbitSpeed;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float keyboardOrbit = 0f;
                if (keyboard.qKey.isPressed)
                    keyboardOrbit -= 1f;
                if (keyboard.eKey.isPressed)
                    keyboardOrbit += 1f;
                yaw += keyboardOrbit * 65f * Time.deltaTime;
            }
        }

        float ResolveCameraDistance(Vector3 focus, Vector3 direction, float requestedDistance)
        {
            if (Physics.SphereCast(
                    focus,
                    collisionRadius,
                    direction,
                    out RaycastHit hit,
                    requestedDistance,
                    obstructionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(1.2f, hit.distance - collisionRadius);
            }

            return requestedDistance;
        }
    }
}
