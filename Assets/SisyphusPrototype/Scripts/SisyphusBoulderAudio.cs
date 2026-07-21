using UnityEngine;

namespace SisyphusPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
    public sealed class SisyphusBoulderAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.35f)] float maximumVolume = 0.16f;
        [SerializeField] float baseFrequency = 43f;
        [SerializeField] float noiseAmount = 0.38f;

        Rigidbody body;
        AudioSource source;
        System.Random noise;
        double phase;
        float targetGain;
        int sampleRate = 48000;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 1f;
            source.minDistance = 2f;
            source.maxDistance = 45f;
            sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            noise = new System.Random(9127);
            if (!source.isPlaying)
                source.Play();
        }

        void Update()
        {
            float speed = body != null
                ? body.linearVelocity.magnitude + body.angularVelocity.magnitude * 0.22f
                : 0f;
            targetGain = Mathf.Clamp01(speed / 5f) * maximumVolume;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = targetGain;
            float frequency = baseFrequency + gain * 90f;
            double phaseStep = frequency / sampleRate;

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                float lowTone = Mathf.Sin((float)(phase * Mathf.PI * 2.0));
                float roughness = (float)(noise.NextDouble() * 2.0 - 1.0);
                float sample = (lowTone * (1f - noiseAmount) + roughness * noiseAmount) * gain;
                for (int channel = 0; channel < channels; channel++)
                    data[frame + channel] = sample;

                phase += phaseStep;
                if (phase >= 1.0)
                    phase -= 1.0;
            }
        }
    }
}
