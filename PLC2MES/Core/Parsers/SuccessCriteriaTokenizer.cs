using System;
using System.Collections.Generic;
using PLC2MES.Core.Models;

namespace PLC2MES.Core.Parsers
{
    /// <summary>
    /// 词法分析器：把原始表达式拆成 Token 列表，供 Parser 使用。
    /// 单独拆出来的好处是，语法扩展时不必改 Parser 的细节。
    /// </summary>
    public class SuccessCriteriaTokenizer
    {
        public List<Token> Tokenize(string expr)
        {
            var tokens = new List<Token>();
            int i = 0;

            while (i < expr.Length)
            {
                if (char.IsWhiteSpace(expr[i])) { i++; continue; }

                if (i < expr.Length - 1)
                {
                    var two = expr.Substring(i, 2);
                    if (two == "&&" || two == "||")
                    {
                        tokens.Add(new Token(TokenType.LogicalOperator, two));
                        i += 2;
                        continue;
                    }

                    if (two == ">=" || two == "<=" || two == "!=")
                    {
                        tokens.Add(new Token(TokenType.Operator, two));
                        i += 2;
                        continue;
                    }
                }

                if (i <= expr.Length - 4)
                {
                    var four = expr.Substring(i, 4);
                    if (four.Equals("like", StringComparison.OrdinalIgnoreCase))
                    {
                        tokens.Add(new Token(TokenType.Operator, "like"));
                        i += 4;
                        continue;
                    }
                }

                if (expr[i] == '=' || expr[i] == '>' || expr[i] == '<' || expr[i] == '!')
                {
                    tokens.Add(new Token(TokenType.Operator, expr[i].ToString()));
                    i++;
                    continue;
                }

                if (expr[i] == '(') { tokens.Add(new Token(TokenType.LeftParen, "(")); i++; continue; }
                if (expr[i] == ')') { tokens.Add(new Token(TokenType.RightParen, ")")); i++; continue; }
                if (expr[i] == '[') { tokens.Add(new Token(TokenType.LeftBracket, "[")); i++; continue; }
                if (expr[i] == ']') { tokens.Add(new Token(TokenType.RightBracket, "]")); i++; continue; }
                if (expr[i] == '.')
                {
                    tokens.Add(new Token(TokenType.Dot, "."));
                    i++;
                    continue;
                }

                if (expr[i] == '"' || expr[i] == '\'')
                {
                    char quote = expr[i];
                    int start = i + 1;
                    i++;
                    while (i < expr.Length && expr[i] != quote) i++;
                    string literal = expr.Substring(start, i - start);
                    tokens.Add(new Token(TokenType.Value, literal));
                    if (i < expr.Length) i++; // skip closing quote
                    continue;
                }

                if (char.IsDigit(expr[i]))
                {
                    int start = i;
                    bool hasDot = false;
                    while (i < expr.Length && (char.IsDigit(expr[i]) || (!hasDot && expr[i] == '.')))
                    {
                        if (expr[i] == '.') hasDot = true;
                        i++;
                    }
                    string number = expr.Substring(start, i - start);
                    tokens.Add(new Token(TokenType.Value, number));
                    continue;
                }

                if (char.IsLetter(expr[i]) || expr[i] == '$' || expr[i] == '_')
                {
                    int start = i;
                    while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_' || expr[i] == '$')) i++;
                    string word = expr.Substring(start, i - start);

                    if (word.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                        word.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        tokens.Add(new Token(TokenType.Value, word));
                    }
                    else if (double.TryParse(word, out _))
                    {
                        tokens.Add(new Token(TokenType.Value, word));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.Variable, word));
                    }

                    continue;
                }

                throw new Exception($"无法识别的字符 '{expr[i]}'");
            }

            tokens.Add(new Token(TokenType.End, string.Empty));
            return tokens;
        }
    }
}
