using System.Text;

namespace FangKeDelivery.UI;

/// <summary>开发期自检：把控件树的几何与文本导出，用于无人值守的布局验证。</summary>
public static class LayoutDump
{
    public static void Write(Control root, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Form {root.GetType().Name} ClientSize={root.ClientSize.Width}x{root.ClientSize.Height}");
        Walk(root, sb, 0);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void Walk(Control c, StringBuilder sb, int depth)
    {
        if (depth > 8) return;
        var indent = new string(' ', depth * 2);
        var text = c.Text.Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 40) text = text[..40];
        var extra = c switch
        {
            DataGridView g => $" cols={g.Columns.Count} rows={g.Rows.Count} visibleCols={g.Columns.Cast<DataGridViewColumn>().Count(x => x.Visible)}",
            TabControl t => $" tabs={t.TabPages.Count}",
            TableLayoutPanel t => $" rows={t.RowCount} rowStyles=[{string.Join(",", t.RowStyles.Cast<RowStyle>().Select(r => $"{r.SizeType}:{r.Height}"))}]" +
                                  $" rowHeights=[{string.Join(",", t.GetRowHeights())}]" +
                                  $" cols={t.ColumnCount} colStyles={t.ColumnStyles.Count} colWidths=[{string.Join(",", t.GetColumnWidths())}]",
            _ => ""
        };
        sb.AppendLine($"{indent}{c.GetType().Name} \"{text}\" loc={c.Left},{c.Top} size={c.Width}x{c.Height} " +
                      $"dock={c.Dock} anchor={c.Anchor} vis={c.Visible}{extra}");

        foreach (Control child in c.Controls)
            Walk(child, sb, depth + 1);
    }

    /// <summary>检查常见布局缺陷：零尺寸、超出父容器、文本被裁剪。</summary>
    public static List<string> Validate(Control root)
    {
        var problems = new List<string>();
        Check(root, root, problems, 0);
        return problems;
    }

    private static void Check(Control c, Control root, List<string> problems, int depth)
    {
        if (depth > 8) return;
        if (!c.Visible) return;

        var isContainer = c is Form || c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel ||
                          c is TabControl || c is TabPage || c is UserControl || c is GroupBox;

        if (!isContainer && (c.Width <= 0 || c.Height <= 0))
        {
            problems.Add($"ZERO_SIZE {c.GetType().Name} \"{Trim(c.Text)}\" {c.Width}x{c.Height}");
        }

        // AutoScroll 容器内的内容本来就允许超出可视区，靠滚动条访问，不算缺陷
        var scrollable = c.Parent is ScrollableControl { AutoScroll: true };

        if (c.Parent is not null && c.Parent is not Form && !scrollable)
        {
            var p = c.Parent.ClientRectangle;
            if (c.Left < -2 || c.Top < -2 || c.Right > p.Right + 2 || c.Bottom > p.Bottom + 2)
            {
                problems.Add($"OUT_OF_BOUNDS {c.GetType().Name} \"{Trim(c.Text)}\" " +
                             $"rect=({c.Left},{c.Top},{c.Right},{c.Bottom}) parent={p.Width}x{p.Height}");
            }
        }

        if (c is Label lbl && lbl.AutoSize == false && lbl.Height < 8)
            problems.Add($"LABEL_SQUASHED \"{Trim(lbl.Text)}\" h={lbl.Height}");

        // 同级控件重叠：绝对定位的窗口里最容易出错的地方，靠肉眼很难发现
        if (c.Parent is not null)
            CheckSiblingOverlap(c, problems);

        foreach (Control child in c.Controls)
            Check(child, root, problems, depth + 1);
    }

    private static void CheckSiblingOverlap(Control c, List<string> problems)
    {
        if (c.Parent is not { } parent) return;
        if (c.Dock != DockStyle.None || c.Anchor != (AnchorStyles.Top | AnchorStyles.Left)) return;
        if (c is Label { AutoSize: true } or Panel || c.Width <= 0 || c.Height <= 0) return;

        foreach (Control other in parent.Controls)
        {
            if (ReferenceEquals(other, c) || !other.Visible) continue;
            if (other.Dock != DockStyle.None || other.Width <= 0 || other.Height <= 0) continue;

            var a = c.Bounds;
            var b = other.Bounds;
            var inter = Rectangle.Intersect(a, b);
            if (inter.Width > 0 && inter.Height > 0)
            {
                problems.Add($"OVERLAP {c.GetType().Name} \"{Trim(c.Text)}\" rect=({a.Left},{a.Top},{a.Right},{a.Bottom}) " +
                             $"与 {other.GetType().Name} \"{Trim(other.Text)}\" rect=({b.Left},{b.Top},{b.Right},{b.Bottom}) " +
                             $"重叠 {inter.Width}x{inter.Height}");
            }
        }
    }

    private static string Trim(string? s)
    {
        s ??= "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length > 30 ? s[..30] : s;
    }
}
