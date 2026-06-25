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

    private float liftYStart = -18f;
    private float liftYEnd = 127.7f;
    private float liftStayDuration = 5f;
    private Coroutine liftCoroutine = null;

    private NetworkVariable<bool> shouldHideObject = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> minigameRunning = new NetworkVariable<bool>(false);
    public NetworkVariable<int> currentMinigame = new NetworkVariable<int>(4);
    // Minigame names: none=0, flashlight=1, oneSword=2, oneShot=3

    private NetworkVariable<bool> isLoadingMinigame = new NetworkVariable<bool>(false);
    private float loadingMinigameDuration = 3f;

    private bool isProcessingWin = false;

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
    private int lastMinigame = -1;
    private int lastEliminatedCount = 0;

    // Returns a random minigame (1-3) that is never the same as lastMinigame
    int RollMinigame()
    {
        List<int> options = new List<int> { 1, 2, 3 };
        options.Remove(lastMinigame);
        int chosen = options[Random.Range(0, options.Count)];
        lastMinigame = chosen;
        return chosen;
    }

    // Shows "Currently loading" for loadingMinigameDuration seconds, then applies the new minigame
    IEnumerator TransitionToMinigame(int newMinigame)
    {
        isLoadingMinigame.Value = true;
        yield return new WaitForSeconds(loadingMinigameDuration);
        isLoadingMinigame.Value = false;
        currentMinigame.Value = newMinigame;
    }

    void Start()
    {
        currentMinigame.OnValueChanged += OnMinigameChanged;
        isLoadingMinigame.OnValueChanged += (prev, next) => UpdateMinigameText();
        StoreFlashlightPositions();
        StoreSwordPosition();
        UpdateIceCubes();
        UpdateFlashlights();
        UpdateSword();
        UpdateGuns();
        UpdateMinigameText();
        currentMinigame.Value = 4;
    }

    void Update()
    {
        objectToHide.SetActive(shouldHideObject.Value);

        if (IsOwner && minigameRunning.Value && !isProcessingWin && !isLoadingMinigame.Value)
        {
            CheckPlayersHeight();
        }

        if (IsOwner && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            currentMinigame.Value = currentMinigame.Value == 1 ? 0 : 1;
        }

        UpdatePlayersEliminatedText();
    }

    void OnMinigameChanged(int previousValue, int newValue)
    {
        UpdateIceCubes();
        UpdateFlashlights();
        UpdateSword();
        UpdateGuns();
        UpdateMinigameText();
        ResetIceCubeSizes();
    }

    void UpdateMinigameText()
    {
        if (currentMinigameText == null) return;

        if (isLoadingMinigame.Value)
        {
            currentMinigameText.text = "Current Minigame: Currently loading";
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
            default:
                minigameName = "UNKNOWN";
                break;
        }

        currentMinigameText.text = "Current Minigame: " + minigameName;
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

    void UpdatePlayersEliminatedText()
    {
        if (playersEliminatedText == null) return;

        XRINetworkPlayer[] allPlayers = FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

        if (allPlayers.Length == 0)
        {
            playersEliminatedText.text = "Players eliminated: 0/0";
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

        int totalPlayers = allPlayers.Length;
        int eliminatedPlayers = playersAboveThreshold;

        playersEliminatedText.text = "Players eliminated: " + eliminatedPlayers + "/" + totalPlayers;
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

    void CheckPlayersHeight()
    {
        XRINetworkPlayer[] allPlayers = FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

        if (allPlayers.Length == 0) return;

        int playersAboveThreshold = 0;

        foreach (XRINetworkPlayer player in allPlayers)
        {
            if (player.transform.position.y > heightThreshold)
            {
                playersAboveThreshold++;
            }
        }

        int playersRemaining = allPlayers.Length - playersAboveThreshold;

        // Only trigger a transition if a new elimination has occurred since the last switch
        if (playersAboveThreshold > 0 && playersAboveThreshold > lastEliminatedCount)
        {
            lastEliminatedCount = playersAboveThreshold;

            if (allPlayers.Length < 6)
            {
                // Less than 6 players: switch minigame whenever anyone new dies
                StartCoroutine(TransitionToMinigame(RollMinigame()));
            }
            else if (playersRemaining <= allPlayers.Length / 2)
            {
                // 6 or more players: switch when half are eliminated
                StartCoroutine(TransitionToMinigame(RollMinigame()));
            }
        }

        // Check if all or all but one players are above the threshold
        if (playersAboveThreshold >= allPlayers.Length - 1 && playersAboveThreshold > 0)
        {
            StartCoroutine(HandleMinigameEnd());
        }
    }

    IEnumerator HandleMinigameEnd()
    {
        isProcessingWin = true;

        shouldHideObject.Value = true;

        yield return new WaitForSeconds(winnerDisplayDelay);

        CleanupGuns();

        minigameRunning.Value = false;
        ResetAllPlayersRpc();

        isProcessingWin = false;
        shouldHideObject.Value = false;
    }

    public void StartMinigame()
    {
        if (!IsOwner) return;

        // Cancel any in-progress coroutines from the previous game
        StopAllCoroutines();

        // Reset all state that may have been left dirty
        isProcessingWin = false;
        isLoadingMinigame.Value = false;
        shouldHideObject.Value = false;
        minigameRunning.Value = false;

        StartCoroutine(StartMinigameDelayed());
    }

    // button
    IEnumerator StartMinigameDelayed()
    {
        lastEliminatedCount = 0;
        currentMinigame.Value = RollMinigame();
        TeleportAllPlayersToSpawns();
        yield return new WaitForSeconds(minigameStartDelay);
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
            }

            if (teleportationProvider != null)
            {
                TeleportRequest teleportRequest = new TeleportRequest
                {
                    destinationPosition = spawnLocations[spawnIndex].position,
                    destinationRotation = spawnLocations[spawnIndex].rotation
                };

                teleportationProvider.QueueTeleportRequest(teleportRequest);
            }
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
    
    public void TriggerLift()
    {
        if (liftObject == null)
        {
            Debug.LogWarning("Lift object not assigned!");
            return;
        }

        if (liftCoroutine != null)
        {
            StopCoroutine(liftCoroutine);
        }

        liftCoroutine = StartCoroutine(LiftCoroutine());
    }

    IEnumerator LiftCoroutine()
    {
        Vector3 startPos = new Vector3(liftObject.transform.position.x, liftYStart, liftObject.transform.position.z);
        Vector3 endPos = new Vector3(liftObject.transform.position.x, liftYEnd, liftObject.transform.position.z);

        // Snap to start position
        liftObject.transform.position = startPos;

        // Move from A to B over liftDuration seconds
        float elapsed = 0f;
        while (elapsed < liftDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / liftDuration);
            liftObject.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        liftObject.transform.position = endPos;

        // Stay at B for liftStayDuration seconds
        yield return new WaitForSeconds(liftStayDuration);

        // Instantly return to A
        liftObject.transform.position = startPos;

        liftCoroutine = null;
    }
    
    public void SpawnSingleGun()
    {
        if (!IsOwner) return;

        if (gunPrefab == null)
        {
            Debug.LogWarning("Gun prefab is not assigned!");
            return;
        }

        List<Vector3> positions = new List<Vector3>(flashlightOriginalPositions.Values);

        if (positions.Count == 0)
        {
            Debug.LogWarning("No torch spawn positions found!");
            return;
        }

        Vector3 spawnPosition = positions[Random.Range(0, positions.Count)];
        SpawnGunServerRpc(spawnPosition, gunPrefab.transform.rotation);
    }
}