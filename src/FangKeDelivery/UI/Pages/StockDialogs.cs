using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

/// <summary>单行文本输入对话框。用于安全库存、审核意见等轻量输入。</summary>
public sealed class PromptDialog : Form
{
    private readonly TextBox _input = Theme.Input();
    public string Value => _input.Text.Trim();

    private PromptDialog(string title, string prompt, string initial, bool multiline)
    {
        Text = title;
        Size = new Size(480, multiline ? 300 : 210);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = Theme.BodyFont;
        BackColor = Color.White;
        Padding = new Padding(20);

        var label = new Label
        {
            Text = prompt,
            Dock = DockStyle.Top,
            Height = multiline ? 50 : 64,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont
        };

        _input.Dock = DockStyle.Top;
        _input.Text = initial;
        if (multiline)
        {
            _input.Multiline = true;
            _input.Height = 90;
            _input.ScrollBars = ScrollBars.Vertical;
        }

        var ok = Theme.PrimaryButton("确定", 88, 32);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.SecondaryButton("取消", 88, 32);
        cancel.DialogResult = DialogResult.Cancel;

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 6, 0, 0)
        };
        bar.Controls.Add(ok);
        bar.Controls.Add(cancel);

        Controls.Add(bar);
        Controls.Add(_input);
        Controls.Add(label);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => { _input.Focus(); _input.SelectAll(); };
    }

    public static string? Ask(IWin32Window? owner, string title, string prompt,
        string initial = "", bool multiline = false)
    {
        using var dlg = new PromptDialog(title, prompt, initial, multiline);
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Value : null;
    }
}

/// <summary>盘点调整：把现存量直接改为盘点实数，差额自动记流水。</summary>
public sealed class StockAdjustDialog : Form
{
    private readonly TextBox _qty = Theme.Input();
    private readonly TextBox _remark = Theme.Input();
    private readonly StockRow _row;

    public decimal NewQty { get; private set; }
    public string Remark => _remark.Text.Trim();

    public StockAdjustDialog(StockRow row, Repository repo)
    {
        _row = row;

        Text = "盘点调整";
        Size = new Size(460, 300);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = Theme.BodyFont;
        BackColor = Color.White;
        Padding = new Padding(20);

        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = Color.White
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 4; i++) t.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        void Row(int i, string label, Control c)
        {
            t.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Theme.TextSub,
                Font = Theme.BodyFont,
                Margin = new Padding(0, 0, 8, 0)
            }, 0, i);
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 5, 0, 5);
            t.Controls.Add(c, 1, i);
        }

        var name = new Label
        {
            Text = string.IsNullOrWhiteSpace(row.Spec) ? row.ProductName : $"{row.ProductName} {row.Spec}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Theme.Font(9.5f, FontStyle.Bold),
            ForeColor = Theme.TextMain
        };
        var current = new Label
        {
            Text = $"{row.Qty:0.###} {row.Unit}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Theme.BodyFont,
            ForeColor = Theme.TextSub
        };

        _qty.Text = row.Qty.ToString("0.###");

        Row(0, "商品", name);
        Row(1, "账面现存", current);
        Row(2, "盘点实数", _qty);
        Row(3, "调整说明", _remark);

        var tip = new Label
        {
            Text = "保存后系统会按「实数 − 账面」记一条盘点流水，库存随之更新。",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSub,
            Font = Theme.Font(8.5f)
        };
        t.Controls.Add(tip, 1, 4);

        var ok = Theme.PrimaryButton("保存", 88, 32);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.SecondaryButton("取消", 88, 32);
        cancel.DialogResult = DialogResult.Cancel;

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 6, 0, 0)
        };
        bar.Controls.Add(ok);
        bar.Controls.Add(cancel);

        Controls.Add(bar);
        Controls.Add(t);
        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, e) =>
        {
            if (!decimal.TryParse(_qty.Text, out var q) || q < 0)
            {
                Msg.Show(this, "盘点实数必须是不小于 0 的数字。", "盘点调整",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                _qty.Focus();
                return;
            }
            NewQty = q;
        };
    }
}
