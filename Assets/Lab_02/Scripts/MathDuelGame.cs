using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum MathOp { Add, Subtract, Multiply, Divide, Function }

/// <summary>
/// Luật chơi Math Duel. Gắn vào object GameManager.
/// Luồng: sinh phép tính → cả hai cùng thấy → ai bấm đúng trước +1 điểm → câu mới.
/// Bấm sai bị khóa nút một lúc. Đủ điểm thì thắng. Nút Pause dừng/tiếp tục,
/// khi ván đã kết thúc thì bấm Pause để chơi lại.
/// </summary>
public class MathDuelGame : MonoBehaviour
{
    [Header("Tham chiếu (kéo thả trong Inspector)")]
    [SerializeField] PlayerPanel player1;
    [SerializeField] PlayerPanel player2;
    [SerializeField] Button pauseButton;

    [Header("Cấu hình ván (CP7 sẽ lấy từ MainScreen)")]
    [SerializeField] MathOp operation = MathOp.Add;

    [Tooltip("Biểu thức f(x) cho chế độ Function. Fig1 mặc định 2*x+3")]
    [SerializeField] string functionExpr = "2*x+3";

    [Tooltip("Số lớn nhất trong phép tính. Fig1 có 3 mức: 10, 20, 50")]
    [SerializeField, Min(2)] int maxNumber = 50;

    [Tooltip("Điểm để thắng. Fig1 có 3 mức: 5, 10, 15")]
    [SerializeField, Min(1)] int targetScore = 10;

    [Tooltip("Số giây cho mỗi câu, 0 = tắt. Fig1: Tắt, 5s, 8s")]
    [SerializeField, Min(0)] float secondsPerQuestion = 0f;

    [Header("Cảm giác chơi")]
    [Tooltip("Khóa nút khi bấm sai. 1 giây đủ để phạt bấm bừa, không đủ lâu để người chơi chán")]
    [SerializeField, Min(0)] float wrongLockSeconds = 1f;

    [Tooltip("Thừa số tối đa cho nhân/chia. 12 = bảng cửu chương mở rộng, vẫn tính nhẩm được")]
    [SerializeField, Min(2)] int maxFactor = 12;

    [Tooltip("Đáp án nhiễu lệch tối đa bao nhiêu so với đáp án đúng. " +
             "10 giữ nhiễu đủ gần để buộc phải tính, không đoán theo độ lớn được")]
    [SerializeField, Min(2)] int distractorRange = 10;

    // Chữ hiển thị. Dùng ASCII vì font LiberationSans mặc định của TMP
    // có thể thiếu glyph tiếng Việt có dấu (sẽ hiện ô vuông).
    const string TextPaused = "PAUSED";
    const string TextWin = "WIN!";
    const string TextLose = "LOSE";

    // Màu phản hồi: đỏ #D9483B khi sai, xanh lá #3FA34D khi đúng
    static readonly Color WrongColor = new Color32(0xD9, 0x48, 0x3B, 255);
    static readonly Color RightColor = new Color32(0x3F, 0xA3, 0x4D, 255);

    int score1, score2;
    int correctIndex;
    int[] options;
    string currentQuestion;
    bool isPaused, isOver;
    Coroutine timerRoutine;

    void Awake()
    {
        // Vào từ MainScreen thì lấy cấu hình người chơi chọn, mở thẳng scene thì giữ giá trị Inspector
        if (GameSettings.HasValue)
        {
            operation = GameSettings.Operation;
            maxNumber = GameSettings.MaxNumber;
            targetScore = GameSettings.TargetScore;
            secondsPerQuestion = GameSettings.SecondsPerQuestion;
            functionExpr = GameSettings.FunctionExpr;
        }

        player1.AnswerClicked += OnAnswerClicked;
        player2.AnswerClicked += OnAnswerClicked;
        pauseButton.onClick.AddListener(OnPausePressed);
    }

    void Start() => StartMatch();

    void OnDestroy()
    {
        // Rời scene khi đang pause mà không trả lại thì scene sau cũng bị đứng hình
        Time.timeScale = 1f;
    }

    // ------------------------------------------------------------------
    // Vòng đời ván chơi
    // ------------------------------------------------------------------

    [ContextMenu("Restart Match")]
    public void StartMatch()
    {
        StopAllCoroutines();
        timerRoutine = null;
        Time.timeScale = 1f;
        isPaused = false;
        isOver = false;
        score1 = score2 = 0;
        player1.SetScore(0);
        player2.SetScore(0);
        player1.SetInteractable(true);
        player2.SetInteractable(true);
        NextQuestion();
    }

    [ContextMenu("Next Question")]
    public void NextQuestion()
    {
        int optionCount = Mathf.Min(player1.ButtonCount, player2.ButtonCount);
        int answer = GenerateQuestion(out currentQuestion);
        options = BuildOptions(answer, optionCount, out correctIndex);

        player1.ShowQuestion(currentQuestion, options);
        player2.ShowQuestion(currentQuestion, options);

        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = secondsPerQuestion > 0f ? StartCoroutine(QuestionTimer()) : null;
    }

    void OnAnswerClicked(PlayerPanel who, int index)
    {
        if (isPaused || isOver) return;

        if (index != correctIndex)
        {
            who.FlashButton(index, WrongColor);
            StartCoroutine(LockPlayer(who));
            return;
        }

        who.FlashButton(index, RightColor);
        if (who == player1) player1.SetScore(++score1);
        else player2.SetScore(++score2);

        if (score1 >= targetScore || score2 >= targetScore) EndMatch(who);
        else NextQuestion();
    }

    void EndMatch(PlayerPanel winner)
    {
        isOver = true;
        if (timerRoutine != null) StopCoroutine(timerRoutine);
        player1.SetInteractable(false);
        player2.SetInteractable(false);
        player1.ShowMessage(winner == player1 ? TextWin : TextLose);
        player2.ShowMessage(winner == player2 ? TextWin : TextLose);
    }

    void OnPausePressed()
    {
        if (isOver) { StartMatch(); return; }

        isPaused = !isPaused;
        // timeScale = 0 làm dừng WaitForSeconds, nên đồng hồ câu hỏi và thời gian khóa cũng dừng theo
        Time.timeScale = isPaused ? 0f : 1f;
        player1.SetInteractable(!isPaused);
        player2.SetInteractable(!isPaused);

        if (isPaused)
        {
            player1.ShowMessage(TextPaused);
            player2.ShowMessage(TextPaused);
        }
        else
        {
            player1.ShowQuestion(currentQuestion, options);
            player2.ShowQuestion(currentQuestion, options);
        }
    }

    IEnumerator LockPlayer(PlayerPanel who)
    {
        who.SetInteractable(false);
        yield return new WaitForSeconds(wrongLockSeconds);
        if (!isOver && !isPaused) who.SetInteractable(true);
    }

    IEnumerator QuestionTimer()
    {
        yield return new WaitForSeconds(secondsPerQuestion);
        timerRoutine = null;
        if (!isOver) NextQuestion(); // hết giờ: không ai được điểm, sang câu mới
    }

    // ------------------------------------------------------------------
    // Sinh câu hỏi
    // ------------------------------------------------------------------

    /// <summary>Trả về đáp án đúng, xuất chuỗi phép tính qua out.</summary>
    int GenerateQuestion(out string text)
    {
        int a, b;
        switch (operation)
        {
            case MathOp.Subtract:
                a = Random.Range(1, maxNumber + 1);   // Range(int) không lấy cận trên nên +1
                b = Random.Range(1, a + 1);            // b ≤ a để kết quả không âm
                text = $"{a} - {b}";
                return a - b;

            case MathOp.Multiply:
                {
                    int cap = Mathf.Min(maxNumber, maxFactor);
                    a = Random.Range(1, cap + 1);
                    b = Random.Range(1, cap + 1);
                    text = $"{a} × {b}";
                    return a * b;
                }

            case MathOp.Divide:
                {
                    // Sinh ngược từ phép nhân để luôn chia hết
                    int cap = Mathf.Min(maxNumber, maxFactor);
                    b = Random.Range(1, cap + 1);
                    int q = Random.Range(1, cap + 1);
                    a = b * q;
                    text = $"{a} ÷ {b}";
                    return q;
                }

            case MathOp.Function:
                {
                    // Hỏi f(a) với a ngẫu nhiên trong phạm vi số. Biểu thức lỗi thì quay về 2*x+3
                    a = Random.Range(0, maxNumber + 1);
                    if (!FunctionParser.TryEvaluate(functionExpr, a, out int fa))
                    {
                        functionExpr = "2*x+3";
                        FunctionParser.TryEvaluate(functionExpr, a, out fa);
                    }
                    text = $"f({a}) = ?";
                    return fa;
                }

            default: // Add
                a = Random.Range(1, maxNumber + 1);
                b = Random.Range(1, maxNumber + 1);
                text = $"{a} + {b}";
                return a + b;
        }
    }

    /// <summary>Tạo mảng đáp án gồm 1 đúng và (count − 1) nhiễu không trùng, đặt đáp án đúng ở vị trí ngẫu nhiên.</summary>
    int[] BuildOptions(int answer, int count, out int answerIndex)
    {
        int[] result = new int[count];
        answerIndex = Random.Range(0, count);
        result[answerIndex] = answer;

        for (int i = 0; i < count; i++)
        {
            if (i == answerIndex) continue;
            int candidate;
            int guard = 0; // chặn vòng lặp vô hạn nếu khoảng nhiễu quá hẹp
            do
            {
                candidate = answer + Random.Range(-distractorRange, distractorRange + 1);
                guard++;
            }
            while ((candidate < 0 || Contains(result, i, candidate) || candidate == answer) && guard < 100);
            result[i] = candidate;
        }
        return result;
    }

    // Chỉ xét các ô đã điền (trước vị trí upTo) và ô đáp án đúng
    bool Contains(int[] arr, int upTo, int value)
    {
        for (int i = 0; i < upTo; i++) if (arr[i] == value) return true;
        return false;
    }
}