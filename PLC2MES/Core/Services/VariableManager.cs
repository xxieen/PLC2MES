using System.Collections.Generic;
using System.Linq;
using PLC2MES.Core.Models;

namespace PLC2MES.Core.Services
{
    public class VariableManager
    {
        private Dictionary<string, Variable> _variables = new Dictionary<string, Variable>();

        public void RegisterVariable(Variable variable)
        {
            if (variable == null) return;
            if (string.IsNullOrWhiteSpace(variable.Name)) return;
            _variables[variable.Name] = variable;
        }

        public Variable GetVariable(string name)
        {
            if (_variables.ContainsKey(name)) return _variables[name];
            return null;
        }

        public bool SetVariableValue(string name, object value)
        {
            if (!_variables.ContainsKey(name)) return false;
            _variables[name].Value = value;
            return true;
        }

        public Dictionary<string, Variable> GetAllVariables()
        {
            return new Dictionary<string, Variable>(_variables);
        }

        public List<Variable> GetRequestVariables()
        {
            return _variables.Values.Where(v => v.Source == VariableSource.Request).OrderBy(v => v.Name).ToList();
        }

        public List<Variable> GetResponseVariables()
        {
            return _variables.Values.Where(v => v.Source == VariableSource.Response).OrderBy(v => v.Name).ToList();
        }

        public bool AreAllRequestVariablesSet()
        {
            foreach (var v in GetRequestVariables())
            {
                if (v.Value == null) return false;
                if (v.Type == VariableType.String && string.IsNullOrEmpty(v.Value.ToString())) return false;
            }
            return true;
        }

        public List<string> GetUnsetVariableNames()
        {
            var list = new List<string>();
            foreach (var v in GetRequestVariables())
            {
                if (v.Value == null || (v.Type == VariableType.String && string.IsNullOrEmpty(v.Value.ToString()))) list.Add(v.Name);
            }
            return list;
        }

        public void Clear() { _variables.Clear(); }
        public int Count => _variables.Count;
    }
}