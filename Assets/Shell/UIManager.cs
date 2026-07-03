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

    [Header("Timing")]
    [SerializeField] private float defaultMessageDuration = 20f;
    [SerializeField] private float statusDuration = 5f;

    private Coroutine currentMessageRoutine;
    private Coroutine minigameStatusRoutine;
    private Coroutine playersStatusRoutine;

    private string lastMinigameStatus = "";
    private string lastPlayersStatus = "";

    private void Awake()
    {
        Instance = this;
        HideFloatingText();
        ClearStatusUI();
    }

    public void ShowMessage(string message)
    {
        ShowMessage(message, defaultMessageDuration);
    }

    public void ShowMessage(string message, float duration)
    {
        if (currentMessageRoutine != null)
        {
            StopCoroutine(currentMessageRoutine);
        }

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
        if (currentMessageRoutine != null)
        {
            StopCoroutine(currentMessageRoutine);
            currentMessageRoutine = null;
        }

        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(true);
        }

        if (floatingText != null)
        {
            floatingText.text = message;
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
        if (message == lastMinigameStatus)
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
        if (message == lastPlayersStatus)
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
        if (currentMessageRoutine != null)
        {
            StopCoroutine(currentMessageRoutine);
        }

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
}