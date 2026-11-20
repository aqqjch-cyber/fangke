using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery;

/// <summary>
/// 进销存与审核自检。重点验证「单据 ↔ 库存」的一致性：
/// 审核入库加库存、审核送货扣库存、反审核原样冲回、重复审核不重复记账。
/// </summary>
internal static class StockSelfTest
{
    public static int Run(Repository repo, string outDir, System.Text.StringBuilder log)
    {
        Directory.CreateDirectory(outDir);
        var failures = 0;

        void Check(string name, string actual, string expected)
        {
            var ok = actual == expected;
            if (!ok) failures++;
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}: \"{actual}\"" +
                           (ok ? "" : $"  expected \"{expected}\""));
        }

        void CheckBool(string name, bool actual, bool expected)
        {
            var ok = actual == expected;
            if (!ok) failures++;
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}: {actual}" +
                           (ok ? "" : $"  expected {expected}"));
        }

        var stamp = DateTime.Now.ToString("HHmmssfff");
        var productName = "自检库存商品" + stamp;

        var product = new Product
        {
            Name = productName,
            Spec = "T-1",
            Unit = "件",
            Price = 10m
        };
        repo.SaveProduct(product);

        // ---------- 1. 入库单审核加库存 ----------
        var stockIn = new StockIn
        {
            InNo = repo.NextStockInNo(new DateTime(2026, 11, 20)),
            Supplier = "自检供应商",
            InDate = new DateTime(2026, 11, 20),
            Maker = "自检",
            Items =
            {
                new StockInItem
                {
                    ProductId = product.Id, ProductName = productName, Spec = "T-1",
                    Unit = "件", Qty = 100, Price = 6m
                }
            }
        };
        stockIn.TotalAmount = stockIn.Items.Sum(i => i.Amount);
        var inId = repo.SaveStockIn(stockIn);

        Check("入库单初始状态", repo.GetStockIn(inId)?.ApproveStatus.ToString() ?? "", "待审核");
        Check("未审核不加库存", repo.GetStockQty(product.Id).ToString("0.###"), "0");

        var r1 = repo.ApproveStockIn(inId, "自检审核员", "数量无误");
        CheckBool("审核入库单成功", r1.Ok, true);
        Check("入库后库存", repo.GetStockQty(product.Id).ToString("0.###"), "100");
        Check("入库单状态", repo.GetStockIn(inId)?.ApproveStatus.ToString() ?? "", "已审核");
        Check("入库单审核人", repo.GetStockIn(inId)?.Approver ?? "", "自检审核员");

        // 重复审核必须被拒绝，不能重复加库存
        var r2 = repo.ApproveStockIn(inId, "自检审核员");
        CheckBool("重复审核被拒绝", r2.Ok, false);
        Check("重复审核后库存不变", repo.GetStockQty(product.Id).ToString("0.###"), "100");

        // ---------- 2. 送货单审核扣库存 ----------
        var customer = new Customer { Name = "自检库存客户" + stamp };
        repo.SaveCustomer(customer);

        var note = new DeliveryNote
        {
            NoteNo = repo.NextNoteNo(new DateTime(2026, 11, 20)),
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            NoteDate = new DateTime(2026, 11, 20),
            Maker = "自检",
            Items =
            {
                new NoteItem
                {
                    ProductId = product.Id, ProductName = productName, Spec = "T-1",
                    Unit = "件", Qty = 30, Price = 10m
                }
            }
        };
        note.TotalAmount = note.Items.Sum(i => i.Amount);
        var noteId = repo.SaveNote(note);

        Check("送货单初始待审核", repo.GetNote(noteId)?.ApproveStatus.ToString() ?? "", "待审核");
        Check("送货单未审核不扣库存", repo.GetStockQty(product.Id).ToString("0.###"), "100");

        var r3 = repo.ApproveNote(noteId, "自检审核员");
        CheckBool("审核送货单成功", r3.Ok, true);
        Check("送货出库后库存", repo.GetStockQty(product.Id).ToString("0.###"), "70");

        // ---------- 3. 反审核冲回 ----------
        var r4 = repo.UnapproveNote(noteId, "自检审核员", "客户改单");
        CheckBool("反审核送货单成功", r4.Ok, true);
        Check("反审核后库存回滚", repo.GetStockQty(product.Id).ToString("0.###"), "100");
        Check("反审核后状态", repo.GetNote(noteId)?.ApproveStatus.ToString() ?? "", "待审核");

        // ---------- 4. 驳回不产生库存变动 ----------
        var r5 = repo.RejectNote(noteId, "自检审核员", "价格有误");
        CheckBool("驳回送货单成功", r5.Ok, true);
        Check("驳回后库存不变", repo.GetStockQty(product.Id).ToString("0.###"), "100");
        Check("驳回后状态", repo.GetNote(noteId)?.ApproveStatus.ToString() ?? "", "已驳回");

        // 驳回后可以再审核
        var r6 = repo.ApproveNote(noteId, "自检审核员", "已改价");
        CheckBool("驳回后可重新审核", r6.Ok, true);
        Check("重新审核后库存", repo.GetStockQty(product.Id).ToString("0.###"), "70");

        // ---------- 5. 盘点调整 ----------
        repo.AdjustStock(product.Id, 65, "盘亏 5 件", "自检");
        Check("盘点后库存", repo.GetStockQty(product.Id).ToString("0.###"), "65");

        // ---------- 6. 库存流水可核对 ----------
        var moves = repo.QueryStockMoves(productName);
        var netFromMoves = moves.Sum(m => m.Qty);
        Check("流水净额等于现存", netFromMoves.ToString("0.###"), "65");
        // 入库100、出库30、冲回30、再出库30、盘点-5
        Check("流水条数", moves.Count.ToString(), "5");

        // ---------- 7. 审核日志留痕 ----------
        var noteLogs = repo.QueryAuditLog(note.NoteNo);
        CheckBool("审核日志有记录", noteLogs.Count > 0, true);
        var actions = string.Join("/", noteLogs.Select(l => l.Action));
        CheckBool("日志含审核通过", actions.Contains("审核通过"), true);
        CheckBool("日志含反审核", actions.Contains("反审核"), true);
        CheckBool("日志含驳回", actions.Contains("驳回"), true);

        // ---------- 8. 已审核单据不可删除入库单 ----------
        var delApproved = repo.DeleteStockIns(new[] { inId });
        CheckBool("已审核入库单禁止删除", delApproved.Ok, false);
        CheckBool("提示需先反审核", delApproved.Message.Contains("反审核"), true);

        // ---------- 9. 安全库存预警 ----------
        repo.SetSafetyQty(product.Id, 80);
        var low = repo.GetStock(productName, onlyLow: true);
        CheckBool("低于安全库存被标记", low.Any(s => s.ProductId == product.Id), true);
        repo.SetSafetyQty(product.Id, 10);
        var notLow = repo.GetStock(productName, onlyLow: true);
        CheckBool("高于安全库存不标记", notLow.All(s => s.ProductId != product.Id), true);

        // ---------- 10. 入库单数量合计 ----------
        var ins = repo.QueryStockIns(null, null, null, null).FirstOrDefault(d => d.Id == inId);
        Check("入库单列表数量合计", ins?.TotalQty.ToString("0.###") ?? "", "100");

        // ---------- 清理 ----------
        repo.UnapproveNote(noteId, "自检", "清理");
        repo.UnapproveStockIn(inId, "自检", "清理");
        repo.DeleteNotes(new[] { noteId });
        repo.DeleteStockIns(new[] { inId });
        repo.DeleteCustomers(new[] { customer.Id });
        repo.PurgeStockOfProduct(product.Id);
        repo.DeleteProducts(new[] { product.Id });

        Check("清理后库存记录已移除",
            repo.GetStock(productName).Count.ToString(), "0");

        return failures;
    }
}
