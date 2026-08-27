#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.XR.CoreUtils;
using AbstractOcclusion.WebGpuWater;

/// <summary>One-click installer for the sonar sphere renderer feature and scene-side controls.</summary>
public static class SonarSphereSystemInstaller
{
    [MenuItem("Tools/Sonar/Install Water Volume Sonar, Outlines And Lantern")]
    [MenuItem("Tools/Sonar/Install Sphere Sonar, Outlines And Lantern")]
    private static void Install()
    {
        InstallRendererFeatures();
        InstallSceneComponents();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog(
            "Water Volume Sonar Installed",
            "Installed the Water Volume fog feature, white-outline renderer feature and scene controls.\n\n" +
            "Use either empty-hand Trigger for player sonar. F is an optional editor-only test input. " +
            "Put revealable objects on the Sonar Reveal Manager target layer mask.\n" +
            "The lantern follows the XR camera and clears Water Volume visibility in a 1m forward cylinder.",
            "OK");
    }

    [MenuItem("Tools/Sonar/Select Sphere Sonar Controls")]
    private static void SelectControls()
    {
        SonarRevealManager manager = Object.FindFirstObjectByType<SonarRevealManager>();
        if (manager != null)
            Selection.activeGameObject = manager.gameObject;
    }

    [MenuItem("Tools/Sonar/Hide Wire Sphere Visuals")]
    private static void HideWireSphereVisuals()
    {
        SonarWaveVisualSystem visuals = Object.FindFirstObjectByType<SonarWaveVisualSystem>();
        if (visuals == null)
        {
            EditorUtility.DisplayDialog(
                "Sonar Wire Sphere",
                "No Sonar Wave Visual System is currently loaded. The wire-sphere visual is already absent.",
                "OK");
            return;
        }

        Undo.RecordObject(visuals, "Hide Sonar Wire Sphere Visuals");
        visuals.SetWireSphereVisible(false);
        EditorUtility.SetDirty(visuals);
        Selection.activeGameObject = visuals.gameObject;
        EditorUtility.DisplayDialog(
            "Sonar Wire Sphere Hidden",
            "Only the white wire-sphere visual was hidden. Sonar pulses, collision triggers, fog interaction and white outlines remain enabled.",
            "OK");
    }

    [MenuItem("Tools/Sonar/Select Wave Visual Controls")]
    private static void SelectWaveVisualControls()
    {
        SonarWaveVisualSystem visuals = Object.FindFirstObjectByType<SonarWaveVisualSystem>();
        if (visuals != null)
            Selection.activeGameObject = visuals.gameObject;
        else
            EditorUtility.DisplayDialog("Sonar Wave Visuals", "Enter Play Mode once to create the runtime visual controller.", "OK");
    }

    [MenuItem("Tools/Sonar/Add Collision Group To Selected Objects")]
    private static void AddCollisionGroupToSelection()
    {
        int changed = 0;
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected == null || selected.GetComponent<SonarCollisionGroup>() != null)
                continue;
            Undo.AddComponent<SonarCollisionGroup>(selected);
            changed++;
        }

        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Sonar Collision Groups",
            changed > 0
                ? $"Added Sonar Collision Group to {changed} selected object(s)."
                : "Select one or more collision target objects that do not already have Sonar Collision Group.",
            "OK");
    }

    [MenuItem("Tools/VR/Install Left Stick Locomotion To XR Origins")]
    private static void InstallLeftStickLocomotion()
    {
        XROrigin[] origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int added = 0;
        foreach (XROrigin origin in origins)
        {
            if (origin == null || origin.GetComponent<QuestLeftStickLocomotion>() != null)
                continue;
            Undo.AddComponent<QuestLeftStickLocomotion>(origin.gameObject);
            added++;
        }

        if (added > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Quest Left Stick Locomotion",
            added > 0
                ? $"Added left-stick movement, right-stick turning and right-stick vertical swimming to {added} XR Origin(s). Select the XR Origin to tune all movement, gravity and sinking values."
                : "Every active XR Origin already has Quest Left Stick Locomotion.",
            "OK");
    }

    private static void InstallRendererFeatures()
    {
        ScriptableRendererData rendererData = ResolveActiveRendererData();
        if (rendererData == null)
        {
            Debug.LogError("Sonar installer could not resolve the active URP Renderer Data.");
            return;
        }

        EnsureWaterUnderwaterFog(rendererData);

        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is SonarWhiteOutlineRendererFeature)
            {
                feature.SetActive(true);
                feature.Create();
                EditorUtility.SetDirty(feature);
                return;
            }
        }

        SonarWhiteOutlineRendererFeature outline = ScriptableObject.CreateInstance<SonarWhiteOutlineRendererFeature>();
        outline.name = "Sonar White Outlines";
        AssetDatabase.AddObjectToAsset(outline, rendererData);
        rendererData.rendererFeatures.Add(outline);
        outline.SetActive(true);
        outline.Create();
        EditorUtility.SetDirty(outline);
        EditorUtility.SetDirty(rendererData);
    }

    private static ScriptableRendererData ResolveActiveRendererData()
    {
        UniversalRenderPipelineAsset pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null)
            pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null)
        {
            // Unity 6.3 no longer exposes scriptableRendererData publicly.
            // Read the same default renderer reference from the URP asset's
            // serialized renderer list, which also works with renderer-indexed
            // quality assets.
            SerializedObject serializedPipeline = new SerializedObject(pipeline);
            SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
            SerializedProperty defaultRenderer = serializedPipeline.FindProperty("m_DefaultRendererIndex");
            if (rendererList != null && rendererList.isArray && rendererList.arraySize > 0)
            {
                int index = defaultRenderer != null ? defaultRenderer.intValue : 0;
                index = Mathf.Clamp(index, 0, rendererList.arraySize - 1);
                ScriptableRendererData active =
                    rendererList.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
                if (active != null)
                    return active;
            }
        }

        // Fallback for an editor session with no active quality pipeline yet.
        const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        ScriptableRendererData mobile = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(MobileRendererPath);
        if (mobile != null)
            return mobile;
        return AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
    }

    private static void EnsureWaterUnderwaterFog(ScriptableRendererData rendererData)
    {
        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is WaterUnderwaterFogFeature)
            {
                feature.SetActive(true);
                feature.Create();
                EditorUtility.SetDirty(feature);
                return;
            }
        }

        WaterUnderwaterFogFeature fog = ScriptableObject.CreateInstance<WaterUnderwaterFogFeature>();
        fog.name = "Water Underwater Fog";
        SerializedObject serialized = new SerializedObject(fog);
        SerializedProperty shader = serialized.FindProperty("underwaterFogShader");
        if (shader != null)
            shader.objectReferenceValue = Shader.Find("AbstractOcclusion/WebGpuWater/WaterUnderwaterFog");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.AddObjectToAsset(fog, rendererData);
        rendererData.rendererFeatures.Add(fog);
        fog.SetActive(true);
        fog.Create();
        EditorUtility.SetDirty(fog);
        EditorUtility.SetDirty(rendererData);
    }

    private static void InstallSceneComponents()
    {
        SonarRevealManager manager = Object.FindFirstObjectByType<SonarRevealManager>();
        if (manager == null)
        {
            GameObject root = new GameObject("Sonar Reveal Manager");
            manager = root.AddComponent<SonarRevealManager>();
        }

        if (Object.FindFirstObjectByType<SonarWaveVisualSystem>() == null)
        {
            GameObject root = new GameObject("Sonar Wave Visual System");
            root.AddComponent<SonarWaveVisualSystem>();
        }

        VolumetricFogPulseEmitter emitter = Object.FindFirstObjectByType<VolumetricFogPulseEmitter>();
        if (emitter == null)
        {
            GameObject root = new GameObject("Volumetric Fog Pulse Emitter");
            emitter = root.AddComponent<VolumetricFogPulseEmitter>();
        }

        Transform lanternOrigin = emitter.OriginTransform;
        if (lanternOrigin.GetComponent<SonarFogLantern>() == null)
            Undo.AddComponent<SonarFogLantern>(lanternOrigin.gameObject);

        Selection.activeGameObject = manager.gameObject;
    }
}
#endif
