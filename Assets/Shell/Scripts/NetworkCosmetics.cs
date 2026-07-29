using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkCosmetics : NetworkBehaviour
{
    [Header("Characters")]
    [Tooltip("Must match the order in CosmeticsManager.")]
    [SerializeField] private GameObject[] characters;

    [Header("Separate Default Head")]
    [Tooltip("Assign Head > icecube here.")]
    [SerializeField] private GameObject originalHeadVisuals;

    [Header("Default Character")]
    [Tooltip("MC3_Red is index 2.")]
    [SerializeField] private int defaultCharacterIndex = 2;

    private const string CharacterSaveKey = "SelectedCharacter";

    private readonly NetworkVariable<int> selectedCharacter =
        new NetworkVariable<int>(
            2,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        selectedCharacter.OnValueChanged += OnCharacterChanged;
        StartCoroutine(ApplyAfterSpawn());
    }

    private IEnumerator ApplyAfterSpawn()
    {
        yield return null;
        yield return null;

        if (characters == null || characters.Length == 0)
        {
            yield break;
        }

        if (IsOwner)
        {
            int savedIndex = PlayerPrefs.GetInt(
                CharacterSaveKey,
                defaultCharacterIndex
            );

            savedIndex = Mathf.Clamp(
                savedIndex,
                0,
                characters.Length - 1
            );

            selectedCharacter.Value = savedIndex;
            ApplyCharacter(savedIndex);
        }
        else
        {
            ApplyCharacter(selectedCharacter.Value);
        }
    }

    private void OnCharacterChanged(int oldIndex, int newIndex)
    {
        ApplyCharacter(newIndex);
    }

    private void ApplyCharacter(int characterIndex)
    {
        if (characters == null || characters.Length == 0)
        {
            return;
        }

        characterIndex = Mathf.Clamp(
            characterIndex,
            0,
            characters.Length - 1
        );

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
            {
                characters[i].SetActive(i == characterIndex);
            }
        }

        // The separate icecube head is only used by MC3_Red.
        if (originalHeadVisuals != null)
        {
            originalHeadVisuals.SetActive(
                characterIndex == defaultCharacterIndex
            );
        }
    }

    public override void OnNetworkDespawn()
    {
        selectedCharacter.OnValueChanged -= OnCharacterChanged;
        base.OnNetworkDespawn();
    }
}