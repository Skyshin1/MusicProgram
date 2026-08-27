using DeepSeaAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace DeepSeaAI.Editor
{
    /// <summary>Creates replaceable placeholder fish so behaviour can be tested before art arrives.</summary>
    public static class DeepSeaFishDemoInstaller
    {
        private const string ScenePath = "Assets/Scenes/1-VR.unity";
        private const string RootName = "Deep Sea Fish Demo School";
        private const string GeneratedFolder = "Assets/DeepSeaAI/Generated";

        [MenuItem("Tools/Deep Sea Fish/Install Or Repair Demo School")]
        private static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Deep Sea Fish", "Exit Play Mode before installing the fish demo.", "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureGeneratedFolder();

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
            Camera camera = origin != null ? origin.Camera : Camera.main;
            Vector3 center = camera != null ? camera.transform.position + camera.transform.forward * 6f : new Vector3(0f, 2f, 6f);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Install Deep Sea Fish Demo School");
            root.transform.position = center;

            for (int i = 0; i < 6; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-3.5f, 3.5f),
                    Random.Range(-1.3f, 1.3f),
                    Random.Range(-2f, 2f));
                CreateFish(root.transform, "Fish " + (i + 1), center + offset, 4.8f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("[DeepSeaFish] Installed six roaming fish. Their procedural swim animation and fallback fish sounds work immediately; replace the model, Animator and audio clips later through DeepSeaFishAI.", root);
        }

        private static void CreateFish(Transform parent, string name, Vector3 position, float radius)
        {
            GameObject fish = new GameObject(name);
            fish.transform.SetParent(parent, false);
            fish.transform.position = position;
            fish.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject visual = new GameObject("Fish Visual");
            visual.transform.SetParent(fish.transform, false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body Mesh";
            body.transform.SetParent(visual.transform, false);
            body.transform.localScale = new Vector3(0.45f, 0.22f, 0.72f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = GetMaterial("Deep Sea Fish Body Material", new Color(0.18f, 0.72f, 0.86f));

            GameObject tail = new GameObject("Tail Mesh");
            tail.transform.SetParent(visual.transform, false);
            tail.transform.localPosition = new Vector3(0f, 0f, -0.7f);
            CreateFin(tail.transform, "Tail Upper", new Vector3(0.17f, 0.05f, 0.34f), -26f);
            CreateFin(tail.transform, "Tail Lower", new Vector3(0.17f, 0.05f, 0.34f), 26f);

            AudioSource audio = fish.AddComponent<AudioSource>();
            DeepSeaFishAI ai = fish.AddComponent<DeepSeaFishAI>();
            ai.ConfigureDemo(parent, tail.transform, visual.transform, audio, radius);
        }

        private static void CreateFin(Transform parent, string name, Vector3 position, float rotation)
        {
            GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fin.name = name;
            fin.transform.SetParent(parent, false);
            fin.transform.localPosition = position;
            fin.transform.localRotation = Quaternion.Euler(0f, rotation, 0f);
            fin.transform.localScale = new Vector3(0.18f, 0.035f, 0.42f);
            Object.DestroyImmediate(fin.GetComponent<Collider>());
            fin.GetComponent<Renderer>().sharedMaterial = GetMaterial("Deep Sea Fish Tail Material", new Color(0.08f, 0.45f, 0.65f));
        }

        private static Material GetMaterial(string name, Color color)
        {
            string path = GeneratedFolder + "/" + name.Replace(" ", string.Empty) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * 0.25f);
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/DeepSeaAI"))
                AssetDatabase.CreateFolder("Assets", "DeepSeaAI");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/DeepSeaAI", "Generated");
        }
    }
}
