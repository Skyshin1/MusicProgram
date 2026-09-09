#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSeaDemo.Editor
{
    internal static class DemoDoorwayRepair
    {
        [MenuItem("Tools/Deep Sea Demo/Repair Equipment Room Door Collisions")]
        public static void Repair()
        {
            try { RepairCore(); }
            catch (Exception ex)
            {
                Directory.CreateDirectory("Logs/DeepSeaDemo");
                File.WriteAllText("Logs/DeepSeaDemo/Doorway-Repair-Error.txt", ex.ToString());
                throw;
            }
        }
        static void RepairCore()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != DeepSeaDemoBuilder.Copy1VR || Application.isPlaying) throw new Exception("Keep the Demo copy open outside Play.");
            var rig = scene.GetRootGameObjects().Single(g => g.name == "__OilRigGenerated");
            var room = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "MainDeck_Preparation_And_Electrical_Block");
            var report = new StringBuilder("Only equipment block south door collisions; no scene save/reload, model or global step-height changes.\n");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Repair equipment room door collisions");
            foreach (string name in new[] { "South_Wall_00", "South_Wall_02" })
            {
                var wall = room.Find(name);
                var renderer = wall.GetComponentInChildren<Renderer>(true);
                if (!wall.gameObject.activeInHierarchy || renderer == null) { report.AppendLine("SKIP inactive/missing " + name); continue; }
                var b = renderer.bounds;
                bool alongX = b.size.x > b.size.z;
                float span = alongX ? b.size.x : b.size.z;
                float thickness = Mathf.Max(.15f, alongX ? b.size.z : b.size.x);
                const float gap = 1.2f, clearHeight = 2.3f;
                if (span < gap + .5f || b.size.y < clearHeight + .1f) throw new Exception("Unexpected door dimensions: " + name);
                foreach (var old in wall.GetComponents<Collider>())
                { Undo.RecordObject(old, "Preserve disabled original door collider"); old.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(old); }
                var container = wall.Find("Demo Walkable Door Collision");
                if (container == null)
                {
                    var go = new GameObject("Demo Walkable Door Collision"); Undo.RegisterCreatedObjectUndo(go, "Create door collision");
                    go.transform.SetParent(wall, false); container = go.transform;
                }
                // World-aligned boxes are deliberate: these two existing south walls
                // are axis-aligned. Preserve their mesh and remove only the sill blocker.
                float side = (span - gap) * .5f;
                void Box(string id, Vector3 center, Vector3 size)
                {
                    var child = container.Find(id);
                    if (child == null) { var go = new GameObject(id); Undo.RegisterCreatedObjectUndo(go, "Create door collider"); go.transform.SetParent(container, false); child = go.transform; }
                    Undo.RecordObject(child, "Align door collider"); child.SetPositionAndRotation(center, Quaternion.identity);
                    child.localScale = new Vector3(1f / container.lossyScale.x, 1f / container.lossyScale.y, 1f / container.lossyScale.z);
                    child.gameObject.layer = LayerMask.NameToLayer("DeepSeaWorld");
                    var collider = child.GetComponent<BoxCollider>();
                    if (collider == null) collider = Undo.AddComponent<BoxCollider>(child.gameObject);
                    Undo.RecordObject(collider, "Size door collider"); collider.center = Vector3.zero; collider.size = size; collider.enabled = true;
                }
                Vector3 horizontal = alongX ? Vector3.right : Vector3.forward;
                Vector3 sideSize = alongX ? new Vector3(side, b.size.y, thickness) : new Vector3(thickness, b.size.y, side);
                Box("Left jamb", b.center - horizontal * (gap + side) * .5f, sideSize);
                Box("Right jamb", b.center + horizontal * (gap + side) * .5f, sideSize);
                float topHeight = b.size.y - clearHeight;
                Box("Lintel", new Vector3(b.center.x, b.min.y + clearHeight + topHeight * .5f, b.center.z),
                    alongX ? new Vector3(gap, topHeight, thickness) : new Vector3(thickness, topHeight, gap));
                Physics.SyncTransforms();
                bool clear = true;
                for (int i = -5; i <= 5; i++)
                {
                    Vector3 p = new Vector3(b.center.x, b.min.y, b.center.z) + (alongX ? Vector3.forward : Vector3.right) * i * .2f;
                    var blockers = Physics.OverlapCapsule(p + Vector3.up * .28f, p + Vector3.up * 1.55f, .24f,
                        1 << LayerMask.NameToLayer("DeepSeaWorld"), QueryTriggerInteraction.Ignore);
                    if (blockers.Length > 0) { clear = false; report.AppendLine("BLOCKED " + name + " point=" + p + " by=" + string.Join(",", blockers.Select(c => c.name))); }
                }
                report.AppendLine((clear ? "PASS" : "CHECK") + " " + name + " 1.2 m wide, 2.3 m high, floor-level collision opening. Visual sill unchanged.");
            }
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory("Logs/DeepSeaDemo"); File.WriteAllText("Logs/DeepSeaDemo/Doorway-Repair.txt", report.ToString());
        }
        public static void Inspect()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != DeepSeaDemoBuilder.Copy1VR || Application.isPlaying) throw new Exception("Keep the Demo copy open outside Play.");
            var rig = scene.GetRootGameObjects().Single(g => g.name == "__OilRigGenerated");
            var room = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "MainDeck_Preparation_And_Electrical_Block");
            var wall = room.Find("South_Wall_00");
            var sb = new StringBuilder();
            sb.AppendLine("Scene dirty=" + scene.isDirty + " Wall=" + wall.name + " position=" + wall.position);
            foreach (var c in wall.GetComponentsInChildren<Collider>(true))
                sb.AppendLine("Collider " + c.name + " " + c.GetType().Name + " center=" + c.bounds.center + " size=" + c.bounds.size);
            foreach (var r in wall.GetComponentsInChildren<Renderer>(true))
                sb.AppendLine("Renderer " + r.name + " center=" + r.bounds.center + " size=" + r.bounds.size);
            Physics.SyncTransforms();
            for (int xi = -1; xi <= 1; xi++)
                for (int zi = -3; zi <= 3; zi++)
                {
                    var p = rig.transform.TransformPoint(new Vector3(-16 + xi * .35f, 7.2f, -10 + zi * .35f));
                    var hits = Physics.RaycastAll(p, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
                    sb.AppendLine("Floor probe " + p + ": " + string.Join("; ", hits.Select(h => h.collider.name + " y=" + h.point.y.ToString("F3"))));
                }
            Directory.CreateDirectory("Logs/DeepSeaDemo"); File.WriteAllText("Logs/DeepSeaDemo/Doorway-Inspection.txt", sb.ToString());
        }
    }
}
#endif
