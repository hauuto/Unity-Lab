/// <summary>
/// Cấu hình ván chơi truyền từ MainScreen sang PlayerScreen.
/// Dùng static vì chỉ cần sống qua một lần LoadScene, không cần lưu xuống đĩa.
/// </summary>
public static class GameSettings
{
    /// <summary>false = mở thẳng PlayerScreen trong Editor, khi đó MathDuelGame dùng giá trị Inspector.</summary>
    public static bool HasValue;
    public static MathOp Operation = MathOp.Add;
    public static int MaxNumber = 50;          // Fig1: 10 / 20 / 50
    public static int TargetScore = 10;        // Fig1: 5 / 10 / 15
    public static float SecondsPerQuestion = 0; // Fig1: Tắt(0) / 5 / 8
    public static string FunctionExpr = "2*x+3"; // chỉ dùng khi Operation = Function
}
