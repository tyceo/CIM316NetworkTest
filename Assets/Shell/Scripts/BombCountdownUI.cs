using TMPro;
using UnityEngine;

public class BombCountdownUI : MonoBehaviour
{
    public static BombCountdownUI Instance;

    [Header("UI")]
    [SerializeField] private TMP_Text countdownText;

    [Header("Display")]
    [SerializeField] private string prefix = "BOMB: ";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HideCountdown();
    }

    public void ShowCountdown(int seconds)
    {
        if (countdownText == null)
        {
            return;
        }

        countdownText.gameObject.SetActive(true);
        countdownText.text = prefix + Mathf.Max(0, seconds);
    }

    public void HideCountdown()
    {
        if (countdownText == null)
        {
            return;
        }

        countdownText.text = "";
        countdownText.gameObject.SetActive(false);
    }
}