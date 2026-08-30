#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using OilRigAssembly.Runtime;

namespace OilRigAssembly.Editor
{
    [InitializeOnLoad]
    public static class OilRigPlatformBuilder
    {
        public const string ScenePath = "Assets/OilRigAssembly/Scenes/OilRig_AboveWater.unity";
        const string GeneratedRootName = "__OilRigGenerated";
        const string ManualRootName = "ManualContent_Preserved";
        const string AssetRoot = "Assets/SierraDivision/Oil_Rig/Prefabs";
        const string GeneratedAssetRoot = "Assets/OilRigAssembly/Generated";
        const string PreviewRoot = "Assets/OilRigAssembly/Previews";
        const string ReportRoot = "Assets/OilRigAssembly/Reports";
        const int BuilderVersion = 16;

        const float WaterlineY = 0f;
        const float ServiceDeckY = 2.4f;
        const float MainDeckY = 6f;
        const float ProcessDeckY = 10f;
        const float UpperDeckY = 10f;
        const float RoofY = 14f;

        static readonly string Floor4 = P("Floor/Floor_4x4M.prefab");
        static readonly string Railing2 = P("Catwalks/Catwalk_Floor_Railing_2M.prefab");
        static readonly string Stairs2 = P("Catwalks/Catwalk_Stairs_2M.prefab");
        static readonly string Wall4 = P("Wall_Panels/Wall_Panel_Beams_4x4M.prefab");
        static readonly string WallWindow4 = P("Wall_Panels/Wall_Panel_Beams_Windows_4x4M.prefab");
        static readonly string WallDoor4 = P("Wall_Panels/Wall_Panel_Beams_Door_Center_4x4M.prefab");
        static readonly string LargePillar10 = P("Support_Columns/Large_Pillar_10M.prefab");
        static readonly string MediumPillar5 = P("Support_Columns/Medium_Pillar_5M.prefab");
        static readonly string Beam12 = P("Beams_80x40/Beam_80x40_12M.prefab");
        static readonly string Brace6 = P("Beams_40x20/Beam_40x20_6M.prefab");
        static readonly string Catwalk2 = P("Catwalks/Catwalk_2M.prefab");
        static readonly string CatwalkFloor2 = P("Catwalks/Catwalk_Floor_2M.prefab");
        static readonly string PipeLarge4 = P("Pipes_Large/Pipe_Large_Strt_4m_01_.prefab");
        static readonly string PipeMedium4 = P("Pipes_Medium/Pipe_Med_Strt_4m_01_.prefab");
        static readonly string PipeSmall4 = P("Pipes_Small/Pipe_Sml_Strt_4m_01_.prefab");
        static PreviewCaptureState previewCapture;

        sealed class PreviewCaptureState
        {
            public Scene scene;
            public Scene previousActive;
            public bool openedHere;
            public GameObject root;
            public Camera[] cameras;
            public int index;
            public Dictionary<GameObject, int> originalLayers;
            public double nextCaptureTime;
        }

        static OilRigPlatformBuilder()
        {
            EditorApplication.delayCall += AutoBuildIfNeeded;
            EditorApplication.delayCall += AutoCaptureIfNeeded;
        }

        [MenuItem("Tools/Oil Rig Assembly/Rebuild Above-Water Platform")]
        public static void RebuildFromMenu()
        {
            BuildScene(true);
        }

        [MenuItem("Tools/Oil Rig Assembly/Open Generated Platform %#&F9")]
        public static void OpenGeneratedScene()
        {
            if (!File.Exists(ScenePath))
            {
                BuildScene(true);
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Oil Rig Assembly/Validate Generated Platform")]
        public static void ValidateFromMenu()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            GameObject root = FindRoot(scene, GeneratedRootName);
            if (root == null)
            {
                Debug.LogError("[OilRig] Generated root was not found. Rebuild the platform first.");
            }
            else
            {
                WriteValidationReport(scene, root);
            }

            if (openedHere)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Tools/Oil Rig Assembly/Capture Preview Images %#&F10")]
        public static void CapturePreviewsFromMenu()
        {
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            GameObject root = FindRoot(scene, GeneratedRootName);
            if (root == null)
            {
                Debug.LogError("[OilRig] Generated root was not found. Rebuild the platform first.");
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            QueuePreviewCapture(scene, previousActive, openedHere, root);
        }

        // Entry point for optional command-line verification.
        public static void BuildFromCommandLine()
        {
            BuildScene(true);
        }

        static void AutoBuildIfNeeded()
        {
            string versionKey = "OilRigAssembly.AutoBuildVersion." + Application.dataPath.GetHashCode();
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += AutoBuildIfNeeded;
                return;
            }

            if (EditorPrefs.GetInt(versionKey, 0) >= BuilderVersion) return;

            BuildScene(false);
            EditorPrefs.SetInt(versionKey, BuilderVersion);
        }

        static void AutoCaptureIfNeeded()
        {
            string buildKey = "OilRigAssembly.AutoBuildVersion." + Application.dataPath.GetHashCode();
            string captureKey = "OilRigAssembly.AutoCaptureVersion." + Application.dataPath.GetHashCode();
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorPrefs.GetInt(buildKey, 0) < BuilderVersion)
            {
                EditorApplication.delayCall += AutoCaptureIfNeeded;
                return;
            }
            if (EditorPrefs.GetInt(captureKey, 0) >= BuilderVersion) return;

            EditorPrefs.SetInt(captureKey, BuilderVersion);
            CapturePreviewsFromMenu();
        }

        static void BuildScene(bool explicitRebuild)
        {
            EnsureFolders();

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;

            if (openedHere)
            {
                scene = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }

            SceneManager.SetActiveScene(scene);
            GameObject oldRoot = FindRoot(scene, GeneratedRootName);
            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot);
            }

            GameObject manualRoot = FindRoot(scene, ManualRootName);
            if (manualRoot == null)
            {
                manualRoot = new GameObject(ManualRootName);
                SceneManager.MoveGameObjectToScene(manualRoot, scene);
            }

            GameObject root = new GameObject(GeneratedRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Transform floatingRoot = Group("FloatingPlatformRoot", root.transform);
            Transform structure = Group("Structure", floatingRoot);
            Transform architecture = Group("Architecture", floatingRoot);
            Transform equipment = Group("Equipment", floatingRoot);
            Transform props = Group("Props", floatingRoot);
            Transform localLighting = Group("Platform_Lighting", floatingRoot);
            Transform lighting = Group("World_Lighting_And_Atmosphere", root.transform);
            Transform preview = Group("Preview", root.transform);
            Transform markers = Group("Future_Gameplay_Markers", floatingRoot);

            CreateMarker("Waterline_Future_Y0", markers, new Vector3(0f, WaterlineY, 0f));
            CreateMarker("VR_Spawn_Future", markers, new Vector3(-16f, MainDeckY + 0.05f, -9f));
            CreateMarker("Dive_Entry_Future", markers, new Vector3(19f, MainDeckY + 0.05f, -9f));

            ConfigureFloatingRoot(floatingRoot, markers);
            BuildCompactFloatingHull(structure);
            BuildCompactDecksAndSafety(structure);
            BuildCompactArchitecture(architecture, props);
            BuildCompactDerrick(structure);
            BuildCompactEquipment(equipment, props);
            BuildCompactLighting(localLighting);
            BuildCompactPreviewCameras(preview);
            ConfigureAtmosphere(lighting);
            AddCompactReflectionHelpers(floatingRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            WriteValidationReport(scene, root);
            AssetDatabase.SaveAssets();
            if (explicitRebuild)
            {
                QueuePreviewCapture(scene, previousActive, openedHere, root);
            }

            Debug.Log($"[OilRig] {(explicitRebuild ? "Rebuilt" : "Created")} scene: {ScenePath}");
        }

        enum BoundsAnchor
        {
            Center,
            BottomCenter,
            TopCenter
        }

        static void ConfigureFloatingRoot(Transform floatingRoot, Transform markers)
        {
            Rigidbody body = floatingRoot.gameObject.AddComponent<Rigidbody>();
            body.mass = 850000f;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            Transform samples = Group("Crest_Buoyancy_Sample_Points", markers);
            Vector3[] localPositions =
            {
                new Vector3(-14f, WaterlineY, -8f),
                new Vector3(14f, WaterlineY, -8f),
                new Vector3(-14f, WaterlineY, 8f),
                new Vector3(14f, WaterlineY, 8f)
            };
            Transform[] samplePoints = new Transform[localPositions.Length];
            string[] names = { "Sample_SW", "Sample_SE", "Sample_NW", "Sample_NE" };
            for (int i = 0; i < localPositions.Length; i++)
            {
                GameObject point = new GameObject(names[i]);
                point.transform.SetParent(samples, false);
                point.transform.localPosition = localPositions[i];
                samplePoints[i] = point.transform;
            }

            CrestFloatingPlatform follower = floatingRoot.gameObject.AddComponent<CrestFloatingPlatform>();
            follower.samplePoints = samplePoints;
            follower.minimumWaveLength = 14f;
            follower.waterlineOffset = 0f;
            follower.heaveSmoothTime = 0.85f;
            follower.rotationResponse = 1.45f;
            follower.maximumTilt = 4f;
        }

        static void BuildCompactFloatingHull(Transform parent)
        {
            Transform hull = Group("Semi_Submersible_Floating_Hull_34x19", parent);
            Material hullMaterial = GetSourceMaterial(P("Support_Pillars/Pillars_Support_Merged_01_.prefab"));
            Material beamMaterial = GetSourceMaterial(Beam12);

            CreateChamferedPontoon(hull, "Port_Pontoon", new Vector3(-1f, -1.15f, -7.25f),
                34f, 5.3f, 4.8f, hullMaterial);
            CreateChamferedPontoon(hull, "Starboard_Pontoon", new Vector3(-1f, -1.15f, 7.25f),
                34f, 5.3f, 4.8f, hullMaterial);

            Transform columns = Group("Four_Load_Bearing_Columns", hull);
            foreach (float x in new[] { -11f, 9f })
            {
                foreach (float z in new[] { -7.25f, 7.25f })
                {
                    CreateBoxVolume(columns, $"Buoyancy_Column_{x:0}_{z:0}", new Vector3(x, 3.55f, z),
                        new Vector3(3.4f, 4.1f, 3.4f), hullMaterial);
                    CreateStructuralBeam(columns, new Vector3(x, 1.5f, z), new Vector3(x, MainDeckY - 0.15f, z),
                        0.8f, 0.8f, beamMaterial, $"Column_Core_{x:0}_{z:0}");
                }
            }

            Transform underDeck = Group("Exact_Underdeck_Grillage", parent);
            foreach (float z in new[] { -10f, -6f, -2f, 2f, 6f, 10f })
            {
                CreateStructuralBeam(underDeck, new Vector3(-20f, MainDeckY - 0.45f, z),
                    new Vector3(20f, MainDeckY - 0.45f, z), 0.8f, 0.4f, beamMaterial, $"Transverse_{z:0}");
            }
            foreach (float x in new[] { -18f, -10f, -2f, 6f, 14f, 20f })
            {
                CreateStructuralBeam(underDeck, new Vector3(x, MainDeckY - 0.75f, -12f),
                    new Vector3(x, MainDeckY - 0.75f, 12f), 0.8f, 0.4f, beamMaterial, $"Longitudinal_{x:0}");
            }

            Transform braces = Group("Column_To_Deck_Bracing", parent);
            Vector3[,] bracePairs =
            {
                { new Vector3(-12.7f, 2f, -7.25f), new Vector3(-18f, MainDeckY - 0.7f, -7.25f) },
                { new Vector3(-9.3f, 2f, -7.25f), new Vector3(-4f, MainDeckY - 0.7f, -7.25f) },
                { new Vector3(7.3f, 2f, -7.25f), new Vector3(2f, MainDeckY - 0.7f, -7.25f) },
                { new Vector3(10.7f, 2f, -7.25f), new Vector3(16f, MainDeckY - 0.7f, -7.25f) },
                { new Vector3(-12.7f, 2f, 7.25f), new Vector3(-18f, MainDeckY - 0.7f, 7.25f) },
                { new Vector3(-9.3f, 2f, 7.25f), new Vector3(-4f, MainDeckY - 0.7f, 7.25f) },
                { new Vector3(7.3f, 2f, 7.25f), new Vector3(2f, MainDeckY - 0.7f, 7.25f) },
                { new Vector3(10.7f, 2f, 7.25f), new Vector3(16f, MainDeckY - 0.7f, 7.25f) }
            };
            for (int i = 0; i < bracePairs.GetLength(0); i++)
            {
                CreateStructuralBeam(braces, bracePairs[i, 0], bracePairs[i, 1], 0.4f, 0.25f,
                    beamMaterial, $"Pontoon_To_Deck_Brace_{i:00}");
            }

            Transform service = Group("Supported_Lower_Maintenance_Deck", parent);
            CreateStructuralBeam(service, new Vector3(-8f, ServiceDeckY - 0.35f, -7.25f), new Vector3(-8f, ServiceDeckY - 0.35f, 7.25f), 0.5f, 0.3f, beamMaterial, "Service_Crossbeam_West");
            CreateStructuralBeam(service, new Vector3(4f, ServiceDeckY - 0.35f, -7.25f), new Vector3(4f, ServiceDeckY - 0.35f, 7.25f), 0.5f, 0.3f, beamMaterial, "Service_Crossbeam_East");
            BuildRectFloorAligned(service, -8f, 4f, -4f, 4f, ServiceDeckY);
        }

        static void BuildCompactDecksAndSafety(Transform parent)
        {
            Transform main = Group("Compact_Main_Deck_40x24", parent);
            BuildRectFloorAligned(main, -20f, 20f, -12f, 12f, MainDeckY);

            Transform cranePad = Group("East_Crane_Cantilever_4x12", parent);
            BuildRectFloorAligned(cranePad, 20f, 24f, -6f, 6f, MainDeckY);
            Material beamMaterial = GetSourceMaterial(Beam12);
            foreach (float z in new[] { -5f, 0f, 5f })
            {
                CreateStructuralBeam(cranePad, new Vector3(16f, MainDeckY - 0.65f, z),
                    new Vector3(24f, MainDeckY - 0.65f, z), 0.65f, 0.35f, beamMaterial, $"Cantilever_Support_{z:0}");
            }

            Transform process = Group("Supported_Process_Deck_12x8", parent);
            BuildRectFloorAligned(process, 2f, 14f, 0f, 8f, ProcessDeckY);
            foreach (float x in new[] { 2f, 8f, 14f })
            {
                foreach (float z in new[] { 0f, 8f })
                {
                    CreateStructuralBeam(process, new Vector3(x, MainDeckY, z), new Vector3(x, ProcessDeckY, z),
                        0.45f, 0.45f, beamMaterial, $"Process_Column_{x:0}_{z:0}");
                }
            }

            Transform railings = Group("Continuous_Perimeter_Safety", parent);
            for (int i = 0; i < 20; i++)
            {
                float x = -19f + i * 2f;
                if (x < 17f || x > 19f)
                {
                    PlacePrefabAnchored(Railing2, railings, new Vector3(x, MainDeckY, -12f), Vector3.zero,
                        Vector3.one, $"South_Rail_{i:00}", BoundsAnchor.BottomCenter);
                }
                PlacePrefabAnchored(Railing2, railings, new Vector3(x, MainDeckY, 12f), new Vector3(0f, 180f, 0f),
                    Vector3.one, $"North_Rail_{i:00}", BoundsAnchor.BottomCenter);
            }
            for (int i = 0; i < 12; i++)
            {
                float z = -11f + i * 2f;
                PlacePrefabAnchored(Railing2, railings, new Vector3(-20f, MainDeckY, z), new Vector3(0f, -90f, 0f),
                    Vector3.one, $"West_Rail_{i:00}", BoundsAnchor.BottomCenter);
                if (z < -7f || z > 7f)
                {
                    PlacePrefabAnchored(Railing2, railings, new Vector3(20f, MainDeckY, z), new Vector3(0f, 90f, 0f),
                        Vector3.one, $"East_Rail_{i:00}", BoundsAnchor.BottomCenter);
                }
            }
            AddRailingRunXAligned(railings, 20f, 24f, -6f, MainDeckY, false, "CranePad_South");
            AddRailingRunXAligned(railings, 20f, 24f, 6f, MainDeckY, true, "CranePad_North");
            AddRailingRunZAligned(railings, 24f, -6f, 6f, MainDeckY, true, "CranePad_East");

            Transform access = Group("Supported_Access_System", parent);
            PlacePrefabAnchored(Stairs2, access, new Vector3(-1f, MainDeckY, 9f), new Vector3(0f, 90f, 0f),
                Vector3.one, "Upper_Stair_Lower", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(Stairs2, access, new Vector3(1f, MainDeckY + 2f, 9f), new Vector3(0f, 90f, 0f),
                Vector3.one, "Upper_Stair_Upper", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Ladders/Ladder_Cage_Module_2.prefab"), access,
                new Vector3(18.5f, 1.5f, -10.5f), Vector3.zero, Vector3.one, "Sea_Access_Ladder_Lower", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Ladders/Ladder_Cage_Module_3_End.prefab"), access,
                new Vector3(18.5f, 3.5f, -10.5f), Vector3.zero, Vector3.one, "Sea_Access_Ladder_Upper", BoundsAnchor.BottomCenter);
        }

        static void BuildCompactArchitecture(Transform parent, Transform props)
        {
            Material beamMaterial = GetSourceMaterial(Beam12);

            Transform south = Group("MainDeck_Preparation_And_Electrical_Block", parent);
            BuildCompactSouthRoomShell(south, -18f, -6f, -10f, -2f, MainDeckY);
            BuildRectFloorAligned(south, -18f, -6f, -10f, -2f, ProcessDeckY);
            CreateMarker("Room_Equipment_Preparation", south, new Vector3(-14f, MainDeckY, -6f));
            CreateMarker("Room_Electrical_Maintenance", south, new Vector3(-8f, MainDeckY, -6f));

            Transform lowerNorth = Group("MainDeck_Log_And_Stair_Block", parent);
            BuildBuildingShellAligned(lowerNorth, -18f, -2f, 2f, 10f, MainDeckY, 0, 2);
            PlaceWallRunZAligned(lowerNorth, -10f, 2f, 10f, MainDeckY, 0, -1, "LogRoom_Partition");
            BuildRectFloorAligned(lowerNorth, -18f, -2f, 2f, 10f, ProcessDeckY);
            CreateMarker("Room_Log_Monitor", lowerNorth, new Vector3(-14f, MainDeckY, 6f));

            Transform upper = Group("Supported_Upper_Control_And_Analysis_Block", parent);
            foreach (float x in new[] { -18f, -10f, -2f })
            {
                foreach (float z in new[] { 2f, 10f })
                {
                    CreateStructuralBeam(upper, new Vector3(x, MainDeckY, z), new Vector3(x, ProcessDeckY, z),
                        0.5f, 0.5f, beamMaterial, $"Upper_Block_Column_{x:0}_{z:0}");
                }
            }
            BuildBuildingShellAligned(upper, -18f, -2f, 2f, 10f, ProcessDeckY, 0, 2);
            PlaceWallRunZAligned(upper, -10f, 2f, 10f, ProcessDeckY, 0, -1, "Analysis_Partition");
            BuildRectFloorAligned(upper, -18f, -2f, 2f, 10f, RoofY);
            CreateMarker("Room_Main_Control", upper, new Vector3(-14f, ProcessDeckY, 6f));
            CreateMarker("Room_BlackBox_Analysis", upper, new Vector3(-6f, ProcessDeckY, 6f));

            Transform roofPlant = Group("Roof_Plant_On_Supported_Roof", parent);
            PlacePrefabAnchored(P("AC_Units/AC_Unit_Main.prefab"), roofPlant, new Vector3(-15f, RoofY, 5f),
                Vector3.zero, Vector3.one, "Roof_AC_Main", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Electrical_Structures/ACunit_Small_Module_2_.prefab"), roofPlant,
                new Vector3(-7f, RoofY, 6f), Vector3.zero, Vector3.one, "Roof_Cooler", BoundsAnchor.BottomCenter);

            DressCompactRooms(props);
        }

        static void BuildCompactSouthRoomShell(Transform parent, float xMin, float xMax, float zMin, float zMax, float y)
        {
            for (int i = 0; i < 3; i++)
            {
                string southPath = i == 0 || i == 2 ? WallDoor4 : Wall4;
                PlacePrefabAnchored(southPath, parent, new Vector3(xMin + 2f + i * 4f, y, zMin),
                    new Vector3(0f, 90f, 0f), Vector3.one, $"South_Wall_{i:00}", BoundsAnchor.BottomCenter);
                PlacePrefabAnchored(i == 1 ? WallWindow4 : Wall4, parent, new Vector3(xMin + 2f + i * 4f, y, zMax),
                    new Vector3(0f, -90f, 0f), Vector3.one, $"North_Wall_{i:00}", BoundsAnchor.BottomCenter);
            }
            PlaceWallRunZAligned(parent, xMin, zMin, zMax, y, -1, 0, "West");
            PlaceWallRunZAligned(parent, xMax, zMin, zMax, y, -1, 0, "East");
            PlaceWallRunZAligned(parent, -10f, zMin, zMax, y, 1, -1, "Electrical_Partition");
        }

        static void DressCompactRooms(Transform props)
        {
            Transform preparation = Group("Equipment_Preparation_Interior", props);
            PlacePrefabAnchored(P("Crate_Metal/BarrelCrate_Rectangular_01_Tarp.prefab"), preparation,
                new Vector3(-15f, MainDeckY, -5f), new Vector3(0f, 90f, 0f), Vector3.one,
                "Dive_Equipment_Crate", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Barrel_Metal/Barrel_Oil_Metal_01_01_.prefab"), preparation,
                new Vector3(-17f, MainDeckY, -8f), Vector3.zero, Vector3.one, "Prep_Barrel", BoundsAnchor.BottomCenter);

            Transform electrical = Group("Electrical_Maintenance_Interior", props);
            PlacePrefabAnchored(P("Electrical_Structures/ElectricBox_Module_1.prefab"), electrical,
                new Vector3(-7f, MainDeckY, -8f), new Vector3(0f, 180f, 0f), Vector3.one,
                "Electrical_Panel_A", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Electrical_Structures/ElectricBox_Module_3.prefab"), electrical,
                new Vector3(-7f, MainDeckY, -4f), new Vector3(0f, 180f, 0f), Vector3.one,
                "Electrical_Panel_B", BoundsAnchor.BottomCenter);

            Transform log = Group("Log_Monitor_Interior", props);
            PlacePrefabAnchored(P("Control_Console/Control_Console_A.prefab"), log,
                new Vector3(-14f, MainDeckY, 7f), new Vector3(0f, 180f, 0f), Vector3.one,
                "Log_Console", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Electrical_Structures/ElectricBox_Module_1.prefab"), log,
                new Vector3(-17f, MainDeckY, 4f), new Vector3(0f, 90f, 0f), Vector3.one,
                "Log_Data_Rack", BoundsAnchor.BottomCenter);

            Transform control = Group("Main_Control_Interior", props);
            PlacePrefabAnchored(P("Control_Console/Control_Console_A.prefab"), control,
                new Vector3(-15f, ProcessDeckY, 7f), new Vector3(0f, 180f, 0f), Vector3.one,
                "Control_Console_A", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Control_Console/Control_Console_B.prefab"), control,
                new Vector3(-12f, ProcessDeckY, 7f), new Vector3(0f, 180f, 0f), Vector3.one,
                "Control_Console_B", BoundsAnchor.BottomCenter);

            Transform analysis = Group("BlackBox_Analysis_Interior", props);
            PlacePrefabAnchored(P("Control_Console/Control_Console_A.prefab"), analysis,
                new Vector3(-7f, ProcessDeckY, 7f), new Vector3(0f, 180f, 0f), Vector3.one,
                "BlackBox_Analysis_Console", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Crate_Metal/BarrelCrate_Square_02_.prefab"), analysis,
                new Vector3(-5f, ProcessDeckY, 4f), Vector3.zero, Vector3.one,
                "Recovered_BlackBox_Placeholder", BoundsAnchor.BottomCenter);
        }

        static void BuildCompactDerrick(Transform parent)
        {
            Transform derrick = Group("Exact_Connected_Drilling_Derrick", parent);
            Material material = GetSourceMaterial(Brace6);
            Vector3 center = new Vector3(8f, 0f, 4f);
            float[] levels = { ProcessDeckY, 16f, 22f, 28f };
            float[] halfWidths = { 4.5f, 3.6f, 2.8f, 2f };
            float[] halfDepths = { 3.5f, 3f, 2.5f, 1.8f };

            for (int level = 0; level < levels.Length; level++)
            {
                float y = levels[level];
                float hx = halfWidths[level];
                float hz = halfDepths[level];
                Vector3 sw = new Vector3(center.x - hx, y, center.z - hz);
                Vector3 se = new Vector3(center.x + hx, y, center.z - hz);
                Vector3 nw = new Vector3(center.x - hx, y, center.z + hz);
                Vector3 ne = new Vector3(center.x + hx, y, center.z + hz);
                CreateStructuralBeam(derrick, sw, se, 0.35f, 0.25f, material, $"Ring_S_{level}");
                CreateStructuralBeam(derrick, nw, ne, 0.35f, 0.25f, material, $"Ring_N_{level}");
                CreateStructuralBeam(derrick, sw, nw, 0.35f, 0.25f, material, $"Ring_W_{level}");
                CreateStructuralBeam(derrick, se, ne, 0.35f, 0.25f, material, $"Ring_E_{level}");
                if (level == levels.Length - 1) continue;

                float ny = levels[level + 1];
                float nhx = halfWidths[level + 1];
                float nhz = halfDepths[level + 1];
                Vector3 nsw = new Vector3(center.x - nhx, ny, center.z - nhz);
                Vector3 nse = new Vector3(center.x + nhx, ny, center.z - nhz);
                Vector3 nnw = new Vector3(center.x - nhx, ny, center.z + nhz);
                Vector3 nne = new Vector3(center.x + nhx, ny, center.z + nhz);
                CreateStructuralBeam(derrick, sw, nsw, 0.45f, 0.35f, material, $"Leg_SW_{level}");
                CreateStructuralBeam(derrick, se, nse, 0.45f, 0.35f, material, $"Leg_SE_{level}");
                CreateStructuralBeam(derrick, nw, nnw, 0.45f, 0.35f, material, $"Leg_NW_{level}");
                CreateStructuralBeam(derrick, ne, nne, 0.45f, 0.35f, material, $"Leg_NE_{level}");
                CreateStructuralBeam(derrick, sw, nse, 0.25f, 0.2f, material, $"Brace_S_A_{level}");
                CreateStructuralBeam(derrick, se, nsw, 0.25f, 0.2f, material, $"Brace_S_B_{level}");
                CreateStructuralBeam(derrick, nw, nne, 0.25f, 0.2f, material, $"Brace_N_A_{level}");
                CreateStructuralBeam(derrick, ne, nnw, 0.25f, 0.2f, material, $"Brace_N_B_{level}");
            }
        }

        static void BuildCompactEquipment(Transform equipment, Transform props)
        {
            Transform crane = Group("Supported_East_Crane", equipment);
            GameObject baseObject = PlacePrefabAnchored(P("Crane/Crane_Bottom_01.prefab"), crane,
                new Vector3(22f, MainDeckY, 0f), Vector3.zero, Vector3.one, "Crane_Base", BoundsAnchor.BottomCenter);
            Bounds baseBounds = GetWorldBounds(baseObject);
            float craneJointY = baseBounds.size.sqrMagnitude > 0f ? baseBounds.max.y : MainDeckY + 7f;
            // This is a long articulated assembly. Its authored root is the slew-ring
            // connector; centering its complete AABB would shift the base by the boom length.
            PlacePrefab(P("Crane/Crane_Top_01.prefab"), crane,
                new Vector3(22f, craneJointY, 0f), new Vector3(0f, -55f, 0f), Vector3.one,
                "Crane_Upper", false);

            Transform process = Group("Dense_Connected_Process_Area", equipment);
            PlacePrefabAnchored(P("Tank/Fuel_Tank_01_.prefab"), process, new Vector3(13f, MainDeckY, -8f),
                new Vector3(0f, 90f, 0f), Vector3.one, "Fuel_Separation_Tank", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Electrical_Structures/Cooler_Module_02_.prefab"), process,
                new Vector3(-1f, MainDeckY, -1f), Vector3.zero, Vector3.one, "Separator_Cooler", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Electrical_Structures/ACunit_Big_Module_4.prefab"), process,
                new Vector3(5f, MainDeckY, -8f), new Vector3(0f, 90f, 0f), Vector3.one,
                "Compression_Skid", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Electrical_Structures/Cooler_Module_03_.prefab"), process,
                new Vector3(17f, MainDeckY, 9f), new Vector3(0f, -90f, 0f), Vector3.one,
                "Main_Cooler", BoundsAnchor.BottomCenter);

            BuildCompactPipeRack(equipment);
            BuildCompactMarineRisers(equipment);

            Transform lower = Group("Lower_Deck_Pumps", equipment);
            PlacePrefabAnchored(P("Electrical_Structures/ACunit_Small_Module_3_.prefab"), lower,
                new Vector3(-5f, ServiceDeckY, -1.5f), Vector3.zero, Vector3.one,
                "Lower_Pump_Skid_A", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Electrical_Structures/ElectricBox_Module_2.prefab"), lower,
                new Vector3(1f, ServiceDeckY, 1.5f), new Vector3(0f, 180f, 0f), Vector3.one,
                "Lower_Power_Skid", BoundsAnchor.BottomCenter);

            Transform clutter = Group("Purposeful_Deck_Clutter", props);
            PlacePrefabAnchored(P("Crate_Metal/BarrelCrate_Rectangular_01_Tarp.prefab"), clutter,
                new Vector3(17f, MainDeckY, -10f), new Vector3(0f, 20f, 0f), Vector3.one,
                "Covered_Supply_Crate", BoundsAnchor.BottomCenter);
            PlacePrefabAnchored(P("Crate_Metal/BarrelCrate_Square_02_.prefab"), clutter,
                new Vector3(18.5f, MainDeckY, 7.5f), new Vector3(0f, -15f, 0f), Vector3.one,
                "Maintenance_Crate", BoundsAnchor.BottomCenter);
            for (int i = 0; i < 4; i++)
            {
                PlacePrefabAnchored(P("Barrel_Metal/Barrel_Oil_Metal_02_02_.prefab"), clutter,
                    new Vector3(-4f + (i % 2) * 1.1f, MainDeckY, -10.2f + (i / 2) * 1.1f),
                    new Vector3(0f, i * 31f, 0f), Vector3.one, $"Oil_Barrel_{i:00}", BoundsAnchor.BottomCenter);
            }
        }

        static void BuildCompactPipeRack(Transform parent)
        {
            Transform rack = Group("Exact_Supported_Pipe_Rack", parent);
            Material beamMaterial = GetSourceMaterial(Brace6);
            foreach (float x in new[] { -4f, 2f, 8f, 14f, 18f })
            {
                foreach (float z in new[] { -9f, -3f })
                {
                    CreateStructuralBeam(rack, new Vector3(x, MainDeckY, z), new Vector3(x, 11.6f, z),
                        0.3f, 0.3f, beamMaterial, $"Rack_Post_{x:0}_{z:0}");
                }
                CreateStructuralBeam(rack, new Vector3(x, 11.25f, -9f), new Vector3(x, 11.25f, -3f),
                    0.35f, 0.25f, beamMaterial, $"Rack_Crossbar_{x:0}");
            }

            Vector3[] starts =
            {
                new Vector3(-4f, 10.9f, -4.3f),
                new Vector3(-4f, 10.45f, -6f),
                new Vector3(-4f, 9.95f, -7.7f)
            };
            for (int i = 0; i < starts.Length; i++)
            {
                float radius = i == 0 ? 0.24f : 0.14f;
                string source = i == 0 ? PipeMedium4 : PipeSmall4;
                CreateExactPipe(rack, starts[i], new Vector3(18f, starts[i].y, starts[i].z),
                    radius, source, $"Continuous_Header_{i:00}");
                string valve = i == 0 ? "Valves_Medium/Valve_Medium_02_.prefab" : "Valves_Small/Valve_Small_01_.prefab";
                PlacePrefabAnchored(P(valve), rack, new Vector3(5f + i * 2f, starts[i].y, starts[i].z),
                    new Vector3(0f, 90f, 0f), Vector3.one, $"Header_Valve_{i:00}", BoundsAnchor.Center);
            }

            CreateExactPipe(rack, new Vector3(-1f, MainDeckY + 0.8f, -1f),
                new Vector3(-1f, 10.9f, -4.3f), 0.24f, PipeMedium4, "Cooler_Header_Connection");
            CreateExactPipe(rack, new Vector3(13f, MainDeckY + 0.8f, -8f),
                new Vector3(13f, 9.95f, -7.7f), 0.14f, PipeSmall4, "Tank_Header_Connection");
        }

        static void BuildCompactMarineRisers(Transform parent)
        {
            Transform risers = Group("Short_Flexible_Riser_Interface", parent);
            float[] zValues = { -4.3f, -6f, -7.7f };
            float[] yValues = { 10.9f, 10.45f, 9.95f };
            for (int i = 0; i < zValues.Length; i++)
            {
                float x = 18f;
                float radius = i == 0 ? 0.24f : 0.14f;
                string source = i == 0 ? PipeMedium4 : PipeSmall4;
                CreateExactPipe(risers, new Vector3(x, -3.8f, zValues[i]),
                    new Vector3(x, yValues[i], zValues[i]), radius, source, $"Marine_Riser_{i:00}");
                CreateMarker($"Future_Flexible_Riser_Connector_{i:00}", risers,
                    new Vector3(x, -3.8f, zValues[i]));
            }
        }

        static void BuildCompactLighting(Transform parent)
        {
            CreateSpotLight(parent, "MainDeck_West_Worklight", new Vector3(-18f, 10.5f, -11f),
                new Vector3(0.35f, -1f, 0.25f), true);
            CreateSpotLight(parent, "MainDeck_East_Worklight", new Vector3(18f, 11f, -10f),
                new Vector3(-0.25f, -1f, 0.3f), true);
            CreateSpotLight(parent, "ProcessDeck_Worklight", new Vector3(1f, 15f, 1f),
                new Vector3(0.2f, -1f, -0.2f), true);
            CreateSpotLight(parent, "UpperBlock_Worklight", new Vector3(-18f, 16f, 1f),
                new Vector3(0.2f, -1f, 0.4f), false);
            CreateSpotLight(parent, "LowerDeck_Worklight", new Vector3(-7f, 5.2f, -4f),
                new Vector3(0.2f, -1f, 0.4f), false);
            CreatePointLight(parent, "ControlRoom_WarmLight", new Vector3(-14f, 12f, 6f),
                new Color(1f, 0.58f, 0.3f), 44f, 8f);
            CreatePointLight(parent, "AnalysisRoom_WarmLight", new Vector3(-6f, 12f, 6f),
                new Color(1f, 0.48f, 0.25f), 38f, 7f);
            CreatePointLight(parent, "LogRoom_Light", new Vector3(-14f, 8f, 6f),
                new Color(0.35f, 0.65f, 1f), 28f, 7f);
        }

        static void BuildCompactPreviewCameras(Transform parent)
        {
            CreatePreviewCamera(parent, "Overview_Camera", new Vector3(54f, 34f, -58f), new Vector3(0f, 10f, 0f), true);
            CreatePreviewCamera(parent, "Hull_And_Supports_Camera", new Vector3(36f, 7f, -38f), new Vector3(-1f, 3f, 0f), false);
            CreatePreviewCamera(parent, "MainDeck_Camera", new Vector3(30f, 13f, -30f), new Vector3(0f, 8f, 0f), false);
            CreatePreviewCamera(parent, "Rooms_Camera", new Vector3(-36f, 18f, -25f), new Vector3(-10f, 10f, 3f), false);
            CreatePreviewCamera(parent, "PipeRack_Camera", new Vector3(29f, 13f, -24f), new Vector3(7f, 9f, -6f), false);
            CreatePreviewCamera(parent, "Derrick_Camera", new Vector3(31f, 24f, 31f), new Vector3(8f, 18f, 4f), false);
        }

        static void AddCompactReflectionHelpers(Transform parent)
        {
            Transform probes = Group("Moving_Reflection_Probes", parent);
            CreateRealtimeProbe(probes, "MainDeck_Realtime_Probe", new Vector3(0f, 9f, 0f), new Vector3(38f, 16f, 22f));
            CreateRealtimeProbe(probes, "UpperBlock_Realtime_Probe", new Vector3(-10f, 12f, 6f), new Vector3(18f, 8f, 10f));
        }

        static void CreateRealtimeProbe(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = size;
            probe.boxProjection = true;
            probe.resolution = 128;
        }

        static void BuildBuildingShellAligned(Transform parent, float xMin, float xMax, float zMin, float zMax,
            float y, int southDoorIndex, int northWindowIndex)
        {
            PlaceWallRunXAligned(parent, xMin, xMax, zMin, y, southDoorIndex, -1, "South");
            PlaceWallRunXAligned(parent, xMin, xMax, zMax, y, -1, northWindowIndex, "North");
            PlaceWallRunZAligned(parent, xMin, zMin, zMax, y, -1, 0, "West");
            PlaceWallRunZAligned(parent, xMax, zMin, zMax, y, -1, 0, "East");
        }

        static void PlaceWallRunXAligned(Transform parent, float xMin, float xMax, float z, float y,
            int doorIndex, int windowIndex, string prefix)
        {
            int count = Mathf.RoundToInt((xMax - xMin) / 4f);
            for (int i = 0; i < count; i++)
            {
                string path = i == doorIndex ? WallDoor4 : i == windowIndex ? WallWindow4 : Wall4;
                PlacePrefabAnchored(path, parent, new Vector3(xMin + 2f + 4f * i, y, z),
                    new Vector3(0f, 90f, 0f), Vector3.one, $"{prefix}_Wall_{i:00}", BoundsAnchor.BottomCenter);
            }
        }

        static void PlaceWallRunZAligned(Transform parent, float x, float zMin, float zMax, float y,
            int doorIndex, int windowIndex, string prefix)
        {
            int count = Mathf.RoundToInt((zMax - zMin) / 4f);
            for (int i = 0; i < count; i++)
            {
                string path = i == doorIndex ? WallDoor4 : i == windowIndex ? WallWindow4 : Wall4;
                PlacePrefabAnchored(path, parent, new Vector3(x, y, zMin + 2f + 4f * i), Vector3.zero,
                    Vector3.one, $"{prefix}_Wall_{i:00}", BoundsAnchor.BottomCenter);
            }
        }

        static void BuildRectFloorAligned(Transform parent, float xMin, float xMax, float zMin, float zMax, float y)
        {
            int xCount = Mathf.RoundToInt((xMax - xMin) / 4f);
            int zCount = Mathf.RoundToInt((zMax - zMin) / 4f);
            for (int ix = 0; ix < xCount; ix++)
            {
                float centerX = xMin + 2f + ix * 4f;
                for (int iz = 0; iz < zCount; iz++)
                {
                    float centerZ = zMin + 2f + iz * 4f;
                    PlacePrefabAnchored(Floor4, parent, new Vector3(centerX, y, centerZ), Vector3.zero,
                        Vector3.one, $"Floor_{centerX:0}_{centerZ:0}_{y:0}", BoundsAnchor.TopCenter);
                }
            }
        }

        static void AddRailingRunXAligned(Transform parent, float xMin, float xMax, float z, float y,
            bool faceNorth, string name)
        {
            int count = Mathf.RoundToInt((xMax - xMin) / 2f);
            for (int i = 0; i < count; i++)
            {
                PlacePrefabAnchored(Railing2, parent, new Vector3(xMin + 1f + i * 2f, y, z),
                    new Vector3(0f, faceNorth ? 180f : 0f, 0f), Vector3.one,
                    $"{name}_{i:00}", BoundsAnchor.BottomCenter);
            }
        }

        static void AddRailingRunZAligned(Transform parent, float x, float zMin, float zMax, float y,
            bool faceEast, string name)
        {
            int count = Mathf.RoundToInt((zMax - zMin) / 2f);
            for (int i = 0; i < count; i++)
            {
                PlacePrefabAnchored(Railing2, parent, new Vector3(x, y, zMin + 1f + i * 2f),
                    new Vector3(0f, faceEast ? 90f : -90f, 0f), Vector3.one,
                    $"{name}_{i:00}", BoundsAnchor.BottomCenter);
            }
        }

        static GameObject PlacePrefabAnchored(string path, Transform parent, Vector3 target, Vector3 euler,
            Vector3 scale, string name, BoundsAnchor anchor)
        {
            GameObject instance = PlacePrefab(path, parent, target, euler, scale, name, false);
            Bounds bounds = GetWorldBounds(instance);
            if (bounds.size.sqrMagnitude <= 0f) return instance;
            Vector3 currentAnchor = bounds.center;
            if (anchor == BoundsAnchor.BottomCenter) currentAnchor.y = bounds.min.y;
            if (anchor == BoundsAnchor.TopCenter) currentAnchor.y = bounds.max.y;
            instance.transform.position += target - currentAnchor;
            return instance;
        }

        static Bounds GetWorldBounds(GameObject go)
        {
            if (go == null) return default;
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found ? bounds : default;
        }

        static Material GetSourceMaterial(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Renderer renderer = prefab != null ? prefab.GetComponentInChildren<Renderer>(true) : null;
            return renderer != null ? renderer.sharedMaterial : null;
        }

        static GameObject CreateBoxVolume(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return go;
        }

        static GameObject CreateChamferedPontoon(Transform parent, string name, Vector3 center,
            float length, float height, float width, Material material)
        {
            string hullFolder = GeneratedAssetRoot + "/HullMeshes";
            if (!AssetDatabase.IsValidFolder(hullFolder))
            {
                AssetDatabase.CreateFolder(GeneratedAssetRoot, "HullMeshes");
            }

            string meshPath = $"{hullFolder}/Pontoon_L{Mathf.RoundToInt(length * 10f)}_H{Mathf.RoundToInt(height * 10f)}_W{Mathf.RoundToInt(width * 10f)}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                float hx = length * 0.5f;
                float hy = height * 0.5f;
                float hz = width * 0.5f;
                float chamfer = Mathf.Min(0.65f, Mathf.Min(hy, hz) * 0.35f);
                Vector2[] section =
                {
                    new Vector2(-hz + chamfer, hy), new Vector2(hz - chamfer, hy),
                    new Vector2(hz, hy - chamfer), new Vector2(hz, -hy + chamfer),
                    new Vector2(hz - chamfer, -hy), new Vector2(-hz + chamfer, -hy),
                    new Vector2(-hz, -hy + chamfer), new Vector2(-hz, hy - chamfer)
                };

                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                for (int end = 0; end < 2; end++)
                {
                    float x = end == 0 ? -hx : hx;
                    for (int i = 0; i < section.Length; i++)
                    {
                        vertices.Add(new Vector3(x, section[i].y, section[i].x));
                        uvs.Add(new Vector2(end * length * 0.25f, i / (float)section.Length));
                    }
                }

                List<int> triangles = new List<int>();
                for (int i = 0; i < section.Length; i++)
                {
                    int next = (i + 1) % section.Length;
                    int a = i;
                    int b = next;
                    int c = section.Length + i;
                    int d = section.Length + next;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
                for (int i = 1; i < section.Length - 1; i++)
                {
                    triangles.Add(0); triangles.Add(i + 1); triangles.Add(i);
                    triangles.Add(section.Length); triangles.Add(section.Length + i); triangles.Add(section.Length + i + 1);
                }

                mesh = new Mesh { name = "Chamfered_SemiSub_Pontoon" };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            return go;
        }

        static GameObject CreateStructuralBeam(Transform parent, Vector3 start, Vector3 end, float width,
            float height, Material material, string name)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length < 0.01f) return Group(name + "_Empty", parent).gameObject;
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = name;
            beam.transform.SetParent(parent, false);
            beam.transform.position = (start + end) * 0.5f;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
            beam.transform.rotation = Quaternion.LookRotation(direction.normalized, up);
            beam.transform.localScale = new Vector3(width, height, length);
            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return beam;
        }

        static GameObject CreateExactPipe(Transform parent, Vector3 start, Vector3 end, float radius,
            string sourcePrefabPath, string name)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length < 0.01f) return Group(name + "_Empty", parent).gameObject;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(-direction.normalized, up);
            return PlaceConstantRadiusPipe(parent, start, direction.normalized, rotation, length, radius,
                sourcePrefabPath, name);
        }

        static void BuildSupportFrame(Transform parent)
        {
            Transform legs = Group("Tapered_Jacket_Legs", parent);
            Vector2[] legPoints =
            {
                new Vector2(-27f, -14f), new Vector2(0f, -14f), new Vector2(27f, -14f),
                new Vector2(-27f, 14f), new Vector2(0f, 14f), new Vector2(27f, 14f)
            };

            foreach (Vector2 point in legPoints)
            {
                PlacePrefab(LargePillar10, legs, new Vector3(point.x, MainDeckY - 0.35f, point.y), new Vector3(0f, 0f, 90f), Vector3.one,
                    $"Leg_{point.x:0}_{point.y:0}_Upper");
                PlacePrefab(LargePillar10, legs, new Vector3(point.x, MainDeckY - 10.3f, point.y), new Vector3(0f, 0f, 90f), Vector3.one,
                    $"Leg_{point.x:0}_{point.y:0}_Lower");
            }

            Transform underFrame = Group("Primary_Deck_Grillage", parent);
            foreach (float y in new[] { ServiceDeckY - 0.65f, MainDeckY - 0.65f })
            {
                foreach (float z in new[] { -14f, 0f, 14f })
                {
                    float halfWidth = y < MainDeckY - 1f ? 27f : 36f;
                    PlaceBeamBetween(Beam12, 12f, underFrame, new Vector3(-halfWidth, y, z), new Vector3(halfWidth, y, z), $"Longitudinal_Y{y:0}_Z{z:0}");
                }
                foreach (float x in new[] { -27f, 0f, 27f })
                {
                    PlaceBeamBetween(Beam12, 12f, underFrame, new Vector3(x, y, -18f), new Vector3(x, y, 18f), $"Transverse_Y{y:0}_X{x:0}");
                }
            }

            Transform braces = Group("Dense_Underdeck_X_Bracing", parent);
            foreach (float z in new[] { -14f, 14f })
            {
                for (int segment = 0; segment < 2; segment++)
                {
                    float x0 = -27f + segment * 27f;
                    float x1 = x0 + 27f;
                    AddCrossBrace(braces, new Vector3(x0, 2f, z), new Vector3(x1, ServiceDeckY - 0.8f, z), $"Lower_Long_Z{z}_{segment}");
                    AddCrossBrace(braces, new Vector3(x0, ServiceDeckY - 0.8f, z), new Vector3(x1, MainDeckY - 0.8f, z), $"Upper_Long_Z{z}_{segment}");
                }
            }

            foreach (float x in new[] { -27f, 0f, 27f })
            {
                AddCrossBrace(braces, new Vector3(x, 2f, -14f), new Vector3(x, ServiceDeckY - 0.8f, 14f), $"Lower_Trans_X{x}");
                AddCrossBrace(braces, new Vector3(x, ServiceDeckY - 0.8f, -14f), new Vector3(x, MainDeckY - 0.8f, 14f), $"Upper_Trans_X{x}");
            }

            Transform upperSupports = Group("Asymmetric_Module_Supports", parent);
            foreach (float x in new[] { -28f, -16f, -4f, 8f })
            {
                foreach (float z in new[] { 8f, 18f })
                {
                    PlacePrefab(MediumPillar5, upperSupports, new Vector3(x, MainDeckY, z), new Vector3(90f, 0f, 0f), Vector3.one, $"Accommodation_Column_{x:0}_{z:0}");
                }
            }
            AddCrossBrace(upperSupports, new Vector3(-32f, MainDeckY, 18f), new Vector3(-4f, UpperDeckY - 0.6f, 18f), "Accommodation_North_Truss");
            AddCrossBrace(upperSupports, new Vector3(-32f, MainDeckY, 8f), new Vector3(-4f, UpperDeckY - 0.6f, 8f), "Accommodation_South_Truss");
        }

        static void BuildDecks(Transform parent)
        {
            Transform service = Group("Narrow_Cellar_Deck_Y8_52x28", parent);
            BuildRectFloor(service, -26f, 26f, -14f, 14f, ServiceDeckY);

            Transform main = Group("Single_Wide_Production_Deck_Y12", parent);
            BuildRectFloor(main, -34f, 30f, -18f, 18f, MainDeckY,
                (x, z) => x > 2f && x < 14f && z > -6f && z < 6f);
            BuildRectFloor(main, 30f, 38f, -10f, 10f, MainDeckY);
            BuildRectFloor(main, -38f, -34f, -14f, 14f, MainDeckY);

            Transform drillFloor = Group("Compact_Drill_Floor_Y16", parent);
            BuildRectFloor(drillFloor, -2f, 18f, -8f, 12f, ProcessDeckY,
                (x, z) => x > 4f && x < 12f && z > -2f && z < 6f);

            Transform catwalks = Group("Cantilever_Catwalks_And_Bridges", parent);
            BuildCatwalkLine(catwalks, new Vector3(-30f, ProcessDeckY, 20f), new Vector3(8f, ProcessDeckY, 20f), "Accommodation_Access_Walk");
            BuildCatwalkLine(catwalks, new Vector3(16f, ProcessDeckY, -12f), new Vector3(32f, ProcessDeckY, -12f), "Crane_Service_Walk");
            BuildCatwalkLine(catwalks, new Vector3(-10f, ProcessDeckY, -12f), new Vector3(16f, ProcessDeckY, -12f), "Pipe_Rack_Walk");
            BuildCatwalkLine(catwalks, new Vector3(-36f, UpperDeckY, 6f), new Vector3(-36f, UpperDeckY, 10f), "Stair_To_Accommodation_Z");
            BuildCatwalkLine(catwalks, new Vector3(-36f, UpperDeckY, 10f), new Vector3(-30f, UpperDeckY, 10f), "Stair_To_Accommodation_X");

            BuildExternalStairTower(parent);

            Transform emergency = Group("East_Emergency_Ladder", parent);
            for (int i = 0; i < 4; i++)
            {
                string ladder = i == 3 ? "Ladders/Ladder_Cage_Module_3_End.prefab" : "Ladders/Ladder_Cage_Module_2.prefab";
                PlacePrefab(P(ladder), emergency, new Vector3(33f, ServiceDeckY + i * 4f, 15f), Vector3.zero, Vector3.one, $"Cage_Ladder_{i:00}");
            }
        }

        static void BuildPerimeterSafety(Transform parent)
        {
            Transform railings = Group("Main_Deck_Railings", parent);
            for (int i = 0; i < 32; i++)
            {
                float x = -32f + i * 2f;
                bool southAccessGap = x > -3f && x < 3f;
                if (!southAccessGap)
                {
                    PlacePrefab(Railing2, railings, new Vector3(x, MainDeckY, -18f), Vector3.zero, Vector3.one, $"South_Rail_{i:00}");
                }
                PlacePrefab(Railing2, railings, new Vector3(x, MainDeckY, 18f), new Vector3(0f, 180f, 0f), Vector3.one, $"North_Rail_{i:00}");
            }

            for (int i = 0; i < 18; i++)
            {
                float z = -16f + i * 2f;
                bool eastDiveGap = z > -19f && z < -15f;
                PlacePrefab(Railing2, railings, new Vector3(-34f, MainDeckY, z), new Vector3(0f, -90f, 0f), Vector3.one, $"West_Rail_{i:00}");
                if (!eastDiveGap)
                {
                    PlacePrefab(Railing2, railings, new Vector3(30f, MainDeckY, z), new Vector3(0f, 90f, 0f), Vector3.one, $"East_Rail_{i:00}");
                }
            }

            AddRailingRunX(railings, 30f, 38f, -10f, MainDeckY, true, "Crane_Cantilever_South");
            AddRailingRunX(railings, 30f, 38f, 10f, MainDeckY, false, "Crane_Cantilever_North");
            AddRailingRunZ(railings, 38f, -10f, 10f, MainDeckY, true, "Crane_Cantilever_East");

            Transform openingRails = Group("Drill_Well_Railings", parent);
            for (int i = 0; i < 6; i++)
            {
                float offset = -4f + i * 2f;
                PlacePrefab(Railing2, openingRails, new Vector3(8f + offset, MainDeckY, -6f), Vector3.zero, Vector3.one, $"Well_S_{i}");
                PlacePrefab(Railing2, openingRails, new Vector3(8f + offset, MainDeckY, 6f), new Vector3(0f, 180f, 0f), Vector3.one, $"Well_N_{i}");
                PlacePrefab(Railing2, openingRails, new Vector3(2f, MainDeckY, offset), new Vector3(0f, -90f, 0f), Vector3.one, $"Well_W_{i}");
                PlacePrefab(Railing2, openingRails, new Vector3(14f, MainDeckY, offset), new Vector3(0f, 90f, 0f), Vector3.one, $"Well_E_{i}");
            }
        }

        static void BuildArchitecture(Transform parent, Transform props)
        {
            Transform preparation = Group("L2_South_Equipment_Preparation", parent);
            BuildBuildingShell(preparation, -30f, -14f, -16f, -8f, MainDeckY, 1, 1);
            BuildRectFloor(preparation, -30f, -14f, -16f, -8f, ProcessDeckY);
            CreateMarker("Room_Equipment_Preparation", preparation, new Vector3(-22f, MainDeckY, -12f));

            Transform electrical = Group("L3_East_Electrical_Maintenance", parent);
            BuildBuildingShell(electrical, 18f, 30f, 6f, 14f, MainDeckY, 1, 1);
            BuildRectFloor(electrical, 18f, 30f, 6f, 14f, ProcessDeckY);
            CreateMarker("Room_Electrical_Maintenance", electrical, new Vector3(24f, MainDeckY, 10f));

            Transform suspended = Group("Offset_Accommodation_Block_36x12", parent);
            BuildRectFloor(suspended, -30f, 6f, 8f, 20f, UpperDeckY);
            BuildBuildingShell(suspended, -30f, 6f, 8f, 20f, UpperDeckY, 3, 4);
            PlaceWallRunZ(suspended, -18f, 8f, 20f, UpperDeckY, 1, -1, "LogRoom_Partition");
            PlaceWallRunZ(suspended, -6f, 8f, 20f, UpperDeckY, 1, -1, "Analysis_Partition");
            BuildRectFloor(suspended, -30f, 6f, 8f, 20f, RoofY);
            CreateMarker("Room_Log_Monitor", suspended, new Vector3(-24f, UpperDeckY, 14f));
            CreateMarker("Room_BlackBox_Analysis", suspended, new Vector3(-12f, UpperDeckY, 14f));

            Transform control = Group("Small_Raised_Control_Bridge_20x8", parent);
            BuildBuildingShell(control, -14f, 6f, 10f, 18f, RoofY, 1, 2);
            BuildRectFloor(control, -14f, 6f, 10f, 18f, RoofY + 4f);
            CreateMarker("Room_Main_Control", control, new Vector3(-4f, RoofY, 14f));

            Transform roofPlant = Group("Roof_HVAC_And_Service_Volumes", parent);
            PlacePrefab(P("AC_Units/AC_Unit_Main.prefab"), roofPlant, new Vector3(-26f, RoofY, 11f), Vector3.zero, Vector3.one, "Accommodation_Roof_AC");
            PlacePrefab(P("Electrical_Structures/ACunit_Small_Module_2_.prefab"), roofPlant, new Vector3(-20f, RoofY, 16f), Vector3.zero, Vector3.one, "Accommodation_Roof_Cooler");
            BuildVentRun(roofPlant, new Vector3(-28f, RoofY + 1.2f, 9f), new Vector3(-16f, RoofY + 1.2f, 9f), "Accommodation_Roof_Vent");

            DressRooms(props);
        }

        static void BuildDerrick(Transform parent)
        {
            Transform derrick = Group("Offset_Tapered_Drilling_Derrick", parent);
            float[] levels = { ProcessDeckY, 24f, 34f, 42f };
            float[] halfWidths = { 6f, 5f, 3.5f, 2.5f };
            float[] halfDepths = { 5f, 4f, 3f, 2.5f };
            Vector3 center = new Vector3(8f, 0f, 1f);

            for (int level = 0; level < levels.Length; level++)
            {
                float y = levels[level];
                float hx = halfWidths[level];
                float hz = halfDepths[level];
                PlaceBeamBetween(Brace6, 6f, derrick, new Vector3(center.x - hx, y, center.z - hz), new Vector3(center.x + hx, y, center.z - hz), $"South_Ring_{level}");
                PlaceBeamBetween(Brace6, 6f, derrick, new Vector3(center.x - hx, y, center.z + hz), new Vector3(center.x + hx, y, center.z + hz), $"North_Ring_{level}");
                PlaceBeamBetween(Brace6, 6f, derrick, new Vector3(center.x - hx, y, center.z - hz), new Vector3(center.x - hx, y, center.z + hz), $"West_Ring_{level}");
                PlaceBeamBetween(Brace6, 6f, derrick, new Vector3(center.x + hx, y, center.z - hz), new Vector3(center.x + hx, y, center.z + hz), $"East_Ring_{level}");

                if (level >= levels.Length - 1)
                {
                    continue;
                }

                float nextY = levels[level + 1];
                float nextHx = halfWidths[level + 1];
                float nextHz = halfDepths[level + 1];
                Vector3[] lower =
                {
                    new Vector3(center.x - hx, y, center.z - hz), new Vector3(center.x + hx, y, center.z - hz),
                    new Vector3(center.x - hx, y, center.z + hz), new Vector3(center.x + hx, y, center.z + hz)
                };
                Vector3[] upper =
                {
                    new Vector3(center.x - nextHx, nextY, center.z - nextHz), new Vector3(center.x + nextHx, nextY, center.z - nextHz),
                    new Vector3(center.x - nextHx, nextY, center.z + nextHz), new Vector3(center.x + nextHx, nextY, center.z + nextHz)
                };
                for (int corner = 0; corner < 4; corner++)
                {
                    PlaceBeamBetween(Beam12, 12f, derrick, lower[corner], upper[corner], $"Tapered_Leg_{level}_{corner}");
                }
                AddCrossBrace(derrick, lower[0], upper[1], $"South_X_{level}");
                AddCrossBrace(derrick, lower[2], upper[3], $"North_X_{level}");
                AddCrossBrace(derrick, lower[0], upper[2], $"West_X_{level}");
                AddCrossBrace(derrick, lower[1], upper[3], $"East_X_{level}");
            }
        }

        static void AddDerrickSideBraces(Transform parent, float plane, bool constantZ)
        {
            for (int level = 0; level < 3; level++)
            {
                float y0 = MainDeckY + level * 7f;
                float y1 = Mathf.Min(MainDeckY + (level + 1) * 7f, MainDeckY + 22f);
                Vector3 a0 = constantZ ? new Vector3(-5f, y0, plane) : new Vector3(plane, y0, -5f);
                Vector3 a1 = constantZ ? new Vector3(5f, y1, plane) : new Vector3(plane, y1, 5f);
                Vector3 b0 = constantZ ? new Vector3(5f, y0, plane) : new Vector3(plane, y0, 5f);
                Vector3 b1 = constantZ ? new Vector3(-5f, y1, plane) : new Vector3(plane, y1, -5f);
                PlaceBeamBetween(Brace6, 6f, parent, a0, a1, $"Derrick_Diagonal_A_{constantZ}_{plane}_{level}");
                PlaceBeamBetween(Brace6, 6f, parent, b0, b1, $"Derrick_Diagonal_B_{constantZ}_{plane}_{level}");
            }
        }

        static void BuildEquipmentZones(Transform equipment, Transform props)
        {
            Transform crane = Group("Independent_East_Cantilever_Crane", equipment);
            PlacePrefab(P("Crane/Crane_Bottom_01.prefab"), crane, new Vector3(34f, MainDeckY, -2f), Vector3.zero, Vector3.one, "Crane_Base");
            PlacePrefab(P("Crane/Crane_Top_01.prefab"), crane, new Vector3(34f, MainDeckY + 7.15f, -2f), new Vector3(0f, -55f, 0f), Vector3.one, "Crane_Upper");
            PlacePrefab(P("Crane/Hook_Pulley.prefab"), crane, new Vector3(24f, MainDeckY + 5f, -11f), Vector3.zero, Vector3.one, "Crane_Hook");

            Transform tanks = Group("L1_Lower_Tank_And_Pump_Deck", equipment);
            PlacePrefab(P("Tank/Fuel_Tank_01_.prefab"), tanks, new Vector3(-18f, ServiceDeckY, -8f), new Vector3(0f, 90f, 0f), Vector3.one, "Lower_Fuel_Tank_A");
            PlacePrefab(P("Tank/Fuel_Tank_01_.prefab"), tanks, new Vector3(18f, ServiceDeckY, -8f), new Vector3(0f, 90f, 0f), Vector3.one, "Lower_Fuel_Tank_B");

            BuildOverheadPipeGallery(equipment);
            BuildDenseProcessCore(equipment);
            BuildBelowDeckRisers(equipment);

            Transform serviceEquipment = Group("L1_Lower_Machinery", equipment);
            PlacePrefab(P("Electrical_Structures/Cooler_Module_01_.prefab"), serviceEquipment, new Vector3(20f, ServiceDeckY, 8f), Vector3.zero, Vector3.one, "Lower_Cooler_A");
            PlacePrefab(P("Electrical_Structures/ACunit_Big_Module_1.prefab"), serviceEquipment, new Vector3(-8f, ServiceDeckY, 9f), Vector3.zero, Vector3.one, "Lower_AC_A");
            PlacePrefab(P("Electrical_Structures/ElectricBox_Module_2.prefab"), serviceEquipment, new Vector3(7f, ServiceDeckY, 10f), new Vector3(0f, 180f, 0f), Vector3.one, "Lower_Power_Skid");

            Transform processEquipment = Group("L3_Process_Machinery", equipment);
            PlacePrefab(P("Electrical_Structures/ACunit_Big_Module_2.prefab"), processEquipment, new Vector3(-8f, MainDeckY, -13f), Vector3.zero, Vector3.one, "Process_AC_A");
            PlacePrefab(P("Electrical_Structures/ACunit_Big_Module_3.prefab"), processEquipment, new Vector3(0f, MainDeckY, -13f), new Vector3(0f, 180f, 0f), Vector3.one, "Process_AC_B");
            PlacePrefab(P("Electrical_Structures/Cooler_Module_03_.prefab"), processEquipment, new Vector3(19f, MainDeckY, -13f), Vector3.zero, Vector3.one, "Main_Cooler");

            Transform deckClutter = Group("Deck_Clutter", props);
            PlacePrefab(P("Crate_Metal/BarrelCrate_Rectangular_01_Tarp.prefab"), deckClutter, new Vector3(12f, MainDeckY, -17f), new Vector3(0f, 20f, 0f), Vector3.one, "Covered_Crate");
            PlacePrefab(P("Crate_Metal/BarrelCrate_Square_02_.prefab"), deckClutter, new Vector3(15f, MainDeckY, -14f), new Vector3(0f, -15f, 0f), Vector3.one, "Metal_Crate");
            for (int i = 0; i < 6; i++)
            {
                string barrel = i % 2 == 0 ? "Barrel_Oil_Metal_01_01_.prefab" : "Barrel_Oil_Metal_02_02_.prefab";
                PlacePrefab(P("Barrel_Metal/" + barrel), deckClutter, new Vector3(-14f + (i % 3) * 1.2f, MainDeckY, -18f + (i / 3) * 1.1f),
                    new Vector3(0f, i * 23f, 0f), Vector3.one, $"Oil_Barrel_{i:00}");
            }

            Transform signs = Group("Safety_Signs", props);
            PlacePrefab(P("Signs/Prop_Sign_Big_01_.prefab"), signs, new Vector3(0f, MainDeckY + 0.2f, -21.7f), new Vector3(0f, 180f, 0f), Vector3.one, "South_Deck_Sign");
            PlacePrefab(P("Signs/Prop_Sign_Medium_07_.prefab"), signs, new Vector3(-33.7f, ProcessDeckY + 1.6f, 7f), new Vector3(0f, 90f, 0f), Vector3.one, "Operations_Sign");
            PlacePrefab(P("Signs/Prop_Sign_Medium_12_.prefab"), signs, new Vector3(29.7f, ProcessDeckY + 1.6f, 9f), new Vector3(0f, -90f, 0f), Vector3.one, "Technical_Sign");
        }

        static void DressRooms(Transform props)
        {
            Transform control = Group("Main_Control_Interior", props);
            PlacePrefab(P("Control_Console/Control_Console_A.prefab"), control, new Vector3(-10f, RoofY, 15f), new Vector3(0f, 180f, 0f), Vector3.one * 1.8f, "Control_Console_A");
            PlacePrefab(P("Control_Console/Control_Console_B.prefab"), control, new Vector3(-3f, RoofY, 15f), new Vector3(0f, 180f, 0f), Vector3.one * 1.8f, "Control_Console_B");

            Transform logRoom = Group("Log_Monitor_Interior", props);
            PlacePrefab(P("Control_Console/Control_Console_A.prefab"), logRoom, new Vector3(-26f, UpperDeckY, 17f), new Vector3(0f, 180f, 0f), Vector3.one * 1.6f, "Log_Console");
            PlacePrefab(P("Electrical_Structures/ElectricBox_Module_1.prefab"), logRoom, new Vector3(-29.5f, UpperDeckY, 11f), new Vector3(0f, 90f, 0f), Vector3.one, "LogRoom_ElectricBox");

            Transform electrical = Group("Electrical_Interior", props);
            PlacePrefab(P("Electrical_Structures/ElectricBox_Module_1.prefab"), electrical, new Vector3(19f, MainDeckY, 8f), new Vector3(0f, -90f, 0f), Vector3.one, "Electric_Panel_A");
            PlacePrefab(P("Electrical_Structures/ElectricBox_Module_3.prefab"), electrical, new Vector3(19f, MainDeckY, 12f), new Vector3(0f, -90f, 0f), Vector3.one, "Electric_Panel_B");
            PlacePrefab(P("Electrical_Structures/ACunit_Small_Module_4_Inside_1.prefab"), electrical, new Vector3(27f, MainDeckY, 12f), Vector3.zero, Vector3.one, "Electrical_AC");

            Transform analysis = Group("BlackBox_Analysis_Interior", props);
            PlacePrefab(P("Control_Console/Control_Console_A.prefab"), analysis, new Vector3(-14f, UpperDeckY, 17f), new Vector3(0f, 180f, 0f), Vector3.one * 1.8f, "Analysis_Console");
            PlacePrefab(P("Crate_Metal/BarrelCrate_Square_01_.prefab"), analysis, new Vector3(-10f, UpperDeckY, 11f), Vector3.zero, new Vector3(0.65f, 0.65f, 0.65f), "BlackBox_Placeholder_Crate");

            Transform equipmentRoom = Group("Equipment_Room_Interior", props);
            PlacePrefab(P("Crate_Metal/BarrelCrate_Rectangular_03_.prefab"), equipmentRoom, new Vector3(-18f, MainDeckY, -13f), new Vector3(0f, -90f, 0f), Vector3.one, "Equipment_Crate_B");
            PlacePrefab(P("Cables_And_Ropes/Large_Cable_01_.prefab"), equipmentRoom, new Vector3(-25f, MainDeckY + 0.1f, -10f), Vector3.zero, Vector3.one, "Equipment_Cable");
        }

        static void BuildLighting(Transform parent)
        {
            GameObject sunGo = new GameObject("Overcast_Directional_Light");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.66f, 0.75f, 0.86f);
            sun.intensity = 0.82f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            RenderSettings.sun = sun;

            Vector3[] fixtures =
            {
                new Vector3(-31f, MainDeckY + 5.5f, -18f), new Vector3(31f, MainDeckY + 5.5f, -18f),
                new Vector3(-31f, MainDeckY + 5.5f, 18f), new Vector3(31f, MainDeckY + 5.5f, 18f),
                new Vector3(-8f, MainDeckY + 7f, 0f), new Vector3(8f, MainDeckY + 7f, 0f),
                new Vector3(-30f, MainDeckY - 0.8f, -10f), new Vector3(30f, MainDeckY - 0.8f, 10f)
            };

            for (int i = 0; i < fixtures.Length; i++)
            {
                Vector3 position = fixtures[i];
                Vector3 direction = (new Vector3(0f, MainDeckY + 0.5f, 0f) - position).normalized;
                PlacePrefab(P(i % 2 == 0 ? "Flood_Lights/Floodlights_Single_01_.prefab" : "Flood_Lights/Floodlights_Single_02_.prefab"),
                    parent, position, Quaternion.LookRotation(direction).eulerAngles, Vector3.one, $"Floodlight_Fixture_{i:00}", false);
                CreateSpotLight(parent, $"Warm_Work_Light_{i:00}", position, direction, i < 4);
            }

            CreatePointLight(parent, "ControlRoom_Warm_Fill", new Vector3(-4f, RoofY + 2.5f, 14f), new Color(1f, 0.55f, 0.29f), 12f, 10f);
            CreatePointLight(parent, "LogRoom_Warm_Fill", new Vector3(-24f, UpperDeckY + 2.5f, 14f), new Color(1f, 0.46f, 0.24f), 10f, 8f);
            CreatePointLight(parent, "Technical_Cold_Fill", new Vector3(24f, MainDeckY + 2.5f, 10f), new Color(0.38f, 0.64f, 1f), 9f, 8f);
            CreatePointLight(parent, "Analysis_Red_Emergency", new Vector3(-12f, UpperDeckY + 2.6f, 14f), new Color(1f, 0.12f, 0.06f), 8f, 7f);
            CreatePointLight(parent, "EquipmentRoom_Warm_Fill", new Vector3(-22f, MainDeckY + 2.5f, -12f), new Color(1f, 0.5f, 0.25f), 10f, 9f);
            CreatePointLight(parent, "LowerDeck_Amber_Fill", new Vector3(0f, ServiceDeckY + 2.3f, -17f), new Color(1f, 0.42f, 0.18f), 8f, 13f);
        }

        static void ConfigureAtmosphere(Transform parent)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.12f, 0.17f, 0.22f);
            RenderSettings.fogDensity = 0.0038f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.27f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.17f, 0.20f);
            RenderSettings.ambientGroundColor = new Color(0.055f, 0.065f, 0.075f);
            RenderSettings.ambientIntensity = 0.9f;
            RenderSettings.reflectionIntensity = 0.7f;

            string skyPath = GeneratedAssetRoot + "/OilRig_OvercastDuskSky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                Shader skyShader = Shader.Find("Skybox/Procedural");
                if (skyShader != null)
                {
                    sky = new Material(skyShader) { name = "OilRig_OvercastDuskSky" };
                    sky.SetFloat("_SunSize", 0.07f);
                    sky.SetFloat("_SunSizeConvergence", 8f);
                    sky.SetFloat("_AtmosphereThickness", 1.35f);
                    sky.SetColor("_SkyTint", new Color(0.28f, 0.38f, 0.50f));
                    sky.SetColor("_GroundColor", new Color(0.07f, 0.08f, 0.09f));
                    sky.SetFloat("_Exposure", 0.72f);
                    AssetDatabase.CreateAsset(sky, skyPath);
                }
            }
            RenderSettings.skybox = sky;

            string volumePath = GeneratedAssetRoot + "/OilRig_DuskVolume.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, volumePath);
                Tonemapping tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.ACES);
                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(-0.35f);
                color.contrast.Override(13f);
                color.saturation.Override(-8f);
                color.colorFilter.Override(new Color(0.91f, 0.95f, 1f));
                WhiteBalance white = profile.Add<WhiteBalance>(true);
                white.temperature.Override(-11f);
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(0.32f);
                bloom.threshold.Override(1.15f);
                bloom.scatter.Override(0.58f);
                Vignette vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.18f);
                vignette.smoothness.Override(0.62f);
            }
            if (profile.TryGet(out ColorAdjustments currentColor))
            {
                currentColor.postExposure.Override(-0.35f);
                currentColor.contrast.Override(13f);
                currentColor.saturation.Override(-8f);
            }

            GameObject volumeGo = new GameObject("Global_Dusk_Volume");
            volumeGo.transform.SetParent(parent, false);
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5f;
            volume.sharedProfile = profile;
        }

        static void AddOcclusionAndReflectionHelpers(Transform root)
        {
            Transform helpers = Group("Baking_Helpers", root);
            GameObject occlusionGo = new GameObject("Platform_Occlusion_Area");
            occlusionGo.transform.SetParent(helpers, false);
            occlusionGo.transform.position = new Vector3(0f, MainDeckY + 6f, 0f);
            OcclusionArea area = occlusionGo.AddComponent<OcclusionArea>();
            area.center = Vector3.zero;
            area.size = new Vector3(82f, 40f, 56f);

            CreateReflectionProbe(helpers, "Accommodation_Reflection", new Vector3(-12f, UpperDeckY + 3f, 14f), new Vector3(40f, 16f, 18f));
            CreateReflectionProbe(helpers, "Main_Deck_Reflection", new Vector3(0f, MainDeckY + 4f, 0f), new Vector3(78f, 24f, 42f));
            CreateReflectionProbe(helpers, "Lower_Deck_Reflection", new Vector3(0f, ServiceDeckY + 2f, 0f), new Vector3(58f, 10f, 34f));
        }

        static void BuildPreviewCameras(Transform parent)
        {
            CreatePreviewCamera(parent, "Overview_Camera", new Vector3(92f, 50f, -88f), new Vector3(0f, 18f, 1f), true);
            CreatePreviewCamera(parent, "SideSilhouette_Camera", new Vector3(76f, 27f, 4f), new Vector3(0f, 18f, 2f), false);
            CreatePreviewCamera(parent, "LowerDeck_Camera", new Vector3(-45f, 8f, -34f), new Vector3(0f, 9f, 0f), false);
            CreatePreviewCamera(parent, "MainDeck_Camera", new Vector3(-12f, MainDeckY + 2f, -20f), new Vector3(8f, 18f, 0f), false);
            CreatePreviewCamera(parent, "AccommodationBlock_Camera", new Vector3(-52f, 29f, -8f), new Vector3(-10f, 23f, 14f), false);
            CreatePreviewCamera(parent, "PipeGallery_Camera", new Vector3(42f, 20f, -28f), new Vector3(4f, 18f, -7f), false);
            CreatePreviewCamera(parent, "RiserSystem_Camera", new Vector3(39f, 3f, -27f), new Vector3(7f, 1f, 4f), false);
            CreatePreviewCamera(parent, "Derrick_Camera", new Vector3(27f, 18f, -18f), new Vector3(8f, 29f, 1f), false);
        }

        static void AddCrossBrace(Transform parent, Vector3 diagonalAStart, Vector3 diagonalAEnd, string name)
        {
            PlaceBeamBetween(Brace6, 6f, parent, diagonalAStart, diagonalAEnd, name + "_A");
            Vector3 diagonalBStart;
            Vector3 diagonalBEnd;
            if (Mathf.Abs(diagonalAEnd.x - diagonalAStart.x) >= Mathf.Abs(diagonalAEnd.z - diagonalAStart.z))
            {
                diagonalBStart = new Vector3(diagonalAStart.x, diagonalAEnd.y, diagonalAStart.z);
                diagonalBEnd = new Vector3(diagonalAEnd.x, diagonalAStart.y, diagonalAEnd.z);
            }
            else
            {
                diagonalBStart = new Vector3(diagonalAStart.x, diagonalAEnd.y, diagonalAEnd.z);
                diagonalBEnd = new Vector3(diagonalAEnd.x, diagonalAStart.y, diagonalAStart.z);
            }
            PlaceBeamBetween(Brace6, 6f, parent, diagonalBStart, diagonalBEnd, name + "_B");
        }

        static void BuildCatwalkLine(Transform parent, Vector3 start, Vector3 end, string name)
        {
            Transform line = Group(name, parent);
            Vector3 direction = (end - start).normalized;
            float distance = Vector3.Distance(start, end);
            int count = Mathf.Max(1, Mathf.RoundToInt(distance / 2f));
            bool alongX = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z);
            Vector3 rotation = alongX ? Vector3.zero : new Vector3(0f, 90f, 0f);
            Vector3 side = alongX ? Vector3.forward : Vector3.right;
            for (int i = 0; i < count; i++)
            {
                Vector3 center = start + direction * (i * 2f + 1f);
                PlacePrefab(CatwalkFloor2, line, center, rotation, Vector3.one, $"Grid_Floor_{i:00}");
                PlacePrefab(Railing2, line, center + side, rotation, Vector3.one, $"Safety_Rail_A_{i:00}");
                PlacePrefab(Railing2, line, center - side, rotation + new Vector3(0f, 180f, 0f), Vector3.one, $"Safety_Rail_B_{i:00}");
            }
        }

        static void BuildExternalStairTower(Transform parent)
        {
            Transform tower = Group("West_External_Stair_Tower_Y8_To_Y24", parent);
            foreach (float y in new[] { ServiceDeckY, MainDeckY, ProcessDeckY, UpperDeckY, RoofY })
            {
                BuildRectFloor(tower, -40f, -32f, -2f, 6f, y);
                AddRailingRunX(tower, -38f, -32f, 6f, y, false, $"Landing_North_{y:0}");
                AddRailingRunZ(tower, -40f, -2f, 6f, y, false, $"Landing_West_{y:0}");
            }

            for (int level = 0; level < 4; level++)
            {
                float y = ServiceDeckY + level * 4f;
                bool reverse = level % 2 == 1;
                Vector3 position = new Vector3(reverse ? -34.5f : -38.5f, y, 2f);
                Vector3 rotation = new Vector3(0f, reverse ? -90f : 90f, 0f);
                PlacePrefab(Stairs2, tower, position, rotation, Vector3.one, $"Stair_Flight_{level:00}");
                AddCrossBrace(tower, new Vector3(-40f, y, -2f), new Vector3(-32f, y + 4f, -2f), $"Tower_South_{level:00}");
                AddCrossBrace(tower, new Vector3(-40f, y, 6f), new Vector3(-32f, y + 4f, 6f), $"Tower_North_{level:00}");
            }
        }

        static void AddRailingRunX(Transform parent, float xMin, float xMax, float z, float y, bool faceSouth, string name)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt((xMax - xMin) / 2f));
            for (int i = 0; i < count; i++)
            {
                PlacePrefab(Railing2, parent, new Vector3(xMin + 1f + i * 2f, y, z), new Vector3(0f, faceSouth ? 0f : 180f, 0f), Vector3.one, $"{name}_{i:00}");
            }
        }

        static void AddRailingRunZ(Transform parent, float x, float zMin, float zMax, float y, bool faceEast, string name)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt((zMax - zMin) / 2f));
            for (int i = 0; i < count; i++)
            {
                PlacePrefab(Railing2, parent, new Vector3(x, y, zMin + 1f + i * 2f), new Vector3(0f, faceEast ? 90f : -90f, 0f), Vector3.one, $"{name}_{i:00}");
            }
        }

        static void BuildVentRun(Transform parent, Vector3 start, Vector3 end, string name)
        {
            PlaceBeamBetween(P("AC_Units/Vent_Pipe_4M.prefab"), 4f, parent, start, end, name);
            PlacePrefab(P("AC_Units/Vent_End_01.prefab"), parent, end, Vector3.zero, Vector3.one, name + "_End");
        }

        static void BuildOverheadPipeGallery(Transform parent)
        {
            Transform gallery = Group("Central_Continuous_Red_Process_Pipe_Gallery", parent);
            foreach (float x in new[] { -12f, 0f, 12f, 24f })
            {
                PlaceBeamBetween(Brace6, 6f, gallery, new Vector3(x, MainDeckY, -11f), new Vector3(x, 19f, -11f), $"Rack_Post_S_{x:0}");
                PlaceBeamBetween(Brace6, 6f, gallery, new Vector3(x, MainDeckY, -3f), new Vector3(x, 19f, -3f), $"Rack_Post_N_{x:0}");
                PlaceBeamBetween(Brace6, 6f, gallery, new Vector3(x, 18.7f, -11f), new Vector3(x, 18.7f, -3f), $"Rack_Crossbar_{x:0}");
            }

            float[] pipeZ = { -9.5f, -7f, -4.5f };
            for (int lane = 0; lane < pipeZ.Length; lane++)
            {
                string pipe = lane == 1 ? PipeMedium4 : PipeSmall4;
                float y = 17.7f + lane * 0.48f;
                PlaceBeamBetween(pipe, 4f, gallery, new Vector3(-16f, y, pipeZ[lane]), new Vector3(26f, y, pipeZ[lane]), $"Overhead_Main_{lane:00}");
                PlaceBeamBetween(pipe, 4f, gallery, new Vector3(-16f, MainDeckY + 0.7f, pipeZ[lane]), new Vector3(-16f, y, pipeZ[lane]), $"West_Drop_{lane:00}");
                PlaceBeamBetween(pipe, 4f, gallery, new Vector3(26f, MainDeckY + 0.7f, pipeZ[lane]), new Vector3(26f, y, pipeZ[lane]), $"East_Drop_{lane:00}");
                PlacePrefab(P(lane == 1 ? "Pipes_Medium/Pipe_Med_Corner_90_01_.prefab" : "Pipes_Small/Pipe_Sml_Corner_90_01_.prefab"),
                    gallery, new Vector3(-16f, y, pipeZ[lane]), new Vector3(0f, 90f, 0f), Vector3.one, $"West_Elbow_{lane:00}");
                PlacePrefab(P(lane == 1 ? "Pipes_Medium/Pipe_Med_Corner_90_02_.prefab" : "Pipes_Small/Pipe_Sml_Corner_90_02_.prefab"),
                    gallery, new Vector3(26f, y, pipeZ[lane]), new Vector3(0f, -90f, 0f), Vector3.one, $"East_Elbow_{lane:00}");
            }

            PlaceBeamBetween(PipeMedium4, 4f, gallery, new Vector3(26f, MainDeckY + 0.7f, -7f), new Vector3(26f, MainDeckY + 0.7f, 8f), "Tank_Feed_Header");
            PlacePrefab(P("Valves_Large/Valve_Large_01_.prefab"), gallery, new Vector3(26f, MainDeckY + 1f, 4f), new Vector3(0f, 90f, 0f), Vector3.one, "Main_Header_Valve");
        }

        static void BuildDenseProcessCore(Transform parent)
        {
            Transform core = Group("Dense_Central_Process_Core", parent);

            PlacePrefab(P("Electrical_Structures/Cooler_Module_02_.prefab"), core, new Vector3(-5f, MainDeckY, 5f), Vector3.zero, Vector3.one, "Separator_Cooler_Skid");
            PlacePrefab(P("Electrical_Structures/ACunit_Big_Module_4.prefab"), core, new Vector3(-12f, MainDeckY, 3f), new Vector3(0f, 90f, 0f), Vector3.one, "Compression_Skid");
            PlacePrefab(P("Electrical_Structures/ElectricBox_Module_3.prefab"), core, new Vector3(-4f, MainDeckY, 11f), new Vector3(0f, 180f, 0f), Vector3.one, "Process_Control_Panel");
            PlacePrefab(P("Electrical_Structures/ACunit_Small_Module_3_.prefab"), core, new Vector3(17f, MainDeckY, 10f), new Vector3(0f, -90f, 0f), Vector3.one, "Chemical_Injection_Skid");

            Vector3[] separatorPositions =
            {
                new Vector3(-9f, MainDeckY + 0.4f, -3f),
                new Vector3(-4f, MainDeckY + 0.4f, -3f),
                new Vector3(18f, MainDeckY + 0.4f, 2f)
            };
            for (int i = 0; i < separatorPositions.Length; i++)
            {
                Vector3 bottom = separatorPositions[i];
                string pipe = i == 2 ? PipeMedium4 : PipeSmall4;
                PlaceBeamBetween(pipe, 4f, core, bottom, bottom + Vector3.up * 6f, $"Vertical_Separator_{i:00}");
                PlacePrefab(P(i == 2 ? "Pipes_Medium/Pipe_Med_Cap_01_.prefab" : "Pipes_Small/Pipe_Sml_Cap_01_.prefab"),
                    core, bottom + Vector3.up * 6f, Vector3.zero, Vector3.one, $"Separator_Cap_{i:00}");
                PlacePrefab(P(i == 2 ? "Valves_Medium/Valve_Medium_02_.prefab" : "Valves_Small/Valve_Small_01_.prefab"),
                    core, bottom + new Vector3(0f, 2.2f, 0f), Vector3.zero, Vector3.one, $"Separator_Valve_{i:00}");
            }

            Transform rack = Group("Process_Skid_Frame", core);
            foreach (float x in new[] { -14f, -8f, -2f })
            {
                PlaceBeamBetween(Brace6, 6f, rack, new Vector3(x, MainDeckY, -5f), new Vector3(x, MainDeckY + 6f, -5f), $"Rack_Post_S_{x:0}");
                PlaceBeamBetween(Brace6, 6f, rack, new Vector3(x, MainDeckY, 5f), new Vector3(x, MainDeckY + 6f, 5f), $"Rack_Post_N_{x:0}");
                PlaceBeamBetween(Brace6, 6f, rack, new Vector3(x, MainDeckY + 5.8f, -5f), new Vector3(x, MainDeckY + 5.8f, 5f), $"Rack_Header_{x:0}");
            }

            PlaceBeamBetween(PipeSmall4, 4f, core, new Vector3(-14f, MainDeckY + 2f, -2f), new Vector3(-2f, MainDeckY + 2f, -2f), "Separator_Low_Header");
            PlaceBeamBetween(PipeSmall4, 4f, core, new Vector3(-14f, MainDeckY + 4f, 1f), new Vector3(-2f, MainDeckY + 4f, 1f), "Separator_High_Header");
            PlacePrefab(P("Valves_Small/Valve_Small_01_.prefab"), core, new Vector3(-8f, MainDeckY + 2f, -2f), new Vector3(0f, 90f, 0f), Vector3.one, "Low_Header_Valve");
            PlacePrefab(P("Valves_Small/Valve_Small_01_.prefab"), core, new Vector3(-8f, MainDeckY + 4f, 1f), new Vector3(0f, 90f, 0f), Vector3.one, "High_Header_Valve");
        }

        static void BuildBelowDeckRisers(Transform parent)
        {
            Transform risers = Group("Risers_Conductors_And_Intake_Lines_To_YMinus10", parent);
            Vector3[] conductors =
            {
                new Vector3(5f, ProcessDeckY, -1f), new Vector3(9f, ProcessDeckY, -1f),
                new Vector3(5f, ProcessDeckY, 3f), new Vector3(9f, ProcessDeckY, 3f)
            };
            for (int i = 0; i < conductors.Length; i++)
            {
                string conductorPipe = i < 2 ? PipeLarge4 : PipeSmall4;
                PlaceBeamBetween(conductorPipe, 4f, risers, conductors[i], new Vector3(conductors[i].x, -10f, conductors[i].z), $"Drill_Conductor_{i:00}");
                foreach (float y in new[] { ServiceDeckY, 4f, WaterlineY, -4f, -8f })
                {
                    PlacePrefab(P(i < 2 ? "Pipes_Brackets/Pipe_Bracket_Large_Ring.prefab" : "Pipes_Brackets/Pipe_Bracket_Medium_Ring.prefab"), risers, new Vector3(conductors[i].x, y, conductors[i].z),
                        Vector3.zero, Vector3.one, $"Conductor_Guide_{i:00}_{y:0}");
                }
            }

            float[] riserX = { -14f, -10f, -6f, 14f, 18f, 22f };
            for (int i = 0; i < riserX.Length; i++)
            {
                Vector3 top = new Vector3(riserX[i], MainDeckY + 0.5f, 6f);
                Vector3 bottom = new Vector3(riserX[i], -8f, 6f);
                PlaceBeamBetween(PipeSmall4, 4f, risers, top, bottom, $"Process_Riser_{i:00}");
                PlacePrefab(P("Pipes_Small/Pipe_Sml_Corner_90_01_.prefab"), risers, top, new Vector3(0f, 90f, 0f), Vector3.one, $"Riser_Top_Elbow_{i:00}");
                PlacePrefab(P("Pipes_Brackets/Pipe_Bracket_Medium_Ring.prefab"), risers, new Vector3(riserX[i], ServiceDeckY, 6f),
                    Vector3.zero, Vector3.one, $"Riser_Service_Guide_{i:00}");
                PlacePrefab(P("Pipes_Brackets/Pipe_Bracket_Medium_Ring.prefab"), risers, new Vector3(riserX[i], WaterlineY, 6f),
                    Vector3.zero, Vector3.one, $"Riser_Y0_Guide_{i:00}");
            }

            PlaceBeamBetween(PipeMedium4, 4f, risers, new Vector3(-18f, ServiceDeckY + 1f, 6f), new Vector3(24f, ServiceDeckY + 1f, 6f), "Cellar_Deck_Main_Manifold");
            PlacePrefab(P("Valves_Large/Valve_Large_01_.prefab"), risers, new Vector3(2f, ServiceDeckY + 1f, 6f), new Vector3(0f, 90f, 0f), Vector3.one, "Cellar_Manifold_Isolation_Valve");
        }

        static void BuildBuildingShell(Transform parent, float xMin, float xMax, float zMin, float zMax, float y, int southDoorIndex, int northWindowIndex)
        {
            PlaceWallRunX(parent, xMin, xMax, zMin, y, southDoorIndex, -1, "South");
            PlaceWallRunX(parent, xMin, xMax, zMax, y, -1, northWindowIndex, "North");
            PlaceWallRunZ(parent, xMin, zMin, zMax, y, -1, 1, "West");
            PlaceWallRunZ(parent, xMax, zMin, zMax, y, -1, 1, "East");
        }

        static void PlaceWallRunX(Transform parent, float xMin, float xMax, float z, float y, int doorIndex, int windowIndex, string prefix)
        {
            int count = Mathf.RoundToInt((xMax - xMin) / 4f);
            for (int i = 0; i < count; i++)
            {
                string path = i == doorIndex ? WallDoor4 : i == windowIndex ? WallWindow4 : Wall4;
                PlacePrefab(path, parent, new Vector3(xMin + 4f * (i + 1), y, z), new Vector3(0f, 90f, 0f), Vector3.one, $"{prefix}_Wall_{i:00}");
            }
        }

        static void PlaceWallRunZ(Transform parent, float x, float zMin, float zMax, float y, int doorIndex, int windowIndex, string prefix)
        {
            int count = Mathf.RoundToInt((zMax - zMin) / 4f);
            for (int i = 0; i < count; i++)
            {
                string path = i == doorIndex ? WallDoor4 : i == windowIndex ? WallWindow4 : Wall4;
                PlacePrefab(path, parent, new Vector3(x, y, zMin + 4f * (i + 1)), Vector3.zero, Vector3.one, $"{prefix}_Wall_{i:00}");
            }
        }

        static void BuildRectFloor(Transform parent, float xMin, float xMax, float zMin, float zMax, float y, Func<float, float, bool> skip = null)
        {
            int xCount = Mathf.RoundToInt((xMax - xMin) / 4f);
            int zCount = Mathf.RoundToInt((zMax - zMin) / 4f);
            for (int ix = 0; ix < xCount; ix++)
            {
                float centerX = xMin + 2f + ix * 4f;
                for (int iz = 0; iz < zCount; iz++)
                {
                    float centerZ = zMin + 2f + iz * 4f;
                    if (skip != null && skip(centerX, centerZ))
                    {
                        continue;
                    }

                    PlacePrefab(Floor4, parent, new Vector3(centerX + 2f, y, centerZ + 2f), Vector3.zero, Vector3.one,
                        $"Floor_{centerX:0}_{centerZ:0}_{y:0}");
                }
            }
        }

        static GameObject PlacePrefab(string path, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, string name, bool makeStatic = true)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[OilRig] Missing prefab: {path}");
                GameObject missing = new GameObject("MISSING_" + name);
                missing.transform.SetParent(parent, false);
                missing.transform.position = position;
                return missing;
            }

            Scene scene = parent.gameObject.scene;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            instance.transform.localScale = scale;
            if (makeStatic)
            {
                SetStaticRecursive(instance);
            }
            return instance;
        }

        static GameObject PlaceBeamBetween(string path, float nominalLength, Transform parent, Vector3 start, Vector3 end, string name)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length <= 0.01f)
            {
                return Group(name + "_Empty", parent).gameObject;
            }
            Quaternion rotation = Quaternion.LookRotation(-direction.normalized, Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up);

            if (path.Contains("/Beams_80x40/"))
            {
                return PlaceDiscreteRun(parent, start, direction.normalized, rotation, Mathf.RoundToInt(length), 1f,
                    new[] { 12, 6, 4, 3, 2 },
                    new[]
                    {
                        P("Beams_80x40/Beam_80x40_12M.prefab"), P("Beams_80x40/Beam_80x40_6M.prefab"),
                        P("Beams_80x40/Beam_80x40_4M.prefab"), P("Beams_80x40/Beam_80x40_3M.prefab"),
                        P("Beams_80x40/Beam_80x40_2M.prefab")
                    }, name);
            }

            if (path.Contains("/Beams_40x20/"))
            {
                return PlaceDiscreteRun(parent, start, direction.normalized, rotation, Mathf.RoundToInt(length), 1f,
                    new[] { 6, 4, 3, 2 },
                    new[]
                    {
                        P("Beams_40x20/Beam_40x20_6M.prefab"), P("Beams_40x20/Beam_40x20_4M.prefab"),
                        P("Beams_40x20/Beam_40x20_3M.prefab"), P("Beams_40x20/Beam_40x20_2M.prefab")
                    }, name);
            }

            if (path.Contains("/Pipes_Large/"))
            {
                return PlaceConstantRadiusPipe(parent, start, direction.normalized, rotation, length, 0.38f, path, name);
            }

            if (path.Contains("/Pipes_Medium/"))
            {
                return PlaceConstantRadiusPipe(parent, start, direction.normalized, rotation, length, 0.24f, path, name);
            }

            if (path.Contains("/Pipes_Small/"))
            {
                return PlaceConstantRadiusPipe(parent, start, direction.normalized, rotation, length, 0.14f, path, name);
            }

            if (path.Contains("/AC_Units/Vent_Pipe"))
            {
                return PlaceDiscreteRun(parent, start, direction.normalized, rotation, Mathf.RoundToInt(length * 10f), 0.1f,
                    new[] { 40, 20, 10, 7, 3 },
                    new[]
                    {
                        P("AC_Units/Vent_Pipe_4M.prefab"), P("AC_Units/Vent_Pipe_2M.prefab"),
                        P("AC_Units/Vent_Pipe_1M_01.prefab"), P("AC_Units/Vent_Pipe_07M.prefab"),
                        P("AC_Units/Vent_Pipe_03M.prefab")
                    }, name);
            }

            int count = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(0.01f, nominalLength)));
            Transform fallback = Group(name, parent);
            for (int i = 0; i < count; i++)
            {
                PlacePrefab(path, fallback, start + direction.normalized * (nominalLength * i), rotation.eulerAngles, Vector3.one, $"{name}_{i:00}");
            }
            return fallback.gameObject;
        }

        static GameObject PlaceDiscreteRun(Transform parent, Vector3 start, Vector3 direction, Quaternion rotation,
            int targetUnits, float unitScale, int[] segmentUnits, string[] segmentPaths, string name)
        {
            Transform run = Group(name, parent);
            int remaining = targetUnits;
            int cursorUnits = 0;
            int segmentIndex = 0;
            int minimum = segmentUnits[segmentUnits.Length - 1];

            while (remaining >= minimum)
            {
                int selected = -1;
                for (int i = 0; i < segmentUnits.Length; i++)
                {
                    int candidate = segmentUnits[i];
                    int remainder = remaining - candidate;
                    if (candidate <= remaining && (remainder == 0 || remainder >= minimum))
                    {
                        selected = i;
                        break;
                    }
                }

                if (selected < 0)
                {
                    break;
                }

                PlacePrefab(segmentPaths[selected], run, start + direction * (cursorUnits * unitScale), rotation.eulerAngles,
                    Vector3.one, $"{name}_Segment_{segmentIndex:00}_{segmentUnits[selected]}");
                cursorUnits += segmentUnits[selected];
                remaining -= segmentUnits[selected];
                segmentIndex++;
            }

            return run.gameObject;
        }

        static GameObject PlaceConstantRadiusPipe(Transform parent, Vector3 start, Vector3 direction, Quaternion rotation,
            float length, float radius, string sourcePrefabPath, string name)
        {
            string meshFolder = GeneratedAssetRoot + "/PipeMeshes";
            if (!AssetDatabase.IsValidFolder(meshFolder))
            {
                AssetDatabase.CreateFolder(GeneratedAssetRoot, "PipeMeshes");
            }

            int lengthMillimeters = Mathf.Max(100, Mathf.RoundToInt(length * 1000f));
            int radiusMillimeters = Mathf.RoundToInt(radius * 1000f);
            string meshPath = $"{meshFolder}/Pipe_R{radiusMillimeters}_L{lengthMillimeters}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = CreatePipeMesh(length, radius, $"Pipe_R{radiusMillimeters}_L{lengthMillimeters}");
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(start, rotation);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            Renderer sourceRenderer = sourcePrefab != null ? sourcePrefab.GetComponentInChildren<Renderer>() : null;
            if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
            {
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            if (!IsUnderFloatingPlatform(parent))
            {
                SetStaticRecursive(go);
            }
            return go;
        }

        static bool IsUnderFloatingPlatform(Transform transform)
        {
            while (transform != null)
            {
                if (transform.name == "FloatingPlatformRoot") return true;
                transform = transform.parent;
            }
            return false;
        }

        static Mesh CreatePipeMesh(float length, float radius, string meshName)
        {
            const int radialSegments = 16;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int ring = 0; ring < 2; ring++)
            {
                float z = ring == 0 ? 0f : -length;
                for (int i = 0; i <= radialSegments; i++)
                {
                    float angle = i / (float)radialSegments * Mathf.PI * 2f;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    vertices.Add(radial * radius + Vector3.forward * z);
                    normals.Add(radial);
                    uvs.Add(new Vector2(i / (float)radialSegments, ring == 0 ? 0f : length * 0.5f));
                }
            }

            int ringSize = radialSegments + 1;
            for (int i = 0; i < radialSegments; i++)
            {
                int a = i;
                int b = i + 1;
                int c = ringSize + i;
                int d = ringSize + i + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }

            int startCenter = vertices.Count;
            vertices.Add(Vector3.zero);
            normals.Add(Vector3.forward);
            uvs.Add(new Vector2(0.5f, 0.5f));
            int endCenter = vertices.Count;
            vertices.Add(Vector3.back * length);
            normals.Add(Vector3.back);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < radialSegments; i++)
            {
                triangles.Add(startCenter); triangles.Add(i + 1); triangles.Add(i);
                triangles.Add(endCenter); triangles.Add(ringSize + i); triangles.Add(ringSize + i + 1);
            }

            Mesh mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void SetStaticRecursive(GameObject go)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
            }
        }

        static void CreateSpotLight(Transform parent, string name, Vector3 position, Vector3 direction, bool shadows)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(direction);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.58f, 0.31f);
            light.intensity = 70f;
            light.range = 32f;
            light.spotAngle = 58f;
            light.innerSpotAngle = 36f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.55f;
        }

        static void CreatePointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        static void CreateReflectionProbe(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = size;
            probe.boxProjection = true;
            probe.importance = 1;
            probe.resolution = 128;
        }

        static void CreatePreviewCamera(Transform parent, string name, Vector3 position, Vector3 target, bool enabled)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
            Camera camera = go.AddComponent<Camera>();
            camera.enabled = enabled;
            camera.fieldOfView = name == "Overview_Camera" ? 48f : 62f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 500f;
            camera.allowHDR = true;
            if (enabled)
            {
                go.tag = "MainCamera";
            }
        }

        static void QueuePreviewCapture(Scene scene, Scene previousActive, bool openedHere, GameObject root)
        {
            Directory.CreateDirectory(PreviewRoot);
            // A single deterministic overview is intentionally captured. Rendering
            // several close cameras back-to-back can exhaust Unity''s separate URP
            // shader compiler process on large purchased-material libraries.
            Camera[] cameras = root.GetComponentsInChildren<Camera>(true)
                .Where(camera => camera.name == "Overview_Camera")
                .ToArray();
            HashSet<string> expectedPreviews = new HashSet<string>(cameras.Select(camera => camera.name + ".png"));
            foreach (string existingPreview in Directory.GetFiles(PreviewRoot, "*.png"))
            {
                if (!expectedPreviews.Contains(Path.GetFileName(existingPreview)))
                {
                    AssetDatabase.DeleteAsset(existingPreview.Replace('\\', '/'));
                }
            }
            previewCapture = new PreviewCaptureState
            {
                scene = scene,
                previousActive = previousActive,
                openedHere = openedHere,
                root = root,
                cameras = cameras,
                index = 0,
                originalLayers = new Dictionary<GameObject, int>(),
                nextCaptureTime = EditorApplication.timeSinceStartup + 2.0
            };

            EditorApplication.delayCall += CaptureNextPreview;
        }

        static void CaptureNextPreview()
        {
            if (previewCapture == null || previewCapture.root == null || previewCapture.index >= previewCapture.cameras.Length)
            {
                FinishPreviewCapture();
                return;
            }

            if (EditorApplication.timeSinceStartup < previewCapture.nextCaptureTime)
            {
                EditorApplication.delayCall += CaptureNextPreview;
                return;
            }

            Camera camera = previewCapture.cameras[previewCapture.index];
            const int previewWidth = 960;
            const int previewHeight = 540;
            RenderTexture rt = RenderTexture.GetTemporary(previewWidth, previewHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            bool oldEnabled = camera.enabled;
            int oldCullingMask = camera.cullingMask;
            try
            {
                camera.enabled = false;
                camera.cullingMask = ~0;
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                Texture2D image = new Texture2D(previewWidth, previewHeight, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, previewWidth, previewHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes($"{PreviewRoot}/{camera.name}.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OilRig] Preview capture failed for {camera.name}: {exception.Message}");
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.enabled = oldEnabled;
                camera.cullingMask = oldCullingMask;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }

            previewCapture.index++;
            previewCapture.nextCaptureTime = EditorApplication.timeSinceStartup + 1.0;
            if (previewCapture.index < previewCapture.cameras.Length)
            {
                EditorApplication.delayCall += CaptureNextPreview;
            }
            else
            {
                EditorApplication.delayCall += FinishPreviewCapture;
            }
        }

        static void FinishPreviewCapture()
        {
            if (previewCapture == null)
            {
                return;
            }

            foreach (KeyValuePair<GameObject, int> layer in previewCapture.originalLayers)
            {
                if (layer.Key != null)
                {
                    layer.Key.layer = layer.Value;
                }
            }

            if (previewCapture.previousActive.IsValid() && previewCapture.previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previewCapture.previousActive);
            }
            if (previewCapture.openedHere && previewCapture.scene.IsValid() && previewCapture.scene.isLoaded)
            {
                EditorSceneManager.CloseScene(previewCapture.scene, true);
            }

            previewCapture = null;
            AssetDatabase.Refresh();
            Debug.Log("[OilRig] Isolated preview images captured.");
        }

        static void WriteValidationReport(Scene scene, GameObject root)
        {
            Directory.CreateDirectory(ReportRoot);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            int missingScripts = transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            int prefabInstances = transforms.Count(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            int nonUniformPrefabScales = transforms.Count(t =>
                PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                (Mathf.Abs(t.localScale.x - t.localScale.y) > 0.001f ||
                 Mathf.Abs(t.localScale.y - t.localScale.z) > 0.001f));
            Transform floatingRoot = transforms.FirstOrDefault(t => t.name == "FloatingPlatformRoot");
            CrestFloatingPlatform floatingFollower = root.GetComponentInChildren<CrestFloatingPlatform>(true);
            int buoyancySampleCount = floatingFollower != null && floatingFollower.samplePoints != null
                ? floatingFollower.samplePoints.Count(point => point != null)
                : 0;
            int movingStaticObjects = floatingRoot == null
                ? 0
                : floatingRoot.GetComponentsInChildren<Transform>(true).Count(t =>
                    GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0);
            bool hasFixedJacket = transforms.Any(t => t.name.IndexOf("Tapered_Jacket", StringComparison.OrdinalIgnoreCase) >= 0);
            int missingMaterials = 0;
            int errorShaders = 0;
            long triangles = 0;
            Dictionary<string, long> trianglesByMesh = new Dictionary<string, long>();
            Dictionary<string, int> instancesByMesh = new Dictionary<string, int>();
            bool hasBounds = false;
            Bounds bounds = default;

            foreach (Renderer renderer in renderers)
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        missingMaterials++;
                    }
                    else if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    {
                        errorShaders++;
                    }
                }
            }

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    long meshTriangles = 0;
                    for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                    {
                        meshTriangles += (long)filter.sharedMesh.GetIndexCount(i) / 3L;
                    }
                    triangles += meshTriangles;
                    string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (string.IsNullOrEmpty(meshPath))
                    {
                        meshPath = filter.sharedMesh.name;
                    }
                    trianglesByMesh[meshPath] = trianglesByMesh.TryGetValue(meshPath, out long previousTriangles)
                        ? previousTriangles + meshTriangles
                        : meshTriangles;
                    instancesByMesh[meshPath] = instancesByMesh.TryGetValue(meshPath, out int previousInstances)
                        ? previousInstances + 1
                        : 1;
                }
            }

            bool hasWaterObject = transforms.Any(t =>
                t.name.IndexOf("Ocean", StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 && t.name != "Waterline_Future_Y0");

            StringBuilder report = new StringBuilder();
            report.AppendLine("Oil Rig Above-Water Platform Validation");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Scene: {scene.path}");
            report.AppendLine($"Prefab instance roots: {prefabInstances}");
            report.AppendLine($"Non-uniformly scaled Prefab roots: {nonUniformPrefabScales}");
            report.AppendLine($"Floating platform controller: {(floatingFollower != null ? "FOUND" : "MISSING")}");
            report.AppendLine($"Crest buoyancy sample points: {buoyancySampleCount} (required: 4)");
            report.AppendLine($"Static objects under moving root: {movingStaticObjects}");
            report.AppendLine($"Fixed seabed jacket structure: {(hasFixedJacket ? "FOUND" : "none")}");
            report.AppendLine($"GameObjects: {transforms.Length}");
            report.AppendLine($"Renderers: {renderers.Length}");
            report.AppendLine($"Colliders: {colliders.Length}");
            report.AppendLine($"Lights: {lights.Length} (shadowed: {lights.Count(l => l.shadows != LightShadows.None)})");
            report.AppendLine($"Approximate rendered triangles (all active LOD meshes counted): {triangles:N0}");
            report.AppendLine($"Triangle budget (maximum 9,500,000): {(triangles <= 9500000L ? "PASS" : "OVER BUDGET")}");
            report.AppendLine($"Bounds center: {(hasBounds ? bounds.center.ToString("F2") : "n/a")}");
            report.AppendLine($"Bounds size: {(hasBounds ? bounds.size.ToString("F2") : "n/a")}");
            report.AppendLine($"Missing scripts: {missingScripts}");
            report.AppendLine($"Missing material slots: {missingMaterials}");
            report.AppendLine($"Error shaders: {errorShaders}");
            report.AppendLine($"Water/ocean objects: {(hasWaterObject ? "FOUND" : "none")}");
            report.AppendLine("Top triangle contributors:");
            foreach (KeyValuePair<string, long> item in trianglesByMesh.OrderByDescending(pair => pair.Value).Take(12))
            {
                report.AppendLine($"  {Path.GetFileNameWithoutExtension(item.Key)}: {item.Value:N0} tris across {instancesByMesh[item.Key]} instances");
            }
            bool passed = missingScripts == 0 && missingMaterials == 0 && errorShaders == 0 &&
                          !hasWaterObject && triangles <= 9500000L && nonUniformPrefabScales == 0 &&
                          floatingFollower != null && buoyancySampleCount == 4 && movingStaticObjects == 0 &&
                          !hasFixedJacket;
            report.AppendLine($"Result: {(passed ? "PASS" : "REVIEW REQUIRED")}");
            File.WriteAllText(ReportRoot + "/OilRigValidationReport.txt", report.ToString(), Encoding.UTF8);
            Debug.Log("[OilRig] Validation report written.\n" + report);
        }

        static Transform Group(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void CreateMarker(string name, Transform parent, Vector3 position)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }
            return scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
        }

        static void EnsureFolders()
        {
            EnsureFolder("Assets", "OilRigAssembly");
            EnsureFolder("Assets/OilRigAssembly", "Editor");
            EnsureFolder("Assets/OilRigAssembly", "Scenes");
            EnsureFolder("Assets/OilRigAssembly", "Generated");
            EnsureFolder("Assets/OilRigAssembly", "Previews");
            EnsureFolder("Assets/OilRigAssembly", "Reports");
        }

        static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        static string P(string relativePath)
        {
            return AssetRoot + "/" + relativePath;
        }
    }
}
#endif
