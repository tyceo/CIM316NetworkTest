using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class playersweeperthing : MonoBehaviour
{
    [SerializeField] private Transform teleportDestination;

    public Transform LobbyDestination => teleportDestination;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the collided object is called "Pole"
        //if (other.gameObject.name != "Pole") return;
        if (other.gameObject.name.Contains("Pole"))
        {
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
    
    public void SendPlayerToStart()
    {
        TrySendPlayerToStart();
    }

    public bool TrySendPlayerToStart()
    {
        if (teleportDestination == null)
        {
            Debug.LogError("Lobby teleport destination is not assigned!", this);
            return false;
        }

        TeleportationProvider teleportationProvider = FindAnyObjectByType<TeleportationProvider>();
        if (teleportationProvider == null)
        {
            Debug.LogError("TeleportationProvider not found!", this);
            return false;
        }

        TeleportRequest teleportRequest = new TeleportRequest
        {
            destinationPosition = teleportDestination.position,
            destinationRotation = teleportDestination.rotation
        };
        bool queued = teleportationProvider.QueueTeleportRequest(teleportRequest);
        if (!queued)
        {
            Debug.LogWarning("Lobby teleport request could not be queued.", this);
        }

        return queued;
    }
}