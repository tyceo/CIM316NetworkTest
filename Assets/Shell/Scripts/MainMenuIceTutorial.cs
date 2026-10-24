using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MainMenuIceTutorial : TutorialFlashlightTarget
{
    private const string TutorialSeenKey = "MiniMayhem.MainMenuIceTutorialSeen";

    private static readonly Vector3[][] CrackPaths =
    {
        new Vector3[]
        {
            new Vector3(-0.36f, 0.38f, -0.506f),
            new Vector3(-0.17f, 0.16f, -0.506f),
            new Vector3(-0.22f, -0.04f, -0.506f),
            new Vector3(0.02f, -0.21f, -0.506f),
            new Vector3(0.24f, -0.42f, -0.506f)
        },
        new Vector3[]
        {
            new Vector3(-0.17f, 0.16f, -0.506f),
            new Vector3(-0.40f, 0.12f, -0.506f),
            new Vector3(-0.48f, -0.02f, -0.506f)
        },
        new Vector3[]
        {
            new Vector3(-0.22f, -0.04f, -0.506f),
            new Vector3(-0.38f, -0.30f, -0.506f)
        },
        new Vector3[]
        {
            new Vector3(-0.02f, 0.39f, -0.506f),
            new Vector3(0.12f, 0.20f, -0.506f),
            new Vector3(0.31f, 0.10f, -0.506f),
            new Vector3(0.25f, -0.10f, -0.506f),
            new Vector3(0.42f, -0.28f, -0.506f)
        },
        new Vector3[]
        {
            new Vector3(0.25f, -0.10f, -0.506f),
            new Vector3(0.08f, -0.31f, -0.506f),
            new Vector3(0.02f, -0.43f, -0.506f)
        }
    };

    [Header("Menu References")]
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject flashlightPrefab;
    [SerializeField] private Transform flashlightSpawnPoint;
    [SerializeField] private TMP_Text floatingHandPrompt;

    [Header("Ice Appearance")]
    [SerializeField] private Renderer iceRenderer;
    [SerializeField] private Material frostedIceMaterial;
    [SerializeField] private Material crackLineMaterial;

    [Header("Melting")]
    [SerializeField] private float shrinkSpeed = 0.35f;
    [SerializeField] private float minimumScale = 0.04f;
    [SerializeField] private float hitTimeout = 0.15f;

    private XRGrabInteractable flashlightInteractable;
    private FlashlightTutorial[] flashlightBehaviours;
    private Material generatedCrackMaterial;
    private float lastFlashlightHitTime = -100f;
    private bool tutorialActive;
    private bool hasGrabbedFlashlight;
    private bool flashlightIsOn;
    private bool isMelted;

    public void ConfigureInEditor(
        Button start,
        GameObject flashlight,
        Transform spawnPoint,
        TMP_Text prompt,
        Renderer ice,
        Material iceMaterial,
        Material cracksMaterial)
    {
        startButton = start;
        flashlightPrefab = flashlight;
        flashlightSpawnPoint = spawnPoint;
        floatingHandPrompt = prompt;
        iceRenderer = ice;
        frostedIceMaterial = iceMaterial;
        crackLineMaterial = cracksMaterial;
    }

    private void Awake()
    {
        if (iceRenderer == null)
        {
            iceRenderer = GetComponent<Renderer>();
        }

        if (startButton == null)
        {
            GameStartMenu menu = FindFirstObjectByType<GameStartMenu>();
            startButton = menu != null ? menu.startButton : null;
        }

        if (PlayerPrefs.GetInt(TutorialSeenKey, 0) != 0)
        {
            if (startButton != null)
            {
                startButton.interactable = true;
            }

            if (floatingHandPrompt != null)
            {
                floatingHandPrompt.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
            return;
        }

        if (startButton != null)
        {
            startButton.interactable = false;
        }
    }

    private void Start()
    {
        if (PlayerPrefs.GetInt(TutorialSeenKey, 0) != 0)
        {
            return;
        }

        if (startButton == null || flashlightPrefab == null || flashlightSpawnPoint == null)
        {
            Debug.LogError("Main menu ice tutorial is missing its START button, flashlight prefab, or spawn point.", this);
            UnlockStartButton();
            return;
        }

        if (iceRenderer != null && frostedIceMaterial != null)
        {
            iceRenderer.sharedMaterial = frostedIceMaterial;
        }

        GameObject flashlight = Instantiate(
            flashlightPrefab,
            flashlightSpawnPoint.position,
            flashlightSpawnPoint.rotation
        );

        flashlightInteractable = flashlight.GetComponent<XRGrabInteractable>();
        if (flashlightInteractable == null)
        {
            Debug.LogError("The assigned tutorial flashlight prefab needs an XRGrabInteractable on its root.", this);
            Destroy(flashlight);
            UnlockStartButton();
            return;
        }

        flashlightBehaviours = flashlight.GetComponentsInChildren<FlashlightTutorial>(true);
        SetFlashlightPower(false);

        flashlightInteractable.selectEntered.AddListener(OnFlashlightGrabbed);
        flashlightInteractable.activated.AddListener(OnFlashlightActivated);
        flashlightInteractable.deactivated.AddListener(OnFlashlightDeactivated);

        tutorialActive = true;
        PlayerPrefs.SetInt(TutorialSeenKey, 1);
        PlayerPrefs.Save();

        SetPrompt("GRIP TO GRAB");
        CreateCrackLines();
    }

    private void Update()
    {
        if (!tutorialActive || isMelted || !hasGrabbedFlashlight || !flashlightIsOn)
        {
            return;
        }

        if (Time.time - lastFlashlightHitTime > hitTimeout)
        {
            return;
        }

        transform.localScale = Vector3.MoveTowards(
            transform.localScale,
            Vector3.zero,
            shrinkSpeed * Time.deltaTime
        );

        if (transform.localScale.x <= minimumScale)
        {
            CompleteTutorial();
        }
    }

    public override void OnFlashlightHit(TutorialItem flashlightItem)
    {
        if (tutorialActive && hasGrabbedFlashlight && flashlightIsOn)
        {
            lastFlashlightHitTime = Time.time;
        }
    }

    private void OnFlashlightGrabbed(SelectEnterEventArgs args)
    {
        hasGrabbedFlashlight = true;
        SetPrompt("TRIGGER TO TURN ON");
    }

    private void OnFlashlightActivated(ActivateEventArgs args)
    {
        if (!hasGrabbedFlashlight || isMelted)
        {
            return;
        }

        flashlightIsOn = true;
        SetFlashlightPower(true);
        SetPrompt("POINT AT ICE TO MELT");
    }

    private void OnFlashlightDeactivated(DeactivateEventArgs args)
    {
        flashlightIsOn = false;
        SetFlashlightPower(false);

        if (tutorialActive && !isMelted && hasGrabbedFlashlight)
        {
            SetPrompt("TRIGGER TO TURN ON");
        }
    }

    private void CompleteTutorial()
    {
        isMelted = true;
        tutorialActive = false;
        SetFlashlightPower(false);

        if (startButton != null)
        {
            startButton.interactable = true;
        }

        PlayerPrefs.SetInt(TutorialSeenKey, 1);
        PlayerPrefs.Save();

        SetPrompt("PRESS START TO PLAY!");
        gameObject.SetActive(false);
    }

    private void UnlockStartButton()
    {
        if (startButton != null)
        {
            startButton.interactable = true;
        }

        if (iceRenderer != null)
        {
            iceRenderer.enabled = false;
        }

        Collider iceCollider = GetComponent<Collider>();
        if (iceCollider != null)
        {
            iceCollider.enabled = false;
        }
    }

    private void SetPrompt(string message)
    {
        if (floatingHandPrompt == null)
        {
            return;
        }

        floatingHandPrompt.gameObject.SetActive(true);
        floatingHandPrompt.text = message;
    }

    private void SetFlashlightPower(bool isOn)
    {
        if (flashlightBehaviours == null)
        {
            return;
        }

        foreach (FlashlightTutorial flashlight in flashlightBehaviours)
        {
            if (flashlight == null)
            {
                continue;
            }

            if (isOn)
            {
                flashlight.TurnOn();
            }
            else
            {
                flashlight.TurnOff();
            }
        }
    }

    private void CreateCrackLines()
    {
        Shader crackShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (crackLineMaterial == null && crackShader == null)
        {
            crackShader = Shader.Find("Sprites/Default");
        }

        if (crackLineMaterial == null && crackShader != null)
        {
            generatedCrackMaterial = new Material(crackShader);
            crackLineMaterial = generatedCrackMaterial;
            Color crackColor = new Color(0.73f, 0.92f, 1f, 0.58f);

            if (crackLineMaterial.HasProperty("_BaseColor"))
            {
                crackLineMaterial.SetColor("_BaseColor", crackColor);
            }
            else if (crackLineMaterial.HasProperty("_Color"))
            {
                crackLineMaterial.SetColor("_Color", crackColor);
            }

            if (crackLineMaterial.HasProperty("_Surface"))
            {
                crackLineMaterial.SetFloat("_Surface", 1f);
                crackLineMaterial.SetFloat("_Blend", 0f);
                crackLineMaterial.SetFloat("_ZWrite", 0f);
                crackLineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                crackLineMaterial.SetOverrideTag("RenderType", "Transparent");
                crackLineMaterial.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        if (crackLineMaterial == null)
        {
            return;
        }

        if (GetComponentsInChildren<LineRenderer>(true).Length > 0)
        {
            return;
        }

        for (int i = 0; i < CrackPaths.Length; i++)
        {
            GameObject crackObject = new GameObject("Ice Crack " + (i + 1));
            crackObject.transform.SetParent(transform, false);

            LineRenderer line = crackObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = CrackPaths[i].Length;
            line.SetPositions(CrackPaths[i]);
            line.widthMultiplier = 0.004f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = crackLineMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromFlashlight();

        if (generatedCrackMaterial != null)
        {
            Destroy(generatedCrackMaterial);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromFlashlight();
        SetFlashlightPower(false);
    }

    private void UnsubscribeFromFlashlight()
    {
        if (flashlightInteractable == null)
        {
            return;
        }

        flashlightInteractable.selectEntered.RemoveListener(OnFlashlightGrabbed);
        flashlightInteractable.activated.RemoveListener(OnFlashlightActivated);
        flashlightInteractable.deactivated.RemoveListener(OnFlashlightDeactivated);
    }
}