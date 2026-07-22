using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renders the player in an overlay camera after the base camera's post processing.
/// The player therefore stays visible but is not included in bloom.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class PlayerBloomExclusion : MonoBehaviour
{
    private const int PlayerNoBloomLayer = 7;

    [SerializeField] private Camera baseCamera;

    private Camera overlayCamera;
    private UniversalAdditionalCameraData baseCameraData;

    private void Awake()
    {
        SetLayerRecursively(transform, PlayerNoBloomLayer);
    }

    private void Start()
    {
        SetupOverlayCamera();
    }

    private void LateUpdate()
    {
        SyncOverlayCamera();
    }

    private void OnDestroy()
    {
        if (baseCameraData != null && overlayCamera != null)
            baseCameraData.cameraStack.Remove(overlayCamera);

        if (baseCamera != null)
            baseCamera.cullingMask |= 1 << PlayerNoBloomLayer;

        if (overlayCamera != null)
            Destroy(overlayCamera.gameObject);
    }

    private void SetupOverlayCamera()
    {
        if (baseCamera == null)
            baseCamera = Camera.main;

        if (baseCamera == null)
        {
            Debug.LogWarning("PlayerBloomExclusion could not find the Main Camera.", this);
            return;
        }

        baseCameraData = baseCamera.GetComponent<UniversalAdditionalCameraData>();
        if (baseCameraData == null || baseCameraData.renderType != CameraRenderType.Base)
        {
            Debug.LogWarning("PlayerBloomExclusion requires a URP Base Camera.", this);
            return;
        }

        GameObject overlayObject = new("Player No Bloom Overlay Camera")
        {
            hideFlags = HideFlags.DontSave
        };
        overlayObject.transform.SetParent(baseCamera.transform, false);

        overlayCamera = overlayObject.AddComponent<Camera>();
        UniversalAdditionalCameraData overlayData = overlayObject.AddComponent<UniversalAdditionalCameraData>();
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderPostProcessing = false;

        overlayCamera.cullingMask = 1 << PlayerNoBloomLayer;
        overlayCamera.clearFlags = CameraClearFlags.Nothing;
        baseCamera.cullingMask &= ~(1 << PlayerNoBloomLayer);
        baseCameraData.cameraStack.Add(overlayCamera);
        SyncOverlayCamera();
    }

    private void SyncOverlayCamera()
    {
        if (baseCamera == null || overlayCamera == null)
            return;

        overlayCamera.orthographic = baseCamera.orthographic;
        overlayCamera.orthographicSize = baseCamera.orthographicSize;
        overlayCamera.fieldOfView = baseCamera.fieldOfView;
        overlayCamera.nearClipPlane = baseCamera.nearClipPlane;
        overlayCamera.farClipPlane = baseCamera.farClipPlane;
        overlayCamera.rect = baseCamera.rect;
        overlayCamera.aspect = baseCamera.aspect;
    }

    private static void SetLayerRecursively(Transform target, int layer)
    {
        target.gameObject.layer = layer;
        for (int i = 0; i < target.childCount; i++)
            SetLayerRecursively(target.GetChild(i), layer);
    }
}