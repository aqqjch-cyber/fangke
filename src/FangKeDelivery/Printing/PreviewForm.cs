using FangKeDelivery.Core;
using FangKeDelivery.Data;
using FangKeDelivery.UI;

namespace FangKeDelivery.Printing;

/// <summary>打印预览：三联一次性列出，支持翻页、缩放、直接打印。与打印共用同一套绘制逻辑。</summary>
public sealed class PreviewForm : Form
{
    private readonly DeliveryNote _note;
    private readonly Repository _repo;
    private readonly List<Bitmap> _pages;
    private readonly List<Label> _pageLabels = new();
    private readonly Panel _viewport;
    private readonly Panel _canvas;
    private readonly PictureBox _picture;
    private readonly Label _pageInfo;

    private int _pageIndex;
    private float _zoom = 1f;

    private static readonly float[] ZoomSteps = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };

    public PreviewForm(DeliveryNote note, Repository repo)
    {
        _note = note;
        _repo = repo;

        Text = $"打印预览 — 送货单 {note.NoteNo}";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1120, 860);
        MinimumSize = new Size(760, 560);
        BackColor = Theme.PageBack;
        Font = Theme.BodyFont;
        KeyPreview = true;

        using (var doc = new NotePrintDocument(note, repo))
            _pages = doc.RenderAllPages(2f);

        var toolbar = BuildToolbar();
        _pageInfo = new Label
        {
            AutoSize = false,
            Width = 130,
            Height = 34,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.TextSub,
            Font = Theme.SmallFont
        };

        _picture = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White
        };

        _canvas = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PageBack, AutoScroll = true };
        _canvas.Controls.Add(_picture);

        _viewport = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(228, 232, 238), Padding = new Padding(24) };
        _viewport.Controls.Add(_canvas);

        var thumbBar = BuildThumbBar();

        Controls.Add(_viewport);
        Controls.Add(thumbBar);
        Controls.Add(toolbar);

        KeyDown += OnKeyDown;
        Resize += (_, _) => LayoutPage();
        Shown += (_, _) => { ShowPage(0); LayoutPage(); };

        if (_pages.Count > 0) ShowPage(0);
    }

    private Control BuildToolbar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White, Padding = new Padding(12, 8, 12, 8) };

        var print = Theme.PrimaryButton("直接打印", 100, 34);
        print.Click += (_, _) => DoPrint();

        var zoomOut = Theme.SecondaryButton("缩小 −", 80, 34);
        zoomOut.Click += (_, _) => StepZoom(-1);

        var zoomIn = Theme.SecondaryButton("放大 +", 80, 34);
        zoomIn.Click += (_, _) => StepZoom(1);

        var fit = Theme.SecondaryButton("适应窗口", 88, 34);
        fit.Click += (_, _) => { _zoom = FitZoom(); LayoutPage(); };

        var actual = Theme.SecondaryButton("实际大小", 88, 34);
        actual.Click += (_, _) => { _zoom = 1f; LayoutPage(); };

        var close = Theme.SecondaryButton("关闭", 76, 34);
        close.Click += (_, _) => Close();

        var prev = Theme.SecondaryButton("上一页", 76, 34);
        prev.Click += (_, _) => ShowPage(_pageIndex - 1);

        var next = Theme.SecondaryButton("下一页", 76, 34);
        next.Click += (_, _) => ShowPage(_pageIndex + 1);

        // 右对齐：关闭 / 打印；左起：缩放与翻页
        var rightFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        rightFlow.Controls.AddRange(new Control[] { close, print });

        var leftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        foreach (var b in new[] { prev, next, zoomOut, zoomIn, fit, actual, close, print })
            b.Margin = new Padding(0, 0, 6, 0);

        leftFlow.Controls.AddRange(new Control[] { prev, next, _pageInfo, zoomOut, zoomIn, fit, actual });

        var hint = new Label
        {
            Text = "三联：存根联 / 客户联 / 回单联",
            ForeColor = Theme.TextSub,
            Font = Theme.SmallFont,
            AutoSize = true,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 10, 14, 0)
        };

        bar.Controls.Add(leftFlow);
        bar.Controls.Add(hint);
        bar.Controls.Add(rightFlow);
        bar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
        };
        return bar;
    }

    /// <summary>底部缩略图，点一下跳转——三联一眼看清。</summary>
    private Control BuildThumbBar()
    {
        var strip = new Panel { Dock = DockStyle.Bottom, Height = 104, BackColor = Color.White, Padding = new Padding(12, 8, 12, 8) };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.White
        };

        for (var i = 0; i < _pages.Count; i++)
        {
            var idx = i;
            var box = new PictureBox
            {
                Size = new Size(120, 68),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = _pages[i],
                Cursor = Cursors.Hand,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 10, 0)
            };
            box.Click += (_, _) => ShowPage(idx);

            var cap = new Label
            {
                Text = $"第 {i + 1} 页",
                AutoSize = false,
                Width = 120,
                Height = 16,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.SmallFont,
                ForeColor = Theme.TextSub
            };

            var cell = new Panel { Size = new Size(122, 88), BackColor = Color.White, Margin = new Padding(0, 0, 8, 0) };
            cell.Controls.Add(box);
            cell.Controls.Add(cap);
            box.Top = 0;
            cap.Top = 69;
            flow.Controls.Add(cell);
            _pageLabels.Add(cap);
        }

        strip.Controls.Add(flow);
        strip.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, strip.Width, 0);
        };
        return strip;
    }

    private void ShowPage(int index)
    {
        if (_pages.Count == 0) return;
        _pageIndex = Math.Clamp(index, 0, _pages.Count - 1);
        _picture.Image = _pages[_pageIndex];
        _pageInfo.Text = $"第 {_pageIndex + 1} / {_pages.Count} 页";

        for (var i = 0; i < _pageLabels.Count; i++)
            _pageLabels[i].Font = i == _pageIndex ? Theme.Font(8.5f, FontStyle.Bold) : Theme.SmallFont;

        LayoutPage();
    }

    private float FitZoom()
    {
        if (_pages.Count == 0) return 1f;
        var img = _pages[_pageIndex];
        var availW = Math.Max(80, _viewport.ClientSize.Width - 60);
        var availH = Math.Max(80, _viewport.ClientSize.Height - 60);
        return Math.Min(availW / (float)img.Width, availH / (float)img.Height) * 2f;
    }

    private void StepZoom(int dir)
    {
        var current = Array.FindIndex(ZoomSteps, z => Math.Abs(z - _zoom) < 0.01f);
        if (current < 0) current = Array.FindIndex(ZoomSteps, z => z > _zoom);
        if (current < 0) current = ZoomSteps.Length - 1;

        var next = Math.Clamp(current + dir, 0, ZoomSteps.Length - 1);
        _zoom = ZoomSteps[next];
        LayoutPage();
    }

    private void LayoutPage()
    {
        if (_pages.Count == 0) return;
        var img = _pages[_pageIndex];

        // RenderAllPages 用了 2 倍采样，显示时按 0.5 折算回 100%
        var baseW = img.Width / 2f;
        var baseH = img.Height / 2f;

        _picture.Size = new Size((int)(baseW * _zoom), (int)(baseH * _zoom));
        _picture.Location = new Point(24, 24);
        _canvas.AutoScrollMinSize = new Size(_picture.Width + 48, _picture.Height + 48);

        _canvas.Invalidate();
    }

    private void StepPage(int dir) => ShowPage(_pageIndex + dir);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.PageUp: StepPage(-1); e.Handled = true; break;
            case Keys.Right or Keys.PageDown: StepPage(1); e.Handled = true; break;
            case Keys.Escape: Close(); break;
            case Keys.Add or Keys.Oemplus: StepZoom(1); e.Handled = true; break;
            case Keys.Subtract or Keys.OemMinus: StepZoom(-1); e.Handled = true; break;
        }
    }

    private void DoPrint()
    {
        using var doc = new NotePrintDocument(_note, _repo);
        using var dlg = new PrintDialog { Document = doc, UseEXDialog = true, AllowSomePages = false };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            doc.Print();
            Msg.Show(this, "已提交打印。", "打印", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Msg.Show(this, "打印失败：" + ex.Message, "打印", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _picture.Image = null;
            foreach (var b in _pages) b.Dispose();
        }
        base.Dispose(disposing);
    }
}
