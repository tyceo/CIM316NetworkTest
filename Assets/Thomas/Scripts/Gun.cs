using UnityEngine;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;

public class Gun : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float fireRate = 50f;
    
    private float nextFireTime = 0f;

    void Start()
    {
        
    }

    void Update()
    {
        if (Time.time >= nextFireTime)
        {

        }
    }
    
    public void Shoot()
    {
        if (Time.time < nextFireTime)
        {
            return;
        }

        // Force drop from player's hand
        XRGrabInteractable grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable != null && grabInteractable.isSelected)
        {
            var interactors = new List<IXRSelectInteractor>(grabInteractable.interactorsSelecting);
            foreach (var interactor in interactors)
            {
                grabInteractable.interactionManager.SelectExit(interactor, grabInteractable);
            }
        }

        // Freeze position and rotation
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        
    
        // Find the LaserPoint child object recursively (including inactive children)
        Transform laserPoint = FindChildRecursive(transform, "LaserPoint");
    
        if (laserPoint != null)
        {
            laserPoint.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("LaserPoint child object not found!");
        }

        nextFireTime = Time.time + fireRate;
        
        // Start coroutine to destroy this gameobject after 0.5 seconds
        StartCoroutine(DestroyAfterDelay(6f));
    }
    
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
    
    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }
        
            Transform result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }
    
        return null;
    }
    
}