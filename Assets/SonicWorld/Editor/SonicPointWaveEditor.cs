#if UNITY_EDITOR
using SonicWorld;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonicWorldEditor
{
    [CustomEditor(typeof(SonicPointWave))]
    public sealed class SonicPointWaveEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            SonicPointWave wave = (SonicPointWave)target;
            Transform[] points = wave.ControlPoints;

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "controlPoints");
            serializedObject.ApplyModifiedProperties();

            int count = points != null ? points.Length : 0;
            int selectedIndex = FindSelectedIndex(points);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Curve Control Points", EditorStyles.boldLabel);
            int requestedCount;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                requestedCount = EditorGUILayout.IntSlider(
                    "Point Count",
                    count,
                    SonicPointWave.MinimumControlPointCount,
                    SonicPointWave.MaximumControlPointCount);
            }

            if (!Application.isPlaying && requestedCount != count)
            {
                SetPointCount(wave, points, requestedCount);
                points = wave.ControlPoints;
                count = points != null ? points.Length : 0;
                selectedIndex = FindSelectedIndex(points);
            }

            EditorGUILayout.HelpBox(
                "Select a cyan control point in the Scene view, then Add inserts " +
                "beside it and Remove deletes it. With no selection, Add splits " +
                "the longest section.\n\nUse the Rotate tool on a point to twist " +
                "the wave surface. The magenta arrow is the wave height direction; " +
                "the cyan cross-line is the wave width direction.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(
                       Application.isPlaying ||
                       count >= SonicPointWave.MaximumControlPointCount ||
                       count < SonicPointWave.MinimumControlPointCount))
            {
                if (GUILayout.Button("Add Point"))
                    AddPoint(wave, points, selectedIndex);
            }

            using (new EditorGUI.DisabledScope(
                       Application.isPlaying ||
                       count <= SonicPointWave.MinimumControlPointCount ||
                       selectedIndex < 0))
            {
                if (GUILayout.Button("Remove Selected"))
                    RemovePoint(wave, points, selectedIndex);
            }

            if (count > SonicPointWave.MinimumControlPointCount &&
                selectedIndex < 0)
            {
                EditorGUILayout.LabelField(
                    "Select one of this wave's control points to enable removal.",
                    EditorStyles.miniLabel);
            }
        }

        private static int FindSelectedIndex(Transform[] points)
        {
            if (points == null || Selection.activeTransform == null)
                return -1;

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == Selection.activeTransform)
                    return i;
            }
            return -1;
        }

        private static void AddPoint(
            SonicPointWave wave,
            Transform[] points,
            int selectedIndex)
        {
            int segment = selectedIndex >= 0
                ? selectedIndex
                : FindLongestSegment(points, wave.ClosedLoop);
            if (!wave.ClosedLoop && selectedIndex == points.Length - 1)
                segment = points.Length - 2;

            Undo.RecordObject(wave, "Add Sonic Curve Point");
            Transform[] next = InsertPoint(wave, points, segment, out Transform created);
            wave.SetControlPoints(next);
            RenamePoints(next);
            MarkChanged(wave);
            Selection.activeGameObject = created.gameObject;
        }

        private static Transform[] InsertPoint(
            SonicPointWave wave,
            Transform[] points,
            int segment,
            out Transform created)
        {
            Transform first = points[segment];
            Transform second = points[(segment + 1) % points.Length];
            GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(point, "Add Sonic Curve Point");
            Undo.SetTransformParent(
                point.transform,
                wave.transform,
                "Parent Sonic Curve Point");

            point.layer = first.gameObject.layer;
            point.transform.localPosition =
                Vector3.Lerp(first.localPosition, second.localPosition, 0.5f);
            point.transform.localRotation =
                Quaternion.Slerp(first.localRotation, second.localRotation, 0.5f);
            point.transform.localScale =
                Vector3.Lerp(first.localScale, second.localScale, 0.5f);
            if (segment == points.Length - 1)
                point.transform.SetSiblingIndex(first.GetSiblingIndex() + 1);
            else
                point.transform.SetSiblingIndex(second.GetSiblingIndex());

            Renderer pointRenderer = point.GetComponent<Renderer>();
            Renderer sourceRenderer = first.GetComponent<Renderer>();
            pointRenderer.sharedMaterial = sourceRenderer != null
                ? sourceRenderer.sharedMaterial
                : wave.LineMaterial;
            Undo.AddComponent<SonicCurveControlPoint>(point);

            int insertionIndex = segment + 1;
            Transform[] next = new Transform[points.Length + 1];
            for (int i = 0; i < insertionIndex; i++)
                next[i] = points[i];
            next[insertionIndex] = point.transform;
            for (int i = insertionIndex; i < points.Length; i++)
                next[i + 1] = points[i];

            created = point.transform;
            return next;
        }

        private static void RemovePoint(
            SonicPointWave wave,
            Transform[] points,
            int selectedIndex)
        {
            Undo.RecordObject(wave, "Remove Sonic Curve Point");
            Transform[] next = RemovePointAt(points, selectedIndex);
            wave.SetControlPoints(next);
            RenamePoints(next);
            MarkChanged(wave);
            Selection.activeGameObject = wave.gameObject;
        }

        private static Transform[] RemovePointAt(
            Transform[] points,
            int selectedIndex)
        {
            Transform removed = points[selectedIndex];
            Transform[] next = new Transform[points.Length - 1];
            int destination = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (i != selectedIndex)
                    next[destination++] = points[i];
            }

            Undo.DestroyObjectImmediate(removed.gameObject);
            return next;
        }

        private static void SetPointCount(
            SonicPointWave wave,
            Transform[] points,
            int requestedCount)
        {
            if (points == null ||
                points.Length < SonicPointWave.MinimumControlPointCount)
                return;

            int targetCount = Mathf.Clamp(
                requestedCount,
                SonicPointWave.MinimumControlPointCount,
                SonicPointWave.MaximumControlPointCount);
            if (targetCount == points.Length)
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Change Sonic Curve Point Count");
            Undo.RecordObject(wave, "Change Sonic Curve Point Count");

            Transform[] next = points;
            while (next.Length < targetCount)
            {
                int segment = FindLongestSegment(next, wave.ClosedLoop);
                next = InsertPoint(wave, next, segment, out _);
            }

            while (next.Length > targetCount)
            {
                int index = FindLeastImportantInteriorPoint(next);
                next = RemovePointAt(next, index);
            }

            wave.SetControlPoints(next);
            RenamePoints(next);
            MarkChanged(wave);
            Selection.activeGameObject = wave.gameObject;
            Undo.CollapseUndoOperations(undoGroup);
        }

        private static int FindLongestSegment(
            Transform[] points,
            bool closedLoop)
        {
            int longest = 0;
            float longestSqrDistance = -1f;
            int segmentCount = closedLoop ? points.Length : points.Length - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                float sqrDistance =
                    (points[(i + 1) % points.Length].localPosition -
                     points[i].localPosition)
                    .sqrMagnitude;
                if (sqrDistance > longestSqrDistance)
                {
                    longestSqrDistance = sqrDistance;
                    longest = i;
                }
            }
            return longest;
        }

        private static int FindLeastImportantInteriorPoint(Transform[] points)
        {
            int bestIndex = 1;
            float bestScore = float.PositiveInfinity;
            for (int i = 1; i < points.Length - 1; i++)
            {
                Vector3 previous = points[i - 1].localPosition;
                Vector3 current = points[i].localPosition;
                Vector3 next = points[i + 1].localPosition;
                float score = DistanceToSegmentSquared(current, previous, next);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private static float DistanceToSegmentSquared(
            Vector3 point,
            Vector3 start,
            Vector3 end)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.000001f)
                return (point - start).sqrMagnitude;

            float t = Mathf.Clamp01(
                Vector3.Dot(point - start, segment) / lengthSquared);
            return (point - (start + segment * t)).sqrMagnitude;
        }

        private static void RenamePoints(Transform[] points)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Undo.RecordObject(points[i].gameObject, "Rename Sonic Curve Points");
                points[i].name = $"Curve Control {i + 1:00}";
            }
        }

        private static void MarkChanged(SonicPointWave wave)
        {
            EditorUtility.SetDirty(wave);
            if (wave.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
            SceneView.RepaintAll();
        }
    }

    internal static class SonicCurveControlPointGizmos
    {
        [DrawGizmo(
            GizmoType.NonSelected |
            GizmoType.Selected |
            GizmoType.Pickable)]
        private static void DrawOrientation(
            SonicCurveControlPoint point,
            GizmoType gizmoType)
        {
            if (point == null)
                return;
            if (Application.isPlaying && !point.RuntimeEditingAllowed)
                return;

            SonicPointWave wave = point.GetComponentInParent<SonicPointWave>();
            if (wave == null)
                return;

            Transform[] points = wave.ControlPoints;
            int index = FindIndex(points, point.transform);
            if (index < 0)
                return;

            int previousIndex = Mathf.Max(0, index - 1);
            int nextIndex = Mathf.Min(points.Length - 1, index + 1);
            Vector3 tangent = points[nextIndex].position -
                              points[previousIndex].position;
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = wave.transform.right;
            tangent.Normalize();

            Vector3 normal = point.transform.up;
            normal -= tangent * Vector3.Dot(normal, tangent);
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.ProjectOnPlane(wave.transform.up, tangent);
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.ProjectOnPlane(wave.transform.forward, tangent);
            normal.Normalize();
            Vector3 widthDirection = Vector3.Cross(tangent, normal).normalized;

            bool selected = (gizmoType & GizmoType.Selected) != 0;
            float handleSize =
                HandleUtility.GetHandleSize(point.transform.position) *
                (selected ? 0.45f : 0.28f);
            Vector3 position = point.transform.position;

            Handles.color = new Color(1f, 0.12f, 0.72f, 0.95f);
            Handles.DrawAAPolyLine(
                selected ? 4f : 2f,
                position,
                position + normal * handleSize);
            Handles.ConeHandleCap(
                0,
                position + normal * handleSize,
                Quaternion.LookRotation(normal),
                handleSize * 0.18f,
                EventType.Repaint);

            Handles.color = new Color(0.05f, 0.95f, 1f, 0.78f);
            Handles.DrawAAPolyLine(
                selected ? 4f : 2f,
                position - widthDirection * handleSize * 0.55f,
                position + widthDirection * handleSize * 0.55f);

            if (selected)
            {
                Handles.color = Color.white;
                Handles.Label(
                    position + normal * handleSize * 1.18f,
                    "Wave height / twist direction");
            }
        }

        private static int FindIndex(Transform[] points, Transform target)
        {
            if (points == null)
                return -1;

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == target)
                    return i;
            }
            return -1;
        }
    }
}
#endif
