using FangKeDelivery.Core;

namespace FangKeDelivery.UI.Pages;

/// <summary>设置密码。withOld=true 时用于"修改自己的密码"，需要原密码。</summary>
public sealed class PasswordDialog : Form
{
    private readonly TextBox _old;
    private readonly TextBox _pwd;
    private readonly TextBox _pwd2;

    public string OldPassword => _old.Text;
    public string Password => _pwd.Text;

    public PasswordDialog(string title, bool withOld)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = Theme.BodyFont;
        ClientSize = new Size(380, withOld ? 300 : 240);

        var y = 24;

        _old = new TextBox();
        if (withOld)
        {
            var oldLabel = Theme.FieldLabel("原密码");
            oldLabel.Location = new Point(28, y);
            _old = Theme.Input(300);
            _old.Location = new Point(28, y + 22);
            _old.UseSystemPasswordChar = true;
            Controls.AddRange(new Control[] { oldLabel, _old });
            y += 62;
        }

        var pwdLabel = Theme.FieldLabel("新密码（至少 6 位）");
        pwdLabel.Location = new Point(28, y);
        _pwd = Theme.Input(300);
        _pwd.Location = new Point(28, y + 22);
        _pwd.UseSystemPasswordChar = true;
        y += 62;

        var pwd2Label = Theme.FieldLabel("再输入一次新密码");
        pwd2Label.Location = new Point(28, y);
        _pwd2 = Theme.Input(300);
        _pwd2.Location = new Point(28, y + 22);
        _pwd2.UseSystemPasswordChar = true;

        var ok = Theme.PrimaryButton("确 定", 110, 36);
        ok.Location = new Point(28, ClientSize.Height - 60);
        ok.Click += (_, _) => Confirm();

        var cancel = Theme.SecondaryButton("取 消", 110, 36);
        cancel.Location = new Point(150, ClientSize.Height - 60);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange(new Control[] { pwdLabel, _pwd, pwd2Label, _pwd2, ok, cancel });

        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => (withOld ? _old : _pwd).Focus();
    }

    private void Confirm()
    {
        if (_pwd.Text != _pwd2.Text)
        {
            Msg.Show(this, "两次输入的密码不一致", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 强度校验对"自己改密码"和"管理员重置密码"是同一套要求
        var weak = PasswordHasher.Validate(_pwd.Text);
        if (weak is not null)
        {
            Msg.Show(this, weak, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
