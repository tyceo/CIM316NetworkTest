using System.Collections;
using UnityEngine;

public class SwordTutorialDetection : MonoBehaviour
{
    [Header("Sword Detection")]
    [SerializeField] private string swordLayerName = "Sword";

    [Header("Dummy")]
    [SerializeField] private GameObject dummyRoot;
    [SerializeField] private float respawnDelay = 2f;

    [Header("Optional Feedback")]
    [SerializeField] private AudioSource hitSound;
    [SerializeField] private ParticleSystem hitEffect;

    private bool isEliminated = false;

    private void Start()
    {
        if (dummyRoot == null)
        {
            dummyRoot = gameObject;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isEliminated)
        {
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer(swordLayerName))
        {
            StartCoroutine(EliminateRoutine());
        }
    }

    private IEnumerator EliminateRoutine()
    {
        isEliminated = true;

        if (hitSound != null)
        {
            hitSound.Play();
        }

        if (hitEffect != null)
        {
            hitEffect.Play();
        }

        dummyRoot.SetActive(false);

        yield return new WaitForSeconds(respawnDelay);

        dummyRoot.SetActive(true);

        isEliminated = false;
    }
}