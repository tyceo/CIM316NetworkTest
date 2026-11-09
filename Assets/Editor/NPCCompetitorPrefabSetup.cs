using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class NPCCompetitorPrefabSetup
{
    private const string NpcPrefabPath = "Assets/Prefabs/NPC Competitor.prefab";
    private const string PlayerPrefabPath =
        "Assets/VRMPAssets 1/Prefabs/PlayerPrefabs/XRI_Network_Player_Avatar.prefab";
    private const float CharacterHeight = 1.7f;

    private static readonly string[] ModelPaths =
    {
        "Assets/Thomas/Assets/Newer/Even Newer/MC1.fbx",
        "Assets/Thomas/Assets/Newer/Even Newer/MC2.fbx",
        "Assets/Thomas/Assets/Newer/Even Newer/MC3.fbx"
    };

    static NPCCompetitorPrefabSetup()
    {
        EditorApplication.delayCall += ConfigureMissingPrefabs;
    }

    [MenuItem("Tools/Mini Mayhem/Configure NPC Competitor Prefabs")]
    public static void ConfigureMissingPrefabs()
    {
        ConfigureNpcPrefab();
        ConfigurePlayerPrefab();
        AssetDatabase.SaveAssets();
        ValidatePrefabs();
    }

    [MenuItem("Tools/Mini Mayhem/Validate NPC Competitor Prefabs")]
    public static void ValidatePrefabs()
    {
        GameObject npcPrefab = PrefabUtility.LoadPrefabContents(NpcPrefabPath);
        NPCCompetitorVisuals visuals = npcPrefab.GetComponent<NPCCompetitorVisuals>();
        SerializedObject visualsSerialized = visuals != null
            ? new SerializedObject(visuals)
            : null;
        SerializedProperty models = visualsSerialized?.FindProperty("characterVisuals");
        bool modelsAssigned = models != null && models.arraySize == ModelPaths.Length;
        if (modelsAssigned)
        {
            for (int i = 0; i < models.arraySize; i++)
            {
                GameObject model = models.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                GameObject source = model != null
                    ? PrefabUtility.GetCorrespondingObjectFromSource(model)
                    : null;
                modelsAssigned &=
                    model != null &&
                    model.transform.parent == npcPrefab.transform.Find("Character Visuals") &&
                    source != null &&
                    AssetDatabase.GetAssetPath(source) == ModelPaths[i] &&
                    model.GetComponentsInChildren<Renderer>(true).Length > 0;
            }
        }

        bool npcValid =
            npcPrefab.GetComponent<NetworkObject>() != null &&
            npcPrefab.GetComponent<NetworkTransform>() != null &&
            npcPrefab.GetComponent<NPCCompetitor>() != null &&
            npcPrefab.GetComponent<NPCCompetitorAI>() != null &&
            npcPrefab.GetComponent<NavMeshAgent>() != null &&
            npcPrefab.GetComponent<CompetitorMeltTarget>() != null &&
            modelsAssigned &&
            FindTaggedChild(npcPrefab.transform, "icecube") != null;
        PrefabUtility.UnloadPrefabContents(npcPrefab);

        GameObject playerPrefab = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        bool playerValid =
            playerPrefab.GetComponent<NetworkObject>() != null &&
            playerPrefab.GetComponent<XRMultiplayer.XRINetworkPlayer>() != null &&
            playerPrefab.GetComponent<CompetitorMeltTarget>() != null &&
            FindTaggedChild(playerPrefab.transform, "icecube") != null;
        PrefabUtility.UnloadPrefabContents(playerPrefab);

        if (!npcValid || !playerValid)
        {
            Debug.LogError(
                $"NPC prefab validation failed. NPC valid={npcValid}; player MELT target valid={playerValid}."
            );
            return;
        }

        Debug.Log(
            "NPC prefab validation passed: NetworkObject, NetworkTransform, competitor, AI, " +
            "NavMeshAgent, three synchronized model references, and MELT targets are configured."
        );
    }

    private static void ConfigureNpcPrefab()
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(NpcPrefabPath);
        bool changed = false;

        NPCCompetitorVisuals visuals = prefab.GetComponent<NPCCompetitorVisuals>();
        if (visuals == null)
        {
            GameObject visualRoot = new GameObject("Character Visuals");
            visualRoot.transform.SetParent(prefab.transform, false);

            GameObject[] models = new GameObject[ModelPaths.Length];
            for (int i = 0; i < ModelPaths.Length; i++)
            {
                GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPaths[i]);
                if (modelPrefab == null)
                {
                    Debug.LogError($"NPC character model was not found: {ModelPaths[i]}");
                    PrefabUtility.UnloadPrefabContents(prefab);
                    return;
                }

                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(
                    modelPrefab,
                    visualRoot.transform
                );
                OrientAndScaleModel(model);
                model.SetActive(i == 0);
                models[i] = model;
            }

            visuals = prefab.AddComponent<NPCCompetitorVisuals>();
            SerializedObject visualsSerialized = new SerializedObject(visuals);
            visualsSerialized.FindProperty("characterVisuals").arraySize = models.Length;
            for (int i = 0; i < models.Length; i++)
            {
                visualsSerialized.FindProperty("characterVisuals")
                    .GetArrayElementAtIndex(i).objectReferenceValue = models[i];
            }
            visualsSerialized.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (prefab.GetComponent<NPCCompetitorAI>() == null)
        {
            NPCCompetitorAI ai = prefab.AddComponent<NPCCompetitorAI>();
            NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = prefab.AddComponent<NavMeshAgent>();
            }

            agent.speed = 2.5f;
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.acceleration = 8f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.5f;
            agent.autoBraking = false;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            CapsuleCollider bodyCollider = prefab.GetComponent<CapsuleCollider>();
            if (bodyCollider == null)
            {
                bodyCollider = prefab.AddComponent<CapsuleCollider>();
            }
            bodyCollider.center = new Vector3(0f, 0.9f, 0f);
            bodyCollider.height = 1.8f;
            bodyCollider.radius = 0.3f;
            bodyCollider.isTrigger = true;

            SerializedObject aiSerialized = new SerializedObject(ai);
            aiSerialized.FindProperty("roamRadius").floatValue = 20f;
            aiSerialized.FindProperty("attackRange").floatValue = 7f;
            aiSerialized.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (ConfigureCharacterVisuals(prefab, visuals))
        {
            changed = true;
        }

        Transform head = prefab.transform.Find("Head");
        if (head != null)
        {
            head.localPosition = new Vector3(0f, CharacterHeight - 0.05f, 0f);
            head.localRotation = Quaternion.identity;
            head.localScale = Vector3.one;

            Transform body = prefab.transform.Find("Body");
            if (body != null)
            {
                body.gameObject.SetActive(false);
            }

            foreach (Renderer renderer in head.GetComponents<Renderer>())
            {
                renderer.enabled = false;
            }

            if (FindTaggedChild(prefab.transform, "icecube") == null)
            {
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                GameObject sourceIce = playerPrefab != null
                    ? FindTaggedChild(playerPrefab.transform, "icecube")
                    : null;
                MeshFilter sourceMesh = sourceIce != null
                    ? sourceIce.GetComponent<MeshFilter>()
                    : null;
                MeshRenderer sourceRenderer = sourceIce != null
                    ? sourceIce.GetComponent<MeshRenderer>()
                    : null;
                if (sourceMesh == null || sourceRenderer == null)
                {
                    Debug.LogError("Could not locate the existing player ice visual to reuse.");
                    PrefabUtility.UnloadPrefabContents(prefab);
                    return;
                }

                GameObject ice = new GameObject("icecube");
                ice.tag = "icecube";
                ice.transform.SetParent(head, false);
                ice.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                ice.transform.localRotation = Quaternion.identity;
                ice.transform.localScale = Vector3.one * 0.42f;
                ice.AddComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
                ice.AddComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
                SphereCollider iceCollider = ice.AddComponent<SphereCollider>();
                iceCollider.radius = 0.5f;
                iceCollider.isTrigger = true;
                ice.SetActive(false);
                changed = true;
            }
        }

        if (prefab.GetComponent<CompetitorMeltTarget>() == null)
        {
            CompetitorMeltTarget meltTarget = prefab.AddComponent<CompetitorMeltTarget>();
            SerializedObject meltSerialized = new SerializedObject(meltTarget);
            GameObject ice = FindTaggedChild(prefab.transform, "icecube");
            meltSerialized.FindProperty("iceVisual").objectReferenceValue =
                ice != null ? ice.transform : null;
            meltSerialized.FindProperty("meltPerSecond").floatValue = 0.35f;
            meltSerialized.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(prefab, NpcPrefabPath);
            Debug.Log("Configured NPC competitor prefab with models, AI, and MELT ice.");
        }

        PrefabUtility.UnloadPrefabContents(prefab);
    }

    private static void ConfigurePlayerPrefab()
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (prefab.GetComponent<CompetitorMeltTarget>() != null)
        {
            PrefabUtility.UnloadPrefabContents(prefab);
            return;
        }

        CompetitorMeltTarget meltTarget = prefab.AddComponent<CompetitorMeltTarget>();
        GameObject ice = FindTaggedChild(prefab.transform, "icecube");
        SerializedObject meltSerialized = new SerializedObject(meltTarget);
        meltSerialized.FindProperty("iceVisual").objectReferenceValue =
            ice != null ? ice.transform : null;
        meltSerialized.FindProperty("meltPerSecond").floatValue = 0.15f;
        meltSerialized.FindProperty("maximumFlashlightDistance").floatValue = 10f;
        meltSerialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(prefab);
        Debug.Log("Added server-authoritative MELT target to the network player prefab.");
    }

    private static void OrientAndScaleModel(GameObject model)
    {
        Vector3 importedScale = model.transform.localScale;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogError($"NPC model '{model.name}' has no renderers.", model);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 size = bounds.size;
        Vector3 longestAxis = size.x > size.y && size.x > size.z
            ? Vector3.right
            : size.z > size.y
                ? Vector3.forward
                : Vector3.up;
        model.transform.localRotation =
            Quaternion.FromToRotation(longestAxis, Vector3.up);

        bounds = GetRenderBounds(model);
        float uniformScale = CharacterHeight / Mathf.Max(bounds.size.y, 0.01f);
        model.transform.localScale = importedScale * uniformScale;
        model.transform.localPosition = Vector3.up * (-bounds.min.y * uniformScale);
    }

    private static bool ConfigureCharacterVisuals(GameObject prefab, NPCCompetitorVisuals visuals)
    {
        bool changed = false;
        Transform visualRoot = prefab.transform.Find("Character Visuals");
        if (visualRoot == null)
        {
            GameObject root = new GameObject("Character Visuals");
            visualRoot = root.transform;
            visualRoot.SetParent(prefab.transform, false);
            changed = true;
        }

        GameObject[] models = new GameObject[ModelPaths.Length];
        for (int i = 0; i < ModelPaths.Length; i++)
        {
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPaths[i]);
            if (modelPrefab == null)
            {
                Debug.LogError($"NPC character model was not found: {ModelPaths[i]}");
                return changed;
            }

            Transform existingModel = visualRoot.Find(modelPrefab.name);
            if (existingModel == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    modelPrefab,
                    visualRoot
                );
                if (instance == null)
                {
                    Debug.LogError($"Could not instantiate NPC character model: {ModelPaths[i]}");
                    return changed;
                }

                existingModel = instance.transform;
                changed = true;
            }

            models[i] = existingModel.gameObject;
            Vector3 previousPosition = existingModel.localPosition;
            Quaternion previousRotation = existingModel.localRotation;
            Vector3 previousScale = existingModel.localScale;
            existingModel.gameObject.SetActive(true);
            existingModel.localScale = modelPrefab.transform.localScale;
            OrientAndScaleModel(models[i]);
            if (previousPosition != existingModel.localPosition ||
                Quaternion.Angle(previousRotation, existingModel.localRotation) > 0.001f ||
                previousScale != existingModel.localScale)
            {
                changed = true;
            }

            bool shouldBeActive = i == 0;
            if (models[i].activeSelf != shouldBeActive)
            {
                models[i].SetActive(shouldBeActive);
                changed = true;
            }

            foreach (Transform child in models[i].GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.layer != prefab.layer)
                {
                    child.gameObject.layer = prefab.layer;
                    changed = true;
                }
            }

            if (models[i].GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                Debug.LogError($"NPC model '{models[i].name}' has no renderers.", models[i]);
            }
        }

        SerializedObject serializedVisuals = new SerializedObject(visuals);
        SerializedProperty references = serializedVisuals.FindProperty("characterVisuals");
        bool referencesChanged = references == null || references.arraySize != models.Length;
        if (references != null)
        {
            if (references.arraySize != models.Length)
            {
                references.arraySize = models.Length;
            }

            for (int i = 0; i < models.Length; i++)
            {
                SerializedProperty reference = references.GetArrayElementAtIndex(i);
                if (reference.objectReferenceValue != models[i])
                {
                    reference.objectReferenceValue = models[i];
                    referencesChanged = true;
                }
            }

            if (referencesChanged)
            {
                serializedVisuals.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
        }
        else
        {
            Debug.LogError("NPCCompetitorVisuals is missing its characterVisuals field.", visuals);
        }

        return changed;
    }

    private static Bounds GetRenderBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static GameObject FindTaggedChild(Transform root, string tag)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.CompareTag(tag))
            {
                return child.gameObject;
            }
        }

        return null;
    }
}
