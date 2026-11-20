using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XRMultiplayer
{
    public class OfflineControllerHandPose : MonoBehaviour
    {
        [SerializeField] JointBasedHand m_LeftHand;
        [SerializeField] JointBasedHand m_RightHand;
        [SerializeField] InputActionReference m_LeftGripInput;
        [SerializeField] InputActionReference m_RightGripInput;
        [SerializeField] InputActionReference m_LeftTriggerInput;
        [SerializeField] InputActionReference m_RightTriggerInput;
        [SerializeField] InputActionReference m_LeftThumbTouchInput;
        [SerializeField] InputActionReference m_RightThumbTouchInput;
        bool m_ReportedDisabledInput;

        void Awake()
        {
            if (m_LeftHand == null || m_RightHand == null)
                throw new InvalidOperationException("Offline controller hand references are not configured.");

            ValidateInputAction(m_LeftGripInput, nameof(m_LeftGripInput));
            ValidateInputAction(m_RightGripInput, nameof(m_RightGripInput));
            ValidateInputAction(m_LeftTriggerInput, nameof(m_LeftTriggerInput));
            ValidateInputAction(m_RightTriggerInput, nameof(m_RightTriggerInput));
            ValidateInputAction(m_LeftThumbTouchInput, nameof(m_LeftThumbTouchInput));
            ValidateInputAction(m_RightThumbTouchInput, nameof(m_RightThumbTouchInput));
        }

        void Update()
        {
            var leftUpdated = UpdateHand(
                m_LeftHand,
                m_LeftThumbTouchInput,
                m_LeftTriggerInput,
                m_LeftGripInput,
                out var leftDisabledInput);
            var rightUpdated = UpdateHand(
                m_RightHand,
                m_RightThumbTouchInput,
                m_RightTriggerInput,
                m_RightGripInput,
                out var rightDisabledInput);

            if (!leftUpdated || !rightUpdated)
            {
                if (!m_ReportedDisabledInput)
                {
                    Debug.LogWarning(
                        $"Offline hand pose is waiting for enabled controller input '{leftDisabledInput ?? rightDisabledInput}'.",
                        this);
                    m_ReportedDisabledInput = true;
                }
                return;
            }

            m_ReportedDisabledInput = false;
        }

        static bool UpdateHand(
            JointBasedHand hand,
            InputActionReference thumbTouchInput,
            InputActionReference triggerInput,
            InputActionReference gripInput,
            out string disabledInputName)
        {
            disabledInputName = null;
            if (!hand.gameObject.activeInHierarchy)
                return true;

            if (!TryReadValue(thumbTouchInput, out var thumbTouch))
            {
                disabledInputName = thumbTouchInput.action.name;
                return false;
            }
            if (!TryReadValue(triggerInput, out var trigger))
            {
                disabledInputName = triggerInput.action.name;
                return false;
            }
            if (!TryReadValue(gripInput, out var grip))
            {
                disabledInputName = gripInput.action.name;
                return false;
            }

            hand.fidelityLevel = 2;
            hand.useCurl = true;
            hand.SetCurl(0, thumbTouch);
            hand.SetCurl(1, trigger);
            hand.SetCurl(2, grip);
            return true;
        }

        static bool TryReadValue(InputActionReference inputActionReference, out float value)
        {
            if (!inputActionReference.action.enabled)
            {
                value = 0f;
                return false;
            }

            value = inputActionReference.action.ReadValue<float>();
            return true;
        }

        static void ValidateInputAction(InputActionReference inputActionReference, string fieldName)
        {
            if (inputActionReference == null || inputActionReference.action == null)
                throw new InvalidOperationException($"Offline controller hand input reference '{fieldName}' is not configured.");
        }
    }
}
