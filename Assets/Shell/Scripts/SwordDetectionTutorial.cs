using System.Collections;
using UnityEngine;

public class SwordDetectionTutorial : MonoBehaviour
{
    [Header("Dummy")]
    [SerializeField] private GameObject dummyRoot;
    [SerializeField] private float respawnDelay = 2f;

    [Header("Optional Feedback")]
    [SerializeField] private AudioSource hitSound;
    [SerializeField] private ParticleSystem hitEffect;

    private bool isEliminated = false;
    private Renderer[] dummyRenderers;
    private Collider[] dummyColliders;

    private void Start()
    {
        if (dummyRoot == null)
        {
            dummyRoot = gameObject;
        }

        dummyRenderers = dummyRoot.GetComponentsInChildren<Renderer>(true);
        dummyColliders = dummyRoot.GetComponentsInChildren<Collider>(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isEliminated)
        {
            return;
        }

        TutorialItem item = other.GetComponentInParent<TutorialItem>();

        if (item != null)
        {
            StartCoroutine(EliminateDummyRoutine(item));
        }
    }

    private IEnumerator EliminateDummyRoutine(TutorialItem item)
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

        SetDummyVisible(false);

        item.UseItem();

        yield return new WaitForSeconds(respawnDelay);

        SetDummyVisible(true);

        isEliminated = false;
    }

    private void SetDummyVisible(bool visible)
    {
        foreach (Renderer renderer in dummyRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        foreach (Collider collider in dummyColliders)
        {
            if (collider != null && collider != GetComponent<Collider>())
            {
                collider.enabled = visible;
            }
        }
    }
}