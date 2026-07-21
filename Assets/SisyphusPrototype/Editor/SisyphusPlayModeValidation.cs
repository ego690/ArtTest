using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SisyphusPrototype.Editor
{
    [InitializeOnLoad]
    public static class SisyphusPlayModeValidation
    {
        const string ScenePath = "Assets/SisyphusPrototype/Scenes/SisyphusPrototype.unity";
        const string RunningKey = "Sisyphus.Validation.Running";
        const string CompleteKey = "Sisyphus.Validation.Complete";
        const string ExitCodeKey = "Sisyphus.Validation.ExitCode";
        const string MessageKey = "Sisyphus.Validation.Message";

        static Camera targetCamera;
        static SisyphusVisionController vision;
        static SisyphusRoundDirector director;
        static SisyphusSpiralRamp ramp;
        static Rigidbody boulder;
        static RenderTexture targetTexture;
        static readonly float[] Luminance = new float[5];
        static int stage;
        static int framesRemaining;
        static bool initialized;
        static double startTime;

        static SisyphusPlayModeValidation()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        public static void StartCapture()
        {
            SessionState.SetBool(RunningKey, true);
            SessionState.SetBool(CompleteKey, false);
            SessionState.SetInt(ExitCodeKey, 1);
            SessionState.SetString(MessageKey, "Validation did not finish.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(RunningKey, false))
                return;

            if (SessionState.GetBool(CompleteKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                int exitCode = SessionState.GetInt(ExitCodeKey, 1);
                string message = SessionState.GetString(MessageKey, "Validation finished without a report.");
                SessionState.EraseBool(RunningKey);
                SessionState.EraseBool(CompleteKey);
                Debug.Log(message);
                EditorApplication.Exit(exitCode);
                return;
            }

            if (!EditorApplication.isPlaying || initialized)
                return;

            try
            {
                SetupCapture();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        static void SetupCapture()
        {
            targetCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            vision = UnityEngine.Object.FindFirstObjectByType<SisyphusVisionController>();
            ramp = UnityEngine.Object.FindFirstObjectByType<SisyphusSpiralRamp>();
            director = UnityEngine.Object.FindFirstObjectByType<SisyphusRoundDirector>();
            SisyphusPlayerController player = UnityEngine.Object.FindFirstObjectByType<SisyphusPlayerController>();
            boulder = GameObject.Find("Boulder")?.GetComponent<Rigidbody>();
            if (targetCamera == null || vision == null || ramp == null || director == null || player == null || boulder == null)
                throw new InvalidOperationException("The play-mode scene is missing required prototype components.");

            Mesh visualMesh = ramp.GetComponent<MeshFilter>().sharedMesh;
            Mesh collisionMesh = ramp.GetComponent<MeshCollider>().sharedMesh;
            if (visualMesh == null || visualMesh.vertexCount < 1000 || collisionMesh == null)
                throw new InvalidOperationException("The spiral ramp did not produce valid render and collision meshes.");

            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                light.shadows = LightShadows.None;

            targetTexture = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32)
            {
                name = "Sisyphus Validation Target"
            };
            if (!targetTexture.Create())
                throw new InvalidOperationException("Could not create the validation render texture.");
            targetCamera.targetTexture = targetTexture;
            stage = 1;
            framesRemaining = 120;
            director.SetRound(stage);
            startTime = EditorApplication.timeSinceStartup;
            initialized = true;
        }

        static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!initialized || camera != targetCamera || !SessionState.GetBool(RunningKey, false))
                return;

            if (EditorApplication.timeSinceStartup - startTime > 60.0)
            {
                Fail(new TimeoutException("Timed out while waiting for play-mode frames."));
                return;
            }

            if (framesRemaining-- > 0)
                return;

            try
            {
                Debug.LogFormat(
                    "Capturing Sisyphus stage {0}: controller={1}, materialMode={2}",
                    stage,
                    vision.Mode,
                    vision.FullScreenMaterial.GetFloat("_SisyphusVisionMode"));
                Luminance[stage - 1] = CaptureStage(stage);
                if (stage == 1)
                    VerifyBoulderSupport();
                if (stage < 5)
                {
                    stage++;
                    director.SetRound(stage);
                    framesRemaining = 20;
                    return;
                }

                if (Luminance[0] < 0.04f)
                    throw new InvalidOperationException("Stage 1 rendered too dark to read the world.");
                if (Luminance[4] > 0.01f)
                    throw new InvalidOperationException("Stage 5 did not render as full black.");

                string message = string.Format(
                    "Sisyphus play-mode validation passed. Luminance: [{0:F3}, {1:F3}, {2:F3}, {3:F3}, {4:F3}]",
                    Luminance[0],
                    Luminance[1],
                    Luminance[2],
                    Luminance[3],
                    Luminance[4]);
                Finish(0, message);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        static float CaptureStage(int stageNumber)
        {
            string directory = Path.Combine(Application.dataPath, "SisyphusPrototype", "Screenshots", "PlayModeValidation");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Sisyphus_Stage" + stageNumber + ".png");
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGB24, false);

            try
            {
                RenderTexture.active = targetTexture;
                texture.ReadPixels(new Rect(0f, 0f, targetTexture.width, targetTexture.height), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return CalculateAverageLuminance(texture.GetPixels32());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(texture);
            }
        }

        static void VerifyBoulderSupport()
        {
            if (!Physics.Raycast(
                    boulder.worldCenterOfMass + Vector3.up,
                    Vector3.down,
                    out RaycastHit hit,
                    8f,
                    ~(1 << 2),
                    QueryTriggerInteraction.Ignore))
            {
                throw new InvalidOperationException("The boulder has no supporting surface after settling.");
            }

            if (hit.collider.GetComponent<SisyphusSpiralRamp>() == null &&
                hit.collider.gameObject.name != "Boulder Start Chock")
            {
                throw new InvalidOperationException(
                    "The boulder fell off the ramp before the player could push it. Support: " +
                    hit.collider.gameObject.name);
            }
        }

        static float CalculateAverageLuminance(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
                return 0f;

            double sum = 0.0;
            int samples = 0;
            for (int i = 0; i < pixels.Length; i += 8)
            {
                Color32 pixel = pixels[i];
                sum += (pixel.r * 0.2126 + pixel.g * 0.7152 + pixel.b * 0.0722) / 255.0;
                samples++;
            }
            return samples > 0 ? (float)(sum / samples) : 0f;
        }

        static void Fail(Exception exception)
        {
            Finish(1, "Sisyphus play-mode validation failed: " + exception);
        }

        static void Finish(int exitCode, string message)
        {
            if (targetCamera != null)
                targetCamera.targetTexture = null;
            if (targetTexture != null)
            {
                if (RenderTexture.active == targetTexture)
                    RenderTexture.active = null;
                targetTexture.Release();
                UnityEngine.Object.Destroy(targetTexture);
            }

            initialized = false;
            SessionState.SetInt(ExitCodeKey, exitCode);
            SessionState.SetString(MessageKey, message);
            SessionState.SetBool(CompleteKey, true);
            EditorApplication.isPlaying = false;
        }
    }
}
