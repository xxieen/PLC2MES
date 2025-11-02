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
            _service = new HttpTestService();
            btnParseTemplates.Click += BtnParseTemplates_Click;
            btnSendRequest.Click += BtnSendRequest_Click;
            InitializeGrids();

            LoadUserDefaults();

            // keyboard shortcuts
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.FormClosing += Form1_FormClosing;
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
                var idx = dgvRequestVariables.Rows.Add(v.Name, v.Type.ToString(), v.Value?.ToString() ?? "", v.FormatString ?? "", v.HasUserDefault ? v.UserDefaultValue?.ToString() : "");
                dgvRequestVariables.Rows[idx].Tag = v;
            }
            dgvResponseVariables.Rows.Clear();
            foreach (var v in vm.GetResponseVariables())
            {
                if (!v.HasUserDefault && _userDefaults.TryGetValue(v.Name, out var ud))
                {
                    v.SetUserDefaultFromString(ud);
                }
                var idx = dgvResponseVariables.Rows.Add(v.Name, v.Type.ToString(), v.Value?.ToString() ?? "", "", v.HasUserDefault ? v.UserDefaultValue?.ToString() : "");
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
