namespace FangKeDelivery.Core;

/// <summary>数据目录解析：优先使用程序目录（绿色便携），不可写时回退到用户目录。</summary>
public static class AppPaths
{
    private const string AppFolderName = "方可送货单管理系统";

    public static string DataDirectory { get; } = Resolve();

    public static string DatabasePath => Path.Combine(DataDirectory, "fangke.db");
    public static string BackupDirectory => Path.Combine(DataDirectory, "backup");
    public static string LogoPath => Path.Combine(DataDirectory, "logo.png");

    private static string Resolve()
    {
        var exeDir = AppContext.BaseDirectory;
        var portable = Path.Combine(exeDir, "data");
        if (IsWritable(exeDir))
        {
            Directory.CreateDirectory(portable);
            return portable;
        }

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
        Directory.CreateDirectory(local);
        return local;
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, $".w{Guid.NewGuid():N}");
            using (var fs = File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
