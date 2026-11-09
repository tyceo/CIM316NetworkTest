using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NPCPhaseADiagnostics
{
    private const string BasicScenePath = "Assets/Scenes/BasicScene.unity";
    private const string NpcPrefabPath = "Assets/Prefabs/NPC Competitor.prefab";
    private const string NavMeshAssetPath = "Assets/Scenes/BasicSceneNavMesh.asset";
    private const string RuntimeScreenshotPath = "/tmp/MiniMayhemNPCPhaseA.png";
    private const string RuntimeTestKey = "MiniMayhem.NPCPhaseA.RuntimeTest";

    private static readonly string[] ModelPaths =
    {
        "Assets/Thomas/Assets/Newer/Even Newer/MC1.fbx",
        "Assets/Thomas/Assets/Newer/Even Newer/MC2.fbx",
        "Assets/Thomas/Assets/Newer/Even Newer/MC3.fbx"
    };

    private static readonly System.Collections.Generic.List<GameObject> runtimeTestInstances =
        new();
    private static float runtimeTestStartTime;

    static NPCPhaseADiagnostics()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Mini Mayhem/Diagnose NPC Visual Instances")]
    private static void DiagnoseNpcVisualInstances()
    {
        var report = new StringBuilder();
        report.AppendLine($"Play Mode: {EditorApplication.isPlaying}");

        for (int i = 0; i < ModelPaths.Length; i++)
        {
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPaths[i]);
            if (sourceAsset == null)
            {
                report.AppendLine($"SOURCE PREFAB {ModelPaths[i]}: could not load model asset.");
                continue;
            }

            GameObject source = Object.Instantiate(sourceAsset);
            source.name = sourceAsset.name + " Source Diagnostic";
            report.AppendLine($"\nSOURCE PREFAB {ModelPaths[i]}");
            DumpHierarchy(source, report);
            Object.DestroyImmediate(source);
        }

        GameObject npcPrefab = PrefabUtility.LoadPrefabContents(NpcPrefabPath);
        NPCCompetitorVisuals visuals = npcPrefab.GetComponent<NPCCompetitorVisuals>();
        SerializedProperty visualReferences = visuals != null
            ? new SerializedObject(visuals).FindProperty("characterVisuals")
            : null;

        report.AppendLine("\nNPC PREFAB VISUALS");
        if (visualReferences == null)
        {
            report.AppendLine("NPCCompetitorVisuals.characterVisuals is missing.");
        }
        else
        {
            for (int i = 0; i < visualReferences.arraySize; i++)
            {
                GameObject visual = visualReferences.GetArrayElementAtIndex(i)
                    .objectReferenceValue as GameObject;
                if (visual == null)
                {
                    report.AppendLine($"Visual {i}: missing reference.");
                    continue;
                }

                visual.SetActive(true);
                report.AppendLine($"\nNPC PREFAB MODEL {visual.name}");
                DumpHierarchy(visual, report);
                visual.SetActive(false);
            }
        }

        PrefabUtility.UnloadPrefabContents(npcPrefab);

        NPCCompetitorVisuals[] instances = Object.FindObjectsByType<NPCCompetitorVisuals>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        report.AppendLine($"\nRUNTIME NPC INSTANCES: {instances.Length}");
        foreach (NPCCompetitorVisuals instance in instances)
        {
            report.AppendLine($"\nRUNTIME ROOT {instance.name}");
            DumpTransform(instance.transform, report);
            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                report.AppendLine(
                    $"NavMeshAgent enabled={agent.enabled}, onNavMesh={agent.isOnNavMesh}, " +
                    $"agentType={agent.agentTypeID}, areaMask={agent.areaMask}"
                );
            }

            NPCCompetitorVisuals serializedInstance = instance;
            SerializedProperty runtimeVisuals = new SerializedObject(serializedInstance)
                .FindProperty("characterVisuals");
            for (int i = 0; runtimeVisuals != null && i < runtimeVisuals.arraySize; i++)
            {
                GameObject visual = runtimeVisuals.GetArrayElementAtIndex(i)
                    .objectReferenceValue as GameObject;
                if (visual == null)
                {
                    report.AppendLine($"Runtime visual {i}: missing reference.");
                    continue;
                }

                report.AppendLine(
                    $"\nRUNTIME MODEL {visual.name}, activeSelf={visual.activeSelf}, " +
                    $"activeInHierarchy={visual.activeInHierarchy}"
                );
                DumpHierarchy(visual, report);
            }
        }

        foreach (Camera camera in Camera.allCameras)
        {
            report.AppendLine(
                $"\nCAMERA {camera.name}, enabled={camera.enabled}, " +
                $"cullingMask=0x{camera.cullingMask:X8}"
            );
        }

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Mini Mayhem/Build BasicScene NPC NavMesh")]
    private static void BuildBasicSceneNpcNavMesh()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play Mode before building the BasicScene NPC NavMesh.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != BasicScenePath)
        {
            Debug.LogError($"Open {BasicScenePath} before building the NPC NavMesh.");
            return;
        }

        NPCFillManager manager = Object.FindFirstObjectByType<NPCFillManager>(
            FindObjectsInactive.Include
        );
        if (manager == null)
        {
            Debug.LogError("Could not find NPCFillManager in BasicScene.");
            return;
        }

        SerializedProperty spawnPoints = new SerializedObject(manager).FindProperty("spawnPoints");
        if (spawnPoints == null || spawnPoints.arraySize == 0)
        {
            Debug.LogError("NPCFillManager has no configured spawn points.");
            return;
        }

        var points = new Transform[spawnPoints.arraySize];
        Bounds spawnBounds = default;
        int validCount = 0;
        for (int i = 0; i < spawnPoints.arraySize; i++)
        {
            points[i] = spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (points[i] == null)
            {
                Debug.LogError($"NPC spawn point {i} is not assigned.");
                return;
            }

            if (validCount == 0)
            {
                spawnBounds = new Bounds(points[i].position, Vector3.zero);
            }
            else
            {
                spawnBounds.Encapsulate(points[i].position);
            }

            validCount++;
        }

        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath) != null)
        {
            Debug.LogError(
                $"A NavMesh asset already exists at {NavMeshAssetPath}; inspect it before rebuilding."
            );
            return;
        }

        NavMeshSurface existingSurface = Object.FindFirstObjectByType<NavMeshSurface>(
            FindObjectsInactive.Include
        );
        if (existingSurface != null)
        {
            Debug.LogError(
                $"NavMeshSurface '{existingSurface.name}' already exists; inspect it before rebuilding."
            );
            return;
        }

        Vector3 surfacePosition = spawnBounds.center + Vector3.down;
        Vector3 surfaceSize = new(
            Mathf.Max(spawnBounds.size.x + 8f, 12f),
            12f,
            Mathf.Max(spawnBounds.size.z + 8f, 12f)
        );
        GameObject surfaceObject = new("NPC NavMesh Surface");
        Undo.RegisterCreatedObjectUndo(surfaceObject, "Build BasicScene NPC NavMesh");
        SceneManager.MoveGameObjectToScene(surfaceObject, scene);
        surfaceObject.transform.position = surfacePosition;
        NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.Volume;
        surface.center = Vector3.zero;
        surface.size = surfaceSize;
        surface.layerMask = ~0;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

        surface.BuildNavMesh();
        if (surface.navMeshData == null)
        {
            Undo.DestroyObjectImmediate(surfaceObject);
            Debug.LogError("NavMeshSurface did not produce NavMesh data.");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine(
            $"Built temporary NavMesh: agentType={surface.agentTypeID}, " +
            $"volume center={surfacePosition}, size={surfaceSize}, " +
            $"spawn bounds={spawnBounds}"
        );

        bool allSpawnsSample = true;
        for (int i = 0; i < points.Length; i++)
        {
            bool sampled = NavMesh.SamplePosition(
                points[i].position,
                out NavMeshHit hit,
                3f,
                NavMesh.AllAreas
            );
            report.AppendLine(
                $"Spawn {i} at {points[i].position}: sampled={sampled}" +
                (sampled ? $", nearest={hit.position}, distance={hit.distance:F3}" : string.Empty)
            );
            allSpawnsSample &= sampled;
        }

        if (!allSpawnsSample)
        {
            Undo.DestroyObjectImmediate(surfaceObject);
            Debug.LogError(
                "The candidate NavMesh did not cover every NPC spawn point; no scene changes were saved.\n" +
                report
            );
            return;
        }

        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError($"Failed to save {BasicScenePath}.");
            return;
        }

        Debug.Log(
            $"Saved and referenced {NavMeshAssetPath} from {BasicScenePath}.\n{report}"
        );
    }

    [MenuItem("Tools/Mini Mayhem/Test NPC Prefab In Play Mode")]
    private static void StartNpcPrefabPlayModeTest()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play Mode before starting the isolated NPC prefab test.");
            return;
        }

        if (SceneManager.GetActiveScene().path != BasicScenePath)
        {
            Debug.LogError($"Open {BasicScenePath} before testing the NPC prefab.");
            return;
        }

        EditorPrefs.SetBool(RuntimeTestKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            EditorPrefs.GetBool(RuntimeTestKey))
        {
            EditorPrefs.DeleteKey(RuntimeTestKey);
            runtimeTestInstances.Clear();
            runtimeTestStartTime = Time.realtimeSinceStartup;
            EditorApplication.update += RunNpcPrefabPlayModeTest;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= RunNpcPrefabPlayModeTest;
            EditorApplication.update -= StopNpcPrefabPlayModeTest;
            EditorPrefs.DeleteKey(RuntimeTestKey);
        }
    }

    private static void RunNpcPrefabPlayModeTest()
    {
        if (!EditorApplication.isPlaying ||
            Time.realtimeSinceStartup - runtimeTestStartTime < 1f)
        {
            return;
        }

        EditorApplication.update -= RunNpcPrefabPlayModeTest;
        NPCFillManager manager = Object.FindFirstObjectByType<NPCFillManager>(
            FindObjectsInactive.Include
        );
        if (manager == null)
        {
            Debug.LogError("Play Mode NPC test could not find NPCFillManager.");
            EditorApplication.isPlaying = false;
            return;
        }

        SerializedProperty spawnPoints = new SerializedObject(manager).FindProperty("spawnPoints");
        GameObject npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
        if (spawnPoints == null || npcPrefab == null || spawnPoints.arraySize < ModelPaths.Length)
        {
            Debug.LogError("Play Mode NPC test is missing its prefab or three spawn points.");
            EditorApplication.isPlaying = false;
            return;
        }

        var report = new StringBuilder("ISOLATED PLAY MODE NPC PREFAB TEST");
        for (int modelIndex = 0; modelIndex < ModelPaths.Length; modelIndex++)
        {
            Transform spawn = spawnPoints.GetArrayElementAtIndex(modelIndex)
                .objectReferenceValue as Transform;
            if (spawn == null)
            {
                report.AppendLine($"\nSpawn {modelIndex} is not assigned.");
                continue;
            }

            GameObject instance = Object.Instantiate(npcPrefab, spawn.position, spawn.rotation);
            instance.name = $"NPC Phase A Test MC{modelIndex + 1}";
            instance.transform.localScale = Vector3.one * 0.79f;
            runtimeTestInstances.Add(instance);

            foreach (NetworkBehaviour behaviour in instance.GetComponents<NetworkBehaviour>())
            {
                behaviour.enabled = false;
            }

            NPCCompetitorVisuals visuals = instance.GetComponent<NPCCompetitorVisuals>();
            SerializedProperty visualReferences = visuals != null
                ? new SerializedObject(visuals).FindProperty("characterVisuals")
                : null;
            if (visualReferences != null)
            {
                for (int i = 0; i < visualReferences.arraySize; i++)
                {
                    GameObject visual = visualReferences.GetArrayElementAtIndex(i)
                        .objectReferenceValue as GameObject;
                    if (visual != null)
                    {
                        visual.SetActive(i == modelIndex);
                    }
                }
            }

            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            bool sampled = NavMesh.SamplePosition(
                spawn.position,
                out NavMeshHit hit,
                3f,
                agent != null ? agent.areaMask : NavMesh.AllAreas
            );
            report.AppendLine(
                $"\nMC{modelIndex + 1}: spawn={spawn.position}, sample={sampled}" +
                (sampled ? $", nearest={hit.position}, distance={hit.distance:F3}" : string.Empty) +
                $", agentEnabled={agent != null && agent.enabled}, " +
                $"isOnNavMesh={(agent != null && agent.isOnNavMesh)}"
            );

            if (visualReferences != null && modelIndex < visualReferences.arraySize)
            {
                GameObject visual = visualReferences.GetArrayElementAtIndex(modelIndex)
                    .objectReferenceValue as GameObject;
                if (visual != null)
                {
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        Mesh mesh = filter != null ? filter.sharedMesh : null;
                        report.AppendLine(
                            $"  {renderer.name}: active={renderer.gameObject.activeInHierarchy}, " +
                            $"enabled={renderer.enabled}, layer={renderer.gameObject.layer}, " +
                            $"mesh={(mesh != null ? mesh.name : "<none>")}, " +
                            $"vertices={(mesh != null ? mesh.vertexCount : 0)}, " +
                            $"worldBounds={renderer.bounds}"
                        );
                    }
                }
            }
        }

        foreach (Camera camera in Camera.allCameras)
        {
            report.AppendLine(
                $"\nCAMERA {camera.name}: enabled={camera.enabled}, " +
                $"cullingMask=0x{camera.cullingMask:X8}"
            );
        }

        Debug.Log(report.ToString());
        ScreenCapture.CaptureScreenshot(RuntimeScreenshotPath);
        runtimeTestStartTime = Time.realtimeSinceStartup;
        EditorApplication.update += StopNpcPrefabPlayModeTest;
    }

    private static void StopNpcPrefabPlayModeTest()
    {
        if (Time.realtimeSinceStartup - runtimeTestStartTime < 2f)
        {
            return;
        }

        EditorApplication.update -= StopNpcPrefabPlayModeTest;
        foreach (GameObject instance in runtimeTestInstances)
        {
            if (instance != null)
            {
                Object.Destroy(instance);
            }
        }

        runtimeTestInstances.Clear();
        EditorApplication.isPlaying = false;
    }

    private static void DumpHierarchy(GameObject root, StringBuilder report)
    {
        DumpTransform(root.transform, report);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root.transform)
            {
                continue;
            }

            DumpTransform(child, report);
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            report.AppendLine(
                $"Renderer {renderer.name}: type={renderer.GetType().Name}, " +
                $"activeSelf={renderer.gameObject.activeSelf}, " +
                $"activeInHierarchy={renderer.gameObject.activeInHierarchy}, " +
                $"enabled={renderer.enabled}, layer={renderer.gameObject.layer}, " +
                $"mesh={(mesh != null ? mesh.name : "<none>")}, " +
                $"vertices={(mesh != null ? mesh.vertexCount : 0)}, " +
                $"meshBounds={(mesh != null ? mesh.bounds.ToString() : "<none>")}, " +
                $"localBounds={renderer.localBounds}, worldBounds={renderer.bounds}"
            );

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                report.AppendLine(
                    $"  Material {i}: {(material != null ? material.name : "<null>")}, " +
                    $"shader={(material != null && material.shader != null ? material.shader.name : "<none>")}, " +
                    $"supported={(material != null && material.shader != null && material.shader.isSupported)}"
                );
            }
        }
    }

    private static void DumpTransform(Transform transform, StringBuilder report)
    {
        report.AppendLine(
            $"Transform {transform.name}: activeSelf={transform.gameObject.activeSelf}, " +
            $"activeInHierarchy={transform.gameObject.activeInHierarchy}, " +
            $"localPosition={transform.localPosition}, worldPosition={transform.position}, " +
            $"localRotation={transform.localRotation.eulerAngles}, " +
            $"worldRotation={transform.rotation.eulerAngles}, localScale={transform.localScale}, " +
            $"lossyScale={transform.lossyScale}, parent={(transform.parent != null ? transform.parent.name : "<root>")}"
        );
    }
}
