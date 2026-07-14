using System.Collections;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Floating Instruction")]
    [SerializeField] private GameObject floatingTextObject;
    [SerializeField] private TextMeshPro floatingText;

    [Header("Round Status")]
    [SerializeField] private TextMeshPro minigameStatusText;
    [SerializeField] private TextMeshPro playersRemainingText;

    [Header("Welcome")]
    [TextArea(2, 4)]
    [SerializeField] private string welcomeMessage =
        "WELCOME TO MINI MAYHEM!\nJOIN THE CHAOS ONLINE BY TOGGLING THE SWITCH UPWARDS.";

    [Header("Round Sequence Timing")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float statusDuration = 2f;
    [SerializeField] private float instructionDuration = 2f;

    private Coroutine activeSequence;
    private Coroutine temporaryMessage;
    private int sequenceVersion;

    public float RoundSequenceDuration =>
        fadeDuration +
        statusDuration +
        fadeDuration +
        fadeDuration +
        instructionDuration +
        fadeDuration;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ClearAllImmediately();
    }

    public void ShowWelcomeMessage()
    {
        CancelAllUI();

        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(true);
        }

        SetFloatingText(welcomeMessage, 1f);
    }

    public void HideWelcomeMessage()
    {
        if (floatingText != null && floatingText.text == welcomeMessage)
        {
            floatingText.text = "";
        }

        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(false);
        }
    }

    public void StartRoundSequence(
        string minigameName,
        string playersMessage,
        string instruction)
    {
        CancelAllUI();

        sequenceVersion++;
        int myVersion = sequenceVersion;

        activeSequence = StartCoroutine(
            RoundSequenceRoutine(
                myVersion,
                minigameName,
                playersMessage,
                instruction
            )
        );
    }

    private IEnumerator RoundSequenceRoutine(
        int myVersion,
        string minigameName,
        string playersMessage,
        string instruction)
    {
        ClearAllImmediately();

        // 1. Show the chosen minigame and starting player count.
        if (minigameStatusText != null)
        {
            minigameStatusText.text = "CURRENT MINIGAME: " + minigameName;
        }

        if (playersRemainingText != null)
        {
            playersRemainingText.text = playersMessage;
        }

        yield return FadeStatus(0f, 1f, myVersion);
        if (myVersion != sequenceVersion) yield break;

        yield return new WaitForSeconds(statusDuration);
        if (myVersion != sequenceVersion) yield break;

        yield return FadeStatus(1f, 0f, myVersion);
        if (myVersion != sequenceVersion) yield break;

        ClearStatusImmediately();

        // 2. Show the instruction.
        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(true);
        }

        SetFloatingText(instruction, 0f);

        yield return FadeText(floatingText, 0f, 1f, myVersion);
        if (myVersion != sequenceVersion) yield break;

        yield return new WaitForSeconds(instructionDuration);
        if (myVersion != sequenceVersion) yield break;

        yield return FadeText(floatingText, 1f, 0f, myVersion);
        if (myVersion != sequenceVersion) yield break;

        ClearAllImmediately();
        activeSequence = null;
    }

    public void ShowMessage(string message)
    {
        ShowMessage(message, 2f);
    }

    public void ShowMessage(string message, float duration)
    {
        if (temporaryMessage != null)
        {
            StopCoroutine(temporaryMessage);
        }

        temporaryMessage = StartCoroutine(
            TemporaryMessageRoutine(message, duration)
        );
    }

    private IEnumerator TemporaryMessageRoutine(string message, float duration)
    {
        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(true);
        }

        SetFloatingText(message, 1f);

        yield return new WaitForSeconds(duration);

        if (floatingText != null)
        {
            floatingText.text = "";
        }

        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(false);
        }

        temporaryMessage = null;
    }

    public void ShowMessageNoTimer(string message)
    {
        if (temporaryMessage != null)
        {
            StopCoroutine(temporaryMessage);
            temporaryMessage = null;
        }

        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(true);
        }

        SetFloatingText(message, 1f);
    }

    public void HideFloatingText()
    {
        if (floatingText != null)
        {
            floatingText.text = "";
        }

        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(false);
        }
    }

    // Kept for compatibility with existing MinigameManager calls.
    // These do nothing while the controlled round sequence is playing.
    public void SetMinigameStatus(string message)
    {
        if (activeSequence != null || minigameStatusText == null)
        {
            return;
        }

        minigameStatusText.text = message;
        SetTextAlpha(minigameStatusText, 1f);
    }

    public void SetPlayersEliminatedStatus(string message)
    {
        if (activeSequence != null || playersRemainingText == null)
        {
            return;
        }

        playersRemainingText.text = message;
        SetTextAlpha(playersRemainingText, 1f);
    }

    public void ShowStatus(string minigameMessage, string playersMessage)
    {
        SetMinigameStatus(minigameMessage);
        SetPlayersEliminatedStatus(playersMessage);
    }

    public void ClearStatusUI()
    {
        ClearStatusImmediately();
    }

    public void CancelAllUI()
    {
        sequenceVersion++;

        if (activeSequence != null)
        {
            StopCoroutine(activeSequence);
            activeSequence = null;
        }

        if (temporaryMessage != null)
        {
            StopCoroutine(temporaryMessage);
            temporaryMessage = null;
        }

        ClearAllImmediately();
    }

    private IEnumerator FadeStatus(float from, float to, int myVersion)
    {
        float elapsed = 0f;

        SetTextAlpha(minigameStatusText, from);
        SetTextAlpha(playersRemainingText, from);

        while (elapsed < fadeDuration)
        {
            if (myVersion != sequenceVersion)
            {
                yield break;
            }

            float t = fadeDuration <= 0f ? 1f : elapsed / fadeDuration;
            float alpha = Mathf.Lerp(from, to, t);

            SetTextAlpha(minigameStatusText, alpha);
            SetTextAlpha(playersRemainingText, alpha);

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetTextAlpha(minigameStatusText, to);
        SetTextAlpha(playersRemainingText, to);
    }

    private IEnumerator FadeText(
        TextMeshPro text,
        float from,
        float to,
        int myVersion)
    {
        float elapsed = 0f;
        SetTextAlpha(text, from);

        while (elapsed < fadeDuration)
        {
            if (myVersion != sequenceVersion)
            {
                yield break;
            }

            float t = fadeDuration <= 0f ? 1f : elapsed / fadeDuration;
            SetTextAlpha(text, Mathf.Lerp(from, to, t));

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetTextAlpha(text, to);
    }

    private void SetFloatingText(string message, float alpha)
    {
        if (floatingText == null)
        {
            return;
        }

        floatingText.text = message;
        SetTextAlpha(floatingText, alpha);
    }

    private void SetTextAlpha(TextMeshPro text, float alpha)
    {
        if (text == null)
        {
            return;
        }

        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }

    private void ClearStatusImmediately()
    {
        if (minigameStatusText != null)
        {
            minigameStatusText.text = "";
            SetTextAlpha(minigameStatusText, 1f);
        }

        if (playersRemainingText != null)
        {
            playersRemainingText.text = "";
            SetTextAlpha(playersRemainingText, 1f);
        }
    }

    private void ClearAllImmediately()
    {
        ClearStatusImmediately();

        if (floatingText != null)
        {
            floatingText.text = "";
            SetTextAlpha(floatingText, 1f);
        }

        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(false);
        }
    }
}