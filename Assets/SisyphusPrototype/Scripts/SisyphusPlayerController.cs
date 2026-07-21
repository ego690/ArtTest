using UnityEngine;
using UnityEngine.InputSystem;

namespace SisyphusPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class SisyphusPlayerController : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] Rigidbody boulder;
        [SerializeField, Min(0f)] float moveSpeed = 7.2f;
        [SerializeField, Min(0f)] float pushMoveSpeed = 4.6f;
        [SerializeField, Min(0f)] float turnSpeed = 11f;
        [SerializeField, Min(0f)] float gravity = 24f;
        [SerializeField, Min(0f)] float pushAcceleration = 42f;
        [SerializeField, Min(0f)] float sustainedPushAcceleration = 18f;
        [SerializeField, Min(0f)] float maxBoulderSpeed = 7.5f;
        [SerializeField, Min(0f)] float pushingDistance = 4.35f;

        CharacterController controller;
        float verticalSpeed;
        Vector3 planarVelocity;

        public float MovementAmount { get; private set; }
        public bool IsPushing { get; private set; }

        public void Configure(Camera camera, Rigidbody targetBoulder)
        {
            viewCamera = camera;
            boulder = targetBoulder;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();

            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = wasEnabled;
            verticalSpeed = 0f;
            planarVelocity = Vector3.zero;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            Vector2 input = ReadMovement();
            Vector3 desiredDirection = CameraRelativeDirection(input);
            MovementAmount = Mathf.Clamp01(input.magnitude);
            IsPushing = boulder != null &&
                Vector3.SqrMagnitude(transform.position - boulder.position) <= pushingDistance * pushingDistance &&
                MovementAmount > 0.05f;

            float speed = IsPushing ? pushMoveSpeed : moveSpeed;
            planarVelocity = desiredDirection * (speed * MovementAmount);

            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            else
                verticalSpeed -= gravity * Time.deltaTime;

            controller.Move((planarVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);

            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody hitBody = hit.rigidbody;
            if (hitBody == null || hitBody != boulder || hitBody.isKinematic || MovementAmount <= 0.05f)
                return;

            Vector3 pushDirection = Vector3.ProjectOnPlane(hit.moveDirection, Vector3.up).normalized;
            Vector3 towardBoulder = Vector3.ProjectOnPlane(boulder.worldCenterOfMass - transform.position, Vector3.up).normalized;
            if (pushDirection.sqrMagnitude < 0.001f || Vector3.Dot(pushDirection, towardBoulder) < 0.25f)
                return;

            float alongPushSpeed = Vector3.Dot(hitBody.linearVelocity, pushDirection);
            if (alongPushSpeed < maxBoulderSpeed)
                hitBody.AddForceAtPosition(pushDirection * pushAcceleration, hit.point, ForceMode.Acceleration);
        }

        void FixedUpdate()
        {
            if (!IsPushing || boulder == null || boulder.isKinematic || planarVelocity.sqrMagnitude < 0.01f)
                return;

            Vector3 towardBoulder = Vector3.ProjectOnPlane(
                boulder.worldCenterOfMass - transform.position,
                Vector3.up).normalized;
            Vector3 requestedDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up).normalized;
            if (towardBoulder.sqrMagnitude < 0.001f || Vector3.Dot(requestedDirection, towardBoulder) < 0.35f)
                return;

            Vector3 groundNormal = Vector3.up;
            if (Physics.Raycast(
                    boulder.worldCenterOfMass + Vector3.up,
                    Vector3.down,
                    out RaycastHit groundHit,
                    8f,
                    ~(1 << 2),
                    QueryTriggerInteraction.Ignore))
            {
                groundNormal = groundHit.normal;
            }

            Vector3 pushDirection = Vector3.ProjectOnPlane(requestedDirection, groundNormal).normalized;
            float alongPushSpeed = Vector3.Dot(boulder.linearVelocity, pushDirection);
            if (pushDirection.sqrMagnitude > 0.001f && alongPushSpeed < maxBoulderSpeed)
                boulder.AddForce(pushDirection * sustainedPushAcceleration, ForceMode.Acceleration);
        }

        static Vector2 ReadMovement()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.y += 1f;
            return Vector2.ClampMagnitude(input, 1f);
        }

        Vector3 CameraRelativeDirection(Vector2 input)
        {
            Transform cameraTransform = viewCamera != null ? viewCamera.transform : null;
            Vector3 forward = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 right = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
                : Vector3.right;
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }
    }
}
