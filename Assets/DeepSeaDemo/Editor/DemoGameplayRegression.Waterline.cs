#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    internal static partial class DemoGameplayRegression
    {
        static readonly float[] crossingHeights = { .3f, .06f, .01f, -.01f, -.06f, -.3f, -2f };
        static float crossingSurface;
        public static void BeginWaterlinePlay()
        {
            SessionState.SetBool(PlayKey + "WaterlineOptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(PlayKey + "WaterlineOptions", (int)EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
            SessionState.SetBool(PlayKey + "Waterline", true);
            BeginPlay();
        }
        static void RestoreWaterlineOptions()
        {
            if (!SessionState.GetBool(PlayKey + "Waterline", false)) return;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(PlayKey + "WaterlineOptions", 0);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(PlayKey + "WaterlineOptionsEnabled", false);
        }
        static void TickWaterline()
        {
            next = EditorApplication.timeSinceStartup + .4;
            if (step == 0)
            {
                playFlow = Object.FindFirstObjectByType<DemoFlow>();
                playFlow.suppressSaveForTests = true;
                playFlow.NewGame(); playFlow.SetPaused(true); playFlow.ui.ShowHUD();
                crossingSurface = Shader.GetGlobalFloat("_UnderwaterSurfaceY");
                foreach (float height in new[] { 5f, -12f })
                {
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    probe.GetComponent<Collider>().enabled = false;
                    probe.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
                    probe.transform.position = new Vector3(0, crossingSurface + height, 12);
                    probe.transform.localScale = new Vector3(8, .1f, 8);
                }
                step++; return;
            }
            int index = (step - 1) / 3;
            if (index >= crossingHeights.Length * 2)
            {
                File.WriteAllText("Logs/DeepSeaDemo/Waterline-Play.txt", playReport.ToString());
                FinishPlay(); return;
            }
            bool up = index < crossingHeights.Length;
            float heightOffset = crossingHeights[index % crossingHeights.Length];
            string label = (up ? "Up" : "Down") + (index % crossingHeights.Length);
            string path = "Logs/DeepSeaDemo/Waterline-" + label + ".png";
            int phase = (step - 1) % 3;
            if (phase == 0)
            {
                var camera = playFlow.player.Camera.transform;
                playFlow.player.transform.position += new Vector3(0, crossingSurface + heightOffset, 12) - camera.position;
                camera.rotation = Quaternion.LookRotation(up ? Vector3.up : Vector3.down, Vector3.forward);
            }
            else if (phase == 1) ScreenCapture.CaptureScreenshot(path);
            else
            {
                var tex = new Texture2D(2, 2); tex.LoadImage(File.ReadAllBytes(path));
                float luma = 0;
                for (int x = 0; x < 5; x++) for (int y = 0; y < 5; y++)
                    luma += tex.GetPixel((int)(tex.width * (.47f + x * .015f)), (int)(tex.height * (.47f + y * .015f))).grayscale / 25;
                playReport.AppendLine($"{label} offset={heightOffset} luma={luma} camera={playFlow.player.Camera.transform.position} surface={Shader.GetGlobalFloat("_UnderwaterSurfaceY")}");
                Object.DestroyImmediate(tex);
            }
            step++;
        }
    }
}
#endif
