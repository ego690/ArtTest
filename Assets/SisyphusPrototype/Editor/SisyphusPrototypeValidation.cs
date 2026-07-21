using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SisyphusPrototype.Editor
{
    public static class SisyphusPrototypeValidation
    {
        const string ScenePath = "Assets/SisyphusPrototype/Scenes/SisyphusPrototype.unity";

        public static void CaptureBatchPreview()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            SisyphusSpiralRamp ramp = UnityEngine.Object.FindFirstObjectByType<SisyphusSpiralRamp>();
            SisyphusVisionController vision = UnityEngine.Object.FindFirstObjectByType<SisyphusVisionController>();
            SisyphusRoundDirector director = UnityEngine.Object.FindFirstObjectByType<SisyphusRoundDirector>();
            SisyphusPlayerController player = UnityEngine.Object.FindFirstObjectByType<SisyphusPlayerController>();
            Rigidbody boulder = GameObject.Find("Boulder")?.GetComponent<Rigidbody>();

            if (camera == null || ramp == null || vision == null || director == null || player == null || boulder == null)
                throw new InvalidOperationException("The prototype scene is missing one or more required gameplay components.");

            ramp.Rebuild();
            Mesh visualMesh = ramp.GetComponent<MeshFilter>().sharedMesh;
            Mesh collisionMesh = ramp.GetComponent<MeshCollider>().sharedMesh;
            if (visualMesh == null || visualMesh.vertexCount < 1000 || collisionMesh == null)
                throw new InvalidOperationException("The spiral ramp did not produce valid render and collision meshes.");

            string outputDirectory = Path.Combine(Application.dataPath, "SisyphusPrototype", "Screenshots", "BatchValidation");
            Directory.CreateDirectory(outputDirectory);
            float[] luminance = new float[5];
            for (int stage = 1; stage <= 5; stage++)
            {
                vision.SetStage(stage);
                string outputPath = Path.Combine(outputDirectory, "Sisyphus_Stage" + stage + ".png");
                luminance[stage - 1] = RenderCamera(camera, outputPath);
            }

            if (luminance[0] < 0.04f)
                throw new InvalidOperationException("Stage 1 rendered too dark to read the world.");
            if (luminance[4] > 0.01f)
                throw new InvalidOperationException("Stage 5 did not render as full black.");

            Debug.LogFormat(
                "Sisyphus validation passed. Ramp vertices: {0}, path length: {1:F1}m, luminance: [{2:F3}, {3:F3}, {4:F3}, {5:F3}, {6:F3}]",
                visualMesh.vertexCount,
                ramp.ApproximatePathLength,
                luminance[0],
                luminance[1],
                luminance[2],
                luminance[3],
                luminance[4]);
        }

        static float RenderCamera(Camera camera, string path)
        {
            const int width = 1280;
            const int height = 720;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return CalculateAverageLuminance(texture.GetPixels32());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static float CalculateAverageLuminance(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
                return 0f;

            double sum = 0.0;
            for (int i = 0; i < pixels.Length; i += 8)
            {
                Color32 pixel = pixels[i];
                sum += (pixel.r * 0.2126 + pixel.g * 0.7152 + pixel.b * 0.0722) / 255.0;
            }
            return (float)(sum / Math.Ceiling(pixels.Length / 8.0));
        }
    }
}
