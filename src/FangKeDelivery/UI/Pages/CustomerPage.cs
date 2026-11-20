using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

public sealed class CustomerPage : UserControl, IRefreshable
{
    private readonly Repository _repo;
    private readonly TextBox _keyword = Theme.Input();
    private readonly DataGridView _grid = new();
    private readonly Label _summary = new();
    private List<Customer> _items = new();

    public CustomerPage(Repository repo)
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
        _keyword.PlaceholderText = "客户名称 / 联系人 / 电话 / 地址";
        _keyword.Margin = new Padding(0, 3, 10, 0);
        var search = Theme.PrimaryButton("查询", 76, 30);
        search.Margin = new Padding(0, 3, 8, 0);
        search.Click += (_, _) => Reload();
        var add = Theme.PrimaryButton("+ 新增客户", 100, 30);
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
            Text = "客户资料",
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
        AddCol("Name", "客户名称", 180);
        AddCol("Contact", "联系人", 90);
        AddCol("Phone", "联系电话", 120);
        AddCol("Address", "地址", 220);
        AddCol("Remark", "备注", 140);
        AddCol("CreatedAt", "建档时间", 130);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };

        card.Controls.Add(_grid);
        card.Controls.Add(head);

        var bar = new Theme.RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8) };
        _summary.Dock = DockStyle.Fill;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _summary.Font = Theme.Font(9.5f, FontStyle.Bold);
        _summary.Text = "共 0 位客户";
        bar.Controls.Add(_summary);

        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(card, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
    }

    private void AddCol(string prop, string header, int fill)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = prop,
            DataPropertyName = prop,
            HeaderText = header,
            FillWeight = fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
    }

    public void Reload()
    {
        _items = _repo.GetCustomers(_keyword.Text);
        _grid.DataSource = _items.Select(c => new
        {
            c.Id, c.Name, c.Contact, c.Phone, c.Address, c.Remark, c.CreatedAt
        }).ToList();
        _summary.Text = $"共 {_items.Count} 位客户";
    }

    private Customer? Selected()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        var item = _grid.SelectedRows[0].DataBoundItem;
        if (item is null) return null;
        var id = (long)item.GetType().GetProperty("Id")!.GetValue(item)!;
        return _items.FirstOrDefault(c => c.Id == id);
    }

    private void EditSelected()
    {
        var c = Selected();
        if (c is null) return;
        Edit(c);
    }

    private void Edit(Customer? customer)
    {
        using var dlg = new CustomerEditDialog(customer);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        _repo.SaveCustomer(dlg.Result);
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

        if (Msg.Show(FindForm(), $"确定删除选中的 {ids.Count} 位客户吗？", "删除确认",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _repo.DeleteCustomers(ids);
        Reload();
    }
}

public sealed class CustomerEditDialog : Form
{
    public Customer Result { get; }

    private readonly TextBox _name = Theme.Input();
    private readonly TextBox _contact = Theme.Input();
    private readonly TextBox _phone = Theme.Input();
    private readonly TextBox _address = Theme.Input();
    private readonly TextBox _remark = Theme.Input();

    public CustomerEditDialog(Customer? existing)
    {
        Result = existing is null
            ? new Customer()
            : new Customer
            {
                Id = existing.Id,
                Name = existing.Name,
                Contact = existing.Contact,
                Phone = existing.Phone,
                Address = existing.Address,
                Remark = existing.Remark,
                CreatedAt = existing.CreatedAt
            };

        Text = existing is null ? "新增客户" : "编辑客户";
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
        _contact.Text = Result.Contact;
        _phone.Text = Result.Phone;
        _address.Text = Result.Address;
        _remark.Text = Result.Remark;

        Row(0, "客户名称 *", _name);
        Row(1, "联系人", _contact);
        Row(2, "联系电话", _phone);
        Row(3, "地址", _address);
        Row(4, "备注", _remark);

        var ok = Theme.PrimaryButton("保存", 88, 32);
        ok.DialogResult = DialogResult.OK;
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                Msg.Show(this, "请填写客户名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                _name.Focus();
                return;
            }
            Result.Name = _name.Text.Trim();
            Result.Contact = _contact.Text.Trim();
            Result.Phone = _phone.Text.Trim();
            Result.Address = _address.Text.Trim();
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
