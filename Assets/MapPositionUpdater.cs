using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapPositionUpdater : MonoBehaviour
{
    public Transform xrOrigin;      // Your XR Origin (root position)
    public RectTransform mapRect;   // Map image RectTransform
    public Vector2 mapSizeWorld = new Vector2(100f, 100f); // World area shown on the map

    public RectTransform viewCone;       // View cone UI element
    public Transform xrHeadTransform;    // Headset/camera (typically Main Camera)

    private RectTransform playerIcon;

    void Start()
    {
        playerIcon = GetComponent<RectTransform>();
    }

    void Update()
    {
        viewCone.anchoredPosition = playerIcon.anchoredPosition;

        Vector3 playerPos = xrOrigin.position;

        // Normalize world position to 0-1
        float normalizedX = Mathf.InverseLerp(-mapSizeWorld.x / 2, mapSizeWorld.x / 2, playerPos.x);
        float normalizedZ = Mathf.InverseLerp(-mapSizeWorld.y / 2, mapSizeWorld.y / 2, playerPos.z);

        // Optional: Flip X or Z depending on map orientation
        normalizedX = 1f - normalizedX;
        normalizedZ = 1f - normalizedZ;

        // Convert to map UI coordinates
        float iconX = (normalizedX - 0.5f) * mapRect.rect.width;
        float iconY = (normalizedZ - 0.5f) * mapRect.rect.height;

        playerIcon.anchoredPosition = new Vector2(iconX, iconY);

        // Update view cone rotation to match headset forward direction
        Vector3 forward = xrHeadTransform.forward;
        forward.y = 0f; // Ignore vertical look angle
        forward.Normalize();

        float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        viewCone.localEulerAngles = new Vector3(0, 0, -angle); // Z-axis for UI rotation
    }
}

