using Unity.InferenceEngine;
using UnityEngine;

/// Chạy YOLO (ONNX, xuất từ Ultralytics) bằng Inference Engine 2.x.
/// Input  "images"  [1, 3, 640, 640]  RGB 0..1
/// Output "output0" [1, 84, 8400]     4 số box (cx, cy, w, h theo pixel 640) + 80 điểm lớp COCO
/// Giao diện chỉ hiện 1 object → lấy box có điểm cao nhất, không cần NMS.
public class JacquardDetector : MonoBehaviour
{
    [SerializeField] ModelAsset modelAsset;
    [SerializeField] BackendType backend = BackendType.GPUCompute;
    [SerializeField, Range(0f, 1f)] float minConfidence = 0.25f;
    [Tooltip("Chỉ xét các lớp này (tên COCO). Để trống = xét cả 80 lớp.")]
    public string[] allowedClasses = new string[0];

    const int InputSize = 640;
    const int NumAnchors = 8400;
    
    // Tên lớp COCO, lấy đúng thứ tự trong metadata "names" của file ONNX.
    static readonly string[] Names = {
        "person","bicycle","car","motorcycle","airplane","bus","train","truck","boat","traffic light",
        "fire hydrant","stop sign","parking meter","bench","bird","cat","dog","horse","sheep","cow",
        "elephant","bear","zebra","giraffe","backpack","umbrella","handbag","tie","suitcase","frisbee",
        "skis","snowboard","sports ball","kite","baseball bat","baseball glove","skateboard","surfboard","tennis racket","bottle",
        "wine glass","cup","fork","knife","spoon","bowl","banana","apple","sandwich","orange",
        "broccoli","carrot","hot dog","pizza","donut","cake","chair","couch","potted plant","bed",
        "dining table","toilet","tv","laptop","mouse","remote","keyboard","cell phone","microwave","oven",
        "toaster","sink","refrigerator","book","clock","vase","scissors","teddy bear","hair drier","toothbrush" };

    Worker worker;

    void Awake() => worker = new Worker(ModelLoader.Load(modelAsset), backend);
    void OnDestroy() => worker?.Dispose();

    /// Trả về true nếu tìm thấy object có điểm >= minConfidence.
    /// Ảnh được kéo giãn về 640×640, nên toạ độ được nhân lại theo hai trục riêng.
    public bool Detect(Texture tex, out DetectionResult result)
    {
        result = default;
        using Tensor<float> input = TextureConverter.ToTensor(tex, InputSize, InputSize, 3);
        worker.Schedule(input);
        using var output = (worker.PeekOutput() as Tensor<float>).ReadbackAndClone();
        float[] o = output.DownloadToArray(); // chỉ số: o[kênh * 8400 + anchor]
        // Số lớp đọc từ shape output [1, 4 + số lớp, 8400]:
        // model COCO = 80 lớp, model fine-tune Jacquard 1 lớp "object" = 1.
        int NumClasses = output.shape[1] - 4;

        // Lọc lớp: chỉ giữ những lớp có trong allowedClasses (nếu được đặt)
        bool[] allow = new bool[NumClasses];
        for (int c = 0; c < NumClasses; c++)
            allow[c] = allowedClasses == null || allowedClasses.Length == 0 || NumClasses != Names.Length
                       || System.Array.IndexOf(allowedClasses, Names[c]) >= 0;

        int bestA = -1, bestC = -1; float best = minConfidence;
        for (int a = 0; a < NumAnchors; a++)
            for (int c = 0; c < NumClasses; c++)
            {
                if (!allow[c]) continue;
                float s = o[(4 + c) * NumAnchors + a];
                if (s > best) { best = s; bestA = a; bestC = c; }
            }
        if (bestA < 0) return false;

        float sx = (float)tex.width / InputSize, sy = (float)tex.height / InputSize;
        float cx = o[0 * NumAnchors + bestA], cy = o[1 * NumAnchors + bestA];
        float w = o[2 * NumAnchors + bestA], h = o[3 * NumAnchors + bestA];
        result.label = NumClasses == Names.Length ? Names[bestC] : (NumClasses == 1 ? "object" : $"class {bestC}");
        result.confidence = best;
        result.x1 = Mathf.Clamp(Mathf.RoundToInt((cx - w / 2) * sx), 0, tex.width);
        result.y1 = Mathf.Clamp(Mathf.RoundToInt((cy - h / 2) * sy), 0, tex.height);
        result.x2 = Mathf.Clamp(Mathf.RoundToInt((cx + w / 2) * sx), 0, tex.width);
        result.y2 = Mathf.Clamp(Mathf.RoundToInt((cy + h / 2) * sy), 0, tex.height);
        return true;
    }
}
