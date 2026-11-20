using System.Globalization;
using Microsoft.Data.Sqlite;
using FangKeDelivery.Core;

namespace FangKeDelivery.Data;

public sealed class Repository
{
    private readonly Db _db;
    public Repository(Db db) => _db = db;

    /// <summary>退出前把 WAL 合并回主库，确保单独复制 .db 文件不丢数据。</summary>
    public void Checkpoint() => _db.Checkpoint();

    // ---------- Settings ----------

    public string GetSetting(string key, string fallback = "")
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        var v = cmd.ExecuteScalar();
        return v as string ?? fallback;
    }

    public void SetSetting(string key, string value)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Settings(Key,Value) VALUES($k,$v) ON CONFLICT(Key) DO UPDATE SET Value=$v";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    // ---------- Customers ----------

    public List<Customer> GetCustomers(string? keyword = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            cmd.CommandText = "SELECT Id,Name,Contact,Phone,Address,Remark,CreatedAt FROM Customer ORDER BY Id DESC";
        }
        else
        {
            cmd.CommandText = """
                SELECT Id,Name,Contact,Phone,Address,Remark,CreatedAt FROM Customer
                WHERE Name LIKE $kw OR Contact LIKE $kw OR Phone LIKE $kw OR Address LIKE $kw
                ORDER BY Id DESC
                """;
            cmd.Parameters.AddWithValue("$kw", $"%{keyword.Trim()}%");
        }

        var list = new List<Customer>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Customer
            {
                Id = r.GetInt64(0),
                Name = r.GetString(1),
                Contact = r.GetString(2),
                Phone = r.GetString(3),
                Address = r.GetString(4),
                Remark = r.GetString(5),
                CreatedAt = r.GetString(6)
            });
        }
        return list;
    }

    public long SaveCustomer(Customer c)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        if (c.Id == 0)
        {
            cmd.CommandText = """
                INSERT INTO Customer(Name,Contact,Phone,Address,Remark,CreatedAt)
                VALUES($n,$c,$p,$a,$r,$t); SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$t", Db.Now());
        }
        else
        {
            cmd.CommandText = """
                UPDATE Customer SET Name=$n,Contact=$c,Phone=$p,Address=$a,Remark=$r WHERE Id=$id;
                SELECT $id;
                """;
            cmd.Parameters.AddWithValue("$id", c.Id);
        }
        cmd.Parameters.AddWithValue("$n", c.Name.Trim());
        cmd.Parameters.AddWithValue("$c", c.Contact.Trim());
        cmd.Parameters.AddWithValue("$p", c.Phone.Trim());
        cmd.Parameters.AddWithValue("$a", c.Address.Trim());
        cmd.Parameters.AddWithValue("$r", c.Remark.Trim());
        var id = Convert.ToInt64(cmd.ExecuteScalar());
        c.Id = id;
        return id;
    }

    public void DeleteCustomers(IEnumerable<long> ids)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Customer WHERE Id=$id";
        var p = cmd.CreateParameter();
        p.ParameterName = "$id";
        cmd.Parameters.Add(p);
        foreach (var id in ids)
        {
            p.Value = id;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // ---------- Products ----------

    public List<Product> GetProducts(string? keyword = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            cmd.CommandText = "SELECT Id,Name,Spec,Unit,Price,Remark,CreatedAt FROM Product ORDER BY Id DESC";
        }
        else
        {
            cmd.CommandText = """
                SELECT Id,Name,Spec,Unit,Price,Remark,CreatedAt FROM Product
                WHERE Name LIKE $kw OR Spec LIKE $kw OR Remark LIKE $kw
                ORDER BY Id DESC
                """;
            cmd.Parameters.AddWithValue("$kw", $"%{keyword.Trim()}%");
        }

        var list = new List<Product>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Product
            {
                Id = r.GetInt64(0),
                Name = r.GetString(1),
                Spec = r.GetString(2),
                Unit = r.GetString(3),
                Price = (decimal)r.GetDouble(4),
                Remark = r.GetString(5),
                CreatedAt = r.GetString(6)
            });
        }
        return list;
    }

    public long SaveProduct(Product p)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        if (p.Id == 0)
        {
            cmd.CommandText = """
                INSERT INTO Product(Name,Spec,Unit,Price,Remark,CreatedAt)
                VALUES($n,$s,$u,$pr,$r,$t); SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$t", Db.Now());
        }
        else
        {
            cmd.CommandText = """
                UPDATE Product SET Name=$n,Spec=$s,Unit=$u,Price=$pr,Remark=$r WHERE Id=$id;
                SELECT $id;
                """;
            cmd.Parameters.AddWithValue("$id", p.Id);
        }
        cmd.Parameters.AddWithValue("$n", p.Name.Trim());
        cmd.Parameters.AddWithValue("$s", p.Spec.Trim());
        cmd.Parameters.AddWithValue("$u", p.Unit.Trim());
        cmd.Parameters.AddWithValue("$pr", (double)p.Price);
        cmd.Parameters.AddWithValue("$r", p.Remark.Trim());
        var id = Convert.ToInt64(cmd.ExecuteScalar());
        p.Id = id;
        return id;
    }

    public void DeleteProducts(IEnumerable<long> ids)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Product WHERE Id=$id";
        var p = cmd.CreateParameter();
        p.ParameterName = "$id";
        cmd.Parameters.Add(p);
        foreach (var id in ids)
        {
            p.Value = id;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // ---------- Delivery notes ----------

    public string NextNoteNo(DateTime date)
    {
        var prefix = "SD" + date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT NoteNo FROM DeliveryNote WHERE NoteNo LIKE $p ORDER BY NoteNo DESC LIMIT 1";
        cmd.Parameters.AddWithValue("$p", prefix + "%");
        var last = cmd.ExecuteScalar() as string;
        var seq = 1;
        if (!string.IsNullOrEmpty(last) && last.Length > prefix.Length &&
            int.TryParse(last[prefix.Length..], out var n))
        {
            seq = n + 1;
        }
        return prefix + seq.ToString("0000", CultureInfo.InvariantCulture);
    }

    public long SaveNote(DeliveryNote note)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        var now = Db.Now();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            if (note.Id == 0)
            {
                cmd.CommandText = """
                    INSERT INTO DeliveryNote
                      (NoteNo,CustomerId,CustomerName,CustomerPhone,CustomerAddress,NoteDate,
                       TotalAmount,Remark,Salesman,Receiver,Maker,Status,CreatedAt,UpdatedAt,
                       Contact,DeliveryMethod,Transporter,VehicleNo,SettleMethod)
                    VALUES($no,$cid,$cn,$cp,$ca,$d,$amt,$rm,$sm,$rc,$mk,$st,$now,$now,
                           $ct,$dm,$tp,$vn,$se);
                    SELECT last_insert_rowid();
                    """;
                cmd.Parameters.AddWithValue("$now", now);
            }
            else
            {
                cmd.CommandText = """
                    UPDATE DeliveryNote SET
                      NoteNo=$no,CustomerId=$cid,CustomerName=$cn,CustomerPhone=$cp,CustomerAddress=$ca,
                      NoteDate=$d,TotalAmount=$amt,Remark=$rm,Salesman=$sm,Receiver=$rc,Maker=$mk,
                      Status=$st,UpdatedAt=$now,Contact=$ct,DeliveryMethod=$dm,
                      Transporter=$tp,VehicleNo=$vn,SettleMethod=$se
                    WHERE Id=$id;
                    SELECT $id;
                    """;
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$id", note.Id);
            }

            cmd.Parameters.AddWithValue("$no", note.NoteNo);
            cmd.Parameters.AddWithValue("$cid", (object?)note.CustomerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cn", note.CustomerName);
            cmd.Parameters.AddWithValue("$cp", note.CustomerPhone);
            cmd.Parameters.AddWithValue("$ca", note.CustomerAddress);
            cmd.Parameters.AddWithValue("$d", note.NoteDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$amt", (double)note.TotalAmount);
            cmd.Parameters.AddWithValue("$rm", note.Remark);
            cmd.Parameters.AddWithValue("$sm", note.Salesman);
            cmd.Parameters.AddWithValue("$rc", note.Receiver);
            cmd.Parameters.AddWithValue("$mk", note.Maker);
            cmd.Parameters.AddWithValue("$st", (int)note.Status);
            cmd.Parameters.AddWithValue("$ct", note.Contact);
            cmd.Parameters.AddWithValue("$dm", note.DeliveryMethod);
            cmd.Parameters.AddWithValue("$tp", note.Transporter);
            cmd.Parameters.AddWithValue("$vn", note.VehicleNo);
            cmd.Parameters.AddWithValue("$se", note.SettleMethod);
            note.Id = Convert.ToInt64(cmd.ExecuteScalar());
        }

        using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM NoteItem WHERE NoteId=$id";
            del.Parameters.AddWithValue("$id", note.Id);
            del.ExecuteNonQuery();
        }

        var seq = 1;
        foreach (var item in note.Items)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO NoteItem(NoteId,Seq,ProductId,ProductName,Spec,Unit,Qty,Price,Remark)
                VALUES($nid,$sq,$pid,$pn,$sp,$un,$q,$pr,$rm);
                """;
            cmd.Parameters.AddWithValue("$nid", note.Id);
            cmd.Parameters.AddWithValue("$sq", seq++);
            cmd.Parameters.AddWithValue("$pid", (object?)item.ProductId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pn", item.ProductName);
            cmd.Parameters.AddWithValue("$sp", item.Spec);
            cmd.Parameters.AddWithValue("$un", item.Unit);
            cmd.Parameters.AddWithValue("$q", (double)item.Qty);
            cmd.Parameters.AddWithValue("$pr", (double)item.Price);
            cmd.Parameters.AddWithValue("$rm", item.Remark);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return note.Id;
    }

    public DeliveryNote? GetNote(long id)
    {
        using var conn = _db.Open();
        var note = QueryNotes(conn, "WHERE n.Id=$id", new() { ["$id"] = id }).FirstOrDefault();
        if (note is null) return null;
        note.Items = GetItems(conn, id);
        return note;
    }

    public List<DeliveryNote> QueryNotes(string? keyword = null, DateTime? from = null, DateTime? to = null,
        NoteStatus? status = null)
    {
        using var conn = _db.Open();
        var where = new List<string>();
        var ps = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Add("(n.NoteNo LIKE $kw OR n.CustomerName LIKE $kw OR n.CustomerPhone LIKE $kw OR n.Remark LIKE $kw)");
            ps["$kw"] = $"%{keyword.Trim()}%";
        }
        if (from.HasValue)
        {
            where.Add("n.NoteDate >= $from");
            ps["$from"] = from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (to.HasValue)
        {
            where.Add("n.NoteDate <= $to");
            ps["$to"] = to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (status.HasValue)
        {
            where.Add("n.Status = $st");
            ps["$st"] = (int)status.Value;
        }

        var sql = "SELECT n.Id,n.NoteNo,n.CustomerId,n.CustomerName,n.CustomerPhone,n.CustomerAddress,n.NoteDate," +
                  "n.TotalAmount,n.Remark,n.Salesman,n.Receiver,n.Maker,n.Status,n.CreatedAt,n.UpdatedAt," +
                  "(SELECT IFNULL(SUM(i.Qty),0) FROM NoteItem i WHERE i.NoteId=n.Id) AS TotalQty," +
                  NoteExtraColumns + " " +
                  "FROM DeliveryNote n ";
        if (where.Count > 0)
            sql += "WHERE " + string.Join(" AND ", where) + " ";
        sql += "ORDER BY n.NoteDate DESC, n.Id DESC";

        return QueryNotes(conn, null, ps, sql);
    }

    private const string NoteExtraColumns =
        "n.Contact,n.DeliveryMethod,n.Transporter,n.VehicleNo,n.SettleMethod";

    private static List<DeliveryNote> QueryNotes(SqliteConnection conn, string? whereClause,
        Dictionary<string, object?> ps, string? fullSql = null)
    {
        using var cmd = conn.CreateCommand();
        if (fullSql is not null)
        {
            cmd.CommandText = fullSql;
        }
        else
        {
            cmd.CommandText =
                "SELECT n.Id,n.NoteNo,n.CustomerId,n.CustomerName,n.CustomerPhone,n.CustomerAddress,n.NoteDate," +
                "n.TotalAmount,n.Remark,n.Salesman,n.Receiver,n.Maker,n.Status,n.CreatedAt,n.UpdatedAt," +
                "(SELECT IFNULL(SUM(i.Qty),0) FROM NoteItem i WHERE i.NoteId=n.Id) AS TotalQty," +
                NoteExtraColumns + " " +
                "FROM DeliveryNote n " + (whereClause ?? "") + " ORDER BY n.NoteDate DESC, n.Id DESC";
        }
        foreach (var kv in ps)
            cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

        var list = new List<DeliveryNote>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new DeliveryNote
            {
                Id = r.GetInt64(0),
                NoteNo = r.GetString(1),
                CustomerId = r.IsDBNull(2) ? null : r.GetInt64(2),
                CustomerName = r.GetString(3),
                CustomerPhone = r.GetString(4),
                CustomerAddress = r.GetString(5),
                NoteDate = DateTime.ParseExact(r.GetString(6), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                TotalAmount = (decimal)r.GetDouble(7),
                Remark = r.GetString(8),
                Salesman = r.GetString(9),
                Receiver = r.GetString(10),
                Maker = r.GetString(11),
                Status = (NoteStatus)r.GetInt32(12),
                CreatedAt = r.GetString(13),
                UpdatedAt = r.GetString(14),
                TotalQty = r.FieldCount > 15 && !r.IsDBNull(15) ? (decimal)r.GetDouble(15) : 0m,
                Contact = r.FieldCount > 16 ? r.GetString(16) : "",
                DeliveryMethod = r.FieldCount > 17 ? r.GetString(17) : "",
                Transporter = r.FieldCount > 18 ? r.GetString(18) : "",
                VehicleNo = r.FieldCount > 19 ? r.GetString(19) : "",
                SettleMethod = r.FieldCount > 20 ? r.GetString(20) : ""
            });
        }
        return list;
    }

    private static List<NoteItem> GetItems(SqliteConnection conn, long noteId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT Id,NoteId,Seq,ProductId,ProductName,Spec,Unit,Qty,Price,Remark " +
            "FROM NoteItem WHERE NoteId=$id ORDER BY Seq";
        cmd.Parameters.AddWithValue("$id", noteId);
        var list = new List<NoteItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new NoteItem
            {
                Id = r.GetInt64(0),
                NoteId = r.GetInt64(1),
                Seq = r.GetInt32(2),
                ProductId = r.IsDBNull(3) ? null : r.GetInt64(3),
                ProductName = r.GetString(4),
                Spec = r.GetString(5),
                Unit = r.GetString(6),
                Qty = (decimal)r.GetDouble(7),
                Price = (decimal)r.GetDouble(8),
                Remark = r.GetString(9)
            });
        }
        return list;
    }

    public void DeleteNotes(IEnumerable<long> ids)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM DeliveryNote WHERE Id=$id";
        var p = cmd.CreateParameter();
        p.ParameterName = "$id";
        cmd.Parameters.Add(p);
        foreach (var id in ids)
        {
            p.Value = id;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void SetNoteStatus(IEnumerable<long> ids, NoteStatus status)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE DeliveryNote SET Status=$st, UpdatedAt=$now WHERE Id=$id";
        var pId = cmd.CreateParameter();
        pId.ParameterName = "$id";
        cmd.Parameters.Add(pId);
        cmd.Parameters.AddWithValue("$st", (int)status);
        cmd.Parameters.AddWithValue("$now", Db.Now());
        foreach (var id in ids)
        {
            pId.Value = id;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // ---------- Reports ----------

    public List<(string Name, decimal Amount, decimal Qty, int Count)> SalesByCustomer(DateTime from, DateTime to)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.CustomerName,
                   SUM(n.TotalAmount) AS Amt,
                   COALESCE((SELECT SUM(i.Qty) FROM NoteItem i
                             JOIN DeliveryNote d ON d.Id=i.NoteId
                             WHERE d.CustomerName=n.CustomerName
                               AND d.NoteDate>=$f AND d.NoteDate<=$t),0) AS Qty,
                   COUNT(*) AS Cnt
            FROM DeliveryNote n
            WHERE n.NoteDate>=$f AND n.NoteDate<=$t
            GROUP BY n.CustomerName
            ORDER BY Amt DESC
            """;
        cmd.Parameters.AddWithValue("$f", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$t", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var list = new List<(string, decimal, decimal, int)>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add((r.GetString(0), (decimal)r.GetDouble(1), (decimal)r.GetDouble(2), r.GetInt32(3)));
        }
        return list;
    }

    public List<(string Name, decimal Amount, decimal Qty)> SalesByProduct(DateTime from, DateTime to)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT i.ProductName, SUM(i.Qty*i.Price) AS Amt, SUM(i.Qty) AS Qty
            FROM NoteItem i
            JOIN DeliveryNote n ON n.Id=i.NoteId
            WHERE n.NoteDate>=$f AND n.NoteDate<=$t
            GROUP BY i.ProductName
            ORDER BY Amt DESC
            """;
        cmd.Parameters.AddWithValue("$f", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$t", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var list = new List<(string, decimal, decimal)>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add((r.GetString(0), (decimal)r.GetDouble(1), (decimal)r.GetDouble(2)));
        }
        return list;
    }

    public List<(string Month, decimal Amount, int Count)> MonthlySummary(DateTime from, DateTime to)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT substr(NoteDate,1,7) AS M, SUM(TotalAmount), COUNT(*)
            FROM DeliveryNote
            WHERE NoteDate>=$f AND NoteDate<=$t
            GROUP BY M ORDER BY M
            """;
        cmd.Parameters.AddWithValue("$f", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$t", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var list = new List<(string, decimal, int)>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add((r.GetString(0), (decimal)r.GetDouble(1), r.GetInt32(2)));
        }
        return list;
    }

    public (decimal Total, int Count) Summary(DateTime from, DateTime to)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(TotalAmount),0), COUNT(*) FROM DeliveryNote WHERE NoteDate>=$f AND NoteDate<=$t";
        cmd.Parameters.AddWithValue("$f", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$t", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        using var r = cmd.ExecuteReader();
        r.Read();
        return ((decimal)r.GetDouble(0), r.GetInt32(1));
    }

    public int Count(string table)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ---------- 用户 ----------

    private static User ReadUser(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        UserName = r.GetString(1),
        DisplayName = r.GetString(2),
        Role = (UserRole)r.GetInt32(3),
        IsEnabled = r.GetInt32(4) != 0,
        CreatedAt = r.GetString(5),
        LastLoginAt = r.GetString(6)
    };

    private const string UserColumns = "Id,UserName,DisplayName,Role,IsEnabled,CreatedAt,LastLoginAt";

    public List<User> ListUsers()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {UserColumns} FROM AppUser ORDER BY Role DESC, UserName";
        var list = new List<User>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadUser(r));
        return list;
    }

    public User? FindUser(string userName)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {UserColumns} FROM AppUser WHERE UserName=$u COLLATE NOCASE";
        cmd.Parameters.AddWithValue("$u", userName.Trim());
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadUser(r) : null;
    }

    public bool UserExists(string userName) => FindUser(userName) is not null;

    public bool HasAnyUser()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM AppUser";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public long InsertUser(User user, string password)
    {
        var (hash, salt) = PasswordHasher.Create(password);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AppUser(UserName,DisplayName,Role,PasswordHash,Salt,IsEnabled,CreatedAt,LastLoginAt)
            VALUES($u,$d,$r,$h,$s,$e,$now,'');
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$u", user.UserName.Trim());
        cmd.Parameters.AddWithValue("$d", user.DisplayName.Trim());
        cmd.Parameters.AddWithValue("$r", (int)user.Role);
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$s", salt);
        cmd.Parameters.AddWithValue("$e", user.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$now", Db.Now());
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public void UpdateUserProfile(long id, string displayName, UserRole role, bool enabled)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE AppUser SET DisplayName=$d, Role=$r, IsEnabled=$e WHERE Id=$id";
        cmd.Parameters.AddWithValue("$d", displayName.Trim());
        cmd.Parameters.AddWithValue("$r", (int)role);
        cmd.Parameters.AddWithValue("$e", enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetPassword(long id, string password)
    {
        var (hash, salt) = PasswordHasher.Create(password);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE AppUser SET PasswordHash=$h, Salt=$s WHERE Id=$id";
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$s", salt);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>返回 (hash, salt, enabled)；用户不存在时返回 null。</summary>
    public (string Hash, string Salt, bool Enabled)? GetCredentials(string userName)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash,Salt,IsEnabled FROM AppUser WHERE UserName=$u COLLATE NOCASE";
        cmd.Parameters.AddWithValue("$u", userName.Trim());
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return (r.GetString(0), r.GetString(1), r.GetInt32(2) != 0);
    }

    public void TouchLastLogin(long id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE AppUser SET LastLoginAt=$now WHERE Id=$id";
        cmd.Parameters.AddWithValue("$now", Db.Now());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteUser(long id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM AppUser WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public int CountAdmins()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM AppUser WHERE Role=1 AND IsEnabled=1";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
