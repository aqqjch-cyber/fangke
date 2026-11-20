using FangKeDelivery.Core;
using FangKeDelivery.Data;
using FangKeDelivery.Printing;

namespace FangKeDelivery.UI.Pages;

public sealed class NoteListPage : UserControl, IRefreshable
{
    private readonly Repository _repo;
    private readonly MainForm _main;

    private readonly TextBox _keyword = Theme.Input();
    private readonly DateTimePicker _from = new();
    private readonly DateTimePicker _to = new();
    private readonly ComboBox _status = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summary = new();

    private List<DeliveryNote> _notes = new();

    public NoteListPage(Repository repo, MainForm main)
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
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.PageBack
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        root.Controls.Add(BuildFilterBar(), 0, 0);
        root.Controls.Add(BuildGridCard(), 0, 1);
        root.Controls.Add(BuildSummaryBar(), 0, 2);

        Controls.Add(root);
    }

    private Control BuildFilterBar()
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 10, 14, 10)
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White
        };

        _keyword.Width = 190;
        _keyword.PlaceholderText = "单号 / 客户 / 电话 / 备注";

        _from.Format = DateTimePickerFormat.Custom;
        _from.CustomFormat = "yyyy-MM-dd";
        _from.Width = 116;
        _from.Font = Theme.BodyFont;
        _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        _to.Format = DateTimePickerFormat.Custom;
        _to.CustomFormat = "yyyy-MM-dd";
        _to.Width = 116;
        _to.Font = Theme.BodyFont;
        _to.Value = DateTime.Today;

        _status.DropDownStyle = ComboBoxStyle.DropDownList;
        _status.Width = 96;
        _status.Font = Theme.BodyFont;
        _status.Items.AddRange(new object[] { "全部状态", "未送货", "已送货", "已结算" });
        _status.SelectedIndex = 0;

        var search = Theme.PrimaryButton("查询", 76, 30);
        search.Click += (_, _) => Reload();
        var reset = Theme.SecondaryButton("重置", 68, 30);
        reset.Click += (_, _) =>
        {
            _keyword.Text = "";
            _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _to.Value = DateTime.Today;
            _status.SelectedIndex = 0;
            Reload();
        };

        void Wrap(string label, Control c)
        {
            var l = new Label
            {
                Text = label,
                AutoSize = false,
                Width = label.Length > 2 ? 66 : 40,
                Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Theme.TextSub,
                Font = Theme.BodyFont,
                Margin = new Padding(0, 3, 4, 0)
            };
            flow.Controls.Add(l);
            c.Margin = new Padding(0, 3, 10, 0);
            flow.Controls.Add(c);
        }

        Wrap("关键字", _keyword);
        Wrap("日期", _from);
        flow.Controls.Add(new Label
        {
            Text = "至", AutoSize = false, Width = 20, Height = 30,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextSub, Font = Theme.BodyFont,
            Margin = new Padding(0, 3, 4, 0)
        });
        _to.Margin = new Padding(0, 3, 10, 0);
        flow.Controls.Add(_to);
        Wrap("状态", _status);
        search.Margin = new Padding(6, 3, 8, 0);
        reset.Margin = new Padding(0, 3, 0, 0);
        flow.Controls.Add(search);
        flow.Controls.Add(reset);

        card.Controls.Add(flow);
        return card;
    }

    private Control BuildGridCard()
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 10, 12, 10)
        };

        var head = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White };
        var title = new Label
        {
            Text = "送货单列表",
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            AutoSize = true,
            Location = new Point(2, 11)
        };

        var open = Theme.PrimaryButton("打开 / 编辑", 100, 30);
        open.Click += (_, _) => OpenSelected();
        var reprint = Theme.SecondaryButton("补打", 70, 30);
        reprint.Click += (_, _) => Reprint();
        var copy = Theme.SecondaryButton("复制新单", 92, 30);
        copy.Click += (_, _) => CopyAsNew();
        var mark = Theme.SecondaryButton("标记已送货", 104, 30);
        mark.Click += (_, _) => MarkStatus(NoteStatus.已送货);
        var settle = Theme.SecondaryButton("标记已结算", 104, 30);
        settle.Click += (_, _) => MarkStatus(NoteStatus.已结算);
        var del = Theme.DangerButton("删除", 70, 30);
        del.Click += (_, _) => DeleteSelected();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 620,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 6, 0, 0)
        };
        foreach (var b in new[] { open, reprint, copy, mark, settle, del })
            actions.Controls.Add(b);

        head.Controls.Add(title);
        head.Controls.Add(actions);

        Theme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AutoGenerateColumns = false;
        BuildColumns();
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenSelected(); };

        card.Controls.Add(_grid);
        card.Controls.Add(head);
        return card;
    }

    private void BuildColumns()
    {
        _grid.Columns.Clear();

        DataGridViewTextBoxColumn Col(string prop, string header, int fill, string align = "left")
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
            return c;
        }

        _grid.Columns.Add(Col("NoteNo", "单号", 110));
        _grid.Columns.Add(Col("NoteDateText", "送货日期", 90, "center"));
        _grid.Columns.Add(Col("CustomerName", "客户名称", 170));
        _grid.Columns.Add(Col("CustomerPhone", "电话", 110));
        _grid.Columns.Add(Col("TotalQtyText", "数量", 60, "right"));
        _grid.Columns.Add(Col("TotalAmount", "金额", 90, "right"));
        _grid.Columns.Add(Col("StatusText", "状态", 70, "center"));
        _grid.Columns.Add(Col("Salesman", "送货员", 80));
        _grid.Columns.Add(Col("Remark", "备注", 140));

        _grid.Columns["TotalAmount"]!.DefaultCellStyle.Format = "N2";
        _grid.Columns["TotalAmount"]!.DefaultCellStyle.ForeColor = Theme.PrimaryDark;
        _grid.Columns["TotalAmount"]!.DefaultCellStyle.Font = Theme.Font(9.5f, FontStyle.Bold);
    }

    private Control BuildSummaryBar()
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(16, 8, 16, 8)
        };

        _summary.Dock = DockStyle.Fill;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _summary.Font = Theme.Font(9.5f, FontStyle.Bold);
        _summary.ForeColor = Theme.TextMain;
        _summary.Text = "共 0 张单据";
        card.Controls.Add(_summary);
        return card;
    }

    public void Reload()
    {
        NoteStatus? status = _status.SelectedIndex switch
        {
            1 => NoteStatus.未送货,
            2 => NoteStatus.已送货,
            3 => NoteStatus.已结算,
            _ => null
        };

        _notes = _repo.QueryNotes(_keyword.Text, _from.Value.Date, _to.Value.Date, status);

        var rows = _notes.Select(n => new
        {
            n.Id,
            n.NoteNo,
            NoteDateText = n.NoteDate.ToString("yyyy-MM-dd"),
            n.CustomerName,
            n.CustomerPhone,
            TotalQtyText = n.TotalQty.ToString("0.###"),
            n.TotalAmount,
            StatusText = n.Status.ToString(),
            n.Salesman,
            n.Remark
        }).ToList();

        _grid.DataSource = rows;
        if (_grid.Columns["StatusText"] is { } sc)
        {
            sc.DefaultCellStyle.ForeColor = Theme.TextMain;
        }

        var total = _notes.Sum(n => n.TotalAmount);
        _summary.Text = $"共 {_notes.Count} 张单据 · 合计金额 {total:N2} 元";
    }

    private List<long> SelectedIds()
    {
        var ids = new List<long>();
        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            if (row.DataBoundItem is null) continue;
            var prop = row.DataBoundItem.GetType().GetProperty("Id");
            if (prop?.GetValue(row.DataBoundItem) is long id)
                ids.Add(id);
        }
        return ids;
    }

    private void OpenSelected()
    {
        var ids = SelectedIds();
        if (ids.Count == 0) return;
        _main.OpenNoteForEdit(ids[0]);
    }

    private void Reprint()
    {
        var ids = SelectedIds();
        if (ids.Count == 0) return;
        var note = _repo.GetNote(ids[0]);
        if (note is null) return;
        NotePrinter.ShowPreview(note, _repo, FindForm());
    }

    private void CopyAsNew()
    {
        var ids = SelectedIds();
        if (ids.Count == 0) return;

        if (Msg.Show(FindForm(), "将选中的单据内容复制为一张今天的新单？\n\n新单会打开在「开单」页面，您可以直接修改后保存。",
                "复制新单", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _main.CopyNoteAsNew(ids[0]);
        _main.SetStatus("已复制为新单，请修改后保存");
    }

    private void MarkStatus(NoteStatus status)
    {
        var ids = SelectedIds();
        if (ids.Count == 0) return;
        _repo.SetNoteStatus(ids, status);
        Reload();
        _main.SetStatus($"已将 {ids.Count} 张单据标记为「{status}」");
    }

    private void DeleteSelected()
    {
        var ids = SelectedIds();
        if (ids.Count == 0) return;
        if (Msg.Show(FindForm(), $"确定删除选中的 {ids.Count} 张单据吗？此操作不可撤销。", "删除确认",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _repo.DeleteNotes(ids);
        Reload();
        _main.SetStatus($"已删除 {ids.Count} 张单据");
    }
}
