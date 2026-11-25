using FangKeDelivery.UI;

namespace FangKeDelivery.Printing;

/// <summary>打印份数与联次模式选择。份数指整份单子打几遍，联次决定一份单子走几遍纸。</summary>
public sealed class PrintCopiesDialog : Form
{
    private readonly NumericUpDown _copies = new()
    {
        Minimum = 1,
        Maximum = 9,
        Width = 80,
        Font = Theme.BodyFont,
        TextAlign = HorizontalAlignment.Center
    };

    private readonly RadioButton _carbonCopy = new()
    {
        Text = "复写纸三联（走一遍纸，纸本身复写出三联）",
        AutoSize = true,
        Font = Theme.BodyFont,
        ForeColor = Theme.TextMain
    };

    private readonly RadioButton _singlePly = new()
    {
        Text = "普通纸分三联（走三遍纸，每遍标不同联次）",
        AutoSize = true,
        Font = Theme.BodyFont,
        ForeColor = Theme.TextMain
    };

    private readonly Label _hint = new()
    {
        AutoSize = false,
        Height = 40,
        ForeColor = Theme.TextSub,
        Font = Theme.SmallFont
    };

    public int Copies => (int)_copies.Value;
    public bool CarbonCopy => _carbonCopy.Checked;

    public PrintCopiesDialog(int copies, bool carbonCopy)
    {
        Text = "打印设置";
        Size = new Size(520, 330);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = Theme.BodyFont;
        BackColor = Color.White;
        Padding = new Padding(20, 16, 20, 16);

        _copies.Value = Math.Clamp(copies, 1, 9);
        _carbonCopy.Checked = carbonCopy;
        _singlePly.Checked = !carbonCopy;

        var title = new Label
        {
            Text = "打印设置",
            Dock = DockStyle.Top,
            Height = 30,
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain
        };

        var copiesRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        copiesRow.Controls.Add(new Label
        {
            Text = "打印份数",
            Width = 76,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont,
            Margin = new Padding(0, 4, 0, 0)
        });
        copiesRow.Controls.Add(_copies);
        copiesRow.Controls.Add(new Label
        {
            Text = "份（每份含完整三联）",
            AutoSize = true,
            ForeColor = Theme.TextSub,
            Font = Theme.SmallFont,
            Margin = new Padding(8, 8, 0, 0)
        });

        var plyGroup = new Panel { Dock = DockStyle.Top, Height = 78 };
        var plyTitle = new Label
        {
            Text = "纸张类型",
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont
        };
        _carbonCopy.Location = new Point(0, 26);
        _singlePly.Location = new Point(0, 50);
        plyGroup.Controls.Add(_singlePly);
        plyGroup.Controls.Add(_carbonCopy);
        plyGroup.Controls.Add(plyTitle);

        _hint.Dock = DockStyle.Top;
        _hint.Text = "复写纸：一份单子只打一遍纸。选错成「普通纸分三联」会导致一份单子打三遍。";
        _carbonCopy.CheckedChanged += (_, _) => UpdateHint();
        _copies.ValueChanged += (_, _) => UpdateHint();
        UpdateHint();

        var ok = Theme.PrimaryButton("打印", 88, 32);
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
        Controls.Add(_hint);
        Controls.Add(plyGroup);
        Controls.Add(copiesRow);
        Controls.Add(title);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void UpdateHint()
    {
        var perCopy = _carbonCopy.Checked ? 1 : 3;
        var total = perCopy * Copies;
        _hint.Text = _carbonCopy.Checked
            ? $"复写纸模式：每份走 {perCopy} 遍纸，共 {Copies} 份 → 合计 {total} 页。"
            : $"普通纸模式：每份走 {perCopy} 遍纸，共 {Copies} 份 → 合计 {total} 页。";
    }
}
