using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Lab 05 - Hien thi bounding box: co gian khoi con 'Visual' (Cube 1x1x1) theo kich thuoc that
/// ma ARBoundingBox bao ve. Gan vao root cua prefab ARBoundingBox_Cyan.
/// </summary>
[RequireComponent(typeof(ARBoundingBox))]
public class ARBoundingBoxVisualizer : MonoBehaviour
{
    [SerializeField] Transform visual;   // Cube 1x1x1, tam o goc cua prefab
    ARBoundingBox box;

    void Awake() => box = GetComponent<ARBoundingBox>();

    void Update()
    {
        if (visual != null) visual.localScale = box.size;  // size tinh bang met
    }
}
