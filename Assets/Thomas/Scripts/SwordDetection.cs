using UnityEngine;

public class SwordDetection : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the collided object is in the "Sword" layer
        if (other.gameObject.layer == LayerMask.NameToLayer("Sword"))
        {
            playersweeperthing playerSweeperThing = FindObjectOfType<playersweeperthing>();
            
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
}