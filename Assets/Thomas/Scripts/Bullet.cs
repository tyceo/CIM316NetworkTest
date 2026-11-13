using UnityEngine;
using Unity.Netcode;

public class Bullet : MonoBehaviour
{
    private NetworkObject shooter;

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
        NPCCompetitor competitor = other.GetComponentInParent<NPCCompetitor>();
        if (competitor != null && shooter != null && shooter.IsSpawned)
        {
            competitor.RequestElimination(shooter);
        }

        Destroy(gameObject);
    }
}
