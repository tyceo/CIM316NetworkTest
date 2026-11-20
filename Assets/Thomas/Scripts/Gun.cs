using System;
using UnityEngine;
using System.Collections;
using Unity.Netcode;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;
using XRMultiplayer;

[RequireComponent(typeof(NetworkObject))]
public class Gun : NetworkBehaviour
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
        if ((IsSpawned && !IsOwner) || Time.time < nextFireTime)
        {
            return;
        }

        TryEliminateNpcInShotDirection();

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

        if (bulletPrefab != null && firePoint != null)
        {
            GameObject bulletObject = Instantiate(
                bulletPrefab,
                firePoint.position,
                firePoint.rotation
            );
            Bullet bullet = bulletObject.GetComponent<Bullet>();
            Rigidbody bulletRigidbody = bulletObject.GetComponent<Rigidbody>();
            if (bullet == null || bulletRigidbody == null)
            {
                Debug.LogError(
                    "Gun bullet prefab requires Bullet and Rigidbody components.",
                    bulletObject
                );
                Destroy(bulletObject);
            }
            else
            {
                bullet.SetShooter(GetComponent<NetworkObject>());
                bulletRigidbody.linearVelocity = firePoint.forward * bulletSpeed;
            }
        }
        else
        {
            Debug.LogWarning("Gun bullet prefab or fire point is not assigned!", this);
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
        StartCoroutine(DestroyAfterDelay(2f));
    }
    
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    private void TryEliminateNpcInShotDirection()
    {
        MinigameManager minigameManager = FindFirstObjectByType<MinigameManager>();
        if (firePoint == null ||
            minigameManager == null ||
            !minigameManager.minigameRunning.Value ||
            minigameManager.currentMinigame.Value != 3)
        {
            return;
        }

        NetworkObject competitor = FindCompetitorInShotDirection();
        if (competitor != null && competitor.TryGetComponent(
                out NPCCompetitor npc))
        {
            npc.RequestElimination(GetComponent<NetworkObject>());
        }
        else if (competitor != null &&
                 competitor.TryGetComponent(
                     out HumanCompetitorElimination humanElimination))
        {
            humanElimination.RequestElimination(GetComponent<NetworkObject>());
        }
    }

    public bool IsAimedAt(NPCCompetitor competitor)
    {
        return competitor != null &&
               FindCompetitorInShotDirection() == competitor.NetworkObject;
    }

    public bool IsAimedAt(XRINetworkPlayer player)
    {
        return player != null &&
               FindCompetitorInShotDirection() == player.NetworkObject;
    }

    private NetworkObject FindCompetitorInShotDirection()
    {
        if (firePoint == null) return null;

        RaycastHit[] hits = Physics.RaycastAll(
            firePoint.position,
            firePoint.forward,
            30f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );
        Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform) ||
                (XRINetworkPlayer.LocalPlayer != null &&
                 hit.collider.GetComponentInParent<XRINetworkPlayer>() ==
                 XRINetworkPlayer.LocalPlayer))
            {
                continue;
            }

            NPCCompetitor competitor =
                hit.collider.GetComponentInParent<NPCCompetitor>();
            if (competitor != null)
            {
                return competitor.NetworkObject;
            }

            XRINetworkPlayer player =
                hit.collider.GetComponentInParent<XRINetworkPlayer>();
            return player != null ? player.NetworkObject : null;
        }

        return null;
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