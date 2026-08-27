using DeepSeaAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace DeepSeaAI.Editor
{
    public static class DeepSeaOxygenMeterInstaller
    {
        private const string ScenePath = "Assets/Scenes/1-VR.unity";
        private const string MeterName = "Oxygen Meter (Move To Glove)";
        private const string GeneratedFolder = "Assets/DeepSeaAI/Generated";

        [MenuItem("Tools/Deep Sea Oxygen/Install Or Repair Mesh Meter")]
        private static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Deep Sea Oxygen", "Exit Play Mode before installing the oxygen meter.", "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null)
            {
                EditorUtility.DisplayDialog("Deep Sea Oxygen", "No XR Origin was found in this scene.", "OK");
                return;
            }

            EnsureGeneratedFolder();
            PlayerOxygen oxygen = origin.GetComponent<PlayerOxygen>();
            if (oxygen == null)
                oxygen = Undo.AddComponent<PlayerOxygen>(origin.gameObject);

            Transform previous = FindNamedChild(origin.transform, MeterName);
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);

            Transform glove = FindHandTransform(origin.transform) ??
                (origin.Camera != null ? origin.Camera.transform : origin.transform);
            GameObject meter = new GameObject(MeterName);
            meter.transform.SetParent(glove, false);
            meter.transform.localPosition = new Vector3(0.08f, -0.06f, 0.16f);
            meter.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

            CreateMesh(meter.transform, "Oxygen Meter Frame Mesh", new Vector3(0f, 0f, 0f), new Vector3(0.18f, 0.04f, 0.018f), new Color(0.015f, 0.035f, 0.05f), "Deep Sea Oxygen Frame Material");
            Transform fill = CreateMesh(meter.transform, "Oxygen Fill Mesh - Scale Controlled", new Vector3(0f, 0f, -0.014f), new Vector3(0.16f, 0.024f, 0.019f), new Color(0.08f, 0.95f, 0.78f), "Deep Sea Oxygen Fill Material");

            OxygenMeshMeter meshMeter = meter.AddComponent<OxygenMeshMeter>();
            meshMeter.Configure(oxygen, fill);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = meter;
            Debug.Log("[DeepSeaOxygen] Installed a mesh-only oxygen meter. Move 'Oxygen Meter (Move To Glove)' under any glove / hand transform later; its fill mesh is controlled by local Scale only.", meter);
        }

        private static Transform CreateMesh(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, string materialName)
        {
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = localPosition;
            mesh.transform.localScale = localScale;
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.GetComponent<Renderer>().sharedMaterial = GetMaterial(materialName, color);
            return mesh.transform;
        }

        private static Transform FindHandTransform(Transform root)
        {
            Transform right = FindNamedChild(root, "Right");
            return right != null ? right : FindNamedChild(root, "right");
        }

        private static Transform FindNamedChild(Transform root, string childName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == childName)
                    return child;
            }
            return null;
        }

        private static Material GetMaterial(string materialName, Color color)
        {
            string path = GeneratedFolder + "/" + materialName.Replace(" ", string.Empty) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * 1.4f);
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
