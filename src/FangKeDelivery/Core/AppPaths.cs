namespace FangKeDelivery.Core;

/// <summary>数据目录解析：优先使用程序目录（绿色便携），不可写时回退到用户目录。</summary>
public static class AppPaths
{
    private const string AppFolderName = "万方商贸管家";

    /// <summary>改名前的目录名。用于把老用户的数据自动接过来。</summary>
    private const string LegacyFolderNames = "方可送货单管理系统";

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
            AdoptLegacyDatabase(portable);
            return portable;
        }

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
        Directory.CreateDirectory(local);
        AdoptLegacyDatabase(local);
        return local;
    }

    /// <summary>
    /// 软件改名后，老用户的数据还在旧目录里。首次启动时若当前目录没有数据库，
    /// 就把旧目录的数据库复制过来，避免用户以为数据丢了。
    /// 只复制不移动，旧目录保持原样，出问题还能回退。
    /// </summary>
    private static void AdoptLegacyDatabase(string targetDir)
    {
        try
        {
            var target = Path.Combine(targetDir, "fangke.db");
            if (File.Exists(target)) return;

            foreach (var legacy in LegacyDataDirectories())
            {
                var source = Path.Combine(legacy, "fangke.db");
                if (!File.Exists(source)) continue;

                File.Copy(source, target, false);
                // WAL 日志一并带走，否则最近写入的数据会丢
                foreach (var suffix in new[] { "-wal", "-shm" })
                {
                    var extra = source + suffix;
                    if (File.Exists(extra))
                        File.Copy(extra, target + suffix, true);
                }

                var legacyBackup = Path.Combine(legacy, "backup");
                var targetBackup = Path.Combine(targetDir, "backup");
                if (Directory.Exists(legacyBackup) && !Directory.Exists(targetBackup))
                    Directory.CreateDirectory(targetBackup);

                return;
            }
        }
        catch
        {
            // 迁移失败不应阻止启动，用户仍可手工复制 data 目录
        }
    }

    /// <summary>旧版本可能存放数据的位置：程序目录旁、用户目录下。</summary>
    private static IEnumerable<string> LegacyDataDirectories()
    {
        var exeDir = AppContext.BaseDirectory;
        yield return Path.Combine(exeDir, "data");
        yield return Path.Combine(Path.GetDirectoryName(exeDir.TrimEnd(Path.DirectorySeparatorChar)) ?? exeDir, "data");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(localAppData, LegacyFolderNames);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Path.Combine(appData, LegacyFolderNames);
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
