using System;
using System.Collections.Generic;
using PLC2MES.Core.Models;

namespace PLC2MES.Core.Parsers
{
    /// <summary>
    /// 递归下降语法解析器：依赖 SuccessCriteriaTokenizer 生成的 token。
    /// 输出结果是一棵 ConditionNode 语法树。
    /// </summary>
    public class SuccessCriteriaParser
    {
        private readonly SuccessCriteriaTokenizer _tokenizer = new SuccessCriteriaTokenizer();
        private List<Token> _tokens;
        private int _position;

        public ConditionNode Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentException("表达式不能为空");
            _tokens = _tokenizer.Tokenize(expression);
            _position = 0;
            return ParseOrExpression();
        }

        private ConditionNode ParseOrExpression()
        {
            var left = ParseAndExpression();
            while (CurrentToken().Type == TokenType.LogicalOperator && CurrentToken().Value == "||")
            {
                Consume();
                var right = ParseAndExpression();
                left = new LogicalOperatorNode { Operator = LogicalOperator.Or, Left = left, Right = right };
            }
            return left;
        }

        private ConditionNode ParseAndExpression()
        {
            var left = ParseUnaryExpression();
            while (CurrentToken().Type == TokenType.LogicalOperator && CurrentToken().Value == "&&")
            {
                Consume();
                var right = ParseUnaryExpression();
                left = new LogicalOperatorNode { Operator = LogicalOperator.And, Left = left, Right = right };
            }
            return left;
        }

        private ConditionNode ParseUnaryExpression()
        {
            if (CurrentToken().Type == TokenType.Operator && CurrentToken().Value == "!")
            {
                Consume();
                var inner = ParseUnaryExpression();
                return new NotNode { Inner = inner };
            }

            return ParsePrimaryExpression();
        }

        private ConditionNode ParsePrimaryExpression()
        {
            if (CurrentToken().Type == TokenType.LeftParen)
            {
                Consume();
                var node = ParseOrExpression();
                if (CurrentToken().Type != TokenType.RightParen) throw new Exception("缺少右括号");
                Consume();
                return node;
            }

            if (CurrentToken().Type != TokenType.Variable)
                throw new Exception($"期望变量，但得到: {CurrentToken().Value}");

            var variableExpr = ParseVariableExpression();

            if (CurrentToken().Type == TokenType.Operator)
            {
                string op = CurrentToken().Value;
                Consume();
                if (CurrentToken().Type != TokenType.Value)
                    throw new Exception($"期望常量值，但得到: {CurrentToken().Value}");

                string valueStr = CurrentToken().Value;
                Consume();

                return new ComparisonNode
                {
                    LeftExpression = variableExpr,
                    Operator = ParseComparisonOperator(op),
                    CompareValue = ParseValue(valueStr)
                };
            }

            return new BooleanVariableNode { Expression = variableExpr };
        }

        private ComparisonOperator ParseComparisonOperator(string op) => op switch
        {
            "=" => ComparisonOperator.Equal,
            ">" => ComparisonOperator.GreaterThan,
            "<" => ComparisonOperator.LessThan,
            ">=" => ComparisonOperator.GreaterOrEqual,
            "<=" => ComparisonOperator.LessOrEqual,
            "!=" => ComparisonOperator.NotEqual,
            "like" => ComparisonOperator.Like,
            _ => throw new Exception($"未知的比较运算符: {op}")
        };

        private object ParseValue(string literal)
        {
            if (literal.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (literal.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (int.TryParse(literal, out var i)) return i;
            if (double.TryParse(literal, out var d)) return d;
            return literal;
        }

        private VariableExpression ParseVariableExpression()
        {
            var expression = new VariableExpression { BaseName = CurrentToken().Value };
            Consume();

            while (true)
            {
                if (CurrentToken().Type == TokenType.Dot)
                {
                    Consume();
                    if (CurrentToken().Type != TokenType.Variable)
                        throw new Exception("点号后需要属性名");
                    expression.Segments.Add(new AccessorSegment
                    {
                        Kind = AccessorSegmentKind.Property,
                        PropertyName = CurrentToken().Value
                    });
                    Consume();
                    continue;
                }

                if (CurrentToken().Type == TokenType.LeftBracket)
                {
                    Consume();
                    if (CurrentToken().Type != TokenType.Value || !int.TryParse(CurrentToken().Value, out var index))
                        throw new Exception("数组访问必须提供数字下标");
                    Consume();
                    if (CurrentToken().Type != TokenType.RightBracket)
                        throw new Exception("缺少 ]");
                    Consume();
                    expression.Segments.Add(new AccessorSegment
                    {
                        Kind = AccessorSegmentKind.Index,
                        Index = index
                    });
                    continue;
                }

                break;
            }

            return expression;
        }

        private Token CurrentToken() => _position >= _tokens.Count ? _tokens[^1] : _tokens[_position];
        private void Consume() { if (_position < _tokens.Count - 1) _position++; }
    }
}
