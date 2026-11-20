using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Dialogs;

/// <summary>开单时快速选择商品的对话框。</summary>
public sealed class ProductPickerDialog : Form
{
    private readonly Repository _repo;
    private readonly TextBox _keyword = Theme.Input();
    private readonly DataGridView _grid = new();
    private List<Product> _items = new();

    public Product? Selected { get; private set; }

    public ProductPickerDialog(Repository repo)
    {
        _repo = repo;
        Text = "选择商品";
        Size = new Size(720, 520);
        StartPosition = FormStartPosition.CenterParent;
        Font = Theme.BodyFont;
        BackColor = Color.White;
        MinimizeBox = false;
        Padding = new Padding(16);

        var top = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.White };
        _keyword.Width = 260;
        _keyword.PlaceholderText = "输入名称 / 规格快速筛选";
        _keyword.Location = new Point(0, 8);
        _keyword.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LoadData();
            }
        };
        var search = Theme.PrimaryButton("筛选", 70, 30);
        search.Location = new Point(270, 7);
        search.Click += (_, _) => LoadData();
        top.Controls.Add(_keyword);
        top.Controls.Add(search);

        Theme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AutoGenerateColumns = false;
        _grid.MultiSelect = false;
        AddCol("Name", "商品名称", 200);
        AddCol("Spec", "规格", 140);
        AddCol("Unit", "单位", 70, "center");
        AddCol("Price", "参考单价", 100, "right");
        AddCol("Remark", "备注", 160);
        _grid.Columns["Price"]!.DefaultCellStyle.Format = "N2";
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Confirm(); };

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        var ok = Theme.PrimaryButton("选择", 90, 32);
        ok.Click += (_, _) => Confirm();
        var cancel = Theme.SecondaryButton("取消", 90, 32);
        cancel.DialogResult = DialogResult.Cancel;
        bottom.Controls.Add(ok);
        bottom.Controls.Add(cancel);

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(top);

        LoadData();
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

    private void LoadData()
    {
        _items = _repo.GetProducts(_keyword.Text);
        _grid.DataSource = _items.Select(p => new
        {
            p.Id, p.Name, p.Spec, p.Unit, p.Price, p.Remark
        }).ToList();
    }

    private void Confirm()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            Msg.Show(this, "请先选择一个商品。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var item = _grid.SelectedRows[0].DataBoundItem;
        if (item is null) return;
        var id = (long)item.GetType().GetProperty("Id")!.GetValue(item)!;
        Selected = _items.FirstOrDefault(p => p.Id == id);
        if (Selected is not null)
            DialogResult = DialogResult.OK;
    }
}
