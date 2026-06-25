using UnityEngine;
using UnityEngine.InputSystem;

public class WheelSpin : MonoBehaviour
{
    [SerializeField] private float minSpinDuration = 3f;
    [SerializeField] private float maxSpinDuration = 7f;
    [SerializeField] private float minSpinSpeed = 360f;
    [SerializeField] private float maxSpinSpeed = 1080f;
    [SerializeField] private AnimationCurve spinCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private MinigameManager minigameManager;
    [SerializeField] private float autoSpinInterval = 30f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spinSound;

    private bool isSpinning = false;
    private float currentSpinTime = 0f;
    private float totalRotation = 0f;
    private float currentSpinDuration;
    private float currentSpinSpeed;
    private float autoSpinTimer = 0f;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.digit7Key.wasPressedThisFrame && !isSpinning)
        {
            StartSpin();
        }

        // Auto spin timer
        if (!isSpinning)
        {
            autoSpinTimer += Time.deltaTime;
            if (autoSpinTimer >= autoSpinInterval)
            {
                autoSpinTimer = 0f;
                StartSpin();
            }
        }

        if (isSpinning)
        {
            currentSpinTime += Time.deltaTime;
            float progress = currentSpinTime / currentSpinDuration;

            if (progress >= 1f)
            {
                isSpinning = false;
                currentSpinTime = 0f;
                CheckHighestSphere();
            }
            else
            {
                float rotationThisFrame = currentSpinSpeed * Time.deltaTime * (1f - progress);
                transform.Rotate(0, rotationThisFrame, 0);
                totalRotation += rotationThisFrame;
            }
        }
    }

    private void StartSpin()
    {
        isSpinning = true;
        currentSpinTime = 0f;
        totalRotation = 0f;

        currentSpinDuration = Random.Range(minSpinDuration, maxSpinDuration);
        currentSpinSpeed = Random.Range(minSpinSpeed, maxSpinSpeed);

        if (audioSource != null && spinSound != null)
            audioSource.PlayOneShot(spinSound);
    }

    private void CheckHighestSphere()
    {
        Transform highestSphere = null;
        float highestY = float.MinValue;

        for (int i = 1; i <= 8; i++)
        {
            string sphereName = "Sphere" + i;
            Transform sphere = transform.Find(sphereName);

            if (sphere != null)
            {
                float yPosition = sphere.position.y;

                if (yPosition > highestY)
                {
                    highestY = yPosition;
                    highestSphere = sphere;
                }
            }
        }

        if (highestSphere != null)
        {
            Debug.Log("Highest sphere: " + highestSphere.name + " at Y position: " + highestY);

            if (highestSphere.name == "Sphere1" || highestSphere.name == "Sphere5")
            {
                if (minigameManager != null)
                {
                    minigameManager.StartMinigame();
                }
                else
                {
                    Debug.LogWarning("MinigameManager reference not set on WheelSpin!");
                }
            }

            if (highestSphere.name == "Sphere3" || highestSphere.name == "Sphere7")
            {
                if (minigameManager != null)
                {
                    minigameManager.TriggerLift();
                }
                else
                {
                    Debug.LogWarning("MinigameManager reference not set on WheelSpin!");
                }
            }
            if (highestSphere.name == "Sphere4" || highestSphere.name == "Sphere8")
            {
                if (minigameManager != null)
                {
                    minigameManager.SpawnSingleGun();
                }
                else
                {
                    Debug.LogWarning("MinigameManager reference not set on WheelSpin!");
                }
            }
        }
        
        else
        {
            Debug.LogWarning("No spheres found as children!");
        }
    }
}