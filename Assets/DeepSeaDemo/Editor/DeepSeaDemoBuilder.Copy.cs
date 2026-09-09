#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    public static partial class DeepSeaDemoBuilder
    {
        public const string Source1VR = "Assets/Scenes/1-VR.unity";
        public const string Copy1VR = Root + "/Scenes/DeepSeaInvestigation_1VR.unity";
        static string HashFile(string path)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }

        [MenuItem("Tools/Deep Sea Demo/10 Copy saved 1-VR (non-destructive)")]
        public static void Prepare1VRCopy()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or back up all open scenes before cloning. No changes made.");
            Directory.CreateDirectory(Root + "/Reports"); Directory.CreateDirectory(Root + "/Settings/1VR");
            string before = HashFile(Source1VR);
            if (!File.Exists(Copy1VR) && !AssetDatabase.CopyAsset(Source1VR, Copy1VR))
                throw new IOException("Cannot copy saved source scene");
            var previous = SceneManager.GetActiveScene();
            var copy = SceneManager.GetSceneByPath(Copy1VR);
            bool opened = !copy.isLoaded;
            if (opened) copy = EditorSceneManager.OpenScene(Copy1VR, OpenSceneMode.Additive);
            var report = new List<string> { "Source SHA256: " + before, "Copy: " + Copy1VR, "No player scripts or scene objects removed." };
            try
            {
                SceneManager.SetActiveScene(copy);
                var assetCopies = new Dictionary<Object, Object>();
                foreach (var root in copy.GetRootGameObjects())
                {
                    report.Add("ROOT " + root.name + " position=" + root.transform.position + " scale=" + root.transform.lossyScale + " active=" + root.activeSelf);
                    foreach (var t in root.GetComponentsInChildren<Terrain>(true))
                    {
                        if (t.terrainData == null) continue;
                        var data = Clone1VRAsset(t.terrainData, assetCopies) as TerrainData; t.terrainData = data;
                        var col = t.GetComponent<TerrainCollider>(); if (col != null) col.terrainData = data;
                        report.Add("TERRAIN " + t.name + " size=" + data.size + " pos=" + t.transform.position);
                    }
                    foreach (var v in root.GetComponentsInChildren<Volume>(true))
                        if (v.sharedProfile != null) v.sharedProfile = Clone1VRAsset(v.sharedProfile, assetCopies) as VolumeProfile;
                    foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (b == null) { report.Add("MISSING SCRIPT on " + root.name); continue; }
                        if (b is AbstractOcclusion.WebGpuWater.WaterVolume || b is Unity.XR.CoreUtils.XROrigin || b is QuestLeftStickLocomotion)
                            report.Add("COMPONENT " + b.GetType().Name + " " + b.transform.position + "\n" + EditorJsonUtility.ToJson(b, true));
                    }
                    if (root.name.IndexOf("sub", StringComparison.OrdinalIgnoreCase) >= 0 || root.name.IndexOf("rig", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        report.Add("BOUNDS " + root.name + " " + BoundsOf(root));
                        foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Door")) report.Add("DOOR " + BoundsOf(t.gameObject));
                    }
                }
                EditorSceneManager.SaveScene(copy);
                if (before != HashFile(Source1VR)) throw new InvalidOperationException("Source hash changed unexpectedly.");
                report.Add("PASS: saved source scene hash unchanged.");
                File.WriteAllLines(Root + "/Reports/1VR-Baseline.txt", report);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(copy, true);
            }
        }
        static Object Clone1VRAsset(Object original, Dictionary<Object, Object> cache)
        {
            if (cache.TryGetValue(original, out var known)) return known;
            string source = AssetDatabase.GetAssetPath(original);
            if (source.StartsWith(Root + "/Settings/1VR/")) return original;
            string path = Root + "/Settings/1VR/" + AssetDatabase.AssetPathToGUID(source) + Path.GetExtension(source);
            if (!File.Exists(path) && !AssetDatabase.CopyAsset(source, path)) throw new IOException("Cannot isolate " + source);
            var result = AssetDatabase.LoadAssetAtPath(path, original.GetType()); cache[original] = result; return result;
        }
    }
}
#endif
