using FangKeDelivery.Core;
using FangKeDelivery.Data;
using FangKeDelivery.Printing;
using FangKeDelivery.UI.Dialogs;

namespace FangKeDelivery.UI.Pages;

public sealed class NoteEditorPage : UserControl, IRefreshable
{
    private readonly Repository _repo;
    private readonly MainForm _main;

    private readonly ComboBox _customer = new();
    private readonly TextBox _phone = Theme.Input();
    private readonly TextBox _address = Theme.Input();
    private readonly DateTimePicker _date = new();
    private readonly TextBox _noteNo = Theme.Input();
    private readonly TextBox _salesman = Theme.Input();
    private readonly TextBox _receiver = Theme.Input();
    private readonly TextBox _remark = Theme.Input();
    private readonly DataGridView _grid = new();
    private readonly Label _lblTotal = new();
    private readonly Label _lblWords = new();
    private readonly Label _lblQty = new();

    private long _noteId;
    private List<Product> _products = new();
    private List<Customer> _customers = new();
    private bool _loading;

    public NoteEditorPage(Repository repo, MainForm main)
    {
        _repo = repo;
        _main = main;
        Dock = DockStyle.Fill;
        BackColor = Theme.PageBack;
        BuildUi();
        Reload();
        NewNote();
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 158));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));

        root.Controls.Add(BuildHeaderCard(), 0, 0);
        root.Controls.Add(BuildGridCard(), 0, 1);
        root.Controls.Add(BuildFooterCard(), 0, 2);

        Controls.Add(root);
    }

    private Control BuildHeaderCard()
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(16, 12, 16, 12)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 3,
            BackColor = Color.White
        };
        for (var i = 0; i < 8; i++)
            grid.ColumnStyles.Add(new ColumnStyle(i % 2 == 0 ? SizeType.Absolute : SizeType.Percent,
                i % 2 == 0 ? 74 : 33));
        for (var i = 0; i < 3; i++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

        _customer.DropDownStyle = ComboBoxStyle.DropDown;
        _customer.Font = Theme.BodyFont;
        _customer.Dock = DockStyle.Fill;
        _customer.Margin = new Padding(0, 4, 12, 4);
        _customer.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _customer.AutoCompleteSource = AutoCompleteSource.ListItems;
        _customer.SelectedIndexChanged += CustomerChanged;
        _customer.Leave += CustomerTyped;

        _date.Format = DateTimePickerFormat.Custom;
        _date.CustomFormat = "yyyy-MM-dd";
        _date.Font = Theme.BodyFont;
        _date.Dock = DockStyle.Fill;
        _date.Margin = new Padding(0, 4, 12, 4);

        _noteNo.ReadOnly = true;
        _noteNo.BackColor = Color.FromArgb(248, 249, 251);
        _noteNo.Dock = DockStyle.Fill;
        _noteNo.Margin = new Padding(0, 4, 12, 4);

        _phone.Dock = DockStyle.Fill;
        _phone.Margin = new Padding(0, 4, 12, 4);
        _address.Dock = DockStyle.Fill;
        _address.Margin = new Padding(0, 4, 12, 4);
        _salesman.Dock = DockStyle.Fill;
        _salesman.Margin = new Padding(0, 4, 12, 4);
        _receiver.Dock = DockStyle.Fill;
        _receiver.Margin = new Padding(0, 4, 12, 4);
        _remark.Dock = DockStyle.Fill;
        _remark.Margin = new Padding(0, 4, 0, 4);

        AddField(grid, 0, 0, "客户", _customer, 2);
        AddField(grid, 4, 0, "电话", _phone, 1);
        AddField(grid, 6, 0, "送货日期", _date, 1);
        AddField(grid, 0, 1, "地址", _address, 2);
        AddField(grid, 4, 1, "单号", _noteNo, 1);
        AddField(grid, 6, 1, "送货员", _salesman, 1);
        AddField(grid, 0, 2, "收货人", _receiver, 1);
        AddField(grid, 2, 2, "备注", _remark, 3);

        card.Controls.Add(grid);
        return card;
    }

    private static void AddField(TableLayoutPanel t, int col, int row, string label, Control input, int span)
    {
        var l = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont,
            Margin = new Padding(0, 4, 6, 4)
        };
        t.Controls.Add(l, col, row);
        t.Controls.Add(input, col + 1, row);
        if (span > 1)
            t.SetColumnSpan(input, span * 2 - 1);
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
            Text = "送货明细",
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            AutoSize = true,
            Location = new Point(2, 11)
        };

        var addBtn = Theme.SecondaryButton("+ 添加行", 92, 30);
        addBtn.Click += (_, _) => AddRow();
        var delBtn = Theme.SecondaryButton("删除行", 80, 30);
        delBtn.Click += (_, _) => DeleteSelectedRows();
        var pickBtn = Theme.SecondaryButton("选择商品", 92, 30);
        pickBtn.Click += (_, _) => PickProduct();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 300,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 6, 0, 0)
        };
        actions.Controls.Add(addBtn);
        actions.Controls.Add(delBtn);
        actions.Controls.Add(pickBtn);

        head.Controls.Add(title);
        head.Controls.Add(actions);

        Theme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.MultiSelect = true;
        BuildGridColumns();
        _grid.CellValueChanged += GridCellValueChanged;
        _grid.CellEndEdit += (_, _) => RecalcTotals();
        _grid.DataError += (_, e) => e.ThrowException = false;
        _grid.EditingControlShowing += GridEditingControlShowing;
        _grid.KeyDown += GridKeyDown;
        _grid.CellDoubleClick += GridCellDoubleClick;

        card.Controls.Add(_grid);
        card.Controls.Add(head);
        return card;
    }

    private void BuildGridColumns()
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

        AddText("Seq", "序号", 40, true, "center");
        AddText("ProductName", "商品名称", 190);
        AddText("Spec", "规格", 100);
        AddText("Unit", "单位", 52, false, "center");
        AddText("Qty", "数量", 68, false, "right");
        AddText("Price", "单价", 80, false, "right");
        AddText("Amount", "金额", 90, true, "right");
        AddText("ItemRemark", "备注", 110);

        _grid.Columns["Amount"]!.DefaultCellStyle.ForeColor = Theme.PrimaryDark;
        _grid.Columns["Amount"]!.DefaultCellStyle.Font = Theme.Font(9.5f, FontStyle.Bold);
    }

    private Control BuildFooterCard()
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(16, 8, 16, 8)
        };

        var left = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        var lblQtyCap = new Label
        {
            Text = "合计数量",
            AutoSize = true,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont,
            Location = new Point(2, 6)
        };
        _lblQty.AutoSize = true;
        _lblQty.Font = Theme.Font(10f, FontStyle.Bold);
        _lblQty.ForeColor = Theme.TextMain;
        _lblQty.Location = new Point(70, 4);
        _lblQty.Text = "0";

        var lblCap = new Label
        {
            Text = "合计金额（大写）",
            AutoSize = true,
            ForeColor = Theme.TextSub,
            Font = Theme.BodyFont,
            Location = new Point(2, 32)
        };
        _lblWords.AutoSize = true;
        _lblWords.Font = Theme.Font(10f, FontStyle.Bold);
        _lblWords.ForeColor = Theme.Accent;
        _lblWords.Location = new Point(116, 30);
        _lblWords.Text = "零元整";

        left.Controls.AddRange(new Control[] { lblQtyCap, _lblQty, lblCap, _lblWords });

        var right = new Panel { Dock = DockStyle.Right, Width = 520, BackColor = Color.White };

        var totalCap = new Label
        {
            Text = "合计 ¥",
            AutoSize = true,
            ForeColor = Theme.TextSub,
            Font = Theme.Font(10f),
            Location = new Point(96, 8)
        };
        _lblTotal.AutoSize = true;
        _lblTotal.Font = Theme.Font(21f, FontStyle.Bold);
        _lblTotal.ForeColor = Theme.PrimaryDark;
        _lblTotal.Text = "0.00";
        _lblTotal.Location = new Point(146, -2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.White,
            Padding = new Padding(0, 4, 0, 0)
        };

        var savePrint = Theme.PrimaryButton("保存并打印", 112, 34);
        savePrint.Click += (_, _) => SaveAndPrint();
        var save = Theme.PrimaryButton("保存", 80, 34);
        save.Click += (_, _) => SaveNote(false);
        var preview = Theme.SecondaryButton("打印预览", 92, 34);
        preview.Click += (_, _) => PrintPreview();
        var clear = Theme.SecondaryButton("新建", 70, 34);
        clear.Click += (_, _) => { if (ConfirmDiscard()) NewNote(); };

        buttons.Controls.Add(savePrint);
        buttons.Controls.Add(save);
        buttons.Controls.Add(preview);
        buttons.Controls.Add(clear);

        right.Controls.Add(totalCap);
        right.Controls.Add(_lblTotal);
        right.Controls.Add(buttons);

        card.Controls.Add(left);
        card.Controls.Add(right);
        return card;
    }

    public void Reload()
    {
        _loading = true;
        try
        {
            _customers = _repo.GetCustomers();
            _products = _repo.GetProducts();

            var keep = _customer.Text;
            _customer.Items.Clear();
            foreach (var c in _customers)
                _customer.Items.Add(c.Name);
            _customer.Text = keep;

            if (string.IsNullOrWhiteSpace(_salesman.Text))
                _salesman.Text = _repo.GetSetting("Salesman", "");
        }
        finally
        {
            _loading = false;
        }
    }

    public void NewNote()
    {
        _noteId = 0;
        _loading = true;
        try
        {
            _grid.Rows.Clear();
            _customer.SelectedIndex = -1;
            _customer.Text = "";
            _phone.Text = "";
            _address.Text = "";
            _remark.Text = "";
            _receiver.Text = "";
            _date.Value = DateTime.Today;
            _salesman.Text = _repo.GetSetting("Salesman", "");
            _noteNo.Text = _repo.NextNoteNo(DateTime.Today);
            for (var i = 0; i < 5; i++)
                AddRow();
        }
        finally
        {
            _loading = false;
        }
        RecalcTotals();
        _grid.Focus();
    }

    public void LoadNote(long id)
    {
        var note = _repo.GetNote(id);
        if (note is null) return;

        _loading = true;
        try
        {
            _noteId = note.Id;
            _noteNo.Text = note.NoteNo;
            _date.Value = note.NoteDate;
            _customer.Text = note.CustomerName;
            _phone.Text = note.CustomerPhone;
            _address.Text = note.CustomerAddress;
            _salesman.Text = note.Salesman;
            _receiver.Text = note.Receiver;
            _remark.Text = note.Remark;

            _grid.Rows.Clear();
            foreach (var item in note.Items)
            {
                var idx = _grid.Rows.Add();
                var row = _grid.Rows[idx];
                row.Cells["Seq"].Value = idx + 1;
                row.Cells["ProductName"].Value = item.ProductName;
                row.Cells["Spec"].Value = item.Spec;
                row.Cells["Unit"].Value = item.Unit;
                row.Cells["Qty"].Value = item.Qty;
                row.Cells["Price"].Value = item.Price;
                row.Cells["Amount"].Value = item.Amount;
                row.Cells["ItemRemark"].Value = item.Remark;
            }
            while (_grid.Rows.Count < 5)
                AddRow();
        }
        finally
        {
            _loading = false;
        }
        RecalcTotals();
    }

    /// <summary>把已有单据的内容载入为一张新单（新单号、今天日期、未送货状态）。</summary>
    public void LoadCopyOf(long sourceId)
    {
        var src = _repo.GetNote(sourceId);
        if (src is null) return;

        _loading = true;
        try
        {
            _noteId = 0;
            _noteNo.Text = _repo.NextNoteNo(DateTime.Today);
            _date.Value = DateTime.Today;
            _customer.Text = src.CustomerName;
            _phone.Text = src.CustomerPhone;
            _address.Text = src.CustomerAddress;
            _salesman.Text = src.Salesman;
            _receiver.Text = src.Receiver;
            _remark.Text = src.Remark;

            _grid.Rows.Clear();
            foreach (var item in src.Items)
            {
                var idx = _grid.Rows.Add();
                var row = _grid.Rows[idx];
                row.Cells["Seq"].Value = idx + 1;
                row.Cells["ProductName"].Value = item.ProductName;
                row.Cells["Spec"].Value = item.Spec;
                row.Cells["Unit"].Value = item.Unit;
                row.Cells["Qty"].Value = item.Qty;
                row.Cells["Price"].Value = item.Price;
                row.Cells["Amount"].Value = item.Amount;
                row.Cells["ItemRemark"].Value = item.Remark;
            }
            while (_grid.Rows.Count < 5)
                AddRow();
        }
        finally
        {
            _loading = false;
        }
        RecalcTotals();
    }

    private void AddRow()
    {
        var idx = _grid.Rows.Add();
        var row = _grid.Rows[idx];
        row.Cells["Seq"].Value = idx + 1;
        row.Cells["Qty"].Value = 1m;
        row.Cells["Price"].Value = 0m;
        row.Cells["Amount"].Value = 0m;
    }

    private void DeleteSelectedRows()
    {
        var targets = _grid.SelectedCells.Cast<DataGridViewCell>()
            .Select(c => c.RowIndex).Distinct().OrderByDescending(i => i).ToList();
        if (targets.Count == 0 && _grid.CurrentRow is not null)
            targets.Add(_grid.CurrentRow.Index);

        foreach (var i in targets)
        {
            if (i >= 0 && i < _grid.Rows.Count)
                _grid.Rows.RemoveAt(i);
        }
        RenumberRows();
        RecalcTotals();
    }

    private void RenumberRows()
    {
        for (var i = 0; i < _grid.Rows.Count; i++)
            _grid.Rows[i].Cells["Seq"].Value = i + 1;
    }

    private void CustomerChanged(object? sender, EventArgs e)
    {
        if (_loading) return;
        if (_customer.SelectedItem is not string name) return;
        var c = _customers.FirstOrDefault(x => x.Name == name);
        if (c is null) return;
        _phone.Text = c.Phone;
        _address.Text = c.Address;
    }

    private void CustomerTyped(object? sender, EventArgs e)
    {
        if (_loading) return;
        var c = _customers.FirstOrDefault(x => x.Name == _customer.Text.Trim());
        if (c is not null && string.IsNullOrWhiteSpace(_phone.Text))
        {
            _phone.Text = c.Phone;
            _address.Text = c.Address;
        }
    }

    private void PickProduct()
    {
        using var dlg = new ProductPickerDialog(_repo);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK || dlg.Selected is null) return;

        var idx = _grid.CurrentRow?.Index ?? -1;
        if (idx < 0 || idx >= _grid.Rows.Count)
        {
            AddRow();
            idx = _grid.Rows.Count - 1;
        }
        FillRowWithProduct(_grid.Rows[idx], dlg.Selected);
        RecalcTotals();
    }

    private void GridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name == "ProductName")
            PickProduct();
    }

    private static void FillRowWithProduct(DataGridViewRow row, Product p)
    {
        row.Cells["ProductName"].Value = p.Name;
        row.Cells["Spec"].Value = p.Spec;
        row.Cells["Unit"].Value = p.Unit;
        if (ToDecimal(row.Cells["Qty"].Value) == 0)
            row.Cells["Qty"].Value = 1m;
        row.Cells["Price"].Value = p.Price;
        row.Cells["Amount"].Value = CalcAmount(row);
    }

    private void GridEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (_grid.CurrentCell?.OwningColumn?.Name != "ProductName") return;
        if (e.Control is not TextBox tb) return;

        tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
        var src = new AutoCompleteStringCollection();
        src.AddRange(_products.Select(p => p.Name).Distinct().ToArray());
        tb.AutoCompleteCustomSource = src;
    }

    private void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loading || e.RowIndex < 0) return;
        var col = _grid.Columns[e.ColumnIndex].Name;
        var row = _grid.Rows[e.RowIndex];

        if (col == "ProductName")
        {
            var p = _products.FirstOrDefault(x => x.Name == row.Cells["ProductName"].Value?.ToString());
            if (p is not null)
            {
                row.Cells["Spec"].Value = p.Spec;
                row.Cells["Unit"].Value = p.Unit;
                if (ToDecimal(row.Cells["Price"].Value) == 0)
                    row.Cells["Price"].Value = p.Price;
            }
        }

        if (col is "Qty" or "Price")
            row.Cells["Amount"].Value = CalcAmount(row);

        RecalcTotals();
    }

    private static decimal CalcAmount(DataGridViewRow row) =>
        Math.Round(ToDecimal(row.Cells["Qty"].Value) * ToDecimal(row.Cells["Price"].Value), 2,
            MidpointRounding.AwayFromZero);

    private static decimal ToDecimal(object? v) =>
        v is null ? 0m : decimal.TryParse(v.ToString(), out var d) ? d : 0m;

    private void GridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            SaveNote(false);
        }
        else if (e.Control && e.KeyCode == Keys.Delete)
        {
            e.Handled = true;
            DeleteSelectedRows();
        }
    }

    private void RecalcTotals()
    {
        decimal total = 0m, qty = 0m;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var amt = CalcAmount(row);
            if (ToDecimal(row.Cells["Amount"].Value) != amt)
                row.Cells["Amount"].Value = amt;

            // 空行不计入合计，与保存逻辑保持一致
            if (string.IsNullOrWhiteSpace(row.Cells["ProductName"].Value?.ToString()))
                continue;

            total += amt;
            qty += ToDecimal(row.Cells["Qty"].Value);
        }

        _lblTotal.Text = total.ToString("N2");
        _lblWords.Text = MoneyToChinese.Convert(total);
        _lblQty.Text = qty.ToString("0.###");
    }

    private DeliveryNote CollectNote()
    {
        var note = new DeliveryNote
        {
            Id = _noteId,
            NoteNo = string.IsNullOrWhiteSpace(_noteNo.Text) ? _repo.NextNoteNo(_date.Value) : _noteNo.Text.Trim(),
            CustomerName = _customer.Text.Trim(),
            CustomerPhone = _phone.Text.Trim(),
            CustomerAddress = _address.Text.Trim(),
            NoteDate = _date.Value.Date,
            Remark = _remark.Text.Trim(),
            Salesman = _salesman.Text.Trim(),
            Receiver = _receiver.Text.Trim(),
            Maker = _repo.GetSetting("Maker", "")
        };

        note.CustomerId = _customers.FirstOrDefault(c => c.Name == note.CustomerName)?.Id;

        foreach (DataGridViewRow row in _grid.Rows)
        {
            var name = row.Cells["ProductName"].Value?.ToString()?.Trim() ?? "";
            // 空行（未填商品名称）不写入明细，即使默认数量为 1
            if (name.Length == 0) continue;
            var q = ToDecimal(row.Cells["Qty"].Value);

            note.Items.Add(new NoteItem
            {
                ProductId = _products.FirstOrDefault(p => p.Name == name)?.Id,
                ProductName = name,
                Spec = row.Cells["Spec"].Value?.ToString()?.Trim() ?? "",
                Unit = row.Cells["Unit"].Value?.ToString()?.Trim() ?? "",
                Qty = q,
                Price = ToDecimal(row.Cells["Price"].Value),
                Remark = row.Cells["ItemRemark"].Value?.ToString()?.Trim() ?? ""
            });
        }

        note.TotalAmount = note.Items.Sum(i => i.Amount);
        return note;
    }

    private bool Validate(DeliveryNote note)
    {
        if (string.IsNullOrWhiteSpace(note.CustomerName))
        {
            Msg.Show(FindForm(), "请填写客户名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _customer.Focus();
            return false;
        }
        if (note.Items.Count == 0)
        {
            Msg.Show(FindForm(), "请至少录入一条送货明细。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _grid.Focus();
            return false;
        }
        return true;
    }

    private bool SaveNote(bool silent)
    {
        var note = CollectNote();
        if (!Validate(note)) return false;

        try
        {
            var isNew = note.Id == 0;
            _noteId = _repo.SaveNote(note);
            _noteNo.Text = note.NoteNo;

            if (!silent)
                Msg.Show(FindForm(), isNew ? "送货单已保存。" : "送货单已更新。", "保存成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

            _main.InvalidatePages("list", "report");
            _main.SetStatus($"已保存送货单 {note.NoteNo}，金额 {note.TotalAmount:N2} 元");
            return true;
        }
        catch (Exception ex)
        {
            Msg.Show(FindForm(), "保存失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void SaveAndPrint()
    {
        var note = CollectNote();
        if (!Validate(note)) return;
        try
        {
            _noteId = _repo.SaveNote(note);
            _noteNo.Text = note.NoteNo;
            _main.InvalidatePages("list", "report");
            _main.SetStatus($"已保存送货单 {note.NoteNo}");

            var saved = _repo.GetNote(_noteId);
            if (saved is not null)
                NotePrinter.ShowPrintDialog(saved, _repo, FindForm());
        }
        catch (Exception ex)
        {
            Msg.Show(FindForm(), "保存或打印失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PrintPreview()
    {
        var note = CollectNote();
        if (!Validate(note)) return;
        NotePrinter.ShowPreview(note, _repo, FindForm());
    }

    private bool ConfirmDiscard()
    {
        if (_noteId != 0) return true;
        var hasData = _grid.Rows.Cast<DataGridViewRow>()
            .Any(r => !string.IsNullOrWhiteSpace(r.Cells["ProductName"].Value?.ToString()));
        if (!hasData) return true;

        return Msg.Show(FindForm(), "当前单据尚未保存，确定放弃并新建吗？", "确认",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
