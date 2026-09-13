#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OilRigAssembly.Editor
{
    [InitializeOnLoad]
    public static class OilRigDetailReview
    {
        const string PathName = "Assets/Prefab/__OilRigGenerated.prefab";
        const string Output = "Tools/OilRigDetailReport";
        static OilRigDetailReview() { EditorApplication.delayCall += ReviewOnce; }
        static void ReviewOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += ReviewOnce; return; }
            if (File.Exists(Output + "/unity-review.txt")) return;
            Review();
        }
        [MenuItem("Tools/Oil Rig Assembly/Review Detailed Platform Prefab")]
        public static void Review()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject root = PrefabUtility.LoadPrefabContents(PathName);
            try
            {
                Transform detail = root.transform.Find("07_SierraDivision_DetailPass");
                if (detail == null) throw new Exception("Detail group did not import");
                var report = new StringBuilder(); int count = 0;
                foreach (Transform zone in detail)
                foreach (Transform item in zone)
                {
                    var renderers = item.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0 || renderers.Any(r => r.sharedMaterials.Any(m => m == null)))
                        throw new Exception("Missing renderer/material: " + item.name);
                    Bounds b = renderers[0].bounds;
                    foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
                    report.AppendLine(item.name + " bounds=" + b);
                    count++;
                }
                Directory.CreateDirectory(Output);
                Capture(root, "overview", new Vector3(-37, 29, -35), new Vector3(0, 8, 0));
                Capture(root, "monitoring-interior", new Vector3(-15.8f, 7.65f, 3.5f), new Vector3(-13.5f, 7, 8.7f));
                Capture(root, "preparation-interior", new Vector3(-11.2f, 7.65f, -3.2f), new Vector3(-15.5f, 6.7f, -8.5f));
                File.WriteAllText(Output + "/unity-review.txt", "PASS " + count + " imported nested prefab renderers/materials.\n" + report);
                Debug.Log("[OilRig] Detailed platform prefab verified; previews in " + Output);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void Capture(GameObject root, string name, Vector3 position, Vector3 target)
        {
            var cameraObject = new GameObject("Prefab Review Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = position; camera.transform.LookAt(target);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.09f, .13f, .17f);
            camera.nearClipPlane = .05f; camera.farClipPlane = 200; camera.fieldOfView = 65;
            var lampObject = new GameObject("Prefab Review Light");
            SceneManager.MoveGameObjectToScene(lampObject, root.scene);
            var light = lampObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(40, -30, 0);
            var rt = new RenderTexture(1400, 1000, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); texture.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(lampObject);
            }
        }
    }
}
#endif
