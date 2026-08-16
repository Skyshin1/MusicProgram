using Crest;
using UnityEditor;
using UnityEngine;

namespace MusicProgram.CrestURP.Editor
{
    [CustomEditor(typeof(CrestURPWaveController))]
    public sealed class CrestURPWaveControllerEditor : UnityEditor.Editor
    {
        bool _showSpectrum = true;

        public override void OnInspectorGUI()
        {
            var controller = (CrestURPWaveController)target;
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Authoring", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Selected Preset"))
                {
                    Undo.RecordObject(controller, "Apply Crest Sea Preset");
                    controller.ApplySelectedPreset();
                    EditorUtility.SetDirty(controller);
                }
                if (GUILayout.Button("Capture Scene Values"))
                {
                    Undo.RecordObject(controller, "Capture Crest Sea State");
                    controller.CaptureCurrentSettings();
                    EditorUtility.SetDirty(controller);
                }
                if (GUILayout.Button("Apply Now")) controller.ApplySettings(true);
            }

            var spectrum = controller.spectrum;
            if (spectrum == null) return;

            _showSpectrum = EditorGUILayout.Foldout(_showSpectrum,
                "Detailed 14-Band Spectrum (0.0625 m – 1024 m)", true);
            if (!_showSpectrum) return;

            var serializedSpectrum = new SerializedObject(spectrum);
            serializedSpectrum.Update();
            var powers = serializedSpectrum.FindProperty("_powerLog");
            var disabled = serializedSpectrum.FindProperty("_powerDisabled");
            var chops = serializedSpectrum.FindProperty("_chopScales");
            var speeds = serializedSpectrum.FindProperty("_gravityScales");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.HelpBox(
                "Energy is log10 spectral power. Each row independently controls energy, horizontal chop and propagation speed for its wavelength octave.",
                MessageType.Info);
            for (var i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
            {
                var wavelength = OceanWaveSpectrum.SmallWavelength(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var enabled = !disabled.GetArrayElementAtIndex(i).boolValue;
                        enabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(18f));
                        disabled.GetArrayElementAtIndex(i).boolValue = !enabled;
                        EditorGUILayout.LabelField($"{wavelength:0.####}–{wavelength * 2f:0.####} m", GUILayout.Width(125f));
                        EditorGUILayout.Slider(powers.GetArrayElementAtIndex(i),
                            OceanWaveSpectrum.MIN_POWER_LOG, OceanWaveSpectrum.MAX_POWER_LOG, "Energy log10");
                    }
                    using (new EditorGUI.DisabledScope(disabled.GetArrayElementAtIndex(i).boolValue))
                    {
                        EditorGUILayout.Slider(chops.GetArrayElementAtIndex(i), 0f, 4f, "Chop scale");
                        EditorGUILayout.Slider(speeds.GetArrayElementAtIndex(i), 0f, 4f, "Speed scale");
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedSpectrum.ApplyModifiedProperties();
                EditorUtility.SetDirty(spectrum);
                controller.ApplySettings(true);
            }

            if (GUILayout.Button("Select Spectrum Asset")) Selection.activeObject = spectrum;
        }
    }
}
