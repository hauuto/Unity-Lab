using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Câu 2c, 2d – Cảm biến ở đầu gripper phát một tia theo trục +Y local (hướng mũi kẹp).
/// Tia chạm vật: hiện khoảng cách vật–cảm biến, vật đổi đỏ.
/// Không chạm: hiện "None", vật trả lại màu cũ.
/// Quy ước dự án: 1 unit = 10 cm → khoảng cách (cm) = unit × 10.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class SensorRay : MonoBehaviour
{
    [Tooltip("Chiều dài tối đa của tia (unit). 30 unit = 3 m")]
    public float maxDistance = 30f;
    [Tooltip("Chỉ phát hiện các layer này (sàn để ở Ignore Raycast)")]
    public LayerMask detectMask = ~0;
    [Tooltip("Text hiển thị khoảng cách trên màn hình")]
    public TMP_Text distanceText;
    [Tooltip("Material đỏ gán cho vật bị tia chiếu")]
    public Material hitMaterial;
    [Tooltip("Đổi unit sang cm (1 unit = 10 cm)")]
    public float cmPerUnit = 10f;

    LineRenderer line;
    Renderer lastHit;
    readonly Dictionary<Renderer, Material[]> original = new Dictionary<Renderer, Material[]>();

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
    }

    void Update()
    {
        Vector3 origin = transform.position;
        Vector3 dir = transform.up;
        Renderer hitRenderer = null;
        Vector3 end = origin + dir * maxDistance;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, detectMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            hitRenderer = hit.collider.GetComponent<Renderer>();
            if (distanceText) distanceText.text = $"Khoảng cách: {hit.distance * cmPerUnit:0.0} cm";
        }
        else if (distanceText)
        {
            distanceText.text = "None";
        }

        line.SetPosition(0, origin);
        line.SetPosition(1, end);

        if (hitRenderer != lastHit)
        {
            Restore(lastHit);
            Paint(hitRenderer);
            lastHit = hitRenderer;
        }
    }

    void Paint(Renderer r)
    {
        if (r == null || hitMaterial == null) return;
        if (!original.ContainsKey(r)) original[r] = r.sharedMaterials;
        var mats = new Material[r.sharedMaterials.Length];
        for (int i = 0; i < mats.Length; i++) mats[i] = hitMaterial;
        r.sharedMaterials = mats;
    }

    void Restore(Renderer r)
    {
        if (r == null) return;
        Material[] m;
        if (original.TryGetValue(r, out m)) r.sharedMaterials = m;
    }

    void OnDisable() { Restore(lastHit); lastHit = null; }
}
