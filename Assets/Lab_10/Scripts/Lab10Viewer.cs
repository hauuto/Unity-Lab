using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// Duyet anh mau, goi API va ve khung nhan dien len anh. Tu dung UI luc chay.
[RequireComponent(typeof(Lab10ApiClient))]
public class Lab10Viewer : MonoBehaviour
{
    const string ImageFolder = "Lab08_Animals"; // trong StreamingAssets
    const float ImageAreaWidth = 1000f, ImageAreaHeight = 800f;
    const int BoxThickness = 4;

    Lab10ApiClient client;
    string[] files;
    int index, requestId;
    RectTransform imageRect, boxLayer;
    RawImage raw;
    Text status;
    Texture2D current;
    readonly List<GameObject> boxes = new List<GameObject>();

    public string LastStatus => status != null ? status.text : "";

    void Start()
    {
        client = GetComponent<Lab10ApiClient>();
        BuildUi();
        files = Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, ImageFolder), "*.jpg");
        System.Array.Sort(files);
        if (files.Length == 0) { status.text = "Không tìm thấy ảnh mẫu."; return; }
        Show(0);
    }

    public void Show(int i)
    {
        index = (i + files.Length) % files.Length;
        StartCoroutine(ShowRoutine(index));
    }

    IEnumerator ShowRoutine(int i)
    {
        int id = ++requestId; // bam nhanh: ket qua cua lan bam cu bi bo qua
        ClearBoxes();
        byte[] jpg = File.ReadAllBytes(files[i]);
        if (current != null) Destroy(current);
        current = new Texture2D(2, 2);
        current.LoadImage(jpg);
        raw.texture = current;

        float scale = Mathf.Min(ImageAreaWidth / current.width, ImageAreaHeight / current.height);
        imageRect.sizeDelta = new Vector2(current.width * scale, current.height * scale);
        status.text = "Đang gọi API...";

        Lab10Response result = null; string error = null;
        yield return client.Predict(jpg, Path.GetFileName(files[i]), (r, e) => { result = r; error = e; });
        if (id != requestId) yield break;

        string name = Path.GetFileName(files[i]);
        if (error != null) { status.text = $"{name}\nLỗi: {error}\nKiểm tra: docker compose up -d"; yield break; }

        var lines = new System.Text.StringBuilder($"{name} ({i + 1}/{files.Length})\nModel: {result.inference_ms:0.0} ms\n{result.detections.Length} vật thể:\n");
        foreach (var d in result.detections)
        {
            DrawBox(d, scale);
            lines.AppendLine($"- {d.label} {d.confidence:P0}");
        }
        status.text = lines.ToString();
    }

    void DrawBox(Lab10Detection d, float scale)
    {
        // API: goc tren-trai, Y xuong. UI: anchor tren-trai nen Y phai am.
        var go = new GameObject("Box_" + d.label, typeof(RectTransform));
        go.transform.SetParent(boxLayer, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(d.x1 * scale, -d.y1 * scale);
        rt.sizeDelta = new Vector2((d.x2 - d.x1) * scale, (d.y2 - d.y1) * scale);
        boxes.Add(go);

        Edge(rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, BoxThickness));
        Edge(rt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, BoxThickness));
        Edge(rt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(BoxThickness, 0));
        Edge(rt, new Vector2(1, 0), new Vector2(1, 1), new Vector2(BoxThickness, 0));
    }

    static void Edge(RectTransform parent, Vector2 min, Vector2 max, Vector2 size)
    {
        var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min; rt.anchorMax = max; rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = Color.green;
    }

    void ClearBoxes()
    {
        foreach (var b in boxes) Destroy(b);
        boxes.Clear();
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);

        var imgGo = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
        imgGo.transform.SetParent(canvasGo.transform, false);
        raw = imgGo.GetComponent<RawImage>();
        imageRect = (RectTransform)imgGo.transform;
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = new Vector2(40 + ImageAreaWidth / 2, 0);

        var layer = new GameObject("Boxes", typeof(RectTransform));
        layer.transform.SetParent(imgGo.transform, false);
        boxLayer = (RectTransform)layer.transform;
        boxLayer.anchorMin = Vector2.zero; boxLayer.anchorMax = Vector2.one;
        boxLayer.offsetMin = boxLayer.offsetMax = Vector2.zero;

        var textGo = new GameObject("Status", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);
        status = textGo.GetComponent<Text>();
        status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        status.fontSize = 28;
        status.color = Color.white;
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 1);
        trt.anchoredPosition = new Vector2(1080, -50);
        trt.sizeDelta = new Vector2(490, 650);

        MakeButton(canvasGo.transform, "Trước", new Vector2(1080, 60), () => Show(index - 1));
        MakeButton(canvasGo.transform, "Sau", new Vector2(1320, 60), () => Show(index + 1));
    }

    static void MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(220, 70);
        go.GetComponent<Button>().onClick.AddListener(onClick);

        var t = new GameObject("Text", typeof(RectTransform), typeof(Text));
        t.transform.SetParent(go.transform, false);
        var text = t.GetComponent<Text>();
        text.text = label; text.color = Color.black; text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var r = (RectTransform)t.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
