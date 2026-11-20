using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

/// <summary>
/// 审核中心。管理员在这里处理待审送货单与入库单：
/// 通过（同时产生库存变动）、驳回（退回修改）、反审核（冲回库存）。
/// </summary>
public sealed class AuditPage : UserControl, IRefreshable
{
    private readonly Repository _repo;
    private readonly MainForm _main;

    private readonly TabControl _tabs = new();

    private readonly DataGridView _noteGrid = new();
    private readonly Label _noteSummary = new();
    private readonly List<DeliveryNote> _notes = new();

    private readonly DataGridView _inGrid = new();
    private readonly Label _inSummary = new();
    private readonly List<StockIn> _ins = new();

    private readonly DataGridView _logGrid = new();
    private readonly List<AuditEntry> _logs = new();

    public AuditPage(Repository repo, MainForm main)
    {
        _repo = repo;
        _main = main;
        Dock = DockStyle.Fill;
        BackColor = Theme.PageBack;
        BuildUi();
        Reload();
    }

    private void BuildUi()
    {
        var head = new Theme.RoundedPanel
        {
            Dock = DockStyle.Top,
            Height = 54,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(16, 10, 16, 10)
        };
        var tip = new Label
        {
            Text = "审核通过后才会扣减/增加库存。已审核的单据不能直接修改，需先反审核。",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont
        };
        head.Controls.Add(tip);

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = Theme.BodyFont;
        _tabs.Padding = new Point(16, 6);

        var noteTab = new TabPage("送货单待审") { BackColor = Theme.PageBack, Padding = new Padding(0, 8, 0, 0) };
        noteTab.Controls.Add(BuildNoteTab());

        var inTab = new TabPage("入库单待审") { BackColor = Theme.PageBack, Padding = new Padding(0, 8, 0, 0) };
        inTab.Controls.Add(BuildInTab());

        var logTab = new TabPage("审核记录") { BackColor = Theme.PageBack, Padding = new Padding(0, 8, 0, 0) };
        logTab.Controls.Add(BuildLogTab());

        _tabs.TabPages.Add(noteTab);
        _tabs.TabPages.Add(inTab);
        _tabs.TabPages.Add(logTab);

        Controls.Add(_tabs);
        Controls.Add(head);
    }

    private Control BuildNoteTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.PageBack
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 10, 12, 10)
        };

        var bar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White };
        var title = new Label
        {
            Text = "待审核送货单",
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            AutoSize = true,
            Location = new Point(2, 11)
        };

        var approve = Theme.PrimaryButton("审核通过", 96, 30);
        approve.Click += (_, _) => ApproveNotes();
        var reject = Theme.DangerButton("驳回", 74, 30);
        reject.Click += (_, _) => RejectNotes();
        var unapprove = Theme.SecondaryButton("反审核", 84, 30);
        unapprove.Click += (_, _) => UnapproveNotes();
        var open = Theme.SecondaryButton("查看单据", 92, 30);
        open.Click += (_, _) => OpenNote();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 390,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 6, 0, 0)
        };
        foreach (var b in new[] { approve, reject, unapprove, open }) actions.Controls.Add(b);

        bar.Controls.Add(title);
        bar.Controls.Add(actions);

        Theme.StyleGrid(_noteGrid);
        _noteGrid.Dock = DockStyle.Fill;
        _noteGrid.ReadOnly = true;
        _noteGrid.AutoGenerateColumns = false;
        _noteGrid.MultiSelect = true;
        AddCol(_noteGrid, "NoteNo", "单号", 120);
        AddCol(_noteGrid, "NoteDateText", "送货日期", 90, "center");
        AddCol(_noteGrid, "CustomerName", "客户名称", 180);
        AddCol(_noteGrid, "TotalQtyText", "数量", 66, "right");
        AddCol(_noteGrid, "TotalAmount", "金额", 96, "right");
        AddCol(_noteGrid, "StatusText", "审核状态", 84, "center");
        AddCol(_noteGrid, "Maker", "制单人", 84);
        AddCol(_noteGrid, "CreatedAt", "制单时间", 130, "center");
        _noteGrid.Columns["TotalAmount"]!.DefaultCellStyle.Format = "N2";
        _noteGrid.Columns["TotalAmount"]!.DefaultCellStyle.ForeColor = Theme.PrimaryDark;
        _noteGrid.Columns["TotalAmount"]!.DefaultCellStyle.Font = Theme.Font(9.5f, FontStyle.Bold);

        card.Controls.Add(_noteGrid);
        card.Controls.Add(bar);

        var foot = new Theme.RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8) };
        _noteSummary.Dock = DockStyle.Fill;
        _noteSummary.TextAlign = ContentAlignment.MiddleLeft;
        _noteSummary.Font = Theme.Font(9.5f, FontStyle.Bold);
        _noteSummary.ForeColor = Theme.TextMain;
        _noteSummary.Text = "共 0 张待审核";
        foot.Controls.Add(_noteSummary);

        root.Controls.Add(card, 0, 0);
        root.Controls.Add(foot, 0, 1);
        return root;
    }

    private Control BuildInTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.PageBack
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 10, 12, 10)
        };

        var bar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White };
        var title = new Label
        {
            Text = "待审核入库单",
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            AutoSize = true,
            Location = new Point(2, 11)
        };

        var approve = Theme.PrimaryButton("审核通过", 96, 30);
        approve.Click += (_, _) => ApproveIns();
        var reject = Theme.DangerButton("驳回", 74, 30);
        reject.Click += (_, _) => RejectIns();
        var unapprove = Theme.SecondaryButton("反审核", 84, 30);
        unapprove.Click += (_, _) => UnapproveIns();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 300,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 6, 0, 0)
        };
        foreach (var b in new[] { approve, reject, unapprove }) actions.Controls.Add(b);

        bar.Controls.Add(title);
        bar.Controls.Add(actions);

        Theme.StyleGrid(_inGrid);
        _inGrid.Dock = DockStyle.Fill;
        _inGrid.ReadOnly = true;
        _inGrid.AutoGenerateColumns = false;
        _inGrid.MultiSelect = true;
        AddCol(_inGrid, "InNo", "入库单号", 130);
        AddCol(_inGrid, "InDateText", "入库日期", 92, "center");
        AddCol(_inGrid, "Supplier", "供应商", 170);
        AddCol(_inGrid, "TotalQtyText", "数量", 70, "right");
        AddCol(_inGrid, "TotalAmount", "金额", 96, "right");
        AddCol(_inGrid, "StatusText", "审核状态", 84, "center");
        AddCol(_inGrid, "Maker", "制单人", 84);
        _inGrid.Columns["TotalAmount"]!.DefaultCellStyle.Format = "N2";
        _inGrid.Columns["TotalAmount"]!.DefaultCellStyle.ForeColor = Theme.PrimaryDark;
        _inGrid.Columns["TotalAmount"]!.DefaultCellStyle.Font = Theme.Font(9.5f, FontStyle.Bold);

        card.Controls.Add(_inGrid);
        card.Controls.Add(bar);

        var foot = new Theme.RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8) };
        _inSummary.Dock = DockStyle.Fill;
        _inSummary.TextAlign = ContentAlignment.MiddleLeft;
        _inSummary.Font = Theme.Font(9.5f, FontStyle.Bold);
        _inSummary.ForeColor = Theme.TextMain;
        _inSummary.Text = "共 0 张待审核";
        foot.Controls.Add(_inSummary);

        root.Controls.Add(card, 0, 0);
        root.Controls.Add(foot, 0, 1);
        return root;
    }

    private Control BuildLogTab()
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(12, 10, 12, 10)
        };

        var head = new Label
        {
            Text = "审核记录（最近 300 条）",
            Dock = DockStyle.Top,
            Height = 34,
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Theme.StyleGrid(_logGrid);
        _logGrid.Dock = DockStyle.Fill;
        _logGrid.ReadOnly = true;
        _logGrid.AutoGenerateColumns = false;
        AddCol(_logGrid, "CreatedAt", "时间", 130, "center");
        AddCol(_logGrid, "DocType", "单据类型", 90, "center");
        AddCol(_logGrid, "DocNo", "单号", 130);
        AddCol(_logGrid, "Action", "操作", 90, "center");
        AddCol(_logGrid, "Operator", "操作人", 100);
        AddCol(_logGrid, "Remark", "意见", 260);

        card.Controls.Add(_logGrid);
        card.Controls.Add(head);
        return card;
    }

    private static void AddCol(DataGridView g, string prop, string header, int fill, string align = "left")
    {
        var c = new DataGridViewTextBoxColumn
        {
            Name = prop,
            DataPropertyName = prop,
            HeaderText = header,
            FillWeight = fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        c.DefaultCellStyle.Alignment = align switch
        {
            "right" => DataGridViewContentAlignment.MiddleRight,
            "center" => DataGridViewContentAlignment.MiddleCenter,
            _ => DataGridViewContentAlignment.MiddleLeft
        };
        g.Columns.Add(c);
    }

    public void Reload()
    {
        ReloadNotes();
        ReloadIns();
        ReloadLogs();
    }

    private void ReloadNotes()
    {
        _notes.Clear();
        _notes.AddRange(_repo.QueryNotesByApproval(ApproveStatus.待审核));

        _noteGrid.DataSource = _notes.Select(n => new
        {
            n.Id,
            n.NoteNo,
            NoteDateText = n.NoteDate.ToString("yyyy-MM-dd"),
            n.CustomerName,
            TotalQtyText = n.TotalQty.ToString("0.###"),
            n.TotalAmount,
            StatusText = n.ApproveStatus.ToString(),
            n.Maker,
            n.CreatedAt
        }).ToList();

        _noteSummary.Text = $"共 {_notes.Count} 张待审核送货单 · 合计金额 {_notes.Sum(n => n.TotalAmount):N2} 元";
    }

    private void ReloadIns()
    {
        _ins.Clear();
        _ins.AddRange(_repo.QueryStockIns(null, null, null, ApproveStatus.待审核));

        _inGrid.DataSource = _ins.Select(d => new
        {
            d.Id,
            d.InNo,
            InDateText = d.InDate.ToString("yyyy-MM-dd"),
            d.Supplier,
            TotalQtyText = d.TotalQty.ToString("0.###"),
            d.TotalAmount,
            StatusText = d.ApproveStatus.ToString(),
            d.Maker
        }).ToList();

        _inSummary.Text = $"共 {_ins.Count} 张待审核入库单 · 合计金额 {_ins.Sum(d => d.TotalAmount):N2} 元";
    }

    private void ReloadLogs()
    {
        _logs.Clear();
        _logs.AddRange(_repo.QueryAuditLog());
        _logGrid.DataSource = _logs.Select(a => new
        {
            a.CreatedAt,
            a.DocType,
            a.DocNo,
            a.Action,
            a.Operator,
            a.Remark
        }).ToList();
    }

    private List<long> SelectedNoteIds() => SelectedIds(_noteGrid);
    private List<long> SelectedInIds() => SelectedIds(_inGrid);

    private static List<long> SelectedIds(DataGridView grid)
    {
        var ids = new List<long>();
        foreach (DataGridViewRow row in grid.SelectedRows)
        {
            if (row.DataBoundItem is null) continue;
            var prop = row.DataBoundItem.GetType().GetProperty("Id");
            if (prop?.GetValue(row.DataBoundItem) is long id) ids.Add(id);
        }
        return ids;
    }

    private string AskRemark(string title)
    {
        var text = PromptDialog.Ask(FindForm(), title, "审核意见（可留空）：", "", true);
        return text ?? "";
    }

    private void ApproveNotes()
    {
        var ids = SelectedNoteIds();
        if (ids.Count == 0)
        {
            Msg.Show(FindForm(), "请先选择要审核的送货单。", "审核",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var remark = AskRemark($"审核通过 {ids.Count} 张送货单");
        var okCount = 0;
        var errors = new List<string>();
        foreach (var id in ids)
        {
            var (ok, msg) = _repo.ApproveNote(id, _main.CurrentUserName, remark);
            if (ok) okCount++;
            else errors.Add(msg);
        }

        Reload();
        _main.InvalidatePages("list", "stock", "report");
        _main.SetStatus($"已审核 {okCount} 张送货单");

        if (errors.Count > 0)
            Msg.Show(FindForm(), string.Join("\n", errors), "部分单据未能审核",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void RejectNotes()
    {
        var ids = SelectedNoteIds();
        if (ids.Count == 0) return;

        var remark = PromptDialog.Ask(FindForm(), "驳回送货单", "驳回原因（会记入审核记录）：", "", true);
        if (remark is null) return;
        if (string.IsNullOrWhiteSpace(remark))
        {
            Msg.Show(FindForm(), "驳回必须填写原因。", "驳回",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var okCount = 0;
        foreach (var id in ids)
        {
            var (ok, _) = _repo.RejectNote(id, _main.CurrentUserName, remark);
            if (ok) okCount++;
        }

        Reload();
        _main.InvalidatePages("list");
        _main.SetStatus($"已驳回 {okCount} 张送货单");
    }

    private void UnapproveNotes()
    {
        var ids = SelectedNoteIds();
        if (ids.Count == 0) return;

        if (Msg.Show(FindForm(),
                "反审核会把已扣减的库存冲回，单据退回可编辑状态。\n\n确定继续吗？",
                "反审核", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var remark = AskRemark("反审核");
        var okCount = 0;
        var errors = new List<string>();
        foreach (var id in ids)
        {
            var (ok, msg) = _repo.UnapproveNote(id, _main.CurrentUserName, remark);
            if (ok) okCount++;
            else errors.Add(msg);
        }

        Reload();
        _main.InvalidatePages("list", "stock", "report");
        _main.SetStatus($"已反审核 {okCount} 张送货单");

        if (errors.Count > 0)
            Msg.Show(FindForm(), string.Join("\n", errors), "部分单据未能反审核",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ApproveIns()
    {
        var ids = SelectedInIds();
        if (ids.Count == 0)
        {
            Msg.Show(FindForm(), "请先选择要审核的入库单。", "审核",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var remark = AskRemark($"审核通过 {ids.Count} 张入库单");
        var okCount = 0;
        var errors = new List<string>();
        foreach (var id in ids)
        {
            var (ok, msg) = _repo.ApproveStockIn(id, _main.CurrentUserName, remark);
            if (ok) okCount++;
            else errors.Add(msg);
        }

        Reload();
        _main.InvalidatePages("stockin", "stock");
        _main.SetStatus($"已审核 {okCount} 张入库单");

        if (errors.Count > 0)
            Msg.Show(FindForm(), string.Join("\n", errors), "部分单据未能审核",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void RejectIns()
    {
        var ids = SelectedInIds();
        if (ids.Count == 0) return;

        var remark = PromptDialog.Ask(FindForm(), "驳回入库单", "驳回原因（会记入审核记录）：", "", true);
        if (remark is null) return;
        if (string.IsNullOrWhiteSpace(remark))
        {
            Msg.Show(FindForm(), "驳回必须填写原因。", "驳回",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var okCount = 0;
        foreach (var id in ids)
        {
            var (ok, _) = _repo.RejectStockIn(id, _main.CurrentUserName, remark);
            if (ok) okCount++;
        }

        Reload();
        _main.InvalidatePages("stockin");
        _main.SetStatus($"已驳回 {okCount} 张入库单");
    }

    private void UnapproveIns()
    {
        var ids = SelectedInIds();
        if (ids.Count == 0) return;

        if (Msg.Show(FindForm(),
                "反审核会把已增加的库存冲回。\n\n确定继续吗？",
                "反审核", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var remark = AskRemark("反审核");
        var okCount = 0;
        foreach (var id in ids)
        {
            var (ok, _) = _repo.UnapproveStockIn(id, _main.CurrentUserName, remark);
            if (ok) okCount++;
        }

        Reload();
        _main.InvalidatePages("stockin", "stock");
        _main.SetStatus($"已反审核 {okCount} 张入库单");
    }

    private void OpenNote()
    {
        var ids = SelectedNoteIds();
        if (ids.Count == 0) return;
        _main.OpenNoteForEdit(ids[0]);
    }
}
