using Unity.InferenceEngine;
using UnityEngine;

/// Kết quả nhận diện. Toạ độ theo PIXEL ảnh gốc, gốc ở góc TRÊN-trái (quy ước của YOLO).
[System.Serializable]
public struct Lab08Detection
{
    public string label;
    public float confidence;   // 0..1
    public int x1, y1, x2, y2; // góc trên-trái, góc dưới-phải
    public int CenterX => Mathf.RoundToInt((x1 + x2) * 0.5f);
    public int CenterY => Mathf.RoundToInt((y1 + y2) * 0.5f);
}

/// Chạy YOLO26n (ONNX, COCO 80 lớp) bằng Inference Engine 2.x (tên mới của Sentis).
/// Input  "images"  [1, 3, 640, 640] RGB 0..1
/// Output "output0" [1, 84, 8400]: 4 số box (cx, cy, w, h theo pixel 640) + 80 điểm lớp.
/// Chỉ xét 10 lớp động vật của COCO (chỉ số 14..23), lấy box điểm cao nhất → không cần NMS.
public class Lab08AnimalDetector : MonoBehaviour
{
    [SerializeField] ModelAsset modelAsset;
    [SerializeField] BackendType backend = BackendType.GPUCompute;
    [SerializeField, Range(0f, 1f)] float minConfidence = 0.25f;

    const int InputSize = 640;      // kích thước input của model
    const int NumAnchors = 8400;    // 80×80 + 40×40 + 20×20 ô lưới
    const int FirstAnimal = 14;     // "bird" trong danh sách COCO
    static readonly string[] Animals = { "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe" };

    Worker worker;

    void Awake() => worker = new Worker(ModelLoader.Load(modelAsset), backend);
    void OnDestroy() => worker?.Dispose();

    public bool Detect(Texture tex, out Lab08Detection result)
    {
        result = default;
        using Tensor<float> input = TextureConverter.ToTensor(tex, InputSize, InputSize, 3);
        worker.Schedule(input);
        using var output = (worker.PeekOutput() as Tensor<float>).ReadbackAndClone();
        float[] o = output.DownloadToArray(); // o[kênh * 8400 + anchor]

        int bestA = -1, bestC = -1; float best = minConfidence;
        for (int a = 0; a < NumAnchors; a++)
            for (int c = 0; c < Animals.Length; c++)
            {
                float s = o[(4 + FirstAnimal + c) * NumAnchors + a];
                if (s > best) { best = s; bestA = a; bestC = c; }
            }
        if (bestA < 0) return false;

        // Ảnh bị kéo giãn về 640×640 → nhân lại theo từng trục.
        float sx = (float)tex.width / InputSize, sy = (float)tex.height / InputSize;
        float cx = o[bestA], cy = o[NumAnchors + bestA], w = o[2 * NumAnchors + bestA], h = o[3 * NumAnchors + bestA];
        result.label = Animals[bestC];
        result.confidence = best;
        result.x1 = Mathf.Clamp(Mathf.RoundToInt((cx - w / 2) * sx), 0, tex.width);
        result.y1 = Mathf.Clamp(Mathf.RoundToInt((cy - h / 2) * sy), 0, tex.height);
        result.x2 = Mathf.Clamp(Mathf.RoundToInt((cx + w / 2) * sx), 0, tex.width);
        result.y2 = Mathf.Clamp(Mathf.RoundToInt((cy + h / 2) * sy), 0, tex.height);
        return true;
    }
}
