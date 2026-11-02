using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Điều khiển viewer: nạp ảnh → chạy model → cập nhật UI → gửi Adafruit.
public class JacquardViewerController : MonoBehaviour
{
    [Header("Dữ liệu")]
    [Tooltip("Đường dẫn tương đối tính từ StreamingAssets (đi kèm package), hoặc đường dẫn tuyệt đối. Ảnh tìm đệ quy theo *_RGB.png")]
    public string datasetFolder = "Jacquard";
    [Tooltip("Số ảnh tối đa đưa vào viewer (0 = lấy tất cả)")]
    public int maxImages = 20;

    [Header("Thành phần")]
    [SerializeField] JacquardDetector detector;
    [SerializeField] DetectionOverlayView overlay;
    [SerializeField] AdafruitClient adafruit;
    [SerializeField] Button prevButton, nextButton;
    [SerializeField] TextMeshProUGUI counterText;
    [SerializeField] TextMeshProUGUI typeValue, confidenceValue, bboxValue, centerValue;
    [SerializeField] TextMeshProUGUI[] tableValues = new TextMeshProUGUI[6]; // x1, y1, x2, y2, cx, cy
    [SerializeField] RawImage[] thumbs = new RawImage[5];
    [SerializeField] Image[] thumbFrames = new Image[5];
    [SerializeField] TextMeshProUGUI[] thumbLabels = new TextMeshProUGUI[5]; // số thứ tự dưới mỗi thumbnail

    [Header("Adafruit")]
    [Tooltip("Cạnh dài của ảnh gửi lên. Feed ảnh giới hạn khoảng 100 KB, 480 px JPEG đủ nhỏ.")]
    public int uploadSize = 480;
    [Range(10, 100)] public int jpegQuality = 60;

    static readonly Color FrameNormal = Color.white;
    static readonly Color FrameActive = new Color32(0x4A, 0x5A, 0x8C, 0xFF);
    static readonly Color BoxColor = new Color32(0x4A, 0x5A, 0x8C, 0xFF);

    List<string> files = new();
    readonly Dictionary<int, Texture2D> thumbCache = new();
    Texture2D current;
    int index;

    void Start()
    {
        string folder = Path.IsPathRooted(datasetFolder) ? datasetFolder : Path.Combine(Application.streamingAssetsPath, datasetFolder);
        if (Directory.Exists(folder))
            files = Directory.GetFiles(folder, "*_RGB.png", SearchOption.AllDirectories).OrderBy(f => f).ToList();
        if (maxImages > 0 && files.Count > maxImages) files = files.Take(maxImages).ToList();
        if (files.Count == 0) { Debug.LogError($"[Viewer] Không có ảnh *_RGB.png trong {folder}"); return; }

        prevButton.onClick.AddListener(() => Step(-1));
        nextButton.onClick.AddListener(() => Step(1));
        if (adafruit != null) adafruit.OnRemoteStep += Step;
        Show(0);
    }

    public void Step(int dir) => Show((index + dir + files.Count) % files.Count); // vòng lại đầu/cuối

    void Show(int i)
    {
        index = i;
        if (current != null) Destroy(current);
        current = LoadPng(files[i]);
        overlay.ShowImage(current);
        counterText.text = $"Image {i + 1} / {files.Count}";
        UpdateThumbs();

        if (detector.Detect(current, out var d))
        {
            overlay.ShowDetection(d, current.width, current.height);
            Fill(d.label, $"{d.confidence * 100f:0.0}%", $"({d.x1}, {d.y1}, {d.x2}, {d.y2})", $"({d.CenterX}, {d.CenterY})",
                 new[] { d.x1, d.y1, d.x2, d.y2, d.CenterX, d.CenterY }.Select(v => v.ToString()).ToArray());
            Publish(d, true);
        }
        else
        {
            overlay.Clear();
            Fill("No object", "—", "—", "—", Enumerable.Repeat("—", 6).ToArray());
            Publish(d, false);
        }
    }

    void Fill(string type, string conf, string bbox, string center, string[] table)
    {
        typeValue.text = type; confidenceValue.text = conf; bboxValue.text = bbox; centerValue.text = center;
        for (int k = 0; k < tableValues.Length; k++) tableValues[k].text = table[k];
    }

    // Dải thumbnail: 5 ảnh quanh ảnh hiện tại, ảnh hiện tại nằm ở ô giữa (ô số 2).
    void UpdateThumbs()
    {
        for (int k = 0; k < thumbs.Length; k++)
        {
            int fi = (index + k - 2 + files.Count) % files.Count;
            if (!thumbCache.TryGetValue(fi, out var t))
            {
                var full = fi == index ? current : LoadPng(files[fi]);
                t = Resize(full, 128);
                if (full != current) Destroy(full);
                thumbCache[fi] = t;
            }
            thumbs[k].texture = t; thumbs[k].color = Color.white;
            thumbFrames[k].color = k == 2 ? FrameActive : FrameNormal;
            if (thumbLabels.Length > k && thumbLabels[k] != null) thumbLabels[k].text = (fi + 1).ToString(); // đánh số từ 1
        }
    }

    void Publish(DetectionResult d, bool found)
    {
        if (adafruit == null || !adafruit.Ready) return;
        adafruit.Send(adafruit.confidenceFeed, found ? (d.confidence * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "0");
        var img = Resize(current, uploadSize);
        if (found) DrawBox(img, d, (float)img.width / current.width);
        adafruit.Send(adafruit.imageFeed, System.Convert.ToBase64String(img.EncodeToJPG(jpegQuality)));
        Destroy(img);
    }

    // ===== Tiện ích ảnh =====
    static Texture2D LoadPng(string path)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(path)); // giữ nguyên kích thước gốc, ví dụ 1024×1024
        return t;
    }

    /// Thu nhỏ bằng GPU, cạnh dài = maxSide, giữ tỉ lệ.
    static Texture2D Resize(Texture2D src, int maxSide)
    {
        float s = Mathf.Min(1f, (float)maxSide / Mathf.Max(src.width, src.height));
        int w = Mathf.Max(1, Mathf.RoundToInt(src.width * s)), h = Mathf.Max(1, Mathf.RoundToInt(src.height * s));
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var dst = new Texture2D(w, h, TextureFormat.RGB24, false);
        dst.ReadPixels(new Rect(0, 0, w, h), 0, 0); dst.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        return dst;
    }

    /// Vẽ khung lên ảnh gửi Adafruit. Texture có gốc ở góc DƯỚI-trái, nên phải lật trục y.
    static void DrawBox(Texture2D img, DetectionResult d, float s, int thick = 3)
    {
        int x1 = (int)(d.x1 * s), x2 = (int)(d.x2 * s) - 1;
        int yTop = img.height - 1 - (int)(d.y1 * s), yBot = img.height - (int)(d.y2 * s);
        for (int t = 0; t < thick; t++)
        {
            for (int x = x1; x <= x2; x++) { Px(img, x, yTop - t); Px(img, x, yBot + t); }
            for (int y = yBot; y <= yTop; y++) { Px(img, x1 + t, y); Px(img, x2 - t, y); }
        }
        img.Apply();
    }
    static void Px(Texture2D t, int x, int y) { if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, BoxColor); }
}
