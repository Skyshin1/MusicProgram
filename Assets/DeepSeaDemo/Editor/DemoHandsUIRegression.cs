#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace DeepSeaDemo.Editor
{
    internal static class DemoHandsUIRegression
    {
        public static void RunSonarRendering()
        {
            var sb = new StringBuilder();
            var near = new Bounds(new Vector3(10, 0, 0), Vector3.one);
            if (SonarRevealManager.BoundsTouchShell(near, Vector3.zero, 2, .3f)) throw new Exception("Distant child revealed before sweep");
            if (!SonarRevealManager.BoundsTouchShell(near, Vector3.zero, 10, .3f)) throw new Exception("Swept child missed");
            if (SonarRevealManager.BoundsTouchShell(near, Vector3.zero, 20, .3f)) throw new Exception("Passed shell falsely hits child");
            if (!SonarRevealManager.BoundsTouchShell(new Bounds(Vector3.zero, Vector3.one * 20), Vector3.zero, 5, .3f)) throw new Exception("Enclosing bounds missed");
            sb.AppendLine("PASS renderer shell bounds: before / crossing / after / enclosing origin.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SonicWorld/Shaders/SonarWhiteOutline.shader");
            if (shader == null) throw new Exception("Outline shader missing");
            foreach (var keywords in new[] { Array.Empty<string>(), new[] { "STEREO_INSTANCING_ON" }, new[] { "INSTANCING_ON", "STEREO_INSTANCING_ON" } })
            {
                var material = new Material(shader) { enableInstancing = true };
                try
                {
                    foreach (var keyword in keywords) material.EnableKeyword(keyword);
                    ShaderUtil.CompilePass(material, 0, true);
                    var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error").ToArray();
                    if (errors.Length > 0) throw new Exception(string.Join("\n", errors.Select(e => e.message)));
                    sb.AppendLine("PASS requested shader compilation: " + (keywords.Length == 0 ? "mono" : string.Join(",", keywords)));
                }
                finally { UnityEngine.Object.DestroyImmediate(material); }
            }
            sb.AppendLine("NOT TESTED: live RenderGraph execution, Quest eye images or GPU/frame-rate performance.");
            Directory.CreateDirectory("Logs/DeepSeaDemo");
            File.WriteAllText("Logs/DeepSeaDemo/Sonar-Render-Regression.txt", sb.ToString());
        }
        public static void RunSonar()
        {
            var gate = typeof(DemoInputRouter).GetMethod("SonarBlockReason", BindingFlags.Static | BindingFlags.NonPublic);
            var cases = new[] {
                (new[] { true, false, false, true, false, false, false }, (string)null),
                (new[] { true, false, false, false, false, false, false }, "sonar.surface"),
                (new[] { true, false, false, true, true, false, false }, "sonar.holding"),
                (new[] { true, true, false, true, false, false, false }, "sonar.paused"),
                (new[] { false, false, false, true, false, false, false }, "sonar.paused"),
                (new[] { true, false, true, true, false, false, false }, "sonar.paused"),
                (new[] { true, false, false, true, false, true, false }, "sonar.qte"),
                (new[] { true, false, false, true, false, false, true }, "sonar.cooldown")
            };
            var sb = new StringBuilder();
            foreach (var test in cases)
            {
                string actual = (string)gate.Invoke(null, test.Item1.Cast<object>().ToArray());
                if (actual != test.Item2) throw new Exception("Sonar gate regression: " + actual);
                sb.AppendLine("PASS " + (actual ?? "empty hand underwater allowed without a suit-task dependency"));
            }
            var catalog = ScriptableObject.CreateInstance<DemoTextCatalog>();
            try
            {
                catalog.entries = Array.Empty<DemoTextCatalog.Entry>();
                if (catalog.Resolve("sonar.sent") == "sonar.sent") throw new Exception("Old catalog fallback missing");
                sb.AppendLine("PASS pre-existing catalog resolves new English feedback.");
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
            sb.AppendLine("NOT TESTED: hardware Trigger, water classification, rendered sonar and AI reaction in Play.");
            Directory.CreateDirectory("Logs/DeepSeaDemo");
            File.WriteAllText("Logs/DeepSeaDemo/Sonar-Gate-Regression.txt", sb.ToString());
        }
        [MenuItem("Tools/Deep Sea Demo/Input/Test Glove Calibration (Isolated)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            var scene = EditorSceneManager.NewPreviewScene();
            var sb = new StringBuilder();
            try
            {
                var root = new GameObject("Isolated hand regression"); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene); root.AddComponent<DemoXRInput>();
                var lc = new GameObject("left"); lc.transform.SetParent(root.transform, false);
                var rc = new GameObject("Right"); rc.transform.SetParent(root.transform, false);
                var left = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeepSeaDemo/Prefabs/LeatherGloveLeft.prefab"), lc.transform).GetComponent<DemoHandAnimator>();
                var right = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeepSeaDemo/Prefabs/LeatherGloveRight.prefab"), rc.transform).GetComponent<DemoHandAnimator>();
                var awake = typeof(DemoHandAnimator).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                awake.Invoke(right, null); awake.Invoke(left, null);
                Transform Bone(DemoHandAnimator hand, string name) => hand.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
                Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);
                foreach (string name in new[] { "hand", "middle_01", "index_01", "pinky_01" })
                {
                    var actual = lc.transform.InverseTransformPoint(Bone(left, name + "_l").position);
                    var expected = Mirror(rc.transform.InverseTransformPoint(Bone(right, name + "_r").position));
                    float error = Vector3.Distance(actual, expected);
                    if (error > .002f) throw new Exception(name + " mirrored landmark error " + error);
                    sb.AppendLine("PASS " + name + " mirrored landmark error: " + error.ToString("F6") + " m");
                }
                var dorsal = Vector3.Cross(Bone(left, "middle_01_l").position - Bone(left, "hand_l").position,
                    Bone(left, "index_01_l").position - Bone(left, "pinky_01_l").position).normalized;
                var axes = (Vector3[])typeof(DemoHandAnimator).GetField("bendAxes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(left);
                for (int i = 0; i < left.poses.joints.Length; i++)
                {
                    var pose = left.poses.joints[i]; var bone = Bone(left, pose.name);
                    var old = bone.localRotation;
                    var along = bone.childCount > 0 ? bone.GetChild(0).position - bone.position : bone.position - bone.parent.position;
                    var tip = bone.InverseTransformVector(along);
                    var before = bone.TransformVector(tip);
                    bone.localRotation = old * Quaternion.AngleAxis(Mathf.Sign(pose.curlDegrees), axes[i]);
                    var delta = bone.TransformVector(tip) - before; bone.localRotation = old;
                    float inward = Vector3.Dot(delta.normalized, -dorsal);
                    if (inward < .1f) throw new Exception(pose.name + " bends away from palm: " + inward);
                    sb.AppendLine("PASS " + pose.name + " initial inward bend: " + inward.ToString("F3"));
                }
                // Exercise the runtime repair, not just a duplicate bitmask formula.
                var caster = lc.AddComponent<CurveInteractionCaster>(); caster.raycastMask = 1792;
                var router = root.AddComponent<DemoInputRouter>();
                var cfg = ScriptableObject.CreateInstance<DemoConfig>(); cfg.uiMask = 2048; router.config = cfg;
                try
                {
                    typeof(DemoInputRouter).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(router, null);
                    if (caster.raycastMask.value != 3840) throw new Exception("UI mask repair failed: " + caster.raycastMask.value);
                }
                finally { UnityEngine.Object.DestroyImmediate(cfg); }
                sb.AppendLine("PASS UI ray mask 1792 -> 3840 includes UI layer 11; retains existing world/prop layers.");
                sb.AppendLine("NOT TESTED: headset comfort/hand fit, physical Trigger click, locomotion and two-eye rendering.");
            }
            catch (Exception ex) { sb.AppendLine("FAIL " + ex); throw; }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Directory.CreateDirectory("Logs/DeepSeaDemo");
                File.WriteAllText("Logs/DeepSeaDemo/Hands-UI-Regression.txt", sb.ToString());
            }
            Debug.Log("[DeepSeaDemo] Isolated glove/UI-mask regression passed. No user scene changed.");
        }
    }
}
#endif
