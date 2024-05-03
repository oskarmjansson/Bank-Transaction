using Lab2Infoinfra.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using System.Xml.Linq;
using System.Diagnostics;
using System.Net;
namespace MyApp.Namespace
{
    public class APIController : Controller
    {
        public static List<Transaction> listOfTransactions = new List<Transaction>();
        static readonly HttpClient client = new HttpClient();
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult GetAPI()
        {
            string jsonResult = string.Empty;

            try
            {
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", "Bearer 3d55fa0215245b71ff8c838ebcea241ac096253f");

                using (HttpResponseMessage response =
                client.GetAsync("https://bank.stuxberg.se/api/iban/SE4550000000058398257466/").Result)
                {
                    Console.WriteLine($"Status code: {response.StatusCode}");

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        var errorViewModel = new ErrorViewModel
                        {
                            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                            ErrorMessage = "Fel token."
                        };
                        return View("Error", errorViewModel);
                    }
                    else if (response.StatusCode == (HttpStatusCode)429)
                    {
                        var errorViewModel = new ErrorViewModel
                        {
                            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                            ErrorMessage = "API kallades för många gånger."
                        };
                        return View("Error", errorViewModel);
                    }
                    using (HttpContent content = response.Content)
                    {
                        jsonResult = content.ReadAsStringAsync().Result;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");


                var errorViewModel = new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    //För att lösa kravet " Noterbart är att varje felmed- delande ska vara informativt.
                    //men detta kanske kan ge användaren känslig information
                    //som gör att det blir enklare att hacka sidan.
                    ErrorMessage = "Okänt fel vid hämtning av API."
                };
                return View("Error", errorViewModel);
            }
            List<Transaction> listOfTransactions;

            try
            {
                listOfTransactions = JsonSerializer.Deserialize<List<Transaction>>(jsonResult);
            }
            catch (JsonException)
            {
                var errorViewModel = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };
                return View("Error", errorViewModel);
            }

            foreach (var transaction in listOfTransactions)
            {
                if (transaction.Category == null)
                {
                    transaction.Category = "Övrigt";
                }
            }

            CreateDatabase(listOfTransactions);
            return View(listOfTransactions);
        }
        public ActionResult DisplayTransactions()
        {
            //Tömmer listan först så den inte fyller på med dubbletter. 
            listOfTransactions.Clear();

            //Hämtar all data frånbasen och skickar den till vyn DisplayTransactions.
            using (var connection = new SqliteConnection("Data Source=transactionDB.db"))
            {
                connection.Open();

                using (var command = new SqliteCommand("SELECT * FROM Transactions", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Transaction transaction = new Transaction
                            {
                                TransactionID = Convert.ToInt32(reader["TransactionID"]),
                                BookingDate = reader["BookingDate"].ToString(),
                                TransactionDate = reader["TransactionDate"].ToString(),
                                Reference = reader["Reference"].ToString(),
                                Amount = Convert.ToDouble(reader["Amount"]),
                                Balance = Convert.ToDouble(reader["Balance"]),
                                Category = reader["Category"].ToString()
                            };
                            listOfTransactions.Add(transaction);
                        }
                    }
                }
                connection.Close();
            }
            return View(listOfTransactions);
        }

        public ActionResult DisplaySummary()
        {
            //Tömmer listan först så den inte fyller på med dubbletter. 
            listOfTransactions.Clear();
            double positiveSum = 0;
            double negativeSum = 0;
            Dictionary<string, (double positive, double negative)> categorySums = new Dictionary<string, (double positive, double negative)>();


            //Hämtar all data frånbasen och skickar den till vyn DisplayTransactions.
            using (var connection = new SqliteConnection("Data Source=transactionDB.db"))
            {
                connection.Open();

                using (var command = new SqliteCommand("SELECT * FROM Transactions", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Transaction transaction = new Transaction
                            {
                                TransactionID = Convert.ToInt32(reader["TransactionID"]),
                                BookingDate = reader["BookingDate"].ToString(),
                                TransactionDate = reader["TransactionDate"].ToString(),
                                Reference = reader["Reference"].ToString(),
                                Amount = Convert.ToDouble(reader["Amount"]),
                                Balance = Convert.ToDouble(reader["Balance"]),
                                Category = reader["Category"].ToString()
                            };
                            listOfTransactions.Add(transaction);

                            if (transaction.Amount > 0)
                            {
                                positiveSum += transaction.Amount;
                            }
                            else if (transaction.Amount < 0)
                            {
                                negativeSum += transaction.Amount;
                            }

                            if (categorySums.ContainsKey(transaction.Category))
                            {
                                if (transaction.Amount > 0)
                                {
                                    categorySums[transaction.Category] = (categorySums[transaction.Category].positive + transaction.Amount, categorySums[transaction.Category].negative);
                                }
                                else
                                {
                                    categorySums[transaction.Category] = (categorySums[transaction.Category].positive, categorySums[transaction.Category].negative + transaction.Amount);
                                }
                            }
                            else
                            {
                                if (transaction.Amount > 0)
                                {
                                    categorySums[transaction.Category] = (transaction.Amount, 0);
                                }
                                else
                                {
                                    categorySums[transaction.Category] = (0, transaction.Amount);
                                }
                            }
                        }

                    }
                }
                connection.Close();
            }

            ViewBag.PositiveSum = positiveSum;
            ViewBag.NegativeSum = negativeSum;
            XElement summary = new XElement("Summary",
                new XElement("PositiveSum", positiveSum),
                new XElement("NegativeSum", negativeSum)
            );


            foreach (var categorySum in categorySums)
            {
                string sanitizedKey = categorySum.Key.Replace(" ", "_").Replace("/", "_").Replace("(", "").Replace(")", "").Replace("&", "and");
                summary.Add(new XElement(sanitizedKey,
                    new XElement("Positive", categorySum.Value.positive),
                    new XElement("Negative", categorySum.Value.negative)
                ));

    
            }
            System.IO.File.WriteAllText("summary.xml", summary.ToString());



            return View(summary);
        }
        public ActionResult EditTransaction(int TransactionIDPara)
        {
            //Hämtar en specifik transaktion och skickar den till en vy för att redigera.
            //Get funktionen
            foreach (var transaction in listOfTransactions)
            {
                if (transaction.TransactionID == TransactionIDPara)
                {
                    return View(transaction);
                }
            }
            return NotFound();
        }
        [HttpPost]
        public ActionResult EditTransaction(Transaction? TransactionIDPara)
        {
            //Redigerar en specifik transaktion och skickar den till en vy för att redigera.
            //Post funktionen
            using (var connection = new SqliteConnection("Data Source=transactionDB.db")) // creates a connection to the database
            {
                connection.Open();

                using (var command = new SqliteCommand("UPDATE Transactions SET Category = @Category WHERE TransactionID = @TransactionID", connection))
                {
                    command.Parameters.AddWithValue("@TransactionID", TransactionIDPara.TransactionID);
                    command.Parameters.AddWithValue("@Category", TransactionIDPara.Category);

                    var result = command.ExecuteNonQueryAsync();

                    var transaction = listOfTransactions.FirstOrDefault(t => t.TransactionID == TransactionIDPara.TransactionID);
                    if (transaction != null)
                    {
                        listOfTransactions.RemoveAll(t => t.TransactionID == TransactionIDPara.TransactionID);
                        listOfTransactions.Add(TransactionIDPara);
                        listOfTransactions = listOfTransactions.OrderBy(x => x.BookingDate).ToList();
                    }
                }
                using (var commandcount = new SqliteCommand("SELECT COUNT(*) FROM Rules", connection))
                {
                    var count = Convert.ToInt32(commandcount.ExecuteScalar());

                    Rule rule = new Rule()
                    {
                        rID = count++,
                        Reference = TransactionIDPara.Reference,
                        TransactionID = TransactionIDPara.TransactionID,
                        Category = TransactionIDPara.Category
                    };
                    InsertRuleToDatabase(rule);
                }
                connection.Close();

                return RedirectToAction("DisplayTransactions");
            }
        }
        [HttpPost]
        public ActionResult UseReferenceAsCatagory(Transaction? TransactionIDPara)
        {
            using (var connection = new SqliteConnection("Data Source=transactionDB.db")) // creates a connection to the database
            {
                connection.Open();
                using (var command = new SqliteCommand("UPDATE Transactions SET Category = @Category WHERE Reference = @Reference", connection))
                {
                    command.Parameters.AddWithValue("@Reference", TransactionIDPara.Reference);
                    command.Parameters.AddWithValue("@Category", TransactionIDPara.Category);

                    var result = command.ExecuteNonQueryAsync();

                    var transactions = listOfTransactions.Where(t => t.Reference == TransactionIDPara.Reference);
                    foreach (var transaction in transactions)
                    {
                        transaction.Category = TransactionIDPara.Category;
                    }
                    listOfTransactions = listOfTransactions.OrderBy(x => x.BookingDate).ToList();
                }

                using (var commandcount = new SqliteCommand("SELECT COUNT(*) FROM Rules", connection))
                {
                    var count = Convert.ToInt32(commandcount.ExecuteScalar());

                    Rule rule = new Rule()
                    {
                        rID = count++,
                        Reference = TransactionIDPara.Reference,
                        TransactionID = null,
                        Category = TransactionIDPara.Category

                    };
                    InsertRuleToDatabase(rule);
                }
                connection.Close();
                return RedirectToAction("DisplayTransactions");
            }
        }
        public ActionResult CreateDatabase(List<Transaction> TransactionPara)
        {
            //Tar emot en lista med transaktioner och skapar en databas med dessa transaktioner.
            using (var connection = new SqliteConnection("Data Source=transactionDB.db"))
            {
                connection.Open();
                using (var command = new SqliteCommand("SELECT COUNT(*) FROM Transactions", connection))
                {
                    var count = Convert.ToInt32(command.ExecuteScalar());

                    if (count == 0)
                    {
                        var commandText = "INSERT INTO Transactions (TransactionID, BookingDate, TransactionDate, Reference, Amount, Balance, Category) VALUES(@TransactionID, @BookingDate, @TransactionDate, @Reference, @Amount, @Balance, @Category)";

                        using (var transaction = connection.BeginTransaction())
                        {
                            using (var insertCommand = new SqliteCommand(commandText, connection, transaction))
                            {
                                var paramTransactionID = insertCommand.Parameters.Add("@TransactionID", SqliteType.Real);
                                var paramBookingDate = insertCommand.Parameters.Add("@BookingDate", SqliteType.Text);
                                var paramTransactionDate = insertCommand.Parameters.Add("@TransactionDate", SqliteType.Text);
                                var paramReference = insertCommand.Parameters.Add("@Reference", SqliteType.Text);
                                var paramAmount = insertCommand.Parameters.Add("@Amount", SqliteType.Real);
                                var paramBalance = insertCommand.Parameters.Add("@Balance", SqliteType.Real);
                                var paramCategory = insertCommand.Parameters.Add("@Category", SqliteType.Text);

                                foreach (var trans in TransactionPara)
                                {
                                    //Här hämtas alla Rules från db och lägger in först de 
                                    //som har matchande id sen matchande referens

                                    using (var command2 = new SqliteCommand("SELECT * FROM Rules", connection, transaction))
                                    {
                                        bool matched = false;

                                        using (var reader = command2.ExecuteReader())
                                        {
                                            while (reader.Read())
                                            {
                                                var ruleTransactionID = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                                                var ruleReference = reader.GetString(1);
                                                var ruleCategory = reader.GetString(3);

                                                if (trans.TransactionID == ruleTransactionID)
                                                {
                                                    paramCategory.Value = ruleCategory;
                                                    matched = true;
                                                    break;
                                                }
                                                else
                                                {
                                                    paramCategory.Value = trans.Category;
                                                }
                                            }
                                        }
                                        if (!matched)
                                        {
                                            using (var reader2 = command2.ExecuteReader())
                                            {
                                                while (reader2.Read())
                                                {
                                                    var ruleTransactionID = reader2.IsDBNull(2) ? (int?)null : reader2.GetInt32(2);
                                                    var ruleReference = reader2.GetString(1);
                                                    var ruleCategory = reader2.GetString(3);

                                                    if (trans.Reference == ruleReference && ruleTransactionID == null)
                                                    {
                                                        paramCategory.Value = ruleCategory;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        if (paramCategory.Value == null)
                                        {
                                            paramCategory.Value = trans.Category;
                                        }
                                        paramTransactionID.Value = trans.TransactionID;
                                        paramBookingDate.Value = trans.BookingDate;
                                        paramTransactionDate.Value = trans.TransactionDate;
                                        paramReference.Value = trans.Reference;
                                        paramAmount.Value = trans.Amount;
                                        paramBalance.Value = trans.Balance;


                                        insertCommand.ExecuteNonQuery();
                                    }
                                }
                                transaction.Commit();
                            }
                        }
                    }
                }
                connection.Close();
                return NotFound();
            }
        }
        public ActionResult InsertRuleToDatabase(Rule TransactionPara)
        {
            //Tar emot en lista med transaktioner och skapar en databas med dessa transaktioner.
            using (var connection = new SqliteConnection("Data Source=transactionDB.db"))
            {
                connection.Open();

                var commandText = "INSERT INTO Rules (rID, Reference, TransactionID, Category) VALUES(@rID, @Reference, @TransactionID, @Category)";
                using (var transaction = connection.BeginTransaction())
                {
                    using (var insertCommand = new SqliteCommand(commandText, connection, transaction))
                    {
                        var paramrID = insertCommand.Parameters.Add("@rID", SqliteType.Real);
                        var paramReference = insertCommand.Parameters.Add("@Reference", SqliteType.Text);
                        var paramTransactionID = insertCommand.Parameters.Add("@TransactionID", SqliteType.Real);
                        var paramCategory = insertCommand.Parameters.Add("@Category", SqliteType.Text);

                        paramrID.Value = TransactionPara.rID;
                        paramReference.Value = TransactionPara.Reference;
                        paramTransactionID.Value = TransactionPara.TransactionID == null ? (object)DBNull.Value : TransactionPara.TransactionID;
                        paramCategory.Value = TransactionPara.Category;

                        insertCommand.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
                connection.Close();
            }
            return NotFound();
        }
    }
}