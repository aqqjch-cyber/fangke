using System.Globalization;
using Microsoft.Data.Sqlite;

namespace FangKeDelivery.Data;

/// <summary>负责 SQLite 数据库文件的创建、连接与结构升级。</summary>
public sealed class Db
{
    private readonly string _path;

    public Db(string dbPath)
    {
        _path = dbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public string DatabasePath => _path;

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    /// <summary>
    /// 把 WAL 内容合并回主库文件并截断日志，
    /// 这样用户只复制 fangke.db 也不会丢数据（首次运行时生成的三件套可简化为一）。
    /// </summary>
    public void Checkpoint()
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // 退出清理失败不影响已写入的数据
        }
    }

    public void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Settings (
                Key   TEXT PRIMARY KEY,
                Value TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS AppUser (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                UserName    TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL DEFAULT '',
                Role        INTEGER NOT NULL DEFAULT 0,
                PasswordHash TEXT NOT NULL,
                Salt        TEXT NOT NULL,
                IsEnabled   INTEGER NOT NULL DEFAULT 1,
                CreatedAt   TEXT NOT NULL DEFAULT '',
                LastLoginAt TEXT NOT NULL DEFAULT ''
            );

            CREATE UNIQUE INDEX IF NOT EXISTS UX_AppUser_UserName
                ON AppUser(UserName COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS Customer (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT NOT NULL,
                Contact   TEXT NOT NULL DEFAULT '',
                Phone     TEXT NOT NULL DEFAULT '',
                Address   TEXT NOT NULL DEFAULT '',
                Remark    TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Product (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT NOT NULL,
                Spec      TEXT NOT NULL DEFAULT '',
                Unit      TEXT NOT NULL DEFAULT '',
                Price     REAL NOT NULL DEFAULT 0,
                Remark    TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS DeliveryNote (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                NoteNo          TEXT NOT NULL,
                CustomerId      INTEGER NULL,
                CustomerName    TEXT NOT NULL DEFAULT '',
                CustomerPhone   TEXT NOT NULL DEFAULT '',
                CustomerAddress TEXT NOT NULL DEFAULT '',
                NoteDate        TEXT NOT NULL,
                TotalAmount     REAL NOT NULL DEFAULT 0,
                Remark          TEXT NOT NULL DEFAULT '',
                Salesman        TEXT NOT NULL DEFAULT '',
                Receiver        TEXT NOT NULL DEFAULT '',
                Maker           TEXT NOT NULL DEFAULT '',
                Status          INTEGER NOT NULL DEFAULT 0,
                CreatedAt       TEXT NOT NULL DEFAULT '',
                UpdatedAt       TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS NoteItem (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                NoteId      INTEGER NOT NULL REFERENCES DeliveryNote(Id) ON DELETE CASCADE,
                Seq         INTEGER NOT NULL DEFAULT 0,
                ProductId   INTEGER NULL,
                ProductName TEXT NOT NULL DEFAULT '',
                Spec        TEXT NOT NULL DEFAULT '',
                Unit        TEXT NOT NULL DEFAULT '',
                Qty         REAL NOT NULL DEFAULT 0,
                Price       REAL NOT NULL DEFAULT 0,
                Remark      TEXT NOT NULL DEFAULT ''
            );

            CREATE INDEX IF NOT EXISTS IX_DeliveryNote_Date ON DeliveryNote(NoteDate);
            CREATE INDEX IF NOT EXISTS IX_DeliveryNote_No ON DeliveryNote(NoteNo);
            CREATE INDEX IF NOT EXISTS IX_NoteItem_NoteId ON NoteItem(NoteId);
            CREATE INDEX IF NOT EXISTS IX_Customer_Name ON Customer(Name);
            CREATE INDEX IF NOT EXISTS IX_Product_Name ON Product(Name);
            """;
        cmd.ExecuteNonQuery();

        MigrateColumns(conn);
    }

    /// <summary>
    /// 老版本数据库缺少后加的列。SQLite 没有 ADD COLUMN IF NOT EXISTS，
    /// 只能先查 table_info 再补，保证升级不丢数据。
    /// </summary>
    private static void MigrateColumns(SqliteConnection conn)
    {
        var wanted = new (string Table, string Column, string Ddl)[]
        {
            ("DeliveryNote", "Contact", "TEXT NOT NULL DEFAULT ''"),
            ("DeliveryNote", "DeliveryMethod", "TEXT NOT NULL DEFAULT ''"),
            ("DeliveryNote", "Transporter", "TEXT NOT NULL DEFAULT ''"),
            ("DeliveryNote", "VehicleNo", "TEXT NOT NULL DEFAULT ''"),
            ("DeliveryNote", "SettleMethod", "TEXT NOT NULL DEFAULT ''")
        };

        foreach (var (table, column, ddl) in wanted)
        {
            if (HasColumn(conn, table, column)) continue;
            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {ddl};";
            alter.ExecuteNonQuery();
        }
    }

    private static bool HasColumn(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
