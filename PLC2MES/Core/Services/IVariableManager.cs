using System;
using System.Collections.Generic;
using PLC2MES.Core.Models;

namespace PLC2MES.Core.Services
{
    public class VariableChangedEventArgs : EventArgs
    {
        public string Name { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }
     
    public interface IVariableManager
    {
        event EventHandler<VariableChangedEventArgs> VariableChanged;
        event EventHandler<string> VariableRegistered;

        void RegisterVariable(Variable variable);
        Variable GetVariable(string name);
        bool SetVariableValue(string name, object value);
        Dictionary<string, Variable> GetAllVariables();
        List<Variable> GetRequestVariables();
        List<Variable> GetResponseVariables();
        bool AreAllRequestVariablesSet();
        List<string> GetUnsetVariableNames();
        void Clear();
        int Count { get; }
    }
}
