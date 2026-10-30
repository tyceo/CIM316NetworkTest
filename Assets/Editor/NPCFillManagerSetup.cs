using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRMultiplayer;

public static class NPCFillManagerSetup
{
    private const string ScenePath = "Assets/Scenes/BasicScene.unity";
    private const string NpcPrefabPath = "Assets/Prefabs/NPC Competitor.prefab";
    private const string NpcMaterialPath = "Assets/Materials/NPC Competitor.mat";
    private const string NetworkPrefabListPath =
        "Assets/Prefabs/NPCCompetitorNetworkPrefabsList.asset";
    private const string NetworkManagerPrefabPath =
        "Assets/VRMPAssets 1/Prefabs/Managers/Network Manager VR Multiplayer.prefab";

    [MenuItem("Tools/Mini Mayhem/Setup NPC Competitors")]
    public static void ApplySetup()
    {
        GameObject npcPrefab = CreateNpcPrefab();
        ConfigureNetworkPrefabList(npcPrefab);
        ValidateNpcNetworkPrefab(npcPrefab);
        ConfigureGameScene(npcPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("NPC competitor prefab, network registration, and BasicScene setup are ready.");
    }

    [MenuItem("Tools/Mini Mayhem/Validate NPC Network Prefab")]
    public static void ValidateNpcNetworkPrefab()
    {
        GameObject npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
        if (npcPrefab == null)
        {
            throw new System.InvalidOperationException(
                $"NPC competitor prefab not found at {NpcPrefabPath}."
            );
        }

        ConfigureNetworkPrefabList(npcPrefab);
        ValidateNpcNetworkPrefab(npcPrefab);
        AssetDatabase.SaveAssets();
    }

    private static void ValidateNpcNetworkPrefab(GameObject npcPrefab)
    {
        NetworkObject networkObject = npcPrefab.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            throw new System.InvalidOperationException(
                "The NPC competitor prefab has no NetworkObject component."
            );
        }

        npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
        networkObject = npcPrefab != null ? npcPrefab.GetComponent<NetworkObject>() : null;
        NetworkPrefabsList prefabList =
            AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabListPath);
        if (networkObject == null ||
            networkObject.PrefabIdHash == 0 ||
            prefabList == null ||
            !prefabList.Contains(npcPrefab))
        {
            throw new System.InvalidOperationException(
                "The NPC prefab hash is zero or its NetworkPrefabsList registration is invalid."
            );
        }

        Debug.Log(
            $"NPC network prefab validation passed: GlobalObjectIdHash=" +
            $"{networkObject.PrefabIdHash}, registered={prefabList.Contains(npcPrefab)}."
        );
    }

    private static GameObject CreateNpcPrefab()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(NpcMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    "Could not find a supported shader for the NPC material."
                );
            }

            material = new Material(shader)
            {
                name = "NPC Competitor"
            };
            material.color = new Color(0.9f, 0.25f, 0.75f);
            AssetDatabase.CreateAsset(material, NpcMaterialPath);
        }

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
        if (existingPrefab != null)
        {
            return existingPrefab;
        }

        GameObject root = new("NPC Competitor");
        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkTransform>();
        root.AddComponent<NPCCompetitor>();

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        body.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().sharedMaterial = material;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        head.transform.localScale = Vector3.one * 0.38f;
        Object.DestroyImmediate(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().sharedMaterial = material;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, NpcPrefabPath);
        Object.DestroyImmediate(root);

        if (savedPrefab == null)
        {
            throw new System.InvalidOperationException("Failed to create the NPC competitor prefab.");
        }

        return savedPrefab;
    }

    private static void ConfigureNetworkPrefabList(GameObject npcPrefab)
    {
        NetworkPrefabsList prefabList =
            AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabListPath);

        if (prefabList == null)
        {
            prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(prefabList, NetworkPrefabListPath);
        }

        if (!prefabList.Contains(npcPrefab))
        {
            prefabList.Add(new NetworkPrefab { Prefab = npcPrefab });
            EditorUtility.SetDirty(prefabList);
        }

        GameObject networkManagerPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPrefabPath);
        if (networkManagerPrefab == null)
        {
            throw new System.InvalidOperationException(
                $"Network manager prefab not found at {NetworkManagerPrefabPath}."
            );
        }

        NetworkManager manager = networkManagerPrefab.GetComponent<NetworkManager>();
        if (manager == null)
        {
            throw new System.InvalidOperationException(
                "The multiplayer network manager prefab has no NetworkManager component."
            );
        }

        SerializedObject managerObject = new(manager);
        SerializedProperty config = managerObject.FindProperty("m_NetworkConfig");
        SerializedProperty prefabLists = config?
            .FindPropertyRelative("Prefabs")?
            .FindPropertyRelative("NetworkPrefabsLists");

        if (prefabLists == null || !prefabLists.isArray)
        {
            throw new System.InvalidOperationException(
                "Could not locate the NetworkManager's serialized network prefab list."
            );
        }

        for (int i = 0; i < prefabLists.arraySize; i++)
        {
            if (prefabLists.GetArrayElementAtIndex(i).objectReferenceValue == prefabList)
            {
                return;
            }
        }

        int newIndex = prefabLists.arraySize;
        prefabLists.InsertArrayElementAtIndex(newIndex);
        prefabLists.GetArrayElementAtIndex(newIndex).objectReferenceValue = prefabList;
        managerObject.ApplyModifiedProperties();
        PrefabUtility.SavePrefabAsset(networkManagerPrefab);
    }

    private static void ConfigureGameScene(GameObject npcPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            MinigameManager minigameManager =
                FindInScene<MinigameManager>(scene);

            if (minigameManager == null)
            {
                throw new System.InvalidOperationException(
                    "No MinigameManager was found in BasicScene."
                );
            }

            NPCFillManager fillManager =
                minigameManager.GetComponent<NPCFillManager>();
            if (fillManager == null)
            {
                fillManager = Undo.AddComponent<NPCFillManager>(minigameManager.gameObject);
            }

            SerializedObject fillManagerObject = new(fillManager);
            fillManagerObject.FindProperty("targetCompetitorCount").intValue = 4;
            fillManagerObject.FindProperty("npcPrefab").objectReferenceValue = npcPrefab;

            SerializedObject minigameManagerObject = new(minigameManager);
            SerializedProperty existingSpawns =
                minigameManagerObject.FindProperty("spawnLocations");
            SerializedProperty configuredSpawns =
                fillManagerObject.FindProperty("spawnPoints");

            configuredSpawns.arraySize = existingSpawns.arraySize;
            for (int i = 0; i < existingSpawns.arraySize; i++)
            {
                configuredSpawns.GetArrayElementAtIndex(i).objectReferenceValue =
                    existingSpawns.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            fillManagerObject.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
