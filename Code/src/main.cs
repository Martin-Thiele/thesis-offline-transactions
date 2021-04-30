// Template for http server found here:
// https://gist.github.com/define-private-public/d05bc52dd0bed1c4699d49e2737e80e7

using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

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
        public static int requestCount = 0;
        public static string pageData =
            "<html>" +
            "  <head>" +
            "  </head>" +
            "  <body>" +
            "    <p>Page Views: {0}</p>" +
            "    <form method=\"post\" action=\"shutdown\">" +
            "      <input type=\"submit\" value=\"Shutdown\" {1}>" +
            "    </form>" +
            "  </body>" +
            "</html>";


        public static IList<User> userList = new List<User>() {
            new User() { ID = 1, PhoneNumber = 12345678, Balance = 145, ActiveAgentEMoney = true, ActiveAgentCash = false, MaxAmountEMoney = 0, MaxAmountCash = 200, Pin = 1234} ,
            new User() { ID = 2, PhoneNumber = 23345678, Balance = 205, ActiveAgentEMoney = false, ActiveAgentCash = false, MaxAmountEMoney = 0, MaxAmountCash = 0,  Pin = 1234} ,
            new User() { ID = 3, PhoneNumber = 34345678, Balance = 250, ActiveAgentEMoney = false, ActiveAgentCash = false, MaxAmountEMoney = 0, MaxAmountCash = 0,  Pin = 1234} ,
            new User() { ID = 4, PhoneNumber = 45345678, Balance = 300, ActiveAgentEMoney = false, ActiveAgentCash = false, MaxAmountEMoney = 0, MaxAmountCash = 0,  Pin = 1234} ,
            new User() { ID = 5, PhoneNumber = 56345678, Balance = 350, ActiveAgentEMoney = false, ActiveAgentCash = false, MaxAmountEMoney = 0, MaxAmountCash = 0,  Pin = 1234}
        };

        public static IList<Transaction> transactionList = new List<Transaction>() {
            new Transaction() { ID = 1, From = 1, To = 2, Amount = (decimal) 5m, Fee = (decimal) 1.0m, Type = "Deposit", Status = "Complete", Complete_time = DateTime.Now} // User 1 has deposited 5 GNF through User 2. In return user 1 has received 4.95 E-GNF
        };

        int phonenumber = 0; // Phonenumber of incoming USSD message
        string session_id = ""; // unique id by USSD sandbox
        string service_code = ""; // USSD code by USSD sandbox
        string text = ""; // the text send by the user through the USSD sandbox



        private static string UssdHelp(){
            string ret =
            "139*1*2# for help regarding confirming transfers" +
            "139*1*3# for help regarding declining transfers" +
            "139*4# to see your balance" +
            "139*1*5# for help regarding your previous transactions" +
            "139*1*6# for help regarding transfers" +
            "139*1*7# for help regarding requesting money" +
            "139*1*8# for help regarding agent transfers" +
            "139*1*9# for help regarding requesting money as an agent" +
            "139*1*10# for help regarding becoming an agent providing e-money" +
            "139*1*11# for help regarding becoming an agent providing cash" +
            "139*1*12# for help regarding how to signup";
            return ret;
        }

        private static string Confirm(int PhoneNumber, int id, int pin){
            // Find user id
            var user = userList.Single(u => u.PhoneNumber == PhoneNumber);
            if(user == null){
                return "No user with that phonenumber found";
            }
            // Check if correct pin
            if(user.Pin != pin){
                return "Incorrect pin code";
            }

            // Fetch transaction
            try{
                Transaction transaction = transactionList.Single(t => t.From == user.ID && t.Status == "Pending" && t.ID == id);
                decimal toTransfer = transaction.Amount - transaction.Amount * transaction.Fee;
                if(user.Balance < toTransfer){
                    return "Invalid funds available to complete transfer";
                }


                // Do transfer
                // Not ACID-proof but for a concept it should be fine
                userList.Where(u => u.ID == transaction.From).SetValue(u => u.Balance = u.Balance - toTransfer);
                userList.Where(u => u.ID == transaction.To).SetValue(u => u.Balance = u.Balance + toTransfer);

                // Update order to complete
                transactionList.Where(t => t.ID == transaction.ID).SetValue(t => t.Status = "Complete").SetValue(t => t.Complete_time = DateTime.Now);

                return "Success";
            } catch(Exception e){
                Console.WriteLine(e);
                return "Operation failed";
            }

        }

        private static string Decline(int PhoneNumber, int id){
            var user = userList.Single(u => u.PhoneNumber == PhoneNumber);
            if(user == null){
                return "No user with that phonenumber found";
            }
            // Fetch transaction
            transactionList.Where(t => t.From == user.ID && t.Status == "Pending" && t.ID == id).SetValue(t => t.Status = "Declined").SetValue(t => t.Complete_time = DateTime.Now);
            return "";

        }

        private static decimal GetBalance(int PhoneNumber){
            var item = userList.Single(u => u.PhoneNumber == PhoneNumber);
            if(item != null){
                return item.Balance;
            } else{
                return 0m;
            }
        }

        private static (int, string, IList<Transaction>) ListTransactions(int PhoneNumber){
            try{
                var user = userList.Single(u => u.PhoneNumber == PhoneNumber);
                var ts = transactionList.Where(t => t.From == user.ID || t.To == user.ID).ToList();
                return (ts.Count, "", ts);
            } catch(Exception e){
                Console.WriteLine(e);
                return (-1, "No user with that phonenumber found", new List<Transaction>(){});
            }
        }

        private static (int, string) Transfer(int fromphone, int tophone, decimal amount, decimal fee, string type){
            var from = userList.Single(u => u.PhoneNumber == fromphone);
            if(from == null){
                return (-1, "Sender not found");
            }
            var to = userList.Single(u => u.PhoneNumber == tophone);
            if(to == null){
                return (-1, "Recipient not found");
            }

            int TCount = transactionList.Count;
            Transaction newT = new Transaction() {ID = TCount+1, From = from.ID, To = to.ID, Amount = amount, Fee = fee, Type = type, Status = "Pending", Complete_time = new DateTime()};
            try{
                transactionList.Insert(TCount, newT);
            } catch(Exception e){
                Console.WriteLine(e);
            }
            return (TCount+1, $"Please confirm or decline the transfer of {amount} GNF to {tophone}");
        }

        // The agent marks themselves as available to transfer cash
        private static int AgentStatusCash(int Phonenumber, decimal MaxAmount){
            return 0;
        }

        private static int AgentStatusEMoney(int Phonenumber, decimal MaxAmount){
            return 0;
        }

        private static IList<User> ListAgents(){
            return new List<User>(){};
        }

        private static int NewUser(int pin){
            int UserCount = userList.Count;
            User newU = new User() {ID = UserCount+1, Balance = 0, ActiveAgentEMoney = false, ActiveAgentCash = false, MaxAmountEMoney = 0, MaxAmountCash = 0, Pin = pin};
            userList.Insert(UserCount+1, newU);
            return 0;
        }

        private static void testTransfer(){
            Console.WriteLine(GetBalance(12345678));
            Console.WriteLine(GetBalance(23345678));
            var (id, msg) = Transfer(12345678, 23345678, 5m, 0m, "Transfer");
            var (id2, msg2) = Transfer(12345678, 23345678, 5m, 0m, "Transfer");
            var (id3, msg3) = Transfer(12345678, 23345678, 5m, 0.05m, "Deposit");
            var (id4, msg4) = Transfer(12345678, 23345678, 5m, 0m, "Transfer");
            Confirm(12345678, id3, 1234);
            Confirm(12345678, id, 1234);
            Decline(12345678, id2);
            Decline(12345678, id4);
            Console.WriteLine(GetBalance(12345678));
            Console.WriteLine(GetBalance(23345678));
        }

        public static async Task HandleIncomingConnections()
        {

            bool runServer = true;

            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
            {
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                // Print out some info about the request
                if (req.Url.AbsolutePath != "/favicon.ico"){
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
                    switch(req.Url.AbsolutePath)
                    {
                        case "/shutdown":
                            Console.WriteLine("Shutdown requested");
                            runServer = false;
                            break;
                        case "/USSD":
                            // decode USSD message and call function here
                            break;

                        default:
                            Console.WriteLine($"requested {req.Url.AbsolutePath} which doesn't exist");
                            break;

                    }
                }

                // Handle get requests
                if (req.HttpMethod == "GET"){
                    switch(req.Url.AbsolutePath){
                        case "/":
                        {

                            // Write the response info
                            string disableSubmit = !runServer ? "disabled" : "";
                            byte[] data = Encoding.UTF8.GetBytes(String.Format(pageData, pageViews, disableSubmit));
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
                            var (status, error, ts) = ListTransactions(12345678);
                            if(status != -1){
                                foreach (Transaction t in ts){
                                    Console.WriteLine($"ID: {t.ID}, From: {t.From}, To: {t.To}, Amount: {t.Amount}, Status: {t.Status}, completetime: {t.Complete_time}");
                                }
                            } else{
                                Console.WriteLine(error);
                            }
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


        public static void SimpleListenerExample(string prefix)
        {
            if (!HttpListener.IsSupported)
            {
                Console.WriteLine ("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
                return;
            }
            // URI prefixes are required,
            // for example "http://contoso.com:8080/index/".
            // Create a listener.
            HttpListener listener = new HttpListener();
            // Add the prefixes.
            listener.Prefixes.Add(prefix);
            listener.Start();
            Console.WriteLine("Listening...");
            // Note: The GetContext method blocks while waiting for a request.
            HttpListenerContext context = listener.GetContext();
            HttpListenerRequest request = context.Request;
            // Obtain a response object.
            HttpListenerResponse response = context.Response;
            // Construct a response.
            string responseString = "<HTML><BODY> Hello world!</BODY></HTML>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer,0,buffer.Length);
            // You must close the output stream.
            output.Close();
            listener.Stop();
        }

        public static void Main(string[] args)
        {
            if(port == null){
                Console.WriteLine("no port found");
                port = "5000";
            }
            string url = $"http://localhost:{port}/";
            Console.WriteLine(port);
            Console.WriteLine(url);
            // Create a Http server and start listening for incoming connections
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            // listener.Prefixes.Add($"http://kubank.herokuapp.com:{port}/");
            // listener.Prefixes.Add($"http://kubank.herokuapp.com/");
            listener.Start();
            Console.WriteLine("Listening for connections on {0}", url);

            // Handle requests
            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            // Close the listener
            listener.Close();


            // SimpleListenerExample(url);
        }
    }
}