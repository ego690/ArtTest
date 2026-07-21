using UnityEngine;

namespace TrainGuessPrototype
{
    public sealed class TrainActor : MonoBehaviour
    {
        [SerializeField] TextMesh[] destinationDisplays;
        [SerializeField] Renderer[] accentRenderers;

        MaterialPropertyBlock propertyBlock;

        public void Configure(int hour, int minute, string destination, Color accent)
        {
            string display = $"{hour:00}:{minute:00}   {destination}";
            foreach (TextMesh destinationDisplay in destinationDisplays)
            {
                if (destinationDisplay != null)
                    destinationDisplay.text = display;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            foreach (Renderer accentRenderer in accentRenderers)
            {
                if (accentRenderer == null)
                    continue;

                accentRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", accent);
                accentRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
