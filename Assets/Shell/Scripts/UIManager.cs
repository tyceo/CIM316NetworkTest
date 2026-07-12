using System.Collections;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Big Floating Instruction")]
    [SerializeField] private GameObject floatingTextObject;
    [SerializeField] private TextMeshPro floatingText;

    [Header("Floating Status UI")]
    [SerializeField] private TextMeshPro minigameStatusText;
    [SerializeField] private TextMeshPro playersEliminatedStatusText;

    [Header("General Timing")]
    [SerializeField] private float defaultMessageDuration = 20f;
    [SerializeField] private float statusDuration = 5f;

    [Header("Round Sequence Timing")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float statusDisplayDuration = 2f;
    [SerializeField] private float countdownStepDuration = 1f;
    [SerializeField] private float goDisplayDuration = 0.8f;
    [SerializeField] private float instructionDisplayDuration = 2f;

    private Coroutine currentMessageRoutine;
    private Coroutine minigameStatusRoutine;
    private Coroutine playersStatusRoutine;
    private Coroutine roundSequenceRoutine;

    private string lastMinigameStatus = "";
    private string lastPlayersStatus = "";

    public bool IsRoundSequencePlaying => roundSequenceRoutine != null;

    // Used by MinigameManager so gameplay waits until the UI sequence finishes.
    public float RoundSequenceDuration =>
        fadeDuration +
        statusDisplayDuration +
        fadeDuration +
        (countdownStepDuration * 3f) +
        goDisplayDuration +
        fadeDuration +
        instructionDisplayDuration +
        fadeDuration;

    private void Awake()
    {
        Instance = this;
        HideFloatingText();
        ClearStatusUI();
        SetAllTextAlpha(1f);
    }

    public void ShowMessage(string message)
    {
        ShowMessage(message, defaultMessageDuration);
    }

    public void ShowMessage(string message, float duration)
    {
        StopCurrentMessage();

        currentMessageRoutine = StartCoroutine(ShowMessageRoutine(message, duration));
    }

    private IEnumerator ShowMessageRoutine(string message, float duration)
    {
        ShowMessageNoTimer(message);

        yield return new WaitForSeconds(duration);

        HideFloatingText();
        currentMessageRoutine = null;
    }

    public void ShowMessageNoTimer(string message)
    {
        StopCurrentMessage();

        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(true);
        }

        if (floatingText != null)
        {
            floatingText.text = message;
            SetTextAlpha(floatingText, 1f);
        }
    }

    public void HideFloatingText()
    {
        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(false);
        }
    }

    public void SetMinigameStatus(string message)
    {
        // The round sequence owns these texts while it is playing.
        if (IsRoundSequencePlaying || message == lastMinigameStatus)
        {
            return;
        }

        lastMinigameStatus = message;

        if (minigameStatusRoutine != null)
        {
            StopCoroutine(minigameStatusRoutine);
        }

        minigameStatusRoutine = StartCoroutine(MinigameStatusRoutine(message));
    }

    private IEnumerator MinigameStatusRoutine(string message)
    {
        if (minigameStatusText != null)
        {
            minigameStatusText.text = message;
            SetTextAlpha(minigameStatusText, 1f);
        }

        yield return new WaitForSeconds(statusDuration);

        if (minigameStatusText != null)
        {
            minigameStatusText.text = "";
        }

        minigameStatusRoutine = null;
    }

    public void SetPlayersEliminatedStatus(string message)
    {
        // The round sequence owns these texts while it is playing.
        if (IsRoundSequencePlaying || message == lastPlayersStatus)
        {
            return;
        }

        lastPlayersStatus = message;

        if (playersStatusRoutine != null)
        {
            StopCoroutine(playersStatusRoutine);
        }

        playersStatusRoutine = StartCoroutine(PlayersStatusRoutine(message));
    }

    private IEnumerator PlayersStatusRoutine(string message)
    {
        if (playersEliminatedStatusText != null)
        {
            playersEliminatedStatusText.text = message;
            SetTextAlpha(playersEliminatedStatusText, 1f);
        }

        yield return new WaitForSeconds(statusDuration);

        if (playersEliminatedStatusText != null)
        {
            playersEliminatedStatusText.text = "";
        }

        playersStatusRoutine = null;
    }

    public void ShowStatus(string minigameMessage, string playersMessage)
    {
        SetMinigameStatus(minigameMessage);
        SetPlayersEliminatedStatus(playersMessage);
    }

    public void ClearStatusUI()
    {
        if (minigameStatusRoutine != null)
        {
            StopCoroutine(minigameStatusRoutine);
            minigameStatusRoutine = null;
        }

        if (playersStatusRoutine != null)
        {
            StopCoroutine(playersStatusRoutine);
            playersStatusRoutine = null;
        }

        if (minigameStatusText != null)
        {
            minigameStatusText.text = "";
        }

        if (playersEliminatedStatusText != null)
        {
            playersEliminatedStatusText.text = "";
        }

        lastMinigameStatus = "";
        lastPlayersStatus = "";
    }

    public void CountdownThenGo()
    {
        StopCurrentMessage();
        currentMessageRoutine = StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        ShowMessageNoTimer("3");
        yield return new WaitForSeconds(1f);

        ShowMessageNoTimer("2");
        yield return new WaitForSeconds(1f);

        ShowMessageNoTimer("1");
        yield return new WaitForSeconds(1f);

        ShowMessageNoTimer("GO!");
        yield return new WaitForSeconds(1f);

        HideFloatingText();
        currentMessageRoutine = null;
    }

    public void StartRoundSequence(
        string minigame,
        string playersRemaining,
        string instruction)
    {
        StopAllUIRoutines();
        roundSequenceRoutine = StartCoroutine(
            RoundSequenceRoutine(minigame, playersRemaining, instruction)
        );
    }

    private IEnumerator RoundSequenceRoutine(
        string minigame,
        string playersRemaining,
        string instruction)
    {
        ClearTextImmediately();

        // 1. Minigame and players remaining.
        if (minigameStatusText != null)
        {
            minigameStatusText.text = "CURRENT MINIGAME\n" + minigame;
        }

        if (playersEliminatedStatusText != null)
        {
            playersEliminatedStatusText.text = playersRemaining;
        }

        yield return FadeStatusText(0f, 1f);
        yield return new WaitForSeconds(statusDisplayDuration);
        yield return FadeStatusText(1f, 0f);

        if (minigameStatusText != null)
        {
            minigameStatusText.text = "";
        }

        if (playersEliminatedStatusText != null)
        {
            playersEliminatedStatusText.text = "";
        }

        // 2. Countdown.
        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(true);
        }

        SetTextAlpha(floatingText, 1f);

        SetFloatingText("3");
        yield return new WaitForSeconds(countdownStepDuration);

        SetFloatingText("2");
        yield return new WaitForSeconds(countdownStepDuration);

        SetFloatingText("1");
        yield return new WaitForSeconds(countdownStepDuration);

        SetFloatingText("GO!");
        yield return new WaitForSeconds(goDisplayDuration);

        yield return FadeSingleText(floatingText, 1f, 0f);

        // 3. Instruction.
        SetFloatingText(instruction);
        SetTextAlpha(floatingText, 0f);
        yield return FadeSingleText(floatingText, 0f, 1f);
        yield return new WaitForSeconds(instructionDisplayDuration);
        yield return FadeSingleText(floatingText, 1f, 0f);

        HideFloatingText();
        ClearTextImmediately();

        // Prevent the same status from instantly appearing again after the sequence.
        lastMinigameStatus = "CURRENT MINIGAME: " + minigame;
        lastPlayersStatus = playersRemaining;

        roundSequenceRoutine = null;
    }

    private void SetFloatingText(string message)
    {
        if (floatingText != null)
        {
            floatingText.text = message;
        }
    }

    private IEnumerator FadeStatusText(float from, float to)
    {
        float elapsed = 0f;

        SetTextAlpha(minigameStatusText, from);
        SetTextAlpha(playersEliminatedStatusText, from);

        while (elapsed < fadeDuration)
        {
            float progress = fadeDuration <= 0f ? 1f : elapsed / fadeDuration;
            float alpha = Mathf.Lerp(from, to, progress);

            SetTextAlpha(minigameStatusText, alpha);
            SetTextAlpha(playersEliminatedStatusText, alpha);

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetTextAlpha(minigameStatusText, to);
        SetTextAlpha(playersEliminatedStatusText, to);
    }

    private IEnumerator FadeSingleText(TextMeshPro text, float from, float to)
    {
        float elapsed = 0f;
        SetTextAlpha(text, from);

        while (elapsed < fadeDuration)
        {
            float progress = fadeDuration <= 0f ? 1f : elapsed / fadeDuration;
            SetTextAlpha(text, Mathf.Lerp(from, to, progress));

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetTextAlpha(text, to);
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

    private void SetAllTextAlpha(float alpha)
    {
        SetTextAlpha(floatingText, alpha);
        SetTextAlpha(minigameStatusText, alpha);
        SetTextAlpha(playersEliminatedStatusText, alpha);
    }

    private void StopCurrentMessage()
    {
        if (currentMessageRoutine != null)
        {
            StopCoroutine(currentMessageRoutine);
            currentMessageRoutine = null;
        }
    }

    private void StopAllUIRoutines()
    {
        StopCurrentMessage();

        if (minigameStatusRoutine != null)
        {
            StopCoroutine(minigameStatusRoutine);
            minigameStatusRoutine = null;
        }

        if (playersStatusRoutine != null)
        {
            StopCoroutine(playersStatusRoutine);
            playersStatusRoutine = null;
        }

        if (roundSequenceRoutine != null)
        {
            StopCoroutine(roundSequenceRoutine);
            roundSequenceRoutine = null;
        }
    }

    private void ClearTextImmediately()
    {
        if (minigameStatusText != null)
        {
            minigameStatusText.text = "";
            SetTextAlpha(minigameStatusText, 1f);
        }

        if (playersEliminatedStatusText != null)
        {
            playersEliminatedStatusText.text = "";
            SetTextAlpha(playersEliminatedStatusText, 1f);
        }

        if (floatingText != null)
        {
            floatingText.text = "";
            SetTextAlpha(floatingText, 1f);
        }
    }
}
