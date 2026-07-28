using TMPro;
using UnityEngine;

public class CosmeticsManager : MonoBehaviour
{
    [Header("Characters")]
    [SerializeField] private GameObject[] characters;

    [Header("UI")]
    [SerializeField] private TextMeshPro characterNameText;

    [Header("Character Names")]
    [SerializeField] private string[] characterNames =
    {
        "Gelly",
        "Polly",
        "Cally",
        "Lally",
        "Bally"
    };

    private int selectedCharacter = 0;

    private const string CharacterSaveKey = "SelectedCharacter";

    private void Start()
    {
        selectedCharacter = PlayerPrefs.GetInt(CharacterSaveKey, 0);

        selectedCharacter = Mathf.Clamp(
            selectedCharacter,
            0,
            characters.Length - 1
        );

        UpdateCharacter();
    }

    public void NextCharacter()
    {
        selectedCharacter++;

        if (selectedCharacter >= characters.Length)
        {
            selectedCharacter = 0;
        }

        UpdateCharacter();
        SaveCharacter();
    }

    public void PreviousCharacter()
    {
        selectedCharacter--;

        if (selectedCharacter < 0)
        {
            selectedCharacter = characters.Length - 1;
        }

        UpdateCharacter();
        SaveCharacter();
    }

    private void UpdateCharacter()
    {
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
            {
                characters[i].SetActive(i == selectedCharacter);
            }
        }

        if (characterNameText != null &&
            selectedCharacter < characterNames.Length)
        {
            characterNameText.text =
                characterNames[selectedCharacter];
        }
    }

    private void SaveCharacter()
    {
        PlayerPrefs.SetInt(
            CharacterSaveKey,
            selectedCharacter
        );

        PlayerPrefs.Save();
    }

    public int GetSelectedCharacter()
    {
        return selectedCharacter;
    }
}