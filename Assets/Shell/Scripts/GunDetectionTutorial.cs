using System.Collections;
using UnityEngine;

public class GunDetectionTutorial : MonoBehaviour
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

    public void EliminateDummy()
    {
        if (isEliminated)
        {
            return;
        }

        StartCoroutine(EliminateRoutine());
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

        SetDummyVisible(false);

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
            if (collider != null)
            {
                collider.enabled = visible;
            }
        }
    }
}