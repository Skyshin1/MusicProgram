using UnityEditor;
using UnityEngine;

namespace MusicProgram.AbyssalWater.Editor
{
    [CustomEditor(typeof(AbyssalWaterSystem))]
    public sealed class AbyssalWaterSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var water = (AbyssalWaterSystem)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "常用视觉参数全部在 Profile。此组件只负责海面、观察者、动态波模拟和共享 Shader 数据。",
                MessageType.Info);
            if (water.profile != null && GUILayout.Button("选中水体 Profile"))
                Selection.activeObject = water.profile;
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("在场景中心注入测试波纹"))
                    water.EnqueueImpulse(new Vector3(0f, water.waterLevel, 0f), 2f, 1.5f);
            }
        }
    }
}
