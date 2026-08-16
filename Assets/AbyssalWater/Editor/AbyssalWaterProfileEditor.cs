using UnityEditor;
using UnityEngine;

namespace MusicProgram.AbyssalWater.Editor
{
    [CustomEditor(typeof(AbyssalWaterProfile))]
    public sealed class AbyssalWaterProfileEditor : UnityEditor.Editor
    {
        static readonly Color HeaderColour = new Color(0.18f, 0.65f, 0.82f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var profile = (AbyssalWaterProfile)target;

            EditorGUILayout.HelpBox(
                "Simple 模式只保留最常用控制；Advanced 展开后包含视频中演示的波谱、光学、泡沫、焦散、水下和性能参数。",
                MessageType.Info);

            DrawQualityButtons(profile);
            SpaceHeader("Simple — 日常调水");
            Draw("waveHeight", "波浪高度");
            Draw("waveScale", "波浪尺度");
            Draw("waveSpeed", "波浪速度");
            Draw("windDirection", "主风向");
            Draw("windSpeed", "风速");
            Draw("choppiness", "浪尖陡峭度");
            Draw("enableAntiTiling", "算法去重复");
            Draw("microWaveAmplitude", "表面微波细节");
            Draw("transmittanceColor", "水体透射颜色");
            Draw("transmittanceReferenceDistance", "透射颜色参考距离");
            Draw("reflectionStrength", "反射强度");
            Draw("causticIntensity", "物理焦散强度");
            Draw("foamStrength", "泡沫强度");
            Draw("underwaterFogMultiplier", "水下吸收/雾倍率");

            EditorGUILayout.Space(6);
            var advanced = serializedObject.FindProperty("showAdvanced");
            advanced.boolValue = EditorGUILayout.Foldout(advanced.boolValue,
                "Advanced — 完整可定制参数", true, EditorStyles.foldoutHeader);
            if (advanced.boolValue)
            {
                SpaceHeader("波谱与曲线");
                Draw("spectrumBands", "波谱频段数量");
                Draw("minimumWavelength", "最短波长");
                Draw("maximumWavelength", "最长波长");
                Draw("directionSpread", "风向离散角");
                Draw("spectrumSeed", "波谱随机种子");
                Draw("amplitudeByWavelength", "波长 → 振幅曲线");
                Draw("directionSpreadByWavelength", "波长 → 方向离散曲线");
                Draw("manualWaves", "手动附加波");

                SpaceHeader("算法去重复 / Anti-Tiling");
                Draw("phaseWarpStrength", "连续相位扰动强度");
                Draw("phaseWarpPatchSize", "相位变化空间尺度");
                Draw("stochasticNormalBlend", "随机旋转法线混合");
                Draw("antiTilingSeed", "去重复随机种子");
                EditorGUILayout.HelpBox(
                    "相位扰动作用于真实波面、法线和焦散；随机旋转混合只消除法线贴图的重复纹理。设为 0 可逐项关闭。",
                    MessageType.None);

                SpaceHeader("光学微波谱（表面高频与焦散）");
                Draw("enableMicroSpectrum", "启用微波谱");
                Draw("microWaveCount", "微波数量");
                Draw("microMinimumWavelength", "微波最短波长");
                Draw("microMaximumWavelength", "微波最长波长");
                Draw("microDirectionSpread", "微波方向离散角");
                Draw("microChoppiness", "微波陡峭度");
                Draw("microWaveSpeed", "微波速度");
                Draw("microSpectrumSeed", "微波随机种子");

                SpaceHeader("Beer–Lambert 与体积散射");
                Draw("scatteringColor", "散射颜色");
                Draw("scatteringStrength", "散射强度");
                Draw("scatteringAnisotropy", "散射各向异性 g");
                Draw("maximumOpticalDepth", "最大水中光程");
                var coefficient = profile.AbsorptionCoefficient;
                EditorGUILayout.HelpBox(
                    $"当前吸收系数 σa（线性空间）\nR {coefficient.x:F4}   G {coefficient.y:F4}   B {coefficient.z:F4}  m⁻¹",
                    MessageType.None);

                SpaceHeader("反射、折射与浪尖透光");
                Draw("indexOfRefraction", "折射率 IOR");
                Draw("refractionStrength", "屏幕空间折射强度");
                Draw("smoothness", "光滑度");
                Draw("normalStrength", "波面法线强度");
                Draw("crestTransmission", "浪尖透光强度");
                Draw("crestTransmissionColor", "浪尖透光颜色");
                Draw("crestTransmissionPower", "浪尖透光聚焦");

                SpaceHeader("波峰、接触、岸边泡沫与弯月面");
                Draw("foamColor", "泡沫颜色");
                Draw("crestFoamThreshold", "波峰泡沫阈值");
                Draw("crestFoamFeather", "波峰泡沫羽化");
                Draw("shorelineFoamDistance", "岸边泡沫距离");
                Draw("contactFoamStrength", "动态接触泡沫");
                Draw("meniscusWidth", "物体接触弯月面宽度");

                SpaceHeader("真实波面曲率焦散");
                Draw("causticColor", "焦散颜色");
                Draw("causticScale", "焦散光程/曲率尺度");
                Draw("causticFocus", "折射光线面积聚焦");
                Draw("causticChromaticAberration", "焦散色散");
                Draw("causticMaximumDepth", "焦散最大深度");

                SpaceHeader("吃水线与水下视角");
                Draw("underwaterDistortion", "水下折射扰动");
                Draw("waterlineThickness", "吃水线厚度");
                Draw("waterlineMeniscus", "吃水线弯月面亮度");
                Draw("enableGodRays", "水下光束");
                Draw("godRayStrength", "水下光束强度");

                SpaceHeader("近场 Dynamic Waves");
                Draw("enableDynamicWaves", "启用真实传播扰动");
                Draw("dynamicResolution", "模拟分辨率");
                Draw("dynamicWorldSize", "模拟覆盖范围");
                Draw("dynamicWaveSpeed", "传播速度");
                Draw("dynamicDamping", "衰减");
                Draw("dynamicDisplacement", "扰动高度");
                Draw("dynamicSubsteps", "每帧子步数");
                Draw("maximumImpulsesPerStep", "每步最大扰动源");

                SpaceHeader("无限海洋 LOD");
                Draw("lodLevels", "LOD 环数量");
                Draw("verticesPerLevel", "每环顶点密度");
                Draw("baseLodSize", "中心环尺寸");
                Draw("skirtDepth", "远端裙边深度");
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawQualityButtons(AbyssalWaterProfile profile)
        {
            SpaceHeader("质量预设");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("PCVR High")) SetQuality(profile, AbyssalWaterQuality.PcVrHigh);
                if (GUILayout.Button("VR Balanced")) SetQuality(profile, AbyssalWaterQuality.VrBalanced);
                if (GUILayout.Button("Quest")) SetQuality(profile, AbyssalWaterQuality.QuestStandalone);
            }
            EditorGUILayout.LabelField(
                $"当前：{profile.quality} · {profile.EffectiveWaveCount} 主波 + {profile.EffectiveMicroWaveCount} 微波 · Dynamic {profile.EffectiveDynamicResolution}²",
                EditorStyles.miniLabel);
        }

        static void SetQuality(AbyssalWaterProfile profile, AbyssalWaterQuality quality)
        {
            Undo.RecordObject(profile, "Change Abyssal Water Quality");
            profile.quality = quality;
            switch (quality)
            {
                case AbyssalWaterQuality.PcVrHigh:
                    profile.dynamicResolution = 256;
                    profile.dynamicSubsteps = 2;
                    profile.lodLevels = 6;
                    profile.verticesPerLevel = 64;
                    profile.enableAntiTiling = true;
                    profile.stochasticNormalBlend = 1f;
                    profile.enableMicroSpectrum = true;
                    profile.microWaveCount = 8;
                    break;
                case AbyssalWaterQuality.VrBalanced:
                    profile.dynamicResolution = 192;
                    profile.dynamicSubsteps = 1;
                    profile.lodLevels = 5;
                    profile.verticesPerLevel = 48;
                    profile.enableAntiTiling = true;
                    profile.stochasticNormalBlend = 0.55f;
                    profile.enableMicroSpectrum = true;
                    profile.microWaveCount = 5;
                    break;
                default:
                    profile.dynamicResolution = 96;
                    profile.dynamicSubsteps = 1;
                    profile.lodLevels = 4;
                    profile.verticesPerLevel = 32;
                    profile.enableAntiTiling = true;
                    profile.stochasticNormalBlend = 0f;
                    profile.enableMicroSpectrum = true;
                    profile.microWaveCount = 3;
                    break;
            }
            EditorUtility.SetDirty(profile);
        }

        void Draw(string propertyName, string label)
            => EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), new GUIContent(label), true);

        static void SpaceHeader(string label)
        {
            EditorGUILayout.Space(7);
            var previous = GUI.color;
            GUI.color = HeaderColour;
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            GUI.color = previous;
        }
    }
}
