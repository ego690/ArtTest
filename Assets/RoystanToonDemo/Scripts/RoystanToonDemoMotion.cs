using UnityEngine;

namespace RoystanToonDemo
{
    [DisallowMultipleComponent]
    public sealed class RoystanToonDemoMotion : MonoBehaviour
    {
        [SerializeField] Transform rotatingGroup;
        [SerializeField] Light sun;
        [SerializeField] float objectSpinSpeed = 12f;
        [SerializeField] float lightYawSpeed = 8f;

        void Update()
        {
            float deltaTime = Time.deltaTime;

            if (rotatingGroup != null)
                rotatingGroup.Rotate(Vector3.up, objectSpinSpeed * deltaTime, Space.World);

            if (sun != null)
                sun.transform.Rotate(Vector3.up, lightYawSpeed * deltaTime, Space.World);
        }
    }
}
