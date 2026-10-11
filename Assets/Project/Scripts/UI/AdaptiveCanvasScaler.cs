using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasScaler))]
public class AdaptiveCanvasScaler : MonoBehaviour
{
    private CanvasScaler canvasScaler;
    private int screenWidth;
    private int screenHeight;

    private void Awake()
    {
        canvasScaler = GetComponent<CanvasScaler>();
        Apply();
    }

    private void Update()
    {
        if (screenWidth != Screen.width || screenHeight != Screen.height)
            Apply();
    }

    private void Apply()
    {
        screenWidth = Screen.width;
        screenHeight = Screen.height;
        if (screenWidth <= 0 || screenHeight <= 0) return;

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        Vector2 reference = canvasScaler.referenceResolution;
        if (reference.x <= 0 || reference.y <= 0) return;

        // Fit the shorter dimension so a 16:9 composition does not crop on wider or taller screens.
        canvasScaler.matchWidthOrHeight =
            (float)screenWidth / screenHeight >= reference.x / reference.y ? 1f : 0f;
    }
}
