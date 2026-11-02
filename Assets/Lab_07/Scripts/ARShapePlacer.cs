using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Lab 06 - Cham len plane de dat hinh dang duoc chon.
[RequireComponent(typeof(ARRaycastManager))]
public class ARShapePlacer : MonoBehaviour
{
    [Tooltip("4 prefab: Torus, Cone, TriangularPrism, Dodecahedron")]
    public GameObject[] shapePrefabs;
    public int selectedIndex = 0;
    public Camera arCamera;

    ARRaycastManager raycastManager;
    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    public readonly List<GameObject> placed = new List<GameObject>();

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        if (arCamera == null) arCamera = Camera.main;
    }

    public void SelectShape(int index)
    {
        if (shapePrefabs == null || index < 0 || index >= shapePrefabs.Length) return;
        selectedIndex = index;
    }

    // Tra ve true neu vi tri man hinh nam tren UI (nut menu) -> khong dat hinh.
    public static bool IsOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        return results.Count > 0;
    }

    // Lay vi tri cham trong frame vua nhan (touch tren dien thoai, chuot trong Editor).
    public static bool TryGetPress(out Vector2 pos)
    {
        pos = Vector2.zero;
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { pos = ts.primaryTouch.position.ReadValue(); return true; }
        var m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) { pos = m.position.ReadValue(); return true; }
        return false;
    }

    void Update()
    {
        Vector2 pos;
        if (!TryGetPress(out pos)) return;
        if (IsOverUI(pos)) return;
        // Cham trung hinh da dat -> de ARShapeDragger xu ly, khong dat them.
        if (arCamera != null)
        {
            RaycastHit ph;
            if (Physics.Raycast(arCamera.ScreenPointToRay(pos), out ph) && ph.collider.GetComponentInParent<PlacedShape>() != null) return;
        }
        if (!raycastManager.Raycast(pos, hits, TrackableType.PlaneWithinPolygon)) return;
        Place(hits[0].pose);
    }

    public GameObject Place(Pose pose)
    {
        if (shapePrefabs == null || shapePrefabs.Length == 0) return null;
        var prefab = shapePrefabs[selectedIndex];
        // Xoay hinh quay mat ve phia camera (chi xoay quanh truc Y de van dung thang tren plane).
        Quaternion rot = pose.rotation;
        if (arCamera != null)
        {
            Vector3 dir = arCamera.transform.position - pose.position; dir.y = 0;
            if (dir.sqrMagnitude > 0.0001f) rot = Quaternion.LookRotation(dir);
        }
        var go = Instantiate(prefab, pose.position, rot);
        if (go.GetComponent<PlacedShape>() == null) go.AddComponent<PlacedShape>();
        placed.Add(go);
        return go;
    }
}

// Danh dau object do nguoi dung dat (dung cho CP5 keo tha).
public class PlacedShape : MonoBehaviour { }
