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
    public void SendPlayerToStart()
    {
        TeleportationProvider teleportationProvider = FindAnyObjectByType<TeleportationProvider>();
        
        if (teleportationProvider != null && teleportDestination != null)
        {
            TeleportRequest teleportRequest = new TeleportRequest
            {
                destinationPosition = new Vector3(259.988007f,157.294006f,-123.014999f),
                destinationRotation = teleportDestination.rotation
            };
            Debug.Log("Sending player to start done");
            teleportationProvider.QueueTeleportRequest(teleportRequest);
        }
    }
}