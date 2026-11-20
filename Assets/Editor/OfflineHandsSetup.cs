using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using XRMultiplayer;

public static class OfflineHandsSetup
{
    const string k_ScenePath = "Assets/Scenes/BasicScene.unity";
    const string k_OnlineAvatarPath = "Assets/VRMPAssets 1/Prefabs/PlayerPrefabs/XRI_Network_Player_Avatar.prefab";
    const string k_LeftHandPrefabPath = "Assets/VRMPAssets 1/Prefabs/PlayerPrefabs/Hand_Left_Networked.prefab";
    const string k_RightHandPrefabPath = "Assets/VRMPAssets 1/Prefabs/PlayerPrefabs/Hand_Right_Networked.prefab";
    const string k_HandsMaterialPath = "Assets/VRMPAssets 1/Materials/AvatarMaterials/DitherMaterials/Hands_Dither.mat";

    [MenuItem("Tools/Mini Mayhem/Configure Offline Hands")]
    public static void Configure()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != k_ScenePath)
        {
            var dirtyScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(loadedScene => loadedScene.isDirty)
                .Select(loadedScene => loadedScene.path)
                .ToArray();
            if (dirtyScenes.Length > 0)
                throw new InvalidOperationException(
                    $"Save open scene(s) before configuring offline hands: {string.Join(", ", dirtyScenes)}.");

            scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
        }

        var origins = UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (origins.Length != 1)
            throw new InvalidOperationException($"Expected one XR Origin in {k_ScenePath}, found {origins.Length}.");

        var origin = origins[0];
        var root = origin.transform;
        var modalityManager = origin.GetComponent<XRInputModalityManager>();
        if (modalityManager == null)
            throw new InvalidOperationException("The BasicScene XR Origin has no XRInputModalityManager.");

        var leftHandGroup = GetModalityGroup(root, modalityManager.leftHand, "Left Hand");
        var rightHandGroup = GetModalityGroup(root, modalityManager.rightHand, "Right Hand");
        var leftController = GetModalityGroup(root, modalityManager.leftController, "Left Controller");
        var rightController = GetModalityGroup(root, modalityManager.rightController, "Right Controller");
        var leftQuestVisual = FindChild(leftHandGroup, "Left Hand Quest Visual");
        var rightQuestVisual = FindChild(rightHandGroup, "Right Hand Quest Visual");
        var leftAndroidVisual = FindChild(leftHandGroup, "Left Hand Android XR Visual");
        var rightAndroidVisual = FindChild(rightHandGroup, "Right Hand Android XR Visual");

        modalityManager.leftHand = leftHandGroup.gameObject;
        modalityManager.rightHand = rightHandGroup.gameObject;
        modalityManager.leftController = leftController.gameObject;
        modalityManager.rightController = rightController.gameObject;
        PrefabUtility.RecordPrefabInstancePropertyModifications(modalityManager);

        leftQuestVisual.gameObject.SetActive(true);
        rightQuestVisual.gameObject.SetActive(true);
        leftAndroidVisual.gameObject.SetActive(false);
        rightAndroidVisual.gameObject.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(leftQuestVisual.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rightQuestVisual.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(leftAndroidVisual.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rightAndroidVisual.gameObject);

        var handsMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_HandsMaterialPath);
        if (handsMaterial == null)
            throw new InvalidOperationException($"Could not load hand material at {k_HandsMaterialPath}.");

        SetHandMaterials(leftQuestVisual.gameObject, handsMaterial, "LeftHand.fbx");
        SetHandMaterials(rightQuestVisual.gameObject, handsMaterial, "RightHand.fbx");

        var onlineAvatar = AssetDatabase.LoadAssetAtPath<GameObject>(k_OnlineAvatarPath);
        if (onlineAvatar == null)
            throw new InvalidOperationException($"Could not load online avatar at {k_OnlineAvatarPath}.");
        var replicator = onlineAvatar.GetComponentInChildren<XRHandPoseReplicator>(true);
        if (replicator == null)
            throw new InvalidOperationException("The online avatar has no XRHandPoseReplicator.");
        var onlineSerialized = new SerializedObject(replicator);

        var leftControllerPosition = GetVector3(onlineSerialized, "m_HandControllerOffsets", 0);
        var rightControllerPosition = GetVector3(onlineSerialized, "m_HandControllerOffsets", 1);
        var leftControllerRotation = GetVector3(onlineSerialized, "m_HandControllerEulerOffsets", 0);
        var rightControllerRotation = GetVector3(onlineSerialized, "m_HandControllerEulerOffsets", 1);

        var leftControllerHand = InstantiateControllerHand(
            leftController,
            k_LeftHandPrefabPath,
            "Offline Left Hand Visual",
            leftControllerPosition,
            leftControllerRotation,
            handsMaterial);
        var rightControllerHand = InstantiateControllerHand(
            rightController,
            k_RightHandPrefabPath,
            "Offline Right Hand Visual",
            rightControllerPosition,
            rightControllerRotation,
            handsMaterial);

        ValidateHandVisual(leftControllerHand, handsMaterial, origin.Camera);
        ValidateHandVisual(rightControllerHand, handsMaterial, origin.Camera);

        var poseDriver = origin.GetComponent<OfflineControllerHandPose>();
        if (poseDriver == null)
            poseDriver = origin.gameObject.AddComponent<OfflineControllerHandPose>();

        ConfigurePoseDriver(
            poseDriver,
            leftControllerHand,
            rightControllerHand,
            onlineSerialized);
        PrefabUtility.RecordPrefabInstancePropertyModifications(poseDriver);

        var leftControllerModel = DisableControllerModel(leftController);
        var rightControllerModel = DisableControllerModel(rightController);
        ValidateScene(
            scene,
            origin,
            leftHandGroup,
            rightHandGroup,
            leftController,
            rightController,
            leftControllerHand,
            rightControllerHand,
            leftControllerModel,
            rightControllerModel,
            leftControllerPosition,
            rightControllerPosition,
            leftControllerRotation,
            rightControllerRotation,
            handsMaterial);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Could not save {k_ScenePath}.");

        Debug.Log("Configured and validated BasicScene offline hands.");
    }

    static Transform GetModalityGroup(Transform origin, GameObject configuredGroup, string fallbackName)
    {
        var group = configuredGroup != null ? configuredGroup.transform : FindChild(origin, fallbackName);
        if (group != origin && !group.IsChildOf(origin))
            throw new InvalidOperationException($"'{group.name}' is not a child of the BasicScene XR Origin.");
        return group;
    }

    static Transform FindChild(Transform parent, string name)
    {
        var match = parent.GetComponentsInChildren<Transform>(true).SingleOrDefault(child => child.name == name);
        if (match == null)
            throw new InvalidOperationException($"Could not find '{name}' under '{parent.name}'.");
        return match;
    }

    static JointBasedHand InstantiateControllerHand(
        Transform controller,
        string prefabPath,
        string instanceName,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Material handsMaterial)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Could not load hand prefab at {prefabPath}.");

        var instance = controller.GetComponentsInChildren<Transform>(true)
            .SingleOrDefault(child => child.parent == controller && child.name == instanceName)?.gameObject;
        if (instance == null)
        {
            instance = PrefabUtility.InstantiatePrefab(prefab, controller) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"Could not instantiate {prefabPath}.");
            instance.name = instanceName;
        }

        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEulerAngles);
        instance.transform.localScale = Vector3.one * 1.1f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

        SetHandMaterials(instance, handsMaterial, null);

        if (!instance.activeSelf)
        {
            instance.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        }

        var networkComponents = instance.GetComponentsInChildren<Component>(true)
            .Where(component => component != null)
            .Where(component => component.GetType().Name == "NetworkObject" ||
                                component.GetType().Name.Contains("NetworkTransform", StringComparison.Ordinal))
            .ToArray();
        if (networkComponents.Length > 0)
            throw new InvalidOperationException($"{instanceName} unexpectedly contains network components.");

        var jointBasedHand = instance.GetComponent<JointBasedHand>();
        if (jointBasedHand == null)
            throw new InvalidOperationException($"{instanceName} has no JointBasedHand component.");

        return jointBasedHand;
    }

    static GameObject DisableControllerModel(Transform controller)
    {
        var models = controller.GetComponentsInChildren<Transform>(true)
            .Where(child => child.name == "Controller_Base")
            .ToArray();
        if (models.Length != 1)
            throw new InvalidOperationException($"Expected one visual-only Controller_Base under {controller.name}, found {models.Length}.");

        var model = models[0].gameObject;
        var unsupportedComponents = model.GetComponents<Component>()
            .Where(component => component != null)
            .Where(component => component is not Transform &&
                                component is not MeshFilter &&
                                component is not MeshRenderer &&
                                component is not SkinnedMeshRenderer)
            .ToArray();
        if (unsupportedComponents.Length > 0)
            throw new InvalidOperationException($"Refusing to disable non-visual components on {model.name}.");
        if (model.GetComponentsInChildren<Renderer>(true).Length == 0)
            throw new InvalidOperationException($"{model.name} has no renderer to hide.");

        model.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        return model;
    }

    static void SetHandMaterials(GameObject handRoot, Material handsMaterial, string expectedMeshFileName)
    {
        var renderers = handRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException($"No skinned hand mesh found under {handRoot.name}.");

        foreach (var renderer in renderers)
        {
            var meshPath = AssetDatabase.GetAssetPath(renderer.sharedMesh);
            if (expectedMeshFileName != null &&
                !string.Equals(Path.GetFileName(meshPath), expectedMeshFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"{handRoot.name} uses mesh '{meshPath}', expected {expectedMeshFileName}.");

            var currentMaterials = renderer.sharedMaterials;
            if (currentMaterials.Length == 0)
                throw new InvalidOperationException($"{renderer.name} has no material slots.");

            renderer.sharedMaterials = Enumerable.Repeat(handsMaterial, currentMaterials.Length).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    static void ValidateHandVisual(JointBasedHand hand, Material handsMaterial, Camera camera)
    {
        if (camera == null)
            throw new InvalidOperationException("The BasicScene XR Origin has no camera for hand visibility validation.");

        var renderers = hand.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0 || !renderers.Any(renderer => renderer.enabled))
            throw new InvalidOperationException($"{hand.name} has no enabled skinned hand renderer.");

        foreach (var renderer in renderers)
        {
            if (renderer.sharedMesh == null)
                throw new InvalidOperationException($"{renderer.name} has no hand mesh.");
            if (renderer.sharedMaterials.Length == 0 ||
                renderer.sharedMaterials.Any(material => material != handsMaterial))
                throw new InvalidOperationException($"{renderer.name} is not using Hands_Dither.");
            if ((camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                throw new InvalidOperationException($"{renderer.name} is on a layer excluded by the XR camera.");
        }
    }

    static void ConfigurePoseDriver(
        OfflineControllerHandPose poseDriver,
        JointBasedHand leftHand,
        JointBasedHand rightHand,
        SerializedObject onlineSerialized)
    {
        var driverSerialized = new SerializedObject(poseDriver);
        driverSerialized.FindProperty("m_LeftHand").objectReferenceValue = leftHand;
        driverSerialized.FindProperty("m_RightHand").objectReferenceValue = rightHand;
        SetInputReference(driverSerialized, "m_LeftGripInput", GetInputReference(onlineSerialized, "m_GripInputProperties", 0));
        SetInputReference(driverSerialized, "m_RightGripInput", GetInputReference(onlineSerialized, "m_GripInputProperties", 1));
        SetInputReference(driverSerialized, "m_LeftTriggerInput", GetInputReference(onlineSerialized, "m_TriggerInputProperties", 0));
        SetInputReference(driverSerialized, "m_RightTriggerInput", GetInputReference(onlineSerialized, "m_TriggerInputProperties", 1));
        SetInputReference(driverSerialized, "m_LeftThumbTouchInput", GetInputReference(onlineSerialized, "m_ThumbTouchProperties", 0));
        SetInputReference(driverSerialized, "m_RightThumbTouchInput", GetInputReference(onlineSerialized, "m_ThumbTouchProperties", 1));
        driverSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static InputActionReference GetInputReference(SerializedObject serializedObject, string propertyName, int index)
    {
        var array = serializedObject.FindProperty(propertyName);
        if (array == null || index >= array.arraySize)
            throw new InvalidOperationException($"Missing {propertyName}[{index}] on the online hand pose replicator.");

        var reference = array.GetArrayElementAtIndex(index).FindPropertyRelative("m_Reference").objectReferenceValue;
        if (reference is not InputActionReference inputActionReference)
            throw new InvalidOperationException($"{propertyName}[{index}] has no input action reference.");
        return inputActionReference;
    }

    static Vector3 GetVector3(SerializedObject serializedObject, string propertyName, int index)
    {
        var array = serializedObject.FindProperty(propertyName);
        if (array == null || index >= array.arraySize)
            throw new InvalidOperationException($"Missing {propertyName}[{index}] on the online hand pose replicator.");

        return array.GetArrayElementAtIndex(index).vector3Value;
    }

    static void SetInputReference(SerializedObject serializedObject, string propertyName, InputActionReference reference)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Missing {propertyName} on OfflineControllerHandPose.");
        property.objectReferenceValue = reference;
    }

    static void ValidateScene(
        Scene scene,
        XROrigin origin,
        Transform leftHandGroup,
        Transform rightHandGroup,
        Transform leftController,
        Transform rightController,
        JointBasedHand leftControllerHand,
        JointBasedHand rightControllerHand,
        GameObject leftControllerModel,
        GameObject rightControllerModel,
        Vector3 leftControllerPosition,
        Vector3 rightControllerPosition,
        Vector3 leftControllerRotation,
        Vector3 rightControllerRotation,
        Material handsMaterial)
    {
        var manager = origin.GetComponent<XRInputModalityManager>();
        if (manager == null || manager.leftHand != leftHandGroup.gameObject || manager.rightHand != rightHandGroup.gameObject ||
            manager.leftController != leftController.gameObject || manager.rightController != rightController.gameObject)
            throw new InvalidOperationException("XRInputModalityManager hand/controller groups are not wired correctly.");

        if (!FindChild(leftHandGroup, "Left Hand Quest Visual").gameObject.activeSelf ||
            !FindChild(rightHandGroup, "Right Hand Quest Visual").gameObject.activeSelf ||
            FindChild(leftHandGroup, "Left Hand Android XR Visual").gameObject.activeSelf ||
            FindChild(rightHandGroup, "Right Hand Android XR Visual").gameObject.activeSelf)
            throw new InvalidOperationException("Offline hand visual activation would show the wrong or duplicate models.");

        var poseDriver = origin.GetComponent<OfflineControllerHandPose>();
        if (poseDriver == null)
            throw new InvalidOperationException("OfflineControllerHandPose was not saved on the XR Origin.");

        var visualRoots = new[]
        {
            FindChild(leftHandGroup, "Left Hand Quest Visual").gameObject,
            FindChild(rightHandGroup, "Right Hand Quest Visual").gameObject,
            leftControllerHand.gameObject,
            rightControllerHand.gameObject
        };
        foreach (var visualRoot in visualRoots)
        {
            foreach (var renderer in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material != handsMaterial))
                    throw new InvalidOperationException($"{renderer.name} is not using Hands_Dither in {scene.path}.");
            }
        }

        if (leftControllerHand.transform.parent != leftController ||
            rightControllerHand.transform.parent != rightController ||
            leftControllerHand.transform.localPosition != leftControllerPosition ||
            rightControllerHand.transform.localPosition != rightControllerPosition ||
            leftControllerHand.transform.localRotation != Quaternion.Euler(leftControllerRotation) ||
            rightControllerHand.transform.localRotation != Quaternion.Euler(rightControllerRotation) ||
            leftControllerHand.transform.localScale != Vector3.one * 1.1f ||
            rightControllerHand.transform.localScale != Vector3.one * 1.1f)
            throw new InvalidOperationException("Offline hand transforms do not match the online controller offsets.");

        if (leftControllerModel.activeSelf || rightControllerModel.activeSelf)
            throw new InvalidOperationException("The redundant white controller meshes are still active.");
    }
}
