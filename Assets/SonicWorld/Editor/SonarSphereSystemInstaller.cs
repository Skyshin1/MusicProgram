#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>One-click installer for the sonar sphere renderer feature and scene-side controls.</summary>
public static class SonarSphereSystemInstaller
{
    private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

    [MenuItem("Tools/Sonar/Install Sphere Sonar, Outlines And Lantern")]
    private static void Install()
    {
        InstallRendererFeature();
        InstallSceneComponents();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog(
            "Sphere Sonar Installed",
            "Installed the white-outline renderer feature and added scene controls.\n\n" +
            "Use F for the player sonar. Put objects that should reveal on the Sonar Reveal Manager target layer mask.\n" +
            "The lantern is attached to the sonar origin and defaults to a 2m forward cylinder.",
            "OK");
    }

    [MenuItem("Tools/Sonar/Select Sphere Sonar Controls")]
    private static void SelectControls()
    {
        SonarRevealManager manager = Object.FindFirstObjectByType<SonarRevealManager>();
        if (manager != null)
            Selection.activeGameObject = manager.gameObject;
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

    private static void InstallRendererFeature()
    {
        ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRendererPath);
        if (rendererData == null)
        {
            Debug.LogError($"Sonar installer could not find the renderer data at {PcRendererPath}.");
            return;
        }

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
