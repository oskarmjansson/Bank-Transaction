namespace Lab2Infoinfra.Models;

public class Rule
{
    public int rID { get; set; }
    public string Reference { get; set; }
    public int? TransactionID { get; set; }
    public string Category { get; set; }

    public Rule() { } 


    public Rule(int rID, string Reference, int TransactionID, string Category)
    {
        this.rID = rID;
        this.Reference = Reference;
        this.TransactionID = TransactionID;
        this.Category = Category;
    }

}