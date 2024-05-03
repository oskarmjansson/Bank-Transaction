namespace Lab2Infoinfra.Models;

public class Transaction
{
    public int TransactionID { get; set; }
    public string BookingDate { get; set; }
    public string TransactionDate { get; set; }
    public string Reference { get; set; }
    public double Amount { get; set; }
    public double Balance { get; set; }
    public string Category { get; set; }

    public Transaction() { } 


    public Transaction(int TransactionID, string BookingDate, string TransactionDate, string Reference, double Amount, double Balance, string Category)
    {
        this.TransactionID = TransactionID;
        this.BookingDate = BookingDate;
        this.TransactionDate = TransactionDate;
        this.Reference = Reference;
        this.Amount = Amount;
        this.Balance = Balance;
        this.Category = Category;
    }
}