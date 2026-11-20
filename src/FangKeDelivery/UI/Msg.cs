namespace FangKeDelivery.UI;

/// <summary>
/// MessageBox 的统一入口。自动化测试下自动应答，避免模态框阻塞无人值守运行。
/// </summary>
public static class Msg
{
    /// <summary>为 true 时所有提示框立即返回预设结果，不弹窗。</summary>
    public static bool AutoAnswer { get; set; }

    public static DialogResult AutoResult { get; set; } = DialogResult.OK;

    public static DialogResult Show(string text, string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information)
    {
        if (AutoAnswer) return Resolve(buttons);
        return MessageBox.Show(text, caption, buttons, icon);
    }

    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information)
    {
        if (AutoAnswer) return Resolve(buttons);
        return MessageBox.Show(owner, text, caption, buttons, icon);
    }

    private static DialogResult Resolve(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.YesNo => AutoResult == DialogResult.No ? DialogResult.No : DialogResult.Yes,
        MessageBoxButtons.OKCancel => AutoResult == DialogResult.Cancel ? DialogResult.Cancel : DialogResult.OK,
        _ => DialogResult.OK
    };
}
