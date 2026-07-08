using UnityEngine;

public class FlashlightTutorial : MonoBehaviour
{
    [Header("Flashlight Settings")]
    [SerializeField] private float range = 10f;
    [SerializeField] private Light flashlightLight;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private bool isOn = true;

    [Header("Cone Raycast Settings")]
    [SerializeField] private float coneAngle = 30f;
    [SerializeField] private int rayCount = 32;

    private Transform lightTransform;
    private Vector3[] rayDirections;

    void Start()
    {
        if (flashlightLight == null)
        {
            flashlightLight = GetComponentInChildren<Light>();
        }

        lightTransform = flashlightLight != null ? flashlightLight.transform : transform;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = isOn;
        }

        GenerateRayDirections();
    }

    void Update()
    {
        if (!isOn)
        {
            return;
        }

        CastFlashlightRays();
    }

    private void GenerateRayDirections()
    {
        rayDirections = new Vector3[rayCount];

        float halfAngle = coneAngle / 2f;

        for (int i = 0; i < rayCount; i++)
        {
            float ringProgress = (float)i / rayCount;
            float angle = i * 137.5f;
            float radius = Mathf.Sqrt(ringProgress) * halfAngle;

            Vector3 direction = Quaternion.AngleAxis(radius, Vector3.right) * Vector3.forward;
            direction = Quaternion.AngleAxis(angle, Vector3.forward) * direction;

            rayDirections[i] = direction;
        }
    }

    private void CastFlashlightRays()
    {
        if (lightTransform == null)
        {
            return;
        }

        Vector3 rayOrigin = lightTransform.position;

        for (int i = 0; i < rayCount; i++)
        {
            Vector3 worldDirection = lightTransform.rotation * rayDirections[i];
            CastRay(rayOrigin, worldDirection);
        }
    }

    private void CastRay(Vector3 origin, Vector3 direction)
    {
        Debug.DrawRay(origin, direction * range, Color.yellow);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, targetLayers))
        {
            IceShrinkingTutorial tutorialTarget = hit.collider.GetComponentInChildren<IceShrinkingTutorial>();

            if (tutorialTarget != null)
            {
             TutorialItem item = GetComponentInParent<TutorialItem>();
            tutorialTarget.OnFlashlightHit(item);
            }
        }
    }

    public void TurnOn()
    {
        isOn = true;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = true;
        }
    }

    public void TurnOff()
    {
        isOn = false;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = false;
        }
    }

    public void ToggleFlashlight()
    {
        if (isOn)
        {
            TurnOff();
        }
        else
        {
            TurnOn();
        }
    }
}