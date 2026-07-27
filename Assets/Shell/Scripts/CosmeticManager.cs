using TMPro;
using UnityEngine;

public class CosmeticsManager : MonoBehaviour
{
    public static CosmeticsManager Instance;

    [Header("Character Preview")]
    [SerializeField] private Renderer characterRenderer;

    [Header("UI")]
    [SerializeField] private TextMeshPro colourText;

    [Header("Colours")]
    [SerializeField] private Color[] availableColours =
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta
    };

    [SerializeField] private string[] colourNames =
    {
        "RED",
        "BLUE",
        "GREEN",
        "YELLOW",
        "PURPLE"
    };

    private int selectedColourIndex = 0;

    private const string ColourPrefKey = "SelectedCosmeticColour";

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        LoadCosmetics();
        ApplyColour();
    }

    public void NextColour()
    {
        if (availableColours == null || availableColours.Length == 0)
        {
            return;
        }

        selectedColourIndex++;

        if (selectedColourIndex >= availableColours.Length)
        {
            selectedColourIndex = 0;
        }

        ApplyColour();
        SaveCosmetics();
    }

    public void PreviousColour()
    {
        if (availableColours == null || availableColours.Length == 0)
        {
            return;
        }

        selectedColourIndex--;

        if (selectedColourIndex < 0)
        {
            selectedColourIndex = availableColours.Length - 1;
        }

        ApplyColour();
        SaveCosmetics();
    }

    private void ApplyColour()
    {
        if (availableColours == null || availableColours.Length == 0)
        {
            return;
        }

        selectedColourIndex = Mathf.Clamp(
            selectedColourIndex,
            0,
            availableColours.Length - 1
        );

        if (characterRenderer != null)
        {
            characterRenderer.material.color =
                availableColours[selectedColourIndex];
        }

        UpdateColourText();
    }

    private void UpdateColourText()
    {
        if (colourText == null)
        {
            return;
        }

        if (colourNames != null &&
            selectedColourIndex < colourNames.Length)
        {
            colourText.text = colourNames[selectedColourIndex];
        }
        else
        {
            colourText.text = "COLOUR " + (selectedColourIndex + 1);
        }
    }

    private void SaveCosmetics()
    {
        PlayerPrefs.SetInt(
            ColourPrefKey,
            selectedColourIndex
        );

        PlayerPrefs.Save();
    }

    private void LoadCosmetics()
    {
        selectedColourIndex =
            PlayerPrefs.GetInt(ColourPrefKey, 0);
    }

    public int GetSelectedColourIndex()
    {
        return selectedColourIndex;
    }

    public Color GetSelectedColour()
    {
        if (availableColours == null ||
            availableColours.Length == 0)
        {
            return Color.white;
        }

        return availableColours[selectedColourIndex];
    }
}