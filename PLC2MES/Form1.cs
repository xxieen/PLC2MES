using System;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using PLC2MES.Core.Services;
using PLC2MES.Core.Models;
using PLC2MES.Utils;

namespace PLC2MES
{
    public partial class Form1 : Form
    {
        private HttpTestService _service;
        private readonly string _userDefaultsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_defaults.json");
        private Dictionary<string, string> _userDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Form1()
        {
            InitializeComponent();
            InitializeApp();
        }

        private void InitializeApp()
        {
            // create a VariableManager and pass into service (no parameterless constructors)
            var vm = new VariableManager();
            _service = new HttpTestService(vm);

            // subscribe to variable manager events to auto-refresh response variables
            vm.VariableChanged += Vm_VariableChanged;
            vm.VariableRegistered += Vm_VariableRegistered;

            btnParseTemplates.Click += BtnParseTemplates_Click;
            btnSendRequest.Click += BtnSendRequest_Click;
            InitializeGrids();

            LoadUserDefaults();

            // keyboard shortcuts
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.FormClosing += Form1_FormClosing;
        }

        private void Vm_VariableRegistered(object sender, string varName)
        {
            // add new response variable row if needed
            try
            {
                var vm = _service.GetVariableManager();
                var v = vm.GetVariable(varName);
                if (v == null) return;
                if (v.Source != VariableSource.Response) return;

                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => EnsureResponseRow(v)));
                    return;
                }

                EnsureResponseRow(v);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling VariableRegistered", ex);
            }
        }

        private void Vm_VariableChanged(object sender, VariableChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(e?.Name)) return;
                var vm = _service.GetVariableManager();
                var v = vm.GetVariable(e.Name);
                if (v == null) return;
                if (v.Source != VariableSource.Response) return;

                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateResponseRowValue(v)));
                    return;
                }

                UpdateResponseRowValue(v);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling VariableChanged", ex);
            }
        }

        private string GetDisplayType(Variable v)
        {
            if (v == null) return string.Empty;
            return v.IsArray ? $"Array<{v.Type}>" : v.Type.ToString();
        }

        private void EnsureResponseRow(Variable v)
        {
            // find existing row by variable name
            foreach (DataGridViewRow row in dgvResponseVariables.Rows)
            {
                if (row.Tag is Variable existing && string.Equals(existing.Name, v.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // update tag and cells
                    row.Tag = v;
                    row.Cells["VariableType"].Value = GetDisplayType(v);
                    row.Cells["VariableValue"].Value = v.GetFormattedValue();
                    return;
                }
            }

            // not found -> add
            int idx = dgvResponseVariables.Rows.Add(v.Name, GetDisplayType(v), v.GetFormattedValue(), "", v.HasUserDefault ? v.UserDefaultValue?.ToString() : "");
            dgvResponseVariables.Rows[idx].Tag = v;
        }

        private void UpdateResponseRowValue(Variable v)
        {
            foreach (DataGridViewRow row in dgvResponseVariables.Rows)
            {
                if (row.Tag is Variable existing && string.Equals(existing.Name, v.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // avoid overwriting user-edited cell if it currently has focus
                    if (dgvResponseVariables.CurrentCell != null && dgvResponseVariables.CurrentCell.OwningRow == row && dgvResponseVariables.CurrentCell.OwningColumn.Name == "VariableValue")
                    {
                        // user is editing this cell — skip update
                        return;
                    }

                    row.Tag = v;
                    row.Cells["VariableValue"].Value = v.GetFormattedValue();
                    return;
                }
            }

            // if not found, add new row
            EnsureResponseRow(v);
        }

        private void LoadUserDefaults()
        {
            try
            {
                if (File.Exists(_userDefaultsPath))
                {
                    var txt = File.ReadAllText(_userDefaultsPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(txt);
                    if (dict != null) _userDefaults = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load user defaults", ex);
            }
        }

        private void SaveUserDefaults()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_userDefaults);
                File.WriteAllText(_userDefaultsPath, txt);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save user defaults", ex);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveUserDefaults();
        }

        private void InitializeGrids()
        {
            dgvRequestVariables.Columns.Clear();
            dgvRequestVariables.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvRequestVariables.AllowUserToAddRows = false;
            dgvRequestVariables.AllowUserToDeleteRows = false;
            dgvRequestVariables.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgvRequestVariables.Columns.Add("VariableName", "变量名");
            dgvRequestVariables.Columns.Add("VariableType", "类型");
            dgvRequestVariables.Columns.Add("VariableValue", "值");
            dgvRequestVariables.Columns.Add("FormatString", "格式");
            dgvRequestVariables.Columns.Add("UserDefault", "用户默认");

            dgvResponseVariables.Columns.Clear();
            dgvResponseVariables.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvResponseVariables.AllowUserToAddRows = false;
            dgvResponseVariables.AllowUserToDeleteRows = false;
            dgvResponseVariables.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvResponseVariables.ReadOnly = false; // allow editing default

            dgvResponseVariables.Columns.Add("VariableName", "变量名");
            dgvResponseVariables.Columns.Add("VariableType", "类型");
            dgvResponseVariables.Columns.Add("VariableValue", "值");
            dgvResponseVariables.Columns.Add("JsonPath", "JSON路径");
            dgvResponseVariables.Columns.Add("UserDefault", "用户默认");

            dgvRequestVariables.CellValueChanged += DgvRequestVariables_CellValueChanged;
            dgvRequestVariables.CurrentCellDirtyStateChanged += DgvRequestVariables_CurrentCellDirtyStateChanged;
            dgvResponseVariables.CellValueChanged += DgvResponseVariables_CellValueChanged;
            dgvResponseVariables.CurrentCellDirtyStateChanged += DgvRequestVariables_CurrentCellDirtyStateChanged;

            // open array editor on double click when variable.IsArray
            dgvRequestVariables.CellDoubleClick += DgvVariables_CellDoubleClick;
            dgvResponseVariables.CellDoubleClick += DgvVariables_CellDoubleClick;
        }

        private void DgvVariables_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex <0 || e.ColumnIndex <0) return;
            var grid = sender as DataGridView;
            var colName = grid.Columns[e.ColumnIndex].Name;
            if (colName != "VariableValue" && colName != "UserDefault") return;

            var row = grid.Rows[e.RowIndex];
            if (!(row.Tag is Variable variable)) return;
            if (!variable.IsArray) return;

            string current = row.Cells[colName].Value?.ToString() ?? string.Empty;
            // launch simple array editor dialog
            var edited = ArrayEditorDialog.ShowDialog(this, current);
            if (edited == null) return; // cancelled

            // update cell and variable
            row.Cells[colName].Value = edited;
            try
            {
                if (colName == "VariableValue")
                {
                    variable.TrySetValue(edited);
                }
                else if (colName == "UserDefault")
                {
                    if (string.IsNullOrWhiteSpace(edited))
                    {
                        variable.ClearUserDefault();
                        _userDefaults.Remove(variable.Name);
                    }
                    else
                    {
                        if (variable.SetUserDefaultFromString(edited))
                        {
                            _userDefaults[variable.Name] = edited;
                            SaveUserDefaults();
                        }
                        else
                        {
                            MessageBox.Show($"默认值 '{edited}' 无法转换为类型 {variable.Type}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            row.Cells["UserDefault"].Value = variable.HasUserDefault ? variable.UserDefaultValue?.ToString() : string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数组编辑失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvRequestVariables_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            // Commit edit immediately to trigger CellValueChanged when editing checkbox-like cells in future
            if (dgvRequestVariables.IsCurrentCellDirty)
            {
                dgvRequestVariables.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
            if (dgvResponseVariables.IsCurrentCellDirty)
            {
                dgvResponseVariables.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void BtnParseTemplates_Click(object sender, EventArgs e)
        {
            try
            {
                _service.Reset();
                _service.SetBaseUrl(txtBaseUrl.Text.Trim());
                _service.LoadRequestTemplate(txtRequestTemplate.Text);
                _service.LoadResponseTemplate(txtResponseTemplate.Text);
                _service.LoadSuccessCriteria(txtSuccessCriteria.Text);
                RefreshVariableGrids();
                lblStatus.Text = "状态:解析成功";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解析失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "状态:解析失败";
            }
        }

        private void RefreshVariableGrids()
        {
            var vm = _service.GetVariableManager();
            dgvRequestVariables.Rows.Clear();
            foreach (var v in vm.GetRequestVariables())
            {
                // apply persisted user default if exists and not already set by user
                if (!v.HasUserDefault && _userDefaults.TryGetValue(v.Name, out var ud))
                {
                    v.SetUserDefaultFromString(ud);
                }
                var idx = dgvRequestVariables.Rows.Add(v.Name, GetDisplayType(v), v.GetFormattedValue(), v.FormatString ?? "", v.HasUserDefault ? v.UserDefaultValue?.ToString() : "");
                dgvRequestVariables.Rows[idx].Tag = v;
            }
            dgvResponseVariables.Rows.Clear();
            foreach (var v in vm.GetResponseVariables())
            {
                if (!v.HasUserDefault && _userDefaults.TryGetValue(v.Name, out var ud))
                {
                    v.SetUserDefaultFromString(ud);
                }
                var idx = dgvResponseVariables.Rows.Add(v.Name, GetDisplayType(v), v.GetFormattedValue(), "", v.HasUserDefault ? v.UserDefaultValue?.ToString() : "");
                dgvResponseVariables.Rows[idx].Tag = v;
            }
        }

        private async void BtnSendRequest_Click(object sender, EventArgs e)
        {
            try
            {
                // read values from request grid
                foreach (DataGridViewRow row in dgvRequestVariables.Rows)
                {
                    if (row.Tag is Variable v)
                    {
                        var val = row.Cells["VariableValue"].Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) v.TrySetValue(val);
                    }
                }
                lblStatus.Text = "状态: 执行中...";
                var result = await _service.ExecuteTestAsync();
                DisplayResult(result);
                RefreshVariableGrids();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvRequestVariables_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex <0 || e.ColumnIndex <0) return;
            var colName = dgvRequestVariables.Columns[e.ColumnIndex].Name;
            try
            {
                DataGridViewRow row = dgvRequestVariables.Rows[e.RowIndex];
                Variable variable = row.Tag as Variable;
                if (variable == null) return;
                if (colName == "VariableValue")
                {
                    string newValue = row.Cells["VariableValue"].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(newValue)) return;
                    if (!variable.TrySetValue(newValue))
                    {
                        MessageBox.Show($"值 '{newValue}' 无法转换为类型 {variable.Type}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        row.Cells["VariableValue"].Value = variable.GetFormattedValue();
                    }
                }
                else if (colName == "UserDefault")
                {
                    string def = row.Cells["UserDefault"].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(def))
                    {
                        // clear user default
                        variable.ClearUserDefault();
                        _userDefaults.Remove(variable.Name);
                        SaveUserDefaults();
                    }
                    else
                    {
                        if (!variable.SetUserDefaultFromString(def))
                        {
                            MessageBox.Show($"默认值 '{def}' 无法转换为类型 {variable.Type}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            row.Cells["UserDefault"].Value = variable.HasUserDefault ? variable.UserDefaultValue?.ToString() : string.Empty;
                        }
                        else
                        {
                            // persist
                            _userDefaults[variable.Name] = def;
                            SaveUserDefaults();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置变量值时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvResponseVariables_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex <0 || e.ColumnIndex <0) return;
            var colName = dgvResponseVariables.Columns[e.ColumnIndex].Name;
            try
            {
                DataGridViewRow row = dgvResponseVariables.Rows[e.RowIndex];
                Variable variable = row.Tag as Variable;
                if (variable == null) return;
                if (colName == "VariableValue")
                {
                    string newValue = row.Cells["VariableValue"].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(newValue)) return;
                    if (!variable.TrySetValue(newValue))
                    {
                        MessageBox.Show($"值 '{newValue}' 无法转换为类型 {variable.Type}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        row.Cells["VariableValue"].Value = variable.GetFormattedValue();
                    }
                }
                else if (colName == "UserDefault")
                {
                    string def = row.Cells["UserDefault"].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(def))
                    {
                        // clear user default
                        variable.ClearUserDefault();
                        _userDefaults.Remove(variable.Name);
                        SaveUserDefaults();
                    }
                    else
                    {
                        if (!variable.SetUserDefaultFromString(def))
                        {
                            MessageBox.Show($"默认值 '{def}' 无法转换为类型 {variable.Type}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            row.Cells["UserDefault"].Value = variable.HasUserDefault ? variable.UserDefaultValue?.ToString() : string.Empty;
                        }
                        else
                        {
                            _userDefaults[variable.Name] = def;
                            SaveUserDefaults();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置变量值时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayResult(TestResult result)
        {
            lblStatus.Text = result.Success ? "状态: ✓ 测试成功" : "状态: ✗ 测试失败";
            lblDuration.Text = $"耗时: {result.DurationMs} ms";
            rtbRequest.Text = result.RequestText ?? "";
            rtbResponse.Text = result.ResponseText ?? "";
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) { BtnParseTemplates_Click(null, null); e.Handled = true; }
            else if (e.KeyCode == Keys.F6) { BtnSendRequest_Click(null, null); e.Handled = true; }
        }
    }
}
