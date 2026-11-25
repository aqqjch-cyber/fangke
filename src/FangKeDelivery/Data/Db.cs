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

            CREATE TABLE IF NOT EXISTS StockIn (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                InNo          TEXT NOT NULL,
                Supplier      TEXT NOT NULL DEFAULT '',
                InDate        TEXT NOT NULL,
                TotalAmount   REAL NOT NULL DEFAULT 0,
                Remark        TEXT NOT NULL DEFAULT '',
                Maker         TEXT NOT NULL DEFAULT '',
                ApproveStatus INTEGER NOT NULL DEFAULT 0,
                Approver      TEXT NOT NULL DEFAULT '',
                ApproveAt     TEXT NOT NULL DEFAULT '',
                ApproveRemark TEXT NOT NULL DEFAULT '',
                CreatedAt     TEXT NOT NULL DEFAULT '',
                UpdatedAt     TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS StockInItem (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                StockInId   INTEGER NOT NULL REFERENCES StockIn(Id) ON DELETE CASCADE,
                Seq         INTEGER NOT NULL DEFAULT 0,
                ProductId   INTEGER NULL,
                ProductName TEXT NOT NULL DEFAULT '',
                Spec        TEXT NOT NULL DEFAULT '',
                Unit        TEXT NOT NULL DEFAULT '',
                Qty         REAL NOT NULL DEFAULT 0,
                Price       REAL NOT NULL DEFAULT 0,
                Remark      TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Stock (
                ProductId   INTEGER PRIMARY KEY,
                ProductName TEXT NOT NULL DEFAULT '',
                Spec        TEXT NOT NULL DEFAULT '',
                Unit        TEXT NOT NULL DEFAULT '',
                Qty         REAL NOT NULL DEFAULT 0,
                SafetyQty   REAL NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS StockMove (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId   INTEGER NULL,
                ProductName TEXT NOT NULL DEFAULT '',
                Spec        TEXT NOT NULL DEFAULT '',
                Unit        TEXT NOT NULL DEFAULT '',
                Qty         REAL NOT NULL DEFAULT 0,
                QtyBefore   REAL NOT NULL DEFAULT 0,
                QtyAfter    REAL NOT NULL DEFAULT 0,
                Type        INTEGER NOT NULL DEFAULT 0,
                SourceNo    TEXT NOT NULL DEFAULT '',
                SourceId    INTEGER NULL,
                Operator    TEXT NOT NULL DEFAULT '',
                Remark      TEXT NOT NULL DEFAULT '',
                CreatedAt   TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS AuditLog (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                DocType   TEXT NOT NULL DEFAULT '',
                DocId     INTEGER NOT NULL DEFAULT 0,
                DocNo     TEXT NOT NULL DEFAULT '',
                Action    TEXT NOT NULL DEFAULT '',
                Operator  TEXT NOT NULL DEFAULT '',
                Remark    TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT ''
            );

            CREATE INDEX IF NOT EXISTS IX_DeliveryNote_Date ON DeliveryNote(NoteDate);
            CREATE INDEX IF NOT EXISTS IX_DeliveryNote_No ON DeliveryNote(NoteNo);
            CREATE INDEX IF NOT EXISTS IX_NoteItem_NoteId ON NoteItem(NoteId);
            CREATE INDEX IF NOT EXISTS IX_Customer_Name ON Customer(Name);
            CREATE INDEX IF NOT EXISTS IX_Product_Name ON Product(Name);
            CREATE INDEX IF NOT EXISTS IX_StockIn_Date ON StockIn(InDate);
            CREATE INDEX IF NOT EXISTS IX_StockIn_No ON StockIn(InNo);
            CREATE INDEX IF NOT EXISTS IX_StockInItem_InId ON StockInItem(StockInId);
            CREATE INDEX IF NOT EXISTS IX_StockMove_Product ON StockMove(ProductId);
            CREATE INDEX IF NOT EXISTS IX_StockMove_Created ON StockMove(CreatedAt);
            CREATE INDEX IF NOT EXISTS IX_AuditLog_Doc ON AuditLog(DocType, DocId);
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
            ("DeliveryNote", "SettleMethod", "TEXT NOT NULL DEFAULT ''"),
            ("DeliveryNote", "ApproveStatus", "INTEGER NOT NULL DEFAULT 0"),
            ("DeliveryNote", "Approver", "TEXT NOT NULL DEFAULT ''"),
            ("DeliveryNote", "ApproveAt", "TEXT NOT NULL DEFAULT ''"),
            ("DeliveryNote", "ApproveRemark", "TEXT NOT NULL DEFAULT ''")
        };

        foreach (var (table, column, ddl) in wanted)
        {
            if (HasColumn(conn, table, column)) continue;
            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {ddl};";
            alter.ExecuteNonQuery();
        }

        BackfillApprovedNotes(conn);
    }

    /// <summary>
    /// 升级到带审核的版本时，历史单据没有审核人。把它们标记为已审核，
    /// 否则老数据会全部卡在「待审核」，库存也建不起来。
    /// 只在首次升级时执行一次（用 Settings 标记）。
    /// </summary>
    private static void BackfillApprovedNotes(SqliteConnection conn)
    {
        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT Value FROM Settings WHERE Key='ApproveBackfilled'";
            if (check.ExecuteScalar() as string == "1") return;
        }

        using (var fill = conn.CreateCommand())
        {
            fill.CommandText = """
                UPDATE DeliveryNote
                SET ApproveStatus = 1,
                    Approver = '（升级前历史单据）',
                    ApproveAt = COALESCE(NULLIF(UpdatedAt,''), CreatedAt)
                WHERE ApproveStatus = 0
                """;
            fill.ExecuteNonQuery();
        }

        using var mark = conn.CreateCommand();
        mark.CommandText =
            "INSERT INTO Settings(Key,Value) VALUES('ApproveBackfilled','1') " +
            "ON CONFLICT(Key) DO UPDATE SET Value='1'";
        mark.ExecuteNonQuery();

        BackfillStockFromHistory(conn);
    }

    /// <summary>
    /// 历史单据被当作已审核，但升级前没有库存记录。若不补账，
    /// 库存会是 0 而实际已出货，之后每次出库都会变成负库存。
    /// 这里按「已审核送货单累计出库」建一条期初盘点流水，把账做平。
    /// 只跑一次，之后由正常审核流程维护。
    /// </summary>
    private static void BackfillStockFromHistory(SqliteConnection conn)
    {
        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT Value FROM Settings WHERE Key='StockBackfilled'";
            if (check.ExecuteScalar() as string == "1") return;
        }

        // 按商品汇总历史已审核送货单的数量，写入 Stock 并留期初流水
        using (var agg = conn.CreateCommand())
        {
            agg.CommandText = """
                SELECT i.ProductName, MAX(i.Spec), MAX(i.Unit), SUM(i.Qty),
                       COALESCE(MAX(i.ProductId), 0)
                FROM NoteItem i
                JOIN DeliveryNote n ON n.Id = i.NoteId
                WHERE n.ApproveStatus = 1
                GROUP BY i.ProductName
                """;
            var rows = new List<(string Name, string Spec, string Unit, double Qty, long ProductId)>();
            using (var r = agg.ExecuteReader())
            {
                while (r.Read())
                {
                    rows.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetDouble(3), r.GetInt64(4)));
                }
            }

            foreach (var (name, spec, unit, qty, productId) in rows)
            {
                var pid = productId;
                if (pid == 0)
                {
                    using var find = conn.CreateCommand();
                    find.CommandText = "SELECT Id FROM Product WHERE Name=$n COLLATE NOCASE LIMIT 1";
                    find.Parameters.AddWithValue("$n", name);
                    var found = find.ExecuteScalar();
                    if (found is null) continue;
                    pid = Convert.ToInt64(found);
                }

                // 期初为 0，把历史出库量补成负数的对侧：以「期初入库」记正数，
                // 使现存 = 历史入库 - 历史出库 ≥ 0 更符合直觉的账实关系
                using var ins = conn.CreateCommand();
                ins.CommandText = """
                    INSERT INTO Stock(ProductId,ProductName,Spec,Unit,Qty,SafetyQty)
                    VALUES($p,$n,$s,$u,$q,0)
                    ON CONFLICT(ProductId) DO UPDATE SET Qty = Qty + $q;
                    INSERT INTO StockMove(ProductId,ProductName,Spec,Unit,Qty,QtyBefore,QtyAfter,
                                          Type,SourceNo,SourceId,Operator,Remark,CreatedAt)
                    VALUES($p,$n,$s,$u,$q,0,$q,0,'期初','' ,'系统升级','历史单据期初建账',$now);
                    """;
                ins.Parameters.AddWithValue("$p", pid);
                ins.Parameters.AddWithValue("$n", name);
                ins.Parameters.AddWithValue("$s", spec);
                ins.Parameters.AddWithValue("$u", unit);
                ins.Parameters.AddWithValue("$q", qty);
                ins.Parameters.AddWithValue("$now", Now());
                ins.ExecuteNonQuery();
            }
        }

        using var mark = conn.CreateCommand();
        mark.CommandText =
            "INSERT INTO Settings(Key,Value) VALUES('StockBackfilled','1') " +
            "ON CONFLICT(Key) DO UPDATE SET Value='1'";
        mark.ExecuteNonQuery();
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
