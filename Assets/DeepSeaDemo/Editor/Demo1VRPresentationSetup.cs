#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AbstractOcclusion.WebGpuWater;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSeaDemo.Editor
{
    public static class Demo1VRPresentationSetup
    {
        [MenuItem("Tools/Deep Sea Demo/12 Apply Deep Water Profile to Active Copy")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != DeepSeaDemoBuilder.Copy1VR)
                throw new InvalidOperationException("Open the 1-VR copy in Edit Mode. Original scenes are not modified.");
            const string folder = "Assets/DeepSeaDemo/Settings/1VR";
            Directory.CreateDirectory(folder);
            var profile = AssetDatabase.LoadAssetAtPath<DemoDeepWaterProfile>(folder + "/DeepWater.asset");
            if (profile == null) { profile = ScriptableObject.CreateInstance<DemoDeepWaterProfile>(); AssetDatabase.CreateAsset(profile, folder + "/DeepWater.asset"); }
            var text = AssetDatabase.LoadAssetAtPath<DemoTextCatalog>(folder + "/EnglishText.asset");
            if (text == null) { text = ScriptableObject.CreateInstance<DemoTextCatalog>(); AssetDatabase.CreateAsset(text, folder + "/EnglishText.asset"); }
            var roots = scene.GetRootGameObjects();
            foreach (var water in roots.SelectMany(r => r.GetComponentsInChildren<WaterVolume>(true)))
            {
                Undo.RecordObject(water, "Apply Demo water profile");
                if (water.Quality != null && !AssetDatabase.GetAssetPath(water.Quality).StartsWith(folder + "/"))
                {
                    string source = AssetDatabase.GetAssetPath(water.Quality);
                    string dest = folder + "/WaterQuality_" + AssetDatabase.AssetPathToGUID(source) + ".asset";
                    if (!File.Exists(dest) && !AssetDatabase.CopyAsset(source, dest)) throw new IOException("Cannot isolate WaterQuality.");
                    water.Quality = AssetDatabase.LoadAssetAtPath<WaterQuality>(dest);
                }
                var so = new SerializedObject(water);
                Bool(so, "waterFogSettings.waterFog", true);
                Color(so, "waterFogSettings.fogColor", profile.fogColor); Color(so, "waterFogSettings.fogExtinction", profile.fogExtinction);
                Float(so, "waterFogSettings.fogDensity", profile.fogDensity);
                Bool(so, "depthAttenuation.depthDarken", true); Bool(so, "depthAttenuation.linkDepthToFog", false);
                Color(so, "depthAttenuation.depthExtinction", profile.depthExtinction); Float(so, "depthAttenuation.depthDarkenStrength", profile.depthStrength);
                Float(so, "depthAttenuation.minimumDepthLight", profile.minimumDepthLight);
                Float(so, "depthAttenuation.screenCausticIntensity", profile.causticIntensity);
                Float(so, "depthAttenuation.causticDepthFade", profile.causticDepthFade); Float(so, "depthAttenuation.godRayDepthFade", profile.causticDepthFade);
                Bool(so, "volumeScatterSettings.volumeScatter", profile.volumeScatter);
                Color(so, "volumeScatterSettings.scatterColor", profile.fogColor);
                Float(so, "volumeScatterSettings.scatterAmbientTerm", profile.scatterAmbient); Float(so, "volumeScatterSettings.scatterSunTerm", profile.scatterSun);
                Float(so, "volumeScatterSettings.scatterIntensity", profile.scatterIntensity); so.ApplyModifiedProperties();
            }
            foreach (var lamp in roots.SelectMany(r => r.GetComponentsInChildren<SonarFogLantern>(true)))
            {
                var so = new SerializedObject(lamp); Float(so, "radius", profile.headlampRadius); Float(so, "forwardOffset", profile.headlampOffset);
                Float(so, "edgeFadeWidth", profile.headlampEdge); so.ApplyModifiedProperties();
            }
            foreach (var lamp in roots.SelectMany(r => r.GetComponentsInChildren<GrabFlashlight>(true)))
            { var so = new SerializedObject(lamp); Float(so, "range", profile.flashlightRange); so.ApplyModifiedProperties(); }
            foreach (var action in roots.SelectMany(r => r.GetComponentsInChildren<DemoWorldAction>(true)))
                if (new[] { "log01", "log02", "log03", "log04" }.Contains(action.fact))
                { action.titleId = action.fact + ".title"; action.bodyId = action.fact + ".body"; action.title = text.Resolve(action.titleId); action.body = text.Resolve(action.bodyId); }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[DeepSeaDemo] Deep-water profile applied to copy only. No global exposure, sun, material or original scene settings changed. Save after visual review.");
        }
        static SerializedProperty Property(SerializedObject so, string path) => so.FindProperty(path) ?? throw new InvalidOperationException("Water plugin field missing: " + path);
        static void Float(SerializedObject so, string path, float value) => Property(so, path).floatValue = value;
        static void Bool(SerializedObject so, string path, bool value) => Property(so, path).boolValue = value;
        static void Color(SerializedObject so, string path, UnityEngine.Color value) => Property(so, path).colorValue = value;
    }
}
#endif
