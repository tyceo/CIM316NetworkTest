using System.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using XRMultiplayer;

public class MinigameManager : NetworkBehaviour
{
    
    [SerializeField] private GameObject objectToHide;
    [SerializeField] private Transform[] spawnLocations = new Transform[8];
    [SerializeField] private float heightThreshold = 100f;
    [SerializeField] private Vector3 resetPosition = new Vector3(0, .15f, 0);
    [SerializeField] private float minigameStartDelay = 2f;
    [SerializeField] private float winnerDisplayDelay = 5f;
    
    private NetworkVariable<bool> shouldHideObject = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> minigameRunning = new NetworkVariable<bool>(false);
    
    private bool isProcessingWin = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        objectToHide.SetActive(shouldHideObject.Value);
        
        if (IsOwner && minigameRunning.Value && !isProcessingWin)
        {
            CheckPlayersHeight();
        }
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

    IEnumerator StartMinigameDelayed()
    {
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