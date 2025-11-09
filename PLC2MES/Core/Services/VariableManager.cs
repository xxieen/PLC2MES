using System;
using System.Collections.Generic;
using System.Linq;
using PLC2MES.Core.Models;

namespace PLC2MES.Core.Services
{
    public class VariableManager : IVariableManager
    {
        private readonly Dictionary<string, Variable> _variables = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);
        public event EventHandler<VariableChangedEventArgs> VariableChanged;
        public event EventHandler<string> VariableRegistered;

        public void RegisterVariable(Variable variable)
        {
            // assume caller provides valid variable; minimal checks
            if (variable == null || string.IsNullOrWhiteSpace(variable.Name)) return;
            _variables[variable.Name] = variable;
            VariableRegistered?.Invoke(this, variable.Name);
        }

        public Variable GetVariable(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _variables.TryGetValue(name, out var v);
            return v;
        }

        public bool SetVariableValue(string name, object value)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!_variables.TryGetValue(name, out var v)) return false;
            var old = v.Value;
            v.Value = value;
            VariableChanged?.Invoke(this, new VariableChangedEventArgs { Name = name, OldValue = old, NewValue = value });
            return true;
        }

        public bool TrySetValueFromString(string name, string valueString)
        {
            var v = GetVariable(name);
            if (v == null) return false;
            var ok = v.TrySetValue(valueString);
            if (ok) VariableChanged?.Invoke(this, new VariableChangedEventArgs { Name = name, OldValue = null, NewValue = v.Value });
            return ok;
        }

        public Dictionary<string, Variable> GetAllVariables()
        {
            // return shallow copy
            return new Dictionary<string, Variable>(_variables, StringComparer.OrdinalIgnoreCase);
        }

        public List<Variable> GetRequestVariables()
        {
            return _variables.Values.Where(x => x.Source == VariableSource.Request).OrderBy(x => x.Name).ToList();
        }

        public List<Variable> GetResponseVariables()
        {
            return _variables.Values.Where(x => x.Source == VariableSource.Response).OrderBy(x => x.Name).ToList();
        }

        public bool AreAllRequestVariablesSet()
        {
            foreach (var v in GetRequestVariables())
            {
                if (v.Value == null) return false;
                if (v.Type != null && !v.Type.IsArray && v.Type.Kind == VariableKind.String && string.IsNullOrEmpty(v.Value.ToString())) return false;
            }
            return true;
        }
         
        public List<string> GetUnsetVariableNames()
        {
            return GetRequestVariables().Where(v => v.Value == null || (v.Type != null && !v.Type.IsArray && v.Type.Kind == VariableKind.String && string.IsNullOrEmpty(v.Value.ToString()))).Select(v => v.Name).ToList();
        }

        public void Clear()
        {
            _variables.Clear();
        }

        public int Count => _variables.Count;
    }
}