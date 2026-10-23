using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;

public class IceShrinking : NetworkBehaviour
{
    [Header("Ice Settings")]
    [SerializeField] private float shrinkSpeed = 0.5f;
    [SerializeField] private float minScale = 0.11f;
    [SerializeField] private bool destroyWhenMinimum = true;

    [Header("Ice Crack Audio")]
    [Tooltip("Add a few different ice cracking sounds here.")]
    [SerializeField] private AudioClip[] iceCrackSounds;

    [Tooltip("How often an ice crack can play.")]
    [SerializeField] private float crackCooldown = 2f;

    [Range(0f, 1f)]
    [SerializeField] private float crackVolume = 0.25f;

    [SerializeField] private AudioSource audioSource;

    private bool isBeingLit = false;
    private Vector3 originalScale;
    private MinigameManager minigameManager;
    private Coroutine finalCrackCoroutine;

    private float lastHitTime;
    private float hitTimeout = 0.1f;

    private float lastCrackTime = -100f;
    private int lastCrackIndex = -1;

    void Start()
    {
        originalScale = transform.localScale;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;

        // Make cracking sound come from the ice.
        audioSource.spatialBlend = 1f;
        audioSource.loop = false;

        minigameManager = FindFirstObjectByType<MinigameManager>();

        if (minigameManager != null)
        {
            minigameManager.currentMinigame.OnValueChanged += OnMinigameStateChanged;
            minigameManager.minigameRunning.OnValueChanged += OnMinigameRunningChanged;
        }
    }

    void OnDestroy()
    {
        if (minigameManager != null)
        {
            minigameManager.currentMinigame.OnValueChanged -= OnMinigameStateChanged;
            minigameManager.minigameRunning.OnValueChanged -= OnMinigameRunningChanged;
        }
    }

    void Update()
    {
        if (!IsMeltMinigameActive())
        {
            StopCrackAudio();
        }

        // Flashlight hasn't hit recently.
        if (Time.time - lastHitTime > hitTimeout)
        {
            isBeingLit = false;
        }

        if (isBeingLit)
        {
            // Shrink the ice.
            transform.localScale -=
                Vector3.one *
                shrinkSpeed *
                Time.deltaTime;

            // Play cracking sounds while shrinking.
            if (
                Time.time - lastCrackTime >=
                crackCooldown
            )
            {
                PlayRandomCrack();
                lastCrackTime = Time.time;
            }

            if (transform.localScale.x <= minScale)
            {
                if (destroyWhenMinimum)
                {
                    Debug.Log(
                        "Destroying Ice Shrinking"
                    );

                    if (!IsOwner)
                    {
                        return;
                    }

                    // Preserve the final crack without overlapping the current clip.
                    if (finalCrackCoroutine == null && IsMeltMinigameActive())
                    {
                        finalCrackCoroutine = StartCoroutine(PlayFinalCrackWhenReady());
                    }

                    playersweeperthing sweeper =
                        FindObjectOfType<playersweeperthing>();

                    if (sweeper != null)
                    {
                        sweeper.SendPlayerToStart();

                        Debug.Log(
                            "Sending player to start"
                        );
                    }
                    else
                    {
                        Debug.LogWarning(
                            "playersweeperthing not found!"
                        );
                    }

                    transform.localScale =
                        originalScale;

                    isBeingLit = false;
                }
            }
        }
    }

    private void PlayRandomCrack()
    {
        if (
            iceCrackSounds == null ||
            iceCrackSounds.Length == 0 ||
            audioSource == null ||
            audioSource.isPlaying ||
            !IsMeltMinigameActive()
        )
        {
            return;
        }

        int randomIndex;

        // Try not to play the exact same crack twice.
        if (iceCrackSounds.Length > 1)
        {
            do
            {
                randomIndex =
                    Random.Range(
                        0,
                        iceCrackSounds.Length
                    );
            }
            while (randomIndex == lastCrackIndex);
        }
        else
        {
            randomIndex = 0;
        }

        lastCrackIndex = randomIndex;

        AudioClip selectedClip =
            iceCrackSounds[randomIndex];

        if (selectedClip != null)
        {
            // Slight random pitch makes repeated cracks
            // sound less identical.
            audioSource.pitch =
                Random.Range(0.9f, 1.1f);

            audioSource.PlayOneShot(
                selectedClip,
                crackVolume
            );
        }
    }

    private System.Collections.IEnumerator PlayFinalCrackWhenReady()
    {
        while (IsMeltMinigameActive() && audioSource != null && audioSource.isPlaying)
        {
            yield return null;
        }

        finalCrackCoroutine = null;

        if (IsMeltMinigameActive())
        {
            PlayRandomCrack();
        }
    }

    private bool IsMeltMinigameActive()
    {
        return minigameManager != null &&
            minigameManager.currentMinigame.Value == 1 &&
            minigameManager.minigameRunning.Value;
    }

    private void OnMinigameStateChanged(int previousValue, int newValue)
    {
        if (!IsMeltMinigameActive())
        {
            StopCrackAudio();
        }
    }

    private void OnMinigameRunningChanged(bool previousValue, bool newValue)
    {
        if (!IsMeltMinigameActive())
        {
            StopCrackAudio();
        }
    }

    private void StopCrackAudio()
    {
        if (finalCrackCoroutine != null)
        {
            StopCoroutine(finalCrackCoroutine);
            finalCrackCoroutine = null;
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    public void ResetTheSize()
    {
        StopCrackAudio();
        transform.localScale = originalScale;

        isBeingLit = false;
        lastCrackTime = -100f;
    }

    // Called by the flashlight when its raycast hits the ice.
    public void OnFlashlightHit()
    {
        isBeingLit = true;
        lastHitTime = Time.time;
    }
}