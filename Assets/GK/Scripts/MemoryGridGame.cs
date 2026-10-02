using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Câu 1 — Trò chơi tìm số.
/// 5 câu; mỗi câu lưới 7x7 = 49 số KHÔNG trùng trong [0, 99], mỗi ô 1 trong 4 màu ngẫu nhiên.
/// Thời gian mỗi câu 5s giảm dần về 0s (số "05" trong hình tròn + thanh thời gian).
/// Click đúng số cần tìm -> sang câu tiếp và cập nhật giao diện. Click sai -> không xử lý.
/// Hết giờ -> sang câu tiếp (không tính là làm được).
/// Màn đầu tiên dùng previewSeed nên giống hệt lưới xem trước trong Editor.
/// </summary>
public class MemoryGridGame : MonoBehaviour
{
    [Header("Cấu hình (dễ sửa khi đề thay đổi)")]
    public int totalQuestions = 5;      // tổng số câu
    public int columns = 7;             // 7 x 7
    public int minValue = 0;            // giá trị nhỏ nhất (bao gồm)
    public int maxValue = 99;           // giá trị lớn nhất (bao gồm)
    public float timePerQuestion = 5f;  // giây mỗi câu
    [Tooltip("Seed của màn đầu tiên = lưới xem trước trong Editor")]
    public int previewSeed = 2026;

    [Header("4 màu: nâu, xanh lá, xanh đậm, tím (tông nhạt)")]
    public Color[] palette =
    {
        new Color32(0xD9, 0xB8, 0xA0, 0xFF), // nâu nhạt      #D9B8A0
        new Color32(0xA8, 0xDC, 0xB0, 0xFF), // xanh lá nhạt  #A8DCB0
        new Color32(0x9F, 0xB2, 0xE6, 0xFF), // xanh đậm nhạt #9FB2E6
        new Color32(0xD2, 0xB8, 0xEA, 0xFF), // tím nhạt      #D2B8EA
    };

    [Header("Sprite ô")]
    public Sprite cellSprite;           // ô vuông
    public Sprite cornerTL, cornerTR, cornerBL, cornerBR;   // 4 ô góc bo 1 góc theo khung ma trận

    [Header("Tham chiếu UI")]
    public Transform gridParent;        // có GridLayoutGroup
    public Button cellPrefab;           // Button + TMP_Text con
    public TMP_Text questionText;       // "Câu: 1/5"   (thẻ trên, bên trái)
    public TMP_Text timerText;          // "05" trong hình tròn (thẻ trên, bên phải)
    public TMP_Text targetText;         // số cần tìm
    [UnityEngine.Serialization.FormerlySerializedAs("progressFill")]
    public Image timeBar;               // thanh thời gian (Image Filled, Horizontal)
    public GameObject endPanel;
    public TMP_Text endText;

    int CellCount { get { return columns * columns; } }

    readonly List<Button> cells = new List<Button>();
    int currentQuestion;   // chỉ số câu hiện tại (0-based)
    int solvedCount;       // số câu đã làm được (chọn đúng)
    int targetValue;
    float timeLeft;
    bool playing;

    void Start()
    {
        EnsureCells();
        cells.Clear();
        foreach (Transform t in gridParent) cells.Add(t.GetComponent<Button>());
        StartGame();
    }

    /// <summary>Đảm bảo gridParent có đúng CellCount ô (dùng chung cho Editor và Play).</summary>
    void EnsureCells()
    {
        if (gridParent.childCount == CellCount) return;
        for (int i = gridParent.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying) Destroy(gridParent.GetChild(i).gameObject);
            else DestroyImmediate(gridParent.GetChild(i).gameObject);
        }
        for (int i = 0; i < CellCount; i++)
        {
            Button b = Instantiate(cellPrefab, gridParent);
            b.name = "Cell_" + i;
        }
    }

    [ContextMenu("Chơi lại")]
    public void StartGame()
    {
        currentQuestion = 0;
        solvedCount = 0;
        if (endPanel) endPanel.SetActive(false);
        playing = true;
        NewRound(new System.Random(previewSeed));   // màn 1 = lưới xem trước
    }

    /// <summary>Nút Đóng trên bảng kết quả: dừng hẳn trò chơi.
    /// Bản build: thoát ứng dụng. Trong Editor: thoát Play Mode (Application.Quit không có tác dụng trong Editor).</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


    /// <summary>Sinh 49 số + màu cho các ô, trả về số cần tìm. Dùng chung cho Play và Editor.</summary>
    int FillGrid(System.Random rng, bool hookClicks)
    {
        // b. 49 số KHÔNG trùng: xáo trộn Fisher–Yates dãy [min..max] rồi lấy 49 số đầu
        List<int> pool = new List<int>();
        for (int v = minValue; v <= maxValue; v++) pool.Add(v);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        int last = columns - 1;
        for (int i = 0; i < gridParent.childCount; i++)
        {
            Transform t = gridParent.GetChild(i);
            Image img = t.GetComponent<Image>();
            int value = pool[i];
            t.GetComponentInChildren<TMP_Text>().text = value.ToString();
            // c. mỗi số gán đúng 1 màu ngẫu nhiên trong 4 màu
            img.color = palette[rng.Next(palette.Length)];
            img.sprite = SpriteFor(i / columns, i % columns, last);

            if (hookClicks)
            {
                Button b = t.GetComponent<Button>();
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => OnCellClicked(value));
            }
        }
        // Số cần tìm lấy trong 49 số đang hiển thị -> luôn có đáp án
        return pool[rng.Next(gridParent.childCount)];
    }

    Sprite SpriteFor(int row, int col, int last)
    {
        if (row == 0 && col == 0 && cornerTL) return cornerTL;
        if (row == 0 && col == last && cornerTR) return cornerTR;
        if (row == last && col == 0 && cornerBL) return cornerBL;
        if (row == last && col == last && cornerBR) return cornerBR;
        return cellSprite;
    }

    void NewRound(System.Random rng)
    {
        targetValue = FillGrid(rng, true);
        timeLeft = timePerQuestion;
        UpdateUI();
    }

    void OnCellClicked(int value)
    {
        if (!playing) return;
        if (value != targetValue) return;   // d. chọn sai: không xử lý
        solvedCount++;
        NextQuestion();
    }

    void NextQuestion()
    {
        currentQuestion++;
        if (currentQuestion >= totalQuestions) { EndGame(); return; }
        NewRound(new System.Random(Random.Range(int.MinValue, int.MaxValue)));   // các màn sau: ngẫu nhiên
    }

    void Update()
    {
        if (!playing) return;
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            NextQuestion();   // hết giờ: sang câu mới
            return;
        }
        UpdateTimer();
    }

    void UpdateTimer()
    {
        timerText.text = Mathf.CeilToInt(timeLeft).ToString("00");
        if (timeBar) timeBar.fillAmount = timeLeft / timePerQuestion;   // co dần theo thời gian còn lại
    }

    void UpdateUI()
    {
        int shown = Mathf.Min(currentQuestion + 1, totalQuestions);
        questionText.text = "Câu: " + shown + "/" + totalQuestions;
        targetText.text = playing ? targetValue.ToString() : "-";
        UpdateTimer();
    }

    void EndGame()
    {
        playing = false;
        timeLeft = 0f;
        UpdateUI();
        if (endPanel) endPanel.SetActive(true);
        if (endText) endText.text = "HOÀN THÀNH\nLàm được " + solvedCount + "/" + totalQuestions + " câu";
    }

    /// <summary>Vẽ lưới xem trước trong Editor giống hệt màn 1 khi Play.</summary>
    [ContextMenu("Xem trước lưới (Editor)")]
    public void PreviewInEditor()
    {
        if (gridParent == null || cellPrefab == null) return;
        EnsureCells();
        int target = FillGrid(new System.Random(previewSeed), false);
        if (targetText) targetText.text = target.ToString();
        if (questionText) questionText.text = "Câu: 1/" + totalQuestions;
        if (timerText) timerText.text = Mathf.CeilToInt(timePerQuestion).ToString("00");
        if (timeBar) timeBar.fillAmount = 1f;
#if UNITY_EDITOR
        // Ghi nhận thay đổi của các ô (prefab instance) để Unity lưu vào scene;
        // thiếu bước này, màu xem trước sẽ mất khi lưu/mở lại scene.
        foreach (Transform t in gridParent)
        {
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<Image>());
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponentInChildren<TMP_Text>());
        }
        if (targetText) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(targetText);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
