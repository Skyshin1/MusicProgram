#if UNITY_EDITOR
using System;
using System.Linq;
using AbstractOcclusion.WebGpuWater;
using DeepSeaAI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    public static partial class DeepSeaDemoBuilder
    {
        static readonly string[] LogTitles = { "例行航海记录", "夜班设备异常", "潜水员观察", "未发送的报告" };
        static readonly string[] LogBodies = {
            "6月12日，晴转阴。补给船比预定时间早到了半小时，厨房终于有新鲜蔬菜。今天例行检查了平台南侧扶梯，海面风浪很小，新来的潜水员说这里比培训基地还安静。午后声纳值班员在二十四米处收到一段重复回声，间隔像有人轻敲钢管。我们检查了绞车和泵房，没有发现松动。他把记录标成海床反射，准备明天再测。晚饭时大家还拿这件事开玩笑，说是海里的邻居在催我们下班。夜里回声又出现了一次，但没有任何设备报警。我在纸上记下了时间：23时17分。",
            "6月14日，夜班。声纳阵列第三通道在23时17分突然失联，排污泵紧接着跳闸。纸质值班簿记录得很清楚，可系统导出的报警时间却是次日零点零三分，还附了一条自动生成的说明：例行维护，无人员风险。我没有签过这份维护单。检修员在外壳上发现了向内凹陷的痕迹，不像锈蚀。我们停机后，监测里的敲击声也停了；重新测试阵列，回声立刻靠近。主管让我们不要传播猜测，把原始记录上传公司即可。我保留了这一页，没有改时间。",
            "6月16日，潜水记录。排放口附近的水变得很浑，手电只能照出面前一小段漂浮物。看不见不代表那里没有东西。我的同伴开了主动声纳，远处出现一个轮廓，下一次扫描时它已经到了泵架旁。我们关掉设备躲在岩石后，它没有继续靠近人，却绕着仍在运转的管线移动。我听见金属受力的呻吟，第一次不敢确认那是水流。返回时同伴一直握着我的手腕。请下一班不要连续扫描，也不要在黑暗里追着回声走。把石头扔到远处的金属架，也许能换来几秒离开的时间。",
            "6月18日，未发送。公司要求删除污染峰值，使用上周的水样作为本周报告。我无法照做。潜艇记录器还保存着原始传感器日志，排污数据与事故报警有明显时间差。最后一次影像中，那些生物先撞毁声纳阵列，再攻击排污泵，并没有追上正在撤离的人。我们把恐惧写成了海洋里的恶意，却没有记录自己每天向那里排了什么。若有人找到这份报告，请带回黑匣子；若条件允许，再取一份水样。不要只相信公司远程提供的摘要。对照时间，听完记录，然后自己决定证据的去向。"
        };

        static void CreateLevel(Transform parent, DemoFlow flow)
        {
            var level = Make("Level Regions", parent);
            var metal = Mat("PlatformMetal", new Color(.17f, .22f, .25f));
            var pale = Mat("EquipmentIvory", new Color(.72f, .76f, .70f));
            var orange = Mat("SafetyOrange", new Color(.95f, .38f, .06f));
            var cyan = Mat("RouteCyan", new Color(.06f, .7f, .75f), true);
            var platform = Make("Platform Investigation", level.transform);
            var rig = Load("Assets/Prefab/__OilRigGenerated.prefab", platform.transform);
            rig.name = "Visual Existing Oil Rig"; Fit(rig, new Vector3(0, -5, -14), 30);
            // Visual rig stays intact. Traversable gameplay deck is a continuous simplified collider.
            foreach (var c in rig.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var b in rig.GetComponentsInChildren<MonoBehaviour>(true)) if (b != null) Object.DestroyImmediate(b);
            UpgradeMaterials(rig); SetLayer(rig, worldLayer);
            Box("Continuous Deck", platform.transform, new(0, 2.05f, -14), new(16, .5f, 12), metal, groundLayer);
            Box("North Safety Rail", platform.transform, new(0, 2.85f, -19.85f), new(16, 1.1f, .16f), metal);
            Box("West Safety Rail", platform.transform, new(-7.85f, 2.85f, -14), new(.16f, 1.1f, 12), metal);
            Box("East Safety Rail", platform.transform, new(7.85f, 2.85f, -14), new(.16f, 1.1f, 12), metal);
            Box("Safe Room Rear", platform.transform, new(-3.8f, 3.8f, -18.4f), new(6, 3, .18f), metal);
            Box("Safe Room Roof", platform.transform, new(-3.8f, 5.3f, -16.4f), new(6, .18f, 4), metal);
            Box("Analysis Room Roof", platform.transform, new(4.4f, 5.3f, -16), new(5, .18f, 5), metal);
            Box("Analysis Rear", platform.transform, new(4.4f, 3.8f, -18.5f), new(5, 3, .18f), metal);
            var equipment = Box("Equipment Table", platform.transform, new(-3.8f, 2.9f, -15.8f), new(3.8f, .2f, 1), pale);
            Sign(platform.transform, new(-3.8f, 4.35f, -17.9f), "01 / 平台调查\n阅读日志 · 核对报警 · 拿取手电 · 确认潜水服", 0);
            for (int i = 0; i < 4; i++)
            {
                var log = Action("Log " + (i + 1), platform.transform, new(-5.1f + i * .75f, 3.08f, -15.6f), new(.48f, .04f, .32f), pale, DemoActionKind.Document);
                log.fact = "log0" + (i + 1); log.title = LogTitles[i]; log.body = LogBodies[i];
            }
            var alarm = Action("Alarm Terminal", platform.transform, new(-5.8f, 3.5f, -17.9f), new(.65f, .55f, .12f), orange, DemoActionKind.Alarm);
            alarm.fact = "alarm"; alarm.title = "报警记录比对";
            alarm.body = "纸质记录：6月14日 23:17，阵列失联、泵组停机。\n系统导出：6月15日 00:03，例行维护。\n相差46分钟；纸质簿没有维护人员签名。原始时间已登记为证据。";
            var suit = Action("Diving Suit Confirmation", platform.transform, new(-1.1f, 3.4f, -16.8f), new(.55f, .9f, .35f), cyan, DemoActionKind.Equip);
            suit.title = "确认常压潜水服";
            var flash = Prop("Flashlight", "flashlight", new(-2.3f, 3.22f, -15.65f), new(.09f, .09f, .28f), orange, platform.transform, true);
            flash.factOnPickup = "flashlight"; var lamp = flash.gameObject.AddComponent<GrabFlashlight>(); Set(lamp, "range", 6f);
            Sign(platform.transform, new(0, 3.6f, -8.4f), "02 / 下潜\n左摇杆移动 · 右摇杆上下潜水\n水面停留后，向下拨才重新下潜", 180);
            flow.checkpoints = new[] {
                Point("Checkpoint Platform Departure", level.transform, new(0, 3.95f, -14)),
                Point("Checkpoint Tool Cage", level.transform, new(-15, -21.8f, 8)),
                Point("Checkpoint Submarine", level.transform, new(14, -21.8f, 17)),
                Point("Checkpoint Analysis", level.transform, new(4, 3.95f, -14)) };
            var board = Action("Safe Boarding", platform.transform, new(0, .4f, -7.1f), new(1.5f, .9f, .3f), orange, DemoActionKind.Board);
            board.destination = flow.checkpoints[0]; board.title = "登上平台";
            Sign(platform.transform, new(0, 1.2f, -7), "返回平台 / 空手 Trigger 登乘", 180);
            CreateDocks(platform.transform, flow, metal, cyan);

            var seabed = Make("Seabed Hub and Branches", level.transform);
            Material sand = AssetDatabase.LoadAssetAtPath<Material>("Assets/Davis3D/OceanEnvironmentPack2/Materials/SandPebbles.mat") ?? Mat("SeabedSand", new(.24f, .26f, .21f));
            Box("Continuous Seabed Navigation", seabed.transform, new(0, -24, 14), new(94, 1, 92), sand, groundLayer);
            // All branches remain within the 25 m sonar radius of their immediate wayfinding markers.
            Pipe(seabed.transform, new(0, -23.15f, -5), new(0, -23.15f, 10), metal);
            Pipe(seabed.transform, new(0, -23.15f, 10), new(-17, -23.15f, 10), metal);
            Pipe(seabed.transform, new(0, -23.15f, 10), new(17, -23.15f, 18), metal);
            Pipe(seabed.transform, new(0, -23.15f, 10), new(0, -23.15f, 30), metal);
            for (int z = -5; z <= 30; z += 7) Box("Return Beacon", seabed.transform, new(1, -22.8f, z), new(.15f, .7f, .15f), cyan);
            for (int i = 0; i < 4; i++) Box("Cage Post", seabed.transform, new(-18 + (i % 2) * 4, -22, 9 + (i / 2) * 4), new(.15f, 3, .15f), metal);
            Box("Cage Roof", seabed.transform, new(-16, -20.4f, 11), new(4.2f, .12f, 4.2f), metal);
            Box("Cage Workbench", seabed.transform, new(-16, -22.6f, 11), new(2.4f, .3f, 1), metal);
            var tool = Prop("Electronic Lock Repair Tool", "locktool", new(-16, -22.23f, 11), new(.15f, .13f, .35f), orange, seabed.transform, false);
            tool.factOnPickup = "locktool"; var repairTool = tool.gameObject.AddComponent<RepairTool>(); Set(repairTool, "allowDesktopKeyboardTest", false);
            tool.gameObject.AddComponent<RepairSkillCheckController>();
            Prop("Electronic Access Card", "card", new(-15.5f, -22.35f, 11), new(.16f, .025f, .10f), cyan, seabed.transform, false);
            Sign(seabed.transform, new(-16, -21.1f, 12.8f), "维修笼 / 电子锁工具\n携工具前往潜艇，手电可放在门外架子上", 180);
            var sample = Prop("Water Sample", "sample", new(1, -23, 30), new(.13f, .28f, .13f), Mat("SampleGreen", Color.green), seabed.transform, false);
            sample.factOnPickup = "sample_found";
            var sensor = Action("Outfall Sensor", seabed.transform, new(0, -22.3f, 30), new(.6f, .5f, .2f), cyan, DemoActionKind.Terminal);
            sensor.fact = "sensor"; sensor.title = "传感器原始日志";
            sensor.body = "23:11 排放阀开启。23:14 浊度突增至基线的8.7倍。23:17 声纳阵列失联。\n上报文件中23:10—23:59段被平滑为正常值。原始只读记录已登记。附近水样可带回平台做交叉验证。";
            var testimony = Action("Survivor Transcript", seabed.transform, new(2, -22.5f, 30), new(.4f, .2f, .2f), pale, DemoActionKind.Document);
            testimony.fact = "testimony"; testimony.title = "幸存者录音转写";
            testimony.body = "我没有看清它。泵一停，它也停了。我们离开的时候，它转向了还在响的阵列，而不是朝我们来。\n请别把那段停机记录删掉。那不是设备维护，是我们活着回来的原因。";
            for (int i = 0; i < 5; i++)
            {
                var stone = Prop("Throwing Stone " + (i + 1), "stone" + i, new(-2 + i * .55f, -23.1f, 6), new(.15f, .12f, .18f), Mat("StoneGrey", new(.24f, .29f, .31f)), seabed.transform, false);
                stone.critical = false;
                Object.DestroyImmediate(stone.transform.Find("Visual").gameObject);
                var rockVisual = Load("Assets/Davis3D/OceanEnvironmentPack2/Prefabs/Rocks/SM_Rock_A.prefab", stone.transform);
                rockVisual.name = "Visual"; Fit(rockVisual, stone.transform.position, .2f); SetLayer(rockVisual, propLayer);
                foreach (var collider in rockVisual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var pulse = stone.gameObject.AddComponent<VolumetricFogCollisionPulse>();
                Set(pulse, "minimumRelativeSpeed", config.impactSpeed); Set(pulse, "retriggerDelay", config.impactCooldown);
                Set(pulse, "collisionLayers", config.worldMask | config.interactMask); Set(pulse, "ignoredLayers", (1 << groundLayer) | (1 << playerLayer));
                Set(pulse, "overrideRingShape", true); Set(pulse, "maximumRadius", config.impactRadius);
                stone.gameObject.AddComponent<SonarCollisionGroup>();
            }
            PopulateOcean(seabed.transform);
            CreateSubmarine(level.transform, flow, metal, orange, cyan);
        }

        static DemoWorldAction Action(string name, Transform parent, Vector3 position, Vector3 size, Material m, DemoActionKind kind)
        {
            var go = Box(name, parent, position, size, m, propLayer);
            var a = go.AddComponent<DemoWorldAction>(); a.kind = kind; a.title = name; return a;
        }
        static void Sign(Transform parent, Vector3 position, string content, float yaw)
        {
            var go = Make("Sign " + content.Split('\n')[0], parent); go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * .0025f;
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(900, 250);
            var textGo = Make("Text", go.transform); var text = textGo.AddComponent<Text>();
            text.font = config.chineseFont; text.text = content; text.fontSize = 32; text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.rectTransform.sizeDelta = rect.sizeDelta; SetLayer(go, worldLayer);
        }
        static DemoProp Prop(string name, string id, Vector3 position, Vector3 size, Material m, Transform parent, bool floats)
        {
            var go = Make(name, parent); go.transform.position = position; go.layer = propLayer;
            var visual = Box("Visual", go.transform, Vector3.zero, size, m, propLayer); Object.DestroyImmediate(visual.GetComponent<Collider>());
            var collider = go.AddComponent<BoxCollider>(); collider.size = size;
            var body = go.AddComponent<Rigidbody>(); body.mass = .6f; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = go.AddComponent<DemoGrabInteractable>(); grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.useDynamicAttach = false; grab.throwOnDetach = true;
            var prop = go.AddComponent<DemoProp>(); prop.id = id;
            prop.leftAttach = Point("Attach Left", go.transform, position); prop.rightAttach = Point("Attach Right", go.transform, position);
            go.AddComponent<WaterMembership>(); go.AddComponent<WaterInteractable>(); go.AddComponent<WaterBuoyancy>(); go.AddComponent<WaterSplash>();
            var bridge = go.AddComponent<BuoyantXRGrabBridge>(); bridge.HeldForceScale = 0; bridge.ReleasedForceScale = floats ? 1 : 0;
            var recovery = Point(name + " Recovery Point", parent, position); prop.recoveryPoint = recovery;
            props.Add(prop); return prop;
        }
        static void Pipe(Transform parent, Vector3 a, Vector3 b, Material material)
        {
            var go = Box("Pipeline", parent, (a + b) * .5f, new(.28f, .28f, Vector3.Distance(a, b)), material);
            go.transform.rotation = Quaternion.LookRotation(b - a); go.AddComponent<SonarCollisionGroup>();
        }
        static void CreateDocks(Transform parent, DemoFlow flow, Material metal, Material cyan)
        {
            Box("Analysis Workbench", parent, new(4.5f, 2.95f, -16.5f), new(3.6f, .3f, 1.3f), metal);
            var dock = Make("Black Box Analysis Socket", parent); dock.transform.position = new(4, 3.28f, -16.5f); dock.layer = propLayer;
            var col = dock.AddComponent<BoxCollider>(); col.isTrigger = true; col.size = new(.6f, .4f, .6f);
            var socket = dock.AddComponent<DemoEvidenceSocket>(); socket.requiredId = "blackbox";
            socket.showInteractableHoverMeshes = false; socket.attachTransform = Point("Attach", dock.transform, dock.transform.position);
            dock.AddComponent<BlackBoxPlaybackDock>();
            Box("Dock Base", parent, new(4, 3.16f, -16.5f), new(.65f, .08f, .65f), cyan);
            var evidence = Action("Water Sample Receiver", parent, new(5.2f, 3.25f, -16.5f), new(.4f, .35f, .4f), cyan, DemoActionKind.EvidenceDock);
            evidence.GetComponent<Collider>().isTrigger = true; evidence.requiredPropId = "sample"; evidence.fact = "sample"; evidence.title = "污染水样";
            Sign(parent, new(4.5f, 4.3f, -18.1f), "05 / 黑匣子解析室\n左座：黑匣子　右座：可选水样\n放入黑匣子，等待记录播放完成", 0);
        }
        static void PopulateOcean(Transform parent)
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Davis3D/OceanEnvironmentPack2/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.Contains("Rocks") || p.Contains("Coral")).OrderBy(p => p).ToArray();
            if (paths.Length == 0) { notes.Add("WARNING: Ocean rock/coral prefabs not found."); return; }
            var rng = new System.Random(817);
            for (int i = 0; i < 38; i++)
            {
                float x = (float)rng.NextDouble() * 66 - 33, z = (float)rng.NextDouble() * 58 - 8;
                if (Mathf.Abs(x) < 4 || (x > -22 && x < 27 && z > 5 && z < 25)) continue;
                var rock = Load(paths[i % paths.Length], parent); rock.name = "Ocean Decoration " + i;
                rock.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                float size = 1 + (float)rng.NextDouble() * 3; Fit(rock, new(x, -23 + size * .25f, z), size);
                SetLayer(rock, worldLayer);
                foreach (var c in rock.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                Bounds b = BoundsOf(rock); var proxy = Make("Rock Collision", parent); proxy.transform.position = b.center;
                proxy.layer = worldLayer; var collider = proxy.AddComponent<BoxCollider>(); collider.size = b.size * .82f;
                proxy.AddComponent<SonarCollisionGroup>();
            }
        }
        static void CreateSubmarine(Transform parent, DemoFlow flow, Material metal, Material orange, Material cyan)
        {
            var region = Make("Flooded Submarine", parent);
            var sub = Load("Assets/Prefab/SubMarine.prefab", region.transform); UpgradeMaterials(sub);
            Fit(sub, new(20, -20.8f, 19), 19); SetLayer(sub, worldLayer);
            var doorGroup = sub.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Equals("Door", StringComparison.OrdinalIgnoreCase));
            if (doorGroup == null) throw new InvalidOperationException("SubMarine has no Door group; refusing to substitute an unrelated door.");
            foreach (var filter in sub.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            Bounds doorBounds = BoundsOf(doorGroup.gameObject);
            Vector3 outward = doorBounds.center - BoundsOf(sub).center; outward.y = 0;
            if (outward.sqrMagnitude < .1f) outward = Vector3.back; outward.Normalize();
            bool topHatch = doorBounds.size.y < Mathf.Min(doorBounds.size.x, doorBounds.size.z);
            Vector3 approach = topHatch ? doorBounds.center + Vector3.up * 2.2f : doorBounds.center + outward * 2.2f;
            var hinge = Point("Door Hinge", region.transform, doorBounds.center + Vector3.left * doorBounds.extents.x);
            doorGroup.SetParent(hinge, true);
            var mechanism = Make("Door QTE Mechanism", region.transform); mechanism.transform.position = doorBounds.center;
            var repair = mechanism.AddComponent<RepairableFacility>();
            var repairCol = mechanism.AddComponent<BoxCollider>(); repairCol.size = new(.55f, .6f, .15f); repairCol.isTrigger = true;
            mechanism.transform.position = doorBounds.center + outward * .35f; mechanism.layer = propLayer;
            var decalGo = Make("Damage Decal", mechanism.transform); var decal = decalGo.AddComponent<DecalProjector>();
            decal.size = new(.55f, .6f, .7f); decalGo.transform.rotation = Quaternion.LookRotation(-outward);
            var template = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Decal.mat");
            if (template == null) throw new InvalidOperationException("Installed URP default Decal material missing.");
            var decalMaterial = Asset<Material>(Root + "/Generated/RepairDamageDecal.mat", () => new Material(template));
            decalMaterial.SetColor("Base_Color", new Color(.65f, .12f, .015f, .92f));
            decalMaterial.SetColor("_BaseColor", new Color(.65f, .12f, .015f, .92f)); EditorUtility.SetDirty(decalMaterial);
            decal.material = decalMaterial;
            repair.Configure("StandardRepairTool", 18f, new[] { decal });
            var controller = mechanism.AddComponent<DemoDoor>(); controller.repair = repair; controller.hinge = hinge;
            if (topHatch) controller.openEuler = new Vector3(0, 0, 105);
            controller.safetyCenter = Point("Door Sweep Safety", region.transform, doorBounds.center + outward * .5f);
            controller.obstructionMask = (1 << playerLayer) | (1 << propLayer); flow.door = controller;
            var blocker = Box("Closed Door Blocking Volume", region.transform, doorBounds.center, Vector3.Max(doorBounds.size, new Vector3(.18f, .18f, .18f)), metal);
            Object.DestroyImmediate(blocker.GetComponent<Renderer>()); controller.doorwayBlocker = blocker.GetComponent<Collider>();
            flow.checkpoints[2].position = approach; flow.checkpoints[2].rotation = Quaternion.LookRotation(-outward);
            Box("Flashlight Rest Shelf", region.transform, approach + Vector3.right * .8f - Vector3.up * .6f, new(.7f, .1f, .7f), metal);
            Sign(region.transform, approach + Vector3.up * .8f, "04 / 潜艇舱门\n工具靠近锁，持续 Trigger 维修\n另一只空手 Grip 判定圆环；放下手电再操作", Quaternion.LookRotation(-outward).eulerAngles.y);
            Vector3 inside = topHatch ? doorBounds.center - Vector3.up * 2.5f : doorBounds.center - outward * 2.2f;
            var blackbox = Prop("Black Box", "blackbox", inside, new(.35f, .25f, .3f), orange, region.transform, false);
            blackbox.factOnPickup = "blackbox"; blackbox.gameObject.AddComponent<BlackBoxItem>();
            var last = Action("Last Submarine Recording", region.transform, inside + Vector3.up * .65f, new(.7f, .4f, .08f), cyan, DemoActionKind.Terminal);
            last.fact = "last_record"; last.title = "最后记录 / 事件画面转写";
            last.body = "画面01：声纳阵列发出连续脉冲，生物转向阵列。\n画面02：排污泵外壳破裂，生物围绕泵体，而撤离者已远去。\n记录员：它们追的不是人，是声音。\n黑匣子含原始时间戳。取走后备用电源将启动，请沿灯标返回平台。";
            flow.engineInvestigation = Point("Engine Investigation Outside Hull", region.transform, approach + outward * 3);
            flow.engineAudio = Make("Engine Story Audio", region.transform).AddComponent<AudioSource>(); flow.engineAudio.transform.position = doorBounds.center;
            flow.engineAudio.spatialBlend = 1; flow.engineAudio.maxDistance = 30; flow.engineAudio.playOnAwake = false;
            var emergency = Make("Emergency Lamp", region.transform).AddComponent<Light>(); emergency.transform.position = inside + Vector3.up;
            emergency.type = LightType.Point; emergency.range = 4; emergency.color = Color.red; emergency.intensity = 1.2f; emergency.enabled = false;
            flow.emergencyLights = new[] { emergency };
            notes.Add("Submarine bounds " + BoundsOf(sub) + "; Door " + doorBounds + "; approach " + approach + "; MUST check corridor clearance in headset.");
        }
    }
}
#endif
