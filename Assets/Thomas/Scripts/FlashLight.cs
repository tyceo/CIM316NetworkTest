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
    private Vector3[] rayDirections; // Store fixed ray directions

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
        
        // Generate fixed ray directions
        GenerateRayDirections();
    }

    private void GenerateRayDirections()
    {
        rayDirections = new Vector3[rayCount];
        float halfAngle = coneAngle / 2f;
        
        // Create evenly distributed rays in a cone pattern
        for (int i = 0; i < rayCount; i++)
        {
            float ringProgress = (float)i / rayCount;
            
            // Use spiral/fibonacci pattern for even distribution
            float angle = i * 137.5f; // Golden angle in degrees
            float radius = Mathf.Sqrt(ringProgress) * halfAngle;
            
            // Create a direction within the cone (relative to forward)
            Vector3 direction = Quaternion.AngleAxis(radius, Vector3.right) * Vector3.forward;
            direction = Quaternion.AngleAxis(angle, Vector3.forward) * direction;
            
            rayDirections[i] = direction;
        }
    }

    void Update()
    {
        CastFlashlightRay();
    }

    private void CastFlashlightRay()
    {
        Vector3 rayOrigin = lightTransform.position;
        
        // Cast rays using the fixed directions, rotated by the light's current orientation
        for (int i = 0; i < rayCount; i++)
        {
            Vector3 worldDirection = lightTransform.rotation * rayDirections[i];
            CastRay(rayOrigin, worldDirection);
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
            IceShrinking iceShrinking = hit.collider.GetComponentInChildren<IceShrinking>();
            if (iceShrinking != null)
            {
                iceShrinking.OnFlashlightHit();
            }
        }
    }
}