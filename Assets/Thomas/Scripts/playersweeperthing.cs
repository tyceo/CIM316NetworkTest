using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class playersweeperthing : MonoBehaviour
{
    [SerializeField] private Transform teleportDestination;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the collided object is called "Pole"
        if (other.gameObject.name != "Pole") return;
        
        // Find the teleportation provider
        TeleportationProvider teleportationProvider = FindAnyObjectByType<TeleportationProvider>();
        
        if (teleportationProvider != null && teleportDestination != null)
        {
            TeleportRequest teleportRequest = new TeleportRequest
            {
                destinationPosition = teleportDestination.position,
                destinationRotation = teleportDestination.rotation
            };
            
            teleportationProvider.QueueTeleportRequest(teleportRequest);
        }
    }
}