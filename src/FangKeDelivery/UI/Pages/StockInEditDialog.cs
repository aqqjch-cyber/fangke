using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

/// <summary>入库单录入/编辑。明细行结构与送货单一致，减少学习成本。</summary>
public sealed class StockInEditDialog : Form
{
    private readonly Repository _repo;
    private readonly StockIn _doc;
    private readonly bool _isNew;

    private readonly TextBox _inNo = Theme.Input();
    private readonly TextBox _supplier = Theme.Input();
    private readonly DateTimePicker _date = new();
    private readonly TextBox _remark = Theme.Input();
    private readonly DataGridView _grid = new();
    private readonly Label _lblTotal = new();

    private List<Product> _products = new();

    public StockIn Result => _doc;

    public StockInEditDialog(StockIn doc, Repository repo, bool isNew)
    {
        _doc = doc;
        _repo = repo;
        _isNew = isNew;

        Text = isNew ? "新建入库单" : $"编辑入库单 {doc.InNo}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 620);
        Size = new Size(1060, 680);
        Font = Theme.BodyFont;
        BackColor = Theme.PageBack;
        Padding = new Padding(14);

        _products = _repo.GetProducts();

        BuildUi();
        LoadDoc();
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildGridCard(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(16, 10, 16, 10)
        };

        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            BackColor = Color.White
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        void Row(int row, int col, string label, Control c)
        {
            t.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Theme.TextSub,
                Font = Theme.BodyFont,
                Margin = new Padding(0, 0, 6, 0)
            }, col, row);
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 6, 10, 6);
            t.Controls.Add(c, col + 1, row);
        }

        _inNo.ReadOnly = true;
        _inNo.BackColor = Color.FromArgb(247, 249, 252);

        _date.Format = DateTimePickerFormat.Custom;
        _date.CustomFormat = "yyyy-MM-dd";
        _date.Font = Theme.BodyFont;

        Row(0, 0, "入库单号", _inNo);
        Row(0, 2, "入库日期", _date);
        Row(0, 4, "供应商", _supplier);
        Row(1, 0, "备注", _remark);

        card.Controls.Add(t);
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

        var head = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.White };
        var title = new Label
        {
            Text = "入库明细",
            Font = Theme.Font(10.5f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            AutoSize = true,
            Location = new Point(2, 9)
        };
        var tip = new Label
        {
            Text = "商品名称可下拉选择，也可直接手打（会自动建档）",
            Font = Theme.Font(8.5f),
            ForeColor = Theme.TextSub,
            AutoSize = true,
            Location = new Point(90, 11)
        };

        var addRow = Theme.SecondaryButton("+ 加一行", 86, 28);
        addRow.Click += (_, _) => AddEmptyRow();
        var delRow = Theme.SecondaryButton("删除行", 80, 28);
        delRow.Click += (_, _) => DeleteSelectedRows();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 190,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 5, 0, 0)
        };
        actions.Controls.Add(addRow);
        actions.Controls.Add(delRow);

        head.Controls.Add(title);
        head.Controls.Add(tip);
        head.Controls.Add(actions);

        Theme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = true;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        BuildColumns();
        _grid.CellValueChanged += (_, e) => { if (e.RowIndex >= 0) RecalcTotals(); };
        _grid.CellEndEdit += (_, _) => RecalcTotals();
        _grid.RowsRemoved += (_, _) => RecalcTotals();
        _grid.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                DeleteSelectedRows();
            }
        };

        card.Controls.Add(_grid);
        card.Controls.Add(head);
        return card;
    }

    private void BuildColumns()
    {
        _grid.Columns.Clear();

        void AddText(string name, string header, int fill, bool readOnly = false, string align = "left")
        {
            var c = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = fill,
                ReadOnly = readOnly,
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

        var nameCol = new DataGridViewComboBoxColumn
        {
            Name = "ProductName",
            HeaderText = "商品名称",
            FillWeight = 190,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        nameCol.Items.AddRange(_products.Select(p => (object)p.Name).ToArray());
        _grid.Columns.Add(nameCol);

        AddText("Spec", "规格", 110);
        AddText("Unit", "单位", 56, false, "center");
        AddText("Qty", "数量", 72, false, "right");
        AddText("Price", "单价", 82, false, "right");
        AddText("Amount", "金额", 92, true, "right");
        AddText("Remark", "备注", 120);

        _grid.Columns["Amount"]!.DefaultCellStyle.ForeColor = Theme.PrimaryDark;
        _grid.Columns["Amount"]!.DefaultCellStyle.Font = Theme.Font(9.5f, FontStyle.Bold);
    }

    private Control BuildFooter()
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(16, 8, 16, 8)
        };

        var left = new Panel { Dock = DockStyle.Left, Width = 380, BackColor = Color.White };
        var cap = new Label
        {
            Text = "合计金额 ¥",
            AutoSize = true,
            ForeColor = Theme.TextSub,
            Font = Theme.Font(10f),
            Location = new Point(0, 14)
        };
        _lblTotal.AutoSize = true;
        _lblTotal.Font = Theme.Font(17f, FontStyle.Bold);
        _lblTotal.ForeColor = Theme.PrimaryDark;
        _lblTotal.Text = "0.00";
        _lblTotal.Location = new Point(96, 6);

        left.Controls.Add(cap);
        left.Controls.Add(_lblTotal);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 280,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 8, 0, 0)
        };
        var save = Theme.PrimaryButton("保存", 92, 34);
        save.Click += (_, _) => Save();
        var cancel = Theme.SecondaryButton("取消", 84, 34);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        card.Controls.Add(left);
        card.Controls.Add(buttons);
        return card;
    }

    private void LoadDoc()
    {
        _inNo.Text = _doc.InNo;
        _supplier.Text = _doc.Supplier;
        _date.Value = _doc.InDate;
        _remark.Text = _doc.Remark;

        _grid.Rows.Clear();
        foreach (var it in _doc.Items)
        {
            var idx = _grid.Rows.Add();
            var row = _grid.Rows[idx];
            row.Cells["ProductName"].Value = it.ProductName;
            row.Cells["Spec"].Value = it.Spec;
            row.Cells["Unit"].Value = it.Unit;
            row.Cells["Qty"].Value = it.Qty.ToString("0.###");
            row.Cells["Price"].Value = it.Price.ToString("0.00");
            row.Cells["Remark"].Value = it.Remark;
        }

        // 留几行空白方便直接录
        for (var i = 0; i < 3; i++) AddEmptyRow();
        RecalcTotals();
    }

    private void AddEmptyRow()
    {
        var idx = _grid.Rows.Add();
        _grid.Rows[idx].Cells["Qty"].Value = "1";
        _grid.Rows[idx].Cells["Price"].Value = "0.00";
    }

    private void DeleteSelectedRows()
    {
        var targets = _grid.SelectedCells.Cast<DataGridViewCell>()
            .Select(c => c.RowIndex)
            .Where(i => i >= 0)
            .Distinct()
            .OrderByDescending(i => i)
            .ToList();
        foreach (var i in targets)
        {
            if (_grid.Rows[i].IsNewRow) continue;
            _grid.Rows.RemoveAt(i);
        }
        RecalcTotals();
    }

    private static decimal ToDecimal(object? v) =>
        v is null ? 0m : decimal.TryParse(v.ToString(), out var d) ? d : 0m;

    private void RecalcTotals()
    {
        decimal total = 0m;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            var amount = Math.Round(ToDecimal(row.Cells["Qty"].Value) * ToDecimal(row.Cells["Price"].Value),
                2, MidpointRounding.AwayFromZero);
            if (ToDecimal(row.Cells["Amount"].Value) != amount)
                row.Cells["Amount"].Value = amount.ToString("0.00");

            if (string.IsNullOrWhiteSpace(row.Cells["ProductName"].Value?.ToString())) continue;
            total += amount;
        }
        _lblTotal.Text = total.ToString("N2");
    }

    private void Save()
    {
        _doc.Supplier = _supplier.Text.Trim();
        _doc.InDate = _date.Value.Date;
        _doc.Remark = _remark.Text.Trim();
        _doc.Maker = string.IsNullOrWhiteSpace(_doc.Maker) ? Environment.UserName : _doc.Maker;

        _doc.Items.Clear();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            var name = row.Cells["ProductName"].Value?.ToString()?.Trim() ?? "";
            if (name.Length == 0) continue;

            var qty = ToDecimal(row.Cells["Qty"].Value);
            if (qty <= 0)
            {
                Msg.Show(this, $"商品「{name}」的数量必须大于 0。", "入库单",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _doc.Items.Add(new StockInItem
            {
                ProductId = _products.FirstOrDefault(p => p.Name == name)?.Id,
                ProductName = name,
                Spec = row.Cells["Spec"].Value?.ToString()?.Trim() ?? "",
                Unit = row.Cells["Unit"].Value?.ToString()?.Trim() ?? "",
                Qty = qty,
                Price = ToDecimal(row.Cells["Price"].Value),
                Remark = row.Cells["Remark"].Value?.ToString()?.Trim() ?? ""
            });
        }

        if (_doc.Items.Count == 0)
        {
            Msg.Show(this, "请至少录入一条入库明细。", "入库单",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _doc.TotalAmount = _doc.Items.Sum(i => i.Amount);
        DialogResult = DialogResult.OK;
        Close();
    }
}
