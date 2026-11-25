namespace FangKeDelivery.Core;

public sealed class Customer
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Remark { get; set; } = "";
    public string CreatedAt { get; set; } = "";

    public override string ToString() => Name;
}

public sealed class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Spec { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Price { get; set; }
    public string Remark { get; set; } = "";
    public string CreatedAt { get; set; } = "";

    public override string ToString() => string.IsNullOrWhiteSpace(Spec) ? Name : $"{Name} {Spec}";
}

public sealed class NoteItem
{
    public long Id { get; set; }
    public long NoteId { get; set; }
    public int Seq { get; set; }
    public long? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Spec { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public string Remark { get; set; } = "";

    public decimal Amount => Math.Round(Qty * Price, 2, MidpointRounding.AwayFromZero);
}

public enum NoteStatus
{
    未送货 = 0,
    已送货 = 1,
    已结算 = 2
}

/// <summary>单据审核状态。只有「已审核」的单据才计入库存并锁定编辑。</summary>
public enum ApproveStatus
{
    待审核 = 0,
    已审核 = 1,
    已驳回 = 2
}

/// <summary>库存变动类型。数量在流水里带符号：入库为正，出库为负。</summary>
public enum StockMoveType
{
    入库 = 0,
    出库 = 1,
    盘点 = 2
}

public sealed class DeliveryNote
{
    public long Id { get; set; }
    public string NoteNo { get; set; } = "";
    public long? CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string CustomerAddress { get; set; } = "";

    /// <summary>客户联系人（参考单上「联系人」栏）。</summary>
    public string Contact { get; set; } = "";

    /// <summary>送货方式，如「送货上门」「物流代运」。</summary>
    public string DeliveryMethod { get; set; } = "";

    /// <summary>运货人 / 司机。</summary>
    public string Transporter { get; set; } = "";

    /// <summary>运货车号。</summary>
    public string VehicleNo { get; set; } = "";

    /// <summary>结算方式，如「现金」「月结」。</summary>
    public string SettleMethod { get; set; } = "";
    public DateTime NoteDate { get; set; } = DateTime.Today;
    public decimal TotalAmount { get; set; }
    public string Remark { get; set; } = "";
    public string Salesman { get; set; } = "";
    public string Receiver { get; set; } = "";
    public string Maker { get; set; } = "";
    public NoteStatus Status { get; set; } = NoteStatus.未送货;
    public ApproveStatus ApproveStatus { get; set; } = ApproveStatus.待审核;
    public string Approver { get; set; } = "";
    public string ApproveAt { get; set; } = "";
    public string ApproveRemark { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public List<NoteItem> Items { get; set; } = new();

    public string TotalAmountInWords => MoneyToChinese.Convert(TotalAmount);

    /// <summary>明细数量合计；列表查询时由 SQL 聚合填充，避免为每张单加载全部明细。</summary>
    public decimal TotalQty { get; set; }
}


/// <summary>入库单（采购/退货入库）。审核通过后增加库存。</summary>
public sealed class StockIn
{
    public long Id { get; set; }
    public string InNo { get; set; } = "";
    public string Supplier { get; set; } = "";
    public DateTime InDate { get; set; } = DateTime.Today;
    public decimal TotalAmount { get; set; }
    public string Remark { get; set; } = "";
    public string Maker { get; set; } = "";
    public ApproveStatus ApproveStatus { get; set; } = ApproveStatus.待审核;
    public string Approver { get; set; } = "";
    public string ApproveAt { get; set; } = "";
    public string ApproveRemark { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public List<StockInItem> Items { get; set; } = new();

    /// <summary>明细数量合计。列表查询由 SQL 聚合填充，避免逐单加载明细。</summary>
    public decimal TotalQty { get; set; }
}

public sealed class StockInItem
{
    public long Id { get; set; }
    public long StockInId { get; set; }
    public int Seq { get; set; }
    public long? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Spec { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public string Remark { get; set; } = "";

    public decimal Amount => Math.Round(Qty * Price, 2, MidpointRounding.AwayFromZero);
}

/// <summary>库存现存量。每种商品一行。</summary>
public sealed class StockRow
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Spec { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal SafetyQty { get; set; }
    public decimal Price { get; set; }

    public decimal Amount => Math.Round(Qty * Price, 2, MidpointRounding.AwayFromZero);
    public bool IsLow => SafetyQty > 0 && Qty <= SafetyQty;
}

/// <summary>库存流水，一次变动一行。Qty 带符号，便于直接求和核对。</summary>
public sealed class StockMove
{
    public long Id { get; set; }
    public long? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Spec { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal QtyBefore { get; set; }
    public decimal QtyAfter { get; set; }
    public StockMoveType Type { get; set; }
    public string SourceNo { get; set; } = "";
    public long? SourceId { get; set; }
    public string Operator { get; set; } = "";
    public string Remark { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

/// <summary>审核日志。每张单据的送审/通过/驳回/反审核都留痕。</summary>
public sealed class AuditEntry
{
    public long Id { get; set; }
    public string DocType { get; set; } = "";
    public long DocId { get; set; }
    public string DocNo { get; set; } = "";
    public string Action { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Remark { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}
