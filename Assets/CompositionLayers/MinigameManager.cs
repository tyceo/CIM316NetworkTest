using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRMultiplayer;
using UnityEngine.InputSystem;
using TMPro;

public class MinigameManager : NetworkBehaviour
{
    [SerializeField] private GameObject objectToHide;
    [SerializeField] private Transform[] spawnLocations = new Transform[8];
    [SerializeField] private float heightThreshold = 100f;
    [SerializeField] private Vector3 resetPosition = new Vector3(0, .15f, 0);
    [SerializeField] private float minigameStartDelay = 2f;
    [SerializeField] private float winnerDisplayDelay = 10f;
    [SerializeField] private TextMeshProUGUI currentMinigameText;
    [SerializeField] private TextMeshProUGUI playersEliminatedText;
    [SerializeField] private GameObject gunPrefab;
    [SerializeField] private GameObject liftObject;
    [SerializeField] private float liftDuration = 3f;
    [SerializeField] private GameObject bombPrefab;

    [SerializeField] private GameObject swordPrefab;

    [Header("Melt Minigame")]
    [SerializeField] private GameObject torchPrefab;
    [Tooltip("Offset from each spawn location where its torch appears.")]
    [SerializeField] private Vector3 torchSpawnOffset = new Vector3(0f, 1f, 0f);

    [Header("Round Audio")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip swordGuitarSound;
    [Range(0f, 1f)]
    [SerializeField] private float victoryVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float swordGuitarVolume = 1f;
    [SerializeField] private AudioSource audioSource;

    [Header("Music")]
    [SerializeField] private AudioClip inGameMusic;
    [SerializeField] private AudioClip calmMusic;
    [Range(0f, 1f)]
    [SerializeField] private float inGameMusicVolume = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float calmMusicVolume = 0.5f;

    [Header("Round Transition")]
    [SerializeField] private float transitionBreakDuration = 7f;

    [Header("Timed Minigames")]
    [SerializeField] private float swordRoundDuration = 20f;
    [SerializeField] private float gunRoundDuration = 5f;

    [Header("Round Start")]
    [SerializeField] private float startsNowDuration = 1f;

    private Coroutine timedMinigameCoroutine = null;

    private float liftYStart = -18f;
    private float liftYEnd = 127.7f;
    private float liftStayDuration = 5f;
    private Coroutine liftCoroutine = null;

    private NetworkVariable<bool> shouldHideObject = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> minigameRunning = new NetworkVariable<bool>(false);
    public NetworkVariable<int> currentMinigame = new NetworkVariable<int>(0);
    public float EliminationHeightThreshold => heightThreshold;

    private NetworkVariable<bool> isLoadingMinigame = new NetworkVariable<bool>(false);
    private float loadingMinigameDuration = 3f;

    private bool isProcessingWin = false;
    private bool isRoundTransitionRunning = false;

    // Positions the guns spawn at (captured from scene objects tagged "flashlight").
    private List<Vector3> gunSpawnPositions = new List<Vector3>();
    private List<GameObject> spawnedTorches = new List<GameObject>();

    // Server-side reference to the spawned sword (like spawnedGuns).
    private Dictionary<GameObject, Vector3> flashlightOriginalPositions = new Dictionary<GameObject, Vector3>();
    private Vector3 flashlightHidePosition = new Vector3(220.5f, -73.0199966f, -190f);
    private Coroutine moveFlashlightsCoroutine = null;

    // Server-side reference to the spawned sword (like spawnedGuns).
    private GameObject spawnedSword;

    // Local (per-client) reference used for grab/release pickup messages.
    private MinigameSword localSword;
    private XRGrabInteractable swordGrabInteractable;
    private bool swordWarningShownForPickup;
    private ulong swordWarningHolderClientId = ulong.MaxValue;

    private Vector3[] swordSpawnPositions = new Vector3[]
    {
        new Vector3(291.220001f, 133.660004f, -64.75f),
        new Vector3(317.769989f, 137.600006f, -79.1699982f),
        new Vector3(290.089996f, 133.720001f, -89.4199982f)
    };

    private List<GameObject> spawnedGuns = new List<GameObject>();
    private List<GameObject> spawnedBombs = new List<GameObject>();
    private int lastMinigame = -1;
    private int lastEliminatedCount = 0;

    int RollMinigame()
    {
        List<int> options = new List<int> { 1, 2, 3, 4 };
        options.Remove(lastMinigame);

        int chosen = options[Random.Range(0, options.Count)];
        lastMinigame = chosen;

        return chosen;
    }

    IEnumerator TransitionToMinigame(int newMinigame)
    {
        if (isRoundTransitionRunning || isProcessingWin)
        {
            yield break;
        }

        isRoundTransitionRunning = true;
        isLoadingMinigame.Value = true;
        minigameRunning.Value = false;

        StopTimedMinigame();

        // Hide the previous minigame using the ORIGINAL object logic.
        currentMinigame.Value = 0;

        CancelUIRpc();
        PlayCalmMusicRpc();

        ShowSurvivorCooldownRpc(
            "READY FOR NEXT MINIGAME\n" + GetPlayersRemainingMessage()
        );

        yield return new WaitForSeconds(transitionBreakDuration);

        if (isProcessingWin)
        {
            yield break;
        }

        CancelUIRpc();

        ShowActivePlayerMessageRpc(
            "MINIGAME IS LOADING...",
            minigameStartDelay
        );

        yield return new WaitForSeconds(minigameStartDelay);

        if (isProcessingWin)
        {
            yield break;
        }

        string minigameName = GetMinigameName(newMinigame);
        string playersMessage = GetPlayersRemainingMessage();
        string instruction = GetInstructionForMinigame(newMinigame);

        ShowRoundSequenceRpc(
            minigameName,
            playersMessage,
            instruction
        );

        yield return new WaitForSeconds(GetRoundSequenceDuration());

        if (isProcessingWin)
        {
            yield break;
        }

        UIManager.Instance.ShowStartMessage(
            "MINIGAME STARTS NOW!",
            startsNowDuration
        );

        yield return new WaitForSeconds(startsNowDuration);

        if (isProcessingWin)
        {
            yield break;
        }

        // ONLY NOW does the selected minigame become active.
        //original ice/flashlight/sword/gun/bomb code reacts to this.
        currentMinigame.Value = newMinigame;

        isLoadingMinigame.Value = false;
        minigameRunning.Value = true;
        isRoundTransitionRunning = false;

        PlayInGameMusicForSurvivorsRpc();
        StartTimedMinigame(newMinigame);
    }

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // If the old AudioSource already has the normal game track,
        // keep using it unless In Game Music is assigned explicitly.
        if (inGameMusic == null && audioSource.clip != null)
        {
            inGameMusic = audioSource.clip;
        }

        currentMinigame.OnValueChanged += OnMinigameChanged;
        isLoadingMinigame.OnValueChanged += (prev, next) => UpdateMinigameText();

        //StoreFlashlightPositions(); 
        StoreGunSpawnPositions();
        UpdateIceCubes();
        //UpdateFlashlights();
        UpdateTorches();
        UpdateSword();
        UpdateGuns();
        UpdateBombs();
        UpdateMinigameText();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowWelcomeMessage();
        }

        PlayCalmMusic();
    }

    void Update()
    {
        if (objectToHide != null)
        {
            objectToHide.SetActive(shouldHideObject.Value);
        }

        if (IsOwner && minigameRunning.Value && !isProcessingWin && !isLoadingMinigame.Value)
        {
            CheckPlayersHeight();
        }

        // Player count is shown only during the round-introduction sequence.
        
        if (IsOwner && Keyboard.current != null && Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            if (minigameRunning.Value && !isProcessingWin && !isRoundTransitionRunning)
            {
                Debug.Log("9 key pressed - transitioning to new minigame");
                int newMinigame = RollMinigame();
                StartCoroutine(TransitionToMinigame(newMinigame));
            }
            else if (!minigameRunning.Value && !isProcessingWin && !isRoundTransitionRunning)
            {
                Debug.Log("9 key pressed - starting new minigame");
                StartMinigame();
            }
        }
    }

    void OnMinigameChanged(int previousValue, int newValue)
    {
        UpdateIceCubes();
        //UpdateFlashlights();
        UpdateTorches();
        UpdateSword();
        UpdateGuns();
        UpdateBombs();
        UpdateMinigameText();
        ResetIceCubeSizes();
    }

    void UpdateMinigameText()
    {
        string message;

        if (isLoadingMinigame.Value)
        {
            message = "Currently loading";

            if (currentMinigameText != null)
            {
                currentMinigameText.text = message;
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetMinigameStatus(message);
            }

            return;
        }

        string minigameName = "";

        switch (currentMinigame.Value)
        {
            case 0:
                minigameName = "NONE";
                break;
            case 1:
                minigameName = "MELT";
                break;
            case 2:
                minigameName = "SWORD";
                break;
            case 3:
                minigameName = "ONESHOT";
                break;
            case 4:
                minigameName = "HOTPOTATO";
                break;
            default:
                minigameName = "UNKNOWN";
                break;
        }

        if (currentMinigame.Value == 0)
        {
            if (currentMinigameText != null)
            {
                currentMinigameText.text = "";
            }

            return;
        }

        message = "Current Minigame: " + minigameName;

        if (currentMinigameText != null)
        {
            currentMinigameText.text = message;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetMinigameStatus(message);
        }
    }

    void UpdatePlayersEliminatedText()
    {
        // Intentionally left blank.
        // The player count is shown before gameplay by StartRoundSequence().
    }

    void ResetIceCubeSizes()
    {
        GameObject[] iceCubes = GameObject.FindGameObjectsWithTag("icecube");

        if (iceCubes.Length == 0)
        {
            iceCubes = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            iceCubes = System.Array.FindAll(iceCubes, obj => obj.name.ToLower().Contains("icecube"));
        }

        foreach (GameObject iceCube in iceCubes)
        {
            IceShrinking iceShrinking = iceCube.GetComponent<IceShrinking>();

            if (iceShrinking != null)
            {
                iceShrinking.ResetTheSize();
            }
        }
    }

    void StoreGunSpawnPositions()
    {
        gunSpawnPositions.Clear();

        GameObject[] flashlights = GameObject.FindGameObjectsWithTag("flashlight");

        foreach (GameObject flashlight in flashlights)
        {
            gunSpawnPositions.Add(flashlight.transform.position);
        }
    }

    /*
    void StoreSwordPosition()
    {
        swordObject = GameObject.Find("Sword");

        if (swordObject != null)
        {
            swordOriginalPosition = swordObject.transform.position;
            swordGrabInteractable = swordObject.GetComponent<XRGrabInteractable>();

            if (swordGrabInteractable != null)
            {
                swordGrabInteractable.selectEntered.AddListener(OnSwordGrabbed);
                swordGrabInteractable.selectExited.AddListener(OnSwordReleased);
            }
            else
            {
                Debug.LogWarning(
                    "The Sword object needs an XRGrabInteractable to show pickup messages.",
                    swordObject
                );
            }
        }
    }
    */

    public void RegisterSword(MinigameSword sword)
    {
        if (sword == null)
        {
            return;
        }

        UnregisterSword(localSword);

        localSword = sword;
        swordGrabInteractable = sword.GrabInteractable;

        if (swordGrabInteractable != null)
        {
            swordGrabInteractable.selectEntered.AddListener(OnSwordGrabbed);
            swordGrabInteractable.selectExited.AddListener(OnSwordReleased);
        }
        else
        {
            Debug.LogWarning(
                "The Sword prefab needs an XRGrabInteractable to show pickup messages.",
                sword
            );
        }
    }

    public void UnregisterSword(MinigameSword sword)
    {
        if (sword == null || sword != localSword)
        {
            return;
        }

        if (swordGrabInteractable != null)
        {
            swordGrabInteractable.selectEntered.RemoveListener(OnSwordGrabbed);
            swordGrabInteractable.selectExited.RemoveListener(OnSwordReleased);
        }

        localSword = null;
        swordGrabInteractable = null;
    }

    public override void OnDestroy()
    {
        UnregisterSword(localSword);
        base.OnDestroy();
    }

    private void OnSwordGrabbed(SelectEnterEventArgs args)
    {
        if (!IsSwordMinigameActive())
        {
            return;
        }

        if (IsServer)
        {
            HandleSwordPickup(NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            SwordGrabbedServerRpc();
        }
    }

    private void OnSwordReleased(SelectExitEventArgs args)
    {
        if (swordGrabInteractable != null && swordGrabInteractable.isSelected)
        {
            return;
        }

        if (IsServer)
        {
            HandleSwordRelease(NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            SwordReleasedServerRpc();
        }
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SwordGrabbedServerRpc(RpcParams rpcParams = default)
    {
        HandleSwordPickup(rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SwordReleasedServerRpc(RpcParams rpcParams = default)
    {
        HandleSwordRelease(rpcParams.Receive.SenderClientId);
    }

    private bool IsSwordMinigameActive()
    {
        return currentMinigame.Value == 2 && minigameRunning.Value;
    }

    private void HandleSwordPickup(ulong holderClientId)
    {
        if (!IsServer ||
            !IsSwordMinigameActive() ||
            swordWarningShownForPickup)
        {
            return;
        }

        swordWarningShownForPickup = true;
        swordWarningHolderClientId = holderClientId;
        ShowSwordPickupMessageClientRpc(holderClientId);
    }

    private void HandleSwordRelease(ulong releasingClientId)
    {
        if (!IsServer || releasingClientId != swordWarningHolderClientId)
        {
            return;
        }

        swordWarningShownForPickup = false;
        swordWarningHolderClientId = ulong.MaxValue;
    }

    [Rpc(SendTo.Everyone)]
    private void ShowSwordPickupMessageClientRpc(ulong holderClientId)
    {
        XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;
        if (localPlayer == null ||
            localPlayer.transform.position.y > heightThreshold ||
            UIManager.Instance == null)
        {
            return;
        }

        string message = localPlayer.OwnerClientId == holderClientId
            ? "YOU HAVE THE SWORD!"
            : "RUN!";

        UIManager.Instance.ShowMessage(message, 2f);
    }

    void UpdateIceCubes()
    {
        GameObject[] iceCubes = GameObject.FindGameObjectsWithTag("icecube");

        if (iceCubes.Length == 0)
        {
            iceCubes = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            iceCubes = System.Array.FindAll(iceCubes, obj => obj.name.ToLower().Contains("icecube"));
        }

        bool shouldShow = currentMinigame.Value == 1;

        foreach (GameObject iceCube in iceCubes)
        {
            MeshRenderer meshRenderer = iceCube.GetComponent<MeshRenderer>();

            if (meshRenderer != null)
            {
                meshRenderer.enabled = shouldShow;
            }
        }
    }

    void UpdateTorches()
    {
        bool shouldSpawn = currentMinigame.Value == 1;

        if (shouldSpawn)
        {
            SpawnTorches();
        }
        else
        {
            CleanupTorches();
        }
    }

    void SpawnTorches()
    {
        if (!IsOwner) return;

        if (torchPrefab == null)
        {
            Debug.LogWarning("Torch prefab is not assigned!");
            return;
        }

        CleanupTorches();

        foreach (Transform spawnLocation in spawnLocations)
        {
            if (spawnLocation == null) continue;

            SpawnTorchServerRpc(
                spawnLocation.position + torchSpawnOffset,
                torchPrefab.transform.rotation
            );
        }
    }

    [Rpc(SendTo.Server)]
    void SpawnTorchServerRpc(Vector3 position, Quaternion rotation)
    {
        GameObject torch = Instantiate(torchPrefab, position, rotation);
        NetworkObject networkObject = torch.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.Spawn();
            spawnedTorches.Add(torch);
        }
        else
        {
            Debug.LogError("Torch prefab doesn't have a NetworkObject component!");
            Destroy(torch);
        }
    }

    void CleanupTorches()
    {
        if (!IsOwner) return;

        foreach (GameObject torch in spawnedTorches)
        {
            if (torch == null) continue;

            NetworkObject networkObject = torch.GetComponent<NetworkObject>();

            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn(true);
            }
            else
            {
                Destroy(torch);
            }
        }

        spawnedTorches.Clear();
    }

    void UpdateSword()
    {
        bool shouldSpawn = currentMinigame.Value == 2;

        if (shouldSpawn)
        {
            SpawnSword();
        }
        else
        {
            CleanupSword();
        }
    }

    void UpdateGuns()
    {
        bool shouldSpawn = currentMinigame.Value == 3;

        if (shouldSpawn)
        {
            SpawnGuns();
        }
        else
        {
            CleanupGuns();
        }
    }

    void SpawnSword()
    {
        if (!IsOwner) return;

        if (swordPrefab == null)
        {
            Debug.LogWarning("Sword prefab is not assigned!");
            return;
        }

        if (swordSpawnPositions.Length == 0)
        {
            Debug.LogWarning("No sword spawn positions defined!");
            return;
        }

        CleanupSword();

        int randomIndex = Random.Range(0, swordSpawnPositions.Length);
        SpawnSwordServerRpc(swordSpawnPositions[randomIndex], swordPrefab.transform.rotation);
    }

    [Rpc(SendTo.Server)]
    void SpawnSwordServerRpc(Vector3 position, Quaternion rotation)
    {
        GameObject sword = Instantiate(swordPrefab, position, rotation);
        NetworkObject networkObject = sword.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.Spawn();
            spawnedSword = sword;
        }
        else
        {
            Debug.LogError("Sword prefab doesn't have a NetworkObject component!");
            Destroy(sword);
        }
    }

    void CleanupSword()
    {
        if (!IsOwner) return;

        swordWarningShownForPickup = false;
        swordWarningHolderClientId = ulong.MaxValue;

        if (spawnedSword == null) return;

        NetworkObject networkObject = spawnedSword.GetComponent<NetworkObject>();

        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
        }
        else
        {
            Destroy(spawnedSword);
        }

        spawnedSword = null;
    }

    void UpdateBombs()
    {
        bool shouldSpawn = currentMinigame.Value == 4;

        if (shouldSpawn)
        {
            SpawnBomb();
        }
        else
        {
            CleanupBombs();
        }
    }

    void SpawnGuns()
    {
        if (!IsOwner) return;

        if (gunPrefab == null)
        {
            Debug.LogWarning("Gun prefab is not assigned!");
            return;
        }

        CleanupGuns();

        if (gunSpawnPositions.Count > 0)
        {
            foreach (Vector3 spawnPosition in gunSpawnPositions)
            {
                SpawnGunServerRpc(spawnPosition, gunPrefab.transform.rotation);
            }
        }
        else
        {
            foreach (Transform spawnLocation in spawnLocations)
            {
                if (spawnLocation == null) continue;

                SpawnGunServerRpc(
                    spawnLocation.position + torchSpawnOffset,
                    gunPrefab.transform.rotation
                );
            }
        }
    }

    [Rpc(SendTo.Server)]
    void SpawnGunServerRpc(Vector3 position, Quaternion rotation)
    {
        GameObject gun = Instantiate(gunPrefab, position, rotation);
        NetworkObject networkObject = gun.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.Spawn();
            spawnedGuns.Add(gun);
        }
        else
        {
            Debug.LogError("Gun prefab doesn't have a NetworkObject component!");
            Destroy(gun);
        }
    }

    void CleanupGuns()
    {
        if (!IsOwner) return;

        foreach (GameObject gun in spawnedGuns)
        {
            if (gun != null)
            {
                NetworkObject networkObject = gun.GetComponent<NetworkObject>();

                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                }

                Destroy(gun);
            }
        }

        spawnedGuns.Clear();
    }
    void SpawnBomb()
    {
        if (!IsOwner) return;

        if (bombPrefab == null)
        {
            Debug.LogWarning("Bomb prefab is not assigned!");
            return;
        }

        CleanupBombs();

        // Spawn a single bomb at a random spawn location
        if (spawnLocations.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnLocations.Length);
            Vector3 spawnPosition = spawnLocations[randomIndex].position;
            SpawnBombServerRpc(spawnPosition, Quaternion.identity);
        }
    }

    [Rpc(SendTo.Server)]
    void SpawnBombServerRpc(Vector3 position, Quaternion rotation)
    {
        GameObject bomb = Instantiate(bombPrefab, position, rotation);
        NetworkObject networkObject = bomb.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.Spawn();
            spawnedBombs.Add(bomb);
        }
        else
        {
            Debug.LogError("Bomb prefab doesn't have a NetworkObject component!");
            Destroy(bomb);
        }
    }

    void CleanupBombs()
    {
        if (!IsOwner) return;

        foreach (GameObject bomb in spawnedBombs)
        {
            if (bomb != null)
            {
                NetworkObject networkObject = bomb.GetComponent<NetworkObject>();

                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                }

                Destroy(bomb);
            }
        }

        spawnedBombs.Clear();
    }

    /*
    void DropAllFlashlights(GameObject[] flashlights)
    {
        foreach (GameObject flashlight in flashlights)
        {
            if (flashlight == null) continue;

            XRGrabInteractable grabInteractable = flashlight.GetComponent<XRGrabInteractable>();

            if (grabInteractable != null && grabInteractable.isSelected)
            {
                var interactors = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grabInteractable.interactorsSelecting);

                foreach (var interactor in interactors)
                {
                    grabInteractable.interactionManager.SelectExit(interactor, grabInteractable);
                }
            }
        }
    }
    

    void MoveFlashlightsToPosition(GameObject[] flashlights, bool shouldShow)
    {
        foreach (GameObject flashlight in flashlights)
        {
            if (flashlight == null) continue;

            Rigidbody rb = flashlight.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (shouldShow)
            {
                if (flashlightOriginalPositions.ContainsKey(flashlight))
                {
                    flashlight.transform.position = flashlightOriginalPositions[flashlight];
                }
            }
            else
            {
                flashlight.transform.position = flashlightHidePosition;
            }
        }
    }

    IEnumerator MoveFlashlightsCoroutine()
    {
        GameObject[] flashlights = GameObject.FindGameObjectsWithTag("flashlight");
        bool shouldShow = currentMinigame.Value == 1;

        yield return new WaitForSeconds(1f);

        DropAllFlashlights(flashlights);

        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            MoveFlashlightsToPosition(flashlights, shouldShow);
            elapsed += Time.deltaTime;
            yield return null;
        }

        MoveFlashlightsToPosition(flashlights, shouldShow);
    }
    */

    private List<XRINetworkPlayer> GetActiveNetworkPlayers()
    {
        XRINetworkPlayer[] foundPlayers =
            FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

        List<XRINetworkPlayer> activePlayers = new List<XRINetworkPlayer>();

        foreach (XRINetworkPlayer player in foundPlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                continue;
            }

            NetworkObject networkObject = player.GetComponent<NetworkObject>();

            if (networkObject != null && networkObject.IsSpawned)
            {
                activePlayers.Add(player);
            }
        }

        return activePlayers;
    }

    void CheckPlayersHeight()
    {
        List<XRINetworkPlayer> allPlayers = GetActiveNetworkPlayers();
        List<NPCCompetitor> npcCompetitors = GetActiveNpcCompetitors();
        int totalCompetitors = allPlayers.Count + npcCompetitors.Count;
        if (totalCompetitors == 0)
        {
            return;
        }

        int eliminatedCompetitors = 0;

        foreach (XRINetworkPlayer player in allPlayers)
        {
            CompetitorMeltTarget meltTarget =
                player.GetComponent<CompetitorMeltTarget>();
            if (player.transform.position.y > heightThreshold ||
                (meltTarget != null && meltTarget.IsEliminated))
            {
                eliminatedCompetitors++;
            }
        }

        foreach (NPCCompetitor competitor in npcCompetitors)
        {
            if (competitor.IsEliminated)
            {
                eliminatedCompetitors++;
            }
        }

        int competitorsRemaining = totalCompetitors - eliminatedCompetitors;

        // Check if competitors were eliminated in timed elimination minigames.
        if ((currentMinigame.Value == 2 || currentMinigame.Value == 4) && 
            eliminatedCompetitors > lastEliminatedCount &&
            eliminatedCompetitors > 0)
        {
            lastEliminatedCount = eliminatedCompetitors;
        
            if (competitorsRemaining > 1 && !isProcessingWin && !isRoundTransitionRunning)
            {
                int newMinigame = RollMinigame();
                StartCoroutine(TransitionToMinigame(newMinigame));
                return;
            }
        }

        // Update eliminated count for other minigames
        if (eliminatedCompetitors > lastEliminatedCount)
        {
            lastEliminatedCount = eliminatedCompetitors;
            ShowEliminatedGameOngoingRpc();
        }

        if (totalCompetitors > 1 &&
            competitorsRemaining <= 1 &&
            eliminatedCompetitors > 0 &&
            !isProcessingWin)
        {
            StartCoroutine(HandleMinigameEnd());
        }
    }

    IEnumerator HandleMinigameEnd()
    {
        if (isProcessingWin)
        {
            yield break;
        }

        isProcessingWin = true;
        isRoundTransitionRunning = false;
        minigameRunning.Value = false;
        shouldHideObject.Value = true;

        CancelUIRpc();
        ShowRoundResultRpc(
            CountRemainingHumans() > 0,
            CountRemainingNpcs() > 0
        );

        // Give the winner and eliminated players time to read their result.
        yield return new WaitForSeconds(5f);

        NPCFillManager npcFillManager = GetComponent<NPCFillManager>();
        if (npcFillManager != null)
        {
            npcFillManager.EndMatch();
        }

        CleanupGuns();
        CleanupBombs();
        CleanupSword();
        CleanupTorches();

        ResetAllPlayersRpc();
        yield return new WaitForSeconds(1f);

        lastEliminatedCount = 0;
        shouldHideObject.Value = false;

        StopTimedMinigame();

        currentMinigame.Value = 0;
        isLoadingMinigame.Value = false;
        minigameRunning.Value = false;

        CancelUIRpc();
        ShowWelcomeRpc();
        PlayCalmMusicRpc();

        // No auto restart. Wait for the host.
        isProcessingWin = false;
    }

    public void StartMinigame()
    {
        if (!IsOwner) return;

        // Store the lift coroutine reference before stopping others
        Coroutine savedLiftCoroutine = liftCoroutine;

        StopAllCoroutines();

        // Restart the lift coroutine if it was running
        if (savedLiftCoroutine != null)
        {
            liftCoroutine = StartCoroutine(LiftRoutine());
        }

        isProcessingWin = false;
        isRoundTransitionRunning = false;
        isLoadingMinigame.Value = false;
        shouldHideObject.Value = false;
        minigameRunning.Value = false;

        NPCFillManager npcFillManager = GetComponent<NPCFillManager>();
        if (npcFillManager != null)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            bool networkIsListening = networkManager != null && networkManager.IsListening;
            bool localIsSessionOwner =
                networkIsListening && networkManager.LocalClient.IsSessionOwner;
            HashSet<ulong> spawnedHumanClientIds = new();
            foreach (XRINetworkPlayer player in
                     FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None))
            {
                if (player != null &&
                    player.gameObject.activeInHierarchy &&
                    player.NetworkObject != null &&
                    player.NetworkObject.IsSpawned)
                {
                    spawnedHumanClientIds.Add(player.OwnerClientId);
                }
            }

            Debug.Log(
                $"[NPC Fill Diagnostic] StartMatch hook called. " +
                $"Listening={networkManager != null && networkManager.IsListening}, " +
                $"Server={networkManager != null && networkManager.IsServer}, " +
                $"DistributedAuthority={networkManager != null && networkManager.DistributedAuthorityMode}, " +
                $"SessionOwner={localIsSessionOwner}, " +
                $"ConnectedClients={(networkIsListening ? networkManager.ConnectedClientsIds.Count : 0)}, " +
                $"SpawnedHumanPlayers={spawnedHumanClientIds.Count}.",
                this
            );

            if (networkManager != null &&
                networkManager.DistributedAuthorityMode &&
                !networkManager.IsServer &&
                !localIsSessionOwner)
            {
                Debug.LogWarning(
                    "[NPC Fill Diagnostic] This client is neither the server nor the " +
                    "Distributed Authority session owner and cannot spawn NPCs.",
                    this
                );
            }

            npcFillManager.StartMatch();
            Debug.Log(
                $"[NPC Fill Diagnostic] Spawned NPCs after hook: {CountSpawnedNpcCompetitors()}.",
                this
            );
        }
        else
        {
            Debug.LogWarning(
                "[NPC Fill Diagnostic] StartMatch hook ran, but no NPCFillManager is attached.",
                this
            );
        }

        CancelUIRpc();
        HideWelcomeRpc();

        StopTimedMinigame();
        PlayCalmMusicRpc();

        StartCoroutine(StartMinigameDelayed());
    }

    private static int CountSpawnedNpcCompetitors()
    {
        int count = 0;
        foreach (NPCCompetitor competitor in
                 FindObjectsByType<NPCCompetitor>(FindObjectsSortMode.None))
        {
            if (competitor != null &&
                competitor.NetworkObject != null &&
                competitor.NetworkObject.IsSpawned)
            {
                count++;
            }
        }

        return count;
    }

    private List<NPCCompetitor> GetActiveNpcCompetitors()
    {
        List<NPCCompetitor> competitors = new();
        foreach (NPCCompetitor competitor in
                 FindObjectsByType<NPCCompetitor>(FindObjectsSortMode.None))
        {
            if (competitor != null &&
                competitor.NetworkObject != null &&
                competitor.NetworkObject.IsSpawned)
            {
                competitors.Add(competitor);
            }
        }

        return competitors;
    }

    private int CountRemainingHumans()
    {
        int remaining = 0;
        foreach (XRINetworkPlayer player in GetActiveNetworkPlayers())
        {
            CompetitorMeltTarget meltTarget =
                player.GetComponent<CompetitorMeltTarget>();
            if (player.transform.position.y <= heightThreshold &&
                (meltTarget == null || !meltTarget.IsEliminated))
            {
                remaining++;
            }
        }

        return remaining;
    }

    private int CountRemainingNpcs()
    {
        int remaining = 0;
        foreach (NPCCompetitor competitor in GetActiveNpcCompetitors())
        {
            if (!competitor.IsEliminated)
            {
                remaining++;
            }
        }

        return remaining;
    }

    [Rpc(SendTo.Everyone)]
    private void ShowGameCountdownRpc(int seconds)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessageNoTimer(
                seconds.ToString()
            );
        }
    }

    IEnumerator StartMinigameDelayed()
    {
        lastEliminatedCount = 0;
        isRoundTransitionRunning = true;
        isLoadingMinigame.Value = true;
        minigameRunning.Value = false;

        CancelUIRpc();

        int selectedMinigame = RollMinigame();

        // Nothing spawns while loading.
        currentMinigame.Value = 0;

        TeleportAllPlayersToSpawns();

        ShowActivePlayerMessageRpc(
            "MINIGAME IS LOADING...",
            minigameStartDelay
        );

        yield return new WaitForSeconds(minigameStartDelay);

        // 5-second countdown before the first minigame.
        CancelUIRpc();

        for (int seconds = 5; seconds >= 1; seconds--)
        {
            ShowGameCountdownRpc(seconds);
            yield return new WaitForSeconds(1f);
        }

        CancelUIRpc();

        ShowActivePlayerMessageRpc(
            "MINIGAME STARTS NOW!",
            startsNowDuration
        );

        yield return new WaitForSeconds(startsNowDuration);

        string minigameName = GetMinigameName(selectedMinigame);
        string playersMessage = GetPlayersRemainingMessage();
        string instruction = GetInstructionForMinigame(selectedMinigame);

        ShowRoundSequenceRpc(
            minigameName,
            playersMessage,
            instruction
        );

        yield return new WaitForSeconds(GetRoundSequenceDuration());

        // ORIGINAL object logic starts here.
        currentMinigame.Value = selectedMinigame;

        isLoadingMinigame.Value = false;
        isRoundTransitionRunning = false;
        minigameRunning.Value = true;

        PlayInGameMusicForSurvivorsRpc();
        StartTimedMinigame(selectedMinigame);
    }

    void TeleportAllPlayersToSpawns()
    {
        XRINetworkPlayer[] allPlayers = FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

        for (int i = 0; i < allPlayers.Length; i++)
        {
            int spawnIndex = i < spawnLocations.Length ? i : 0;
            TeleportPlayerRpc(allPlayers[i].OwnerClientId, spawnIndex);
        }

        if (IsOwner)
        {
            shouldHideObject.Value = false;
        }
    }

    [Rpc(SendTo.Everyone)]
    private void TeleportPlayerRpc(ulong playerClientId, int spawnIndex)
    {
        if (spawnLocations.Length == 0 || spawnLocations[spawnIndex] == null) return;

        if (XRINetworkPlayer.LocalPlayer != null && XRINetworkPlayer.LocalPlayer.OwnerClientId == playerClientId)
        {
            TeleportationProvider teleportationProvider = FindAnyObjectByType<TeleportationProvider>();

            if (teleportationProvider == null)
            {
                Debug.LogError("Local player does not have a teleportation provider!");
                return;
            }

            TeleportRequest teleportRequest = new TeleportRequest
            {
                destinationPosition = spawnLocations[spawnIndex].position,
                destinationRotation = spawnLocations[spawnIndex].rotation
            };

            teleportationProvider.QueueTeleportRequest(teleportRequest);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void ResetAllPlayersRpc()
    {
        TeleportationProvider teleportationProvider = FindAnyObjectByType<TeleportationProvider>();

        if (teleportationProvider != null)
        {
            TeleportRequest teleportRequest = new TeleportRequest
            {
                destinationPosition = resetPosition,
                destinationRotation = Quaternion.identity
            };

            teleportationProvider.QueueTeleportRequest(teleportRequest);
        }

        if (IsOwner)
        {
            shouldHideObject.Value = false;
        }
    }




    [Rpc(SendTo.Everyone)]
    private void CancelUIRpc()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CancelAllUI();
        }
    }


    [Rpc(SendTo.Everyone)]
    private void ShowEliminatedGameOngoingRpc()
    {
        if (UIManager.Instance == null)
        {
            return;
        }

        XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;

        if (localPlayer != null &&
            localPlayer.transform.position.y > heightThreshold)
        {
            UIManager.Instance.ShowMessageNoTimer(
                "YOU ARE ELIMINATED\nGAME ONGOING"
            );

            PlayMusic(calmMusic, calmMusicVolume);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void ShowSurvivorCooldownRpc(string message)
    {
        if (UIManager.Instance == null)
        {
            return;
        }

        XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;

        if (localPlayer != null &&
            localPlayer.transform.position.y > heightThreshold)
        {
            UIManager.Instance.ShowMessageNoTimer(
                "YOU ARE ELIMINATED\nGAME ONGOING"
            );
        }
        else
        {
            UIManager.Instance.ShowMessageNoTimer(message);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void ShowActivePlayerMessageRpc(
        string message,
        float duration)
    {
        if (UIManager.Instance == null)
        {
            return;
        }

        XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;

        if (localPlayer != null &&
            localPlayer.transform.position.y > heightThreshold)
        {
            UIManager.Instance.ShowMessageNoTimer(
                "YOU ARE ELIMINATED\nGAME ONGOING"
            );
        }
        else
        {
            UIManager.Instance.ShowMessage(message, duration);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void ShowRoundResultRpc(bool humanWon, bool npcWon)
    {
        if (UIManager.Instance == null)
        {
            return;
        }

        if (npcWon && !humanWon)
        {
            UIManager.Instance.ShowMessage("NPC WINS!", 3f);
            PlayVictorySound();
            return;
        }

        if (!humanWon)
        {
            UIManager.Instance.ShowMessage("NO WINNER", 3f);
            return;
        }

        XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;

        if (localPlayer != null &&
            localPlayer.transform.position.y <= heightThreshold)
        {
            UIManager.Instance.ShowMessage("YOU WIN!", 3f);
            PlayVictorySound();
        }
        else
        {
            UIManager.Instance.ShowMessage(
                "ELIMINATED\\nWAIT FOR THE ROUND TO FINISH",
                3f
            );
        }
    }

    [Rpc(SendTo.Everyone)]
    private void ShowWelcomeRpc()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowWelcomeMessage();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void HideWelcomeRpc()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideWelcomeMessage();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void ShowRoundSequenceRpc(
        string minigameName,
        string playersMessage,
        string instruction)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.StartRoundSequence(
                minigameName,
                playersMessage,
                instruction
            );

            XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;

            if (minigameName == "SWORD" &&
                (localPlayer == null ||
                 localPlayer.transform.position.y <= heightThreshold))
            {
                PlaySwordGuitarSound();
            }
        }
    }

    private float GetRoundSequenceDuration()
    {
        if (UIManager.Instance != null)
        {
            return UIManager.Instance.RoundSequenceDuration;
        }

        // Safe fallback if a client has no UIManager reference.
        return 8.4f;
    }

    private string GetMinigameName(int minigame)
    {
        switch (minigame)
        {
            case 1:
                return "MELT";

            case 2:
                return "SWORD";

            case 3:
                return "ONESHOT";

            case 4:
                return "HOT POTATO";

            default:
                return "GET READY";
        }
    }

    private string GetPlayersRemainingMessage()
    {
        List<XRINetworkPlayer> allPlayers = GetActiveNetworkPlayers();
        List<NPCCompetitor> npcCompetitors = GetActiveNpcCompetitors();

        int totalPlayers = allPlayers.Count + npcCompetitors.Count;
        int eliminatedPlayers = 0;

        foreach (XRINetworkPlayer player in allPlayers)
        {
            CompetitorMeltTarget meltTarget =
                player.GetComponent<CompetitorMeltTarget>();
            if (player.transform.position.y > heightThreshold ||
                (meltTarget != null && meltTarget.IsEliminated))
            {
                eliminatedPlayers++;
            }
        }

        foreach (NPCCompetitor competitor in npcCompetitors)
        {
            if (competitor.IsEliminated)
            {
                eliminatedPlayers++;
            }
        }

        int playersRemaining = Mathf.Max(0, totalPlayers - eliminatedPlayers);

        return "PLAYERS REMAINING: " + playersRemaining + " / " + totalPlayers;
    }

    [Rpc(SendTo.Everyone)]
    private void ShowInstructionRpc(string message, float duration)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage(message, duration);
        }
    }

    private string GetInstructionForMinigame(int minigame)
    {
        switch (minigame)
        {
            case 1:
                return "USE THE FLASHLIGHT TO SHRINK THE OPPONENTS!";
            case 2:
                return "FIRST ONE TO GET THE SWORD CAN ELIMINATE THE OPPONENTS. SURVIVE THE ROUND!";
            case 3:
                return "SHOOT, DON'T SHOOT... JUST SURVIVE!";
            case 4:
                return "PASS THE BOMB! DON'T BE HOLDING IT WHEN IT EXPLODES!";
            default:
                return "GET READY!";
        }
    }

    private void StartTimedMinigame(int minigame)
    {
        if (!IsOwner)
        {
            return;
        }

        StopTimedMinigame();

        switch (minigame)
        {
            case 1:
                timedMinigameCoroutine = null;
                break;

            case 2:
                timedMinigameCoroutine = StartCoroutine(
                    TimedMinigameRoutine(
                        2,
                        swordRoundDuration,
                        "SWORD MINIGAME OVER!",
                        false
                    )
                );
                break;

            case 3:
                timedMinigameCoroutine = StartCoroutine(
                    TimedMinigameRoutine(
                        3,
                        gunRoundDuration,
                        "TIME'S UP!",
                        true
                    )
                );
                break;

            case 4:
                // Hot Potato stays controlled by Bomb.cs.
                timedMinigameCoroutine = null;
                break;
        }
    }

    private void StopTimedMinigame()
    {
        if (timedMinigameCoroutine != null)
        {
            StopCoroutine(timedMinigameCoroutine);
            timedMinigameCoroutine = null;
        }
    }

    private IEnumerator TimedMinigameRoutine(
        int minigame,
        float duration,
        string endMessage,
        bool visibleCountdown)
    {
        float remaining = duration;
        int lastShownSecond = -1;

        while (
            remaining > 0f &&
            currentMinigame.Value == minigame &&
            minigameRunning.Value &&
            !isProcessingWin &&
            !isRoundTransitionRunning)
        {
            if (visibleCountdown)
            {
                int second = Mathf.CeilToInt(remaining);

                if (second != lastShownSecond)
                {
                    lastShownSecond = second;

                    ShowActivePlayerMessageRpc(
                        "SHOOT, DON'T SHOOT... JUST SURVIVE!\n" + second,
                        1.05f
                    );
                }
            }

            remaining -= Time.deltaTime;
            yield return null;
        }

        if (
            currentMinigame.Value != minigame ||
            !minigameRunning.Value ||
            isProcessingWin ||
            isRoundTransitionRunning)
        {
            timedMinigameCoroutine = null;
            yield break;
        }

        minigameRunning.Value = false;

        ShowActivePlayerMessageRpc(endMessage, 2f);

        yield return new WaitForSeconds(2f);

        if (!isProcessingWin)
        {
            int nextMinigame = RollMinigame();

            timedMinigameCoroutine = null;
            isRoundTransitionRunning = false;

            StartCoroutine(
                TransitionToMinigame(nextMinigame)
            );
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PlayCalmMusicRpc()
    {
        PlayMusic(calmMusic, calmMusicVolume);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayInGameMusicForSurvivorsRpc()
    {
        XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;

        if (localPlayer != null &&
            localPlayer.transform.position.y > heightThreshold)
        {
            PlayMusic(calmMusic, calmMusicVolume);
        }
        else
        {
            PlayMusic(inGameMusic, inGameMusicVolume);
        }
    }

    private void PlayCalmMusic()
    {
        PlayMusic(calmMusic, calmMusicVolume);
    }

    private void PlayMusic(
        AudioClip clip,
        float volume)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.pitch = 1f;
        audioSource.Play();
    }

    private void PlayVictorySound()
    {
        if (audioSource == null || victorySound == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            victorySound,
            victoryVolume
        );
    }

    private void PlaySwordGuitarSound()
    {
        if (audioSource == null || swordGuitarSound == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            swordGuitarSound,
            swordGuitarVolume
        );
    }

    public void TriggerLift()
    {
        if (!IsOwner) return;

        if (liftObject == null)
        {
            Debug.LogWarning("Lift object is not assigned!");
            return;
        }

        if (liftCoroutine != null)
        {
            StopCoroutine(liftCoroutine);
        }

        ShowInstructionRpc("LIFT ROUND!", 2f);
        liftCoroutine = StartCoroutine(LiftRoutine());
    }

    IEnumerator LiftRoutine()
    {
        // Explicitly set the lift to start position at the beginning
        Vector3 startPosition = new Vector3(
            liftObject.transform.position.x,
            liftYStart,
            liftObject.transform.position.z
        );
    
        Vector3 endPosition = new Vector3(
            liftObject.transform.position.x,
            liftYEnd,
            liftObject.transform.position.z
        );

        // Immediately set to start position
        liftObject.transform.position = startPosition;

        float timer = 0f;

        // Move up
        while (timer < liftDuration)
        {
            float progress = timer / liftDuration;
            liftObject.transform.position = Vector3.Lerp(startPosition, endPosition, progress);
            timer += Time.deltaTime;
            yield return null;
        }

        liftObject.transform.position = endPosition;

        yield return new WaitForSeconds(liftStayDuration);

        // Move down
        timer = 0f;

        while (timer < liftDuration)
        {
            float progress = timer / liftDuration;
            liftObject.transform.position = Vector3.Lerp(endPosition, startPosition, progress);
            timer += Time.deltaTime;
            yield return null;
        }

        liftObject.transform.position = startPosition;
        liftCoroutine = null; // Mark as complete
    }

    public void SpawnSingleGun()
    {
        if (!IsOwner) return;

        ShowInstructionRpc("SHOOT OR DON'T SHOOT, JUST SURVIVE!", 3f);

        currentMinigame.Value = 3;
        
    }
}