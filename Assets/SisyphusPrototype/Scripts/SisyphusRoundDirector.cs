using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SisyphusPrototype
{
    [DisallowMultipleComponent]
    public sealed class SisyphusRoundDirector : MonoBehaviour
    {
        [SerializeField] Rigidbody boulder;
        [SerializeField] SisyphusPlayerController playerController;
        [SerializeField] SisyphusCameraRig cameraRig;
        [SerializeField] MonoBehaviour visionController;
        [SerializeField, Range(1, 5)] int currentRound = 1;
        [SerializeField] Vector3 boulderStartPosition;
        [SerializeField] Quaternion boulderStartRotation = Quaternion.identity;
        [SerializeField] Vector3 playerStartPosition;
        [SerializeField] Quaternion playerStartRotation = Quaternion.identity;
        [SerializeField] Vector3 summitOutward = Vector3.right;
        [SerializeField, Min(0f)] float summitHoldSeconds = 0.75f;
        [SerializeField, Min(0f)] float fallSeconds = 2.35f;
        [SerializeField, Min(0f)] float launchSpeed = 14f;
        [SerializeField] float resetBelowHeight = -20f;

        Coroutine summitSequence;
        bool hasSucceeded;

        public int CurrentRound => currentRound;

        public void Configure(
            Rigidbody targetBoulder,
            SisyphusPlayerController targetPlayer,
            SisyphusCameraRig targetCamera,
            MonoBehaviour targetVision,
            Vector3 boulderStart,
            Quaternion boulderRotation,
            Vector3 playerStart,
            Quaternion playerRotation,
            Vector3 outward)
        {
            boulder = targetBoulder;
            playerController = targetPlayer;
            cameraRig = targetCamera;
            visionController = targetVision;
            boulderStartPosition = boulderStart;
            boulderStartRotation = boulderRotation;
            playerStartPosition = playerStart;
            playerStartRotation = playerRotation;
            summitOutward = outward.normalized;
        }

        public bool IsTargetBoulder(Rigidbody body)
        {
            return body != null && body == boulder;
        }

        public void ReachSummit()
        {
            if (summitSequence == null && !hasSucceeded)
                summitSequence = StartCoroutine(PlaySummitSequence());
        }

        public void SetRound(int round)
        {
            currentRound = Mathf.Clamp(round, 1, 5);
            hasSucceeded = false;
            ApplyRound(currentRound);
        }

        public void ResetAttempt()
        {
            if (summitSequence != null)
            {
                StopCoroutine(summitSequence);
                summitSequence = null;
            }

            hasSucceeded = false;
            ResetActors();
            ApplyRound(currentRound);
        }

        void Start()
        {
            ResetActors();
            ApplyRound(currentRound);
        }

        void Update()
        {
            if (summitSequence == null &&
                ((boulder != null && boulder.position.y < resetBelowHeight) ||
                 (playerController != null && playerController.transform.position.y < resetBelowHeight)))
            {
                ResetAttempt();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || summitSequence != null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                SetRound(1);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                SetRound(2);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                SetRound(3);
            else if (keyboard.digit4Key.wasPressedThisFrame)
                SetRound(4);
            else if (keyboard.digit5Key.wasPressedThisFrame)
                SetRound(5);

            if (keyboard.rKey.wasPressedThisFrame)
                ResetAttempt();
        }

        IEnumerator PlaySummitSequence()
        {
            playerController.enabled = false;
            boulder.isKinematic = true;
            boulder.linearVelocity = Vector3.zero;
            boulder.angularVelocity = Vector3.zero;
            yield return new WaitForSeconds(summitHoldSeconds);

            if (currentRound >= 5)
            {
                hasSucceeded = true;
                cameraRig.SetRound(1);
                visionController?.SendMessage("RevealWorld", SendMessageOptions.DontRequireReceiver);
                yield return new WaitForSeconds(2.5f);
                playerController.enabled = true;
                summitSequence = null;
                yield break;
            }

            boulder.isKinematic = false;
            boulder.detectCollisions = false;
            Vector3 launchDirection = (summitOutward + Vector3.up * 0.32f).normalized;
            boulder.linearVelocity = launchDirection * launchSpeed;
            boulder.angularVelocity = Vector3.Cross(Vector3.up, summitOutward) * 5f;
            yield return new WaitForSeconds(0.28f);
            boulder.detectCollisions = true;
            yield return new WaitForSeconds(Mathf.Max(0f, fallSeconds - 0.28f));

            currentRound = Mathf.Min(5, currentRound + 1);
            ResetActors();
            ApplyRound(currentRound);
            playerController.enabled = true;
            summitSequence = null;
        }

        void ResetActors()
        {
            if (boulder != null)
            {
                boulder.detectCollisions = true;
                boulder.isKinematic = true;
                boulder.transform.SetPositionAndRotation(boulderStartPosition, boulderStartRotation);
                boulder.isKinematic = false;
                boulder.linearVelocity = Vector3.zero;
                boulder.angularVelocity = Vector3.zero;
                boulder.WakeUp();
            }

            if (playerController != null)
            {
                playerController.enabled = true;
                playerController.Teleport(playerStartPosition, playerStartRotation);
            }
        }

        void ApplyRound(int round)
        {
            cameraRig?.SetRound(round);
            visionController?.SendMessage("SetRound", round, SendMessageOptions.DontRequireReceiver);
        }
    }
}
