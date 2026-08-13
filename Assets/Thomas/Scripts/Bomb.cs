using UnityEngine;
using Unity.Netcode;
using XRMultiplayer;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class Bomb : NetworkBehaviour
{
    [Header("Bomb Settings")]
    [SerializeField] private float explosionTimer = 5f;
    [SerializeField] private float transferDistance = 5f;
    [SerializeField] private Transform[] spawnLocations = new Transform[8];

    [Header("Bomb Audio")]
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private AudioClip explosionSound;

    [Range(0f, 1f)]
    [SerializeField] private float tickVolume = 0.8f;

    [Range(0f, 1f)]
    [SerializeField] private float explosionVolume = 1f;

    [SerializeField] private AudioSource audioSource;

    private NetworkVariable<ulong> currentPlayerClientId =
        new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    private NetworkVariable<float> remainingTime =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    private bool hasExploded = false;
    private bool initialized = false;
    private GameObject poleObject;

    private float lastTransferTime = 0f;
    private float transferCooldown = 1f;

    private int lastTickSecond = -1;

    void Start()
    {
        poleObject = transform.Find("Bad")?.gameObject;

        if (poleObject == null)
        {
            Debug.LogWarning("Bomb: Could not find 'Bad' child object!");
        }
        else
        {
            poleObject.SetActive(false);
        }

        // Automatically make an AudioSource if one isn't assigned.
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;

        // 3D audio so the ticking comes from the bomb.
        audioSource.spatialBlend = 1f;
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

    public override void OnNetworkDespawn()
    {
        currentPlayerClientId.OnValueChanged -= OnPlayerChanged;
        remainingTime.OnValueChanged -= OnTimeChanged;

        if (BombCountdownUI.Instance != null)
        {
            BombCountdownUI.Instance.HideCountdown();
        }

        base.OnNetworkDespawn();
    }

    IEnumerator InitializeBomb()
    {
        yield return new WaitForSeconds(0.5f);

        XRINetworkPlayer[] allPlayers =
            FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

        if (allPlayers.Length > 0)
        {
            int randomIndex = Random.Range(0, allPlayers.Length);
            ulong randomPlayerId = allPlayers[randomIndex].OwnerClientId;

            currentPlayerClientId.Value = randomPlayerId;
            remainingTime.Value = explosionTimer;

            lastTickSecond = Mathf.CeilToInt(explosionTimer);

            if (BombCountdownUI.Instance != null)
            {
                BombCountdownUI.Instance.ShowCountdown(lastTickSecond);
            }

            initialized = true;

            Debug.Log(
                $"Bomb initialized! Assigned to player: {randomPlayerId}"
            );
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
        XRINetworkPlayer player =
            GetPlayerById(currentPlayerClientId.Value);

        if (player != null)
        {
            Transform attachPoint = player.head;

            if (attachPoint == null)
            {
                attachPoint = player.transform;
            }

            Vector3 targetPosition =
                attachPoint.position + Vector3.up * 0.5f;

            transform.position = targetPosition;
            transform.rotation = Quaternion.identity;
        }
    }

    void CheckForTransfer()
    {
        if (Time.time - lastTransferTime < transferCooldown)
        {
            return;
        }

        XRINetworkPlayer currentPlayer =
            GetPlayerById(currentPlayerClientId.Value);

        if (currentPlayer == null)
        {
            return;
        }

        XRINetworkPlayer[] allPlayers =
            FindObjectsByType<XRINetworkPlayer>(
                FindObjectsSortMode.None
            );

        foreach (XRINetworkPlayer player in allPlayers)
        {
            if (player.OwnerClientId ==
                currentPlayerClientId.Value)
            {
                continue;
            }

            float distance = Vector3.Distance(
                currentPlayer.transform.position,
                player.transform.position
            );

            if (distance <= transferDistance)
            {
                Debug.Log(
                    $"Transferring bomb from player " +
                    $"{currentPlayerClientId.Value} " +
                    $"to {player.OwnerClientId}"
                );

                ulong previousPlayerId =
                    currentPlayerClientId.Value;

                currentPlayerClientId.Value =
                    player.OwnerClientId;

                lastTransferTime = Time.time;

                TeleportPlayerToSpawn(previousPlayerId);

                return;
            }
        }
    }

    void TeleportPlayerToSpawn(ulong playerId)
    {
        if (spawnLocations.Length == 0)
        {
            Debug.LogWarning(
                "No spawn locations assigned for bomb teleportation!"
            );

            return;
        }

        int randomSpawnIndex =
            Random.Range(0, spawnLocations.Length);

        Transform spawnLocation =
            spawnLocations[randomSpawnIndex];

        if (spawnLocation == null)
        {
            Debug.LogWarning("Spawn location is null!");
            return;
        }

        TeleportPlayerRpc(
            playerId,
            spawnLocation.position,
            spawnLocation.rotation
        );
    }

    [Rpc(SendTo.Everyone)]
    void TeleportPlayerRpc(
        ulong playerClientId,
        Vector3 position,
        Quaternion rotation
    )
    {
        if (
            XRINetworkPlayer.LocalPlayer != null &&
            XRINetworkPlayer.LocalPlayer.OwnerClientId ==
            playerClientId
        )
        {
            TeleportationProvider teleportationProvider =
                FindAnyObjectByType<TeleportationProvider>();

            if (teleportationProvider != null)
            {
                TeleportRequest teleportRequest =
                    new TeleportRequest
                    {
                        destinationPosition = position,
                        destinationRotation = rotation
                    };

                teleportationProvider.QueueTeleportRequest(
                    teleportRequest
                );

                Debug.Log(
                    $"Player {playerClientId} teleported " +
                    "to spawn after transferring bomb"
                );
            }
            else
            {
                Debug.LogError(
                    "TeleportationProvider not found!"
                );
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

        Debug.Log(
            $"Bomb exploding on player " +
            $"{currentPlayerClientId.Value}"
        );

        StartCoroutine(ExplodeSequence());
    }

    IEnumerator ExplodeSequence()
    {
        // Explosion visual + sound on everyone.
        ActivateExplosionRpc();

        if (BombCountdownUI.Instance != null)
        {
            BombCountdownUI.Instance.HideCountdown();
        }

        yield return new WaitForSeconds(1f);

        if (IsOwner)
        {
            NetworkObject networkObject =
                GetComponent<NetworkObject>();

            if (
                networkObject != null &&
                networkObject.IsSpawned
            )
            {
                networkObject.Despawn();
            }

            Destroy(gameObject);
        }
    }

    [Rpc(SendTo.Everyone)]
    void ActivateExplosionRpc()
    {
        if (poleObject != null)
        {
            poleObject.SetActive(true);
            Debug.Log("Bomb explosion activated!");
        }

        // PlayClipAtPoint creates a temporary audio object,
        // so the explosion won't suddenly stop when the bomb is destroyed.
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(
                explosionSound,
                transform.position,
                explosionVolume
            );
        }
    }

    void OnPlayerChanged(
        ulong previousValue,
        ulong newValue
    )
    {
        Debug.Log(
            $"Bomb transferred: {previousValue} -> {newValue}"
        );

        if (XRINetworkPlayer.LocalPlayer == null ||
            UIManager.Instance == null)
        {
            return;
        }

        ulong localPlayerId =
            XRINetworkPlayer.LocalPlayer.OwnerClientId;

        // The player who just got the bomb.
        if (localPlayerId == newValue)
        {
            UIManager.Instance.ShowMessage(
                "YOU HAVE THE BOMB!",
                1.5f
            );
        }
        // The player who successfully passed it.
        else if (
            previousValue != ulong.MaxValue &&
            localPlayerId == previousValue)
        {
            UIManager.Instance.ShowMessage(
                "YOU PASSED THE BOMB!",
                1.5f
            );
        }
    }

    void OnTimeChanged(
        float previousValue,
        float newValue
    )
    {
        int newSecond = Mathf.CeilToInt(newValue);

        // Play one tick every second.
        if (
            newSecond > 0 &&
            newSecond != lastTickSecond
        )
        {
            lastTickSecond = newSecond;

            if (BombCountdownUI.Instance != null)
            {
                BombCountdownUI.Instance.ShowCountdown(newSecond);
            }

            PlayTick();

            Debug.Log(
                $"Bomb timer: {newSecond} seconds"
            );
        }

        if (newValue <= 0f && BombCountdownUI.Instance != null)
        {
            BombCountdownUI.Instance.HideCountdown();
        }
    }

    void PlayTick()
    {
        if (tickSound == null || audioSource == null)
        {
            return;
        }

        // Tick gets slightly higher pitched as time runs out.
        float timePercent =
            Mathf.Clamp01(
                remainingTime.Value / explosionTimer
            );

        audioSource.pitch =
            Mathf.Lerp(1.5f, 1f, timePercent);

        audioSource.PlayOneShot(
            tickSound,
            tickVolume
        );
    }

    XRINetworkPlayer GetPlayerById(ulong playerId)
    {
        XRINetworkPlayer[] allPlayers =
            FindObjectsByType<XRINetworkPlayer>(
                FindObjectsSortMode.None
            );

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