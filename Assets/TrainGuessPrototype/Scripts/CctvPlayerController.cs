using UnityEngine;
using UnityEngine.InputSystem;

namespace TrainGuessPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CctvPlayerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 4.2f;
        [SerializeField] float turnSpeed = 12f;
        [SerializeField] float gravity = 24f;

        CharacterController characterController;
        Camera movementCamera;
        float verticalSpeed;

        public bool CanMove { get; set; }

        void Awake()
        {
            characterController = GetComponent<CharacterController>();
            movementCamera = Camera.main;
        }

        void Update()
        {
            if (!characterController.enabled)
                return;

            Vector2 input = CanMove ? ReadMoveInput() : Vector2.zero;
            Vector3 move = CameraRelativeMove(input);

            if (characterController.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            else
                verticalSpeed -= gravity * Time.deltaTime;

            characterController.Move((move * moveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);

            if (move.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }

        public void SetMovementCamera(Camera targetCamera)
        {
            movementCamera = targetCamera;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            characterController.enabled = wasEnabled;
            verticalSpeed = -2f;
        }

        static Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    input.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    input.x += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    input.y -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    input.y += 1f;
            }

            if (Gamepad.current != null)
                input += Gamepad.current.leftStick.ReadValue();

            return Vector2.ClampMagnitude(input, 1f);
        }

        Vector3 CameraRelativeMove(Vector2 input)
        {
            if (input.sqrMagnitude < 0.001f)
                return Vector3.zero;

            Transform cameraTransform = movementCamera != null ? movementCamera.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            if (right.sqrMagnitude < 0.01f)
                right = Vector3.right;

            return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
        }
    }
}
