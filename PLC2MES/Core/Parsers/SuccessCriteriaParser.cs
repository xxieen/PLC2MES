using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PLC2MES.Core.Models;

namespace PLC2MES.Core.Parsers
{
    public class SuccessCriteriaParser
    {
        private List<Token> _tokens;
        private int _position;

        public ConditionNode Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentException("表达式不能为空");
            _tokens = Tokenize(expression);
            _position = 0;
            return ParseOrExpression();
        }

        private List<Token> Tokenize(string expr)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < expr.Length)
            {
                if (char.IsWhiteSpace(expr[i])) { i++; continue; }
                if (i < expr.Length - 1)
                {
                    var two = expr.Substring(i, 2);
                    if (two == "&&" || two == "||") { tokens.Add(new Token(TokenType.LogicalOperator, two)); i += 2; continue; }
                }
                if (i < expr.Length - 1)
                {
                    var two = expr.Substring(i, 2);
                    if (two == ">=" || two == "<=") { tokens.Add(new Token(TokenType.Operator, two)); i += 2; continue; }
                }
                if (i <= expr.Length - 4)
                {
                    var four = expr.Substring(i, 4);
                    if (four.Equals("like", StringComparison.OrdinalIgnoreCase)) { tokens.Add(new Token(TokenType.Operator, "like")); i += 4; continue; }
                }
                if (expr[i] == '=' || expr[i] == '>' || expr[i] == '<') { tokens.Add(new Token(TokenType.Operator, expr[i].ToString())); i++; continue; }
                if (expr[i] == '(') { tokens.Add(new Token(TokenType.LeftParen, "(")); i++; continue; }
                if (expr[i] == ')') { tokens.Add(new Token(TokenType.RightParen, ")")); i++; continue; }
                if (expr[i] == '"' || expr[i] == '\'')
                {
                    char q = expr[i]; int start = i + 1; i++;
                    while (i < expr.Length && expr[i] != q) i++;
                    if (i < expr.Length) { string val = expr.Substring(start, i - start); tokens.Add(new Token(TokenType.Value, val)); i++; continue; }
                }
                if (char.IsLetterOrDigit(expr[i]) || expr[i] == '$' || expr[i] == '_')
                {
                    int start = i;
                    while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_' || expr[i] == '$' || expr[i] == '.')) i++;
                    string word = expr.Substring(start, i - start);
                    if (word == "true" || word == "false" || double.TryParse(word, out _)) tokens.Add(new Token(TokenType.Value, word)); else tokens.Add(new Token(TokenType.Variable, word));
                    continue;
                }
                i++;
            }
            tokens.Add(new Token(TokenType.End, ""));
            return tokens;
        }

        private ConditionNode ParseOrExpression()
        {
            var left = ParseAndExpression();
            while (CurrentToken().Type == TokenType.LogicalOperator && CurrentToken().Value == "||")
            {
                Consume(); var right = ParseAndExpression(); left = new LogicalOperatorNode { Operator = LogicalOperator.Or, Left = left, Right = right };
            }
            return left;
        }

        private ConditionNode ParseAndExpression()
        {
            var left = ParseComparisonExpression();
            while (CurrentToken().Type == TokenType.LogicalOperator && CurrentToken().Value == "&&")
            {
                Consume(); var right = ParseComparisonExpression(); left = new LogicalOperatorNode { Operator = LogicalOperator.And, Left = left, Right = right };
            }
            return left;
        }

        private ConditionNode ParseComparisonExpression()
        {
            if (CurrentToken().Type == TokenType.LeftParen) { Consume(); var node = ParseOrExpression(); if (CurrentToken().Type == TokenType.RightParen) Consume(); return node; }
            if (CurrentToken().Type != TokenType.Variable) throw new Exception($"期望变量，但得到: {CurrentToken().Value}");
            string variableName = CurrentToken().Value; Consume();
            if (CurrentToken().Type == TokenType.Operator)
            {
                string op = CurrentToken().Value; Consume(); if (CurrentToken().Type != TokenType.Value) throw new Exception($"期望值，但得到: {CurrentToken().Value}"); string valueStr = CurrentToken().Value; Consume();
                return new ComparisonNode { VariableName = variableName, Operator = ParseComparisonOperator(op), CompareValue = ParseValue(valueStr) };
            }
            else
            {
                return new BooleanVariableNode { VariableName = variableName };
            }
        }

        private ComparisonOperator ParseComparisonOperator(string op)
        {
            return op switch
            {
                "=" => ComparisonOperator.Equal,
                ">" => ComparisonOperator.GreaterThan,
                "<" => ComparisonOperator.LessThan,
                ">=" => ComparisonOperator.GreaterOrEqual,
                "<=" => ComparisonOperator.LessOrEqual,
                "like" => ComparisonOperator.Like,
                _ => throw new Exception($"未知的比较运算符: {op}"),
            };
        }

        private object ParseValue(string s)
        {
            if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (int.TryParse(s, out var i)) return i;
            if (double.TryParse(s, out var d)) return d;
            return s;
        }

        private Token CurrentToken() => _position >= _tokens.Count ? _tokens[^1] : _tokens[_position];
        private void Consume() { if (_position < _tokens.Count - 1) _position++; }
    }
}
