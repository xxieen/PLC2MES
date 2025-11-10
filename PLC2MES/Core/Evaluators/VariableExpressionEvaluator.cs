using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using PLC2MES.Core.Models;

namespace PLC2MES.Core.Evaluators
{
    /// <summary>
    /// 负责把 VariableExpression（变量 + 访问器链）解析成实际值和类型。
    /// 单一职责：只处理“如何从变量字典中取值”，不参与比较逻辑。
    /// </summary>
    public class VariableExpressionEvaluator
    {
        public bool TryEvaluate(VariableExpression expression, IDictionary<string, Variable> variables, out object value, out VariableType resolvedType, out string errorMessage)
        {
            value = null;
            resolvedType = null;
            errorMessage = null;

            if (expression == null || string.IsNullOrWhiteSpace(expression.BaseName))
            {
                errorMessage = "变量表达式为空";
                return false;
            }

            if (!variables.TryGetValue(expression.BaseName, out var variable) || variable == null)
            {
                errorMessage = $"变量 {expression.BaseName} 不存在";
                return false;
            }

            value = variable.Value;
            resolvedType = variable.Type;

            foreach (var segment in expression.Segments)
            {
                switch (segment.Kind)
                {
                    case AccessorSegmentKind.Property:
                        if (!ApplyProperty(segment.PropertyName, ref value, ref resolvedType, out errorMessage))
                            return false;
                        break;
                    case AccessorSegmentKind.Index:
                        if (!segment.Index.HasValue)
                        {
                            errorMessage = "数组访问缺少下标";
                            return false;
                        }
                        if (!ApplyIndex(segment.Index.Value, ref value, ref resolvedType, out errorMessage))
                            return false;
                        break;
                    default:
                        errorMessage = $"未知的访问器类型: {segment.Kind}";
                        return false;
                }
            }

            if (value == null)
            {
                errorMessage = $"变量 {expression} 解析结果为空";
                return false;
            }

            return true;
        }

        private bool ApplyProperty(string propertyName, ref object currentValue, ref VariableType currentType, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(propertyName))
            {
                errorMessage = "属性名不能为空";
                return false;
            }

            if (!propertyName.Equals("count", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"暂不支持属性 {propertyName}";
                return false;
            }

            if (currentValue == null)
            {
                errorMessage = "无法在 null 上获取 Count";
                return false;
            }

            if (currentValue is JsonArray jsonArray)
            {
                currentValue = jsonArray.Count;
            }
            else if (currentValue is ICollection coll)
            {
                currentValue = coll.Count;
            }
            else if (currentValue is IEnumerable enumerable)
            {
                int count = 0;
                foreach (var _ in enumerable) count++;
                currentValue = count;
            }
            else
            {
                errorMessage = $"类型 {currentValue.GetType().Name} 不支持 Count";
                return false;
            }

            currentType = VariableType.CreateScalar(VariableKind.Int);
            return true;
        }

        private bool ApplyIndex(int index, ref object currentValue, ref VariableType currentType, out string errorMessage)
        {
            errorMessage = null;
            if (index < 0)
            {
                errorMessage = "数组下标不能为负数";
                return false;
            }

            if (currentValue == null)
            {
                errorMessage = "无法在 null 上取索引";
                return false;
            }

            object resolved = null;
            bool found = false;

            switch (currentValue)
            {
                case JsonArray jsonArray:
                    if (index >= jsonArray.Count)
                    {
                        errorMessage = $"JsonArray 下标越界（{index}）";
                        return false;
                    }
                    resolved = jsonArray[index];
                    found = true;
                    break;

                case IList list:
                    if (index >= list.Count)
                    {
                        errorMessage = $"List 下标越界（{index}）";
                        return false;
                    }
                    resolved = list[index];
                    found = true;
                    break;

                default:
                    if (currentValue.GetType().IsArray)
                    {
                        var arr = (Array)currentValue;
                        if (index >= arr.Length)
                        {
                            errorMessage = $"数组下标越界（{index}）";
                            return false;
                        }
                        resolved = arr.GetValue(index);
                        found = true;
                    }
                    else if (currentValue is IEnumerable enumerable)
                    {
                        int i = 0;
                        foreach (var item in enumerable)
                        {
                            if (i == index)
                            {
                                resolved = item;
                                found = true;
                                break;
                            }
                            i++;
                        }
                        if (!found)
                        {
                            errorMessage = $"Enumerable 在下标 {index} 处没有元素";
                            return false;
                        }
                    }
                    else
                    {
                        errorMessage = $"类型 {currentValue.GetType().Name} 不支持索引访问";
                        return false;
                    }
                    break;
            }

            currentValue = resolved;
            if (currentType != null && currentType.IsArray)
                currentType = currentType.ElementType;

            return true;
        }
    }
}
