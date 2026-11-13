using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;

public class CompetitorMeltTarget : NetworkBehaviour
{
    [SerializeField] private Transform iceVisual;
    [SerializeField, Min(0.01f)] private float meltPerSecond = 0.35f;
    [SerializeField, Min(0.1f)] private float maximumFlashlightDistance = 12f;

    private readonly NetworkVariable<float> iceIntegrity = new(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> eliminated = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly Dictionary<ulong, float> lastHumanHitByClient = new();
    private MinigameManager minigameManager;
    private Vector3 originalIceScale;
    private bool isLit;
    private bool initialized;

    public bool IsEliminated => eliminated.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        minigameManager = FindFirstObjectByType<MinigameManager>();
        if (iceVisual == null)
        {
            GameObject iceObject = FindWithTagInChildren(transform, "icecube");
            if (iceObject != null)
            {
                iceVisual = iceObject.transform;
            }
        }

        if (iceVisual != null)
        {
            originalIceScale = iceVisual.localScale;
        }

        iceIntegrity.OnValueChanged += OnIceIntegrityChanged;
        eliminated.OnValueChanged += OnEliminatedChanged;

        if (IsServer)
        {
            iceIntegrity.Value = 1f;
            eliminated.Value = false;
        }

        if (minigameManager != null)
        {
            minigameManager.currentMinigame.OnValueChanged += OnMinigameChanged;
            minigameManager.minigameRunning.OnValueChanged += OnMinigameRunningChanged;
        }

        initialized = true;
        RefreshIceVisual();
    }

    public override void OnNetworkDespawn()
    {
        iceIntegrity.OnValueChanged -= OnIceIntegrityChanged;
        eliminated.OnValueChanged -= OnEliminatedChanged;
        if (minigameManager != null)
        {
            minigameManager.currentMinigame.OnValueChanged -= OnMinigameChanged;
            minigameManager.minigameRunning.OnValueChanged -= OnMinigameRunningChanged;
        }

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        bool meltRound = IsMeltRound();
        if (!meltRound || eliminated.Value || iceIntegrity.Value <= 0f)
        {
            isLit = false;
            return;
        }

        if (isLit)
        {
            iceIntegrity.Value = Mathf.Max(
                0f,
                iceIntegrity.Value - meltPerSecond * Time.deltaTime
            );
            isLit = false;

            if (iceIntegrity.Value <= 0f)
            {
                EliminateCompetitor();
            }
        }
    }

    public void OnFlashlightHit(Vector3 rayOrigin, Vector3 rayDirection)
    {
        if (!IsMeltRound() || eliminated.Value)
        {
            return;
        }

        if (IsServer)
        {
            isLit = true;
        }
        else
        {
            RequestHumanMeltRpc(rayOrigin, rayDirection);
        }
    }

    public void ApplyNpcMelt()
    {
        if (IsServer && IsMeltRound() && !eliminated.Value)
        {
            isLit = true;
        }
    }

    public void EliminateByHotPotato(Bomb bomb)
    {
        if (bomb == null ||
            !bomb.HasBombAuthority ||
            !bomb.IsHolding(NetworkObject))
        {
            Debug.LogError(
                "HOT POTATO elimination requires the authoritative bomb and its current holder.",
                this
            );
            return;
        }

        if (!IsHotPotatoRound() || eliminated.Value)
        {
            return;
        }

        NPCCompetitor npc = GetComponent<NPCCompetitor>();
        if (npc != null && npc.IsEliminated)
        {
            return;
        }

        if (HasEliminationWriteAuthority())
        {
            EliminateCompetitor();
            return;
        }

        EliminateByHotPotatoAuthorityRpc(
            new NetworkObjectReference(bomb.NetworkObject)
        );
    }

    [Rpc(SendTo.Authority, InvokePermission = RpcInvokePermission.Everyone)]
    private void EliminateByHotPotatoAuthorityRpc(
        NetworkObjectReference bombReference,
        RpcParams rpcParams = default)
    {
        if (!HasEliminationWriteAuthority() ||
            !IsHotPotatoCompetitorActive() ||
            !bombReference.TryGet(out NetworkObject bombObject) ||
            !bombObject.TryGetComponent(out Bomb bomb) ||
            !bomb.IsOwnedByClient(rpcParams.Receive.SenderClientId) ||
            !bomb.IsHolding(NetworkObject))
        {
            return;
        }

        EliminateCompetitor();
    }

    private void OnIceIntegrityChanged(float previousValue, float newValue)
    {
        RefreshIceVisual();
    }

    private void OnEliminatedChanged(bool previousValue, bool newValue)
    {
        RefreshIceVisual();
    }

    private void OnMinigameChanged(int previousValue, int newValue)
    {
        if (IsServer &&
            GetComponent<NPCCompetitor>() == null &&
            newValue == 1 &&
            IsWithinArenaHeight())
        {
            iceIntegrity.Value = 1f;
            eliminated.Value = false;
        }

        RefreshIceVisual();
    }

    private void OnMinigameRunningChanged(bool previousValue, bool newValue)
    {
        RefreshIceVisual();
    }

    private void RefreshIceVisual()
    {
        if (!initialized || iceVisual == null)
        {
            return;
        }

        bool shouldShow = IsMeltRound() &&
                          iceIntegrity.Value > 0f &&
                          !eliminated.Value &&
                          (GetComponent<NPCCompetitor>() == null ||
                           !GetComponent<NPCCompetitor>().IsEliminated) &&
                          IsWithinArenaHeight();
        foreach (Renderer renderer in iceVisual.GetComponents<Renderer>())
        {
            renderer.enabled = shouldShow;
        }

        iceVisual.localScale = originalIceScale * Mathf.Clamp01(iceIntegrity.Value);
    }

    private bool IsMeltRound()
    {
        return minigameManager != null &&
               minigameManager.currentMinigame.Value == 1 &&
               minigameManager.minigameRunning.Value;
    }

    private bool IsHotPotatoRound()
    {
        return minigameManager != null &&
               minigameManager.currentMinigame.Value == 4 &&
               minigameManager.minigameRunning.Value;
    }

    private bool IsHotPotatoCompetitorActive()
    {
        if (!IsHotPotatoRound() ||
            eliminated.Value ||
            NetworkObject == null ||
            !NetworkObject.IsSpawned ||
            !gameObject.activeInHierarchy ||
            !IsWithinArenaHeight())
        {
            return false;
        }

        NPCCompetitor npc = GetComponent<NPCCompetitor>();
        return npc == null || !npc.IsEliminated;
    }

    private bool HasEliminationWriteAuthority()
    {
        return NetworkManager != null &&
               (NetworkManager.DistributedAuthorityMode ? IsOwner : IsServer);
    }

    private bool IsWithinArenaHeight()
    {
        return minigameManager == null ||
               transform.position.y <= minigameManager.EliminationHeightThreshold;
    }

    private void EliminateCompetitor()
    {
        if (eliminated.Value)
        {
            return;
        }

        eliminated.Value = true;
        NPCCompetitor npc = GetComponent<NPCCompetitor>();
        if (npc != null)
        {
            npc.SetEliminated();
            return;
        }

        XRINetworkPlayer human = GetComponent<XRINetworkPlayer>();
        if (human != null)
        {
            EliminateHumanClientRpc(human.OwnerClientId);
        }
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RequestHumanMeltRpc(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        RpcParams rpcParams = default)
    {
        if (!IsMeltRound() || eliminated.Value ||
            Time.time - GetLastHitTime(rpcParams.Receive.SenderClientId) < 0.08f)
        {
            return;
        }

        XRINetworkPlayer shooter = FindPlayer(rpcParams.Receive.SenderClientId);
        if (shooter == null)
        {
            return;
        }

        Transform source = shooter.head != null ? shooter.head : shooter.transform;
        if (Vector3.Distance(source.position, rayOrigin) > 4f ||
            !HasLineOfSightToTarget(rayOrigin, rayDirection, shooter))
        {
            return;
        }

        lastHumanHitByClient[rpcParams.Receive.SenderClientId] = Time.time;
        isLit = true;
    }

    [Rpc(SendTo.Everyone)]
    private void EliminateHumanClientRpc(ulong playerClientId)
    {
        XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;
        if (localPlayer == null || localPlayer.OwnerClientId != playerClientId)
        {
            return;
        }

        playersweeperthing eliminationTeleporter =
            FindAnyObjectByType<playersweeperthing>();
        if (eliminationTeleporter == null)
        {
            Debug.LogError(
                "Cannot eliminate a melted human: playersweeperthing was not found in the scene.",
                this
            );
            return;
        }

        eliminationTeleporter.SendPlayerToStart();
    }

    private float GetLastHitTime(ulong clientId)
    {
        return lastHumanHitByClient.TryGetValue(clientId, out float value)
            ? value
            : float.NegativeInfinity;
    }

    private bool HasLineOfSightToTarget(
        Vector3 origin,
        Vector3 direction,
        XRINetworkPlayer shooter)
    {
        if (direction.sqrMagnitude < 0.9f)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            maximumFlashlightDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == shooter.transform ||
                hit.transform.IsChildOf(shooter.transform))
            {
                continue;
            }

            CompetitorMeltTarget hitTarget =
                hit.collider.GetComponentInParent<CompetitorMeltTarget>();
            if (hitTarget == this &&
                iceVisual != null &&
                (hit.transform == iceVisual || hit.transform.IsChildOf(iceVisual)))
            {
                return true;
            }

            return false;
        }

        return false;
    }

    private static XRINetworkPlayer FindPlayer(ulong clientId)
    {
        foreach (XRINetworkPlayer player in
                 FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player != null && player.OwnerClientId == clientId)
            {
                return player;
            }
        }

        return null;
    }

    private static GameObject FindWithTagInChildren(Transform parent, string tag)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.CompareTag(tag))
            {
                return child.gameObject;
            }
        }

        return null;
    }
}
