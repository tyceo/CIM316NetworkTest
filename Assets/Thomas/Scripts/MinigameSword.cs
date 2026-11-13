using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(XRGrabInteractable))]
public class MinigameSword : NetworkBehaviour
{
    private MinigameManager minigameManager;

    public XRGrabInteractable GrabInteractable { get; private set; }

    private void Awake()
    {
        GrabInteractable = GetComponent<XRGrabInteractable>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        minigameManager = FindFirstObjectByType<MinigameManager>();
        if (minigameManager != null)
        {
            minigameManager.RegisterSword(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (minigameManager != null)
        {
            minigameManager.UnregisterSword(this);
        }

        base.OnNetworkDespawn();
    }
}
