using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Một nửa màn hình của một người chơi.
/// Gắn vào Player1_Panel và Player2_Panel.
/// Script này chỉ hiển thị và báo sự kiện bấm nút, không chứa luật chơi.
/// Nhờ vậy bài 2 dựng giao diện mới vẫn dùng lại được, miễn giữ đúng đường dẫn con.
/// </summary>
public class PlayerPanel : MonoBehaviour
{
    // ---- Đường dẫn con, khớp với Hierarchy đã dựng ở CP3–CP5 ----
    // Đổi tên object trong Hierarchy thì phải sửa ở đây.
    const string QuestionPath = "QuestionBox/Inner/QuestionText";
    const string AnswersPath = "Answers";
    const string AnswerTextPath = "Inner/AnswerText";   // tính từ mỗi AnswerButton_i
    const string ScorePath = "ScoreBox/ScoreText";

    [Header("Tự điền bằng chuột phải tên component > Auto Bind")]
    [SerializeField] TMP_Text questionText;
    [SerializeField] Button[] answerButtons;
    [SerializeField] TMP_Text[] answerTexts;
    [SerializeField] TMP_Text scoreText;

    /// <summary>Báo cho MathDuelGame: (panel nào, nút thứ mấy).</summary>
    public event Action<PlayerPanel, int> AnswerClicked;

    public int ButtonCount => answerButtons != null ? answerButtons.Length : 0;

    void Awake()
    {
        AutoBind();
        for (int i = 0; i < answerButtons.Length; i++)
        {
            int index = i; // chép ra biến riêng, nếu dùng thẳng i thì mọi nút đều báo i = 3
            answerButtons[i].onClick.AddListener(() => AnswerClicked?.Invoke(this, index));
        }
    }

    /// <summary>Tìm lại toàn bộ tham chiếu theo đường dẫn. Chạy được ngay trong Editor.</summary>
    [ContextMenu("Auto Bind")]
    public void AutoBind()
    {
        questionText = FindText(QuestionPath);
        scoreText = FindText(ScorePath);

        Transform answers = transform.Find(AnswersPath);
        if (answers == null)
        {
            Debug.LogError($"[{name}] Không tìm thấy '{AnswersPath}'", this);
            return;
        }

        // Lấy nút theo thứ tự trong Hierarchy: AnswerButton_0, _1, _2
        int count = answers.childCount;
        answerButtons = new Button[count];
        answerTexts = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            Transform btn = answers.GetChild(i);
            answerButtons[i] = btn.GetComponent<Button>();
            Transform t = btn.Find(AnswerTextPath);
            answerTexts[i] = t != null ? t.GetComponent<TMP_Text>() : null;

            if (answerButtons[i] == null || answerTexts[i] == null)
                Debug.LogError($"[{name}] '{btn.name}' thiếu Button hoặc '{AnswerTextPath}'", btn);
        }
    }

    TMP_Text FindText(string path)
    {
        Transform t = transform.Find(path);
        if (t == null) { Debug.LogError($"[{name}] Không tìm thấy '{path}'", this); return null; }
        return t.GetComponent<TMP_Text>();
    }

    public void ShowQuestion(string question, int[] options)
    {
        questionText.text = question;
        for (int i = 0; i < answerTexts.Length && i < options.Length; i++)
            answerTexts[i].text = options[i].ToString();
    }

    /// <summary>Hiện chữ thay cho phép tính (PAUSED, WIN, LOSE...).</summary>
    public void ShowMessage(string message) => questionText.text = message;

    public void SetScore(int score) => scoreText.text = score.ToString();

    /// <summary>Nháy màu ô trong của nút (xanh = đúng, đỏ = sai) rồi trả về màu cũ.
    /// Dùng unscaled time để vẫn chạy khi đang Pause.</summary>
    public void FlashButton(int index, Color color, float seconds = 0.35f)
    {
        if (index < 0 || index >= answerButtons.Length) return;
        Transform inner = answerButtons[index].transform.Find("Inner");
        if (inner == null) return;
        StartCoroutine(FlashRoutine(inner.GetComponent<Image>(), color, seconds));
    }

    System.Collections.IEnumerator FlashRoutine(Image img, Color color, float seconds)
    {
        Color old = InnerColor;
        img.color = color;
        yield return new WaitForSecondsRealtime(seconds);
        img.color = old;
    }

    // Màu gốc ô trong của nút (#7A6A5D), giữ cố định để nháy liên tiếp không bị lưu nhầm màu đỏ
    static readonly Color InnerColor = new Color32(0x7A, 0x6A, 0x5D, 255);

    /// <summary>Bật/tắt cả 3 nút. Tắt thì Button tự đổi sang Disabled Color (xám mờ).</summary>
    public void SetInteractable(bool value)
    {
        foreach (Button b in answerButtons) b.interactable = value;
    }
}