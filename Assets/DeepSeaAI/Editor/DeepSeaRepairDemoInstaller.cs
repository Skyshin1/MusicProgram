using DeepSeaAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

namespace DeepSeaAI.Editor
{
    /// <summary>Creates a small, removable repair-tool example in the 1-VR scene.</summary>
    public static class DeepSeaRepairDemoInstaller
    {
        private const string ScenePath = "Assets/Scenes/1-VR.unity";
        private const string RootName = "Deep Sea Repair Demo";
        private const string ToolId = "StandardRepairTool";
        private const string GeneratedFolder = "Assets/DeepSeaAI/Generated";

        [MenuItem("Tools/Deep Sea Repair/Install Or Repair Basic Demo")]
        private static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Deep Sea Repair", "Exit Play Mode before installing the demo.", "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EnsureGeneratedFolder();

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Install Deep Sea Repair Demo");
            EnsureInteractionManager(root.transform);

            XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
            Camera camera = origin != null ? origin.Camera : Camera.main;
            Vector3 playerPosition = camera != null ? camera.transform.position : Vector3.zero;
            Vector3 forward = camera != null
                ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized
                : Vector3.forward;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float floorY = FindFloorY(playerPosition.y - 1.6f);

            CreateTool(root.transform, playerPosition + forward * 1.05f + right * 0.38f + Vector3.up * 0.12f);
            CreateFacility(root.transform, playerPosition + forward * 2.2f - right * 0.35f, floorY);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("[DeepSeaRepair] Installed a grab-able Repair Tool and a damaged Repair Facility. Grab the tool, approach the red panel, then hold the controller Activate input to repair.", root);
        }

        [MenuItem("Tools/Deep Sea Repair/Select Basic Demo")]
        private static void SelectDemo()
        {
            Selection.activeGameObject = GameObject.Find(RootName);
        }

        private static void EnsureInteractionManager(Transform parent)
        {
            if (Object.FindFirstObjectByType<XRInteractionManager>() != null)
                return;
            var manager = new GameObject("XR Interaction Manager");
            manager.transform.SetParent(parent, false);
            manager.AddComponent<XRInteractionManager>();
        }

        private static void CreateTool(Transform parent, Vector3 position)
        {
            GameObject tool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tool.name = "Repair Tool (Grab Me)";
            tool.transform.SetParent(parent, false);
            tool.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            tool.transform.localScale = new Vector3(0.075f, 0.28f, 0.075f);

            Renderer renderer = tool.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateLitMaterial("Deep Sea Repair Tool Material", new Color(0.08f, 0.65f, 0.75f), 1.1f);

            Rigidbody body = tool.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = tool.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.throwOnDetach = true;

            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Repair Tip";
            tip.transform.SetParent(tool.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, 2.05f);
            tip.transform.localScale = Vector3.one * 0.45f;
            Object.DestroyImmediate(tip.GetComponent<Collider>());
            tip.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial("Deep Sea Repair Tip Material", new Color(0.35f, 1f, 0.92f), 2.4f);

            GameObject beamObject = new GameObject("Repair Beam");
            beamObject.transform.SetParent(tool.transform, false);
            LineRenderer beam = beamObject.AddComponent<LineRenderer>();
            beam.sharedMaterial = CreateLineMaterial();
            beam.alignment = LineAlignment.View;
            beam.textureMode = LineTextureMode.Stretch;

            RepairTool repairTool = tool.AddComponent<RepairTool>();
            repairTool.Configure(ToolId, tip.transform, beam);
        }

        private static void CreateFacility(Transform parent, Vector3 position, float floorY)
        {
            GameObject facility = new GameObject("Repair Facility (Decal Damage)");
            facility.name = "Repair Facility (Decal Damage)";
            facility.transform.SetParent(parent, false);
            facility.transform.position = new Vector3(position.x, floorY + 0.65f, position.z);
            BoxCollider collider = facility.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0f, 0f);
            collider.size = new Vector3(0.95f, 1.3f, 0.55f);

            CreateFacilityVariant(
                facility.transform,
                "Facility Mesh",
                new Color(0.08f, 0.42f, 0.48f),
                new Vector3(0.85f, 1.3f, 0.35f),
                PrimitiveType.Cube);
            DecalProjector damageDecal = CreateDamageDecal(facility.transform);

            RepairableFacility repairable = facility.AddComponent<RepairableFacility>();
            repairable.Configure(ToolId, 3f, new[] { damageDecal });
        }

        private static DecalProjector CreateDamageDecal(Transform parent)
        {
            GameObject decalObject = new GameObject("Damage Decal (Fades During Repair)");
            decalObject.transform.SetParent(parent, false);
            // The projector sits just in front of the facility and projects back
            // onto its front face. Its opacity is controlled by RepairableFacility.
            decalObject.transform.localPosition = new Vector3(0f, 0f, -0.21f);
            decalObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            DecalProjector decal = decalObject.AddComponent<DecalProjector>();
            decal.size = new Vector3(0.68f, 0.95f, 0.18f);
            decal.drawDistance = 50f;
            decal.fadeFactor = 1f;
            decal.material = CreateDamageDecalMaterial();
            return decal;
        }

        private static GameObject CreateFacilityVariant(
            Transform parent,
            string name,
            Color color,
            Vector3 scale,
            PrimitiveType primitive)
        {
            GameObject variant = GameObject.CreatePrimitive(primitive);
            variant.name = name;
            variant.transform.SetParent(parent, false);
            variant.transform.localScale = scale;
            Object.DestroyImmediate(variant.GetComponent<Collider>());
            Renderer renderer = variant.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateLitMaterial(
                "Deep Sea " + name.Replace(" ", string.Empty) + " Material",
                color,
                1.2f);
            return variant;
        }

        private static float FindFloorY(float fallback)
        {
            GameObject floor = GameObject.Find("Plane") ?? GameObject.Find("Floor Collider");
            Collider collider = floor != null ? floor.GetComponent<Collider>() : null;
            return collider != null ? collider.bounds.max.y : fallback;
        }

        private static Material CreateLitMaterial(string name, Color color, float emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            string path = GeneratedFolder + "/" + name.Replace(" ", string.Empty) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * emission);
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateDamageDecalMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Decal");
            if (shader == null)
            {
                Debug.LogWarning("[DeepSeaRepair] URP Decal shader was not found. Add/enable the URP Decal Renderer Feature before installing the decal repair demo.");
                return null;
            }
            const string path = GeneratedFolder + "/DeepSeaDamageDecal.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Deep Sea Damage Decal Material" };
                AssetDatabase.CreateAsset(material, path);
            }

            Color damageColor = new Color(0.9f, 0.05f, 0.01f, 0.9f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", damageColor);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            const string path = GeneratedFolder + "/DeepSeaRepairBeamMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Deep Sea Repair Beam Material" };
                AssetDatabase.CreateAsset(material, path);
            }
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
