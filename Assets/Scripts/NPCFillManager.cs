using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;

public class NPCFillManager : MonoBehaviour
{
    [SerializeField, Min(0)] private int targetCompetitorCount = 4;
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private Transform[] spawnPoints = new Transform[0];

    private readonly List<NPCCompetitor> spawnedCompetitors = new();
    private bool matchActive;

    public void StartMatch()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!HasSpawnAuthority(networkManager))
        {
            return;
        }

        if (matchActive)
        {
            return;
        }

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

        matchActive = true;
        CollectSpawnedCompetitors();

        int humanCount = CountConnectedHumanPlayers();
        int requiredCompetitorCount = Mathf.Max(0, targetCompetitorCount);
        int desiredNpcCount = Mathf.Max(0, requiredCompetitorCount - humanCount);
        int existingNpcCount = spawnedCompetitors.Count;

        if (existingNpcCount >= desiredNpcCount)
        {
            return;
        }

        int availableSpawnPoints = spawnPoints == null ? 0 : spawnPoints.Length;
        int maximumSpawnableCount = Mathf.Max(0, availableSpawnPoints - humanCount);
        int npcCountToSpawn = Mathf.Min(
            desiredNpcCount - existingNpcCount,
            maximumSpawnableCount
        );

        if (npcCountToSpawn < desiredNpcCount - existingNpcCount)
        {
            Debug.LogWarning(
                $"Only {availableSpawnPoints} NPC spawn points are configured; " +
                $"spawning {npcCountToSpawn} of {desiredNpcCount - existingNpcCount} required NPCs.",
                this
            );
        }

        for (int i = 0; i < npcCountToSpawn; i++)
        {
            int spawnIndex = humanCount + existingNpcCount + i;
            Transform spawnPoint = spawnPoints[spawnIndex];
            if (spawnPoint == null)
            {
                Debug.LogWarning($"NPC spawn point {spawnIndex} is not assigned.", this);
                continue;
            }

            GameObject npc = Instantiate(
                npcPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            NetworkObject networkObject = npc.GetComponent<NetworkObject>();
            NPCCompetitor competitor = npc.GetComponent<NPCCompetitor>();
            networkObject.Spawn(destroyWithScene: true);
            competitor.InitializeCompetitor(humanCount + existingNpcCount + i);
            spawnedCompetitors.Add(competitor);
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
        matchActive = false;
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
