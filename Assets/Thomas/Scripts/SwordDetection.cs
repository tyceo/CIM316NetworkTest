using UnityEngine;

public class SwordDetection : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        MinigameSword sword = other.GetComponentInParent<MinigameSword>();
        if (sword == null &&
            other.gameObject.layer != LayerMask.NameToLayer("Sword"))
        {
            return;
        }

        NPCCompetitor npc = GetComponentInParent<NPCCompetitor>();
        if (npc != null)
        {
            if (sword != null && sword.NetworkObject != null)
            {
                npc.RequestElimination(sword.NetworkObject);
            }

            return;
        }

        playersweeperthing playerSweeperThing =
            FindFirstObjectByType<playersweeperthing>();
        if (playerSweeperThing != null)
        {
            playerSweeperThing.SendPlayerToStart();
        }
        else
        {
            Debug.LogWarning("playersweeperthing not found in scene!");
        }
    }
}