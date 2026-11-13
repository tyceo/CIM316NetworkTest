using Unity.Netcode;
using UnityEngine;

public class NPCCompetitor : NetworkBehaviour
{
    private readonly NetworkVariable<int> competitorIndex = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<bool> isEliminated = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public int CompetitorIndex => competitorIndex.Value;
    public bool IsEliminated => isEliminated.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isEliminated.OnValueChanged += OnEliminatedChanged;
        if (isEliminated.Value)
        {
            StopNpcForElimination();
        }
    }

    public override void OnNetworkDespawn()
    {
        isEliminated.OnValueChanged -= OnEliminatedChanged;
        base.OnNetworkDespawn();
    }

    public void InitializeCompetitor(int index)
    {
        if (!HasStateWriteAuthority())
        {
            Debug.LogError(
                "Only the server or Distributed Authority session owner can initialize an NPC competitor.",
                this
            );
            return;
        }

        competitorIndex.Value = index;
        isEliminated.Value = false;
    }

    public void SetEliminated()
    {
        if (!HasStateWriteAuthority())
        {
            Debug.LogError(
                "Only the server or Distributed Authority session owner can eliminate an NPC competitor.",
                this
            );
            return;
        }

        isEliminated.Value = true;
    }

    public void RequestElimination(NetworkObject cause)
    {
        if (cause == null || !cause.IsSpawned || IsEliminated)
        {
            return;
        }

        NetworkObjectReference causeReference = new(cause);
        if (HasStateWriteAuthority())
        {
            TryApplyElimination(cause, NetworkManager.LocalClientId);
        }
        else
        {
            RequestEliminationRpc(causeReference);
        }
    }

    [Rpc(SendTo.Authority, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestEliminationRpc(
        NetworkObjectReference causeReference,
        RpcParams rpcParams = default)
    {
        if (!causeReference.TryGet(out NetworkObject cause) ||
            !TryApplyElimination(cause, rpcParams.Receive.SenderClientId))
        {
            return;
        }
    }

    private bool TryApplyElimination(NetworkObject cause, ulong senderClientId)
    {
        if (cause == null ||
            !cause.IsSpawned ||
            IsEliminated ||
        !HasStateWriteAuthority())
        {
            return false;
        }

        MinigameManager minigameManager = FindFirstObjectByType<MinigameManager>();
        if (minigameManager == null || !minigameManager.minigameRunning.Value)
        {
            return false;
        }

        int minigame = minigameManager.currentMinigame.Value;
        if (cause.TryGetComponent(out Bomb bomb))
        {
            if (minigame != 4 ||
                cause.OwnerClientId != senderClientId ||
                !bomb.IsHeldBy(this))
            {
                return false;
            }
        }
        else if (cause.TryGetComponent(out MinigameSword _))
        {
            if (minigame != 2 ||
                cause.OwnerClientId != senderClientId ||
                (cause.transform.position - transform.position).sqrMagnitude > 4f)
            {
                return false;
            }
        }
        else if (cause.TryGetComponent(out Gun gun))
        {
            if (minigame != 3 ||
                cause.OwnerClientId != senderClientId ||
                !gun.IsAimedAt(this))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        isEliminated.Value = true;
        return true;
    }

    private void OnEliminatedChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            StopNpcForElimination();
        }
    }

    private void StopNpcForElimination()
    {
        NPCCompetitorAI ai = GetComponent<NPCCompetitorAI>();
        if (ai != null)
        {
            ai.StopForElimination();
        }
    }

    private bool HasStateWriteAuthority()
    {
        return NetworkManager != null
            ? NetworkManager.DistributedAuthorityMode
                ? IsOwner
                : IsServer
            : IsServer;
    }
}
