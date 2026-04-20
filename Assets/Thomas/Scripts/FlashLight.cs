using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [SerializeField] private float range = 10f;
    [SerializeField] private Light flashlightLight;
    [SerializeField] private LayerMask targetLayers = ~0; // All layers by default
    [SerializeField] private bool isOn = true;
    [SerializeField] private float coneAngle = 30f; // Cone angle in degrees
    [SerializeField] private int rayCount = 32; // Number of rays to cast in the cone
    
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
        Vector3 rayOrigin = lightTransform.position;
        Vector3 forward = lightTransform.forward;
        
        float halfAngle = coneAngle / 2f;
        
        // Cast rays distributed throughout the cone
        for (int i = 0; i < rayCount; i++)
        {
            // Random distribution within the cone for better coverage
            float randomAngle = Random.Range(0f, 360f);
            float randomRadius = Mathf.Sqrt(Random.Range(0f, 1f)) * halfAngle; // sqrt for uniform distribution
            
            // Create a direction within the cone
            Vector3 rayDirection = Quaternion.AngleAxis(randomRadius, lightTransform.right) * forward;
            rayDirection = Quaternion.AngleAxis(randomAngle, forward) * rayDirection;
            
            CastRay(rayOrigin, rayDirection);
        }
    }
    
    private void CastRay(Vector3 origin, Vector3 direction)
    {
        RaycastHit hit;
        
        // Visualize the ray in the Scene view
        Debug.DrawRay(origin, direction * range, Color.yellow);

        if (Physics.Raycast(origin, direction, out hit, range, targetLayers))
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