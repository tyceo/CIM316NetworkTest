using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Multiplayer;
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
    
    private NetworkVariable<bool> shouldHideObject = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> minigameRunning = new NetworkVariable<bool>(false);

    public NetworkVariable<int> currentMinigame = new NetworkVariable<int>(4);
    //Minigame names: none=0 , flashlight=1, oneSword=2,
    
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
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentMinigame.OnValueChanged += OnMinigameChanged;
        StoreFlashlightPositions();
        StoreSwordPosition();
        UpdateIceCubes();
        UpdateFlashlights();
        UpdateSword();
        UpdateMinigameText();
        currentMinigame.Value = 4;
    }

    // Update is called once per frame
    void Update()
    {
        objectToHide.SetActive(shouldHideObject.Value);
        
        if (IsOwner && minigameRunning.Value && !isProcessingWin)
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
        UpdateMinigameText();
        ResetIceCubeSizes();
    }

    void UpdateMinigameText()
    {
        if (currentMinigameText == null) return;
        
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
            // Fallback: search by name if no objects with tag found
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
        
        int playersRemaining = allPlayers.Length - playersAboveThreshold;
        
        // Check if we should switch minigame (when half the players are eliminated)
        if (allPlayers.Length == 4 && playersRemaining == 2)
        {
            // Switch between minigame 1 and 2
            if (currentMinigame.Value == 1)
            {
                currentMinigame.Value = 2;
            }
            else if (currentMinigame.Value == 2)
            {
                currentMinigame.Value = 1;
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
            // Fallback: search by name if no objects with tag found
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
        // Stop any existing coroutine
        if (moveFlashlightsCoroutine != null)
        {
            StopCoroutine(moveFlashlightsCoroutine);
        }
        
        // Start new coroutine to move flashlights
        moveFlashlightsCoroutine = StartCoroutine(MoveFlashlightsCoroutine());
    }

    void UpdateSword()
    {
        // Stop any existing coroutine
        if (moveSwordCoroutine != null)
        {
            StopCoroutine(moveSwordCoroutine);
        }
        
        // Start new coroutine to move sword
        moveSwordCoroutine = StartCoroutine(MoveSwordCoroutine());
    }

    void DropAllFlashlights(GameObject[] flashlights)
    {
        foreach (GameObject flashlight in flashlights)
        {
            if (flashlight == null) continue;
            
            XRGrabInteractable grabInteractable = flashlight.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null && grabInteractable.isSelected)
            {
                // Force drop the flashlight
                // Create a copy of the list to avoid modification during iteration
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
            
            // Disable rigidbody physics temporarily to prevent interference
            Rigidbody rb = flashlight.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            if (shouldShow)
            {
                // Move back to original position
                if (flashlightOriginalPositions.ContainsKey(flashlight))
                {
                    flashlight.transform.position = flashlightOriginalPositions[flashlight];
                }
            }
            else
            {
                // Move to hide position
                flashlight.transform.position = flashlightHidePosition;
            }
        }
    }

    IEnumerator MoveFlashlightsCoroutine()
    {
        GameObject[] flashlights = GameObject.FindGameObjectsWithTag("flashlight");
        bool shouldShow = currentMinigame.Value == 1;
        
        // Small delay before starting
        yield return new WaitForSeconds(1f);
        
        // First, force drop all flashlights
        DropAllFlashlights(flashlights);
        
        float duration = 1f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            MoveFlashlightsToPosition(flashlights, shouldShow);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // One final move to ensure position is set
        MoveFlashlightsToPosition(flashlights, shouldShow);
    }

    void DropSword()
    {
        if (swordObject == null) return;
        
        XRGrabInteractable grabInteractable = swordObject.GetComponent<XRGrabInteractable>();
        if (grabInteractable != null && grabInteractable.isSelected)
        {
            // Force drop the sword
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
        
        // Disable rigidbody physics temporarily to prevent interference
        Rigidbody rb = swordObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        if (shouldShow)
        {
            // Move to a random spawn position
            int randomIndex = Random.Range(0, swordSpawnPositions.Length);
            swordObject.transform.position = swordSpawnPositions[randomIndex];
        }
        else
        {
            // Move to hide position
            swordObject.transform.position = flashlightHidePosition;
        }
    }

    IEnumerator MoveSwordCoroutine()
    {
        if (swordObject == null) yield break;
        
        bool shouldShow = currentMinigame.Value == 2;
        
        // Small delay before starting
        yield return new WaitForSeconds(1f);
        
        // First, force drop the sword
        DropSword();
        
        float duration = 1f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            MoveSwordToPosition(shouldShow);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // One final move to ensure position is set
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
        
        // If minigame is 2 (SWORD) and anyone is eliminated, switch to minigame 1 (MELT)
        if (currentMinigame.Value == 2 && playersAboveThreshold > 0)
        {
            currentMinigame.Value = 1;
        }
        // Check if we should switch minigame (when half the players are eliminated)
        else if (allPlayers.Length == 4 && playersRemaining == 2)
        {
            // Switch between minigame 1 and 2
            if (currentMinigame.Value == 1)
            {
                currentMinigame.Value = 2;
            }
            else if (currentMinigame.Value == 2)
            {
                currentMinigame.Value = 1;
            }
        }
        else if (allPlayers.Length == 5 && playersRemaining == 3)
        {
            // Switch between minigame 1 and 2
            if (currentMinigame.Value == 1)
            {
                currentMinigame.Value = 2;
            }
            else if (currentMinigame.Value == 2)
            {
                currentMinigame.Value = 1;
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
        
        // Hide the object to reveal winner/results
        shouldHideObject.Value = true;
        
        // Wait for 5 seconds to display winner
        yield return new WaitForSeconds(winnerDisplayDelay);
        
        // End the minigame and reset players
        minigameRunning.Value = false;
        ResetAllPlayersRpc();
        
        isProcessingWin = false;
        shouldHideObject.Value = false;
    }

    public void StartMinigame()
    {
        if (!IsOwner) return;
        
        StartCoroutine(StartMinigameDelayed());
    }
    //button
    IEnumerator StartMinigameDelayed()
    {
        currentMinigame.Value = Random.Range(1, 3);
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
}