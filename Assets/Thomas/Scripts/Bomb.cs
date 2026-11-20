using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using XRMultiplayer;

public class Bomb : NetworkBehaviour
{
    [Header("Bomb Settings")]
    [SerializeField] private float explosionTimer = 5f;
    [SerializeField, Min(0.1f)] private float transferDistance = 5f;
    [SerializeField, Min(0f)] private float transferCooldown = 1f;
    [SerializeField] private Transform[] spawnLocations = new Transform[8];

    [Header("Bomb Audio")]
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private AudioClip explosionSound;

    [Range(0f, 1f)]
    [SerializeField] private float tickVolume = 0.8f;

    [Range(0f, 1f)]
    [SerializeField] private float explosionVolume = 1f;

    [SerializeField] private AudioSource audioSource;

    private readonly NetworkVariable<NetworkObjectReference> currentHolder =
        new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    private readonly NetworkVariable<float> remainingTime =
        new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    private readonly NetworkVariable<bool> isInitialized =
        new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    private readonly NetworkVariable<bool> isExploded =
        new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );
    private bool explosionVisualActivated;
    private GameObject poleObject;
    private MinigameManager minigameManager;
    private float lastTransferTime;
    private float despawnAt = -1f;
    private int lastTickSecond = -1;

    public float TransferDistance => transferDistance;

    private void Start()
    {
        poleObject = transform.Find("Bad")?.gameObject;
        if (poleObject == null)
        {
            Debug.LogWarning("Bomb: Could not find 'Bad' child object!", this);
        }
        else
        {
            poleObject.SetActive(false);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        minigameManager = FindFirstObjectByType<MinigameManager>();
        currentHolder.OnValueChanged += OnHolderChanged;
        remainingTime.OnValueChanged += OnTimeChanged;

        StartAuthoritySimulation();
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        StartAuthoritySimulation();
    }

    public override void OnNetworkDespawn()
    {
        currentHolder.OnValueChanged -= OnHolderChanged;
        remainingTime.OnValueChanged -= OnTimeChanged;

        if (BombCountdownUI.Instance != null)
        {
            BombCountdownUI.Instance.HideCountdown();
        }

        base.OnNetworkDespawn();
    }

    private IEnumerator InitializeBomb()
    {
        yield return new WaitForSeconds(0.5f);

        float waitRemaining = 5f;
        while (IsOwner && !isExploded.Value && !isInitialized.Value && waitRemaining > 0f)
        {
            if (!IsHotPotatoActive())
            {
                yield break;
            }

            List<NetworkObject> competitors = GetActiveCompetitors();
            if (competitors.Count > 0)
            {
                NetworkObject initialHolder =
                    competitors[Random.Range(0, competitors.Count)];
                currentHolder.Value = new NetworkObjectReference(initialHolder);
                remainingTime.Value = Mathf.Max(0f, explosionTimer);
                isInitialized.Value = true;
                lastTransferTime = Time.time;
                lastTickSecond = Mathf.CeilToInt(explosionTimer);

                Debug.Log(
                    $"Bomb initialized! Assigned to {DescribeCompetitor(initialHolder)}.",
                    this
                );
                yield break;
            }

            yield return new WaitForSeconds(0.25f);
            waitRemaining -= 0.25f;
        }

        if (IsOwner && !isInitialized.Value)
        {
            Debug.LogError(
                "Bomb could not find an active human or NPC competitor to hold it.",
                this
            );
        }
    }

    private void Update()
    {
        if (IsOwner && isExploded.Value)
        {
            if (despawnAt < 0f)
            {
                despawnAt = Time.time;
            }

            if (despawnAt >= 0f && Time.time >= despawnAt)
            {
                DespawnBomb();
            }

            return;
        }

        if (IsOwner)
        {
            if (!isInitialized.Value || !IsHotPotatoActive())
            {
                return;
            }

            if (!TryGetCurrentHolder(out NetworkObject holder) ||
                !IsEligibleCompetitor(holder))
            {
                if (!TryAssignRandomHolder())
                {
                    remainingTime.Value = Mathf.Max(
                        0f,
                        remainingTime.Value - Time.deltaTime
                    );
                    if (remainingTime.Value <= 0f)
                    {
                        ExplodeBomb();
                    }

                    return;
                }

                lastTransferTime = Time.time;
                return;
            }

            remainingTime.Value = Mathf.Max(
                0f,
                remainingTime.Value - Time.deltaTime
            );

            if (remainingTime.Value <= 0f)
            {
                ExplodeBomb();
                return;
            }

            CheckForTransfer(holder);
            UpdateBombPosition(holder);
        }
    }

    public bool IsHeldBy(NPCCompetitor competitor)
    {
        return competitor != null &&
               competitor.NetworkObject != null &&
               IsHolding(competitor.NetworkObject);
    }

    public bool IsHeldBy(XRINetworkPlayer player)
    {
        return player != null &&
               player.NetworkObject != null &&
               IsHolding(player.NetworkObject);
    }

    public bool IsHolding(NetworkObject competitor)
    {
        return competitor != null &&
               TryGetCurrentHolder(out NetworkObject holder) &&
               holder == competitor;
    }

    public bool IsOwnedByClient(ulong clientId)
    {
        return NetworkObject != null &&
               NetworkObject.OwnerClientId == clientId;
    }

    public bool HasBombAuthority => IsOwner;

    private bool IsHotPotatoActive()
    {
        if (minigameManager == null)
        {
            minigameManager = FindFirstObjectByType<MinigameManager>();
        }

        return minigameManager != null &&
               minigameManager.currentMinigame.Value == 4 &&
               minigameManager.minigameRunning.Value;
    }

    private bool TryGetCurrentHolder(out NetworkObject holder)
    {
        return currentHolder.Value.TryGet(out holder) &&
               holder != null &&
               holder.IsSpawned;
    }

    private bool TryAssignRandomHolder()
    {
        List<NetworkObject> competitors = GetActiveCompetitors();
        if (competitors.Count == 0)
        {
            currentHolder.Value = default;
            return false;
        }

        NetworkObject newHolder = competitors[Random.Range(0, competitors.Count)];
        currentHolder.Value = new NetworkObjectReference(newHolder);
        Debug.Log($"Bomb reassigned to {DescribeCompetitor(newHolder)}.", this);
        return true;
    }

    private List<NetworkObject> GetActiveCompetitors()
    {
        List<NetworkObject> competitors = new();

        foreach (XRINetworkPlayer player in
                 FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player != null &&
                player.NetworkObject != null &&
                IsEligibleCompetitor(player.NetworkObject))
            {
                competitors.Add(player.NetworkObject);
            }
        }

        foreach (NPCCompetitor competitor in
                 FindObjectsByType<NPCCompetitor>(FindObjectsSortMode.None))
        {
            if (competitor != null &&
                competitor.NetworkObject != null &&
                IsEligibleCompetitor(competitor.NetworkObject))
            {
                competitors.Add(competitor.NetworkObject);
            }
        }

        return competitors;
    }

    private bool IsEligibleCompetitor(NetworkObject competitor)
    {
        if (competitor == null ||
            !competitor.IsSpawned ||
            !competitor.gameObject.activeInHierarchy)
        {
            return false;
        }

        XRINetworkPlayer player = competitor.GetComponent<XRINetworkPlayer>();
        if (player != null)
        {
            HumanCompetitorElimination elimination =
                player.GetComponent<HumanCompetitorElimination>();
            return (elimination == null || !elimination.IsEliminated) &&
                   (minigameManager == null ||
                    player.transform.position.y <=
                    minigameManager.EliminationHeightThreshold);
        }

        NPCCompetitor npc = competitor.GetComponent<NPCCompetitor>();
        return npc != null && !npc.IsEliminated;
    }

    private void CheckForTransfer(NetworkObject holder)
    {
        if (Time.time - lastTransferTime < transferCooldown)
        {
            return;
        }

        Vector3 holderPosition = GetCompetitorPosition(holder);
        NetworkObject nearestCompetitor = null;
        float nearestDistanceSquared = transferDistance * transferDistance;

        foreach (NetworkObject competitor in GetActiveCompetitors())
        {
            if (competitor == holder)
            {
                continue;
            }

            float distanceSquared =
                GetDistanceToCompetitorSquared(holderPosition, competitor);
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestCompetitor = competitor;
            }
        }

        if (nearestCompetitor == null)
        {
            return;
        }

        currentHolder.Value = new NetworkObjectReference(nearestCompetitor);
        lastTransferTime = Time.time;

        Debug.Log(
            $"Bomb transferred: {DescribeCompetitor(holder)} -> " +
            $"{DescribeCompetitor(nearestCompetitor)}",
            this
        );

        if (holder.TryGetComponent(out XRINetworkPlayer previousPlayer) &&
            nearestCompetitor.TryGetComponent<XRINetworkPlayer>(out _))
        {
            TeleportPlayerToSpawn(previousPlayer.OwnerClientId);
        }
    }

    private static Vector3 GetCompetitorPosition(NetworkObject competitor)
    {
        XRINetworkPlayer player = competitor.GetComponent<XRINetworkPlayer>();
        if (player != null && player.head != null)
        {
            return player.head.position;
        }

        return competitor.transform.position;
    }

    private static float GetDistanceToCompetitorSquared(
        Vector3 sourcePosition,
        NetworkObject competitor)
    {
        float nearestDistanceSquared = float.PositiveInfinity;
        bool foundCollider = false;

        foreach (Collider collider in competitor.GetComponentsInChildren<Collider>())
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 closestPoint = collider.ClosestPoint(sourcePosition);
            float distanceSquared = (closestPoint - sourcePosition).sqrMagnitude;
            nearestDistanceSquared = Mathf.Min(nearestDistanceSquared, distanceSquared);
            foundCollider = true;
        }

        return foundCollider
            ? nearestDistanceSquared
            : (competitor.transform.position - sourcePosition).sqrMagnitude;
    }

    private void UpdateBombPosition(NetworkObject holder)
    {
        Transform holderTransform = holder.transform;
        XRINetworkPlayer player = holder.GetComponent<XRINetworkPlayer>();
        NPCCompetitor npc = holder.GetComponent<NPCCompetitor>();

        if (player != null && player.head != null)
        {
            holderTransform = player.head;
            transform.position = holderTransform.position + Vector3.up * 0.5f;
        }
        else if (npc != null)
        {
            transform.position = holderTransform.position + Vector3.up * 1.05f;
        }
        else
        {
            transform.position = holderTransform.position + Vector3.up * 0.5f;
        }

        transform.rotation = Quaternion.identity;
    }

    private void ExplodeBomb()
    {
        if (isExploded.Value || !IsOwner)
        {
            return;
        }

        isExploded.Value = true;

        if (TryGetCurrentHolder(out NetworkObject holder) &&
            IsEligibleCompetitor(holder))
        {
            XRINetworkPlayer player = holder.GetComponent<XRINetworkPlayer>();
            NPCCompetitor npc = holder.GetComponent<NPCCompetitor>();
            if (player != null)
            {
                HumanCompetitorElimination elimination =
                    player.GetComponent<HumanCompetitorElimination>();
                if (elimination == null)
                {
                    Debug.LogError(
                        "Cannot eliminate the bomb holder: HumanCompetitorElimination is missing.",
                        player
                    );
                }
                else
                {
                    elimination.RequestElimination(GetComponent<NetworkObject>());
                }
            }
            else if (npc != null)
            {
                npc.RequestElimination(GetComponent<NetworkObject>());
            }
            else
            {
                Debug.LogError(
                    $"Cannot eliminate {DescribeCompetitor(holder)}: unsupported competitor type.",
                    this
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "Bomb expired without an active competitor holding it.",
                this
            );
        }

        Debug.Log("BOMB EXPLODING!", this);
        ActivateExplosionRpc();

        if (BombCountdownUI.Instance != null)
        {
            BombCountdownUI.Instance.HideCountdown();
        }

        despawnAt = Time.time + 1f;
    }

    private void StartAuthoritySimulation()
    {
        if (!IsOwner)
        {
            return;
        }

        if (isExploded.Value)
        {
            despawnAt = Time.time;
            return;
        }

        if (isInitialized.Value)
        {
            return;
        }

        StartCoroutine(InitializeBomb());
    }

    private void DespawnBomb()
    {
        despawnAt = -1f;
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn();
        }

        Destroy(gameObject);
    }

    [Rpc(SendTo.Everyone)]
    private void ActivateExplosionRpc()
    {
        if (explosionVisualActivated)
        {
            return;
        }

        explosionVisualActivated = true;
        if (poleObject != null)
        {
            poleObject.SetActive(true);
            Debug.Log("Bomb explosion activated!", this);
        }

        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(
                explosionSound,
                transform.position,
                explosionVolume
            );
        }
    }

    private void OnHolderChanged(
        NetworkObjectReference previousValue,
        NetworkObjectReference newValue)
    {
        XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;
        if (localPlayer == null || UIManager.Instance == null)
        {
            return;
        }

        if (previousValue.TryGet(out NetworkObject previousObject) &&
            previousObject != null &&
            previousObject.TryGetComponent(out XRINetworkPlayer previousPlayer) &&
            previousPlayer.NetworkObject == localPlayer.NetworkObject)
        {
            UIManager.Instance.ShowMessage("YOU PASSED THE BOMB!", 1.5f);
        }

        if (newValue.TryGet(out NetworkObject newObject) &&
            newObject != null &&
            newObject.TryGetComponent(out XRINetworkPlayer newPlayer) &&
            newPlayer.NetworkObject == localPlayer.NetworkObject)
        {
            UIManager.Instance.ShowMessage("YOU HAVE THE BOMB!", 1.5f);
        }
    }

    private void OnTimeChanged(float previousValue, float newValue)
    {
        int newSecond = Mathf.CeilToInt(newValue);
        if (newSecond > 0 && newSecond != lastTickSecond)
        {
            lastTickSecond = newSecond;
            if (BombCountdownUI.Instance != null)
            {
                BombCountdownUI.Instance.ShowCountdown(newSecond);
            }

            PlayTick();
            Debug.Log($"Bomb timer: {newSecond} seconds", this);
        }

        if (newValue <= 0f && BombCountdownUI.Instance != null)
        {
            BombCountdownUI.Instance.HideCountdown();
        }
    }

    private void PlayTick()
    {
        if (tickSound == null || audioSource == null)
        {
            return;
        }

        float timePercent = explosionTimer > 0f
            ? Mathf.Clamp01(remainingTime.Value / explosionTimer)
            : 0f;
        audioSource.pitch = Mathf.Lerp(1.5f, 1f, timePercent);
        audioSource.PlayOneShot(tickSound, tickVolume);
    }

    private void TeleportPlayerToSpawn(ulong playerId)
    {
        if (spawnLocations == null || spawnLocations.Length == 0)
        {
            Debug.LogWarning(
                "No spawn locations assigned for bomb teleportation!",
                this
            );
            return;
        }

        List<Transform> availableSpawns = new();
        foreach (Transform spawnLocation in spawnLocations)
        {
            if (spawnLocation != null)
            {
                availableSpawns.Add(spawnLocation);
            }
        }

        if (availableSpawns.Count == 0)
        {
            Debug.LogWarning("All bomb spawn locations are null.", this);
            return;
        }

        Transform selectedSpawn =
            availableSpawns[Random.Range(0, availableSpawns.Count)];
        TeleportPlayerRpc(
            playerId,
            selectedSpawn.position,
            selectedSpawn.rotation
        );
    }

    [Rpc(SendTo.Everyone)]
    private void TeleportPlayerRpc(
        ulong playerClientId,
        Vector3 position,
        Quaternion rotation)
    {
        if (XRINetworkPlayer.LocalPlayer == null ||
            XRINetworkPlayer.LocalPlayer.OwnerClientId != playerClientId)
        {
            return;
        }

        TeleportationProvider teleportationProvider =
            FindAnyObjectByType<TeleportationProvider>();
        if (teleportationProvider == null)
        {
            Debug.LogError("TeleportationProvider not found!", this);
            return;
        }

        TeleportRequest teleportRequest = new()
        {
            destinationPosition = position,
            destinationRotation = rotation
        };
        teleportationProvider.QueueTeleportRequest(teleportRequest);
        Debug.Log(
            $"Player {playerClientId} teleported to spawn after transferring the bomb.",
            this
        );
    }

    private static string DescribeCompetitor(NetworkObject competitor)
    {
        if (competitor == null)
        {
            return "a missing competitor";
        }

        NPCCompetitor npc = competitor.GetComponent<NPCCompetitor>();
        return npc != null
            ? $"NPC competitor {npc.CompetitorIndex}"
            : $"player {competitor.OwnerClientId}";
    }
}
