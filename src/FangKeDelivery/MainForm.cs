using FangKeDelivery.Core;
using FangKeDelivery.Data;
using FangKeDelivery.UI;
using FangKeDelivery.UI.Pages;

namespace FangKeDelivery;

public sealed class MainForm : Form
{
    private readonly Repository _repo;
    private readonly AuthService _auth;
    private readonly Panel _content;
    private readonly Label _titleLabel;
    private readonly Label _crumbLabel;
    private Label _userLabel = null!;
    private Button _userNavBtn = null!;
    private readonly StatusStrip _status;
    private readonly ToolStripStatusLabel _statusLeft;
    private readonly ToolStripStatusLabel _statusRight;
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, UserControl> _pages = new();
    private string _current = "";

    public MainForm(Repository repo, AuthService auth)
    {
        _repo = repo;
        _auth = auth;

        Text = "万方商贸管家 — 单机版";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 720);
        Size = new Size(1320, 820);
        BackColor = Theme.PageBack;
        Font = Theme.BodyFont;
        Icon = LoadAppIcon();
        FormClosing += (_, _) => _repo.Checkpoint();

        var menu = BuildMenu();

        var header = BuildHeader(out _titleLabel, out _crumbLabel);

        _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBack, Padding = new Padding(16, 12, 16, 8) };

        var nav = BuildNav();

        _status = new StatusStrip { BackColor = Color.White, SizingGrip = false, Font = Theme.SmallFont };
        _statusLeft = new ToolStripStatusLabel { Text = "就绪", Spring = false, ForeColor = Theme.TextSub };
        _statusRight = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.TextSub };
        _status.Items.Add(_statusLeft);
        _status.Items.Add(_statusRight);

        Controls.Add(_content);
        Controls.Add(nav);
        Controls.Add(header);
        Controls.Add(menu);
        Controls.Add(_status);
        MainMenuStrip = menu;

        ShowPage("note");
        UpdateUserBadge();
        RefreshStatus();
    }

    /// <summary>右上角显示当前登录人，员工额外标注角色。</summary>
    private void UpdateUserBadge()
    {
        var u = _auth.Current;
        if (u is null) { _userLabel.Text = ""; return; }
        var name = string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName;
        _userLabel.Text = $"{name}（{u.Role}）";
        _userLabel.Left = Math.Max(200, (_userLabel.Parent?.Width ?? 1000) - _userLabel.Width - 90);
    }

    private void ChangeOwnPassword()
    {
        using var dlg = new PasswordDialog("修改我的密码", true);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var r = _auth.ChangeOwnPassword(dlg.OldPassword, dlg.Password);
        Msg.Show(this, r.Message, "修改密码", MessageBoxButtons.OK,
            r.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    /// <summary>退出当前账号回到登录窗口；取消登录则关闭程序。</summary>
    private void SwitchUser()
    {
        if (Msg.Show(this, "确定要退出当前账号吗？未保存的单据将会丢失。", "切换账号",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _auth.Logout();
        using var login = new LoginForm(_auth);
        if (login.ShowDialog(this) != DialogResult.OK)
        {
            Close();
            return;
        }

        // 角色可能变了，重建整个界面（导航项、菜单都跟角色相关）
        var next = new MainForm(_repo, _auth);
        next.Show();
        Hide();
        next.FormClosed += (_, _) => Close();
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            var stream = typeof(MainForm).Assembly.GetManifestResourceStream("FangKeDelivery.app.ico");
            if (stream is not null) return new Icon(stream);
            var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            return exeIcon;
        }
        catch
        {
            return null;
        }
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip
        {
            BackColor = Color.White,
            Font = Theme.BodyFont,
            Padding = new Padding(8, 4, 0, 4),
            Renderer = new ToolStripProfessionalRenderer(new MenuColors())
        };

        var file = new ToolStripMenuItem("文件(&F)");
        file.DropDownItems.Add(Item("新建送货单(&N)", Keys.Control | Keys.N, () => ShowPage("note")));
        file.DropDownItems.Add(new ToolStripSeparator());
        if (_auth.IsAdmin)
        {
            file.DropDownItems.Add(Item("备份数据(&B)...", Keys.None, BackupData));
            file.DropDownItems.Add(Item("恢复数据(&R)...", Keys.None, RestoreData));
            file.DropDownItems.Add(new ToolStripSeparator());
        }
        file.DropDownItems.Add(Item("切换账号(&S)...", Keys.None, SwitchUser));
        file.DropDownItems.Add(Item("退出(&X)", Keys.Alt | Keys.F4, Close));

        var basic = new ToolStripMenuItem("基础资料(&M)");
        basic.DropDownItems.Add(Item("客户资料(&C)", Keys.None, () => ShowPage("customer")));
        basic.DropDownItems.Add(Item("商品资料(&P)", Keys.None, () => ShowPage("product")));

        var docs = new ToolStripMenuItem("单据管理(&D)");
        docs.DropDownItems.Add(Item("送货单列表(&L)", Keys.None, () => ShowPage("list")));
        docs.DropDownItems.Add(Item("单据查询(&Q)", Keys.None, () => ShowPage("list")));
        if (_auth.IsAdmin)
            docs.DropDownItems.Add(Item("审核中心(&A)", Keys.None, () => ShowPage("audit")));

        var stockMenu = new ToolStripMenuItem("进销存(&S)");
        stockMenu.DropDownItems.Add(Item("入库单(&I)", Keys.None, () => ShowPage("stockin")));
        stockMenu.DropDownItems.Add(Item("库存管理(&K)", Keys.None, () => ShowPage("stock")));
        stockMenu.DropDownItems.Add(Item("库存流水(&M)", Keys.None, () => ShowPage("stock")));

        var report = new ToolStripMenuItem("统计报表(&R)");
        report.DropDownItems.Add(Item("销售统计(&S)", Keys.None, () => ShowPage("report")));

        var tool = new ToolStripMenuItem("系统(&T)");
        if (_auth.IsAdmin)
        {
            tool.DropDownItems.Add(Item("系统设置(&O)", Keys.None, () => ShowPage("settings")));
            tool.DropDownItems.Add(Item("用户管理(&U)", Keys.None, () => ShowPage("users")));
            tool.DropDownItems.Add(new ToolStripSeparator());
        }
        tool.DropDownItems.Add(Item("修改我的密码(&P)...", Keys.None, ChangeOwnPassword));
        tool.DropDownItems.Add(Item("打开数据目录(&D)", Keys.None, () =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.DataDirectory)
            { UseShellExecute = true })));

        var help = new ToolStripMenuItem("帮助(&H)");
        help.DropDownItems.Add(Item("使用说明(&U)", Keys.F1, ShowHelp));
        help.DropDownItems.Add(Item("关于(&A)", Keys.None, ShowAbout));

        menu.Items.AddRange(new ToolStripItem[] { file, basic, docs, stockMenu, report, tool, help });
        return menu;
    }

    private static ToolStripMenuItem Item(string text, Keys shortcut, Action action)
    {
        var mi = new ToolStripMenuItem(text);
        if (shortcut != Keys.None)
            mi.ShortcutKeys = shortcut;
        mi.Click += (_, _) => action();
        return mi;
    }

    private Panel BuildHeader(out Label title, out Label crumb)
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Theme.Primary };

        var logo = new Panel { Size = new Size(34, 34), Location = new Point(20, 14), BackColor = Theme.Primary };
        logo.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var white = new SolidBrush(Color.White);
            g.FillRectangle(white, 6, 3, 20, 26);
            using var blue = new SolidBrush(Theme.Primary);
            for (var i = 0; i < 3; i++)
                g.FillRectangle(blue, 10, 9 + i * 6, 12, 2);
            using var green = new SolidBrush(Color.FromArgb(120, 200, 130));
            g.FillEllipse(green, 20, 20, 12, 12);
        };

        title = new Label
        {
            Text = "万方商贸管家",
            ForeColor = Color.White,
            Font = Theme.Font(14f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(64, 10)
        };

        crumb = new Label
        {
            Text = "单机版 · 本地数据存储",
            ForeColor = Color.FromArgb(190, 214, 245),
            Font = Theme.Font(8.5f),
            AutoSize = true,
            Location = new Point(66, 36)
        };

        var version = new Label
        {
            Text = "v1.3",
            ForeColor = Color.FromArgb(170, 200, 240),
            Font = Theme.Font(8.5f),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        version.Location = new Point(header.Width - 50, 22);

        _userLabel = new Label
        {
            Text = "",
            ForeColor = Color.White,
            Font = Theme.Font(9f),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _userLabel.Location = new Point(header.Width - 220, 21);

        header.Controls.AddRange(new Control[] { logo, title, crumb, _userLabel, version });
        header.Resize += (_, _) =>
        {
            version.Left = header.Width - version.Width - 22;
            _userLabel.Left = version.Left - _userLabel.Width - 18;
        };
        return header;
    }

    private Control BuildNav()
    {
        var nav = new Panel { Dock = DockStyle.Left, Width = 190, BackColor = Color.White, Padding = new Padding(10, 14, 10, 10) };

        var items = new List<(string key, string text, string glyph)>
        {
            ("note", "开单 / 送货单", "✎"),
            ("list", "单据列表", "☰"),
            ("stockin", "入库单", "📥"),
            ("stock", "库存管理", "📦"),
            ("customer", "客户资料", "👥"),
            ("product", "商品资料", "🏷"),
            ("report", "统计报表", "📊")
        };
        if (_auth.IsAdmin) items.Add(("audit", "审核中心", "✔"));
        if (_auth.IsAdmin) items.Add(("settings", "系统设置", "⚙"));
        if (_auth.IsAdmin) items.Add(("users", "用户管理", "🔑"));

        var y = 14;
        foreach (var (key, text, glyph) in items)
        {
            var b = new Button
            {
                Text = $"  {glyph}   {text}",
                Tag = key,
                Left = 10,
                Top = y,
                Width = 170,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.Font(9.5f),
                Cursor = Cursors.Hand,
                BackColor = Color.White,
                ForeColor = Theme.TextMain,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Theme.PrimaryLight;
            b.Click += (_, _) => ShowPage(key);
            nav.Controls.Add(b);
            _navButtons[key] = b;
            y += 48;
        }

        var sep = new Panel { Left = 12, Top = y + 6, Width = 166, Height = 1, BackColor = Theme.Border };
        nav.Controls.Add(sep);

        var tip = new Label
        {
            Text = "提示：Ctrl+N 快速开单\n数据保存在本机 data 目录",
            ForeColor = Theme.TextSub,
            Font = Theme.Font(8f),
            Left = 14,
            Top = y + 20,
            Width = 162,
            Height = 36
        };
        nav.Controls.Add(tip);

        _userNavBtn = new Button
        {
            Text = "  🔓   切换账号",
            Left = 10,
            Top = y + 62,
            Width = 170,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Theme.Font(9f),
            Cursor = Cursors.Hand,
            BackColor = Color.White,
            ForeColor = Theme.TextSub,
            UseVisualStyleBackColor = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _userNavBtn.FlatAppearance.BorderSize = 0;
        _userNavBtn.FlatAppearance.MouseOverBackColor = Theme.PrimaryLight;
        _userNavBtn.Click += (_, _) => SwitchUser();
        nav.Controls.Add(_userNavBtn);

        nav.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, nav.Width - 1, 0, nav.Width - 1, nav.Height);
        };

        return nav;
    }

    public void ShowPage(string key)
    {
        if (_current == key && _pages.ContainsKey(key))
            return;

        if (!_pages.TryGetValue(key, out var page))
        {
            page = CreatePage(key);
            page.Dock = DockStyle.Fill;
            _pages[key] = page;
        }

        _content.SuspendLayout();
        _content.Controls.Clear();
        _content.Controls.Add(page);
        _content.ResumeLayout();

        foreach (var (k, b) in _navButtons)
        {
            var active = k == key;
            b.BackColor = active ? Theme.PrimaryLight : Color.White;
            b.ForeColor = active ? Theme.Primary : Theme.TextMain;
            b.Font = Theme.Font(9.5f, active ? FontStyle.Bold : FontStyle.Regular);
        }

        _current = key;
        _titleLabel.Text = key switch
        {
            "note" => "开单 / 送货单",
            "list" => "单据列表",
            "customer" => "客户资料",
            "product" => "商品资料",
            "stockin" => "入库单",
            "stock" => "库存管理",
            "audit" => "审核中心",
            "report" => "统计报表",
            "settings" => "系统设置",
            "users" => "用户管理",
            _ => "万方商贸管家"
        };
        _crumbLabel.Text = "单机版 · 本地数据存储";
        RefreshStatus();
    }

    /// <summary>打开指定送货单进行编辑（在真正的开单页面里）。</summary>
    public void OpenNoteForEdit(long noteId)
    {
        ShowPage("note");
        if (_pages.TryGetValue("note", out var page) && page is NoteEditorPage editor)
            editor.LoadNote(noteId);
    }

    /// <summary>把已有单据的内容复制成一张新单，并切到开单页面继续编辑。</summary>
    public void CopyNoteAsNew(long sourceNoteId)
    {
        ShowPage("note");
        if (_pages.TryGetValue("note", out var page) && page is NoteEditorPage editor)
            editor.LoadCopyOf(sourceNoteId);
    }

    private UserControl CreatePage(string key) => key switch
    {
        "note" => new NoteEditorPage(_repo, this),
        "list" => new NoteListPage(_repo, this),
        "customer" => new CustomerPage(_repo),
        "product" => new ProductPage(_repo),
        "stockin" => new StockInPage(_repo, this),
        "stock" => new StockPage(_repo, this),
        "audit" => new AuditPage(_repo, this),
        "report" => new ReportPage(_repo),
        "settings" => new SettingsPage(_repo),
        "users" => new UserPage(_repo, _auth),
        _ => new NoteEditorPage(_repo, this)
    };

    public void InvalidatePages(params string[] keys)
    {
        foreach (var k in keys)
        {
            if (_pages.TryGetValue(k, out var p) && p is IRefreshable r)
                r.Reload();
        }
        RefreshStatus();
    }

    public void RefreshStatus()
    {
        try
        {
            var today = DateTime.Today;
            var (total, count) = _repo.Summary(new DateTime(today.Year, today.Month, 1), today);
            _statusLeft.Text = $"今日 {today:yyyy-MM-dd}    本月单据 {count} 张 · 金额 {total:N2} 元";
            var pending = _auth.IsAdmin
                ? _repo.QueryNotesByApproval(Core.ApproveStatus.待审核).Count
                : 0;
            var pendingText = pending > 0 ? $" · ⚠ {pending} 张单据待审核" : "";
            _statusRight.Text = $"客户 {_repo.Count("Customer")} 家 · 商品 {_repo.Count("Product")} 种" +
                                $"{pendingText} · 数据库 {AppPaths.DatabasePath}";
        }
        catch
        {
            _statusLeft.Text = "就绪";
        }
    }

    public void SetStatus(string text) => _statusLeft.Text = text;

    /// <summary>当前操作人，用于库存流水与审核记录留痕。</summary>
    public string CurrentUserName
    {
        get
        {
            var u = _auth.Current;
            if (u is null) return "系统";
            return string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName;
        }
    }

    private void BackupData()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.BackupDirectory);
            var name = $"fangke-{DateTime.Now:yyyyMMdd-HHmmss}.db";
            var target = Path.Combine(AppPaths.BackupDirectory, name);

            using (var conn = _repo is null ? null : new Db(AppPaths.DatabasePath).Open())
            using (var cmd = conn!.CreateCommand())
            {
                cmd.CommandText = "VACUUM INTO $p";
                cmd.Parameters.AddWithValue("$p", target);
                cmd.ExecuteNonQuery();
            }

            Msg.Show(this, $"备份成功！\n\n{target}", "备份数据",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetStatus($"已备份到 {target}");
        }
        catch (Exception ex)
        {
            Msg.Show(this, "备份失败：" + ex.Message, "备份数据",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreData()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "选择备份文件",
            Filter = "数据库备份 (*.db)|*.db|所有文件 (*.*)|*.*",
            InitialDirectory = Directory.Exists(AppPaths.BackupDirectory) ? AppPaths.BackupDirectory : null
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (Msg.Show(this, "恢复会覆盖当前所有数据，确定继续吗？\n\n建议先做一次备份。", "恢复数据",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            File.Copy(dlg.FileName, AppPaths.DatabasePath, true);
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var f = AppPaths.DatabasePath + suffix;
                if (File.Exists(f)) File.Delete(f);
            }
            Msg.Show(this, "恢复成功，请重新打开软件以加载数据。", "恢复数据",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            InvalidatePages("note", "list", "customer", "product", "report", "stock", "stockin", "audit");
        }
        catch (Exception ex)
        {
            Msg.Show(this, "恢复失败：" + ex.Message, "恢复数据",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowHelp()
    {
        var text = """
            使用说明

            1. 开单 / 送货单
               选择客户（或直接填写客户名称），逐行录入商品、数量与单价，
               合计金额与中文大写会自动计算。点击"保存并打印"可直接打印三联单。

            2. 客户资料 / 商品资料
               维护基础档案。开单时可从下拉框选择，自动带出电话、地址与单价。

            3. 单据列表
               按日期、单号、客户或状态筛选，可修改状态（未送货/已送货/已结算）、
               补打、复制为新单、删除。

            4. 统计报表
               按时间段查看客户销售汇总、商品销售汇总与月度趋势。

            5. 数据安全
               所有数据保存在程序目录的 data\fangke.db 中。
               请通过"文件 → 备份数据"定期备份。
            """;
        Msg.Show(this, text, "使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowAbout()
    {
        Msg.Show(this,
            "万方商贸管家（单机版）\n版本 1.3.0\n\n" +
            "本地数据存储 · 无需联网 · 支持三联送货单打印\n\n" +
            "数据目录：" + AppPaths.DataDirectory,
            "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.PrimaryLight;
        public override Color MenuItemSelectedGradientBegin => Theme.PrimaryLight;
        public override Color MenuItemSelectedGradientEnd => Theme.PrimaryLight;
        public override Color MenuItemBorder => Theme.PrimaryLight;
        public override Color MenuStripGradientBegin => Color.White;
        public override Color MenuStripGradientEnd => Color.White;
        public override Color ToolStripDropDownBackground => Color.White;
        public override Color ImageMarginGradientBegin => Color.White;
        public override Color ImageMarginGradientMiddle => Color.White;
        public override Color ImageMarginGradientEnd => Color.White;
    }
}

public interface IRefreshable
{
    void Reload();
}
