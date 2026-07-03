using System.Collections;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Floating VR Text")]
    [SerializeField] private GameObject floatingTextObject;
    [SerializeField] private TextMeshPro floatingText;

    [Header("Timing")]
    [SerializeField] private float defaultMessageDuration = 2f;

    private Coroutine currentMessageRoutine;

    private void Awake()
    {
        Instance = this;
        HideFloatingText();
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
        if (floatingTextObject != null)
        {
            floatingTextObject.SetActive(true);
        }

        if (floatingText != null)
        {
            floatingText.text = message;
        }

        yield return new WaitForSeconds(duration);

        HideFloatingText();
    }

    public void ShowMessageNoTimer(string message)
    {
        if (currentMessageRoutine != null)
        {
            StopCoroutine(currentMessageRoutine);
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
    }
}