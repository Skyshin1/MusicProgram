using System.Collections.Generic;
using MK.Toon;
using UnityEngine;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    public sealed class SonicMKToonWorldDriver : MonoBehaviour
    {
        private sealed class MaterialState
        {
            public Material Original;
            public Material Material;
            public float OutlineSize;
            public float OutlineNoise;
            public float RimSize;
            public float VertexIntensity;
            public Vector3 VertexFrequency;
            public float LightBandsScale;
            public float LightThreshold;
            public SonicMKToonTarget Target;
        }

        private sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] Originals;
        }

        private readonly List<MaterialState> materials = new List<MaterialState>();
        private readonly List<RendererState> renderers = new List<RendererState>();

        private void Start()
        {
            BuildRuntimeMaterials();
        }

        private void LateUpdate()
        {
            if (SonicAudioBus.Instance == null)
                return;

            SonicAudioBus.Frame frame = SonicAudioBus.Instance.Current;
            foreach (MaterialState state in materials)
                ApplyFrame(state, frame);
        }

        private void OnDestroy()
        {
            foreach (RendererState state in renderers)
            {
                if (state.Renderer != null)
                    state.Renderer.sharedMaterials = state.Originals;
            }

            foreach (MaterialState state in materials)
            {
                if (state.Material != null)
                    Destroy(state.Material);
            }
        }

        private void BuildRuntimeMaterials()
        {
            SonicMKToonTarget[] targets =
                FindObjectsByType<SonicMKToonTarget>(FindObjectsSortMode.None);
            foreach (SonicMKToonTarget target in targets)
            {
                Renderer renderer = target.TargetRenderer;
                if (renderer == null)
                    continue;

                Material[] originals = renderer.sharedMaterials;
                Material[] runtimeMaterials = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    Material original = originals[i];
                    if (original == null ||
                        original.shader == null ||
                        !original.shader.name.StartsWith("MK/Toon"))
                    {
                        runtimeMaterials[i] = original;
                        continue;
                    }

                    MaterialState sharedState = FindSharedState(original, target);
                    if (sharedState == null)
                    {
                        Material runtime = new Material(original)
                        {
                            name = original.name + " (Sonic Runtime)",
                            hideFlags = HideFlags.DontSave
                        };
                        sharedState = Capture(original, runtime, target);
                        materials.Add(sharedState);
                    }
                    runtimeMaterials[i] = sharedState.Material;
                }

                renderer.sharedMaterials = runtimeMaterials;
                renderers.Add(new RendererState
                {
                    Renderer = renderer,
                    Originals = originals
                });
            }
        }

        private MaterialState FindSharedState(
            Material original,
            SonicMKToonTarget target)
        {
            foreach (MaterialState state in materials)
            {
                SonicMKToonTarget existing = state.Target;
                if (state.Original == original &&
                    Mathf.Approximately(existing.Emission, target.Emission) &&
                    Mathf.Approximately(existing.Outline, target.Outline) &&
                    Mathf.Approximately(existing.Rim, target.Rim) &&
                    Mathf.Approximately(existing.Iridescence, target.Iridescence) &&
                    Mathf.Approximately(
                        existing.VertexAnimation,
                        target.VertexAnimation))
                {
                    return state;
                }
            }
            return null;
        }

        private static MaterialState Capture(
            Material original,
            Material material,
            SonicMKToonTarget target)
        {
            return new MaterialState
            {
                Original = original,
                Material = material,
                Target = target,
                OutlineSize = Properties.outlineSize.GetValue(material),
                OutlineNoise = Properties.outlineNoise.GetValue(material),
                RimSize = Properties.rimSize.GetValue(material),
                VertexIntensity = Properties.vertexAnimationIntensity.GetValue(material),
                VertexFrequency = Properties.vertexAnimationFrequency.GetValue(material),
                LightBandsScale = Properties.lightBandsScale.GetValue(material),
                LightThreshold = Properties.lightThreshold.GetValue(material)
            };
        }

        private static void ApplyFrame(MaterialState state, SonicAudioBus.Frame frame)
        {
            Material material = state.Material;
            SonicMKToonTarget target = state.Target;
            float pulse = frame.Pulse;

            Properties.outlineSize.SetValue(
                material,
                state.OutlineSize + (frame.Loudness * 0.65f + pulse * 0.5f) * target.Outline);
            material.SetFloat(
                Properties.outlineNoise.uniform.id,
                Mathf.Clamp(state.OutlineNoise + frame.Treble * 0.12f, -1f, 1f));
            Properties.rimSize.SetValue(
                material,
                Mathf.Clamp01(state.RimSize + frame.Mid * 0.14f * target.Rim));
            Properties.vertexAnimationIntensity.SetValue(
                material,
                Mathf.Clamp(
                    state.VertexIntensity +
                    (frame.Bass * 0.055f + pulse * 0.045f) * target.VertexAnimation,
                    0f,
                    0.16f));
            Properties.vertexAnimationFrequency.SetValue(
                material,
                state.VertexFrequency + new Vector3(frame.Bass, frame.Mid, frame.Treble) * 0.85f);
            Properties.lightBandsScale.SetValue(
                material,
                Mathf.Clamp01(state.LightBandsScale + frame.Bass * 0.16f));
            Properties.lightThreshold.SetValue(
                material,
                Mathf.Clamp01(state.LightThreshold + (frame.Mid - frame.Bass) * 0.08f));
        }
    }
}
