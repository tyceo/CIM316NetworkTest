using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [SerializeField] private float range = 10f;
    [SerializeField] private Light flashlightLight;
    [SerializeField] private LayerMask targetLayers = ~0; // All layers by default
    [SerializeField] private bool isOn = true;
    
    private Transform lightTransform;

    void Start()
    {
        // Get the light component if not assigned
        if (flashlightLight == null)
        {
            flashlightLight = GetComponentInChildren<Light>();
        }
        
        lightTransform = flashlightLight != null ? flashlightLight.transform : transform;
        
        // Ensure light is on
        if (flashlightLight != null)
        {
            flashlightLight.enabled = true;
        }
    }

    void Update()
    {
        CastFlashlightRay();
    }

    private void CastFlashlightRay()
    {
        RaycastHit hit;
        Vector3 rayOrigin = lightTransform.position;
        Vector3 rayDirection = lightTransform.forward;

        // Visualize the ray in the Scene view
        Debug.DrawRay(rayOrigin, rayDirection * range, Color.yellow);

        if (Physics.Raycast(rayOrigin, rayDirection, out hit, range, targetLayers))
        {
            // Check if the hit object has IceShrinking component
            IceShrinking iceShrinking = hit.collider.GetComponent<IceShrinking>();
            if (iceShrinking != null)
            {
                iceShrinking.OnFlashlightHit();
            }
        }
    }

    
}