using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Lab 06 - Cham vao hinh da dat roi keo de di chuyen tren plane.
[RequireComponent(typeof(ARRaycastManager))]
public class ARShapeDragger : MonoBehaviour
{
    public Camera arCamera;
    [Tooltip("Phong to khi dang keo de nguoi dung biet hinh nao dang duoc chon")]
    public float dragScaleBoost = 1.15f;

    ARRaycastManager raycastManager;
    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    Transform dragged;
    Vector3 originalScale;

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        if (arCamera == null) arCamera = Camera.main;
    }

    // Doc trang thai con tro: dang giu hay khong, vi tri, va vua nhan / vua tha.
    static bool ReadPointer(out Vector2 pos, out bool pressed, out bool held, out bool released)
    {
        pos = Vector2.zero; pressed = held = released = false;
        var ts = Touchscreen.current;
        if (ts != null && (ts.primaryTouch.press.isPressed || ts.primaryTouch.press.wasReleasedThisFrame))
        {
            var t = ts.primaryTouch;
            pos = t.position.ReadValue(); pressed = t.press.wasPressedThisFrame;
            held = t.press.isPressed; released = t.press.wasReleasedThisFrame; return true;
        }
        var m = Mouse.current;
        if (m != null)
        {
            pos = m.position.ReadValue(); pressed = m.leftButton.wasPressedThisFrame;
            held = m.leftButton.isPressed; released = m.leftButton.wasReleasedThisFrame; return true;
        }
        return false;
    }

    void Update()
    {
        Vector2 pos; bool pressed, held, released;
        if (!ReadPointer(out pos, out pressed, out held, out released)) return;

        if (pressed && !ARShapePlacer.IsOverUI(pos)) TryPick(pos);

        if (dragged != null && held)
        {
            // Hinh chi di chuyen tren plane -> dung AR raycast, khong dung Physics.
            if (raycastManager.Raycast(pos, hits, TrackableType.PlaneWithinPolygon))
                dragged.position = hits[0].pose.position;
        }

        if (released) Drop();
    }

    void TryPick(Vector2 pos)
    {
        if (arCamera == null) return;
        RaycastHit hit;
        if (!Physics.Raycast(arCamera.ScreenPointToRay(pos), out hit)) return;
        var shape = hit.collider.GetComponentInParent<PlacedShape>();
        if (shape == null) return;
        dragged = shape.transform;
        originalScale = dragged.localScale;
        dragged.localScale = originalScale * dragScaleBoost;
    }

    void Drop()
    {
        if (dragged == null) return;
        dragged.localScale = originalScale;
        dragged = null;
    }
}
