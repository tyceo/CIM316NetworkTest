using System.Collections;
using UnityEngine;

public class TutorialItemSpawner : MonoBehaviour
{
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float respawnDelay = 2f;

    private GameObject currentItem;

    private void Start()
    {
        SpawnItem();
    }

    private void SpawnItem()
    {
        currentItem = Instantiate(itemPrefab, spawnPoint.position, spawnPoint.rotation);

        TutorialItem tutorialItem = currentItem.GetComponent<TutorialItem>();

        if (tutorialItem != null)
        {
            tutorialItem.SetSpawner(this);
        }
    }

    public void Respawn()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        SpawnItem();
    }
}