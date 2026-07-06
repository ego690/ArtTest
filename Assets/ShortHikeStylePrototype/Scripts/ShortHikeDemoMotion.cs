using UnityEngine;

namespace ShortHikeStylePrototype
{
    [DisallowMultipleComponent]
    public sealed class ShortHikeDemoMotion : MonoBehaviour
    {
        [SerializeField] Transform character;
        [SerializeField] Material waterMaterial;
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float bobSpeed = 1.35f;
        [SerializeField] float turnSpeed = 18f;

        Vector3 initialCharacterPosition;

        void Awake()
        {
            if (character != null)
                initialCharacterPosition = character.localPosition;
        }

        void Update()
        {
            float time = Time.time;

            if (character != null)
            {
                character.localPosition = initialCharacterPosition + Vector3.up * (Mathf.Sin(time * bobSpeed) * bobHeight);
                character.Rotate(Vector3.up, turnSpeed * Time.deltaTime, Space.World);
            }

            if (waterMaterial != null)
                waterMaterial.SetFloat("_TimeOffset", time);
        }
    }
}
