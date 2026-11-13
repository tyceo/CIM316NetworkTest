using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRMultiplayer;
using TMPro;

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

            NPCCountLobbySlider countSlider =
                minigameManager.GetComponent<NPCCountLobbySlider>();
            if (countSlider == null)
            {
                countSlider = Undo.AddComponent<NPCCountLobbySlider>(
                    minigameManager.gameObject
                );
            }

            BuildLobbySlider(scene, countSlider, minigameManager);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void BuildLobbySlider(
        Scene scene,
        NPCCountLobbySlider countSlider,
        MinigameManager minigameManager)
    {
        Button startButton = null;
        TMP_Text startButtonText = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text == null || !text.text.Contains("Start Game with"))
                {
                    continue;
                }

                startButton = button;
                startButtonText = text;
                break;
            }

            if (startButton != null)
            {
                break;
            }
        }

        if (startButton == null || startButtonText == null)
        {
            throw new System.InvalidOperationException(
                "Could not find the lobby Start Game button and its TextMeshPro label in BasicScene."
            );
        }

        RectTransform startRect = startButton.transform as RectTransform;
        RectTransform canvasParent = startRect != null
            ? startRect.parent as RectTransform
            : null;
        Canvas canvas = startButton.GetComponentInParent<Canvas>();
        if (startRect == null ||
            canvasParent == null ||
            canvas == null ||
            canvas.renderMode != RenderMode.WorldSpace)
        {
            throw new System.InvalidOperationException(
                "The lobby Start Game button must be under the existing world-space lobby Canvas."
            );
        }

        if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
        {
            throw new System.InvalidOperationException(
                "The lobby Canvas has no TrackedDeviceGraphicRaycaster for XR UI interaction."
            );
        }

        Transform existingPanel = canvasParent.Find("NPC Competitors Panel");
        if (existingPanel != null)
        {
            Undo.DestroyObjectImmediate(existingPanel.gameObject);
        }

        const float panelWidth = 180f;
        const float panelHeight = 112f;
        GameObject panelObject = new(
            "NPC Competitors Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        SceneManager.MoveGameObjectToScene(panelObject, scene);
        Undo.RegisterCreatedObjectUndo(panelObject, "Create NPC competitor slider");
        panelObject.layer = canvas.gameObject.layer;
        panelObject.transform.SetParent(canvasParent, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = startRect.anchorMin;
        panelRect.anchorMax = startRect.anchorMax;
        panelRect.pivot = startRect.pivot;
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        panelRect.localScale = startRect.localScale;
        float buttonHeight = startRect.rect.height * Mathf.Abs(startRect.localScale.y);
        float panelHalfHeight = panelHeight * Mathf.Abs(startRect.localScale.y) * 0.5f;
        panelRect.anchoredPosition = startRect.anchoredPosition +
            new Vector2(
                0f,
                -buttonHeight * 0.5f - 0.12f - panelHalfHeight
            );

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = startButton.targetGraphic != null
            ? startButton.targetGraphic.color
            : new Color(0.08f, 0.3f, 0.72f, 1f);
        panelImage.raycastTarget = false;

        TextMeshProUGUI countLabel = CreateLabel(
            panelRect,
            "NPC Count Label",
            startButtonText,
            "NPC COMPETITORS: 0",
            new Vector2(0f, 33f),
            new Vector2(170f, 28f),
            18f
        );
        countLabel.fontStyle = FontStyles.Bold;

        GameObject sliderObject = new(
            "NPC Count Slider",
            typeof(RectTransform),
            typeof(Slider)
        );
        sliderObject.layer = panelObject.layer;
        sliderObject.transform.SetParent(panelRect, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        SetRectTransform(
            sliderRect,
            Vector2.zero,
            new Vector2(150f, 30f),
            new Vector2(0.5f, 0.5f)
        );

        RectTransform trackRect = CreateSliderImage(
            sliderRect,
            "Track",
            new Color(0.08f, 0.16f, 0.34f, 1f),
            new Vector2(0f, 0.35f),
            new Vector2(1f, 0.65f),
            new Vector2(-24f, 0f),
            true
        );
        trackRect.SetAsFirstSibling();

        RectTransform fillRect = CreateSliderImage(
            sliderRect,
            "Fill",
            new Color(0.35f, 0.82f, 1f, 1f),
            new Vector2(0f, 0.35f),
            new Vector2(0f, 0.65f),
            Vector2.zero,
            false
        );
        RectTransform handleRect = CreateSliderImage(
            sliderRect,
            "Handle",
            Color.white,
            new Vector2(0f, 0.05f),
            new Vector2(0f, 0.95f),
            new Vector2(24f, 0f),
            true
        );

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleRect.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 3f;
        slider.wholeNumbers = true;
        slider.value = 0f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        const float markerSpacing = 42f;
        for (int i = 0; i <= 3; i++)
        {
            CreateLabel(
                panelRect,
                $"NPC Count Mark {i}",
                startButtonText,
                i.ToString(),
                new Vector2((i - 1.5f) * markerSpacing, -38f),
                new Vector2(24f, 20f),
                18f
            );
        }

        SerializedObject sliderComponent = new(countSlider);
        sliderComponent.FindProperty("npcFillManager").objectReferenceValue =
            minigameManager.GetComponent<NPCFillManager>();
        sliderComponent.FindProperty("slider").objectReferenceValue = slider;
        sliderComponent.FindProperty("countLabel").objectReferenceValue = countLabel;
        sliderComponent.ApplyModifiedProperties();
    }

    private static TextMeshProUGUI CreateLabel(
        RectTransform parent,
        string objectName,
        TMP_Text sourceText,
        string text,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        GameObject labelObject = new(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        labelObject.layer = parent.gameObject.layer;
        labelObject.transform.SetParent(parent, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        SetRectTransform(labelRect, position, size, new Vector2(0.5f, 0.5f));

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = sourceText.font;
        label.fontSharedMaterial = sourceText.fontSharedMaterial;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.text = text;
        label.raycastTarget = false;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private static RectTransform CreateSliderImage(
        RectTransform parent,
        string objectName,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 size,
        bool raycastTarget)
    {
        GameObject imageObject = new(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        imageObject.layer = parent.gameObject.layer;
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return rect;
    }

    private static void SetRectTransform(
        RectTransform rect,
        Vector2 position,
        Vector2 size,
        Vector2 anchor)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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
