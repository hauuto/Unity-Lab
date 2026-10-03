using System;

/// <summary>
/// Bộ tính biểu thức f(x) đơn giản cho chế độ f(x) của Fig1.
/// Hỗ trợ: số nguyên, biến x, + - * / ( ), dấu âm đứng đầu. Ví dụ: 2*x+3, x*x, (x+1)*2
/// Dùng phương pháp đệ quy xuống (recursive descent): Expr = Term (+|- Term)*, Term = Factor (*|/ Factor)*
/// </summary>
public static class FunctionParser
{
    /// <summary>Tính f(x). Trả về false nếu biểu thức sai cú pháp hoặc chia cho 0.</summary>
    public static bool TryEvaluate(string expr, int x, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(expr)) return false;
        try
        {
            var p = new Parser(expr.Replace(" ", "").ToLowerInvariant(), x);
            result = p.ParseExpr();
            return p.AtEnd; // còn ký tự thừa = sai cú pháp
        }
        catch (Exception) { return false; }
    }

    class Parser
    {
        readonly string s; readonly int x; int i;
        public Parser(string s, int x) { this.s = s; this.x = x; }
        public bool AtEnd => i >= s.Length;
        char Peek => i < s.Length ? s[i] : '\0';

        public int ParseExpr()
        {
            int v = ParseTerm();
            while (Peek == '+' || Peek == '-') { char op = s[i++]; int r = ParseTerm(); v = op == '+' ? v + r : v - r; }
            return v;
        }
        int ParseTerm()
        {
            int v = ParseFactor();
            while (Peek == '*' || Peek == '/')
            {
                char op = s[i++]; int r = ParseFactor();
                if (op == '/') { if (r == 0) throw new DivideByZeroException(); v /= r; } else v *= r;
            }
            return v;
        }
        int ParseFactor()
        {
            if (Peek == '-') { i++; return -ParseFactor(); }
            if (Peek == '(') { i++; int v = ParseExpr(); if (Peek != ')') throw new FormatException(); i++; return v; }
            if (Peek == 'x') { i++; return x; }
            int start = i;
            while (char.IsDigit(Peek)) i++;
            if (start == i) throw new FormatException();
            return int.Parse(s.Substring(start, i - start));
        }
    }
}
