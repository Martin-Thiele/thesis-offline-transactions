// Template for http server found here:
// https://gist.github.com/define-private-public/d05bc52dd0bed1c4699d49e2737e80e7

using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Mime;
using System.Collections.Generic;
using static System.Net.WebUtility;

namespace HttpListenerBank
{

    public class User
    {
        public int ID { get; set; }
        // public string Name { get; set; }
        public int PhoneNumber { get; set; }
        public decimal Balance { get; set; }
        public bool ActiveAgentEMoney {get; set;}
        public bool ActiveAgentCash {get; set;}
        public decimal EMoneyFee {get; set;}
        public decimal CashFee {get; set;}
        public decimal MaxAmountEMoney {get; set;}
        public decimal MaxAmountCash {get; set;}
        public int Pin {get; set;} // Encrypt this in a real scenario
    }

    public class Transaction
    {
        public int ID { get; set; }
        public int From { get; set; }
        public int To { get; set; }
        public decimal Amount { get; set; }
        public decimal Fee { get; set; } // percent
        public string Type { get; set; } // Transfer, Depoit, Withdrawal
        public string Status { get; set; } // 'Pending', 'Complete'
        public string Reason { get; set; }
        public DateTime Complete_time { get; set; }
    }

    // used for display purposes. When converting from and to to phone numbers and "you"
    public class DTransaction
    {
        public int ID { get; set; }
        public String From { get; set; }
        public String To { get; set; }
        public decimal Amount { get; set; }
        public decimal Fee { get; set; } // percent
        public string Type { get; set; } // Transfer, Depoit, Withdrawal
        public string Status { get; set; } // 'Pending', 'Complete'
        public string Reason { get; set; }
        public DateTime Complete_time { get; set; }
    }


    public static class LingExtension
    {
        public static IEnumerable<T> SetValue<T>(this IEnumerable<T> items, Action<T>updateMethod)
        {
            foreach (T item in items)
            {
                updateMethod(item);
            }
            return items;
        }
    }


    class HttpServer
    {
        public static HttpListener listener;
        public static string port = System.Environment.GetEnvironmentVariable("PORT");
        public static int pageViews = 0;
        public static bool showRequestInfo = false;
        public static int requestCount = 0;
        public static string pageData =
            "<html>" +
            "  <head>" +
            " <style> " +
            "   table {{border-collapse: collapse}}" +
            "   #tTable {{min-width: 900px;}}" +
            " table tr td {{border: solid 1px #C7C7C7;}}" +
            " </style>" +
            "  </head>" +
            "  <body>" +
            "    {0}" + // account table
            "    <br/><br/>"+
            "    {1}" + // transaction table
            "    <br/><br/><br/>" +
            "    <form method=\"post\" action=\"reset\">" +
            "      <input type=\"submit\" value=\"Reset\">" +
            "    </form>" +
            "    <form method=\"post\" action=\"donate\">" +
            "      <input type=\"submit\" value=\"donate 10 E-GNF to all users\">" +
            "    </form>" +

            "  </body>" +
            "</html>";


        public static List<User> userList = new List<User>() {
        };

        public static List<Transaction> transactionList = new List<Transaction>() {
        };

        private static void reset(){
            userList = new List<User>(){
                new User() { ID = 1, PhoneNumber = 12345678, Balance = 100.0m, ActiveAgentEMoney = false, ActiveAgentCash = false, MaxAmountEMoney = 0, CashFee = 0.1m, EMoneyFee = 0.1m, MaxAmountCash = 0, Pin = 1234} ,
                new User() { ID = 2, PhoneNumber = 87654321, Balance = 100.0m, ActiveAgentEMoney = true, ActiveAgentCash = true, MaxAmountEMoney = 200, CashFee = 0.1m, EMoneyFee = 0.1m, MaxAmountCash = 200,  Pin = 1234} ,
            };
            transactionList = new List<Transaction>() {
                // new Transaction() { ID = 1, From = 1, To = 2, Amount = (decimal) 5m, Fee = (decimal) 1.0m, Type = "Deposit", Status = "Complete", Reason = null, Complete_time = DateTime.Now } // User 1 has deposited 5 GNF through User 2. In return user 1 has received 4.95 E-GNF
           };
        }



        private static (int, string) Confirm(int PhoneNumber, int id, int pin){
            // Find user id
            var user = userList.SingleOrDefault(u => u.PhoneNumber == PhoneNumber);
            if(user == null){
                return (-1, "No user with that phonenumber found");
            }
            // Check if correct pin
            if(user.Pin != pin){
                return (-1, "Incorrect pin code");
            }

            // Fetch transaction
            try{
                Transaction transaction = transactionList.SingleOrDefault(t => t.From == user.ID && t.Status == "Pending" && t.ID == id);
                decimal toTransfer = 0m;
                if (transaction.Type == "Withdrawal"){
                    toTransfer = transaction.Amount;
                } else{
                    toTransfer = transaction.Amount - transaction.Amount * transaction.Fee;
                }
                if(user.Balance < toTransfer){
                    return (-1, "Invalid funds available to complete transfer");
                }


                // Do transfer
                // Not ACID-proof but for a concept it should be fine
                if (transaction.Type == "Withdrawal"){
                    // A user would like to withdraw 10 E-GNF worth cash. In order to do so he transfers 10 E-GNF and in return gets 10-10*fee GNF in cash
                    userList.Where(u => u.ID == transaction.From).SetValue(u => u.Balance = u.Balance - transaction.Amount);
                    userList.Where(u => u.ID == transaction.To).SetValue(u => u.Balance = u.Balance + transaction.Amount);
                } else{
                    userList.Where(u => u.ID == transaction.From).SetValue(u => u.Balance = u.Balance - toTransfer);
                    userList.Where(u => u.ID == transaction.To).SetValue(u => u.Balance = u.Balance + toTransfer);
                }

                // Update order to complete
                transactionList.Where(t => t.ID == transaction.ID).SetValue(t => t.Status = "Complete").SetValue(t => t.Complete_time = DateTime.Now);

                return (0, "Success");
            } catch(Exception e){
                Console.WriteLine(e);
                return (-1, "Operation failed");
            }

        }

        private static string Decline(int PhoneNumber, int id, int pin){
            var user = userList.SingleOrDefault(u => u.PhoneNumber == PhoneNumber);
            if(user == null){
                return "No user with that phonenumber found";
            }
            if(user.Pin != pin){
                return "Incorrect pin code";
            }
            // Fetch transaction
            transactionList.Where(t => t.From == user.ID && t.Status == "Pending" && t.ID == id).SetValue(t => t.Status = "Declined").SetValue(t => t.Complete_time = DateTime.Now);
            return "";

        }

        private static (int, decimal) GetBalance(int PhoneNumber){
            var user = userList.SingleOrDefault(u => u.PhoneNumber == PhoneNumber);
            if(user == null){
                return (-1, 0m);
            }
            else {
                return (0, user.Balance);
            }
        }

        private static (int, List<DTransaction>) ListTransactions(int PhoneNumber, int offset){
            try{
                var user = userList.SingleOrDefault(u => u.PhoneNumber == PhoneNumber);
                var ts = transactionList.Where(t => t.From == user.ID || t.To == user.ID).ToList();
                ts = ts.Skip(offset).Take(5).ToList();
                List<DTransaction> newts = new List<DTransaction>();
                Dictionary<int, string> dp = new Dictionary<int, string>(); // convert user ids to phone number
                foreach(var t in ts){
                    DTransaction newT = new DTransaction() {ID = t.ID, From = null, To = null, Amount = t.Amount, Fee = t.Fee, Type = t.Type, Status = t.Status, Reason=t.Reason, Complete_time = t.Complete_time};
                    // convert from ids to phone numbers
                    if (t.From == user.ID){
                        newT.From = "you";
                    } else{
                        if(dp.ContainsKey(t.From)){
                            newT.From = dp[t.From];
                        } else{
                            User u = userList.SingleOrDefault(u => u.ID == t.From);
                            if(u == null){
                                Console.WriteLine($"Tried converting ID to phonenumber for user ID {t.From} but it failed");
                            } else{
                                dp.Add(t.From, u.PhoneNumber.ToString());
                                newT.From = u.PhoneNumber.ToString();
                            }

                        }
                    }


                    // convert to ids to phone numbers
                    if (t.To == user.ID){
                        newT.To = "you";
                    } else{
                        if(dp.ContainsKey(t.To)){
                            newT.To = dp[t.To];
                        } else{
                            User u = userList.SingleOrDefault(u => u.ID == t.To);
                            if(u == null){
                                Console.WriteLine($"Tried converting ID to phonenumber for user ID {t.To} but it failed");
                            } else{
                                dp.Add(t.To, u.PhoneNumber.ToString());
                                newT.To = u.PhoneNumber.ToString();
                            }

                        }
                    }


                    newts.Add(newT);
                }
                return (newts.Count, newts);
            } catch(Exception e){
                Console.WriteLine(e);
                return (-1, null);
            }
        }

        private static (int, string) Transfer(int fromphone, int tophone, decimal amount, decimal fee, string type, string reason){
            var from = userList.SingleOrDefault(u => u.PhoneNumber == fromphone);
            if(from == null){
                return (-1, "Sender not found");
            }
            var to = userList.SingleOrDefault(u => u.PhoneNumber == tophone);
            if(to == null){
                return (-1, "Recipient not found");
            }
            if(tophone == fromphone){
                return (-1, "Can't send E-GNF to yourself");
            }

            int TCount = transactionList.Count;
            Transaction newT = new Transaction() {ID = TCount+1, From = from.ID, To = to.ID, Amount = amount, Fee = fee, Type = type, Status = "Pending", Reason=reason, Complete_time = DateTime.MinValue};
            try{
                transactionList.Add(newT);

                // Notify agent
                if (type == "Withdrawal"){
                    // Should be an SMS
                    Console.WriteLine($"SMS -> ({to.PhoneNumber}) - User {from.PhoneNumber} has requested {amount-amount*fee} GNF in exchange for {amount} E-GNF");
                } else if(type == "Deposit"){
                    Console.WriteLine($"SMS -> ({from.PhoneNumber}) - User {to.PhoneNumber} has requested {amount-amount*fee} E-GNF in exchange for {amount} GNF");
                } else if(type == "Request"){
                    var ret = $"SMS -> ({from.PhoneNumber}) - User {to.PhoneNumber} has requested {amount-amount*fee} E-GNF";
                    if(reason != null){
                        ret+=$". Reason: \" {reason} \"";
                    }
                    Console.WriteLine(ret);
                }
            } catch(Exception e){
                Console.WriteLine(e);
            }
            return (TCount+1, "");
        }

        // The agent marks themselves as available to transfer cash
        private static int AgentStatusCash(int Phonenumber, decimal MaxAmount){
            return 0;
        }

        private static int AgentStatusEMoney(int Phonenumber, decimal MaxAmount){
            return 0;
        }

        private static List<User> ListAgents(){
            return new List<User>(){};
        }

        private static int NewUser(int pin, int phone){
            try{
                var user = userList.SingleOrDefault(u => u.PhoneNumber == phone);
                if(user != null){
                    Console.WriteLine($"user ({phone}) already exists");
                    return -1;
                } else{
                    int UserCount = userList.Count;
                    User newU = new User() {ID = UserCount+1, PhoneNumber = phone, Balance = 0, ActiveAgentEMoney = false, ActiveAgentCash = false, MaxAmountEMoney = 0, MaxAmountCash = 0, Pin = pin};
                    userList.Add(newU);
                    return 0;
                }
            } catch(Exception e){
                Console.WriteLine(e);
                return -1;
            }
        }

        private static void testTransfer(){
            // Transfer
            bool testTransfer = true;
            bool testDeposit = true;
            bool testWithdraw = true;

            if(testTransfer){
                var (id, _) = Transfer(12345678, 87654321, 5m, 0.00m, "Transfer", "For dinner tonight :)");
                var (_, balgiver) = GetBalance(12345678);
                var (_, balrecipient) = GetBalance(87654321);
                Confirm(12345678, id, 1234);
                decimal expectedGiver = balgiver - 5m;
                decimal expectedRecipient = balrecipient + 5m;
                var (_, balgiver2) = GetBalance(12345678);
                var (_, balrecipient2) = GetBalance(87654321);
                if (balrecipient2 == expectedRecipient && balgiver2 == expectedGiver){
                    Console.WriteLine("Test (Transfer) - Passed");
                } else{
                    Console.WriteLine("Test (Transfer) - Failed");
                    Console.WriteLine($"Expected: {expectedRecipient} & {expectedGiver}");
                    Console.WriteLine($"Got {balrecipient2} & {balgiver2}");
                }
            }


            // Deposit
            if(testDeposit){
                decimal amount = 10m;
                decimal fee = 0.05m;
                var (b1, baluser) = GetBalance(12345678);
                var (b2, balagent) = GetBalance(87654321);
                var (id, _) = Transfer(87654321, 12345678, amount, fee, "Deposit", null);
                Confirm(87654321, id, 1234);
                var (b5, baldepositUser) = GetBalance(12345678);
                var (b6, baldepositAgent) = GetBalance(87654321);
                decimal expectedUser = baluser + (amount-(amount*fee));
                decimal expectedAgent = balagent - (amount-(amount*fee));
                if (baldepositAgent == expectedAgent && baldepositUser == expectedUser){
                    Console.WriteLine("Test (Deposit) - Passed");
                } else{
                    Console.WriteLine("Test (Deposit) - Failed");
                    Console.WriteLine($"Expected: {expectedAgent} & {expectedUser}");
                    Console.WriteLine($"Got {baldepositAgent} & {baldepositUser}");
                }
            }

            // Withdrawal
            if(testWithdraw){
                decimal amount = 10m;
                decimal fee = 0.05m;
                var (_, baluser) = GetBalance(12345678);
                var (_, balagent) = GetBalance(87654321);
                var (id, _) = Transfer(12345678, 87654321, amount, fee, "Withdrawal", null);
                Confirm(12345678, id, 1234);
                var (_, baldepositUser) = GetBalance(12345678);
                var (_, baldepositAgent) = GetBalance(87654321);
                decimal expectedUser = baluser - amount;
                decimal expectedAgent = balagent + amount;
                if (baldepositAgent == expectedAgent && baldepositUser == expectedUser){
                    Console.WriteLine("Test (Withdrawal) - Passed");
                } else{
                    Console.WriteLine("Test (Withdrawal) - Failed");
                    Console.WriteLine($"Expected: {expectedAgent} & {expectedUser}");
                    Console.WriteLine($"Got {baldepositAgent} & {baldepositUser}");
                }
            }
        }

        private static string formatUserHtml(){
            string ret = "<h1>Users</h1><table id='uTable'><thead><tr><td><b>ID</b></td><td><b>Phone number</b></td><td><b>Balance</b></td></tr></thead><tbody>";
            foreach (var user in userList){
                ret+=$"<tr><td>{user.ID}</td><td>{user.PhoneNumber}</td><td>{user.Balance}</td></tr>";
            }
            ret+="</tbody></table>";
            return ret;
        }
        private static string formatTransactionHtml(){
            string ret = "<h1>Transactions</h1><table id='tTable'><thead><tr>" +

            "<td><b>ID</b></td>" +
            "<td><b>From</b></td>" +
            "<td><b>To</b></td>"+
            "<td><b>Amount</b></td>"+
            "<td><b>Fee</b></td>"+
            "<td><b>Sent</b></td>"+
            "<td><b>Received</b></td>"+
            "<td><b>Reason</b></td>"+
            "<td><b>Type</b></td>"+
            "<td><b>Status</b></td>"+
            "<td><b>Complete time</b></td>"+

            "</tr></thead><tbody>";
            foreach (var t in transactionList){
                decimal sent = t.Amount - t.Amount*t.Fee;
                decimal received = 0.00m;
                string sentstr = "";
                string recstr = "";
                if(t.Type == "Deposit"){
                    sentstr = $"{(t.Amount - (t.Amount*t.Fee))} E-GNF";
                    recstr = $"{t.Amount} GNF";
                } else if(t.Type == "Withdrawal"){
                    sentstr = $"{t.Amount} E-GNF";
                    recstr = $"{(t.Amount - (t.Amount*t.Fee))} GNF";
                } else{
                    sentstr = $"{sent} E-GNF";
                    recstr = $"{received} GNF";
                }

                ret+=$"<tr>"+

                $"<td>{t.ID}</td>"+
                $"<td>{t.From}</td>" +
                $"<td>{t.To}</td>" +
                $"<td>{t.Amount}</td>" +
                $"<td>{t.Fee}</td>" +
                $"<td>{sentstr}</td>" +
                $"<td>{recstr}</td>" +
                $"<td>{t.Reason}</td>" +
                $"<td>{t.Type}</td>" +
                $"<td>{t.Status}</td>" +
                $"<td>{t.Complete_time}</td>" +

                "</tr>";
            }
            ret+="</tbody></table>";
            return ret;
        }















        // USD
        public static string handleUSSD(Dictionary<String, String> data){

            string reqText = "";
            if(data.ContainsKey("text")){
                reqText = data["text"];
            } else if(data.ContainsKey("input")){
                reqText = data["input"];
            } else{
                return "END no form data could be found";
            }
            int reqPhone = int.Parse(data["phoneNumber"].Remove(0,3)); // remove +45 for danish numbers. Good enough for proof of concept
            string[] sections = reqText.Split('*');
            string section = sections[0];
            int reqlen = sections.Length;
            switch(section){
                case "":
                    string response = "CON "+
                         "1 - Help \n"+
                         "2 - Check balance \n"+
                         "3 - List transaction \n" +
                         "4 - Transfer money \n"+
                         "5 - Request money \n"+
                         "6 - Deposit money \n"+
                         "7 - Withdraw money \n"+
                         "8 - Confirm transfer \n"+
                         "9 - Decline transfer \n"+
                         "10 - Deliver e-money \n"+
                         "11 - Deliver cash \n"+
                         "12 - Sign up";
                    return response;
                case "1": // help
                    break;
                case "2": // balance
                {

                    var (err, bal) = GetBalance(reqPhone);
                    if(err == 0){
                        return $"END Your balance is: {bal.ToString()} E-GNF";
                    } else{
                        return $"END Something went wrong. Have you signed up?";
                    }
                }
                case "3": // list transactions
                    if(reqlen == 1){
                        return "CON please enter the order offset. 0 if you would like to see the 5 latest transactions. 5 if you'd like to see the 5 afterwards, 10 for the following 5 and so on.";
                    } else{
                        var (amount, ts) = ListTransactions(reqPhone, int.Parse(sections[1]));
                        if(amount == -1){
                            return $"END Something went wrong. Have you signed up?";
                        } else{
                            if(amount == 0){
                                return "END No transactions found";
                            } else{
                                string ret = "END "; //$"END {"From - To - Amount - Fee - Type"} \n";
                                foreach(var t in ts){
                                    if(t.Complete_time != DateTime.MinValue){
                                        ret+=$"{t.Complete_time.ToString("dd-MM")} : ";
                                    }
                                    ret+=$"{t.From} -> {t.To}. {t.Amount} GNF ({t.Type}) [{t.Status}]"; // 02-05 : you -> 12345678. 1.23 GNF (Transfer) [Completed] "Cinema"
                                    if(t.Reason != null){
                                        ret+=$" \"{t.Reason}\"";
                                    }
                                }
                                return ret;
                            }
                        }
                    }
                case "4": // transfer
                    switch(reqlen){
                        case 1:
                            return "CON please enter the phone number of the recipient";
                        case 2:
                            return $"CON please enter the amount of E-GNF you'd like to send to {sections[1]}";
                        case 3:
                            return $"CON please enter the reason for the transfer. e.g. dinner or bill. Enter 0 if you'd like to not provide a reason";
                        case 4:
                            return $"CON please complete your transfer of {sections[2]} E-GNF to {sections[1]} by entering your PIN. Enter 0 to cancel";
                        case 5:
                            if(sections[4] == "0"){
                                return $"END you've declined the transfer of {sections[2]} E-GNF to {sections[1]}";
                            } else{
                                int phone;
                                decimal amount;
                                int pin;
                                string reason = sections[3];
                                bool successPh = int.TryParse(sections[1], out phone);
                                bool successAm = decimal.TryParse(sections[2], out amount);
                                bool successPin = int.TryParse(sections[4], out pin);
                                if(!successPh){
                                    return $"END {sections[1]} is not a valid phone number";
                                }
                                if(!successAm){
                                    return $"END {sections[2]} is not a valid amount";
                                }
                                if(!successPin){
                                    return $"END {sections[4]} is not a valid PIN";
                                }
                                var (id, msg) = Transfer(reqPhone, phone, amount, 0m, "Transfer", reason != "0" ? reason : null);
                                if(id == -1){
                                    transactionList.RemoveAt(id);
                                    return $"END {msg}";
                                } else{
                                    var (err, msg2) = Confirm(reqPhone, id, pin);
                                    if(err == -1){
                                        return $"END {msg2}";
                                    }
                                }
                                return $"END you've completed the transfer of {sections[2]} E-GNF to {sections[1]}";
                            }
                        default:
                            break;
                    }
                    break;
                case "5": // request
                    switch(reqlen){
                        case 1:
                            return "CON please enter the phone number of the person you'd like to request money from";
                        case 2:
                            return $"CON please enter the amount of E-GNF you'd like to request from {sections[1]}";
                        case 3:
                            return $"CON please enter the reason for the transfer. e.g. dinner or bill. Enter 0 if you'd like to not provide a reason";
                        case 4:
                            int phone;
                            decimal amount;
                            bool successPh = int.TryParse(sections[1], out phone);
                            bool successAm = decimal.TryParse(sections[2], out amount);
                            string reason = sections[3];
                            if(!successPh){
                                return $"END {sections[1]} is not a valid phone number";
                            }
                            if(!successAm){
                                return $"END {sections[2]} is not a valid amount";
                            }
                            var (id, msg) = Transfer(phone, reqPhone, amount, 0m, "Request", reason != "0" ? reason : null);
                            if(id == -1){
                                    transactionList.RemoveAt(id);
                                    return $"END {msg}";
                            } else{
                                return $"END you've requested {sections[2]} E-GNF from {sections[1]}";
                            }
                    }
                    break;
                case "6": // deposit
                    break;
                case "7": // withdraw
                    break;
                case "8": // confirm
                    break;
                case "9": // decline
                    break;
                case "10": // Mark agent as active for e-money
                    break;
                case "11": // Mark agent as active for cash
                    break;
                case "12": // Signup
                    if(reqlen == 1){
                        return "CON please enter a safe PIN number greater than 3 characters";
                    } else{
                        if(sections[1].Length < 4){
                            return $"END your PIN was too short. Please start over";
                        }
                        int err = NewUser(int.Parse(sections[1]), reqPhone);
                        if(err == 0){
                            return $"END you've signed up to the service. Your PIN is {sections[1]}";
                        } else{
                            return $"END you couldn't be registered. Perhaps you're already a user?";
                        }
                    }
                default:
                    break;
            }
            return $"END invalid input: {section}";
        }







        // Android support
        public static string handleAndroid(Dictionary<String, String> data){
            // TODO: See if you can rewrite ussd to also handle android first
            return "";
        }










        // Run server
        public static async Task HandleIncomingConnections()
        {

            bool runServer = true;

            while (runServer)
            {
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;
                resp.ContentType = MediaTypeNames.Text.Plain;
                resp.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                resp.AddHeader("Access-Control-Allow-Methods", "GET,POST");
                resp.AddHeader("Access-Control-Allow-Origin", "*");

                // Print out some info about the request
                if (req.Url.AbsolutePath != "/favicon.ico" && showRequestInfo){
                    Console.WriteLine("Request #: {0}", ++requestCount);
                    Console.WriteLine(req.Url.ToString());
                    Console.WriteLine(req.HttpMethod);
                    Console.WriteLine();
                }

                // Make sure we don't increment the page views counter if `favicon.ico` is requested
                if (req.Url.AbsolutePath != "/favicon.ico")
                    pageViews += 1;

                // Handle post requests
                if (req.HttpMethod == "POST")
                {
                    switch(req.Url.AbsolutePath){
                        case "/reset":
                        {
                                Console.WriteLine("Reset accounts and transactions");
                                reset();
                                string response = "Reset accounts and transactions";
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                        }
                        break;
                        case "/donate":
                        {
                            Console.WriteLine("funded all accounts 10 E-GNF");
                            userList.SetValue(u => u.Balance = u.Balance+10);
                            string response = "All account balances have increased 10 E-GNF";
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                        case "/ussd":
                        {
                            if (!req.HasEntityBody)
                            {
                                Console.WriteLine("No form data was found");
                                string response = "Please provide data when making a request";
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            } else{
                                // get post data
                                Dictionary<string, string> postParams = new Dictionary<string, string>();
                                Stream body = req.InputStream;
                                Encoding encoding = req.ContentEncoding;
                                StreamReader reader = new System.IO.StreamReader(body, encoding);
                                string rawData = reader.ReadToEnd();
                                string[] rawParams = rawData.Split('&');
                                foreach (string param in rawParams)
                                {
                                    string[] kvPair = param.Split('=');
                                    string key = kvPair[0];
                                    string value = UrlDecode(kvPair[1]);
                                    // Console.WriteLine($"{key}: {value}");
                                    postParams.Add(key, value);
                                }

                                // handle ussd codes
                                string response = handleUSSD(postParams);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            }
                        }
                        break;
                        case "/android":
                        {
                            if (!req.HasEntityBody)
                            {
                                Console.WriteLine("No form data was found");
                                string response = "Please provide data when making a request";
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            } else{
                                // get post data
                                Dictionary<string, string> postParams = new Dictionary<string, string>();
                                Stream body = req.InputStream;
                                Encoding encoding = req.ContentEncoding;
                                StreamReader reader = new System.IO.StreamReader(body, encoding);
                                string rawData = reader.ReadToEnd();
                                string[] rawParams = rawData.Split('&');
                                foreach (string param in rawParams)
                                {
                                    string[] kvPair = param.Split('=');
                                    string key = kvPair[0];
                                    string value = UrlDecode(kvPair[1]);
                                    // Console.WriteLine($"{key}: {value}");
                                    postParams.Add(key, value);
                                }

                                // handle ussd codes
                                string response = handleAndroid(postParams);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            }                        }
                        break;
                        default:
                            {
                                Console.WriteLine($"requested {req.Url.AbsolutePath} which doesn't exist");
                            }
                        break;

                    }
                }

                // Handle get requests
                if (req.HttpMethod == "GET"){
                    switch(req.Url.AbsolutePath){
                        case "/":
                        {

                            // Write the response info
                            byte[] data = Encoding.UTF8.GetBytes(String.Format(pageData, formatUserHtml(), formatTransactionHtml()));
                            resp.ContentType = "text/html";
                            resp.ContentEncoding = Encoding.UTF8;
                            resp.ContentLength64 = data.LongLength;

                            // Write out to the response stream (asynchronously), then close it
                            await resp.OutputStream.WriteAsync(data, 0, data.Length);
                            resp.Close();
                        }
                        break;
                        case "/test":
                        {
                            testTransfer();
                            string response = "Running tests";
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                        default:
                        {

                            string response = "Path not found";
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                    }
                }

            }
        }


        public static void Main(string[] args)
        {
            reset(); // reset bank and transactions
            if(port == null){
                Console.WriteLine("no port found");
                port = "5000";
            }
            string url = $"http://localhost:{port}/";
            // Create a Http server and start listening for incoming connections
            listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{port}/");
            listener.Start();
            Console.WriteLine("Listening for connections on {0}", url);

            // Handle requests
            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            // Close the listener
            listener.Close();


        }
    }
}