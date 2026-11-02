using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Real-Time Visual Dataset Explorer:
/// nạp ảnh → chạy model → hiện nhãn + box + toạ độ → Previous/Next → Category gửi MQTT.
public class Lab08ExplorerController : MonoBehaviour
{
    [Header("Dữ liệu")]
    [Tooltip("Thư mục con trong StreamingAssets (đi kèm khi build/export package)")]
    public string datasetFolder = "Lab08_Animals";

    [Header("Thành phần")]
    [SerializeField] Lab08AnimalDetector detector;
    [SerializeField] Lab08MqttPublisher mqtt;
    [SerializeField] RectTransform imageArea;   // khung cố định chứa ảnh
    [SerializeField] RawImage datasetImage;
    [SerializeField] RectTransform boundingBox;
    [SerializeField] TextMeshProUGUI labelText, coordText, counterText, statusText;
    [SerializeField] Button prevButton, nextButton, categoryButton;

    [Header("Gửi MQTT")]
    [Tooltip("Tắt: chỉ gửi khi bấm Category. Bật: gửi mỗi lần đổi ảnh (dễ vượt giới hạn 30 tin/phút).")]
    public bool autoPublish = false;
    [Tooltip("Cạnh dài ảnh gửi lên. Adafruit giới hạn 100 KB/tin → 320 px JPEG khoảng 20–30 KB.")]
    public int uploadSize = 320;
    [Range(10, 100)] public int jpegQuality = 60;

    List<string> files = new();
    Texture2D current;
    Lab08Detection det;
    bool found;
    int index;

    void Start()
    {
        string folder = Path.Combine(Application.streamingAssetsPath, datasetFolder);
        if (Directory.Exists(folder))
            files = Directory.GetFiles(folder).Where(f => f.EndsWith(".jpg") || f.EndsWith(".png")).OrderBy(f => f).ToList();
        if (files.Count == 0) { labelText.text = "No images"; Debug.LogError($"[Lab08] Không có ảnh trong {folder}"); return; }

        prevButton.onClick.AddListener(() => Show((index - 1 + files.Count) % files.Count)); // vòng về cuối
        nextButton.onClick.AddListener(() => Show((index + 1) % files.Count));               // vòng về đầu
        categoryButton.onClick.AddListener(SendToCloud);
        Show(0);
    }

    void Update() { if (statusText != null && mqtt != null) statusText.text = mqtt.LastStatus; }

    void Show(int i)
    {
        index = i;
        if (current != null) Destroy(current);
        current = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        current.LoadImage(File.ReadAllBytes(files[i]));
        counterText.text = $"{i + 1} / {files.Count}";

        Rect r = FitRect(current.width, current.height);
        datasetImage.texture = current;
        datasetImage.rectTransform.anchoredPosition = new Vector2(r.x, -r.y);
        datasetImage.rectTransform.sizeDelta = r.size;

        found = detector.Detect(current, out det);
        if (found)
        {
            float s = r.width / current.width; // pixel ảnh → pixel UI
            boundingBox.anchoredPosition = new Vector2(r.x + det.x1 * s, -(r.y + det.y1 * s));
            boundingBox.sizeDelta = new Vector2((det.x2 - det.x1) * s, (det.y2 - det.y1) * s);
            labelText.text = $"{det.label}  {det.confidence * 100f:0.0}%";
            coordText.text = $"Box (x1, y1, x2, y2) = ({det.x1}, {det.y1}, {det.x2}, {det.y2})\nCenter = ({det.CenterX}, {det.CenterY})   Image {current.width}×{current.height}";
        }
        else { labelText.text = "No animal"; coordText.text = "Box: —"; }
        boundingBox.gameObject.SetActive(found);

        if (autoPublish) SendToCloud();
    }

    /// Gắn vào nút Category: gửi nhãn, toạ độ box và ảnh (đã vẽ box) lên cloud qua MQTT.
    public void SendToCloud()
    {
        if (current == null || mqtt == null) return;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        string label = found ? $"{det.label} {(det.confidence * 100f).ToString("0.0", ci)}%" : "none";
        string box = found ? $"{{\"label\":\"{det.label}\",\"x1\":{det.x1},\"y1\":{det.y1},\"x2\":{det.x2},\"y2\":{det.y2},\"w\":{current.width},\"h\":{current.height}}}" : "{}";
        var img = Resize(current, uploadSize);
        if (found) DrawBox(img, det, (float)img.width / current.width);
        string b64 = System.Convert.ToBase64String(img.EncodeToJPG(jpegQuality));
        Destroy(img);
        mqtt.Publish((mqtt.labelFeed, label), (mqtt.boxFeed, box), (mqtt.imageFeed, b64));
    }

    // Vị trí hiển thị ảnh trong imageArea, giữ tỉ lệ (fit), x/y tính từ góc trên-trái.
    Rect FitRect(int w, int h)
    {
        Vector2 a = imageArea.rect.size;
        float s = Mathf.Min(a.x / w, a.y / h);
        return new Rect((a.x - w * s) * 0.5f, (a.y - h * s) * 0.5f, w * s, h * s);
    }

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

    // Texture có gốc ở góc DƯỚI-trái → lật trục y khi vẽ khung.
    static void DrawBox(Texture2D img, Lab08Detection d, float s, int thick = 3)
    {
        Color col = new Color32(0x2E, 0xCC, 0x40, 0xFF);
        int x1 = (int)(d.x1 * s), x2 = (int)(d.x2 * s) - 1;
        int yTop = img.height - 1 - (int)(d.y1 * s), yBot = img.height - (int)(d.y2 * s);
        for (int t = 0; t < thick; t++)
        {
            for (int x = x1; x <= x2; x++) { Px(img, x, yTop - t, col); Px(img, x, yBot + t, col); }
            for (int y = yBot; y <= yTop; y++) { Px(img, x1 + t, y, col); Px(img, x2 - t, y, col); }
        }
        img.Apply();
    }
    static void Px(Texture2D t, int x, int y, Color c) { if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c); }
}
