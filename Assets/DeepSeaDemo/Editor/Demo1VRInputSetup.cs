#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeepSeaAI;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace DeepSeaDemo.Editor
{
    public static class Demo1VRInputSetup
    {
        const string Folder = "Assets/DeepSeaDemo/Settings/1VR";
        const string ActionsPath = "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/XRI Default Input Actions.inputactions";

        [MenuItem("Tools/Deep Sea Demo/11 Configure 1-VR Copy Input")]
        public static void ConfigureSavedCopy()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or back up open scenes first.");
            if (!File.Exists(DeepSeaDemoBuilder.Copy1VR)) DeepSeaDemoBuilder.Prepare1VRCopy();
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(DeepSeaDemoBuilder.Copy1VR);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(DeepSeaDemoBuilder.Copy1VR, OpenSceneMode.Additive);
            try
            {
                Configure(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static DemoXRInput Configure(Scene scene)
        {
            if (scene.path != DeepSeaDemoBuilder.Copy1VR) throw new InvalidOperationException("Only the 1-VR copy can be configured.");
            var origins = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).ToArray();
            if (origins.Length != 1) throw new InvalidOperationException("Expected one inherited XR Origin; found " + origins.Length);
            var origin = origins[0];
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null) throw new InvalidOperationException("Starter input actions missing.");
            Directory.CreateDirectory(Folder);
            var input = origin.GetComponent<DemoXRInput>() ?? origin.gameObject.AddComponent<DemoXRInput>();
            input.left = Hand(actions, false); input.right = Hand(actions, true);
            input.simulatorRoots = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(b => b != null && (b.GetType().Name == "XRInteractionSimulator" || b.GetType().Name == "XRDeviceSimulator"))
                .Select(b => b.gameObject).Distinct().ToArray();
            var managers = origin.transform.root.GetComponentsInChildren<InputActionManager>(true);
            if (managers.Length == 0)
            {
                var manager = origin.gameObject.AddComponent<InputActionManager>();
                manager.actionAssets = new System.Collections.Generic.List<InputActionAsset> { actions };
            }
            foreach (var interactor in origin.GetComponentsInChildren<XRBaseInteractor>(true))
            {
                bool right = interactor.transform.GetComponentsInParent<Transform>(true).Any(t => t.name.Equals("Right", StringComparison.OrdinalIgnoreCase));
                var press = Ref(actions, "XRI " + (right ? "Right" : "Left") + " Interaction/UI Press");
                var value = Ref(actions, "XRI " + (right ? "Right" : "Left") + " Interaction/UI Press Value");
                if (interactor is NearFarInteractor nf)
                {
                    nf.enableUIInteraction = true;
                    nf.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.InputActionReference;
                    nf.uiPressInput.inputActionReferencePerformed = press; nf.uiPressInput.inputActionReferenceValue = value;
                }
                if (interactor is XRRayInteractor ray)
                {
                    ray.enableUIInteraction = true;
                    ray.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.InputActionReference;
                    ray.uiPressInput.inputActionReferencePerformed = press; ray.uiPressInput.inputActionReferenceValue = value;
                }
            }
            var events = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).ToArray();
            if (events.Length > 1) throw new InvalidOperationException("Duplicate EventSystems: resolve explicitly before UI testing.");
            var eventSystem = events.FirstOrDefault();
            if (eventSystem == null)
            {
                var go = new GameObject("Demo XR EventSystem", typeof(EventSystem)); SceneManager.MoveGameObjectToScene(go, scene); eventSystem = go.GetComponent<EventSystem>();
            }
            foreach (var module in eventSystem.GetComponents<BaseInputModule>()) if (module is not XRUIInputModule) module.enabled = false;
            var xr = eventSystem.GetComponent<XRUIInputModule>() ?? eventSystem.gameObject.AddComponent<XRUIInputModule>();
            xr.enabled = true; xr.enableXRInput = true; xr.enableMouseInput = input.editorMode == DemoXRInput.TestMode.DesktopUI;
            xr.enableTouchInput = false; xr.enableGamepadInput = false; xr.enableJoystickInput = false;
            foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
            {
                if (canvas.renderMode != RenderMode.WorldSpace) continue;
                canvas.worldCamera = origin.Camera;
                if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
            EditorUtility.SetDirty(input); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            Debug.Log("[DeepSeaDemo] Input System adapter configured. Tracking, movement parameters and existing hands preserved. Runtime testing still required.");
            return input;
        }
        static DemoXRInput.HandActions Hand(InputActionAsset actions, bool right)
        {
            string side = "XRI " + (right ? "Right" : "Left");
            return new DemoXRInput.HandActions {
                trigger = Ref(actions, side + " Interaction/Activate Value"), grip = Ref(actions, side + " Interaction/Select Value"),
                stick = Ref(actions, side + "/Thumbstick"), tracked = Ref(actions, side + "/Is Tracked"), menu = Menu(right)
            };
        }
        static InputActionReference Ref(InputActionAsset asset, string name)
        {
            var action = asset.FindAction(name, true);
            string path = Folder + "/" + name.Replace('/', '_').Replace(' ', '_') + ".asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (reference == null) { reference = InputActionReference.Create(action); AssetDatabase.CreateAsset(reference, path); }
            else { reference.Set(action); EditorUtility.SetDirty(reference); }
            return reference;
        }
        static InputActionReference Menu(bool right)
        {
            const string path = Folder + "/DemoMenuActions.asset";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<InputActionAsset>(); var map = asset.AddActionMap("Demo");
                map.AddAction("Left Menu", InputActionType.Button, "<XRController>{LeftHand}/menuButton");
                map.AddAction("Right Menu", InputActionType.Button, "<XRController>{RightHand}/menuButton");
                AssetDatabase.CreateAsset(asset, path);
            }
            return Ref(asset, "Demo/" + (right ? "Right" : "Left") + " Menu");
        }
    }
}
#endif
