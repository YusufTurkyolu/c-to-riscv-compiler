using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CtoRiscVMvc.Services
{
    public static class LoopConverter
    {
        private static int _labelCounter;
        private static readonly Dictionary<string, string> _registerMap = new Dictionary<string, string>();
        private static readonly string[] _availableRegisters = { "t0", "t1", "t2", "t3", "t4", "t5", "t6", "s0", "s1", "s2" };

        private static readonly Regex _whitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex _forLoopRegex = new Regex(@"for\s*\(\s*(.*?)\s*;\s*(.*?)\s*;\s*(.*?)\s*\)\s*(\{.*?\})?", RegexOptions.Compiled);
        private static readonly Regex _whileLoopRegex = new Regex(@"while\s*\(\s*(.*?)\s*\)\s*(\{.*?\})?", RegexOptions.Compiled);
        private static readonly Regex _doWhileLoopRegex = new Regex(@"do\s*(\{.*?\})\s*while\s*\(\s*(.*?)\s*\)\s*;", RegexOptions.Compiled);

        private static readonly Regex _initRegex = new Regex(@"(\w+)\s*=\s*(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex _conditionRegex = new Regex(@"(\w+)\s*([<>=!]+)\s*(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex _incrementRegex = new Regex(@"(\w+)\s*([+\-*/])=\s*(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex _fullIncrementRegex = new Regex(@"(\w+)\s*=\s*\1\s*([+\-])\s*(\d+)", RegexOptions.Compiled);

        public static void ResetState()
        {
            _labelCounter = 0;
            _registerMap.Clear();
        }

        private static string GetRegisterForVar(string varName)
        {
            if (!_registerMap.TryGetValue(varName, out var register))
            {
                register = _availableRegisters[_registerMap.Count % _availableRegisters.Length];
                _registerMap[varName] = register;
            }
            return register;
        }

        private static string GetBranchOp(string op, bool negate = false)
        {
            return op switch
            {
                "<" => negate ? "bge" : "blt",
                "<=" => negate ? "bgt" : "ble",
                ">" => negate ? "ble" : "bgt",
                ">=" => negate ? "blt" : "bge",
                "==" => negate ? "bne" : "beq",
                "!=" => negate ? "beq" : "bne",
                _ => throw new ArgumentException($"Unsupported operator '{op}'")
            };
        }

        private static string ParseIncrement(string expr, string reg)
        {
            expr = expr.Replace(" ", "");
            if (expr.EndsWith("++")) return $"addi {reg}, {reg}, 1";
            if (expr.EndsWith("--")) return $"addi {reg}, {reg}, -1";

            var m = _incrementRegex.Match(expr);
            if (m.Success)
            {
                var op = m.Groups[2].Value;
                var val = int.Parse(m.Groups[3].Value);
                return op switch
                {
                    "+" => $"addi {reg}, {reg}, {val}",
                    "-" => $"addi {reg}, {reg}, -{val}",
                    "*" => $"mul {reg}, {reg}, {val}",
                    "/" => $"li t7, {val}\ndiv {reg}, {reg}, t7",
                    _ => "# Unsupported inc op"
                };
            }

            m = _fullIncrementRegex.Match(expr);
            if (m.Success)
            {
                var op = m.Groups[2].Value;
                var val = int.Parse(m.Groups[3].Value);
                return op switch
                {
                    "+" => $"addi {reg}, {reg}, {val}",
                    "-" => $"addi {reg}, {reg}, -{val}",
                    "*" => $"mul {reg}, {reg}, {val}",
                    "/" => $"li t7, {val}\ndiv {reg}, {reg}, t7",
                    _ => "# Unsupported inc op"
                };
            }

            return "# Unsupported inc expr";
        }


        public static string ConvertLoops(string cCode)
        {
            ResetState();
            var normalized = _whitespaceRegex.Replace(cCode, " ").Trim();
            if (_forLoopRegex.IsMatch(normalized))
                return ConvertForLoop(normalized);
            if (_doWhileLoopRegex.IsMatch(normalized)) // yukarıya aldım
                return ConvertDoWhileLoop(normalized);
            if (_whileLoopRegex.IsMatch(normalized))
                return ConvertWhileLoop(normalized);
            return "# Unsupported loop type";
        }

        private static string ConvertForLoop(string cCode)
        {
            var m = _forLoopRegex.Match(cCode);
            if (!m.Success)
                return "# Invalid for syntax";

            var init = m.Groups[1].Value.Trim();
            var cond = m.Groups[2].Value.Trim();
            var inc = m.Groups[3].Value.Trim();
            var body = m.Groups[4].Success ? m.Groups[4].Value.Trim() : string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"# C Kodu: for ({init}; {cond}; {inc})");
            if (!string.IsNullOrEmpty(body))
                sb.AppendLine($"# Loop body: {body}");

            // init
            var im = _initRegex.Match(init);
            var varName = im.Groups[1].Value;
            var startVal = im.Groups[2].Value;
            var reg = GetRegisterForVar(varName);
            sb.AppendLine($"li {reg}, {startVal}    # {varName} init");

            // labels
            var startLbl = $"loop_start_{_labelCounter}";
            _labelCounter++;
            var endLbl = $"loop_end_{_labelCounter}";

            sb.AppendLine(startLbl + ":");

            // condition
            sb.AppendLine(ParseComplexCondition(cond, endLbl, jumpIfFalse: true));

            sb.AppendLine("    # Loop body instructions here");

            // increment
            var incInstr = ParseIncrement(inc, GetRegisterForVar(varName));
            sb.AppendLine($"    {incInstr}    # inc");
            sb.AppendLine($"    j {startLbl}");
            sb.AppendLine(endLbl + ":");

            return sb.ToString();
        }

        private static string ConvertWhileLoop(string cCode)
        {
            var m = _whileLoopRegex.Match(cCode);
            if (!m.Success)
                return "# Invalid while syntax";

            var cond = m.Groups[1].Value.Trim();
            var body = m.Groups[2].Success ? m.Groups[2].Value.Trim() : string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"# C Kodu: while ({cond})");
            if (!string.IsNullOrEmpty(body))
                sb.AppendLine($"# Loop body: {body}");

            // Döngü değişkenini bulmak için condition ifadesinden değişken ismini al
            var condVarMatch = _conditionRegex.Match(cond);
            if (!condVarMatch.Success)
                return "# Unsupported or invalid condition in while loop";

            var varName = condVarMatch.Groups[1].Value;
            var reg = GetRegisterForVar(varName);

            // Döngü değişkeni için başlangıç ataması (0 olarak varsayıldı, istenirse geliştirilebilir)
            sb.AppendLine($"li {reg}, 0    # {varName} init");

            var startLbl = $"while_start_{_labelCounter}";
            _labelCounter++;
            var endLbl = $"while_end_{_labelCounter}";

            sb.AppendLine(startLbl + ":");
            sb.AppendLine(ParseComplexCondition(cond, endLbl, jumpIfFalse: true));

            // Döngü gövdesi
            sb.AppendLine("    # Loop body instructions here");

            // Döngü gövdesindeki artış/azalış ifadeleri işleniyor
            if (!string.IsNullOrEmpty(body))
            {
                var exprs = body.Trim('{', '}').Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var expr in exprs)
                {
                    var trim = expr.Trim();
                    // Artış/azalış veya ++/-- kontrolü
                    if (_incrementRegex.IsMatch(trim) || _fullIncrementRegex.IsMatch(trim) || trim.EndsWith("++") || trim.EndsWith("--"))
                    {
                        // Burada mutlaka döngü değişkeni registeri kullanılmalı
                        var instr = ParseIncrement(trim, reg);
                        sb.AppendLine($"    {instr}    # {trim}");
                    }
                }
            }

            sb.AppendLine($"    j {startLbl}");
            sb.AppendLine(endLbl + ":");
            return sb.ToString();
        }



        private static string ConvertDoWhileLoop(string cCode)
        {
            var m = _doWhileLoopRegex.Match(cCode);
            if (!m.Success)
                return "# Invalid do-while syntax";

            var rawBody = m.Groups[1].Value;  // "{ i = i + 2 }"
            var cond = m.Groups[2].Value.Trim();  // "i < 7"

            var body = rawBody.Trim('{', '}').Trim();

            var sb = new StringBuilder();
            sb.AppendLine($"# C Kodu: do {{ {body} }} while ({cond});");

            var initMatch = _initRegex.Match(body);
            if (initMatch.Success)
            {
                var varName = initMatch.Groups[1].Value;
                var initVal = initMatch.Groups[2].Value;
                var regInit = GetRegisterForVar(varName);
                sb.AppendLine($"li {regInit}, {initVal}    # {varName} başlangıç");
            }

            var startLbl = $"do_while_start_{_labelCounter++}";
            var endLbl = $"do_while_end_{_labelCounter}";
            sb.AppendLine(startLbl + ":");

            if (!string.IsNullOrWhiteSpace(body))
            {
                var stmts = body.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var stmt in stmts)
                {
                    var s = stmt.Trim();
                    if (_incrementRegex.IsMatch(s)
                     || _fullIncrementRegex.IsMatch(s)
                     || s.EndsWith("++")
                     || s.EndsWith("--"))
                    {
                        var varMatch = _initRegex.Match(s);
                        var varName = varMatch.Success
                                       ? varMatch.Groups[1].Value
                                       : s.Split(new[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries)[0];
                        var reg = GetRegisterForVar(varName);
                        var instr = ParseIncrement(s, reg);
                        sb.AppendLine($"    {instr}    # {s}");
                    }
                    else
                    {
                        sb.AppendLine("    # Loop body instructions here");
                    }
                }
            }

            var cm = _conditionRegex.Match(cond);
            if (cm.Success)
            {
                var varC = cm.Groups[1].Value;
                var op = cm.Groups[2].Value;
                var val = cm.Groups[3].Value;
                var regC = GetRegisterForVar(varC);
                var br = GetBranchOp(op, negate: false);
                sb.AppendLine($"    {br} {regC}, {val}, {startLbl}    # {varC} {op} {val}");
            }
            else
            {
                sb.AppendLine("    # Invalid do-while condition");
            }

            sb.AppendLine(endLbl + ":");

            return sb.ToString();
        }







        private static string ParseComplexCondition(string condExpr, string jumpLabel, bool jumpIfFalse)
        {
            var sb = new StringBuilder();
            var orParts = condExpr.Split("||", StringSplitOptions.RemoveEmptyEntries);

            if (orParts.Length > 1)
            {
                foreach (var orPart in orParts)
                {
                    var andParts = orPart.Split("&&", StringSplitOptions.RemoveEmptyEntries);
                    var skipLabel = $"skip_{_labelCounter++}";
                    foreach (var andExpr in andParts)
                    {
                        var m = _conditionRegex.Match(andExpr.Trim());
                        var reg = GetRegisterForVar(m.Groups[1].Value);
                        var op = m.Groups[2].Value;
                        var val = m.Groups[3].Value;
                        var br = GetBranchOp(op, negate: true);
                        sb.AppendLine($"    {br} {reg}, {val}, {skipLabel}    # {andExpr.Trim()}");
                    }
                    sb.AppendLine($"    j {(jumpIfFalse ? jumpLabel : skipLabel)}");
                    sb.AppendLine(skipLabel + ":");
                }

                if (jumpIfFalse)
                    sb.AppendLine($"    j {jumpLabel}");
            }
            else
            {
                var andParts = condExpr.Split("&&", StringSplitOptions.RemoveEmptyEntries);
                foreach (var andExpr in andParts)
                {
                    var m = _conditionRegex.Match(andExpr.Trim());
                    var reg = GetRegisterForVar(m.Groups[1].Value);
                    var op = m.Groups[2].Value;
                    var val = m.Groups[3].Value;
                    var br = GetBranchOp(op, negate: true);
                    sb.AppendLine($"    {br} {reg}, {val}, {jumpLabel}    # {andExpr.Trim()}");
                }
            }

            return sb.ToString();
        }
    }
}
