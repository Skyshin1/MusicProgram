#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Editor-only setup helpers for objects that should be picked up and used to
/// create volumetric-fog collision pulses. Uses the project's XRI Starter
/// Assets prefabs so the Select input bindings are kept intact.
/// </summary>
public static class SonarGrabSetup
{
    private const string LeftInteractorPath =
        "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/Interactors/Left_NearFarInteractor.prefab";
    private const string RightInteractorPath =
        "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/Interactors/Right_NearFarInteractor.prefab";

    [MenuItem("Tools/Sonar/Make Selected Object Grabbable")]
    private static void MakeSelectedObjectGrabbable()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Sonar Grab Setup", "Select an object with a Collider and Rigidbody first.", "OK");
            return;
        }

        if (selected.GetComponentInChildren<Collider>() == null ||
            selected.GetComponentInParent<Rigidbody>() == null)
        {
            EditorUtility.DisplayDialog(
                "Sonar Grab Setup",
                "The selected object needs a Collider and a Rigidbody before it can be grabbed.",
                "OK");
            return;
        }

        if (selected.GetComponent<XRGrabInteractable>() == null)
            Undo.AddComponent<XRGrabInteractable>(selected);

        if (selected.GetComponent<XRGrabRuntimeDiagnostics>() == null)
            Undo.AddComponent<XRGrabRuntimeDiagnostics>(selected);

        EditorSceneManager.MarkSceneDirty(selected.scene);
        Selection.activeGameObject = selected;
    }

    [MenuItem("Tools/Sonar/Set Up Active Scene Hands For Grabbing")]
    private static void SetUpActiveSceneHandsForGrabbing()
    {
        int created = SetUpHands(out int correctedHandedness, out int configuredToggle);
        EditorUtility.DisplayDialog(
            "Sonar Grab Setup",
            $"Hand interaction setup complete.\n\n" +
            $"Near/Far interactors created: {created}\n" +
            $"Handedness corrections: {correctedHandedness}\n\n" +
            $"Toggle-grab interactors configured: {configuredToggle}\n\n" +
            "Expand Player > Camera Offset > left / Right in the Hierarchy. Each hand must now contain a Left_NearFarInteractor or Right_NearFarInteractor child.",
            "OK");
    }

    [MenuItem("Tools/Sonar/Diagnose Active Scene Grabbing")]
    private static void DiagnoseActiveSceneGrabbing()
    {
        Transform left = FindTransformInActiveScene("left");
        Transform right = FindTransformInActiveScene("Right");
        GameObject interact = FindGameObjectInActiveScene("Interact");

        int leftInteractors = CountInteractors(left);
        int rightInteractors = CountInteractors(right);
        bool hasGrab = interact != null && interact.GetComponent<XRGrabInteractable>() != null;
        bool hasCollider = interact != null && interact.GetComponentInChildren<Collider>() != null;
        bool hasRigidbody = interact != null && interact.GetComponentInParent<Rigidbody>() != null;

        EditorUtility.DisplayDialog(
            "Sonar Grab Diagnosis",
            $"Active scene: {SceneManager.GetActiveScene().name}\n\n" +
            $"left hand found: {left != null}; interactors: {leftInteractors}\n" +
            $"Right hand found: {right != null}; interactors: {rightInteractors}\n" +
            $"Interact found: {interact != null}\n" +
            $"XR Grab Interactable: {hasGrab}\n" +
            $"Collider: {hasCollider}\n" +
            $"Rigidbody: {hasRigidbody}\n\n" +
            (leftInteractors > 0 && rightInteractors > 0 && hasGrab && hasCollider && hasRigidbody
                ? "The scene has the required grab components. Test with the controller Grip/Select input."
                : "A required component is missing. Run Tools > Sonar > Fix Current Sonar Grab Demo, then save the scene."),
            "OK");
    }

    private static int SetUpHands(out int correctedHandedness, out int configuredToggle)
    {
        Transform left = FindTransformInActiveScene("left");
        Transform right = FindTransformInActiveScene("Right");
        bool changed = false;

        int created = 0;
        if (AddInteractorIfMissing(left, LeftInteractorPath))
        {
            changed = true;
            created++;
        }
        if (AddInteractorIfMissing(right, RightInteractorPath))
        {
            changed = true;
            created++;
        }

        correctedHandedness = CorrectHandedness(left, 1) + CorrectHandedness(right, 2);
        changed |= correctedHandedness > 0;

        configuredToggle = ConfigureToggleGrab(left) + ConfigureToggleGrab(right);
        changed |= configuredToggle > 0;

        if (changed)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        return created;
    }

    [MenuItem("Tools/Sonar/Fix Current Sonar Grab Demo")]
    private static void FixCurrentSonarGrabDemo()
    {
        GameObject interact = FindGameObjectInActiveScene("Interact");
        if (interact != null)
        {
            Selection.activeGameObject = interact;
            MakeSelectedObjectGrabbable();
        }

        int created = SetUpHands(out int correctedHandedness, out int configuredToggle);
        DiagnoseActiveSceneGrabbing();
        Debug.Log($"Sonar grab setup finished. Created {created} Near/Far interactor prefab(s), corrected {correctedHandedness} hand setting(s), configured {configuredToggle} toggle-grab interactor(s).");
    }

    private static bool AddInteractorIfMissing(Transform hand, string prefabPath)
    {
        if (hand == null)
        {
            Debug.LogWarning($"Sonar grab setup could not find the hand transform for {prefabPath}.");
            return false;
        }

        string expectedName = prefabPath.Contains("Left_")
            ? "Left_NearFarInteractor"
            : "Right_NearFarInteractor";
        if (FindTransform(hand, expectedName) != null)
            return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Sonar grab setup could not load XRI prefab: {prefabPath}");
            return false;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hand);
        Undo.RegisterCreatedObjectUndo(instance, "Add XR Near-Far Interactor");
        instance.name = expectedName;
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        return true;
    }

    private static Transform FindTransformInActiveScene(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                    return transform;
            }
        }

        return null;
    }

    private static GameObject FindGameObjectInActiveScene(string name)
    {
        Transform transform = FindTransformInActiveScene(name);
        return transform != null ? transform.gameObject : null;
    }

    private static int CountInteractors(Transform root)
    {
        if (root == null)
            return 0;

        int count = 0;
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string typeName = component.GetType().Name;
            if (typeName.Contains("Interactor"))
                count++;
        }

        return count;
    }

    private static int CorrectHandedness(Transform hand, int expectedEnumValue)
    {
        if (hand == null)
            return 0;

        int corrected = 0;
        foreach (MonoBehaviour component in hand.GetComponents<MonoBehaviour>())
        {
            if (component == null || component.GetType().FullName != "UnityEngine.XR.Hands.XRHandTrackingEvents")
                continue;

            SerializedObject serializedComponent = new SerializedObject(component);
            SerializedProperty handedness = serializedComponent.FindProperty("m_Handedness");
            if (handedness != null && handedness.enumValueIndex != expectedEnumValue)
            {
                Undo.RecordObject(component, "Correct XR Handedness");
                handedness.enumValueIndex = expectedEnumValue;
                serializedComponent.ApplyModifiedProperties();
                corrected++;
            }
        }

        return corrected;
    }

    private static int ConfigureToggleGrab(Transform hand)
    {
        if (hand == null)
            return 0;

        int changed = 0;
        foreach (XRBaseInputInteractor interactor in hand.GetComponentsInChildren<XRBaseInputInteractor>(true))
        {
            if (interactor.selectActionTrigger == XRBaseInputInteractor.InputTriggerType.Toggle)
                continue;

            Undo.RecordObject(interactor, "Set XR Grab To Toggle");
            interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Toggle;
            EditorUtility.SetDirty(interactor);
            changed++;
        }

        return changed;
    }

    private static Transform FindTransform(Transform root, string name)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == name)
                return transform;
        }

        return null;
    }
}
#endif
