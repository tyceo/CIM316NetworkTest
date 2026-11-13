using UnityEngine;
using Unity.Netcode;
using XRMultiplayer;

public class Bullet : MonoBehaviour
{
    private NetworkObject shooter;
    private bool hasHit;

    public void SetShooter(NetworkObject networkObject)
    {
        shooter = networkObject;
    }

    void Start()
    {
        Destroy(gameObject, 5f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit)
        {
            return;
        }

        XRINetworkPlayer player =
            other.GetComponentInParent<XRINetworkPlayer>();
        if (player != null &&
            shooter != null &&
            player.OwnerClientId == shooter.OwnerClientId)
        {
            return;
        }

        hasHit = true;

        NPCCompetitor competitor = other.GetComponentInParent<NPCCompetitor>();
        if (competitor != null && shooter != null && shooter.IsSpawned)
        {
            competitor.RequestElimination(shooter);
        }
        else if (player != null &&
                 player.TryGetComponent(
                     out HumanCompetitorElimination elimination) &&
                 shooter != null &&
                 shooter.IsSpawned)
        {
            elimination.RequestElimination(shooter);
        }

        Destroy(gameObject);
    }
}
