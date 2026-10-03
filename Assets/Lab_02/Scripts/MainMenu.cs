using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Màn hình chọn cấu hình (theo Fig1). Gắn vào object MainMenu.
/// Mỗi nhóm là một hàng nút, chỉ một nút được chọn; nút chọn đổi sang màu tím.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] string gameSceneName = "PlayerScreen";

    [Header("Hàng nút, thứ tự khớp với mảng giá trị bên dưới")]
    [SerializeField] Button[] modeButtons;    // × + − ÷ f(x)
    [SerializeField] Button[] rangeButtons;   // 10 20 50
    [SerializeField] Button[] targetButtons;  // 5 10 15
    [SerializeField] Button[] timerButtons;   // Off 5s 8s
    [SerializeField] Button startButton;

    [Header("Chế độ f(x): ô nhập hàm, chỉ hiện khi chọn f(x)")]
    [SerializeField] GameObject functionRow;
    [SerializeField] TMPro.TMP_InputField functionInput;
    [SerializeField] TMPro.TMP_Text functionError;

    // Giá trị tương ứng từng nút
    static readonly MathOp[] Modes = { MathOp.Multiply, MathOp.Add, MathOp.Subtract, MathOp.Divide, MathOp.Function };
    static readonly int[] Ranges = { 10, 20, 50 };
    static readonly int[] Targets = { 5, 10, 15 };
    static readonly float[] Timers = { 0f, 5f, 8f };

    // Màu theo Fig1: chọn = tím #A78BFA, chưa chọn = trắng
    static readonly Color Selected = new Color32(0xA7, 0x8B, 0xFA, 255);
    static readonly Color Normal = Color.white;
    static readonly Color TextSelected = Color.white;
    static readonly Color TextNormal = new Color32(0x4B, 0x3B, 0x6B, 255);

    // Mặc định giống ảnh Fig1: +, đến 50, 10 điểm, 8s
    int mode = 1, range = 2, target = 1, timer = 2;

    void Start()
    {
        Hook(modeButtons, i => mode = i);
        Hook(rangeButtons, i => range = i);
        Hook(targetButtons, i => target = i);
        Hook(timerButtons, i => timer = i);
        startButton.onClick.AddListener(StartGame);
        RefreshAll();
    }

    void Hook(Button[] row, System.Action<int> set)
    {
        for (int i = 0; i < row.Length; i++)
        {
            int index = i; // chép biến để lambda không bắt nhầm i cuối vòng lặp
            row[i].onClick.AddListener(() => { set(index); RefreshAll(); });
        }
    }

    void RefreshAll()
    {
        Paint(modeButtons, mode); Paint(rangeButtons, range);
        Paint(targetButtons, target); Paint(timerButtons, timer);
        if (functionRow != null) functionRow.SetActive(Modes[mode] == MathOp.Function);
    }

    static void Paint(Button[] row, int selected)
    {
        for (int i = 0; i < row.Length; i++)
        {
            bool on = i == selected;
            row[i].image.color = on ? Selected : Normal;
            var t = row[i].GetComponentInChildren<TMPro.TMP_Text>();
            if (t != null) t.color = on ? TextSelected : TextNormal;
        }
    }

    [ContextMenu("Start Game")]
    public void StartGame()
    {
        if (Modes[mode] == MathOp.Function)
        {
            // Kiểm tra hàm trước khi vào game: thử tính f(1)
            string expr = functionInput != null ? functionInput.text : "2*x+3";
            if (!FunctionParser.TryEvaluate(expr, 1, out _))
            {
                if (functionError != null) functionError.text = "Invalid f(x)";
                return;
            }
            GameSettings.FunctionExpr = expr;
        }
        GameSettings.Operation = Modes[mode];
        GameSettings.MaxNumber = Ranges[range];
        GameSettings.TargetScore = Targets[target];
        GameSettings.SecondsPerQuestion = Timers[timer];
        GameSettings.HasValue = true;
        LoadGameScene();
    }

    void LoadGameScene()
    {
        // Scene có trong Build Settings thì load bình thường (cả bản build)
        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }
#if UNITY_EDITOR
        // Chưa thêm vào Build Settings: trong Editor vẫn chạy thử được bằng đường dẫn
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/Lab_02/Scenes/" + gameSceneName + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogError($"Scene '{gameSceneName}' chưa có trong Build Settings");
#endif
    }
}
