using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Kết quả của model, toạ độ tính theo PIXEL của ảnh gốc (gốc ở góc trên-trái).
[System.Serializable]
public struct DetectionResult
{
    public string label;
    public float confidence;   // 0..1
    public int x1, y1, x2, y2; // góc trên-trái và góc dưới-phải
    public int CenterX => Mathf.RoundToInt((x1 + x2) * 0.5f);
    public int CenterY => Mathf.RoundToInt((y1 + y2) * 0.5f);
}

/// Vẽ ảnh và bounding box theo kết quả model. Khi chưa có kết quả thì không vẽ box.
public class DetectionOverlayView : MonoBehaviour
{
    [SerializeField] RectTransform imageArea;   // vùng hiển thị (ImageArea)
    [SerializeField] RawImage datasetImage;
    [SerializeField] RectTransform boundingBox;
    [SerializeField] TextMeshProUGUI labelText;
    [SerializeField] RectTransform centerDot;
    [SerializeField] TextMeshProUGUI centerText;
    [Tooltip("Bật: ảnh lấp đầy khung, cắt bớt mép (giống ảnh mẫu). Tắt: hiện trọn ảnh, chừa viền hai bên.")]
    [SerializeField] bool fillArea = true;

    void Awake() => Clear();

    /// Hiện ảnh, giữ đúng tỉ lệ (letterbox) bên trong ImageArea.
    public void ShowImage(Texture tex)
    {
        datasetImage.texture = tex;
        datasetImage.color = Color.white;
        Rect r = FitRect(tex.width, tex.height);
        var rt = datasetImage.rectTransform;
        rt.anchoredPosition = new Vector2(r.x, -r.y);
        rt.sizeDelta = new Vector2(r.width, r.height);
    }

    /// Đặt box từ toạ độ pixel ảnh gốc sang toạ độ UI.
    public void ShowDetection(DetectionResult d, int srcW, int srcH)
    {
        Rect r = FitRect(srcW, srcH);
        float s = r.width / srcW; // hệ số pixel ảnh -> pixel UI
        boundingBox.anchoredPosition = new Vector2(r.x + d.x1 * s, -(r.y + d.y1 * s));
        boundingBox.sizeDelta = new Vector2((d.x2 - d.x1) * s, (d.y2 - d.y1) * s);
        // Chấm tâm (local trong box). Trừ 9 = nửa đường kính 18 px để chấm nằm đúng giữa.
        centerDot.anchoredPosition = new Vector2((d.CenterX - d.x1) * s - 9, -((d.CenterY - d.y1) * s - 9));
        labelText.text = $"{d.label} {d.confidence * 100f:0.0}%";
        // Nhãn co giãn theo độ dài chữ: lề trái 10 + chữ + lề phải 10
        var tag = (RectTransform)labelText.transform.parent;
        tag.sizeDelta = new Vector2(labelText.preferredWidth + 20f, tag.sizeDelta.y);
        labelText.rectTransform.sizeDelta = new Vector2(labelText.preferredWidth + 10f, labelText.rectTransform.sizeDelta.y);
        centerText.text = $"({d.CenterX}, {d.CenterY})";
        boundingBox.gameObject.SetActive(true);
    }

    public void Clear() => boundingBox.gameObject.SetActive(false);

    // Hình chữ nhật hiển thị ảnh trong imageArea (x, y tính từ góc trên-trái).
    Rect FitRect(int w, int h)
    {
        Vector2 a = imageArea.rect.size;
        float s = fillArea ? Mathf.Max(a.x / w, a.y / h) : Mathf.Min(a.x / w, a.y / h);
        float dw = w * s, dh = h * s;
        return new Rect((a.x - dw) * 0.5f, (a.y - dh) * 0.5f, dw, dh);
    }
}
