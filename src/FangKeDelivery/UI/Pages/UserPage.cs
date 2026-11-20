using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

public sealed class UserPage : UserControl
{
    private readonly Repository _repo;
    private readonly AuthService _auth;
    private readonly DataGridView _grid;

    public UserPage(Repository repo, AuthService auth)
    {
        _repo = repo;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = Theme.PageBack;
        Padding = new Padding(0);

        var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.PageBack };

        var add = Theme.PrimaryButton("+ 新增用户", 110);
        add.Location = new Point(0, 8);
        add.Click += (_, _) => AddUser();

        var edit = Theme.SecondaryButton("修改", 88);
        edit.Location = new Point(120, 8);
        edit.Click += (_, _) => EditUser();

        var reset = Theme.SecondaryButton("重置密码", 100);
        reset.Location = new Point(216, 8);
        reset.Click += (_, _) => ResetPassword();

        var del = Theme.DangerButton("删除", 88);
        del.Location = new Point(324, 8);
        del.Click += (_, _) => DeleteUser();

        var mima = Theme.SecondaryButton("修改我的密码", 128);
        mima.Location = new Point(420, 8);
        mima.Click += (_, _) => ChangeOwnPassword();

        bar.Controls.AddRange(new Control[] { add, edit, reset, del, mima });

        _grid = new DataGridView { Dock = DockStyle.Fill };
        Theme.StyleGrid(_grid);
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoGenerateColumns = false;
        _grid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "UserName", HeaderText = "用户名", FillWeight = 18 },
            new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "姓名", FillWeight = 18 },
            new DataGridViewTextBoxColumn { Name = "Role", HeaderText = "角色", FillWeight = 12 },
            new DataGridViewTextBoxColumn { Name = "Enabled", HeaderText = "状态", FillWeight = 10 },
            new DataGridViewTextBoxColumn { Name = "LastLogin", HeaderText = "最后登录", FillWeight = 24 },
            new DataGridViewTextBoxColumn { Name = "Created", HeaderText = "创建时间", FillWeight = 20 }
        });
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditUser(); };

        var card = Theme.Card(_grid);
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(1);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBack };
        body.Controls.Add(card);

        Controls.Add(body);
        Controls.Add(bar);

        Load += (_, _) => Reload();
    }

    private void Reload()
    {
        _grid.Rows.Clear();
        foreach (var u in _repo.ListUsers())
        {
            var i = _grid.Rows.Add(
                u.UserName,
                u.DisplayName,
                u.Role.ToString(),
                u.IsEnabled ? "启用" : "停用",
                string.IsNullOrEmpty(u.LastLoginAt) ? "从未登录" : u.LastLoginAt,
                u.CreatedAt);
            _grid.Rows[i].Tag = u;
            if (!u.IsEnabled) _grid.Rows[i].DefaultCellStyle.ForeColor = Theme.TextSub;
        }
    }

    private User? Selected() =>
        _grid.CurrentRow?.Tag as User;

    private void AddUser()
    {
        using var dlg = new UserEditDialog(_repo, _auth, null);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
    }

    private void EditUser()
    {
        var u = Selected();
        if (u is null) { Msg.Show(FindForm(), "请先选择一个用户", "提示"); return; }

        using var dlg = new UserEditDialog(_repo, _auth, u);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Reload();
    }

    private void ResetPassword()
    {
        var u = Selected();
        if (u is null) { Msg.Show(FindForm(), "请先选择一个用户", "提示"); return; }

        using var dlg = new PasswordDialog($"重置 {u.UserName} 的密码", false);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        var result = _auth.ResetPassword(u, dlg.Password);
        Msg.Show(FindForm(), result.Message, "重置密码",
            MessageBoxButtons.OK,
            result.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private void DeleteUser()
    {
        var u = Selected();
        if (u is null) { Msg.Show(FindForm(), "请先选择一个用户", "提示"); return; }

        if (Msg.Show(FindForm(), $"确定要删除用户「{u.UserName}」吗？此操作不可撤销。", "删除用户",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var result = _auth.DeleteUser(u);
        Msg.Show(FindForm(), result.Message, "删除用户",
            MessageBoxButtons.OK,
            result.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        Reload();
    }

    private void ChangeOwnPassword()
    {
        using var dlg = new PasswordDialog("修改我的密码", true);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        var result = _auth.ChangeOwnPassword(dlg.OldPassword, dlg.Password);
        Msg.Show(FindForm(), result.Message, "修改密码",
            MessageBoxButtons.OK,
            result.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }
}
