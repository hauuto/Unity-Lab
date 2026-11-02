using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Lab 05 - Cham vao plane da phat hien de dat robot len tren plane.
/// Gan vao XR Origin (cung object voi ARRaycastManager).
/// Cham lan dau: tao robot. Cham lan sau: di chuyen robot toi vi tri moi.
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class ARTapToPlace : MonoBehaviour
{
    [Tooltip("Prefab robot se dat len plane")]
    [SerializeField] GameObject robotPrefab;

    [Tooltip("Chi raycast vao phan ben trong da giac cua plane")]
    [SerializeField] TrackableType hitType = TrackableType.PlaneWithinPolygon;

    [Tooltip("true = moi lan cham tao them robot moi; false = di chuyen robot cu")]
    [SerializeField] bool spawnMultiple = false;

    ARRaycastManager raycastManager;
    GameObject spawned;
    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Awake() => raycastManager = GetComponent<ARRaycastManager>();

    void Update()
    {
        if (!TryGetTapPosition(out Vector2 screenPos)) return;
        if (IsPointerOverUI(screenPos)) return;   // cham vao nut UI thi khong dat robot
        TryPlaceAt(screenPos);
    }

    /// Raycast tu diem man hinh xuong plane; dat/di chuyen robot. Tra ve true neu trung plane.
    public bool TryPlaceAt(Vector2 screenPos)
    {
        if (!raycastManager.Raycast(screenPos, hits, hitType)) return false;

        // hits[0] la diem cham gan camera nhat
        Pose pose = hits[0].pose;

        if (spawned == null || spawnMultiple)
            spawned = Instantiate(robotPrefab, pose.position, pose.rotation);
        else
            spawned.transform.SetPositionAndRotation(pose.position, pose.rotation);

        FaceCamera(spawned.transform);
        return true;
    }

    /// Chi xoay quanh truc Y de robot quay mat (+Z) ve phia nguoi choi, khong nghieng.
    void FaceCamera(Transform t)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 dir = cam.transform.position - t.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(dir);
    }

    static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    /// Kiem tra diem cham co nam tren mot phan tu UI (Button...) khong.
    static bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = screenPos };
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);
        return uiHits.Count > 0;
    }

    /// Lay vi tri cham tren dien thoai, hoac click chuot khi test trong Editor (XR Simulation).
    static bool TryGetTapPosition(out Vector2 pos)
    {
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
        {
            pos = ts.primaryTouch.position.ReadValue();
            return true;
        }
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            pos = mouse.position.ReadValue();
            return true;
        }
        pos = default;
        return false;
    }
}
