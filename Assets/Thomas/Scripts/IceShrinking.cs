using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using XRMultiplayer;

public class IceShrinking : NetworkBehaviour
{
    [SerializeField] private float shrinkSpeed = 0.5f;
    [SerializeField] private float minScale = 0.1f;
    [SerializeField] private bool destroyWhenMinimum = false;
    [SerializeField] private Vector3 resetPosition = new Vector3(0, .15f, 0);
    
    private bool isBeingLit = false;
    private Vector3 originalScale;
    private float lastHitTime;
    private float hitTimeout = 0.1f; // Time before considering light is no longer hitting

    void Start()
    {
        originalScale = transform.localScale;
    }

    void Update()
    {

        // Check if flashlight hasn't hit recently
        if (Time.time - lastHitTime > hitTimeout)
        {
            isBeingLit = false;
        }

        // Shrink if being lit
        if (isBeingLit && transform.localScale.x > minScale)
        {
            // Shrink the object uniformly
            transform.localScale -= Vector3.one * shrinkSpeed * Time.deltaTime;
            
            // Ensure we don't go below minimum scale
            if (transform.localScale.x <= minScale)
            {
                transform.localScale = Vector3.one * minScale;

                
                
                if (destroyWhenMinimum)
                {
                    // Reset the scale before destroying
                    transform.localScale = originalScale;
                    
                    //Destroy(gameObject);
                    
                    // Teleport player to reset position
                    TeleportationProvider teleportationProvider = FindAnyObjectByType<TeleportationProvider>();
                    
                    if (teleportationProvider != null)
                    {
                        TeleportRequest teleportRequest = new TeleportRequest
                        {
                            destinationPosition = resetPosition,
                            destinationRotation = Quaternion.identity
                        };
                        
                        teleportationProvider.QueueTeleportRequest(teleportRequest);
                    }
                }
            }
        }
        
    }

    // Called by the Flashlight script when raycast hits this object
    public void OnFlashlightHit()
    {
        isBeingLit = true;
        lastHitTime = Time.time;
    }
}