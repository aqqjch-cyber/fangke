using FangKeDelivery.Core;
using FangKeDelivery.UI;
using FangKeDelivery.Data;

namespace FangKeDelivery.Printing;

public static class NotePrinter
{
    public static void ShowPrintDialog(DeliveryNote note, Repository repo, IWin32Window? owner)
    {
        using var doc = new NotePrintDocument(note, repo);
        using var dlg = new PrintDialog
        {
            Document = doc,
            UseEXDialog = true,
            AllowSomePages = false
        };
        if (dlg.ShowDialog(owner) == DialogResult.OK)
        {
            try
            {
                doc.Print();
            }
            catch (Exception ex)
            {
                Msg.Show(owner, "打印失败：" + ex.Message, "打印", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public static void ShowPreview(DeliveryNote note, Repository repo, IWin32Window? owner)
    {
        using var doc = new NotePrintDocument(note, repo) { PreviewOnly = true };
        using var dlg = new PrintPreviewDialog
        {
            Document = doc,
            Width = 1000,
            Height = 740,
            UseAntiAlias = true,
            Text = $"送货单打印预览 — {note.NoteNo}",
            StartPosition = FormStartPosition.CenterParent
        };
        dlg.ShowDialog(owner);
    }
}
