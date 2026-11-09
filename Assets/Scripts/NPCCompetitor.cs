using Unity.Netcode;
using UnityEngine;

public class NPCCompetitor : NetworkBehaviour
{
    private readonly NetworkVariable<int> competitorIndex = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> isEliminated = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
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
