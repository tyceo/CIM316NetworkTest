using TMPro;
using UnityEngine;

public class CosmeticsManager : MonoBehaviour
{
    [Header("Characters")]
    [SerializeField] private GameObject[] characters;

    [Header("UI")]
    [SerializeField] private TextMeshPro characterNameText;
    [SerializeField] private TextMeshPro savedMessageText;

    [Header("Character Names")]
    [SerializeField] private string[] characterNames =
    {
        "Gelly",
        "Polly",
        "Cally",
        "Lally",
        "Bally"
    };

    [Header("Saved Message")]
    [SerializeField] private float savedMessageDuration = 1.5f;
    

    private int selectedCharacter = 0;
    private float savedMessageTimer = 0f;

    private const string CharacterSaveKey = "SelectedCharacter";

    private void Start()
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogWarning(
                "CosmeticsManager: No characters have been assigned."
            );

            return;
        }

        selectedCharacter = PlayerPrefs.GetInt(
            CharacterSaveKey,
            0
        );

        selectedCharacter = Mathf.Clamp(
            selectedCharacter,
            0,
            characters.Length - 1
        );

        if (savedMessageText != null)
        {
            savedMessageText.gameObject.SetActive(false);
        }

        UpdateCharacter();
    }

    private void Update()
    {
        if (savedMessageTimer <= 0f)
        {
            return;
        }

        savedMessageTimer -= Time.deltaTime;

        if (savedMessageTimer <= 0f &&
            savedMessageText != null)
        {
            savedMessageText.gameObject.SetActive(false);
        }
    }

    public void NextCharacter()
    {
        if (characters == null || characters.Length == 0)
        {
            return;
        }

        selectedCharacter++;

        if (selectedCharacter >= characters.Length)
        {
            selectedCharacter = 0;
        }

        UpdateCharacter();
        HideSavedMessage();
    }

    public void PreviousCharacter()
    {
        if (characters == null || characters.Length == 0)
        {
            return;
        }

        selectedCharacter--;

        if (selectedCharacter < 0)
        {
            selectedCharacter = characters.Length - 1;
        }

        UpdateCharacter();
        HideSavedMessage();
    }

    public void SaveCharacter()
    {
        PlayerPrefs.SetInt(
            CharacterSaveKey,
            selectedCharacter
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Character saved: " +
            selectedCharacter
        );

        ShowSavedMessage();
    }

    private void UpdateCharacter()
    {
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
            {
                characters[i].SetActive(
                    i == selectedCharacter
                );
            }
        }

        if (characterNameText != null)
        {
            if (characterNames != null &&
                selectedCharacter < characterNames.Length)
            {
                characterNameText.text =
                    characterNames[selectedCharacter];
            }
            else
            {
                characterNameText.text =
                    "CHARACTER " +
                    (selectedCharacter + 1);
            }
        }
    }

    private void ShowSavedMessage()
    {
        if (savedMessageText == null)
        {
            return;
        }

        savedMessageText.text = "SAVED!";
        savedMessageText.gameObject.SetActive(true);

        savedMessageTimer = savedMessageDuration;
    }

    private void HideSavedMessage()
    {
        savedMessageTimer = 0f;

        if (savedMessageText != null)
        {
            savedMessageText.gameObject.SetActive(false);
        }
    }

    public int GetSelectedCharacter()
    {
        return selectedCharacter;
    }
}