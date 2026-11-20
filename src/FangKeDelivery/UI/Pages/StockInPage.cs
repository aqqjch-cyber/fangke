using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

/// <summary>入库单管理：录入采购入库，审核通过后增加库存。</summary>
public sealed class StockInPage : UserControl, IRefreshable
{
    private readonly Repository _repo;
    private readonly MainForm _main;

    private readonly TextBox _keyword = Theme.Input();
    private readonly DateTimePicker _from = new();
    private readonly DateTimePicker _to = new();
    private readonly ComboBox _status = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summary = new();

    private List<StockIn> _docs = new();

    public StockInPage(Repository repo, MainForm main)
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

        _keyword.Width = 180;
        _keyword.PlaceholderText = "入库单号 / 供应商";

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
        _status.Width = 100;
        _status.Font = Theme.BodyFont;
        _status.Items.AddRange(new object[] { "全部状态", "待审核", "已审核", "已驳回" });
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

        void Wrap(string label, Control c, int labelWidth)
        {
            flow.Controls.Add(new Label
            {
                Text = label,
                AutoSize = false,
                Width = labelWidth,
                Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Theme.TextSub,
                Font = Theme.BodyFont,
                Margin = new Padding(0, 3, 4, 0)
            });
            c.Margin = new Padding(0, 3, 10, 0);
            flow.Controls.Add(c);
        }

        Wrap("关键字", _keyword, 56);
        Wrap("日期", _from, 40);
        flow.Controls.Add(new Label
        {
            Text = "至",
            AutoSize = false,
            Width = 20,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont,
            Margin = new Padding(0, 3, 4, 0)
        });
        _to.Margin = new Padding(0, 3, 10, 0);
        flow.Controls.Add(_to);
        Wrap("状态", _status, 40);
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
            Text = "入库单",
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            AutoSize = true,
            Location = new Point(2, 11)
        };

        var add = Theme.PrimaryButton("+ 新建入库单", 120, 30);
        add.Click += (_, _) => NewDoc();
        var edit = Theme.SecondaryButton("编辑", 70, 30);
        edit.Click += (_, _) => EditSelected();
        var del = Theme.DangerButton("删除", 70, 30);
        del.Click += (_, _) => DeleteSelected();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 300,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 6, 0, 0)
        };
        foreach (var b in new[] { add, edit, del }) actions.Controls.Add(b);

        head.Controls.Add(title);
        head.Controls.Add(actions);

        Theme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AutoGenerateColumns = false;
        _grid.MultiSelect = true;
        _grid.AutoGenerateColumns = false;
        AddCol("InNo", "入库单号", 120);
        AddCol("InDateText", "入库日期", 92, "center");
        AddCol("Supplier", "供应商", 150);
        AddCol("TotalQtyText", "数量", 70, "right");
        AddCol("TotalAmount", "金额", 95, "right");
        AddCol("StatusText", "审核状态", 84, "center");
        AddCol("Approver", "审核人", 84);
        AddCol("Remark", "备注", 150);

        _grid.Columns["TotalAmount"]!.DefaultCellStyle.Format = "N2";
        _grid.Columns["TotalAmount"]!.DefaultCellStyle.ForeColor = Theme.PrimaryDark;
        _grid.Columns["TotalAmount"]!.DefaultCellStyle.Font = Theme.Font(9.5f, FontStyle.Bold);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };

        card.Controls.Add(_grid);
        card.Controls.Add(head);
        return card;
    }

    private void AddCol(string prop, string header, int fill, string align = "left")
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
        _grid.Columns.Add(c);
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
        _summary.Text = "共 0 张入库单";
        card.Controls.Add(_summary);
        return card;
    }

    public void Reload()
    {
        ApproveStatus? status = _status.SelectedIndex switch
        {
            1 => ApproveStatus.待审核,
            2 => ApproveStatus.已审核,
            3 => ApproveStatus.已驳回,
            _ => null
        };

        _docs = _repo.QueryStockIns(_keyword.Text, _from.Value.Date, _to.Value.Date, status);

        _grid.DataSource = _docs.Select(d => new
        {
            d.Id,
            d.InNo,
            InDateText = d.InDate.ToString("yyyy-MM-dd"),
            d.Supplier,
            TotalQtyText = d.TotalQty.ToString("0.###"),
            d.TotalAmount,
            StatusText = d.ApproveStatus.ToString(),
            d.Approver,
            d.Remark
        }).ToList();

        var total = _docs.Sum(d => d.TotalAmount);
        var pending = _docs.Count(d => d.ApproveStatus == ApproveStatus.待审核);
        _summary.Text = $"共 {_docs.Count} 张入库单 · 合计金额 {total:N2} 元" +
                        (pending > 0 ? $" · {pending} 张待审核" : "");
    }

    private StockIn? Selected()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        var bound = _grid.SelectedRows[0].DataBoundItem;
        if (bound is null) return null;
        var id = (long)bound.GetType().GetProperty("Id")!.GetValue(bound)!;
        return _docs.FirstOrDefault(d => d.Id == id);
    }

    private void NewDoc()
    {
        var doc = new StockIn
        {
            InNo = _repo.NextStockInNo(DateTime.Today),
            InDate = DateTime.Today,
            Maker = _main.CurrentUserName
        };

        using var dlg = new StockInEditDialog(doc, _repo, true);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        _repo.SaveStockIn(dlg.Result);
        Reload();
        _main.InvalidatePages("stock", "audit");
        _main.SetStatus($"已保存入库单 {dlg.Result.InNo}，待审核");
    }

    private void EditSelected()
    {
        var doc = Selected();
        if (doc is null) return;

        if (doc.ApproveStatus == ApproveStatus.已审核)
        {
            Msg.Show(FindForm(), "已审核的入库单不能直接修改。\n\n如需修改，请先到「审核中心」反审核。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var full = _repo.GetStockIn(doc.Id);
        if (full is null) return;

        using var dlg = new StockInEditDialog(full, _repo, false);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        _repo.SaveStockIn(dlg.Result);
        Reload();
        _main.InvalidatePages("audit");
        _main.SetStatus($"已更新入库单 {dlg.Result.InNo}");
    }

    private void DeleteSelected()
    {
        var ids = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem)
            .Where(d => d is not null)
            .Select(d => (long)d!.GetType().GetProperty("Id")!.GetValue(d)!)
            .ToList();
        if (ids.Count == 0) return;

        if (Msg.Show(FindForm(), $"确定删除选中的 {ids.Count} 张入库单吗？", "删除确认",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var (ok, msg) = _repo.DeleteStockIns(ids);
        Reload();
        _main.InvalidatePages("audit");
        Msg.Show(FindForm(), msg, "删除入库单", MessageBoxButtons.OK,
            ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }
}
