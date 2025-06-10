using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class TriggerMovementBlocker : MonoBehaviour
{
    public ContinuousMoveProvider moveProvider;
    public InputActionReference rightTriggerAction;
    public GameObject mapCanvas;

    private void Update()
    {
        float triggerValue = rightTriggerAction.action.ReadValue<float>();
        bool isHoldingTrigger = triggerValue > 0.1f;
        moveProvider.enabled = !isHoldingTrigger;
        if (mapCanvas != null )
        { mapCanvas.SetActive(isHoldingTrigger); 
            }
    }
}