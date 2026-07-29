using Unity.Netcode;
using UnityEngine;

public class NetworkCosmetics : NetworkBehaviour
{
    [Header("Character Models")]
    [SerializeField] private GameObject[] characters;
    [SerializeField] private GameObject originalAvatarVisuals;
    private const string CharacterSaveKey = "SelectedCharacter";

    private NetworkVariable<int> selectedCharacter =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        selectedCharacter.OnValueChanged += OnCharacterChanged;

        // Apply whatever value already exists.
        ApplyCharacter(selectedCharacter.Value);

        // Only this player's headset sends its own saved choice.
        if (IsOwner)
        {
            int savedCharacter =
                PlayerPrefs.GetInt(CharacterSaveKey, 0);

            savedCharacter = Mathf.Clamp(
                savedCharacter,
                0,
                characters.Length - 1
            );

            selectedCharacter.Value = savedCharacter;

            ApplyCharacter(savedCharacter);
        }
    }

    public override void OnNetworkDespawn()
    {
        selectedCharacter.OnValueChanged -= OnCharacterChanged;

        base.OnNetworkDespawn();
    }

    private void OnCharacterChanged(
        int previousValue,
        int newValue
    )
    {
        ApplyCharacter(newValue);
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

        if (originalAvatarVisuals != null)
        {
            originalAvatarVisuals.SetActive(false);
        }

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
            {
                characters[i].SetActive(
                    i == characterIndex
                );
            }
        }
    }
}