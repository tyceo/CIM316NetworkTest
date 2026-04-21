using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using XRMultiplayer;

public class IceShrinking : NetworkBehaviour
{
    [SerializeField] private float shrinkSpeed = 0.5f;
    [SerializeField] private float minScale = 0.1f;
    [SerializeField] private bool destroyWhenMinimum = false;
    
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
                    Destroy(gameObject);
                    //NetworkManager.Singleton.Shutdown();
                    //XRINetworkGameManager.Disconnect();
                    //XRINetworkGameManager.Instance.Disconnect();
                    if (!IsOwner) return;

                    //UnityEditor.EditorApplication.isPlaying = false;
                    Application.Quit();


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