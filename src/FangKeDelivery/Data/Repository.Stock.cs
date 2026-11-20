using System.Globalization;
using Microsoft.Data.Sqlite;
using FangKeDelivery.Core;

namespace FangKeDelivery.Data;

/// <summary>
/// 进销存与审核。库存只由「已审核」的单据驱动：
/// 送货单审核通过扣库存，入库单审核通过加库存，反审核按流水原样冲回。
/// </summary>
public sealed partial class Repository
{
    public const string DocNote = "送货单";
    public const string DocStockIn = "入库单";

    /// <summary>是否启用进销存。关闭后审核仍可用，但不产生库存变动。</summary>
    public bool StockEnabled => GetSetting("StockEnabled", "1") != "0";

    /// <summary>允许负库存。默认允许：小企业期初库存常常没录全，硬卡会没法用。</summary>
    public bool AllowNegativeStock => GetSetting("StockAllowNegative", "1") != "0";

    // ---------- 库存现存量 ----------

    /// <summary>列出库存。关键字匹配名称/规格；onlyLow 只看低于安全库存的。</summary>
    public List<StockRow> GetStock(string? keyword = null, bool onlyLow = false)
    {
        using var conn = _db.Open();
        var where = new List<string>();
        var ps = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Add("(s.ProductName LIKE $kw OR s.Spec LIKE $kw)");
            ps["$kw"] = $"%{keyword.Trim()}%";
        }
        if (onlyLow) where.Add("s.SafetyQty > 0 AND s.Qty <= s.SafetyQty");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.ProductId, s.ProductName, s.Spec, s.Unit, s.Qty, s.SafetyQty,
                   COALESCE((SELECT p.Price FROM Product p WHERE p.Id = s.ProductId), 0)
            FROM Stock s
            """ + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") +
            " ORDER BY s.ProductName, s.Spec";
        foreach (var kv in ps) cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

        var list = new List<StockRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new StockRow
            {
                ProductId = r.GetInt64(0),
                ProductName = r.GetString(1),
                Spec = r.GetString(2),
                Unit = r.GetString(3),
                Qty = (decimal)r.GetDouble(4),
                SafetyQty = (decimal)r.GetDouble(5),
                Price = (decimal)r.GetDouble(6)
            });
        }
        return list;
    }

    public decimal GetStockQty(long productId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Qty FROM Stock WHERE ProductId=$p";
        cmd.Parameters.AddWithValue("$p", productId);
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? 0m : (decimal)Convert.ToDouble(v);
    }

    public void SetSafetyQty(long productId, decimal safety)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Stock SET SafetyQty=$q WHERE ProductId=$p";
        cmd.Parameters.AddWithValue("$q", (double)Math.Max(0m, safety));
        cmd.Parameters.AddWithValue("$p", productId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>盘点：把库存直接改成指定数量，差额记一条盘点流水。</summary>
    public void AdjustStock(long productId, decimal newQty, string remark, string op)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        var row = ReadStockRow(conn, tx, productId)
                  ?? throw new InvalidOperationException("该商品没有库存记录");
        var delta = newQty - row.Qty;
        if (delta != 0)
        {
            ApplyDelta(conn, tx, row, delta, StockMoveType.盘点, "盘点", null, op,
                string.IsNullOrWhiteSpace(remark) ? "手工盘点" : remark);
        }
        tx.Commit();
    }

    /// <summary>库存流水，按时间倒序。</summary>
    public List<StockMove> QueryStockMoves(string? keyword = null, DateTime? from = null,
        DateTime? to = null, StockMoveType? type = null, int limit = 500)
    {
        using var conn = _db.Open();
        var where = new List<string>();
        var ps = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Add("(ProductName LIKE $kw OR SourceNo LIKE $kw OR Remark LIKE $kw)");
            ps["$kw"] = $"%{keyword.Trim()}%";
        }
        if (from.HasValue)
        {
            where.Add("substr(CreatedAt,1,10) >= $f");
            ps["$f"] = from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (to.HasValue)
        {
            where.Add("substr(CreatedAt,1,10) <= $t");
            ps["$t"] = to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (type.HasValue)
        {
            where.Add("Type = $ty");
            ps["$ty"] = (int)type.Value;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id,ProductId,ProductName,Spec,Unit,Qty,QtyBefore,QtyAfter,Type,
                   SourceNo,SourceId,Operator,Remark,CreatedAt
            FROM StockMove
            """ + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") +
            " ORDER BY Id DESC LIMIT " + Math.Max(1, limit);
        foreach (var kv in ps) cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

        var list = new List<StockMove>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new StockMove
            {
                Id = r.GetInt64(0),
                ProductId = r.IsDBNull(1) ? null : r.GetInt64(1),
                ProductName = r.GetString(2),
                Spec = r.GetString(3),
                Unit = r.GetString(4),
                Qty = (decimal)r.GetDouble(5),
                QtyBefore = (decimal)r.GetDouble(6),
                QtyAfter = (decimal)r.GetDouble(7),
                Type = (StockMoveType)r.GetInt32(8),
                SourceNo = r.GetString(9),
                SourceId = r.IsDBNull(10) ? null : r.GetInt64(10),
                Operator = r.GetString(11),
                Remark = r.GetString(12),
                CreatedAt = r.GetString(13)
            });
        }
        return list;
    }

    // ---------- 审核日志 ----------

    public List<AuditEntry> QueryAuditLog(string? keyword = null, int limit = 300)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id,DocType,DocId,DocNo,Action,Operator,Remark,CreatedAt
            FROM AuditLog
            """ + (string.IsNullOrWhiteSpace(keyword) ? "" :
                   " WHERE DocNo LIKE $kw OR Operator LIKE $kw OR Action LIKE $kw OR Remark LIKE $kw") +
            " ORDER BY Id DESC LIMIT " + Math.Max(1, limit);
        if (!string.IsNullOrWhiteSpace(keyword))
            cmd.Parameters.AddWithValue("$kw", $"%{keyword.Trim()}%");

        var list = new List<AuditEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new AuditEntry
            {
                Id = r.GetInt64(0),
                DocType = r.GetString(1),
                DocId = r.GetInt64(2),
                DocNo = r.GetString(3),
                Action = r.GetString(4),
                Operator = r.GetString(5),
                Remark = r.GetString(6),
                CreatedAt = r.GetString(7)
            });
        }
        return list;
    }

    // ---------- 送货单审核 ----------

    /// <summary>待审核的送货单，供审核中心使用。</summary>
    public List<DeliveryNote> QueryNotesByApproval(ApproveStatus status, string? keyword = null)
    {
        using var conn = _db.Open();
        var where = new List<string> { "n.ApproveStatus = $ap" };
        var ps = new Dictionary<string, object?> { ["$ap"] = (int)status };
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Add("(n.NoteNo LIKE $kw OR n.CustomerName LIKE $kw)");
            ps["$kw"] = $"%{keyword.Trim()}%";
        }

        var sql = "SELECT n.Id,n.NoteNo,n.CustomerId,n.CustomerName,n.CustomerPhone,n.CustomerAddress,n.NoteDate," +
                  "n.TotalAmount,n.Remark,n.Salesman,n.Receiver,n.Maker,n.Status,n.CreatedAt,n.UpdatedAt," +
                  "(SELECT IFNULL(SUM(i.Qty),0) FROM NoteItem i WHERE i.NoteId=n.Id) AS TotalQty," +
                  NoteExtraColumns + " FROM DeliveryNote n WHERE " + string.Join(" AND ", where) +
                  " ORDER BY n.NoteDate, n.Id";
        return QueryNotes(conn, null, ps, sql);
    }

    /// <summary>
    /// 审核通过送货单：扣减库存。已审核的单据再审核会被拒绝，
    /// 保证同一张单不会重复扣库存。
    /// </summary>
    public (bool Ok, string Message) ApproveNote(long noteId, string approver, string remark = "")
    {
        return ChangeNoteApproval(noteId, ApproveStatus.已审核, approver, remark);
    }

    /// <summary>驳回：不做库存变动，单据退回可编辑。</summary>
    public (bool Ok, string Message) RejectNote(long noteId, string approver, string remark = "")
    {
        return ChangeNoteApproval(noteId, ApproveStatus.已驳回, approver, remark);
    }

    /// <summary>反审核：把已审核单据的库存影响原样冲回。</summary>
    public (bool Ok, string Message) UnapproveNote(long noteId, string approver, string remark = "")
    {
        return ChangeNoteApproval(noteId, ApproveStatus.待审核, approver, remark);
    }

    private (bool Ok, string Message) ChangeNoteApproval(long noteId, ApproveStatus target,
        string approver, string remark)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        var note = ReadNoteHead(conn, tx, noteId);
        if (note is null) return (false, "单据不存在");

        var from = note.ApproveStatus;
        if (from == target) return (false, $"单据已是「{target}」状态");

        // 只有待审核/已驳回 → 已审核 才扣库存；已审核 → 待审核 要冲回
        var needOut = target == ApproveStatus.已审核 && from != ApproveStatus.已审核;
        var needRollback = from == ApproveStatus.已审核 && target != ApproveStatus.已审核;

        if (needOut && StockEnabled)
        {
            var items = ReadNoteItems(conn, tx, noteId);
            if (items.Count == 0) return (false, "单据没有明细，不能审核");

            var shortage = new List<string>();
            foreach (var it in items)
            {
                var product = ResolveProduct(conn, tx, it.ProductId, it.ProductName, it.Spec, it.Unit, it.Price);
                var row = ReadStockRow(conn, tx, product.Id);
                var have = row?.Qty ?? 0m;
                if (have < it.Qty) shortage.Add($"{it.ProductName} 现存 {have:0.###}，需 {it.Qty:0.###}");
            }
            if (shortage.Count > 0 && !AllowNegativeStock)
                return (false, "库存不足，无法审核：\n" + string.Join("\n", shortage));
        }

        var now = Db.Now();
        using (var up = conn.CreateCommand())
        {
            up.Transaction = tx;
            up.CommandText = """
                UPDATE DeliveryNote
                SET ApproveStatus=$ap, Approver=$by, ApproveAt=$at, ApproveRemark=$rm, UpdatedAt=$now
                WHERE Id=$id
                """;
            up.Parameters.AddWithValue("$ap", (int)target);
            up.Parameters.AddWithValue("$by", approver);
            up.Parameters.AddWithValue("$at", target == ApproveStatus.待审核 ? "" : now);
            up.Parameters.AddWithValue("$rm", remark ?? "");
            up.Parameters.AddWithValue("$now", now);
            up.Parameters.AddWithValue("$id", noteId);
            up.ExecuteNonQuery();
        }

        if (StockEnabled && (needOut || needRollback))
        {
            var items = ReadNoteItems(conn, tx, noteId);
            foreach (var it in items)
            {
                var product = ResolveProduct(conn, tx, it.ProductId, it.ProductName, it.Spec, it.Unit, it.Price);
                var row = EnsureStockRow(conn, tx, product);
                var delta = needOut ? -it.Qty : it.Qty;
                ApplyDelta(conn, tx, row, delta, StockMoveType.出库, note.NoteNo, noteId, approver,
                    needOut ? "送货单审核出库" : "送货单反审核冲回");
            }
        }

        WriteAudit(conn, tx, DocNote, noteId, note.NoteNo, ActionText(from, target), approver, remark);
        tx.Commit();
        return (true, $"送货单 {note.NoteNo} 已{ActionText(from, target)}");
    }

    private static string ActionText(ApproveStatus from, ApproveStatus to) => to switch
    {
        ApproveStatus.已审核 => "审核通过",
        ApproveStatus.已驳回 => "驳回",
        _ => from == ApproveStatus.已审核 ? "反审核" : "退回待审"
    };

    // ---------- 入库单 ----------

    public string NextStockInNo(DateTime date)
    {
        var prefix = "RK" + date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT InNo FROM StockIn WHERE InNo LIKE $p ORDER BY InNo DESC LIMIT 1";
        cmd.Parameters.AddWithValue("$p", prefix + "%");
        var last = cmd.ExecuteScalar() as string;
        var seq = 1;
        if (!string.IsNullOrEmpty(last) && last.Length > prefix.Length &&
            int.TryParse(last[prefix.Length..], out var n))
            seq = n + 1;
        return prefix + seq.ToString("0000", CultureInfo.InvariantCulture);
    }

    public long SaveStockIn(StockIn doc)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        var now = Db.Now();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            if (doc.Id == 0)
            {
                cmd.CommandText = """
                    INSERT INTO StockIn(InNo,Supplier,InDate,TotalAmount,Remark,Maker,
                                        ApproveStatus,Approver,ApproveAt,ApproveRemark,CreatedAt,UpdatedAt)
                    VALUES($no,$sp,$d,$amt,$rm,$mk,$ap,'','','',$now,$now);
                    SELECT last_insert_rowid();
                    """;
            }
            else
            {
                cmd.CommandText = """
                    UPDATE StockIn SET InNo=$no,Supplier=$sp,InDate=$d,TotalAmount=$amt,
                                       Remark=$rm,Maker=$mk,UpdatedAt=$now
                    WHERE Id=$id;
                    SELECT $id;
                    """;
                cmd.Parameters.AddWithValue("$id", doc.Id);
            }
            cmd.Parameters.AddWithValue("$no", doc.InNo);
            cmd.Parameters.AddWithValue("$sp", doc.Supplier);
            cmd.Parameters.AddWithValue("$d", doc.InDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$amt", (double)doc.TotalAmount);
            cmd.Parameters.AddWithValue("$rm", doc.Remark);
            cmd.Parameters.AddWithValue("$mk", doc.Maker);
            cmd.Parameters.AddWithValue("$ap", (int)doc.ApproveStatus);
            cmd.Parameters.AddWithValue("$now", now);
            doc.Id = Convert.ToInt64(cmd.ExecuteScalar());
        }

        using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM StockInItem WHERE StockInId=$id";
            del.Parameters.AddWithValue("$id", doc.Id);
            del.ExecuteNonQuery();
        }

        var seq = 1;
        foreach (var it in doc.Items)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO StockInItem(StockInId,Seq,ProductId,ProductName,Spec,Unit,Qty,Price,Remark)
                VALUES($d,$sq,$pid,$pn,$sp,$un,$q,$pr,$rm)
                """;
            cmd.Parameters.AddWithValue("$d", doc.Id);
            cmd.Parameters.AddWithValue("$sq", seq++);
            cmd.Parameters.AddWithValue("$pid", (object?)it.ProductId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pn", it.ProductName);
            cmd.Parameters.AddWithValue("$sp", it.Spec);
            cmd.Parameters.AddWithValue("$un", it.Unit);
            cmd.Parameters.AddWithValue("$q", (double)it.Qty);
            cmd.Parameters.AddWithValue("$pr", (double)it.Price);
            cmd.Parameters.AddWithValue("$rm", it.Remark);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return doc.Id;
    }

    public StockIn? GetStockIn(long id)
    {
        using var conn = _db.Open();
        var doc = ReadStockInHead(conn, null, id);
        if (doc is null) return null;
        doc.Items = ReadStockInItems(conn, null, id);
        return doc;
    }

    public List<StockIn> QueryStockIns(string? keyword = null, DateTime? from = null,
        DateTime? to = null, ApproveStatus? status = null)
    {
        using var conn = _db.Open();
        var where = new List<string>();
        var ps = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Add("(InNo LIKE $kw OR Supplier LIKE $kw OR Remark LIKE $kw)");
            ps["$kw"] = $"%{keyword.Trim()}%";
        }
        if (from.HasValue)
        {
            where.Add("InDate >= $f");
            ps["$f"] = from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (to.HasValue)
        {
            where.Add("InDate <= $t");
            ps["$t"] = to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (status.HasValue)
        {
            where.Add("ApproveStatus = $ap");
            ps["$ap"] = (int)status.Value;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.Id,s.InNo,s.Supplier,s.InDate,s.TotalAmount,s.Remark,s.Maker,
                   s.ApproveStatus,s.Approver,s.ApproveAt,s.ApproveRemark,s.CreatedAt,s.UpdatedAt,
                   (SELECT IFNULL(SUM(i.Qty),0) FROM StockInItem i WHERE i.StockInId=s.Id)
            FROM StockIn s
            """ + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") +
            " ORDER BY s.InDate DESC, s.Id DESC";
        foreach (var kv in ps) cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

        var list = new List<StockIn>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new StockIn
            {
                Id = r.GetInt64(0),
                InNo = r.GetString(1),
                Supplier = r.GetString(2),
                InDate = DateTime.ParseExact(r.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                TotalAmount = (decimal)r.GetDouble(4),
                Remark = r.GetString(5),
                Maker = r.GetString(6),
                ApproveStatus = (ApproveStatus)r.GetInt32(7),
                Approver = r.GetString(8),
                ApproveAt = r.GetString(9),
                ApproveRemark = r.GetString(10),
                CreatedAt = r.GetString(11),
                UpdatedAt = r.GetString(12),
                TotalQty = (decimal)r.GetDouble(13)
            });
        }
        return list;
    }

    /// <summary>列表页用的数量合计，避免逐单加载明细。</summary>
    public Dictionary<long, decimal> StockInQtyTotals(IEnumerable<long> ids)
    {
        var result = new Dictionary<long, decimal>();
        var idList = ids.ToList();
        if (idList.Count == 0) return result;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        var names = new List<string>();
        for (var i = 0; i < idList.Count; i++)
        {
            var p = "$i" + i;
            names.Add(p);
            cmd.Parameters.AddWithValue(p, idList[i]);
        }
        cmd.CommandText =
            "SELECT StockInId, IFNULL(SUM(Qty),0) FROM StockInItem " +
            "WHERE StockInId IN (" + string.Join(",", names) + ") GROUP BY StockInId";
        using var r = cmd.ExecuteReader();
        while (r.Read()) result[r.GetInt64(0)] = (decimal)r.GetDouble(1);
        return result;
    }

    public (bool Ok, string Message) ApproveStockIn(long id, string approver, string remark = "")
        => ChangeStockInApproval(id, ApproveStatus.已审核, approver, remark);

    public (bool Ok, string Message) RejectStockIn(long id, string approver, string remark = "")
        => ChangeStockInApproval(id, ApproveStatus.已驳回, approver, remark);

    public (bool Ok, string Message) UnapproveStockIn(long id, string approver, string remark = "")
        => ChangeStockInApproval(id, ApproveStatus.待审核, approver, remark);

    private (bool Ok, string Message) ChangeStockInApproval(long id, ApproveStatus target,
        string approver, string remark)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        var doc = ReadStockInHead(conn, tx, id);
        if (doc is null) return (false, "入库单不存在");
        if (doc.ApproveStatus == target) return (false, $"入库单已是「{target}」状态");

        var needIn = target == ApproveStatus.已审核 && doc.ApproveStatus != ApproveStatus.已审核;
        var needRollback = doc.ApproveStatus == ApproveStatus.已审核 && target != ApproveStatus.已审核;

        var now = Db.Now();
        using (var up = conn.CreateCommand())
        {
            up.Transaction = tx;
            up.CommandText = """
                UPDATE StockIn
                SET ApproveStatus=$ap, Approver=$by, ApproveAt=$at, ApproveRemark=$rm, UpdatedAt=$now
                WHERE Id=$id
                """;
            up.Parameters.AddWithValue("$ap", (int)target);
            up.Parameters.AddWithValue("$by", approver);
            up.Parameters.AddWithValue("$at", target == ApproveStatus.待审核 ? "" : now);
            up.Parameters.AddWithValue("$rm", remark ?? "");
            up.Parameters.AddWithValue("$now", now);
            up.Parameters.AddWithValue("$id", id);
            up.ExecuteNonQuery();
        }

        if (StockEnabled && (needIn || needRollback))
        {
            var items = ReadStockInItems(conn, tx, id);
            if (items.Count == 0) return (false, "入库单没有明细，不能审核");
            foreach (var it in items)
            {
                var product = ResolveProduct(conn, tx, it.ProductId, it.ProductName, it.Spec, it.Unit, it.Price);
                var row = EnsureStockRow(conn, tx, product);
                var delta = needIn ? it.Qty : -it.Qty;
                ApplyDelta(conn, tx, row, delta, StockMoveType.入库, doc.InNo, id, approver,
                    needIn ? "入库单审核入库" : "入库单反审核冲回");
            }
        }

        WriteAudit(conn, tx, DocStockIn, id, doc.InNo, ActionText(doc.ApproveStatus, target), approver, remark);
        tx.Commit();
        return (true, $"入库单 {doc.InNo} 已{ActionText(doc.ApproveStatus, target)}");
    }

    /// <summary>删除入库单。已审核的不能删，必须先反审核。</summary>
    public (bool Ok, string Message) DeleteStockIns(IEnumerable<long> ids)
    {
        var blocked = new List<string>();
        var deleted = 0;
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        foreach (var id in ids)
        {
            var doc = ReadStockInHead(conn, tx, id);
            if (doc is null) continue;
            if (doc.ApproveStatus == ApproveStatus.已审核)
            {
                blocked.Add(doc.InNo);
                continue;
            }
            using var del = conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = "DELETE FROM StockIn WHERE Id=$id";
            del.Parameters.AddWithValue("$id", id);
            del.ExecuteNonQuery();
            deleted++;
        }
        tx.Commit();

        if (blocked.Count > 0)
            return (deleted > 0, $"已删除 {deleted} 张。以下单据已审核，需先反审核：{string.Join("、", blocked)}");
        return (true, $"已删除 {deleted} 张入库单");
    }

    /// <summary>
    /// 彻底移除某商品的库存记录与流水。仅供自检清理测试数据使用，
    /// 正常业务流程不应调用（会丢失账实追溯）。
    /// </summary>
    public void PurgeStockOfProduct(long productId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        using (var delMove = conn.CreateCommand())
        {
            delMove.Transaction = tx;
            delMove.CommandText = "DELETE FROM StockMove WHERE ProductId=$p";
            delMove.Parameters.AddWithValue("$p", productId);
            delMove.ExecuteNonQuery();
        }

        using (var delStock = conn.CreateCommand())
        {
            delStock.Transaction = tx;
            delStock.CommandText = "DELETE FROM Stock WHERE ProductId=$p";
            delStock.Parameters.AddWithValue("$p", productId);
            delStock.ExecuteNonQuery();
        }

        tx.Commit();
    }

    // ---------- 内部：库存写入 ----------

    private sealed class StockSnapshot
    {
        public long ProductId;
        public string Name = "";
        public string Spec = "";
        public string Unit = "";
        public decimal Qty;
        public decimal SafetyQty;
    }

    private static StockSnapshot? ReadStockRow(SqliteConnection conn, SqliteTransaction? tx, long productId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT ProductId,ProductName,Spec,Unit,Qty,SafetyQty FROM Stock WHERE ProductId=$p";
        cmd.Parameters.AddWithValue("$p", productId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new StockSnapshot
        {
            ProductId = r.GetInt64(0),
            Name = r.GetString(1),
            Spec = r.GetString(2),
            Unit = r.GetString(3),
            Qty = (decimal)r.GetDouble(4),
            SafetyQty = (decimal)r.GetDouble(5)
        };
    }

    private static StockSnapshot EnsureStockRow(SqliteConnection conn, SqliteTransaction tx, Product product)
    {
        var row = ReadStockRow(conn, tx, product.Id);
        if (row is not null) return row;

        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO Stock(ProductId,ProductName,Spec,Unit,Qty,SafetyQty)
            VALUES($p,$n,$s,$u,0,0)
            """;
        ins.Parameters.AddWithValue("$p", product.Id);
        ins.Parameters.AddWithValue("$n", product.Name);
        ins.Parameters.AddWithValue("$s", product.Spec);
        ins.Parameters.AddWithValue("$u", product.Unit);
        ins.ExecuteNonQuery();

        return new StockSnapshot
        {
            ProductId = product.Id,
            Name = product.Name,
            Spec = product.Spec,
            Unit = product.Unit,
            Qty = 0m,
            SafetyQty = 0m
        };
    }

    /// <summary>写一条流水并更新现存量。数量与前后值同事务落库，保证账实一致。</summary>
    private static void ApplyDelta(SqliteConnection conn, SqliteTransaction tx, StockSnapshot row,
        decimal delta, StockMoveType type, string sourceNo, long? sourceId, string op, string remark)
    {
        var before = row.Qty;
        var after = before + delta;

        using (var up = conn.CreateCommand())
        {
            up.Transaction = tx;
            up.CommandText = "UPDATE Stock SET Qty=$q WHERE ProductId=$p";
            up.Parameters.AddWithValue("$q", (double)after);
            up.Parameters.AddWithValue("$p", row.ProductId);
            up.ExecuteNonQuery();
        }

        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO StockMove(ProductId,ProductName,Spec,Unit,Qty,QtyBefore,QtyAfter,Type,
                                  SourceNo,SourceId,Operator,Remark,CreatedAt)
            VALUES($p,$n,$s,$u,$q,$b,$a,$ty,$no,$sid,$op,$rm,$now)
            """;
        ins.Parameters.AddWithValue("$p", row.ProductId);
        ins.Parameters.AddWithValue("$n", row.Name);
        ins.Parameters.AddWithValue("$s", row.Spec);
        ins.Parameters.AddWithValue("$u", row.Unit);
        ins.Parameters.AddWithValue("$q", (double)delta);
        ins.Parameters.AddWithValue("$b", (double)before);
        ins.Parameters.AddWithValue("$a", (double)after);
        ins.Parameters.AddWithValue("$ty", (int)type);
        ins.Parameters.AddWithValue("$no", sourceNo);
        ins.Parameters.AddWithValue("$sid", (object?)sourceId ?? DBNull.Value);
        ins.Parameters.AddWithValue("$op", op);
        ins.Parameters.AddWithValue("$rm", remark);
        ins.Parameters.AddWithValue("$now", Db.Now());
        ins.ExecuteNonQuery();

        row.Qty = after;
    }

    /// <summary>
    /// 把单据明细行落到商品档案：优先用明细里的 ProductId，
    /// 其次按名称精确匹配，都不中则自动建档（送货单允许手打商品名）。
    /// </summary>
    private static Product ResolveProduct(SqliteConnection conn, SqliteTransaction tx,
        long? productId, string name, string spec, string unit, decimal price)
    {
        if (productId.HasValue)
        {
            using var byId = conn.CreateCommand();
            byId.Transaction = tx;
            byId.CommandText = "SELECT Id,Name,Spec,Unit,Price FROM Product WHERE Id=$id";
            byId.Parameters.AddWithValue("$id", productId.Value);
            using var r1 = byId.ExecuteReader();
            if (r1.Read())
            {
                return new Product
                {
                    Id = r1.GetInt64(0),
                    Name = r1.GetString(1),
                    Spec = r1.GetString(2),
                    Unit = r1.GetString(3),
                    Price = (decimal)r1.GetDouble(4)
                };
            }
        }

        using (var byName = conn.CreateCommand())
        {
            byName.Transaction = tx;
            byName.CommandText = "SELECT Id,Name,Spec,Unit,Price FROM Product WHERE Name=$n COLLATE NOCASE LIMIT 1";
            byName.Parameters.AddWithValue("$n", name);
            using var r2 = byName.ExecuteReader();
            if (r2.Read())
            {
                return new Product
                {
                    Id = r2.GetInt64(0),
                    Name = r2.GetString(1),
                    Spec = r2.GetString(2),
                    Unit = r2.GetString(3),
                    Price = (decimal)r2.GetDouble(4)
                };
            }
        }

        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO Product(Name,Spec,Unit,Price,Remark,CreatedAt)
            VALUES($n,$s,$u,$p,'由单据审核自动建档',$now);
            SELECT last_insert_rowid();
            """;
        ins.Parameters.AddWithValue("$n", name);
        ins.Parameters.AddWithValue("$s", spec);
        ins.Parameters.AddWithValue("$u", unit);
        ins.Parameters.AddWithValue("$p", (double)price);
        ins.Parameters.AddWithValue("$now", Db.Now());
        var newId = Convert.ToInt64(ins.ExecuteScalar());
        return new Product { Id = newId, Name = name, Spec = spec, Unit = unit, Price = price };
    }

    private static void WriteAudit(SqliteConnection conn, SqliteTransaction tx, string docType,
        long docId, string docNo, string action, string op, string remark)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO AuditLog(DocType,DocId,DocNo,Action,Operator,Remark,CreatedAt)
            VALUES($t,$id,$no,$a,$op,$rm,$now)
            """;
        cmd.Parameters.AddWithValue("$t", docType);
        cmd.Parameters.AddWithValue("$id", docId);
        cmd.Parameters.AddWithValue("$no", docNo);
        cmd.Parameters.AddWithValue("$a", action);
        cmd.Parameters.AddWithValue("$op", op);
        cmd.Parameters.AddWithValue("$rm", remark ?? "");
        cmd.Parameters.AddWithValue("$now", Db.Now());
        cmd.ExecuteNonQuery();
    }

    private static DeliveryNote? ReadNoteHead(SqliteConnection conn, SqliteTransaction tx, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT Id,NoteNo,ApproveStatus FROM DeliveryNote WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new DeliveryNote
        {
            Id = r.GetInt64(0),
            NoteNo = r.GetString(1),
            ApproveStatus = (ApproveStatus)r.GetInt32(2)
        };
    }

    private static List<NoteItem> ReadNoteItems(SqliteConnection conn, SqliteTransaction tx, long noteId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT ProductId,ProductName,Spec,Unit,Qty,Price
            FROM NoteItem WHERE NoteId=$id ORDER BY Seq
            """;
        cmd.Parameters.AddWithValue("$id", noteId);
        var list = new List<NoteItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new NoteItem
            {
                ProductId = r.IsDBNull(0) ? null : r.GetInt64(0),
                ProductName = r.GetString(1),
                Spec = r.GetString(2),
                Unit = r.GetString(3),
                Qty = (decimal)r.GetDouble(4),
                Price = (decimal)r.GetDouble(5)
            });
        }
        return list;
    }

    private static StockIn? ReadStockInHead(SqliteConnection conn, SqliteTransaction? tx, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT Id,InNo,Supplier,InDate,TotalAmount,Remark,Maker,ApproveStatus,
                   Approver,ApproveAt,ApproveRemark,CreatedAt,UpdatedAt
            FROM StockIn WHERE Id=$id
            """;
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new StockIn
        {
            Id = r.GetInt64(0),
            InNo = r.GetString(1),
            Supplier = r.GetString(2),
            InDate = DateTime.ParseExact(r.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            TotalAmount = (decimal)r.GetDouble(4),
            Remark = r.GetString(5),
            Maker = r.GetString(6),
            ApproveStatus = (ApproveStatus)r.GetInt32(7),
            Approver = r.GetString(8),
            ApproveAt = r.GetString(9),
            ApproveRemark = r.GetString(10),
            CreatedAt = r.GetString(11),
            UpdatedAt = r.GetString(12)
        };
    }

    private static List<StockInItem> ReadStockInItems(SqliteConnection conn, SqliteTransaction? tx, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT Id,StockInId,Seq,ProductId,ProductName,Spec,Unit,Qty,Price,Remark
            FROM StockInItem WHERE StockInId=$id ORDER BY Seq
            """;
        cmd.Parameters.AddWithValue("$id", id);
        var list = new List<StockInItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new StockInItem
            {
                Id = r.GetInt64(0),
                StockInId = r.GetInt64(1),
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
}
