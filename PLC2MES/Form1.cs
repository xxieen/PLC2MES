using System;
using System.Linq;
using System.Windows.Forms;
using PLC2MES.Core.Services;
using PLC2MES.Core.Models;

namespace PLC2MES
{
    public partial class Form1 : Form
    {
        private HttpTestService _service;
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
        }

        private void InitializeGrids()
        {
            dgvRequestVariables.Columns.Clear();
            dgvRequestVariables.Columns.Add("VariableName", "变量名");
            dgvRequestVariables.Columns.Add("VariableType", "类型");
            dgvRequestVariables.Columns.Add("VariableValue", "值");
            dgvRequestVariables.Columns.Add("FormatString", "格式");

            dgvResponseVariables.Columns.Clear();
            dgvResponseVariables.Columns.Add("VariableName", "变量名");
            dgvResponseVariables.Columns.Add("VariableType", "类型");
            dgvResponseVariables.Columns.Add("VariableValue", "值");
            dgvResponseVariables.Columns.Add("JsonPath", "JSON路径");
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
                var idx = dgvRequestVariables.Rows.Add(v.Name, v.Type.ToString(), v.Value?.ToString() ?? "", v.FormatString ?? "");
                dgvRequestVariables.Rows[idx].Tag = v;
            }
            dgvResponseVariables.Rows.Clear();
            foreach (var v in vm.GetResponseVariables())
            {
                var idx = dgvResponseVariables.Rows.Add(v.Name, v.Type.ToString(), v.Value?.ToString() ?? "", "");
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

        private void DisplayResult(TestResult result)
        {
            lblStatus.Text = result.Success ? "状态: ✓ 测试成功" : "状态: ✗ 测试失败";
            lblDuration.Text = $"耗时: {result.DurationMs} ms";
            rtbRequest.Text = result.RequestText ?? "";
            rtbResponse.Text = result.ResponseText ?? "";
        }
    }
}
