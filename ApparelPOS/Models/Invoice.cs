namespace ApparelPOS.Models;

public class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty; // e.g. INV-20261029-0001
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    
    // Customer Info
    public string CustomerName { get; set; } = "Walk-in Customer";
    public string CustomerPhone { get; set; } = string.Empty;
    
    // Payment details
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Card, UPI, Split
    public decimal SubTotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    
    // Tax breakdown (GST: CGST + SGST)
    public decimal TaxAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    
    public decimal RoundOff { get; set; }
    public decimal TotalAmount { get; set; }
    
    public decimal CashReceived { get; set; }
    public decimal ChangeDue { get; set; }
    
    public string CashierName { get; set; } = "Admin Cashier";
    public string? Notes { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}
