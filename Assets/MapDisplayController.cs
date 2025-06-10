using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MapDisplayController : MonoBehaviour
{
    public InputActionReference rightTriggerAction;
    public RectTransform playerIcon; // Drag your green dot icon here

    private Canvas mapCanvas;

    void Start()
    {
        mapCanvas = GetComponent<Canvas>();

        // Optional: Hide icon initially
        if (playerIcon != null)
            playerIcon.gameObject.SetActive(false);
    }

    void Update()
    {
        float triggerValue = rightTriggerAction.action.ReadValue<float>();
        bool triggerHeld = triggerValue > 0.1f;

        // Toggle map visibility
        mapCanvas.enabled = triggerHeld;

        // Toggle icon visibility along with the map
        if (playerIcon != null)
            playerIcon.gameObject.SetActive(triggerHeld);
    }
}

