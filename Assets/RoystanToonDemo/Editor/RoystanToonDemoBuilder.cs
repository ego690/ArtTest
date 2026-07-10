using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RoystanToonDemo.Editor
{
    public static class RoystanToonDemoBuilder
    {
        const string Root = "Assets/RoystanToonDemo";
        const string MaterialFolder = Root + "/Materials";
        const string SceneFolder = Root + "/Scenes";
        const string ScenePath = SceneFolder + "/RoystanToonDemo.unity";

        [MenuItem("Tools/Roystan Toon Demo/Rebuild Demo Scene")]
        public static void RebuildDemoScene()
        {
            EnsureFolders();

            Shader toonShader = Shader.Find("RoystanToonDemo/Toon URP");
            if (toonShader == null)
            {
                Debug.LogError("Roystan toon shader was not found. Let Unity finish importing, then rebuild the demo.");
                return;
            }

            Material cream = CreateToonMaterial("M_RTD_Cream", toonShader, new Color(0.92f, 0.80f, 0.56f), new Color(0.34f, 0.44f, 0.58f), 0.11f, 52f, 0.56f);
            Material moss = CreateToonMaterial("M_RTD_Moss", toonShader, new Color(0.40f, 0.66f, 0.34f), new Color(0.16f, 0.30f, 0.28f), 0.18f, 38f, 0.60f);
            Material clay = CreateToonMaterial("M_RTD_Clay", toonShader, new Color(0.82f, 0.42f, 0.32f), new Color(0.38f, 0.24f, 0.36f), 0.14f, 64f, 0.52f);
            Material slate = CreateToonMaterial("M_RTD_Slate", toonShader, new Color(0.38f, 0.48f, 0.62f), new Color(0.18f, 0.22f, 0.30f), 0.09f, 80f, 0.50f);
            Material ground = CreateToonMaterial("M_RTD_Ground", toonShader, new Color(0.54f, 0.60f, 0.48f), new Color(0.30f, 0.36f, 0.36f), 0.45f, 18f, 0.88f);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RoystanToonDemo";

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.36f, 0.44f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.24f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.13f, 0.12f);

            Camera camera = CreateCamera();
            Light sun = CreateSun();

            GameObject root = new GameObject("Roystan Toon Demo");
            GameObject display = new GameObject("Toon Lit Objects");
            display.transform.SetParent(root.transform);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Shadow Receiver - Toon Ground";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(1.8f, 1f, 1.2f);
            SetMaterial(floor, ground);

            CreatePrimitive(display.transform, PrimitiveType.Sphere, "1 Light Band + Shadow", new Vector3(-3.2f, 1.0f, 0f), new Vector3(1.35f, 1.35f, 1.35f), cream);
            CreatePrimitive(display.transform, PrimitiveType.Capsule, "2 Specular Toon Highlight", new Vector3(-1.05f, 1.15f, 0f), new Vector3(1.1f, 1.35f, 1.1f), moss);
            CreatePrimitive(display.transform, PrimitiveType.Cylinder, "3 Rim Light", new Vector3(1.05f, 1.0f, 0f), new Vector3(1.25f, 1.35f, 1.25f), clay);
            CreatePrimitive(display.transform, PrimitiveType.Cube, "4 Hard Surface Toon", new Vector3(3.2f, 1.0f, 0f), new Vector3(1.35f, 1.35f, 1.35f), slate);

            CreatePrimitive(root.transform, PrimitiveType.Cube, "Tall Shadow Caster", new Vector3(-4.4f, 1.5f, -1.7f), new Vector3(0.5f, 3f, 0.5f), slate);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Short Shadow Caster", new Vector3(2.3f, 0.7f, -2.0f), new Vector3(1.2f, 1.4f, 0.45f), clay);

            GameObject label = new GameObject("Demo Labels");
            label.transform.SetParent(root.transform);
            CreateLabel(label.transform, "Roystan-style toon shader: banded diffuse, ambient fill, specular patch, rim light, URP shadows", new Vector3(0f, 2.9f, 1.2f), 0.22f);
            CreateLabel(label.transform, "Press Play: objects rotate and the main light moves so the bands, highlights, rim and shadows are easy to inspect.", new Vector3(0f, 2.55f, 1.2f), 0.14f);

            RoystanToonDemo.RoystanToonDemoMotion motion = root.AddComponent<RoystanToonDemo.RoystanToonDemoMotion>();
            SerializedObject serializedMotion = new SerializedObject(motion);
            serializedMotion.FindProperty("rotatingGroup").objectReferenceValue = display.transform;
            serializedMotion.FindProperty("sun").objectReferenceValue = sun;
            serializedMotion.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeObject = root;
            camera.transform.LookAt(new Vector3(0f, 1f, 0f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt Roystan toon demo scene at " + ScenePath);
        }

        static void EnsureFolders()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(SceneFolder);
            AssetDatabase.Refresh();
        }

        static Material CreateToonMaterial(string name, Shader shader, Color baseColor, Color shadowColor, float specularSize, float glossiness, float rimAmount)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_AmbientColor", new Color(0.28f, 0.32f, 0.40f, 1f));
            material.SetColor("_ShadowColor", shadowColor);
            material.SetFloat("_ShadowThreshold", 0.02f);
            material.SetFloat("_ShadowSoftness", 0.025f);
            material.SetColor("_SpecularColor", new Color(1.12f, 1.02f, 0.76f, 1f));
            material.SetFloat("_SpecularSize", specularSize);
            material.SetFloat("_Glossiness", glossiness);
            material.SetColor("_RimColor", new Color(0.56f, 0.78f, 1.18f, 1f));
            material.SetFloat("_RimAmount", rimAmount);
            material.SetFloat("_RimThreshold", 0.38f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 3.15f, 6.9f);
            cameraObject.transform.rotation = Quaternion.Euler(64f, 180f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.58f, 0.70f, 0.82f, 1f);
            camera.fieldOfView = 36f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            return camera;
        }

        static Light CreateSun()
        {
            GameObject sunObject = new GameObject("Main Directional Light");
            sunObject.transform.rotation = Quaternion.Euler(44f, -32f, 0f);

            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.78f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            return sun;
        }

        static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent);
            primitive.transform.position = position;
            primitive.transform.localScale = scale;
            SetMaterial(primitive, material);
            return primitive;
        }

        static void SetMaterial(GameObject gameObject, Material material)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        static void CreateLabel(Transform parent, string text, Vector3 position, float size)
        {
            GameObject labelObject = new GameObject(text.Length > 28 ? text.Substring(0, 28) : text);
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.Euler(65f, 180f, 0f);

            TextMesh textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = size;
            textMesh.fontSize = 48;
            textMesh.color = new Color(0.08f, 0.10f, 0.12f, 1f);
        }
    }
}
