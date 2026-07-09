using UnityEngine;

public class TutorialItem : MonoBehaviour
{
    private TutorialItemSpawner spawner;

    public void SetSpawner(TutorialItemSpawner newSpawner)
    {
        spawner = newSpawner;
    }

    public void UseItem()
    {
        if (spawner != null)
        {
            spawner.Respawn();
        }

        Destroy(gameObject);
    }
}