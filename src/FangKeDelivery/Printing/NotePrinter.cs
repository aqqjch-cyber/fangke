using FangKeDelivery.Core;
using FangKeDelivery.UI;
using FangKeDelivery.Data;

namespace FangKeDelivery.Printing;

/// <summary>打印入口：统一处理联次模式与打印份数。</summary>
public static class NotePrinter
{
    /// <summary>默认打印份数。三联复写纸一次出三联，份数指的是整份单子打几遍。</summary>
    public const int DefaultCopies = 1;

    /// <summary>
    /// 是否复写纸（默认是）。老版本把三联画成三页，导致一份单子打三遍；
    /// 默认走复写纸即一遍纸，升级后无需任何设置就能恢复正常。
    /// </summary>
    public static bool ReadCarbonCopy(Repository repo) =>
        repo.GetSetting("PrintCarbonCopy", "1") != "0";

    public static int ReadCopies(Repository repo) =>
        int.TryParse(repo.GetSetting("PrintCopies", DefaultCopies.ToString()), out var n)
            ? Math.Clamp(n, 1, 9)
            : DefaultCopies;

    public static void WriteCopies(Repository repo, int copies) =>
        repo.SetSetting("PrintCopies", Math.Clamp(copies, 1, 9).ToString());

    public static void WriteCarbonCopy(Repository repo, bool carbonCopy) =>
        repo.SetSetting("PrintCarbonCopy", carbonCopy ? "1" : "0");

    /// <summary>
    /// 打印指定份数。PrintDocument 自身没有份数概念，必须在外面循环：
    /// 一份单子走一遍纸（复写纸）或三遍纸（分三联），再重复 copies 次。
    /// </summary>
    /// <param name="printerName">
    /// PrintDialog 里选定的打印机。必须显式传入，否则每份都会落到系统默认打印机上。
    /// </param>
    public static void PrintCopies(DeliveryNote note, Repository repo, int copies,
        IWin32Window? owner, string? printerName = null)
    {
        var carbonCopy = ReadCarbonCopy(repo);
        copies = Math.Clamp(copies, 1, 9);
        for (var i = 0; i < copies; i++)
        {
            using var doc = new NotePrintDocument(note, repo) { CarbonCopy = carbonCopy };
            if (!string.IsNullOrWhiteSpace(printerName))
                doc.PrinterSettings.PrinterName = printerName;
            doc.Print();
        }
    }

    public static void ShowPrintDialog(DeliveryNote note, Repository repo, IWin32Window? owner)
    {
        var carbonCopy = ReadCarbonCopy(repo);
        using var doc = new NotePrintDocument(note, repo) { CarbonCopy = carbonCopy };
        using var dlg = new PrintDialog
        {
            Document = doc,
            UseEXDialog = true,
            AllowSomePages = false
        };
        if (dlg.ShowDialog(owner) != DialogResult.OK) return;
        var printerName = dlg.PrinterSettings.PrinterName;

        var copies = AskCopies(owner, repo, carbonCopy);
        if (copies is null) return;

        try
        {
            PrintCopies(note, repo, copies.Value, owner, printerName);
            if (copies.Value > 1)
                Msg.Show(owner, $"已提交打印，共 {copies.Value} 份。", "打印",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Msg.Show(owner, "打印失败：" + ex.Message, "打印", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static void ShowPreview(DeliveryNote note, Repository repo, IWin32Window? owner)
    {
        using var dlg = new PreviewForm(note, repo);
        dlg.ShowDialog(owner);
    }

    private static int? AskCopies(IWin32Window? owner, Repository repo, bool carbonCopy)
    {
        using var dlg = new PrintCopiesDialog(ReadCopies(repo), carbonCopy);
        if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
        WriteCopies(repo, dlg.Copies);
        WriteCarbonCopy(repo, dlg.CarbonCopy);
        return dlg.Copies;
    }
}
