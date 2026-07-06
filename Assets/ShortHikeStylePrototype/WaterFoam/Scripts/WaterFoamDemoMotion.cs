using UnityEngine;

namespace ShortHikeStylePrototype.WaterFoam
{
    [DisallowMultipleComponent]
    public sealed class WaterFoamDemoMotion : MonoBehaviour
    {
        [SerializeField] Transform orbitRoot;
        [SerializeField] Transform bobbingObject;
        [SerializeField] float orbitDegreesPerSecond = 6f;
        [SerializeField] float bobHeight = 0.18f;
        [SerializeField] float bobSpeed = 1.35f;

        Vector3 bobStartPosition;

        void Awake()
        {
            if (bobbingObject != null)
                bobStartPosition = bobbingObject.position;
        }

        void Update()
        {
            float time = Time.time;

            if (orbitRoot != null)
                orbitRoot.Rotate(Vector3.up, orbitDegreesPerSecond * Time.deltaTime, Space.World);

            if (bobbingObject != null)
            {
                Vector3 offset = Vector3.up * (Mathf.Sin(time * bobSpeed) * bobHeight);
                bobbingObject.position = bobStartPosition + offset;
            }
        }
    }
}
