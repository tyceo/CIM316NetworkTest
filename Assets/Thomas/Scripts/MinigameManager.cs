using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
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

    [Header("Round Audio")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip swordGuitarSound;
    [Range(0f, 1f)]
    [SerializeField] private float victoryVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float swordGuitarVolume = 1f;
    [SerializeField] private AudioSource audioSource;

    private float liftYStart = -18f;
    private float liftYEnd = 127.7f;
    private float liftStayDuration = 5f;
    private Coroutine liftCoroutine = null;

    private NetworkVariable<bool> shouldHideObject = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> minigameRunning = new NetworkVariable<bool>(false);
    public NetworkVariable<int> currentMinigame = new NetworkVariable<int>(0);

    private NetworkVariable<bool> isLoadingMinigame = new NetworkVariable<bool>(false);
    private float loadingMinigameDuration = 3f;

    private bool isProcessingWin = false;
    private bool isRoundTransitionRunning = false;

    private Dictionary<GameObject, Vector3> flashlightOriginalPositions = new Dictionary<GameObject, Vector3>();
    private Vector3 flashlightHidePosition = new Vector3(220.5f, -73.0199966f, -190f);
    private Coroutine moveFlashlightsCoroutine = null;

    private Vector3 swordOriginalPosition;
    private GameObject swordObject;
    private Coroutine moveSwordCoroutine = null;

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
    // Hot Potato (4) appears more often in the pool
        List<int> options = new List<int>
        {
            1, // Melt
            2, // Sword
            3, // One Shot
            4, // Hot Potato
            4  // Extra Hot Potato chance
        };

        // Still don't allow the same minigame twice in a row
        options.RemoveAll(x => x == lastMinigame);

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

        currentMinigame.Value = newMinigame;

        string minigameName = GetMinigameName(newMinigame);
        string playersMessage = GetPlayersRemainingMessage();
        string instruction = GetInstructionForMinigame(newMinigame);

        ShowRoundSequenceRpc(
            minigameName,
            playersMessage,
            instruction
        );

        yield return new WaitForSeconds(GetRoundSequenceDuration());

        isLoadingMinigame.Value = false;
        minigameRunning.Value = true;
        isRoundTransitionRunning = false;
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

        currentMinigame.OnValueChanged += OnMinigameChanged;
        isLoadingMinigame.OnValueChanged += (prev, next) => UpdateMinigameText();

        StoreFlashlightPositions();
        StoreSwordPosition();
        UpdateIceCubes();
        UpdateFlashlights();
        UpdateSword();
        UpdateGuns();
        UpdateBombs();
        UpdateMinigameText();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowWelcomeMessage();
        }
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

        if (IsOwner && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            currentMinigame.Value = currentMinigame.Value == 1 ? 0 : 1;
        }

        // Player count is shown only during the round-introduction sequence.
    }

    void OnMinigameChanged(int previousValue, int newValue)
    {
        UpdateIceCubes();
        UpdateFlashlights();
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

    void StoreFlashlightPositions()
    {
        GameObject[] flashlights = GameObject.FindGameObjectsWithTag("flashlight");

        foreach (GameObject flashlight in flashlights)
        {
            if (!flashlightOriginalPositions.ContainsKey(flashlight))
            {
                flashlightOriginalPositions[flashlight] = flashlight.transform.position;
            }
        }
    }

    void StoreSwordPosition()
    {
        swordObject = GameObject.Find("Sword");

        if (swordObject != null)
        {
            swordOriginalPosition = swordObject.transform.position;
        }
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

    void UpdateFlashlights()
    {
        if (moveFlashlightsCoroutine != null)
        {
            StopCoroutine(moveFlashlightsCoroutine);
        }

        moveFlashlightsCoroutine = StartCoroutine(MoveFlashlightsCoroutine());
    }

    void UpdateSword()
    {
        if (moveSwordCoroutine != null)
        {
            StopCoroutine(moveSwordCoroutine);
        }

        moveSwordCoroutine = StartCoroutine(MoveSwordCoroutine());
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

        GameObject[] flashlights = GameObject.FindGameObjectsWithTag("flashlight");

        foreach (GameObject flashlight in flashlights)
        {
            if (flashlightOriginalPositions.ContainsKey(flashlight))
            {
                Vector3 spawnPosition = flashlightOriginalPositions[flashlight];
                SpawnGunServerRpc(spawnPosition, gunPrefab.transform.rotation);
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

    void DropSword()
    {
        if (swordObject == null) return;

        XRGrabInteractable grabInteractable = swordObject.GetComponent<XRGrabInteractable>();

        if (grabInteractable != null && grabInteractable.isSelected)
        {
            var interactors = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grabInteractable.interactorsSelecting);

            foreach (var interactor in interactors)
            {
                grabInteractable.interactionManager.SelectExit(interactor, grabInteractable);
            }
        }
    }

    void MoveSwordToPosition(bool shouldShow)
    {
        if (swordObject == null) return;

        Rigidbody rb = swordObject.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (shouldShow)
        {
            int randomIndex = Random.Range(0, swordSpawnPositions.Length);
            swordObject.transform.position = swordSpawnPositions[randomIndex];
        }
        else
        {
            swordObject.transform.position = flashlightHidePosition;
        }
    }

    IEnumerator MoveSwordCoroutine()
    {
        if (swordObject == null) yield break;

        bool shouldShow = currentMinigame.Value == 2;

        yield return new WaitForSeconds(1f);

        DropSword();

        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            MoveSwordToPosition(shouldShow);
            elapsed += Time.deltaTime;
            yield return null;
        }

        MoveSwordToPosition(shouldShow);
    }


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

        if (allPlayers.Count == 0)
        {
            return;
        }

        int playersAboveThreshold = 0;

        foreach (XRINetworkPlayer player in allPlayers)
        {
            if (player.transform.position.y > heightThreshold)
            {
                playersAboveThreshold++;
            }
        }

        int playersRemaining = allPlayers.Count - playersAboveThreshold;

        // Check if players were eliminated in Sword or HotPotato minigames
        if ((currentMinigame.Value == 2 || currentMinigame.Value == 4) && 
            playersAboveThreshold > lastEliminatedCount && 
            playersAboveThreshold > 0)
        {
            lastEliminatedCount = playersAboveThreshold;
        
            // If there are still multiple players remaining, transition to a new minigame
            if (playersRemaining > 1 && !isProcessingWin && !isRoundTransitionRunning)
            {
                int newMinigame = RollMinigame();
                StartCoroutine(TransitionToMinigame(newMinigame));
                return;
            }
        }

        // Update eliminated count for other minigames
        if (playersAboveThreshold > lastEliminatedCount)
        {
            lastEliminatedCount = playersAboveThreshold;
        }

        if (allPlayers.Count > 1 &&
            playersRemaining <= 1 &&
            playersAboveThreshold > 0 &&
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
        ShowRoundResultRpc();

        // Give the winner and eliminated players time to read their result.
        yield return new WaitForSeconds(5f);

        CleanupGuns();
        CleanupBombs();

        ResetAllPlayersRpc();
        yield return new WaitForSeconds(1f);

        lastEliminatedCount = 0;
        shouldHideObject.Value = false;
        isProcessingWin = false;

        // Automatically begin the next round. Do not return to the welcome screen.
        StartCoroutine(StartMinigameDelayed());
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

        CancelUIRpc();
        HideWelcomeRpc();
        StartCoroutine(StartMinigameDelayed());
    }

    IEnumerator StartMinigameDelayed()
    {
        lastEliminatedCount = 0;
        isRoundTransitionRunning = true;
        isLoadingMinigame.Value = true;
        minigameRunning.Value = false;

        CancelUIRpc();

        int selectedMinigame = RollMinigame();
        currentMinigame.Value = selectedMinigame;

        TeleportAllPlayersToSpawns();
        yield return new WaitForSeconds(minigameStartDelay);

        string minigameName = GetMinigameName(selectedMinigame);
        string playersMessage = GetPlayersRemainingMessage();
        string instruction = GetInstructionForMinigame(selectedMinigame);

        ShowRoundSequenceRpc(
            minigameName,
            playersMessage,
            instruction
        );

        yield return new WaitForSeconds(GetRoundSequenceDuration());

        isLoadingMinigame.Value = false;
        isRoundTransitionRunning = false;
        minigameRunning.Value = true;
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
    private void ShowRoundResultRpc()
    {
        if (UIManager.Instance == null)
        {
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
        }

        if (minigameName == "SWORD")
        {
            PlaySwordGuitarSound();
        }
    }

    private void PlayVictorySound()
    {
        if (victorySound == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(victorySound, victoryVolume);
    }

    private void PlaySwordGuitarSound()
    {
        if (swordGuitarSound == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(swordGuitarSound, swordGuitarVolume);
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

        int totalPlayers = allPlayers.Count;
        int eliminatedPlayers = 0;

        foreach (XRINetworkPlayer player in allPlayers)
        {
            if (player.transform.position.y > heightThreshold)
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
                return "GRAB THE GUN. ONE SHOT ONLY!";
            case 4:
                return "PASS THE BOMB! DON'T BE HOLDING IT WHEN IT EXPLODES!";
            default:
                return "GET READY!";
        }
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

        ShowInstructionRpc("GRAB THE GUN. ONE SHOT ONLY!", 3f);

        currentMinigame.Value = 3;
        
    }
}