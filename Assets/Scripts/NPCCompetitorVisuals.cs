using Unity.Netcode;
using UnityEngine;

public class NPCCompetitorVisuals : NetworkBehaviour
{
    [SerializeField] private GameObject[] characterVisuals;

    private NPCCompetitor competitor;
    private Collider bodyCollider;
    private int displayedCharacter = -1;
    private bool wasEliminated;

    private readonly NetworkVariable<int> selectedCharacter = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        competitor = GetComponent<NPCCompetitor>();
        bodyCollider = GetComponent<Collider>();
        selectedCharacter.OnValueChanged += OnSelectedCharacterChanged;

        if (HasSelectionAuthority() &&
            characterVisuals != null &&
            characterVisuals.Length > 0)
        {
            selectedCharacter.Value = Random.Range(0, characterVisuals.Length);
        }

        ApplySelection(selectedCharacter.Value);
        if (bodyCollider != null)
        {
            bodyCollider.enabled = competitor == null || !competitor.IsEliminated;
        }
    }

    private void Update()
    {
        bool isEliminated = competitor != null && competitor.IsEliminated;
        if (isEliminated != wasEliminated)
        {
            wasEliminated = isEliminated;
            ApplySelection(displayedCharacter);
            if (bodyCollider != null)
            {
                bodyCollider.enabled = !isEliminated;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        selectedCharacter.OnValueChanged -= OnSelectedCharacterChanged;
        base.OnNetworkDespawn();
    }

    private void OnSelectedCharacterChanged(int previousValue, int newValue)
    {
        ApplySelection(newValue);
    }

    private void ApplySelection(int index)
    {
        if (characterVisuals == null || characterVisuals.Length == 0)
        {
            Debug.LogError($"No NPC character visual references are assigned on '{name}'.", this);
            return;
        }

        if (index < 0 || index >= characterVisuals.Length)
        {
            displayedCharacter = -1;
            foreach (GameObject visual in characterVisuals)
            {
                if (visual != null)
                {
                    visual.SetActive(false);
                }
            }

            Debug.LogError($"NPC character selection {index} is invalid on '{name}'.", this);
            return;
        }

        displayedCharacter = index;
        bool isEliminated = competitor != null && competitor.IsEliminated;
        for (int i = 0; i < characterVisuals.Length; i++)
        {
            GameObject visual = characterVisuals[i];
            if (visual == null)
            {
                Debug.LogError(
                    $"NPC character visual reference {i} is missing on '{name}'.",
                    this
                );
                continue;
            }

            bool selected = i == index && !isEliminated;
            visual.SetActive(selected);
            if (!selected)
            {
                continue;
            }

            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = gameObject.layer;
            }

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError(
                    $"Selected NPC model '{visual.name}' has no MeshRenderer or SkinnedMeshRenderer.",
                    visual
                );
                continue;
            }

            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled)
                {
                    renderer.enabled = true;
                }

                if (renderer.sharedMaterials.Length == 0)
                {
                    Debug.LogWarning(
                        $"Selected NPC renderer '{renderer.name}' has no materials assigned.",
                        renderer
                    );
                }
                else
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null || material.shader == null)
                        {
                            Debug.LogWarning(
                                $"Selected NPC renderer '{renderer.name}' has a missing material or shader.",
                                renderer
                            );
                        }
                        else if (!material.shader.isSupported)
                        {
                            Debug.LogWarning(
                                $"NPC material '{material.name}' uses unsupported shader '{material.shader.name}'.",
                                material
                            );
                        }
                    }
                }

                if (renderer.bounds.size.sqrMagnitude <= 0.0001f)
                {
                    Debug.LogWarning(
                        $"Selected NPC renderer '{renderer.name}' has empty bounds.",
                        renderer
                    );
                }
            }
        }
    }

    private bool HasSelectionAuthority()
    {
        return IsServer ||
               (NetworkManager != null &&
                NetworkManager.DistributedAuthorityMode &&
                IsSessionOwner);
    }
}
