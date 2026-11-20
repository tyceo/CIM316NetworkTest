using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Unity.XR.CoreUtils;
using XRMultiplayer;

public class HumanCompetitorElimination : NetworkBehaviour
{
    private readonly NetworkVariable<bool> isEliminated = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private bool eliminationAccepted;
    private Coroutine lobbyTeleportCoroutine;

    public bool IsEliminated => isEliminated.Value;

    public void RequestElimination(NetworkObject cause = null)
    {
        if (IsEliminated)
        {
            return;
        }

        NetworkObjectReference causeReference = cause != null
            ? new NetworkObjectReference(cause)
            : default;

        if (HasStateAuthority())
        {
            TryApplyElimination(cause, NetworkManager.LocalClientId);
        }
        else
        {
            RequestEliminationRpc(causeReference, cause != null);
        }
    }

    public void ResetForNewMatch()
    {
        if (!IsOwner)
        {
            return;
        }

        eliminationAccepted = false;
        isEliminated.Value = false;
    }

    [Rpc(SendTo.Authority, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestEliminationRpc(
        NetworkObjectReference causeReference,
        bool hasCause,
        RpcParams rpcParams = default)
    {
        NetworkObject cause = null;
        if (hasCause && !causeReference.TryGet(out cause))
        {
            return;
        }

        TryApplyElimination(cause, rpcParams.Receive.SenderClientId);
    }

    private void TryApplyElimination(NetworkObject cause, ulong senderClientId)
    {
        if (!HasStateAuthority() ||
            eliminationAccepted ||
            IsEliminated ||
            !TryGetComponent(out XRINetworkPlayer player))
        {
            return;
        }

        MinigameManager manager = FindFirstObjectByType<MinigameManager>();
        if (manager == null || !manager.minigameRunning.Value)
        {
            return;
        }

        int minigame = manager.currentMinigame.Value;
        bool valid = false;

        if (cause == null)
        {
            valid = minigame == 1 && player.OwnerClientId == senderClientId;
        }
        else if (cause.TryGetComponent(out MinigameSword _))
        {
            valid = minigame == 2 &&
                    (senderClientId == player.OwnerClientId ||
                     senderClientId == cause.OwnerClientId) &&
                    cause.OwnerClientId != player.OwnerClientId &&
                    IsActiveHuman(senderClientId) &&
                    IsActiveHuman(cause.OwnerClientId) &&
                    Vector3.Distance(cause.transform.position, transform.position) <= 3f;
        }
        else if (cause.TryGetComponent(out Gun gun))
        {
            valid = minigame == 3 &&
                    cause.OwnerClientId == senderClientId &&
                    IsActiveHuman(senderClientId) &&
                    gun.IsAimedAt(player);
        }
        else if (cause.TryGetComponent(out Bomb bomb))
        {
            valid = minigame == 4 &&
                    cause.OwnerClientId == senderClientId &&
                    bomb.IsHeldBy(player);
        }

        if (!valid)
        {
            return;
        }

        eliminationAccepted = true;
        isEliminated.Value = true;
        EliminateLocalVictimRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void EliminateLocalVictimRpc()
    {
        if (!IsOwner || lobbyTeleportCoroutine != null)
        {
            return;
        }

        lobbyTeleportCoroutine = StartCoroutine(ReturnToLobby());
    }

    private IEnumerator ReturnToLobby()
    {
        playersweeperthing sweeper = FindFirstObjectByType<playersweeperthing>();
        XROrigin origin = FindFirstObjectByType<XROrigin>();
        if (sweeper == null || origin == null || sweeper.LobbyDestination == null)
        {
            Debug.LogError(
                "Cannot return eliminated player to the lobby: local rig or lobby destination is missing.",
                this
            );
            lobbyTeleportCoroutine = null;
            yield break;
        }

        Vector3 destination = sweeper.LobbyDestination.position;
        Quaternion rotation = sweeper.LobbyDestination.rotation;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (sweeper.TrySendPlayerToStart())
            {
                yield return new WaitForSeconds(0.35f);
                if (Vector3.Distance(origin.transform.position, destination) <= 2f)
                {
                    lobbyTeleportCoroutine = null;
                    yield break;
                }
            }
            else
            {
                yield return new WaitForSeconds(0.15f);
            }
        }

        Debug.LogWarning(
            "Lobby teleport was not applied by the TeleportationProvider; moving the local XR Origin to the configured lobby destination.",
            this
        );
        origin.transform.SetPositionAndRotation(destination, rotation);
        lobbyTeleportCoroutine = null;
    }

    private bool IsActiveHuman(ulong clientId)
    {
        foreach (XRINetworkPlayer candidate in
                 FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None))
        {
            if (candidate != null &&
                candidate.OwnerClientId == clientId &&
                candidate.TryGetComponent(out HumanCompetitorElimination state))
            {
                return !state.IsEliminated;
            }
        }

        return false;
    }

    private bool HasStateAuthority()
    {
        return IsServer ||
               (NetworkManager != null &&
                NetworkManager.DistributedAuthorityMode &&
                IsOwner);
    }
}
