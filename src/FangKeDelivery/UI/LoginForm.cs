using FangKeDelivery.Core;

namespace FangKeDelivery.UI;

public sealed class LoginForm : Form
{
    private readonly AuthService _auth;
    private TextBox _user = null!;
    private TextBox _pass = null!;
    private CheckBox _show = null!;
    private Label _error = null!;
    private Button _ok = null!;

    public LoginForm(AuthService auth)
    {
        _auth = auth;

        Text = "登录 — 方可送货单管理系统";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(860, 480);
        BackColor = Color.White;
        Font = Theme.BodyFont;

        var banner = BuildBanner();
        var form = BuildForm();

        Controls.Add(form);
        Controls.Add(banner);

        AcceptButton = _ok;
        Shown += (_, _) => _user.Focus();
    }

    private Panel BuildBanner()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 380,
            BackColor = Theme.Primary
        };

        var title = new Label
        {
            Text = "方可送货单",
            Font = Theme.Font(28f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(44, 130)
        };

        var sub = new Label
        {
            Text = "单机版 · 送货单据管理",
            Font = Theme.Font(11f),
            ForeColor = Color.FromArgb(210, 227, 250),
            AutoSize = true,
            Location = new Point(46, 190)
        };

        var line = new Panel
        {
            BackColor = Color.FromArgb(120, 190, 220, 250),
            Location = new Point(46, 226),
            Size = new Size(70, 2)
        };

        var tips = new Label
        {
            Text = "送货单开单 · 三联打印\n客户商品资料 · 统计报表\n数据本地保存，不联网",
            Font = Theme.Font(9.5f),
            ForeColor = Color.FromArgb(190, 214, 242),
            AutoSize = true,
            Location = new Point(46, 252)
        };

        var ver = new Label
        {
            Text = "v1.1.0",
            Font = Theme.SmallFont,
            ForeColor = Color.FromArgb(150, 185, 225),
            AutoSize = true,
            Location = new Point(46, 400)
        };

        panel.Controls.AddRange(new Control[] { title, sub, line, tips, ver });
        return panel;
    }

    private Panel BuildForm()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(56, 0, 56, 0)
        };

        var heading = new Label
        {
            Text = "账号登录",
            Font = Theme.Font(17f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            AutoSize = true,
            Location = new Point(56, 84)
        };

        var hint = new Label
        {
            Text = "请输入您的账号和密码",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextSub,
            AutoSize = true,
            Location = new Point(58, 122)
        };

        var userLabel = Theme.FieldLabel("用户名");
        userLabel.Location = new Point(56, 168);

        _user = new TextBox
        {
            Location = new Point(56, 190),
            Width = 300,
            Font = Theme.Font(11f),
            BorderStyle = BorderStyle.FixedSingle
        };

        var passLabel = Theme.FieldLabel("密码");
        passLabel.Location = new Point(56, 238);

        _pass = new TextBox
        {
            Location = new Point(56, 260),
            Width = 300,
            Font = Theme.Font(11f),
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = true
        };

        _show = new CheckBox
        {
            Text = "显示密码",
            Location = new Point(56, 292),
            AutoSize = true,
            Font = Theme.SmallFont,
            ForeColor = Theme.TextSub
        };
        _show.CheckedChanged += (_, _) => _pass.UseSystemPasswordChar = !_show.Checked;

        _error = new Label
        {
            Location = new Point(56, 318),
            Size = new Size(320, 32),
            Font = Theme.SmallFont,
            ForeColor = Theme.Danger,
            Text = ""
        };

        _ok = Theme.PrimaryButton("登 录", 300, 40);
        _ok.Location = new Point(56, 356);
        _ok.Click += (_, _) => DoLogin();

        var cancel = Theme.SecondaryButton("退 出", 300, 34);
        cancel.Location = new Point(56, 404);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        var seed = new Label
        {
            Text = _auth.HasAnyUserHint(),
            Font = Theme.SmallFont,
            ForeColor = Theme.TextSub,
            Location = new Point(56, 444),
            Size = new Size(360, 18)
        };

        panel.Controls.AddRange(new Control[]
        {
            heading, hint, userLabel, _user, passLabel, _pass, _show, _error, _ok, cancel, seed
        });
        return panel;
    }

    private void DoLogin()
    {
        _ok.Enabled = false;
        try
        {
            var result = _auth.Login(_user.Text, _pass.Text);
            if (result.Ok)
            {
                _error.Text = "";
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _error.Text = "✕ " + result.Message;
            _pass.SelectAll();
            _pass.Focus();
        }
        finally
        {
            _ok.Enabled = true;
        }
    }
}
