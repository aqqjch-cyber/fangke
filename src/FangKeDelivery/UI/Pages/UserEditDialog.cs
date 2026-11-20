using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

/// <summary>新增/修改用户。新增时需要设初始密码，修改时密码走"重置密码"单独处理。</summary>
public sealed class UserEditDialog : Form
{
    private readonly Repository _repo;
    private readonly AuthService _auth;
    private readonly User? _existing;

    private readonly TextBox _name;
    private readonly TextBox _display;
    private readonly ComboBox _role;
    private readonly CheckBox _enabled;
    private readonly TextBox _pwd;
    private readonly TextBox _pwd2;

    public UserEditDialog(Repository repo, AuthService auth, User? existing)
    {
        _repo = repo;
        _auth = auth;
        _existing = existing;

        Text = existing is null ? "新增用户" : $"修改用户 — {existing.UserName}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, existing is null ? 380 : 300);
        BackColor = Color.White;
        Font = Theme.BodyFont;

        var y = 24;

        var nameLabel = Theme.FieldLabel("用户名");
        nameLabel.Location = new Point(28, y);
        _name = Theme.Input(340);
        _name.Location = new Point(28, y + 22);
        _name.Font = Theme.Font(10.5f);
        if (existing is not null)
        {
            _name.Text = existing.UserName;
            _name.ReadOnly = true;
            _name.BackColor = Color.FromArgb(246, 247, 249);
        }
        y += 62;

        var displayLabel = Theme.FieldLabel("姓名 / 显示名");
        displayLabel.Location = new Point(28, y);
        _display = Theme.Input(340);
        _display.Location = new Point(28, y + 22);
        _display.Font = Theme.Font(10.5f);
        _display.Text = existing?.DisplayName ?? "";
        y += 62;

        var roleLabel = Theme.FieldLabel("角色");
        roleLabel.Location = new Point(28, y);
        _role = new ComboBox
        {
            Location = new Point(28, y + 22),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Font(10.5f)
        };
        _role.Items.AddRange(new object[] { UserRole.管理员, UserRole.员工 });
        _role.SelectedItem = existing?.Role ?? UserRole.员工;

        _enabled = new CheckBox
        {
            Text = "启用该账号",
            Location = new Point(190, y + 24),
            AutoSize = true,
            Checked = existing?.IsEnabled ?? true
        };
        y += 62;

        if (existing is null)
        {
            var pwdLabel = Theme.FieldLabel("初始密码（至少 6 位）");
            pwdLabel.Location = new Point(28, y);
            _pwd = Theme.Input(160);
            _pwd.Location = new Point(28, y + 22);
            _pwd.UseSystemPasswordChar = true;

            _pwd2 = Theme.Input(160);
            _pwd2.Location = new Point(208, y + 22);
            _pwd2.UseSystemPasswordChar = true;
            _pwd2.PlaceholderText = "再输入一次";

            Controls.AddRange(new Control[] { pwdLabel, _pwd, _pwd2 });
        }
        else
        {
            _pwd = new TextBox();
            _pwd2 = new TextBox();
            var note = new Label
            {
                Text = "如需修改密码，请使用「重置密码」功能。",
                Location = new Point(28, y),
                AutoSize = true,
                ForeColor = Theme.TextSub,
                Font = Theme.SmallFont
            };
            Controls.Add(note);
        }

        var ok = Theme.PrimaryButton("保 存", 120, 36);
        ok.Location = new Point(28, ClientSize.Height - 60);
        ok.Click += (_, _) => Save();

        var cancel = Theme.SecondaryButton("取 消", 120, 36);
        cancel.Location = new Point(160, ClientSize.Height - 60);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange(new Control[] { nameLabel, _name, displayLabel, _display, roleLabel, _role, _enabled, ok, cancel });

        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void Save()
    {
        if (_existing is null)
        {
            if (_pwd.Text != _pwd2.Text)
            {
                Msg.Show(this, "两次输入的密码不一致", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = _auth.CreateUser(_name.Text, _display.Text,
                (UserRole)(_role.SelectedItem ?? UserRole.员工), _pwd.Text);
            if (!result.Ok)
            {
                Msg.Show(this, result.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        else
        {
            var result = _auth.UpdateUser(_existing, _display.Text,
                (UserRole)(_role.SelectedItem ?? UserRole.员工), _enabled.Checked);
            if (!result.Ok)
            {
                Msg.Show(this, result.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
