#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    internal static class DemoGloveWristRepair
    {
        [MenuItem("Tools/Deep Sea Demo/Input/Generate Stable Glove Wrist Meshes")]
        public static void Generate()
        {
            const string folder = "Assets/DeepSeaDemo/Resources/DeepSeaDemo";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var report = new List<string>();
            foreach (bool right in new[] { true, false })
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeepSeaDemo/Prefabs/LeatherGlove" + (right ? "Right" : "Left") + ".prefab");
                // Instantiate disabled, so regeneration always starts from supplier data.
                var parent = new GameObject("Temporary glove binding"); parent.SetActive(false);
                try
                {
                    var hand = Object.Instantiate(source, parent.transform);
                    var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    SkinnedMeshRenderer referenceSkin = null;
                    if (!right)
                    {
                        // The left export dropped the wrist weights across the entire
                        // palm. Recover them from the geometrically matching right glove.
                        parent.AddComponent<DemoXRInput>();
                        var controller = new GameObject("Right reference"); controller.transform.SetParent(parent.transform, false);
                        var reference = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeepSeaDemo/Prefabs/LeatherGloveRight.prefab"), controller.transform);
                        referenceSkin = reference.GetComponentInChildren<SkinnedMeshRenderer>(true);
                        var byName = hand.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
                        typeof(DemoHandAnimator).GetMethod("AlignLeftPalm", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hand.GetComponent<DemoHandAnimator>(), new object[] { byName });
                    }
                    var wrist = hand.GetComponentsInChildren<Transform>(true).First(t => t.name == (right ? "hand_r" : "hand_l"));
                    var middle = hand.GetComponentsInChildren<Transform>(true).First(t => t.name == (right ? "middle_01_r" : "middle_01_l"));
                    var original = skin.sharedMesh;
                    var mesh = Object.Instantiate(original);
                    mesh.name = "LeatherGlove" + (right ? "Right" : "Left") + "Wrist";
                    var bones = skin.bones.ToList();
                    int wristIndex = bones.IndexOf(wrist);
                    if (wristIndex < 0) { wristIndex = bones.Count; bones.Add(wrist); }
                    var world = DemoModelRegression.WorldVertices(skin);
                    var positions = world.Select(v => skin.transform.InverseTransformPoint(v)).ToArray();
                    var weights = original.boneWeights;
                    var originalWeights = original.boneWeights;
                    var referenceWorld = referenceSkin != null ? DemoModelRegression.WorldVertices(referenceSkin).Select(v => new Vector3(-v.x, v.y, v.z)).ToArray() : null;
                    var referenceWeights = referenceSkin != null ? AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/LeatherGloveRightWrist.asset").boneWeights : null;
                    var referenceBones = referenceSkin != null ? referenceSkin.bones.Select(b => bones.FindIndex(t => t.name == b.name.Replace("_r", "_l"))).ToArray() : null;
                    var matrices = skin.bones.Select((b, i) => skin.transform.worldToLocalMatrix * b.localToWorldMatrix * original.bindposes[i]).ToArray();
                    var normals = original.normals; var tangents = original.tangents;
                    var forward = (middle.position - wrist.position).normalized;
                    float fadeDistance = Vector3.Distance(middle.position, wrist.position) * .3f;
                    int changed = 0;
                    for (int i = 0; i < weights.Length; i++)
                    {
                        var w = originalWeights[i];
                        var ids = new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                        var values = new[] { w.weight0, w.weight1, w.weight2, w.weight3 };
                        if (normals.Length == positions.Length)
                        {
                            Vector3 n = Vector3.zero;
                            for (int j = 0; j < 4; j++) if (values[j] > 0) n += matrices[ids[j]].inverse.transpose.MultiplyVector(normals[i]) * values[j];
                            normals[i] = n.normalized;
                        }
                        if (tangents.Length == positions.Length)
                        {
                            Vector3 t = Vector3.zero;
                            for (int j = 0; j < 4; j++) if (values[j] > 0) t += matrices[ids[j]].MultiplyVector((Vector3)tangents[i]) * values[j];
                            t.Normalize(); tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                        }
                        if (referenceWorld != null)
                        {
                            int nearest = 0; float distance = float.PositiveInfinity;
                            for (int j = 0; j < referenceWorld.Length; j++)
                            {
                                float d = (referenceWorld[j] - world[i]).sqrMagnitude;
                                if (d < distance) { nearest = j; distance = d; }
                            }
                            if (distance > .001f * .001f) throw new InvalidOperationException("Left/right glove geometry mismatch: " + Mathf.Sqrt(distance));
                            var rw = referenceWeights[nearest];
                            rw.boneIndex0 = referenceBones[rw.boneIndex0]; rw.boneIndex1 = referenceBones[rw.boneIndex1];
                            rw.boneIndex2 = referenceBones[rw.boneIndex2]; rw.boneIndex3 = referenceBones[rw.boneIndex3];
                            if (new[] { rw.boneIndex0, rw.boneIndex1, rw.boneIndex2, rw.boneIndex3 }.Any(id => id < 0)) throw new InvalidOperationException("Missing mirrored glove bone");
                            weights[i] = rw; changed++; continue;
                        }
                        // Cuff is rigid; smoothly restore supplier weights across the
                        // first 30% of the palm. Finger/knuckle weights stay untouched.
                        float amount = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(Vector3.Dot(world[i] - wrist.position, forward) / fadeDistance));
                        if (amount <= 0) continue;
                        changed++;
                        var merged = new Dictionary<int, float>();
                        for (int j = 0; j < 4; j++) if (values[j] > 0)
                            merged[ids[j]] = (merged.TryGetValue(ids[j], out var old) ? old : 0) + values[j] * (1 - amount);
                        merged[wristIndex] = (merged.TryGetValue(wristIndex, out float existing) ? existing : 0) + amount;
                        var top = merged.OrderByDescending(p => p.Value).Take(4).ToArray();
                        float sum = top.Sum(p => p.Value); var result = new BoneWeight();
                        for (int j = 0; j < top.Length; j++)
                        {
                            int id = top[j].Key; float value = top[j].Value / sum;
                            if (j == 0) { result.boneIndex0 = id; result.weight0 = value; }
                            if (j == 1) { result.boneIndex1 = id; result.weight1 = value; }
                            if (j == 2) { result.boneIndex2 = id; result.weight2 = value; }
                            if (j == 3) { result.boneIndex3 = id; result.weight3 = value; }
                        }
                        weights[i] = result;
                    }
                    mesh.vertices = positions; mesh.normals = normals; mesh.tangents = tangents;
                    mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * skin.transform.localToWorldMatrix).ToArray();
                    mesh.boneWeights = weights; mesh.RecalculateBounds();
                    string path = folder + "/" + mesh.name + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) AssetDatabase.CreateAsset(mesh, path);
                    else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                    AssetDatabase.SaveAssetIfDirty(saved != null ? saved : mesh);
                    report.Add((right ? "Right" : "Left") + ": stabilized " + changed + " vertices; wrist bone index=" + wristIndex);
                }
                finally { Object.DestroyImmediate(parent); }
            }
            File.WriteAllLines("Logs/DeepSeaDemo/Wrist-Generation.txt", report);
        }
    }
}
#endif
