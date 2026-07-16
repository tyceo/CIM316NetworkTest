using UnityEngine;
using Unity.Netcode;
using XRMultiplayer;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class Bomb : NetworkBehaviour
{
    [SerializeField] private float explosionTimer = 5f;
    [SerializeField] private float transferDistance = 5f;
    [SerializeField] private Transform[] spawnLocations = new Transform[8];

    private NetworkVariable<ulong> currentPlayerClientId = new NetworkVariable<ulong>(ulong.MaxValue, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> remainingTime = new NetworkVariable<float>(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    private bool hasExploded = false;
    private bool initialized = false;
    private GameObject poleObject;
    
    private float lastTransferTime = 0f;
    private float transferCooldown = 1f;

    void Start()
    {
        // Find the Pole child object
        poleObject = transform.Find("Bad")?.gameObject;
        
        if (poleObject == null)
        {
            Debug.LogWarning("Bomb: Could not find 'Pole' child object!");
        }
        else
        {
            poleObject.SetActive(false);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        currentPlayerClientId.OnValueChanged += OnPlayerChanged;
        remainingTime.OnValueChanged += OnTimeChanged;

        if (IsOwner)
        {
            StartCoroutine(InitializeBomb());
        }
    }

    IEnumerator InitializeBomb()
    {
        yield return new WaitForSeconds(0.5f);

        XRINetworkPlayer[] allPlayers = FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

        if (allPlayers.Length > 0)
        {
            int randomIndex = Random.Range(0, allPlayers.Length);
            ulong randomPlayerId = allPlayers[randomIndex].OwnerClientId;
            
            currentPlayerClientId.Value = randomPlayerId;
            remainingTime.Value = explosionTimer;
            initialized = true;
            
            Debug.Log($"Bomb initialized! Assigned to player: {randomPlayerId}");
        }
        else
        {
            Debug.LogError("NO PLAYERS FOUND! Bomb cannot initialize!");
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            if (!initialized || hasExploded)
            {
                return;
            }

            remainingTime.Value -= Time.deltaTime;

            if (remainingTime.Value <= 0f)
            {
                Debug.Log("BOMB EXPLODING!");
                ExplodeBomb();
                return;
            }

            if (currentPlayerClientId.Value != ulong.MaxValue)
            {
                CheckForTransfer();
            }
        }

        if (currentPlayerClientId.Value != ulong.MaxValue)
        {
            UpdateBombPosition();
        }
    }

    void UpdateBombPosition()
    {
        XRINetworkPlayer player = GetPlayerById(currentPlayerClientId.Value);
        
        if (player != null)
        {
            Transform attachPoint = player.head;
            
            if (attachPoint == null)
            {
                attachPoint = player.transform;
            }

            Vector3 targetPosition = attachPoint.position + Vector3.up * 0.5f;
            transform.position = targetPosition;
            transform.rotation = Quaternion.identity;
        }
    }

 void CheckForTransfer()
    {
        // Check if cooldown is still active
        if (Time.time - lastTransferTime < transferCooldown)
        {
            return;
        }

        XRINetworkPlayer currentPlayer = GetPlayerById(currentPlayerClientId.Value);
        if (currentPlayer == null)
        {
            return;
        }

        XRINetworkPlayer[] allPlayers = FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

        foreach (XRINetworkPlayer player in allPlayers)
        {
            if (player.OwnerClientId == currentPlayerClientId.Value)
            {
                continue;
            }

            float distance = Vector3.Distance(currentPlayer.transform.position, player.transform.position);

            if (distance <= transferDistance)
            {
                Debug.Log($"Transferring bomb from player {currentPlayerClientId.Value} to {player.OwnerClientId}");
                
                // Store the previous player's ID before transferring
                ulong previousPlayerId = currentPlayerClientId.Value;
                
                // Transfer the bomb
                currentPlayerClientId.Value = player.OwnerClientId;
                lastTransferTime = Time.time;
                
                // Teleport the previous player to a random spawn location
                TeleportPlayerToSpawn(previousPlayerId);
                
                return;
            }
        }
    }

    void TeleportPlayerToSpawn(ulong playerId)
    {
        if (spawnLocations.Length == 0)
        {
            Debug.LogWarning("No spawn locations assigned for bomb teleportation!");
            return;
        }

        // Get a random spawn location
        int randomSpawnIndex = Random.Range(0, spawnLocations.Length);
        Transform spawnLocation = spawnLocations[randomSpawnIndex];

        if (spawnLocation == null)
        {
            Debug.LogWarning("Spawn location is null!");
            return;
        }

        // Call the RPC to teleport the player
        TeleportPlayerRpc(playerId, spawnLocation.position, spawnLocation.rotation);
    }

    [Rpc(SendTo.Everyone)]
    void TeleportPlayerRpc(ulong playerClientId, Vector3 position, Quaternion rotation)
    {
        // Only teleport if this is the local player
        if (XRINetworkPlayer.LocalPlayer != null && XRINetworkPlayer.LocalPlayer.OwnerClientId == playerClientId)
        {
            TeleportationProvider teleportationProvider = FindAnyObjectByType<TeleportationProvider>();

            if (teleportationProvider != null)
            {
                TeleportRequest teleportRequest = new TeleportRequest
                {
                    destinationPosition = position,
                    destinationRotation = rotation
                };

                teleportationProvider.QueueTeleportRequest(teleportRequest);
                Debug.Log($"Player {playerClientId} teleported to spawn after transferring bomb");
            }
            else
            {
                Debug.LogError("TeleportationProvider not found!");
            }
        }
    }
    void ExplodeBomb()
    {
        if (hasExploded)
        {
            return;
        }
        
        hasExploded = true;

        Debug.Log($"Bomb exploding on player {currentPlayerClientId.Value}");

        // Activate the Pole for 1 second, then destroy the bomb
        StartCoroutine(ExplodeSequence());
    }

    IEnumerator ExplodeSequence()
    {
        // Activate the Pole on all clients
        ActivatePoleRpc();
        
        // Wait 1 second
        yield return new WaitForSeconds(1f);
        
        // Deactivate and destroy
        if (IsOwner)
        {
            NetworkObject networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn();
            }
            
            Destroy(gameObject);
        }
    }

    [Rpc(SendTo.Everyone)]
    void ActivatePoleRpc()
    {
        if (poleObject != null)
        {
            poleObject.SetActive(true);
            Debug.Log("Pole activated!");
        }
        else
        {
            Debug.LogWarning("Pole object is null, cannot activate!");
        }
    }

    void OnPlayerChanged(ulong previousValue, ulong newValue)
    {
        Debug.Log($"Bomb transferred: {previousValue} -> {newValue}");
    }

    void OnTimeChanged(float previousValue, float newValue)
    {
        if (Mathf.FloorToInt(newValue) != Mathf.FloorToInt(previousValue) && newValue > 0)
        {
            Debug.Log($"Bomb timer: {Mathf.CeilToInt(newValue)} seconds");
        }
    }

    XRINetworkPlayer GetPlayerById(ulong playerId)
    {
        XRINetworkPlayer[] allPlayers = FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

        foreach (XRINetworkPlayer player in allPlayers)
        {
            if (player.OwnerClientId == playerId)
            {
                return player;
            }
        }

        return null;
    }
}