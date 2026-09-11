using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace DeepSeaDemo
{
    public sealed class DemoHandAnimator : MonoBehaviour
    {
        public bool right;
        public DemoHandPose poses;
        [Tooltip("Align the supplied left glove wrist/palm to the mirrored right glove in controller-local space. Does not modify tracking or bone scale.")]
        public bool calibrateLeftPalm = true;
        [Tooltip("Use each left finger's actual local bend plane, including the supplier's mirrored wrist. Disable to use the pose asset axes verbatim.")]
        public bool anatomicalLeftCurl = true;
        Transform[] joints;
        Quaternion[] rest;
        Vector3[] bendAxes;
        float grip, trigger;
        void Awake()
        {
            if (poses == null || poses.joints == null) { enabled = false; return; }
            var byName = new Dictionary<string, Transform>();
            foreach (Transform bone in GetComponentsInChildren<Transform>(true)) byName[bone.name] = bone;
            if (!right && calibrateLeftPalm) AlignLeftPalm(byName);
            ApplyStableWristSkin(byName);
            joints = new Transform[poses.joints.Length]; rest = new Quaternion[joints.Length];
            bendAxes = new Vector3[joints.Length];
            Vector3 dorsal = Vector3.zero;
            if (!right && byName.TryGetValue("hand_l", out var wrist) && byName.TryGetValue("middle_01_l", out var middle) &&
                byName.TryGetValue("index_01_l", out var index) && byName.TryGetValue("pinky_01_l", out var pinky))
                dorsal = Vector3.Cross(middle.position - wrist.position, index.position - pinky.position).normalized;
            for (int i = 0; i < joints.Length; i++)
            {
                if (!byName.TryGetValue(poses.joints[i].name, out joints[i])) continue;
                var joint = joints[i]; rest[i] = joint.localRotation;
                bendAxes[i] = poses.joints[i].curlAxis;
                if (!right && anatomicalLeftCurl && dorsal.sqrMagnitude > .5f)
                {
                    // InverseTransformVector includes reflected scales. Taking the cross
                    // product AFTER conversion avoids inverting the left wrist twice.
                    var along = joint.childCount > 0 ? joint.GetChild(0).position - joint.position : joint.position - joint.parent.position;
                    var localAlong = joint.InverseTransformVector(along).normalized;
                    var localPalm = joint.InverseTransformVector(-dorsal).normalized;
                    var axis = Vector3.Cross(localAlong, localPalm).normalized;
                    if (axis.sqrMagnitude > .5f) bendAxes[i] = axis * Mathf.Sign(poses.joints[i].curlDegrees);
                }
            }
        }
        void ApplyStableWristSkin(Dictionary<string, Transform> bones)
        {
            if (!bones.TryGetValue(right ? "hand_r" : "hand_l", out var wrist)) return;
            var mesh = Resources.Load<Mesh>("DeepSeaDemo/LeatherGlove" + (right ? "Right" : "Left") + "Wrist");
            if (mesh == null) return;
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Only the supplied leather glove mesh uses this corrected binding.
                if (skin.sharedMesh == null || skin.sharedMesh.vertexCount != mesh.vertexCount || skin.name != "Leather_glove_lowpoly:Group2") continue;
                var bound = new List<Transform>(skin.bones);
                if (!bound.Contains(wrist)) bound.Add(wrist);
                if (bound.Count != mesh.bindposes.Length) continue;
                skin.bones = bound.ToArray(); skin.sharedMesh = mesh; skin.rootBone = wrist;
                // Skinned bounds follow rootBone. The left mesh is exported in
                // centimetres, so copying its mesh-local bounds here is incorrect.
                float radius = 0;
                var bounds = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    radius = Mathf.Max(radius, wrist.InverseTransformPoint(skin.transform.TransformPoint(bounds.center + offset)).magnitude);
                }
                skin.localBounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.5f);
            }
        }
        void AlignLeftPalm(Dictionary<string, Transform> bones)
        {
            if (transform.parent == null || !bones.TryGetValue("hand_l", out var wrist) ||
                !bones.TryGetValue("middle_01_l", out var middle) || !bones.TryGetValue("index_01_l", out var index) ||
                !bones.TryGetValue("pinky_01_l", out var pinky)) return;
            var input = GetComponentInParent<DemoXRInput>(true);
            if (input == null) return;
            DemoHandAnimator other = null;
            foreach (var candidate in input.GetComponentsInChildren<DemoHandAnimator>(true))
                if (candidate.right) { other = candidate; break; }
            if (other == null || other.transform.parent == null) return;
            var otherBones = new Dictionary<string, Transform>();
            foreach (var bone in other.GetComponentsInChildren<Transform>(true)) otherBones[bone.name] = bone;
            if (!otherBones.TryGetValue("hand_r", out var rw) || !otherBones.TryGetValue("middle_01_r", out var rm) ||
                !otherBones.TryGetValue("index_01_r", out var ri) || !otherBones.TryGetValue("pinky_01_r", out var rp)) return;
            Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);
            var targetWrist = Mirror(other.transform.parent.InverseTransformPoint(rw.position));
            var targetForward = Mirror(other.transform.parent.InverseTransformVector(rm.position - rw.position));
            var targetAcross = Mirror(other.transform.parent.InverseTransformVector(ri.position - rp.position));
            var sourceWrist = transform.InverseTransformPoint(wrist.position);
            var sourceForward = transform.InverseTransformVector(middle.position - wrist.position);
            var sourceAcross = transform.InverseTransformVector(index.position - pinky.position);
            if (Vector3.Cross(sourceForward, sourceAcross).sqrMagnitude < 1e-8f || Vector3.Cross(targetForward, targetAcross).sqrMagnitude < 1e-8f) return;
            var source = Quaternion.LookRotation(sourceForward, Vector3.Cross(sourceForward, sourceAcross));
            var target = Quaternion.LookRotation(targetForward, Vector3.Cross(targetForward, targetAcross));
            transform.localRotation = target * Quaternion.Inverse(source);
            transform.localPosition = targetWrist - transform.localRotation * Vector3.Scale(sourceWrist, transform.localScale);
        }
        void LateUpdate()
        {
            var device = InputDevices.GetDeviceAtXRNode(right ? XRNode.RightHand : XRNode.LeftHand);
            device.TryGetFeatureValue(CommonUsages.grip, out float g);
            device.TryGetFeatureValue(CommonUsages.trigger, out float t);
            var input = GetComponentInParent<DemoXRInput>();
            if (input != null) { g = input.Hand(right).Grip; t = input.Hand(right).Trigger; }
            var prop = DemoInputRouter.Instance != null ? DemoInputRouter.Instance.HeldProp(right) : null;
            if (prop != null) g = Mathf.Max(g, prop.fingerCurl);
            float blend = 1f - Mathf.Exp(-poses.blendSpeed * Time.unscaledDeltaTime);
            grip = Mathf.Lerp(grip, g, blend); trigger = Mathf.Lerp(trigger, t, blend);
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null) continue;
                var pose = poses.joints[i];
                float amount = pose.index ? Mathf.Max(trigger, prop != null ? .3f : 0) : grip;
                joints[i].localRotation = rest[i] * Quaternion.AngleAxis(pose.curlDegrees * amount, bendAxes[i]);
            }
        }
    }
}
