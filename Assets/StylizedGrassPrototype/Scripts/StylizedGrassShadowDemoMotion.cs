using UnityEngine;

namespace StylizedGrassPrototype
{
    public sealed class StylizedGrassShadowDemoMotion : MonoBehaviour
    {
        public Vector3 orbitCenter = new Vector3(0f, 1.15f, 0f);
        public Vector2 orbitRadius = new Vector2(2.2f, 1.45f);
        [Range(0f, 3f)] public float orbitSpeed = 0.45f;
        [Range(0f, 1f)] public float bobHeight = 0.2f;
        [Range(0f, 5f)] public float spinSpeed = 1.4f;

        void Update()
        {
            float phase = Time.time * orbitSpeed;
            transform.position = orbitCenter + new Vector3(
                Mathf.Cos(phase) * orbitRadius.x,
                Mathf.Sin(phase * 1.7f) * bobHeight,
                Mathf.Sin(phase) * orbitRadius.y);
            transform.Rotate(Vector3.up, spinSpeed * 60f * Time.deltaTime, Space.World);
        }
    }
}
