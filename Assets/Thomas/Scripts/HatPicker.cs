using UnityEngine;
using Unity.Netcode;

namespace XRMultiplayer
{
    public class HatPicker : NetworkBehaviour
    {
        [Header("Hat References")]
        [SerializeField, Tooltip("Array of 12 hat GameObjects")]
        GameObject[] hats = new GameObject[12];

        // Network variable to sync the current hat across all clients
        NetworkVariable<int> currentHatIndex = new NetworkVariable<int>(
            -1, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Owner
        );

        void Start()
        {
            // Subscribe to hat index changes
            currentHatIndex.OnValueChanged += OnHatChanged;
            
            // Initialize hats (all inactive by default)
            DeactivateAllHats();
            
            // Set initial hat if this is the local player
            if (IsOwner && currentHatIndex.Value == -1)
            {
                currentHatIndex.Value = 0;
            }
        }

        void OnHatChanged(int previousValue, int newValue)
        {
            UpdateHatVisibility(newValue);
        }

        void UpdateHatVisibility(int activeIndex)
        {
            DeactivateAllHats();

            if (activeIndex >= 0 && activeIndex < hats.Length && hats[activeIndex] != null)
            {
                hats[activeIndex].SetActive(true);
            }
        }

        void DeactivateAllHats()
        {
            foreach (var hat in hats)
            {
                if (hat != null)
                    hat.SetActive(false);
            }
        }

        /// <summary>
        /// Cycles to the next hat in the array.
        /// Can be called from UI buttons or inspector.
        /// </summary>
        public void NextHat()
        {
            if (!IsOwner) return;
            if (hats.Length == 0) return;

            int newIndex = currentHatIndex.Value + 1;
            if (newIndex >= hats.Length)
                newIndex = 0;

            currentHatIndex.Value = newIndex;
        }

        /// <summary>
        /// Cycles to the previous hat in the array.
        /// Can be called from UI buttons or inspector.
        /// </summary>
        public void PreviousHat()
        {
            if (!IsOwner) return;
            if (hats.Length == 0) return;

            int newIndex = currentHatIndex.Value - 1;
            if (newIndex < 0)
                newIndex = hats.Length - 1;

            currentHatIndex.Value = newIndex;
        }

        /// <summary>
        /// Sets a specific hat by index.
        /// Can be called from UI buttons or inspector.
        /// </summary>
        /// <param name="index">Index of the hat to activate (0-11)</param>
        public void SetHat(int index)
        {
            if (!IsOwner) return;

            if (index >= 0 && index < hats.Length)
            {
                currentHatIndex.Value = index;
            }
        }

        void OnDestroy()
        {
            currentHatIndex.OnValueChanged -= OnHatChanged;
        }
    }
}