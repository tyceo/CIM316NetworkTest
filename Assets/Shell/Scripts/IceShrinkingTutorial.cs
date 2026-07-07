using System.Collections;
using UnityEngine;

public class IceShrinkingTutorial : MonoBehaviour
{
    [Header("Shrink Settings")]
    [SerializeField] private float shrinkSpeed = 0.5f;
    [SerializeField] private float minScale = 0.11f;
    [SerializeField] private float resetDelay = 2f;

    [Header("Flashlight")]
    [SerializeField] private GameObject tutorialFlashlight;

    private bool isBeingLit = false;
    private bool isResetting = false;

    private Vector3 originalScale;
    private float lastHitTime;
    private float hitTimeout = 0.1f;

    void Start()
    {
        originalScale = transform.localScale;
    }

    void Update()
    {
        if (Time.time - lastHitTime > hitTimeout)
        {
            isBeingLit = false;
        }

        if (isBeingLit && !isResetting)
        {
            transform.localScale -= Vector3.one * shrinkSpeed * Time.deltaTime;

            if (transform.localScale.x <= minScale)
            {
                StartCoroutine(ResetRoutine());
            }
        }
    }

    public void OnFlashlightHit()
    {
        if (isResetting) return;

        isBeingLit = true;
        lastHitTime = Time.time;
    }

    private IEnumerator ResetRoutine()
    {
        isResetting = true;

        if (tutorialFlashlight != null)
        {
            TutorialItem item = tutorialFlashlight.GetComponent<TutorialItem>();
            if (item != null)
            {
                item.UseItem();
            }
        }

        yield return new WaitForSeconds(resetDelay);

        transform.localScale = originalScale;

        isBeingLit = false;
        isResetting = false;
    }
}