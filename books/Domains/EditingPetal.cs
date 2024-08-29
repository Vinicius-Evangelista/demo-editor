public class EditingPetal
{
    public int? NumberOfChapters { get; set; }
    public Status? Status { get; set; }
}

public class Status
{
    public string Value { get; set; }
}

public class SalesPetal
{
    public MonetaryAmount? Price { get; set; }
    public decimal? Weight { get; set; }
}

public class MonetaryAmount
{
    public decimal Value { get; set; }
    public string MonetaryUnit { get; set; } 
}