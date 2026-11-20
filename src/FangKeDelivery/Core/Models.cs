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
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public List<NoteItem> Items { get; set; } = new();

    public string TotalAmountInWords => MoneyToChinese.Convert(TotalAmount);

    /// <summary>明细数量合计；列表查询时由 SQL 聚合填充，避免为每张单加载全部明细。</summary>
    public decimal TotalQty { get; set; }
}
