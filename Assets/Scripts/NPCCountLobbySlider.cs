using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NPCCountLobbySlider : MonoBehaviour
{
    [SerializeField] private NPCFillManager npcFillManager;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text countLabel;

    private bool managerSubscribed;
    private bool sliderSubscribed;
    private int displayedCount;

    private void OnEnable()
    {
        if (npcFillManager == null)
        {
            npcFillManager = GetComponent<NPCFillManager>();
        }

        ResolveUIReferences();

        if (npcFillManager != null)
        {
            npcFillManager.SelectedNpcCountChanged += OnSelectedNpcCountChanged;
            npcFillManager.MatchActiveChanged += OnMatchActiveChanged;
            npcFillManager.NetworkSpawnStateChanged += OnNetworkSpawnStateChanged;
            managerSubscribed = true;

            if (npcFillManager.IsSpawned)
            {
                OnSelectedNpcCountChanged(npcFillManager.SelectedNpcCount);
                OnMatchActiveChanged(npcFillManager.IsMatchActive);
            }
        }
    }

    private void Start()
    {
        ResolveUIReferences();

        if (npcFillManager == null || slider == null || countLabel == null)
        {
            Debug.LogError(
                "NPC count slider is missing its NPCFillManager, Slider, or TextMeshPro label reference.",
                this
            );
            enabled = false;
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 3f;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(Mathf.Clamp(slider.value, 0f, 3f));
        slider.onValueChanged.RemoveListener(OnSliderChanged);
        slider.onValueChanged.AddListener(OnSliderChanged);
        sliderSubscribed = true;

        OnSelectedNpcCountChanged(npcFillManager.SelectedNpcCount);
        UpdateInteractable();
    }

    private void ResolveUIReferences()
    {
        if (slider != null && countLabel != null)
        {
            return;
        }

        GameObject panel = GameObject.Find("NPC Competitors Panel");
        if (panel == null)
        {
            return;
        }

        Transform sliderTransform = panel.transform.Find("NPC Count Slider");
        if (slider == null && sliderTransform != null)
        {
            slider = sliderTransform.GetComponent<Slider>();
        }

        Transform labelTransform = panel.transform.Find("NPC Count Label");
        if (countLabel == null && labelTransform != null)
        {
            countLabel = labelTransform.GetComponent<TMP_Text>();
        }

        if (slider != null && countLabel != null)
        {
            Debug.Log(
                "[NPC Slider Debug] Resolved slider and label from the serialized lobby panel.",
                this
            );
        }
    }

    private void OnDisable()
    {
        if (sliderSubscribed && slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
            sliderSubscribed = false;
        }

        if (managerSubscribed && npcFillManager != null)
        {
            npcFillManager.SelectedNpcCountChanged -= OnSelectedNpcCountChanged;
            npcFillManager.MatchActiveChanged -= OnMatchActiveChanged;
            npcFillManager.NetworkSpawnStateChanged -= OnNetworkSpawnStateChanged;
            managerSubscribed = false;
        }
    }

    private void OnSliderChanged(float value)
    {
        int requestedCount = Mathf.Clamp(Mathf.RoundToInt(value), 0, 3);
        NetworkManager networkManager = NetworkManager.Singleton;
        bool isSessionOwner = networkManager != null &&
                              networkManager.DistributedAuthorityMode &&
                              networkManager.IsListening &&
                              networkManager.LocalClient.IsSessionOwner;
        Debug.Log(
            $"[NPC Slider Debug] Slider changed: old={displayedCount}, " +
            $"requested={requestedCount}, server={networkManager != null && networkManager.IsServer}, " +
            $"objectOwner={npcFillManager != null && npcFillManager.IsOwner}, " +
            $"sessionOwner={isSessionOwner}, " +
            $"canChange={npcFillManager != null && npcFillManager.CanChangeNpcCount}.",
            this
        );

        if (npcFillManager != null && npcFillManager.CanChangeNpcCount)
        {
            UpdateCountDisplay(requestedCount);
            npcFillManager.RequestNpcCount(requestedCount);
        }
        else if (npcFillManager != null)
        {
            OnSelectedNpcCountChanged(npcFillManager.SelectedNpcCount);
        }
    }

    private void OnSelectedNpcCountChanged(int value)
    {
        UpdateCountDisplay(value);
        UpdateInteractable();
    }

    private void UpdateCountDisplay(int value)
    {
        displayedCount = value;
        if (countLabel != null)
        {
            countLabel.text = $"NPC COMPETITORS: {value}";
        }

        if (slider != null)
        {
            slider.SetValueWithoutNotify(value);
        }
    }

    private void OnMatchActiveChanged(bool isActive)
    {
        UpdateInteractable();
    }

    private void OnNetworkSpawnStateChanged(bool isSpawned)
    {
        if (isSpawned && npcFillManager != null)
        {
            OnSelectedNpcCountChanged(npcFillManager.SelectedNpcCount);
            OnMatchActiveChanged(npcFillManager.IsMatchActive);
        }
        else
        {
            UpdateInteractable();
        }
    }

    private void UpdateInteractable()
    {
        if (slider != null)
        {
            slider.interactable = npcFillManager != null &&
                                  npcFillManager.CanChangeNpcCount;
        }
    }
}
