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
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    internal static class DemoModelRegression
    {
        public static void Verify()
        {
            var scene = EditorSceneManager.NewPreviewScene(); var sb = new StringBuilder();
            try
            {
                var root = new GameObject("Isolated model regression"); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene); root.AddComponent<DemoXRInput>();
                foreach(bool right in new[] {true, false})
                {
                    string side = right ? "Right" : "Left";
                    var parent = new GameObject(side); parent.transform.SetParent(root.transform, false);
                    var hand = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeepSeaDemo/Prefabs/LeatherGlove"+side+".prefab"),parent.transform).GetComponent<DemoHandAnimator>();
                    var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    var originalLocal = WorldVertices(skin).Select(v=>skin.transform.InverseTransformPoint(v)).ToArray();
                    typeof(DemoHandAnimator).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hand,null);
                    if(!skin.sharedMesh.name.EndsWith("Wrist")) throw new Exception("Corrected mesh not loaded: "+side);
                    var before=WorldVertices(skin);
                    float restError=before.Select((v,i)=>Vector3.Distance(skin.transform.InverseTransformPoint(v),originalLocal[i])*Mathf.Abs(skin.transform.lossyScale.x)).Max();
                    if(restError>.0001f) throw new Exception(side+" rest geometry changed by "+restError);
                    sb.AppendLine("PASS "+side+" unchanged open geometry: max error="+restError+" m");
                    var joints=(Transform[])typeof(DemoHandAnimator).GetField("joints",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hand);
                    var axes=(Vector3[])typeof(DemoHandAnimator).GetField("bendAxes",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hand);
                    var rest=joints.Select(t=>t.localRotation).ToArray();
                    var wrist=skin.bones.First(t=>t.name==(right?"hand_r":"hand_l"));
                    var middle=joints.First(t=>t.name==(right?"middle_01_r":"middle_01_l"));
                    var forward=(middle.position-wrist.position).normalized;
                    var cuff=before.Select((v,i)=>(v,i)).Where(p=>Vector3.Dot(p.v-wrist.position,forward)<-.0001f).Select(p=>p.i).ToArray();
                    if(cuff.Length<100) throw new Exception("Cuff test selected too few vertices");
                    foreach(float amount in new[]{0f,.25f,.5f,.75f,1f,0f})
                    {
                        for(int i=0;i<joints.Length;i++) joints[i].localRotation=rest[i]*Quaternion.AngleAxis(hand.poses.joints[i].curlDegrees*amount,axes[i]);
                        var after=WorldVertices(skin);
                        if(after.Any(v=>!skin.bounds.Contains(v))) throw new Exception(side+" animated vertices leave renderer culling bounds at grip="+amount+" bounds="+skin.bounds);
                        float movement=cuff.Max(i=>Vector3.Distance(before[i],after[i]));
                        if(movement>.0001f) throw new Exception(side+" cuff moves "+movement+" at grip="+amount);
                        float fingerMovement=after.Select((v,i)=>Vector3.Distance(v,before[i])).Max();
                        if(amount==1 && fingerMovement<.03f) throw new Exception("Fingers no longer animate");
                        sb.AppendLine("PASS "+side+" grip="+amount+" cuff max="+movement+" m; mesh motion="+fingerMovement+" m");
                        if(amount==1) Preview(skin,after,side+"-Closed");
                    }
                    Preview(skin,before,side+"-Open");
                    foreach(var w in skin.sharedMesh.boneWeights)
                    {
                        if(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.0001f) throw new Exception("Unnormalized weights");
                        if(new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3}.Any(i=>i>=skin.bones.Length)) throw new Exception("Invalid bone index");
                    }
                }
                var nav=new GameObject("Navigation root"); nav.transform.SetParent(root.transform,false);
                var fish=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fish/Enemy.fbx"),nav.transform);
                var head=fish.GetComponentsInChildren<Transform>(true).First(t=>t.name=="atama");
                var body=fish.GetComponentsInChildren<Transform>(true).First(t=>t.name=="kosi");
                if(!DeepSeaAI.DeepSeaStalkerController.AlignImportedFish(fish.transform,nav.transform)) throw new Exception("Fish not recognized");
                foreach(float yaw in new[]{0f,90f,180f,-45f})
                {
                    nav.transform.rotation=Quaternion.Euler(0,yaw,0);
                    float dot=Vector3.Dot((head.position-body.position).normalized,nav.transform.forward);
                    if(dot<.999f) throw new Exception("Fish sideways at yaw="+yaw+" dot="+dot);
                    sb.AppendLine("PASS fish heading yaw="+yaw+" head/forward dot="+dot);
                }
                var old=fish.transform.localRotation;
                DeepSeaAI.DeepSeaStalkerController.AlignImportedFish(fish.transform,nav.transform);
                if(Quaternion.Angle(old,fish.transform.localRotation)>.01f) throw new Exception("Fish correction accumulates");
                foreach(var clip in AssetDatabase.LoadAllAssetsAtPath("Assets/Fish/Enemy.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")))
                {
                    float min=1;
                    foreach(float phase in new[]{0f,.25f,.5f,.75f})
                    {
                        clip.SampleAnimation(fish,phase*clip.length);
                        min=Mathf.Min(min,Vector3.Dot((head.position-body.position).normalized,nav.transform.forward));
                    }
                    if(min<.5f) throw new Exception("Animation overwrites fish heading: "+clip.name+" dot="+min);
                    sb.AppendLine("PASS imported animation "+clip.name+" head alignment min="+min);
                }
                sb.AppendLine("NOT TESTED: Quest controller comfort or live navigation/door gameplay.");
            }
            catch(Exception ex) { sb.AppendLine("FAIL "+ex); throw; }
            finally { EditorSceneManager.ClosePreviewScene(scene); File.WriteAllText("Logs/DeepSeaDemo/Model-Regression.txt",sb.ToString()); }
        }

        static void Preview(SkinnedMeshRenderer skin, Vector3[] vertices, string name)
        {
            var mesh=Object.Instantiate(skin.sharedMesh); mesh.vertices=vertices; mesh.RecalculateBounds(); mesh.RecalculateNormals();
            var preview=new PreviewRenderUtility();
            try
            {
                preview.camera.orthographic=true; preview.camera.orthographicSize=.21f;
                var center=mesh.bounds.center;
                preview.camera.transform.position=center+new Vector3(.35f,.55f,-.3f);
                preview.camera.transform.LookAt(center,Vector3.up);
                preview.camera.nearClipPlane=.01f; preview.camera.farClipPlane=10;
                preview.camera.backgroundColor=new Color(.13f,.16f,.19f); preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.lights[0].intensity=1.2f; preview.lights[0].transform.rotation=Quaternion.Euler(40,30,0);
                preview.lights[1].intensity=.7f;
                preview.BeginStaticPreview(new Rect(0,0,640,640));
                for(int i=0;i<mesh.subMeshCount;i++) preview.DrawMesh(mesh,Matrix4x4.identity,skin.sharedMaterials[Mathf.Min(i,skin.sharedMaterials.Length-1)],i);
                preview.Render(true); var texture=preview.EndStaticPreview();
                File.WriteAllBytes("Logs/DeepSeaDemo/"+name+".png",texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            }
            finally { preview.Cleanup(); Object.DestroyImmediate(mesh); }
        }
        public static void Inspect()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Active scene: " + SceneManager.GetActiveScene().path);
            foreach (var hand in Object.FindObjectsByType<DemoHandAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine("Live hand: " + hand.name + " right=" + hand.right + " poses=" + AssetDatabase.GetAssetPath(hand.poses) + " scripts=" + string.Join(",", hand.GetComponentsInChildren<MonoBehaviour>(true).Select(b => b == null ? "missing" : b.GetType().Name)));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Model inspection"); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene); root.AddComponent<DemoXRInput>();
                foreach (bool right in new[] {true, false})
                {
                    var parent = new GameObject(right ? "Right" : "Left"); parent.transform.SetParent(root.transform, false);
                    var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeepSeaDemo/Prefabs/LeatherGlove" + (right ? "Right" : "Left") + ".prefab"), parent.transform);
                    sb.AppendLine("HAND " + model.name);
                    foreach (var t in model.GetComponentsInChildren<Transform>(true))
                        sb.AppendLine(t.name + " parent=" + t.parent?.name + " p=" + t.localPosition.ToString("F4") + " r=" + t.localEulerAngles + " s=" + t.localScale);
                    foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        sb.AppendLine("Skin " + skin.name + " mesh=" + AssetDatabase.GetAssetPath(skin.sharedMesh) + " bindposes=" + skin.sharedMesh.bindposes.Length + " bones=" + string.Join(",", skin.bones.Select(b => b.name)));
                        var imported=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(skin.sharedMesh));
                        foreach(var original in imported.GetComponentsInChildren<SkinnedMeshRenderer>(true)) sb.AppendLine("Imported bones="+string.Join(",",original.bones.Select(b=>b.name)));
                        for(int bi=0;bi<skin.sharedMesh.bindposes.Length;bi++) sb.AppendLine("Bind "+bi+" origin="+skin.sharedMesh.bindposes[bi].inverse.MultiplyPoint3x4(Vector3.zero));
                        var mesh = new Mesh(); skin.BakeMesh(mesh);
                        var before = mesh.vertices;
                        var animator = model.GetComponent<DemoHandAnimator>();
                        typeof(DemoHandAnimator).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(animator, null);
                        before = WorldVertices(skin);
                        var joints = (Transform[])typeof(DemoHandAnimator).GetField("joints", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(animator);
                        var axes = (Vector3[])typeof(DemoHandAnimator).GetField("bendAxes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(animator);
                        for (int i=0;i<joints.Length;i++) joints[i].localRotation *= Quaternion.AngleAxis(animator.poses.joints[i].curlDegrees, axes[i]);
                        var after = WorldVertices(skin);
                        var wrist = model.GetComponentsInChildren<Transform>(true).First(t=>t.name == (right ? "hand_r" : "hand_l"));
                        var middle = model.GetComponentsInChildren<Transform>(true).First(t=>t.name == (right ? "middle_01_r" : "middle_01_l"));
                        var forward = (middle.position-wrist.position).normalized;
                        int cuffCount=0, moved=0; float max=0;
                        for(int i=0;i<before.Length;i++)
                        {
                            if(Vector3.Dot(before[i]-wrist.position,forward)>0) continue;
                            cuffCount++; float d=Vector3.Distance(before[i],after[i]); max=Mathf.Max(max,d); if(d>.0001f) moved++;
                        }
                        sb.AppendLine("Cuff vertices="+cuffCount+" moved="+moved+" max="+max);
                        Object.DestroyImmediate(mesh);
                    }
                }
                var fish=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fish/Enemy.fbx"),root.transform);
                sb.AppendLine("FISH");
                foreach(var t in fish.GetComponentsInChildren<Transform>(true)) sb.AppendLine(t.name+" p="+fish.transform.InverseTransformPoint(t.position).ToString("F4")+" r="+t.localEulerAngles);
                foreach(var r in fish.GetComponentsInChildren<Renderer>()) sb.AppendLine("Bounds="+r.bounds);
            }
            catch(Exception ex) { sb.AppendLine(ex.ToString()); throw; }
            finally { EditorSceneManager.ClosePreviewScene(scene); Directory.CreateDirectory("Logs/DeepSeaDemo"); File.WriteAllText("Logs/DeepSeaDemo/Model-Inspection.txt",sb.ToString()); }
        }
        internal static Vector3[] WorldVertices(SkinnedMeshRenderer skin)
        {
            var mesh=skin.sharedMesh; var vertices=mesh.vertices; var weights=mesh.boneWeights; var bind=mesh.bindposes;
            var matrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*bind[i]).ToArray();
            for(int i=0;i<vertices.Length;i++)
            {
                var v=vertices[i]; var w=weights[i];
                vertices[i]=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
            }
            return vertices;
        }
    }
}
#endif
