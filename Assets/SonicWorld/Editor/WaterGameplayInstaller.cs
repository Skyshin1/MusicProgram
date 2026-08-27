#if UNITY_EDITOR
using System.Collections.Generic;
using AbstractOcclusion.WebGpuWater;
using DeepSeaAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

/// <summary>Creates reusable water-gameplay prefabs and wires the active scene/URP renderers.</summary>
public static class WaterGameplayInstaller
{
    private const string PrefabFolder = "Assets/SonicWorld/Prefab/WaterGameplay";
    private const string FlashlightPath = PrefabFolder + "/VR Water Flashlight.prefab";
    private const string BlackBoxPath = PrefabFolder + "/VR Black Box.prefab";
    private const string DockPath = PrefabFolder + "/Black Box Playback Dock.prefab";

    [InitializeOnLoadMethod]
    private static void ScheduleProjectAssetSetup()
    {
        EditorApplication.delayCall += EnsureProjectAssets;
    }

    private static void EnsureProjectAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        EnsureFolders();
        EnsureRendererFeatures();
        EnsureFlashlightPrefab();
        EnsureBlackBoxPrefab();
        EnsureDockPrefab();
        AssetDatabase.SaveAssets();
    }

    /// <summary>Headless entry point used by CI/automation to create reusable assets.</summary>
    public static void InstallProjectAssetsBatch()
    {
        EnsureProjectAssets();
    }

    [MenuItem("Tools/VR Water Gameplay/Install Complete Water Gameplay")]
    public static void Install()
    {
        EnsureFolders();
        EnsureRendererFeatures();
        GameObject flashlight = EnsureFlashlightPrefab();
        GameObject blackBox = EnsureBlackBoxPrefab();
        GameObject dock = EnsureDockPrefab();
        EnsurePlayerComponents();
        EnsureRepairToolsAreBuoyant();
        EnsureSceneProps(flashlight, blackBox, dock);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog(
            "VR Water Gameplay Installed",
            "Player swimming/gravity, empty-hand Trigger sonar, underwater ambience and exit droplets are wired.\n\n" +
            "Flashlight, black-box and playback-dock prefabs were created under:\n" + PrefabFolder +
            "\n\nMove the generated Water Gameplay Props root onto your surface platform, then replace the temporary audio clips in the Inspector.",
            "OK");
    }

    [MenuItem("Tools/VR Water Gameplay/Configure Selected Props For Water Buoyancy")]
    private static void ConfigureSelectedProps()
    {
        int changed = 0;
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected == null)
                continue;
            ConfigureBuoyantGrabProp(selected);
            EditorUtility.SetDirty(selected);
            changed++;
        }
        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Water Buoyancy", $"Configured {changed} selected prop(s).", "OK");
    }

    private static void EnsurePlayerComponents()
    {
        XROrigin[] origins = Object.FindObjectsByType<XROrigin>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (XROrigin origin in origins)
        {
            AddUndoIfMissing<CharacterController>(origin.gameObject);
            AddUndoIfMissing<WaterSurfaceStateTracker>(origin.gameObject);
            AddUndoIfMissing<QuestLeftStickLocomotion>(origin.gameObject);
            AddUndoIfMissing<XRHandSonarInput>(origin.gameObject);
            AddUndoIfMissing<UnderwaterAmbienceController>(origin.gameObject);
            AddUndoIfMissing<WaterExitLensEffect>(origin.gameObject);

            SonarFogLantern lantern = origin.GetComponent<SonarFogLantern>();
            if (lantern == null)
                lantern = Undo.AddComponent<SonarFogLantern>(origin.gameObject);
            SerializedObject serialized = new(lantern);
            SerializedProperty radius = serialized.FindProperty("radius");
            if (radius != null && radius.floatValue > 1f)
                radius.floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lantern);
        }
    }

    private static void EnsureRepairToolsAreBuoyant()
    {
        RepairTool[] tools = Object.FindObjectsByType<RepairTool>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (RepairTool tool in tools)
            ConfigureBuoyantGrabProp(tool.gameObject);
    }

    private static void ConfigureBuoyantGrabProp(GameObject root)
    {
        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body == null)
            body = Undo.AddComponent<Rigidbody>(root);
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (root.GetComponent<Collider>() == null)
            Undo.AddComponent<BoxCollider>(root);
        AddUndoIfMissing<XRGrabInteractable>(root);
        AddUndoIfMissing<WaterBuoyancy>(root);
        AddUndoIfMissing<WaterSplash>(root);
        AddUndoIfMissing<BuoyantXRGrabBridge>(root);
        AddUndoIfMissing<VolumetricFogCollisionPulse>(root);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer is LineRenderer || renderer is ParticleSystemRenderer)
                continue;
            AddUndoIfMissing<WaterInteractable>(renderer.gameObject);
            AddUndoIfMissing<WaterMembership>(renderer.gameObject);
        }
    }

    private static GameObject EnsureFlashlightPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPath);
        if (existing != null)
            return existing;

        GameObject root = new("VR Water Flashlight");
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.13f, 0.13f, 0.42f);
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 0.45f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        root.AddComponent<XRGrabInteractable>();
        root.AddComponent<WaterBuoyancy>();
        root.AddComponent<WaterSplash>();
        root.AddComponent<BuoyantXRGrabBridge>();
        root.AddComponent<VolumetricFogCollisionPulse>();
        GrabFlashlight flashlight = root.AddComponent<GrabFlashlight>();
        SerializedObject flashlightSerialized = new(flashlight);
        SerializedProperty beamShader = flashlightSerialized.FindProperty("beamShader");
        if (beamShader != null)
            beamShader.objectReferenceValue =
                Shader.Find("Hidden/Sonar/Quest Flashlight Beam");
        flashlightSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "Flashlight Body";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        visual.transform.localScale = new Vector3(0.07f, 0.2f, 0.07f);
        visual.AddComponent<WaterInteractable>();
        visual.AddComponent<WaterMembership>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, FlashlightPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject EnsureBlackBoxPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BlackBoxPath);
        if (existing != null)
            return existing;

        GameObject root = new("VR Black Box");
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.28f, 0.18f, 0.38f);
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 1.1f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        root.AddComponent<XRGrabInteractable>();
        root.AddComponent<WaterBuoyancy>();
        root.AddComponent<WaterSplash>();
        root.AddComponent<BuoyantXRGrabBridge>();
        root.AddComponent<VolumetricFogCollisionPulse>();
        root.AddComponent<BlackBoxItem>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Black Box Visual";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = collider.size;
        visual.AddComponent<WaterInteractable>();
        visual.AddComponent<WaterMembership>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BlackBoxPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject EnsureDockPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DockPath);
        if (existing != null)
            return existing;

        GameObject root = new("Black Box Playback Dock");
        BoxCollider trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(0.45f, 0.3f, 0.55f);
        root.AddComponent<XRSocketInteractor>();
        AudioSource audio = root.AddComponent<AudioSource>();
        audio.spatialBlend = 1f;
        root.AddComponent<BlackBoxPlaybackDock>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Dock Visual";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, -0.13f, 0f);
        visual.transform.localScale = new Vector3(0.55f, 0.08f, 0.65f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DockPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void EnsureSceneProps(GameObject flashlight, GameObject blackBox, GameObject dock)
    {
        GameObject parent = GameObject.Find("Water Gameplay Props");
        if (parent == null)
            parent = new GameObject("Water Gameplay Props");

        Vector3 basePosition = ResolveSurfacePlacement();
        EnsureSceneInstance(flashlight, "VR Water Flashlight", parent.transform,
            basePosition + new Vector3(-0.5f, 0.25f, 0f));
        EnsureSceneInstance(blackBox, "VR Black Box", parent.transform,
            basePosition + new Vector3(0f, 0.3f, 0f));
        EnsureSceneInstance(dock, "Black Box Playback Dock", parent.transform,
            basePosition + new Vector3(0.65f, 0.1f, 0f));
    }

    private static Vector3 ResolveSurfacePlacement()
    {
        XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
        Vector3 position = origin != null ? origin.transform.position + origin.transform.forward * 1.5f : Vector3.zero;
        WaterVolume water = Object.FindFirstObjectByType<WaterVolume>();
        if (water != null && water.TryGetAnalyticWaterline(position.x, position.z, out float surfaceY))
            position.y = surfaceY + 0.5f;
        return position;
    }

    private static void EnsureSceneInstance(GameObject prefab, string objectName,
        Transform parent, Vector3 position)
    {
        if (GameObject.Find(objectName) != null || prefab == null)
            return;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return;
        Undo.RegisterCreatedObjectUndo(instance, "Create " + objectName);
        instance.name = objectName;
        instance.transform.SetParent(parent, true);
        instance.transform.position = position;
    }

    private static void EnsureRendererFeatures()
    {
        HashSet<ScriptableRendererData> renderers = new();
        string[] paths =
        {
            "Assets/Settings/Mobile_Renderer.asset",
            "Assets/Settings/PC_Renderer.asset"
        };
        foreach (string path in paths)
        {
            ScriptableRendererData data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (data != null)
                renderers.Add(data);
        }

        foreach (ScriptableRendererData data in renderers)
        {
            EnsureWaterFogFeature(data);
            EnsureOutlineFeature(data);
            EnsureDropletFeature(data);
        }
    }

    private static void EnsureWaterFogFeature(ScriptableRendererData rendererData)
    {
        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is not WaterUnderwaterFogFeature fog)
                continue;
            fog.SetActive(true);
            fog.Create();
            EditorUtility.SetDirty(fog);
            return;
        }

        WaterUnderwaterFogFeature created =
            ScriptableObject.CreateInstance<WaterUnderwaterFogFeature>();
        created.name = "WebGPU Water - Underwater Fog";
        SerializedObject serialized = new(created);
        SerializedProperty shader = serialized.FindProperty("underwaterFogShader");
        if (shader != null)
            shader.objectReferenceValue =
                Shader.Find("AbstractOcclusion/WebGpuWater/WaterUnderwaterFog");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.AddObjectToAsset(created, rendererData);
        rendererData.rendererFeatures.Insert(0, created);
        created.SetActive(true);
        created.Create();
        EditorUtility.SetDirty(created);
        EditorUtility.SetDirty(rendererData);
    }

    private static void EnsureOutlineFeature(ScriptableRendererData rendererData)
    {
        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is not SonarWhiteOutlineRendererFeature outline)
                continue;
            outline.SetActive(true);
            outline.Create();
            EditorUtility.SetDirty(outline);
            return;
        }

        SonarWhiteOutlineRendererFeature created =
            ScriptableObject.CreateInstance<SonarWhiteOutlineRendererFeature>();
        created.name = "Sonar White Outlines";
        AssetDatabase.AddObjectToAsset(created, rendererData);
        rendererData.rendererFeatures.Add(created);
        created.SetActive(true);
        created.Create();
        EditorUtility.SetDirty(created);
        EditorUtility.SetDirty(rendererData);
    }

    private static void EnsureDropletFeature(ScriptableRendererData rendererData)
    {
        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is not WaterExitDropletsRendererFeature droplets)
                continue;
            AssignDropletShader(droplets);
            droplets.SetActive(true);
            droplets.Create();
            EditorUtility.SetDirty(droplets);
            return;
        }

        WaterExitDropletsRendererFeature created =
            ScriptableObject.CreateInstance<WaterExitDropletsRendererFeature>();
        created.name = "Water Exit Lens Droplets";
        AssignDropletShader(created);
        AssetDatabase.AddObjectToAsset(created, rendererData);
        rendererData.rendererFeatures.Add(created);
        created.SetActive(true);
        created.Create();
        EditorUtility.SetDirty(created);
        EditorUtility.SetDirty(rendererData);
    }

    private static void AssignDropletShader(WaterExitDropletsRendererFeature feature)
    {
        SerializedObject serialized = new(feature);
        SerializedProperty shader = serialized.FindProperty("shader");
        if (shader != null)
            shader.objectReferenceValue =
                Shader.Find("Hidden/Sonar/Water Exit Lens Droplets");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/SonicWorld/Prefab"))
            AssetDatabase.CreateFolder("Assets/SonicWorld", "Prefab");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/SonicWorld/Prefab", "WaterGameplay");
    }

    private static T AddUndoIfMissing<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(target);
    }
}
#endif
