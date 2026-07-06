using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GunTutorial : MonoBehaviour
{
    [Header("Gun Settings")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private float range = 20f;
    [SerializeField] private float fireRate = 0.5f;
    [SerializeField] private LayerMask targetLayers = ~0;

    [Header("Visuals")]
    [SerializeField] private GameObject laserPoint;
    [SerializeField] private float laserDuration = 1.5f;

    [Header("Optional Feedback")]
    [SerializeField] private AudioSource shootSound;

    private float nextFireTime = 0f;

    public void Shoot()
    {
        if (Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + fireRate;

        DropGunFromHand();
        FreezeGun();

        if (shootSound != null)
            shootSound.Play();

        StartCoroutine(LaserRoutine());

        if (firePoint == null)
        {
            Debug.LogWarning("Fire Point not assigned!");
            return;
        }

        RaycastHit hit;

        if (Physics.Raycast(firePoint.position, firePoint.forward, out hit, range, targetLayers))
        {
            Debug.DrawRay(firePoint.position, firePoint.forward * range, Color.red, 2f);

            GunDetectionTutorial target = hit.collider.GetComponentInParent<GunDetectionTutorial>();

            if (target != null)
            {
                target.EliminateDummy();
            }
        }
    }

    private void DropGunFromHand()
    {
        XRGrabInteractable grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null && grabInteractable.isSelected)
        {
            var interactors = new List<IXRSelectInteractor>(grabInteractable.interactorsSelecting);

            foreach (var interactor in interactors)
            {
                grabInteractable.interactionManager.SelectExit(interactor, grabInteractable);
            }
        }
    }

    private void FreezeGun()
    {
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    private IEnumerator LaserRoutine()
    {
        if (laserPoint != null)
            laserPoint.SetActive(true);

        yield return new WaitForSeconds(laserDuration);

        if (laserPoint != null)
            laserPoint.SetActive(false);
    }
}