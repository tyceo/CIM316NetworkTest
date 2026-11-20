using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;

public class NPCFillManager : NetworkBehaviour
{
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private Transform[] spawnPoints = new Transform[0];

    private readonly NetworkVariable<int> selectedNpcCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<bool> matchActive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly List<NPCCompetitor> spawnedCompetitors = new();

    public int SelectedNpcCount => selectedNpcCount.Value;
    public bool IsMatchActive => matchActive.Value;
    public bool CanChangeNpcCount =>
        IsSpawned &&
        !matchActive.Value &&
        HasCountWriteAuthority();

    public event System.Action<int> SelectedNpcCountChanged;
    public event System.Action<bool> MatchActiveChanged;
    public event System.Action<bool> NetworkSpawnStateChanged;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        selectedNpcCount.OnValueChanged += OnSelectedNpcCountChanged;
        matchActive.OnValueChanged += OnMatchActiveChanged;

        SelectedNpcCountChanged?.Invoke(selectedNpcCount.Value);
        MatchActiveChanged?.Invoke(matchActive.Value);
        NetworkSpawnStateChanged?.Invoke(true);
        LogAuthorityState("Network spawn");
    }

    public override void OnNetworkDespawn()
    {
        selectedNpcCount.OnValueChanged -= OnSelectedNpcCountChanged;
        matchActive.OnValueChanged -= OnMatchActiveChanged;
        base.OnNetworkDespawn();
        NetworkSpawnStateChanged?.Invoke(false);
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        NetworkSpawnStateChanged?.Invoke(IsSpawned);
        LogAuthorityState("Ownership gained");
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();
        NetworkSpawnStateChanged?.Invoke(IsSpawned);
        LogAuthorityState("Ownership lost");
    }

    public void RequestNpcCount(int count)
    {
        if (!IsSpawned || matchActive.Value)
        {
            Debug.LogWarning(
                $"[NPC Slider Debug] Count request {count} rejected: " +
                $"IsSpawned={IsSpawned}, MatchActive={matchActive.Value}.",
                this
            );
            return;
        }

        int clampedCount = Mathf.Clamp(count, 0, 3);
        NetworkManager networkManager = NetworkManager.Singleton;
        bool authorizedWriter = HasCountWriteAuthority();
        Debug.Log(
            $"[NPC Slider Debug] Count request: requested={clampedCount}, " +
            $"current={selectedNpcCount.Value}, server={networkManager != null && networkManager.IsServer}, " +
            $"objectOwner={IsOwner}, sessionOwner={IsLocalSessionOwner(networkManager)}, " +
            $"localClientId={(networkManager != null ? networkManager.LocalClientId : ulong.MaxValue)}, " +
            $"networkOwner={OwnerClientId}, authorizedWriter={authorizedWriter}.",
            this
        );

        if (authorizedWriter)
        {
            ApplyNpcCount(clampedCount, networkManager.LocalClientId);
        }
        else
        {
            SetNpcCountRpc(clampedCount);
        }
    }

    [Rpc(SendTo.Authority, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetNpcCountRpc(int count, RpcParams rpcParams = default)
    {
        ApplyNpcCount(count, rpcParams.Receive.SenderClientId);
    }

    private void ApplyNpcCount(int count, ulong senderClientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        bool senderIsAuthorized = networkManager != null &&
            (networkManager.DistributedAuthorityMode
                ? senderClientId == networkManager.CurrentSessionOwner &&
                  senderClientId == OwnerClientId
                : senderClientId == NetworkManager.ServerClientId);
        if (!HasCountWriteAuthority() || matchActive.Value || !senderIsAuthorized)
        {
            Debug.LogWarning(
                $"[NPC Slider Debug] Count request rejected: requested={count}, " +
                $"sender={senderClientId}, owner={OwnerClientId}, " +
                $"sessionOwner={networkManager != null && IsLocalSessionOwner(networkManager)}, " +
                $"matchActive={matchActive.Value}, authorizedWriter={HasCountWriteAuthority()}.",
                this
            );
            return;
        }

        int confirmedCount = Mathf.Clamp(count, 0, 3);
        selectedNpcCount.Value = confirmedCount;
        Debug.Log(
            $"[NPC Slider Debug] Count synchronized: requested={count}, " +
            $"confirmed={selectedNpcCount.Value}.",
            this
        );
    }

    private void OnSelectedNpcCountChanged(int previousValue, int newValue)
    {
        Debug.Log(
            $"[NPC Slider Debug] Count synchronized: previous={previousValue}, confirmed={newValue}.",
            this
        );
        SelectedNpcCountChanged?.Invoke(newValue);
    }

    private void OnMatchActiveChanged(bool previousValue, bool newValue)
    {
        MatchActiveChanged?.Invoke(newValue);
    }

    public void StartMatch()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!HasSpawnAuthority(networkManager))
        {
            Debug.LogWarning(
                $"[NPC Slider Debug] Match start rejected: server={networkManager != null && networkManager.IsServer}, " +
                $"objectOwner={IsOwner}, sessionOwner={IsLocalSessionOwner(networkManager)}, " +
                $"ownerClientId={OwnerClientId}.",
                this
            );
            return;
        }

        if (matchActive.Value)
        {
            Debug.LogWarning(
                "[NPC Slider Debug] Match start ignored because the NPC match is already active.",
                this
            );
            return;
        }

        int desiredNpcCount = Mathf.Clamp(selectedNpcCount.Value, 0, 3);
        Debug.Log(
            $"[NPC Slider Debug] Match start: selectedNpcCount={desiredNpcCount}, " +
            $"server={networkManager.IsServer}, objectOwner={IsOwner}, " +
            $"sessionOwner={IsLocalSessionOwner(networkManager)}, " +
            $"prefabAssigned={npcPrefab != null}, spawnPointCount={(spawnPoints != null ? spawnPoints.Length : 0)}.",
            this
        );

        if (npcPrefab == null ||
            !npcPrefab.TryGetComponent(out NetworkObject _) ||
            !npcPrefab.TryGetComponent(out NPCCompetitor _))
        {
            Debug.LogError(
                "NPC Fill Manager requires a prefab with NetworkObject and NPCCompetitor components.",
                this
            );
            return;
        }

        int availableSpawnPoints = spawnPoints == null ? 0 : spawnPoints.Length;
        if (desiredNpcCount > 0 && availableSpawnPoints == 0)
        {
            Debug.LogError(
                $"Cannot spawn {desiredNpcCount} NPC competitors: no spawn points are configured.",
                this
            );
            return;
        }

        int humanCount = CountConnectedHumanPlayers();
        for (int npcIndex = 0; npcIndex < desiredNpcCount; npcIndex++)
        {
            int spawnIndex = (humanCount + npcIndex) % availableSpawnPoints;
            if (spawnPoints[spawnIndex] == null)
            {
                Debug.LogError(
                    $"Cannot spawn {desiredNpcCount} NPC competitors: spawn point {spawnIndex} is not assigned.",
                    this
                );
                return;
            }
        }

        matchActive.Value = true;
        CollectSpawnedCompetitors();

        while (spawnedCompetitors.Count > desiredNpcCount)
        {
            NPCCompetitor extraCompetitor =
                spawnedCompetitors[spawnedCompetitors.Count - 1];
            spawnedCompetitors.RemoveAt(spawnedCompetitors.Count - 1);
            if (extraCompetitor != null &&
                extraCompetitor.NetworkObject != null &&
                extraCompetitor.NetworkObject.IsSpawned)
            {
                extraCompetitor.NetworkObject.Despawn(destroy: true);
            }
        }

        while (spawnedCompetitors.Count < desiredNpcCount)
        {
            int npcIndex = spawnedCompetitors.Count;
            int spawnIndex = (humanCount + npcIndex) % availableSpawnPoints;
            Transform spawnPoint = spawnPoints[spawnIndex];

            GameObject npc = Instantiate(
                npcPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            NetworkObject networkObject = npc.GetComponent<NetworkObject>();
            NPCCompetitor competitor = npc.GetComponent<NPCCompetitor>();
            networkObject.Spawn(destroyWithScene: true);
            competitor.InitializeCompetitor(humanCount + npcIndex);
            spawnedCompetitors.Add(competitor);
        }

        Debug.Log(
            $"[NPC Slider Debug] Spawn result: selected={desiredNpcCount}, " +
            $"spawned={spawnedCompetitors.Count}, humans={humanCount}.",
            this
        );

        if (spawnedCompetitors.Count != desiredNpcCount)
        {
            Debug.LogError(
                $"NPC spawning completed with {spawnedCompetitors.Count} of {desiredNpcCount} selected competitors.",
                this
            );
        }
    }

    public void EndMatch()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!HasSpawnAuthority(networkManager))
        {
            return;
        }

        CollectSpawnedCompetitors();

        foreach (NPCCompetitor competitor in spawnedCompetitors)
        {
            if (competitor == null)
            {
                continue;
            }

            NetworkObject networkObject = competitor.NetworkObject;
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn(destroy: true);
            }
        }

        spawnedCompetitors.Clear();
        matchActive.Value = false;
    }

    private static bool HasSpawnAuthority(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            return false;
        }

        return networkManager.IsServer ||
               (networkManager.DistributedAuthorityMode &&
                networkManager.IsListening &&
                networkManager.LocalClient.IsSessionOwner);
    }

    private bool HasCountWriteAuthority()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!IsSpawned || networkManager == null)
        {
            return false;
        }

        if (networkManager.DistributedAuthorityMode)
        {
            return IsOwner &&
                  IsLocalSessionOwner(networkManager) &&
                  OwnerClientId == networkManager.CurrentSessionOwner;
        }

        return networkManager.IsServer &&
               OwnerClientId == NetworkManager.ServerClientId;
    }

    private static bool IsLocalSessionOwner(NetworkManager networkManager)
    {
        return networkManager != null &&
               networkManager.DistributedAuthorityMode &&
               networkManager.IsListening &&
               networkManager.LocalClient.IsSessionOwner;
    }

    private void LogAuthorityState(string context)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        Debug.Log(
            $"[NPC Slider Debug] {context}: server={networkManager != null && networkManager.IsServer}, " +
            $"objectOwner={IsOwner}, sessionOwner={IsLocalSessionOwner(networkManager)}, " +
            $"distributedAuthority={networkManager != null && networkManager.DistributedAuthorityMode}, " +
            $"localClientId={(networkManager != null ? networkManager.LocalClientId : ulong.MaxValue)}, " +
            $"networkOwner={OwnerClientId}, selected={selectedNpcCount.Value}, " +
            $"matchActive={matchActive.Value}, canChange={CanChangeNpcCount}.",
            this
        );
    }

    private void CollectSpawnedCompetitors()
    {
        spawnedCompetitors.RemoveAll(competitor => competitor == null);

        NPCCompetitor[] sceneCompetitors =
            FindObjectsByType<NPCCompetitor>(FindObjectsSortMode.None);

        foreach (NPCCompetitor competitor in sceneCompetitors)
        {
            if (competitor != null &&
                competitor.NetworkObject != null &&
                competitor.NetworkObject.IsSpawned &&
                !spawnedCompetitors.Contains(competitor))
            {
                spawnedCompetitors.Add(competitor);
            }
        }
    }

    private static int CountConnectedHumanPlayers()
    {
        XRINetworkPlayer[] players =
            FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

        HashSet<ulong> clientIds = new();
        foreach (XRINetworkPlayer player in players)
        {
            if (player == null ||
                !player.gameObject.activeInHierarchy ||
                player.NetworkObject == null ||
                !player.NetworkObject.IsSpawned)
            {
                continue;
            }

            clientIds.Add(player.OwnerClientId);
        }

        return clientIds.Count;
    }
}
