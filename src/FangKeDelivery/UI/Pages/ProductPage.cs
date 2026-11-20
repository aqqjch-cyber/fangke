using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

public sealed class ProductPage : UserControl, IRefreshable
{
    private readonly Repository _repo;
    private readonly TextBox _keyword = Theme.Input();
    private readonly DataGridView _grid = new();
    private readonly Label _summary = new();
    private List<Product> _items = new();

    public ProductPage(Repository repo)
    {
        _repo = repo;
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

        var filter = new Theme.RoundedPanel
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
        _keyword.Width = 240;
        _keyword.PlaceholderText = "商品名称 / 规格 / 备注";
        _keyword.Margin = new Padding(0, 3, 10, 0);
        var search = Theme.PrimaryButton("查询", 76, 30);
        search.Margin = new Padding(0, 3, 8, 0);
        search.Click += (_, _) => Reload();
        var add = Theme.PrimaryButton("+ 新增商品", 100, 30);
        add.Margin = new Padding(0, 3, 8, 0);
        add.Click += (_, _) => Edit(null);
        var edit = Theme.SecondaryButton("编辑", 70, 30);
        edit.Margin = new Padding(0, 3, 8, 0);
        edit.Click += (_, _) => EditSelected();
        var del = Theme.DangerButton("删除", 70, 30);
        del.Margin = new Padding(0, 3, 0, 0);
        del.Click += (_, _) => DeleteSelected();

        flow.Controls.AddRange(new Control[] { _keyword, search, add, edit, del });
        filter.Controls.Add(flow);

        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 10, 12, 10)
        };
        var head = new Label
        {
            Text = "商品资料",
            Dock = DockStyle.Top,
            Height = 34,
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Theme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AutoGenerateColumns = false;
        AddCol("Name", "商品名称", 200);
        AddCol("Spec", "规格", 140);
        AddCol("Unit", "单位", 70, "center");
        AddCol("Price", "参考单价", 100, "right");
        AddCol("Remark", "备注", 180);
        AddCol("CreatedAt", "建档时间", 130);
        _grid.Columns["Price"]!.DefaultCellStyle.Format = "N2";
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };

        card.Controls.Add(_grid);
        card.Controls.Add(head);

        var bar = new Theme.RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8) };
        _summary.Dock = DockStyle.Fill;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _summary.Font = Theme.Font(9.5f, FontStyle.Bold);
        _summary.Text = "共 0 种商品";
        bar.Controls.Add(_summary);

        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(card, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
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

    public void Reload()
    {
        _items = _repo.GetProducts(_keyword.Text);
        _grid.DataSource = _items.Select(p => new
        {
            p.Id, p.Name, p.Spec, p.Unit, p.Price, p.Remark, p.CreatedAt
        }).ToList();
        _summary.Text = $"共 {_items.Count} 种商品";
    }

    private Product? Selected()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        var item = _grid.SelectedRows[0].DataBoundItem;
        if (item is null) return null;
        var id = (long)item.GetType().GetProperty("Id")!.GetValue(item)!;
        return _items.FirstOrDefault(p => p.Id == id);
    }

    private void EditSelected()
    {
        var p = Selected();
        if (p is null) return;
        Edit(p);
    }

    private void Edit(Product? product)
    {
        using var dlg = new ProductEditDialog(product);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        _repo.SaveProduct(dlg.Result);
        Reload();
    }

    private void DeleteSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var ids = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem)
            .Where(d => d is not null)
            .Select(d => (long)d!.GetType().GetProperty("Id")!.GetValue(d)!)
            .ToList();
        if (ids.Count == 0) return;

        if (Msg.Show(FindForm(), $"确定删除选中的 {ids.Count} 种商品吗？", "删除确认",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _repo.DeleteProducts(ids);
        Reload();
    }
}

public sealed class ProductEditDialog : Form
{
    public Product Result { get; }

    private readonly TextBox _name = Theme.Input();
    private readonly TextBox _spec = Theme.Input();
    private readonly TextBox _unit = Theme.Input();
    private readonly TextBox _price = Theme.Input();
    private readonly TextBox _remark = Theme.Input();

    public ProductEditDialog(Product? existing)
    {
        Result = existing is null
            ? new Product()
            : new Product
            {
                Id = existing.Id,
                Name = existing.Name,
                Spec = existing.Spec,
                Unit = existing.Unit,
                Price = existing.Price,
                Remark = existing.Remark,
                CreatedAt = existing.CreatedAt
            };

        Text = existing is null ? "新增商品" : "编辑商品";
        Size = new Size(480, 350);
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
            RowCount = 6,
            BackColor = Color.White,
            Padding = new Padding(0, 6, 0, 0)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 5; i++)
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
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
            c.Margin = new Padding(0, 6, 0, 6);
            t.Controls.Add(c, 1, i);
        }

        _name.Text = Result.Name;
        _spec.Text = Result.Spec;
        _unit.Text = Result.Unit;
        _price.Text = Result.Price.ToString("0.00");
        _remark.Text = Result.Remark;

        Row(0, "商品名称 *", _name);
        Row(1, "规格", _spec);
        Row(2, "单位", _unit);
        Row(3, "参考单价", _price);
        Row(4, "备注", _remark);

        var ok = Theme.PrimaryButton("保存", 88, 32);
        ok.DialogResult = DialogResult.OK;
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                Msg.Show(this, "请填写商品名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                _name.Focus();
                return;
            }
            if (!decimal.TryParse(_price.Text, out var price) || price < 0)
            {
                Msg.Show(this, "参考单价请填写有效数字。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                _price.Focus();
                return;
            }
            Result.Name = _name.Text.Trim();
            Result.Spec = _spec.Text.Trim();
            Result.Unit = _unit.Text.Trim();
            Result.Price = Math.Round(price, 2, MidpointRounding.AwayFromZero);
            Result.Remark = _remark.Text.Trim();
        };
        var cancel = Theme.SecondaryButton("取消", 88, 32);
        cancel.DialogResult = DialogResult.Cancel;

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        bar.Controls.Add(ok);
        bar.Controls.Add(cancel);

        Controls.Add(t);
        Controls.Add(bar);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
