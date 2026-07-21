using UnityEngine;

namespace SisyphusPrototype
{
    [DisallowMultipleComponent]
    public sealed class SisyphusVisibilityAnchor : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] SisyphusSpiralRamp ramp;

        public void Configure(Transform followTarget, SisyphusSpiralRamp targetRamp)
        {
            target = followTarget;
            ramp = targetRamp;
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            transform.position = target.position;
            if (ramp == null)
                return;

            float baseHeight = ramp.transform.position.y;
            float normalizedHeight = Mathf.InverseLerp(baseHeight, baseHeight + ramp.rise, target.position.y);
            ramp.EvaluateFrame(normalizedHeight, out _, out Vector3 tangent, out Vector3 normal, out _);
            transform.rotation = Quaternion.LookRotation(tangent, normal);
        }
    }
}
