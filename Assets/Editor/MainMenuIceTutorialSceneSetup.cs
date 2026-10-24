using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MainMenuIceTutorialSceneSetup
{
    private const string ScenePath = "Assets/Scenes/1 Start Scene.unity";
    private const string FlashlightPrefabPath = "Assets/Shell/Prefab Tutorial/FlashLighTutorial.prefab";
    private const string IceMaterialPath = "Assets/Shell/Materials/MainMenuFrostedIce.mat";
    private const string CrackMaterialPath = "Assets/Shell/Materials/MainMenuIceCracks.mat";

    private static readonly Vector3[][] CrackPaths =
    {
        new Vector3[]
        {
            new Vector3(-0.36f, 0.38f, -0.506f),
            new Vector3(-0.17f, 0.16f, -0.506f),
            new Vector3(-0.22f, -0.04f, -0.506f),
            new Vector3(0.02f, -0.21f, -0.506f),
            new Vector3(0.24f, -0.42f, -0.506f)
        },
        new Vector3[]
        {
            new Vector3(-0.17f, 0.16f, -0.506f),
            new Vector3(-0.40f, 0.12f, -0.506f),
            new Vector3(-0.48f, -0.02f, -0.506f)
        },
        new Vector3[]
        {
            new Vector3(-0.22f, -0.04f, -0.506f),
            new Vector3(-0.38f, -0.30f, -0.506f)
        },
        new Vector3[]
        {
            new Vector3(-0.02f, 0.39f, -0.506f),
            new Vector3(0.12f, 0.20f, -0.506f),
            new Vector3(0.31f, 0.10f, -0.506f),
            new Vector3(0.25f, -0.10f, -0.506f),
            new Vector3(0.42f, -0.28f, -0.506f)
        },
        new Vector3[]
        {
            new Vector3(0.25f, -0.10f, -0.506f),
            new Vector3(0.08f, -0.31f, -0.506f),
            new Vector3(0.02f, -0.43f, -0.506f)
        }
    };

    private static bool isSettingUp;

    static MainMenuIceTutorialSceneSetup()
    {
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorApplication.delayCall += SetupActiveScene;
    }

    [MenuItem("Tools/Mini Mayhem/Set Up Main Menu Ice Tutorial")]
    public static void SetupActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || isSettingUp)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == ScenePath)
        {
            SetupScene(activeScene);
            return;
        }

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene targetScene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSetup = !targetScene.IsValid() || !targetScene.isLoaded;
        if (openedForSetup)
        {
            targetScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        bool changedActiveScene = targetScene.IsValid() &&
            targetScene.isLoaded &&
            previousActiveScene != targetScene;

        if (changedActiveScene)
        {
            SceneManager.SetActiveScene(targetScene);
        }

        try
        {
            if (targetScene.IsValid() && targetScene.isLoaded)
            {
                SetupScene(targetScene);
            }
        }
        finally
        {
            if (openedForSetup && targetScene.IsValid() && targetScene.isLoaded)
            {
                EditorSceneManager.CloseScene(targetScene, true);
            }

            if (changedActiveScene && previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }
        }
    }

    private static void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        if (newScene.path == ScenePath)
        {
            EditorApplication.delayCall += SetupActiveScene;
        }
    }

    private static void SetupScene(Scene scene)
    {
        GameStartMenu menu = FindComponent<GameStartMenu>(scene);
        Camera mainCamera = FindMainCamera(scene);
        Transform rightHand = FindTransform(scene, "RightHand");
        GameObject flashlightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPrefabPath);

        if (menu == null || menu.startButton == null || mainCamera == null || rightHand == null || flashlightPrefab == null)
        {
            Debug.LogError("Main menu ice tutorial setup could not find the menu START button, main camera, RightHand, or tutorial flashlight prefab.");
            return;
        }

        isSettingUp = true;
        try
        {
            Material iceMaterial = CreateOrUpdateMaterial(
                IceMaterialPath,
                "Universal Render Pipeline/Lit",
                new Color(0.58f, 0.83f, 1f, 0.48f),
                true
            );
            Material crackMaterial = CreateOrUpdateMaterial(
                CrackMaterialPath,
                "Universal Render Pipeline/Unlit",
                new Color(0.65f, 0.87f, 1f, 0.82f),
                true
            );

            if (iceMaterial == null || crackMaterial == null)
            {
                Debug.LogError("Could not create the URP ice and crack materials. Verify the project uses URP.");
                return;
            }

            Transform spawnPoint = FindTransform(scene, "Main Menu Flashlight Spawn Point");
            if (spawnPoint == null)
            {
                GameObject spawnObject = new GameObject("Main Menu Flashlight Spawn Point");
                Undo.RegisterCreatedObjectUndo(spawnObject, "Create Tutorial Flashlight Spawn Point");
                spawnPoint = spawnObject.transform;
            }

            spawnPoint.SetParent(rightHand, false);
            spawnPoint.localPosition = new Vector3(0.04f, -0.03f, 0.14f);
            spawnPoint.localRotation = Quaternion.identity;
            spawnPoint.localScale = Vector3.one;

            Canvas promptCanvas = FindComponent<Canvas>(scene, "Main Menu Tutorial Prompt Canvas");
            if (promptCanvas == null)
            {
                GameObject canvasObject = new GameObject(
                    "Main Menu Tutorial Prompt Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler)
                );
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Tutorial Prompt Canvas");
                promptCanvas = canvasObject.GetComponent<Canvas>();
            }

            promptCanvas.renderMode = RenderMode.WorldSpace;
            promptCanvas.worldCamera = mainCamera;
            promptCanvas.sortingOrder = 20;
            promptCanvas.transform.SetParent(rightHand, false);
            promptCanvas.transform.localPosition = new Vector3(-0.24f, 0.18f, 0.25f);
            promptCanvas.transform.rotation = mainCamera.transform.rotation;
            promptCanvas.transform.localScale = Vector3.one * 0.001f;

            CanvasScaler canvasScaler = promptCanvas.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasScaler.dynamicPixelsPerUnit = 10f;

            TextMeshProUGUI prompt = FindComponent<TextMeshProUGUI>(scene, "Main Menu Tutorial Prompt Text");
            if (prompt == null)
            {
                GameObject textObject = new GameObject(
                    "Main Menu Tutorial Prompt Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI)
                );
                Undo.RegisterCreatedObjectUndo(textObject, "Create Tutorial Prompt Text");
                prompt = textObject.GetComponent<TextMeshProUGUI>();
            }

            RectTransform promptRect = prompt.rectTransform;
            promptRect.SetParent(promptCanvas.transform, false);
            promptRect.localPosition = Vector3.zero;
            promptRect.localRotation = Quaternion.identity;
            promptRect.localScale = Vector3.one;
            promptRect.sizeDelta = new Vector2(760f, 150f);
            prompt.text = "GRIP TO GRAB";
            prompt.font = TMP_Settings.defaultFontAsset;
            prompt.fontSize = 52f;
            prompt.color = Color.white;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.enableWordWrapping = false;
            prompt.raycastTarget = false;
            prompt.outlineColor = new Color(0.03f, 0.18f, 0.28f, 0.95f);
            prompt.outlineWidth = 0.16f;
            prompt.gameObject.SetActive(true);

            Transform iceTransform = FindTransform(scene, "Main Menu Tutorial Ice");
            if (iceTransform == null)
            {
                GameObject iceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(iceObject, "Create Main Menu Tutorial Ice");
                iceObject.name = "Main Menu Tutorial Ice";
                iceTransform = iceObject.transform;
            }

            RectTransform startRect = menu.startButton.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            startRect.GetWorldCorners(corners);
            Vector3 widthVector = corners[3] - corners[0];
            Vector3 heightVector = corners[1] - corners[0];
            Vector3 center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
            Vector3 towardCamera = mainCamera.transform.position - center;
            towardCamera.Normalize();

            iceTransform.SetPositionAndRotation(
                center + towardCamera * 0.14f,
                menu.startButton.transform.rotation
            );
            iceTransform.localScale = new Vector3(
                widthVector.magnitude * 1.04f,
                heightVector.magnitude * 1.10f,
                0.18f
            );

            Renderer iceRenderer = iceTransform.GetComponent<Renderer>();
            iceRenderer.sharedMaterial = iceMaterial;
            iceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            iceRenderer.receiveShadows = false;

            BoxCollider iceCollider = iceTransform.GetComponent<BoxCollider>();
            if (iceCollider == null)
            {
                iceCollider = iceTransform.gameObject.AddComponent<BoxCollider>();
            }
            iceCollider.center = Vector3.zero;
            iceCollider.size = Vector3.one;
            iceCollider.isTrigger = false;
            iceCollider.enabled = true;

            MainMenuIceTutorial tutorial = iceTransform.GetComponent<MainMenuIceTutorial>();
            if (tutorial == null)
            {
                tutorial = Undo.AddComponent<MainMenuIceTutorial>(iceTransform.gameObject);
            }

            CreateCrackLines(iceTransform, crackMaterial);
            tutorial.ConfigureInEditor(
                menu.startButton,
                flashlightPrefab,
                spawnPoint,
                prompt,
                iceRenderer,
                iceMaterial,
                crackMaterial
            );

            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(menu.startButton);
            EditorUtility.SetDirty(tutorial);
            EditorUtility.SetDirty(iceRenderer);
            EditorUtility.SetDirty(prompt);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Main menu ice tutorial objects were created, but Unity could not save the Start Scene.");
                return;
            }

            Debug.Log("Main menu ice tutorial scene setup complete and saved.");
        }
        finally
        {
            isSettingUp = false;
        }
    }

    private static Material CreateOrUpdateMaterial(string assetPath, string shaderName, Color color, bool transparent)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
        }

        Shader activeShader = Shader.Find(shaderName);
        if (activeShader != null && material.shader != activeShader)
        {
            material.shader = activeShader;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.92f);
        }
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.04f);
        }

        if (transparent && material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateCrackLines(Transform iceTransform, Material material)
    {
        LineRenderer[] existingLines = iceTransform.GetComponentsInChildren<LineRenderer>(true);
        if (existingLines.Length >= CrackPaths.Length)
        {
            return;
        }

        foreach (LineRenderer existingLine in existingLines)
        {
            Undo.DestroyObjectImmediate(existingLine.gameObject);
        }

        for (int crackIndex = 0; crackIndex < CrackPaths.Length; crackIndex++)
        {
            GameObject crackObject = new GameObject("Ice Crack " + (crackIndex + 1));
            Undo.RegisterCreatedObjectUndo(crackObject, "Create Ice Crack Detail");
            crackObject.transform.SetParent(iceTransform, false);

            LineRenderer line = crackObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = CrackPaths[crackIndex].Length;
            line.SetPositions(CrackPaths[crackIndex]);
            line.widthMultiplier = 0.004f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            EditorUtility.SetDirty(line);
        }
    }

    private static T FindComponent<T>(Scene scene, string gameObjectName = null) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            foreach (T component in components)
            {
                if (gameObjectName == null || component.gameObject.name == gameObjectName)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private static Camera FindMainCamera(Scene scene)
    {
        Camera[] cameras = FindComponents<Camera>(scene);
        foreach (Camera camera in cameras)
        {
            if (camera.CompareTag("MainCamera"))
            {
                return camera;
            }
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }

    private static T[] FindComponents<T>(Scene scene) where T : Component
    {
        System.Collections.Generic.List<T> found = new System.Collections.Generic.List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            found.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return found.ToArray();
    }

    private static Transform FindTransform(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == objectName)
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
