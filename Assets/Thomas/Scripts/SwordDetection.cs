using UnityEngine;
using XRMultiplayer;

public class SwordDetection : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        MinigameSword sword = other.GetComponentInParent<MinigameSword>();
        if (sword == null ||
            other.gameObject.layer != LayerMask.NameToLayer("Sword"))
        {
            return;
        }

        NPCCompetitor npc = GetComponentInParent<NPCCompetitor>();
        if (npc != null)
        {
            if (sword.NetworkObject != null)
            {
                npc.RequestElimination(sword.NetworkObject);
            }

            return;
        }

        XRINetworkPlayer localPlayer = XRINetworkPlayer.LocalPlayer;
        HumanCompetitorElimination elimination = localPlayer != null
            ? localPlayer.GetComponent<HumanCompetitorElimination>()
            : null;
        if (elimination != null &&
            sword.NetworkObject != null &&
            sword.NetworkObject.OwnerClientId != localPlayer.OwnerClientId)
        {
            elimination.RequestElimination(sword.NetworkObject);
        }
        else
        {
            Debug.LogWarning("Could not identify the human sword victim or networked sword.", this);
        }
    }
}