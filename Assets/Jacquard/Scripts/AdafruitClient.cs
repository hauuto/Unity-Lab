using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// Gửi và đọc dữ liệu Adafruit IO qua REST API v2.
/// AIO Key KHÔNG lưu trong scene. Key được đọc từ file keyFile (mặc định UserSettings/adafruit_key.txt).
/// Thư mục UserSettings nằm trong .gitignore chuẩn của Unity, nên key không bị đẩy lên GitHub.
public class AdafruitClient : MonoBehaviour
{
    [SerializeField] string username = "thanhkhoi448";
    [SerializeField] string keyFile = "UserSettings/adafruit_key.txt";

    [Header("Feed keys")]
    public string imageFeed = "jacquard-image";           // ảnh JPEG base64 (khối Image trên dashboard)
    public string confidenceFeed = "jacquard-confidence"; // số 0..100
    public string buttonFeed = "jacquard-nextpreviousbtn";

    [Tooltip("Chu kỳ hỏi feed nút bấm (giây). Gói miễn phí giới hạn số request mỗi phút, nên đừng đặt quá nhỏ.")]
    public float pollInterval = 3f;

    /// Gọi khi trên dashboard có lệnh mới. Feed nút: 1 = next, 0 = previous → trả ra +1 / -1.
    public event Action<int> OnRemoteStep;

    string key;
    string lastButtonId;
    bool pollErrorLogged; // chỉ báo lỗi hỏi nút 1 lần, tránh spam Console mỗi 3 giây
    string Base => $"https://io.adafruit.com/api/v2/{username}/feeds/";
    public bool Ready => !string.IsNullOrEmpty(key);

    void Awake()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), keyFile);
        if (File.Exists(path)) key = File.ReadAllText(path).Trim();
        else Debug.LogWarning($"[Adafruit] Không tìm thấy key tại {path}. Chỉ chạy giao diện, không gửi dữ liệu.");
    }

    void Start() { if (Ready) StartCoroutine(PollButton()); }

    public void Send(string feed, string value)
    {
        if (Ready) StartCoroutine(Post(feed, value));
    }

    IEnumerator Post(string feed, string value)
    {
        // JSON tối giản. Escape dấu " và \ để chuỗi không làm hỏng JSON.
        string body = "{\"value\":\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
        using var req = new UnityWebRequest(Base + feed + "/data", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-AIO-Key", key);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning($"[Adafruit] POST {feed} lỗi: {req.responseCode} {req.downloadHandler.text}");
    }

    [Serializable] class DataPoint { public string id; public string value; }

    IEnumerator PollButton()
    {
        bool first = true;
        while (true)
        {
            using (var req = UnityWebRequest.Get(Base + buttonFeed + "/data/last"))
            {
                req.SetRequestHeader("X-AIO-Key", key);
                req.timeout = 8; // giây
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success && !pollErrorLogged)
                {
                    Debug.LogWarning($"[Adafruit] Không đọc được feed nút: {req.responseCode} {req.error}");
                    pollErrorLogged = true;
                }
                if (req.result == UnityWebRequest.Result.Success) pollErrorLogged = false;
                if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.text.Length > 2)
                {
                    var d = JsonUtility.FromJson<DataPoint>(req.downloadHandler.text);
                    // Lần đọc đầu chỉ ghi nhớ id, để lệnh cũ còn trên feed không làm chuyển ảnh khi mở app.
                    if (d != null && d.id != lastButtonId)
                    {
                        lastButtonId = d.id;
                        int step = ParseStep(d.value);
                        if (!first && step != 0) OnRemoteStep?.Invoke(step);
                    }
                }
            }
            first = false;
            yield return new WaitForSeconds(pollInterval);
        }
    }

    /// Nhận nhiều cách đặt giá trị nút. Giá trị khác (ví dụ giá trị nhả của nút Momentary) bị bỏ qua.
    static int ParseStep(string v)
    {
        switch ((v ?? "").Trim().ToLowerInvariant())
        {
            case "1": case "next": return 1;      // quy ước của Khôi: 1 = next
            case "0": case "prev": return -1;      // 0 = previous
            default: return 0;
        }
    }
}
