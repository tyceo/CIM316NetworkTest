using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using XRMultiplayer;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class IceShrinking : NetworkBehaviour
{
    [SerializeField] private float shrinkSpeed = 0.5f;
    [SerializeField] private float minScale = 0.11f;
    [SerializeField] private bool destroyWhenMinimum = true;
    
    private bool isBeingLit = false;
    private Vector3 originalScale;
    private float lastHitTime;
    private float hitTimeout = 0.1f; // Time before considering light is no longer hitting
    
    //[SerializeField] private Transform teleportDestination;

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
        if (isBeingLit)
        {
            // Shrink the object uniformly
            transform.localScale -= Vector3.one * shrinkSpeed * Time.deltaTime;
            
            // Check if we've reached minimum scale for destruction
            if (transform.localScale.x <= minScale)
            {
                if (destroyWhenMinimum)
                {
                    Debug.Log("Destroying Ice Shrinking");
                    if (!IsOwner) return;

                    playersweeperthing playersweeperthing = FindObjectOfType<playersweeperthing>();
                    playersweeperthing.SendPlayerToStart();
                    Debug.Log("Sending player to start");
                    transform.localScale = originalScale;

                }
            }
            
        }
        
    }

    public void ResetTheSize()
    {
        transform.localScale = originalScale;
    }

    // Called by the Flashlight script when raycast hits this object
    public void OnFlashlightHit()
    {
        isBeingLit = true;
        lastHitTime = Time.time;
    }
}