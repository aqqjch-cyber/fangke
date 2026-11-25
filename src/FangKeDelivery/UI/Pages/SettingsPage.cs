using FangKeDelivery.Core;
using FangKeDelivery.Data;
using FangKeDelivery.Printing;

namespace FangKeDelivery.UI.Pages;

public sealed class SettingsPage : UserControl, IRefreshable
{
    private readonly Repository _repo;
    private readonly TextBox _company = Theme.Input();
    private readonly TextBox _phone = Theme.Input();
    private readonly TextBox _address = Theme.Input();
    private readonly TextBox _maker = Theme.Input();
    private readonly TextBox _salesman = Theme.Input();
    private readonly TextBox _footer = Theme.Input();
    private readonly TextBox _fax = Theme.Input();
    private readonly CheckBox _prePrinted = new()
    {
        Text = "按预印单（241-1）位置排版：内容靠左，右侧留白对齐预印框",
        AutoSize = true,
        Font = Theme.BodyFont,
        ForeColor = Theme.TextSub
    };
    private readonly CheckBox _carbonCopy = new()
    {
        Text = "复写纸三联：一份单子只打一遍纸（纸本身复写出三联）",
        AutoSize = true,
        Font = Theme.BodyFont,
        ForeColor = Theme.TextSub
    };
    private readonly NumericUpDown _copies = new()
    {
        Minimum = 1,
        Maximum = 9,
        Width = 80,
        Font = Theme.BodyFont,
        TextAlign = HorizontalAlignment.Center
    };
    private readonly Label _dbPath = new();
    private readonly Label _dbSize = new();
    private readonly Label _counts = new();

    public SettingsPage(Repository repo)
    {
        _repo = repo;
        Dock = DockStyle.Fill;
        BackColor = Theme.PageBack;
        BuildUi();
        Reload();
    }

    private void BuildUi()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.PageBack };
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.PageBack,
            Padding = new Padding(0)
        };

        stack.Controls.Add(BuildCompanyCard());
        stack.Controls.Add(BuildDataCard());
        Controls.Add(scroll);
        scroll.Controls.Add(stack);
        scroll.Resize += (_, _) =>
        {
            stack.Width = scroll.ClientSize.Width - 4;
            foreach (Control c in stack.Controls)
                c.Width = stack.Width - 6;
        };
    }

    private Control BuildCompanyCard()
    {
        var card = new Theme.RoundedPanel
        {
            Height = 530,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(20, 16, 20, 16)
        };

        var title = new Label
        {
            Text = "公司信息（用于送货单抬头）",
            Dock = DockStyle.Top,
            Height = 30,
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain
        };

        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 424,
            ColumnCount = 2,
            RowCount = 11,
            BackColor = Color.White
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 10; i++)
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

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

        Row(0, "公司名称", _company);
        Row(1, "联系电话", _phone);
        Row(2, "传真", _fax);
        Row(3, "公司地址", _address);
        Row(4, "制单人", _maker);
        Row(5, "默认送货员", _salesman);
        Row(6, "单据页脚说明", _footer);
        Row(7, "打印版式", _prePrinted);
        Row(8, "纸张类型", _carbonCopy);

        var copiesBox = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 5, 0, 5)
        };
        copiesBox.Controls.Add(_copies);
        copiesBox.Controls.Add(new Label
        {
            Text = "份（每份含完整三联）",
            AutoSize = true,
            ForeColor = Theme.TextSub,
            Font = Theme.SmallFont,
            Margin = new Padding(8, 8, 0, 0)
        });
        Row(9, "默认打印份数", copiesBox);

        var save = Theme.PrimaryButton("保存设置", 100, 34);
        save.Click += (_, _) => SaveSettings();
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(96, 8, 0, 0)
        };
        bar.Controls.Add(save);

        card.Controls.Add(bar);
        card.Controls.Add(t);
        card.Controls.Add(title);
        return card;
    }

    private Control BuildDataCard()
    {
        var card = new Theme.RoundedPanel
        {
            Height = 300,
            Margin = new Padding(0),
            Padding = new Padding(20, 16, 20, 16)
        };

        var title = new Label
        {
            Text = "数据与备份",
            Dock = DockStyle.Top,
            Height = 30,
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain
        };

        var info = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Color.White };

        var lblPathCap = new Label
        {
            Text = "数据库文件：",
            AutoSize = true,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont,
            Location = new Point(0, 6)
        };
        _dbPath.AutoSize = true;
        _dbPath.Font = Theme.Font(9f);
        _dbPath.ForeColor = Theme.TextMain;
        _dbPath.Location = new Point(96, 6);

        var lblSizeCap = new Label
        {
            Text = "文件大小：",
            AutoSize = true,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont,
            Location = new Point(0, 32)
        };
        _dbSize.AutoSize = true;
        _dbSize.Font = Theme.Font(9f);
        _dbSize.ForeColor = Theme.TextMain;
        _dbSize.Location = new Point(96, 32);

        var lblCountCap = new Label
        {
            Text = "数据统计：",
            AutoSize = true,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont,
            Location = new Point(0, 58)
        };
        _counts.AutoSize = true;
        _counts.Font = Theme.Font(9f);
        _counts.ForeColor = Theme.TextMain;
        _counts.Location = new Point(96, 58);

        info.Controls.AddRange(new Control[] { lblPathCap, _dbPath, lblSizeCap, _dbSize, lblCountCap, _counts });

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.White
        };

        var openDir = Theme.SecondaryButton("打开数据目录", 120, 32);
        openDir.Margin = new Padding(0, 6, 10, 0);
        openDir.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.DataDirectory)
            { UseShellExecute = true });

        var backup = Theme.PrimaryButton("立即备份", 100, 32);
        backup.Margin = new Padding(0, 6, 10, 0);
        backup.Click += (_, _) => Backup();

        var restore = Theme.SecondaryButton("从备份恢复", 112, 32);
        restore.Margin = new Padding(0, 6, 0, 0);
        restore.Click += (_, _) => Restore();

        buttons.Controls.AddRange(new Control[] { openDir, backup, restore });

        var note = new Label
        {
            Dock = DockStyle.Fill,
            Text = "提示：软件为绿色单机版，所有数据保存在本机，不联网、不上传。\n" +
                   "建议定期点击「立即备份」，备份文件保存在数据目录下的 backup 文件夹中。\n" +
                   "重装或换电脑时，把整个程序文件夹（含 data 目录）一起复制即可。",
            ForeColor = Theme.TextSub,
            Font = Theme.SmallFont,
            Padding = new Padding(0, 8, 0, 0)
        };

        card.Controls.Add(note);
        card.Controls.Add(buttons);
        card.Controls.Add(info);
        card.Controls.Add(title);
        return card;
    }

    public void Reload()
    {
        _company.Text = _repo.GetSetting("CompanyName", "");
        _phone.Text = _repo.GetSetting("CompanyPhone", "");
        _address.Text = _repo.GetSetting("CompanyAddress", "");
        _maker.Text = _repo.GetSetting("Maker", "");
        _salesman.Text = _repo.GetSetting("Salesman", "");
        _footer.Text = _repo.GetSetting("PrintFooter", "客户签收后请妥善保管，作为结算凭证。");
        _fax.Text = _repo.GetSetting("CompanyFax", "");
        _prePrinted.Checked = _repo.GetSetting("PrintAlignPrePrinted", "1") != "0";
        _carbonCopy.Checked = NotePrinter.ReadCarbonCopy(_repo);
        _copies.Value = NotePrinter.ReadCopies(_repo);

        _dbPath.Text = AppPaths.DatabasePath;
        try
        {
            var fi = new FileInfo(AppPaths.DatabasePath);
            _dbSize.Text = fi.Exists ? $"{fi.Length / 1024.0:N1} KB" : "（尚未创建）";
        }
        catch
        {
            _dbSize.Text = "未知";
        }

        try
        {
            _counts.Text = $"客户 {_repo.Count("Customer")} 家 · 商品 {_repo.Count("Product")} 种 · 送货单 {_repo.Count("DeliveryNote")} 张";
        }
        catch
        {
            _counts.Text = "未知";
        }
    }

    private void SaveSettings()
    {
        _repo.SetSetting("CompanyName", _company.Text.Trim());
        _repo.SetSetting("CompanyPhone", _phone.Text.Trim());
        _repo.SetSetting("CompanyAddress", _address.Text.Trim());
        _repo.SetSetting("Maker", _maker.Text.Trim());
        _repo.SetSetting("Salesman", _salesman.Text.Trim());
        _repo.SetSetting("PrintFooter", _footer.Text.Trim());
        _repo.SetSetting("CompanyFax", _fax.Text.Trim());
        _repo.SetSetting("PrintAlignPrePrinted", _prePrinted.Checked ? "1" : "0");
        NotePrinter.WriteCarbonCopy(_repo, _carbonCopy.Checked);
        NotePrinter.WriteCopies(_repo, (int)_copies.Value);

        Msg.Show(FindForm(), "设置已保存，打印时会使用新的公司信息。", "保存成功",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Backup()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.BackupDirectory);
            var target = Path.Combine(AppPaths.BackupDirectory, $"fangke-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            using (var conn = new Db(AppPaths.DatabasePath).Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "VACUUM INTO $p";
                cmd.Parameters.AddWithValue("$p", target);
                cmd.ExecuteNonQuery();
            }
            Msg.Show(FindForm(), $"备份成功！\n\n{target}", "备份数据",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Reload();
        }
        catch (Exception ex)
        {
            Msg.Show(FindForm(), "备份失败：" + ex.Message, "备份数据",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Restore()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "选择备份文件",
            Filter = "数据库备份 (*.db)|*.db|所有文件 (*.*)|*.*",
            InitialDirectory = Directory.Exists(AppPaths.BackupDirectory) ? AppPaths.BackupDirectory : null
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        if (Msg.Show(FindForm(), "恢复会覆盖当前所有数据，确定继续吗？", "恢复数据",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            File.Copy(dlg.FileName, AppPaths.DatabasePath, true);
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var f = AppPaths.DatabasePath + suffix;
                if (File.Exists(f)) File.Delete(f);
            }
            Msg.Show(FindForm(), "恢复成功，请重新打开软件。", "恢复数据",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Reload();
        }
        catch (Exception ex)
        {
            Msg.Show(FindForm(), "恢复失败：" + ex.Message, "恢复数据",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
