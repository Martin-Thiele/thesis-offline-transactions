using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Mime;
using System.Collections.Generic;
using static System.Net.WebUtility;
using System.Text.Json;
using BankTypes;

namespace HttpListenerBank
{

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
            "   table {{min-width: 1000px;}}" +
            " table tr td {{border: solid 1px #C7C7C7;}}" +
            " </style>" +
            "  </head>" +
            "  <body>" +
            "    {0}" + // account table
            "    <br/><br/>"+
            "    {1}" + // transaction table
            "    <br/><br/>"+
            "    {2}" + // question table
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

        public static List<Question> questionList = new List<Question>() {
        };

        public static List<Transaction> transactionList = new List<Transaction>() {
        };

        public static Dictionary<string, int> usersLoggedIn = new Dictionary<string, int>(){};


        private static void reset(){
            userList = new List<User>(){
//                new User() { ID = 1, PhoneNumber = 12345678, Balance = 100.0m, ActiveAgentEMoney = true, ActiveAgentCash = true, MaxAmountEMoney = 20, CashFee = 0.05m, EMoneyFee = 0.05m, MaxAmountCash = 25, Pin = 1234} ,
//                new User() { ID = 2, PhoneNumber = 87654321, Balance = 100.0m, ActiveAgentEMoney = true, ActiveAgentCash = true, MaxAmountEMoney = 200, CashFee = 0.1m, EMoneyFee = 0.1m, MaxAmountCash = 200,  Pin = 1234} ,
            };
            transactionList = new List<Transaction>() {
                // new Transaction() { ID = 1, From = 1, To = 2, Amount = (decimal) 5m, Fee = (decimal) 1.0m, Type = "Deposit", Status = "Complete", Reason = null, Complete_time = DateTime.Now } // User 1 has deposited 5 GNF through User 2. In return user 1 has received 4.95 E-GNF
           };

           questionList = new List<Question>(){};

           usersLoggedIn = new Dictionary<string, int>(){};
        }


        // token -> phone number


        private static (int, string) Confirm(int PhoneNumber, int id, int pin){
            // Find user id
            User user = userList.SingleOrDefault(u => u.PhoneNumber == PhoneNumber);
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

                // Notify sender and recipient that they have confirmed the transaction
                User from = userList.SingleOrDefault(u => u.PhoneNumber == transaction.From);
                User to = userList.SingleOrDefault(u => u.PhoneNumber == transaction.To);
                if(from != null && to != null){
                    var retSender = "";
                    var retRecipient = "";

                    if(transaction.Type == "Transfer"){
                        // Send to sender
                        retSender = $"SMS -> ({from.PhoneNumber}) You have sent {transaction.Amount} E-GNF to {to.PhoneNumber}";
                        if(transaction.Reason != null){
                            retSender+=$". \" {transaction.Reason} \"";
                        }

                        // Send to recipient
                        retRecipient = $"SMS -> ({to.PhoneNumber}) You have received {transaction.Amount} E-GNF from {from.PhoneNumber}";
                        if(transaction.Reason != null){
                            retRecipient+=$". \" {transaction.Reason} \"";
                        }
                    }
                    else if(transaction.Type == "Request"){
                        // Send to sender
                        retSender = $"SMS -> ({from.PhoneNumber}) You have sent the requested {transaction.Amount} E-GNF to {to.PhoneNumber}";
                        if(transaction.Reason != null){
                            retSender+=$". \" {transaction.Reason} \"";
                        }

                        // Send to recipient
                        retRecipient = $"SMS -> ({to.PhoneNumber}) You have received your requested {transaction.Amount} E-GNF from {from.PhoneNumber}";
                        if(transaction.Reason != null){
                            retRecipient+=$". \" {transaction.Reason} \"";
                        }
                    }
                    else if (transaction.Type == "Deposit"){
                        // Send to sender
                        retSender = $"SMS -> ({from.PhoneNumber}) You have delivered {transaction.Amount - (transaction.Amount*transaction.Fee)} E-GNF to {to.PhoneNumber} in exchange for {transaction.Amount} GNF";
                        // Send to recipient
                        retRecipient = $"SMS -> ({to.PhoneNumber}) You have deposited {transaction.Amount} GNF through {from.PhoneNumber}. Your account balance has increased by {transaction.Amount - (transaction.Amount*transaction.Fee)} E-GNF";
                    } else if(transaction.Type == "Withdrawal"){
                        // Send to sender (in this case, the sender is the user, the recipient is the agent)
                        retSender = $"SMS -> ({from.PhoneNumber}) You have withdrawed {transaction.Amount - (transaction.Amount*transaction.Fee)} GNF through {to.PhoneNumber} in exchange for {transaction.Amount} E-GNF";
                        // Send to recipient
                        retRecipient = $"SMS -> ({to.PhoneNumber}) You have delivered {transaction.Amount - (transaction.Amount*transaction.Fee)} GNF to {to.PhoneNumber} in exchange for {transaction.Amount} E-GNF";
                    }
                    Console.WriteLine(retSender);
                    Console.WriteLine(retRecipient);
                }



                return (0, "Success");
            } catch(Exception e){
                Console.WriteLine(e);
                return (-1, "Operation failed");
            }

        }

        private static (int, string) Decline(int PhoneNumber, int id){
            User user = userList.SingleOrDefault(u => u.PhoneNumber == PhoneNumber);
            if(user == null){
                return (-1, "No user with that phonenumber found");
            }
            // Fetch transaction
            transactionList.Where(t => t.From == user.ID && t.Status == "Pending" && t.ID == id).SetValue(t => t.Complete_time = DateTime.Now).SetValue(t => t.Status = "Declined");
            return (0, "");

        }

        private static (int, decimal) GetBalance(int PhoneNumber){
            User user = userList.SingleOrDefault(u => u.PhoneNumber == PhoneNumber);
            if(user == null){
                return (-1, 0m);
            }
            else {
                return (0, user.Balance);
            }
        }

        private static (int, List<DTransaction>) ListTransactions(int PhoneNumber, int offset, int amount){
            try{
                User user = userList.SingleOrDefault(u => u.PhoneNumber == PhoneNumber);
                List<Transaction> ts = transactionList.Where(t => t.From == user.ID || t.To == user.ID).OrderByDescending(t => t.Status == "Pending").ThenByDescending(t => t.Complete_time).ToList();
                ts = ts.Skip(offset).Take(amount).ToList();
                List<DTransaction> newts = new List<DTransaction>();
                Dictionary<int, string> dp = new Dictionary<int, string>(); // convert user ids to phone number
                foreach(Transaction t in ts){
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
            User from = userList.SingleOrDefault(u => u.PhoneNumber == fromphone);
            if(from == null){
                return (-1, "Sender not found");
            }
            User to = userList.SingleOrDefault(u => u.PhoneNumber == tophone);
            if(to == null){
                return (-1, "Recipient does not exist");
            }
            if(tophone == fromphone){
                return (-1, "Can't send E-GNF to yourself");
            }
            if(amount < 0.0m){
                return (-1, "Amount can't be negative");
            }
            if(amount < 0.1m){
                return (-1, "Amount can't be less than 0.1 E-GNF");
            }
            if(fee < 0.0m){
                return (-1, "Fee can't be negative");
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
                    string ret = $"SMS -> ({from.PhoneNumber}) - User {to.PhoneNumber} has requested {amount-amount*fee} E-GNF";
                    if(reason != null && reason != ""){
                        ret+=$". \" {reason} \"";
                    }
                    Console.WriteLine(ret);
                }
            } catch(Exception e){
                Console.WriteLine(e);
            }
            return (TCount+1, "");
        }

        // The agent marks themselves as available to transfer cash
        private static (int, string) SetAgentStatusCash(int Phonenumber, decimal MaxAmount, decimal fee){
            if(MaxAmount < 0.0m){
                return (-1, "Amount can't be negative");
            }
            if(fee < 0.0m){
                return (-1, "Fee can't be less than 0");
            }
            if(fee > 100.0m){
                return (-1, "Fee can't be greater than 100");
            }
            User user = userList.SingleOrDefault(u => u.PhoneNumber == Phonenumber);
            if(user == null){
                return (-1, "User not found");
            }

            userList.Where(u => u.ID == user.ID)
                .SetValue(u => u.ActiveAgentCash = !u.ActiveAgentCash)
                .SetValue(u => u.CashFee = fee/100.0m)
                .SetValue(u => u.MaxAmountCash = MaxAmount);
            return (0, "");
        }

        private static (int, string) SetAgentStatusEMoney(int Phonenumber, decimal MaxAmount, decimal fee){
            User user = userList.SingleOrDefault(u => u.PhoneNumber == Phonenumber);
            if(user == null){
                return (-1, "User not found");
            }

            userList.Where(u => u.ID == user.ID)
                .SetValue(u => u.ActiveAgentEMoney = !u.ActiveAgentEMoney)
                .SetValue(u => u.EMoneyFee = fee/100.0m)
                .SetValue(u => u.MaxAmountEMoney = MaxAmount);
            return (0, "");
        }

        private static int NewUser(int pin, int phone){
            try{
                User user = userList.SingleOrDefault(u => u.PhoneNumber == phone);
                if(user != null){
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

        private static (int, string) newQuestion(int phone, string question){

            User user = userList.SingleOrDefault(u => u.PhoneNumber == phone);
            if(user == null){
                return (-1, "Your phone number could not be found. Are you registered?");
            }
            int qCount = questionList.Count;
            Question newQ = new Question() {ID = qCount+1, From = user.ID, Q = question, Asked_time = DateTime.Now};
            questionList.Add(newQ);
            return (0, "Success");
        }

        private static void testTransfer(){
            // Transfer
            bool testTransfer = true;
            bool testRequest = true;
            bool testDeposit = true;
            bool testWithdraw = true;
            bool testDecline = true;

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

            // Request
            if(testRequest){
                var (id, _) = Transfer(87654321, 12345678, 5m, 0.00m, "Request", "For dinner tonight :)");
                var (_, balrecipient) = GetBalance(12345678);
                var (_, balgiver) = GetBalance(87654321);
                Confirm(87654321, id, 1234);
                decimal expectedGiver = balgiver - 5m;
                decimal expectedRecipient = balrecipient + 5m;
                var (_, balgiver2) = GetBalance(87654321);
                var (_, balrecipient2) = GetBalance(12345678);
                if (balrecipient2 == expectedRecipient && balgiver2 == expectedGiver){
                    Console.WriteLine("Test (Request) - Passed");
                } else{
                    Console.WriteLine("Test (Request) - Failed");
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

            // Decline
            if(testDecline){
                decimal amount = 10m;
                decimal fee = 0m;
                var (_, baluser) = GetBalance(12345678);
                var (_, balagent) = GetBalance(87654321);
                var (id, _) = Transfer(12345678, 87654321, amount, fee, "Transfer", "yay");
                Decline(12345678, id);
                var (_, baldepositUser) = GetBalance(12345678);
                var (_, baldepositAgent) = GetBalance(87654321);
                decimal expectedUser = baluser;
                decimal expectedAgent = balagent;
                if (baldepositAgent == expectedAgent && baldepositUser == expectedUser){
                    Console.WriteLine("Test (Decline) - Passed");
                } else{
                    Console.WriteLine("Test (Decline) - Failed");
                    Console.WriteLine($"Expected: {expectedAgent} & {expectedUser}");
                    Console.WriteLine($"Got {baldepositAgent} & {baldepositUser}");
                }
            }
        }

        private static string formatUserHtml(){
            string ret = "<h1>Users</h1>"+
            "<table id='uTable'>"+
            "    <thead>"+
            "        <tr>"+
            "            <td><b>ID</b></td>"+
            "            <td><b>Phone number</b></td>"+
            "            <td><b>Balance</b></td>"+
            "            <td><b>Agent (cash)</b></td>"+
            "            <td><b>Agent fee (cash)</b></td>"+
            "            <td><b>Max amount (cash)</b></td>"+
            "            <td><b>Agent (E-money)</b></td>"+
            "            <td><b>Agent fee (E-money)</b></td>"+
            "            <td><b>Max amount (E-money)</b></td>"+
            "        </tr>"+
            "        </thead>"+
            "<tbody>";
            foreach (User user in userList){
                ret+=$"<tr>"+
                    $"<td>{user.ID}</td>"+
                    $"<td>{user.PhoneNumber}</td>"+
                    $"<td>{user.Balance}</td>"+
                    $"<td>{user.ActiveAgentCash}</td>"+
                    $"<td>{user.CashFee}</td>"+
                    $"<td>{user.MaxAmountCash}</td>"+
                    $"<td>{user.ActiveAgentEMoney}</td>"+
                    $"<td>{user.EMoneyFee}</td>"+
                    $"<td>{user.MaxAmountEMoney}</td>"+
                    "</tr>";
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
            foreach (Transaction t in transactionList){
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

        private static string formatQuestionHtml(){
            string ret = "<h1>Questions</h1><table id='tTable'><thead><tr>" +

            "<td><b>ID</b></td>" +
            "<td><b>From</b></td>" +
            "<td><b>Question</b></td>"+
            "<td><b>When asked</b></td>"+

            "</tr></thead><tbody>";
            foreach (Question q in questionList){
                ret+=$"<tr>"+

                $"<td>{q.ID}</td>"+
                $"<td>{q.From}</td>" +
                $"<td>{q.Q}</td>" +
                $"<td>{q.Asked_time}</td>" +

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
                return "END No form data could be found";
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
                         "3 - List transactions \n" +
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
                    if(reqlen == 1){
                        return "CON enter a question and we'll get back to you. If you'd like for us to call you, leave this space empty";
                    } else{
                        var (err, msg) = newQuestion(reqPhone, sections[1]);
                        if(err == -1) {
                            return $"END {msg}";
                        } else{
                            return $"END Your question has been sent. You'll get a response shortly";
                        }
                    }
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
                        User user = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                        if(user == null){
                            return "END Your phone number could not be found. Are you registered?";
                        }
                        return "CON Please enter the order offset. 0 if you would like to see the 5 latest transactions. 5 if you'd like to see the 5 afterwards, 10 for the following 5 and so on.";
                    } else{
                        var (amount, ts) = ListTransactions(reqPhone, int.Parse(sections[1]), 5);
                        if(amount == -1){
                            return $"END Something went wrong. Have you signed up?";
                        } else{
                            if(amount == 0){
                                return "END No transactions found";
                            } else{
                                string ret = "END "; //$"END {"From - To - Amount - Fee - Type"} \n";
                                foreach(DTransaction t in ts){
                                    if(t.Complete_time != DateTime.MinValue){
                                        ret+=$"{t.Complete_time.ToString("dd-MM")} : ";
                                    }
                                    if(t.Type == "deposit"){
                                        ret+=$"{t.From} -> {t.To}. {t.Amount - t.Amount*t.Fee} E-GNF ({t.Type}) [{t.Status}]"; // 02-05 : you -> 12345678. 1.23 GNF (Transfer) [Completed] "Cinema"
                                    } else{
                                        ret+=$"{t.From} -> {t.To}. {t.Amount} E-GNF ({t.Type}) [{t.Status}]"; // 02-05 : you -> 12345678. 1.23 GNF (Transfer) [Completed] "Cinema"
                                    }
                                    if(t.Reason != null && t.Reason != ""){
                                        ret+=$" \"{t.Reason}\"";
                                    }
                                    ret+="\n";
                                }
                                return ret;
                            }
                        }
                    }
                case "4": // transfer
                    switch(reqlen){
                        case 1:
                            User user = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                            if(user == null){
                                return "END Your phone number could not be found. Are you registered?";
                            }
                            return "CON Please enter the phone number of the recipient";
                        case 2:
                            return $"CON Please enter the amount of E-GNF you'd like to send to {sections[1]}";
                        case 3:
                            return $"CON Please enter the reason for the transfer. e.g. dinner or bill. Enter 0 if you'd like to not provide a reason";
                        case 4:
                            return $"CON Please complete your transfer of {sections[2]} E-GNF to {sections[1]} by entering your PIN. Enter 0 to cancel";
                        case 5:
                            if(sections[4] == "0"){
                                return $"END You've declined the transfer of {sections[2]} E-GNF to {sections[1]}";
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
                                    return $"END {msg}";
                                } else{
                                    var (err, msg2) = Confirm(reqPhone, id, pin);
                                    if(err == -1){
                                        return $"END {msg2}";
                                    }
                                }
                                return $"END You've completed the transfer of {sections[2]} E-GNF to {sections[1]}";
                            }
                        default:
                            break;
                    }
                    break;
                case "5": // request
                    switch(reqlen){
                        case 1:
                            User user = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                            if(user == null){
                                return "END Your phone number could not be found. Are you registered?";
                            }
                            return "CON Please enter the phone number of the person you'd like to request money from";
                        case 2:
                            return $"CON Please enter the amount of E-GNF you'd like to request from {sections[1]}";
                        case 3:
                            return $"CON Please enter the reason for the transfer. e.g. dinner or bill. Enter 0 if you'd like to not provide a reason";
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
                            var (id, msg) = Transfer(phone, reqPhone, amount, 0m, "Request", (reason != "0" && reason != "") ? reason : null);
                            if(id == -1){
                                    return $"END {msg}";
                            } else{
                                return $"END You've requested {sections[2]} E-GNF from {sections[1]}";
                            }
                    }
                    break;
                case "6": // Deposit
                    switch(reqlen){
                        case 1:
                        {
                            User user = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                            if(user == null){
                                return "END Your phone number could not be found. Are you registered?";
                            }
                            return "CON Please enter the amount of GNF you'd like to deposit";
                        }
                        case 2:
                        {
                            decimal amount;
                            bool success = decimal.TryParse(sections[1], out amount);
                            if(!success){
                                return "END The amount you entered was invalid. Please start over";
                            }
                            var agents = userList.Where(u => u.ActiveAgentEMoney && amount <= u.MaxAmountEMoney && u.PhoneNumber != reqPhone).Select(u => new {u.ID, u.PhoneNumber, u.EMoneyFee, u.MaxAmountEMoney}).OrderBy(u => u.EMoneyFee).ToList();
                            if(agents.Count > 0){
                                string ret = "CON Please choose an agent by their phone number\nFee - Phone - You will receive\n";
                                foreach(var a in agents){
                                    ret+=$"{a.EMoneyFee*100m}% - {a.PhoneNumber} - {amount - (amount*a.EMoneyFee)} E-GNF\n";
                                }
                                return ret;
                            } else{
                                return $"END No agents delivering E-GNF could be found within the amount of {amount}";
                            }
                        }
                        case 3:
                        {
                            decimal amount = decimal.Parse(sections[1]);
                            int agentPhone = int.Parse(sections[2]);
                            User agent = userList.SingleOrDefault(u => u.PhoneNumber == agentPhone && u.ActiveAgentEMoney && amount <= u.MaxAmountEMoney && u.PhoneNumber != reqPhone);
                            if(agent == null){
                                return $"END The requested agent ({agentPhone}) could not be found or is not registered as an agent accepting {amount} GNF";
                            }
                            var (err, msg) = Transfer(agent.PhoneNumber, reqPhone, amount, agent.EMoneyFee, "Deposit", null);
                            if(err == -1){
                                return $"END {msg}";
                            }
                            return $"END You've requested {amount-amount*agent.EMoneyFee} E-GNF from {agent.PhoneNumber} in exchange for your {amount} GNF. Please meet and deliver the agent your GNF";
                        }
                    }
                    break;
                case "7": // withdraw
                    switch(reqlen){
                        case 1:
                        {
                            User user = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                            if(user == null){
                                return "END Your phone number could not be found. Are you registered?";
                            }
                            return "CON Please enter the amount of GNF you'd like to withdraw";
                        }
                        case 2:
                        {
                            decimal amount;
                            bool success = decimal.TryParse(sections[1], out amount);
                            if(!success){
                                return "END The amount you entered was invalid. Please start over";
                            }
                            var agents = userList.Where(u => u.ActiveAgentCash && amount <= u.MaxAmountCash && u.PhoneNumber != reqPhone).Select(u => new {u.ID, u.PhoneNumber, u.CashFee, u.MaxAmountCash}).OrderBy(u => u.CashFee).ToList();
                            if(agents.Count > 0){
                                string ret = "CON Please choose an agent by their phone number\nFee - Phone - You will receive\n";
                                foreach(var a in agents){
                                    ret+=$"{a.CashFee*100m}% - {a.PhoneNumber} - {amount-(amount*a.CashFee)} GNF\n";
                                }
                                return ret;
                            } else{
                                return $"END No agents delivering E-GNF could be found within the amount of {amount}";
                            }
                        }
                        case 3:
                        {
                            decimal amount = decimal.Parse(sections[1]);
                            int agentPhone = int.Parse(sections[2]);
                            User agent = userList.SingleOrDefault(u => u.PhoneNumber == agentPhone && u.ActiveAgentCash && amount <= u.MaxAmountCash && u.PhoneNumber != reqPhone);
                            if(agent == null){
                                return $"END The requested agent ({agentPhone}) could not be found or is not registered as an agent accepting {amount} GNF";
                            }
                            var (err, msg) = Transfer(reqPhone, agent.PhoneNumber, amount, agent.CashFee, "Withdrawal", null);
                            if(err == -1){
                                return $"END {msg}";
                            }
                            return $"END You've requested {amount-amount*agent.CashFee} GNF from {agent.PhoneNumber} in exchange for your {amount} E-GNF. Please meet and receive your GNF from the agent before confirming the transaction";
                        }
                    }
                    break;
                case "8": // confirm
                    switch(reqlen){
                        case 1: // List transactions pending confirmation
                        {
                            User u = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                            if(u == null){
                                return "END Your phone number could not be found. Are you registered?";
                            }
                            List<Transaction> ts = transactionList.Where(t => t.Status == "Pending" && t.From == u.ID).ToList();
                            if(ts.Count == 0){
                                return "END No transactions are awaiting your confirmation";
                            } else{
                                ts = ts.Take(5).ToList();
                                Dictionary<int, string> dp = new Dictionary<int, string>(); // convert user ids to phone number
                                string ret = "CON please enter the [ID] of the order you'd like to confirm\n";
                                foreach(Transaction t in ts){
                                    if(dp.ContainsKey(t.To)){
                                        if(t.Type == "Deposit"){
                                            ret+=$"[{t.ID}] you -> {dp[t.To]}. {t.Amount-(t.Amount*t.Fee)} E-GNF ({t.Type})";
                                        } else{
                                            ret+=$"[{t.ID}] you -> {dp[t.To]}. {t.Amount} E-GNF ({t.Type})";
                                        }
                                    } else{
                                        User rec = userList.SingleOrDefault(u => u.ID == t.To);
                                        if(rec == null){
                                            Console.WriteLine($"Recipient could not be found. ID: {t.ID}. Recipient ID: {t.To}");
                                            continue;
                                        }
                                        dp.Add(t.To, rec.PhoneNumber.ToString());
                                        if(t.Type == "Deposit"){
                                            ret+=$"[{t.ID}] you -> {rec.PhoneNumber}. {t.Amount-(t.Amount*t.Fee)} E-GNF ({t.Type})";
                                        } else{
                                            ret+=$"[{t.ID}] you -> {rec.PhoneNumber}. {t.Amount} E-GNF ({t.Type})";

                                        }
                                    }
                                    if(t.Reason != null && t.Reason != ""){
                                        ret+=$". \" {t.Reason} \"";
                                    }
                                    ret+="\n";
                                }
                                return ret;
                            }
                        }
                        case 2: // Enter ID
                        {
                            int id;
                            bool success = int.TryParse(sections[1], out id);
                            if(!success){
                                return $"END {sections[1]} is not a valid ID.";
                            }
                            User u = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                            if(u == null){
                                return "END Your phone number could not be found. Are you registered?";
                            }
                            Transaction t = transactionList.SingleOrDefault(t => t.ID == id && t.From == u.ID && t.Status == "Pending");
                            if(t == null){
                                return $"END no transaction with ID {id} could be found";
                            }
                            User rec = userList.SingleOrDefault(u => u.ID == t.To);
                            if(rec == null){
                                return $"END The recipient of the E-GNF could not be found";
                            }
                            if(t.Type == "Withdrawal"){ // user confirms a withdrawal
                                return $"CON please enter your PIN to verify the transfer of {t.Amount} E-GNF to {rec.PhoneNumber} in exchange for {t.Amount-(t.Amount*t.Fee)} GNF";
                            } else if(t.Type == "Deposit"){ // agent confirms a deposit
                                return $"CON please enter your PIN to verify the transfer of {t.Amount-(t.Amount*t.Fee)} E-GNF to {rec.PhoneNumber} in exchange for {t.Amount} GNF";
                            } else if(t.Type == "Request"){
                                return $"CON please enter your PIN to verify the transfer of {t.Amount} E-GNF to {rec.PhoneNumber}";
                            } else{
                                return $"END Unable to confirm a transaction of type {t.Type}";
                            }
                        }
                        case 3: // Enter PIN
                        {
                            int id;
                            int pin;
                            bool success = int.TryParse(sections[1], out id);
                            bool successPin = int.TryParse(sections[2], out pin);
                            if(!success){
                                return $"END {sections[1]} is not a valid ID.";
                            }
                            if(!successPin){
                                return $"END {sections[2]} is not a valid PIN.";
                            }
                            User u = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                            if(u == null){
                                return "END Your phone number could not be found. Are you registered?";
                            }
                            Transaction t = transactionList.SingleOrDefault(t => t.ID == id && t.From == u.ID && t.Status == "Pending");
                            if(t == null){
                                return $"END no transaction with ID {id} could be found";
                            }
                            User rec = userList.SingleOrDefault(u => u.ID == t.To);
                            if(rec == null){
                                return $"END The recipient of the E-GNF could not be found";
                            }
                            var (err, msg) = Confirm(reqPhone, id, pin);
                            if(err == -1){
                                return $"END {msg}";
                            } else{
                                if(t.Type == "Withdrawal"){ // user confirms a withdrawal
                                    return $"END You have completed the transfer of {t.Amount} E-GNF to {rec.PhoneNumber} in exchange for {t.Amount-(t.Amount*t.Fee)} GNF";
                                } else if(t.Type == "Deposit"){ // agent confirms a deposit
                                    return $"END You have completed the transfer of {t.Amount-(t.Amount*t.Fee)} E-GNF to {rec.PhoneNumber} in exchange for {t.Amount} GNF";
                                } else if(t.Type == "Request"){
                                    return $"END You have completed the transfer of {t.Amount} E-GNF to {rec.PhoneNumber}";
                                }
                                else{
                                    return $"END Unable to confirm a transaction of type {t.Type}";
                                }
                            }
                        }
                    }
                    break;
                case "9":
                    switch(reqlen){ // decline
                        case 1: // List transactions pending declining
                        {
                            User u = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                            if(u == null){
                                return "END Your phone number could not be found. Are you registered?";
                            }
                            List<Transaction> ts = transactionList.Where(t => t.Status == "Pending" && t.From == u.ID).ToList();
                            if(ts.Count == 0){
                                return "END No transactions are awaiting your declining";
                            } else{
                                ts = ts.Take(5).ToList();
                                Dictionary<int, string> dp = new Dictionary<int, string>(); // convert user ids to phone number
                                string ret = "CON please enter the [ID] of the order you'd like to decline\n";
                                foreach(Transaction t in ts){
                                    if(dp.ContainsKey(t.To)){
                                        ret+=$"[{t.ID}] you -> {dp[t.To]}. {t.Amount} E-GNF ({t.Type})";
                                    } else{
                                        User rec = userList.SingleOrDefault(u => u.ID == t.To);
                                        if(rec == null){
                                            Console.WriteLine($"Recipient could not be found. ID: {t.ID}. Recipient ID: {t.To}");
                                            continue;
                                        }
                                        dp.Add(t.To, rec.PhoneNumber.ToString());
                                        ret+=$"[{t.ID}] you -> {rec.PhoneNumber}. {t.Amount} E-GNF ({t.Type})";
                                    }
                                    if(t.Reason != null && t.Reason != ""){
                                        ret+=$". \" {t.Reason} \"";
                                    }
                                    ret+="\n";
                                }
                                return ret;
                            }
                        }
                        case 2: // Enter ID
                        {
                            int id;
                            bool success = int.TryParse(sections[1], out id);
                            if(!success){
                                return $"END {sections[1]} is not a valid ID.";
                            }
                            User u = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                            if(u == null){
                                return "END Your phone number could not be found. Are you registered?";
                            }
                            Transaction t = transactionList.SingleOrDefault(t => t.ID == id && t.From == u.ID && t.Status == "Pending");
                            if(t == null){
                                return $"END no transaction with ID {id} could be found";
                            }
                            User rec = userList.SingleOrDefault(u => u.ID == t.To);
                            if(rec == null){
                                return $"END The recipient of the E-GNF could not be found";
                            }
                            var (err, msg) = Decline(reqPhone, id);
                            if(err == -1){
                                return $"END {msg}";
                            } else{
                                if(t.Type == "Withdrawal"){ // user declines a withdrawal
                                    return $"END You have declined the transfer of {t.Amount} E-GNF to {rec.PhoneNumber} in exchange for {t.Amount-(t.Amount*t.Fee)} GNF";
                                } else if(t.Type == "Deposit"){ // agent declines a deposit
                                    return $"END You have declined the transfer of {t.Amount-(t.Amount*t.Fee)} E-GNF to {rec.PhoneNumber} in exchange for {t.Amount} GNF";
                                } else if(t.Type == "Request"){
                                    return $"END You have declined the transfer of {t.Amount} E-GNF to {rec.PhoneNumber}";
                                } else{
                                    return $"END Unable to decline a transaction of type {t.Type}";
                                }
                            }
                        }
                    }
                    break;
                case "10": // Mark agent as active for e-money
                    {
                        User user = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                        if(user == null){
                            return "END Your phone number could not be found. Are you registered?";
                        }
                        switch(reqlen){
                            case 1: // Toggle off if toggled on.
                            {
                                if(user.ActiveAgentEMoney){
                                    var (err, msg) = SetAgentStatusEMoney(reqPhone, 0, 0); // toggle status off
                                    if(err == -1){
                                        return $"END {msg}";
                                    }
                                    return "END You are no longer an agent delivering E-GNF";
                                } else{
                                    return "CON Please enter the maximum amount of E-GNF you are willing to transact";
                                }
                            }
                            case 2:
                            {
                                decimal maxamount;
                                bool success = decimal.TryParse(sections[1], out maxamount);
                                if(success){
                                    return $"CON Please enter your fee % for delivering E-GNF (Between 0 and 100). e.g. 5 for 5%";
                                } else{
                                    return "END Please start over and enter a valid amount, e.g. 10.20";
                                }
                            }
                            case 3:
                            {
                                decimal maxamount;
                                bool success = decimal.TryParse(sections[1], out maxamount);
                                decimal feePercentage;
                                bool successFee = decimal.TryParse(sections[2], out feePercentage);
                                if(!success){
                                    return "END Please start over and enter a valid amount, e.g. 10.20";
                                }
                                if(!successFee){
                                    return "END Please enter a valid fee. e.g. 5.5 or 10";
                                }
                                if(feePercentage < 0.0m){
                                    return "END Fee can not be less than 0";
                                }
                                if(feePercentage > 100.0m){
                                    return "END Fee can not be greater than 100";
                                }
                                var (err, msg) = SetAgentStatusEMoney(reqPhone, maxamount, feePercentage); // toggle status off
                                if(err == -1){
                                    return $"END {msg}";
                                } else{
                                    return $"END You have signed up as an agent delivering E-GNF. The maximum amount you've indicated is {maxamount}. Your fee is {feePercentage}%";
                                }
                            }
                        }
                    }
                    break;
                case "11": // Mark agent as active for cash
                    {

                        User user = userList.SingleOrDefault(u => u.PhoneNumber == reqPhone);
                        if(user == null){
                            return "END Your phone number could not be found. Are you registered?";
                        }
                        switch(reqlen){
                            case 1: // Toggle off if toggled on.
                            {
                                if(user.ActiveAgentCash){
                                    var (err, msg) = SetAgentStatusCash(reqPhone, 0, 0); // toggle status off
                                    if(err == -1){
                                        return $"END {msg}";
                                    }
                                    return "END You are no longer an agent delivering GNF";
                                } else{
                                    return "CON Please enter the maximum amount of GNF you are willing to transact";
                                }
                            }
                            case 2:
                            {
                                decimal maxamount;
                                bool success = decimal.TryParse(sections[1], out maxamount);
                                if(success){
                                    return $"CON Please enter your fee % for delivering GNF (Between 0 and 100). e.g. 5 for 5%";
                                } else{
                                    return "END Please start over and enter a valid amount, e.g. 10.20";
                                }
                            }
                            case 3:
                            {
                                decimal maxamount;
                                bool success = decimal.TryParse(sections[1], out maxamount);
                                decimal feePercentage;
                                bool successFee = decimal.TryParse(sections[2], out feePercentage);
                                if(!success){
                                    return "END Please start over and enter a valid amount, e.g. 10.20";
                                }
                                if(!successFee){
                                    return "END Please enter a valid fee. e.g. 5.5 or 10";
                                }
                                if(maxamount < 0.0m){
                                    return "END amount can not be less than 0";
                                }
                                if(feePercentage < 0.0m){
                                    return "END Fee can not be less than 0";
                                }
                                if(feePercentage > 100.0m){
                                    return "END Fee can not be greater than 100";
                                }
                                var (err, msg) = SetAgentStatusCash(reqPhone, maxamount, feePercentage); // toggle status off
                                if(err == -1){
                                    return $"END {msg}";
                                } else{
                                    return $"END You have signed up as an agent delivering GNF. The maximum amount you've indicated is {maxamount}. Your fee is {feePercentage}%";
                                }
                            }
                        }
                    }
                    break;
                case "12": // Signup
                    if(reqlen == 1){
                        return "CON Please enter a safe PIN number greater than 3 characters";
                    } else{
                        if(sections[1].Length < 4){
                            return $"END Your PIN was too short. Please start over";
                        }
                        int err = NewUser(int.Parse(sections[1]), reqPhone);
                        if(err == 0){
                            return $"END You've signed up to the service. Your PIN is {sections[1]}";
                        } else{
                            return $"END You couldn't be registered. Perhaps you're already a user?";
                        }
                    }
                default:
                    break;
            }
            return $"END Invalid input: {section}";
        }






        private static Dictionary<string, string> parsePostParams(HttpListenerRequest req){
            if (!req.HasEntityBody)
            {
                Console.WriteLine("No form data was found");
                return null;
            } else{
                // get post data
                Dictionary<string, string> postParams = new Dictionary<string, string>();
                Stream body = req.InputStream;
                Encoding encoding = req.ContentEncoding;
                StreamReader reader = new System.IO.StreamReader(body, encoding);
                string rawData = reader.ReadToEnd().Trim();
                //Console.WriteLine($"{req.Url.ToString()} - {rawData}");
                rawData = rawData.Substring(1, rawData.Length-2);
                string[] rawParams = rawData.Split(',');
                foreach (string param in rawParams)
                {
                    try{
                        string[] kvPair = param.Split(':');
                        string key = kvPair[0].Replace("\"", string.Empty);
                        string val = kvPair[1].Replace("\"", string.Empty);
                        string value = UrlDecode(val);
                        // Console.WriteLine($"{key}: {value}");
                        postParams.Add(key, value);
                    } catch(Exception e){
                        Console.WriteLine("Couldn't parse POST request");
                        Console.WriteLine(e);
                    }
                }
                return postParams;
            }
        }

        private static Dictionary<string, string> parseGetParams(HttpListenerRequest req){
            // get post data
            Console.WriteLine(req.QueryString);
            Dictionary<string, string> postParams = new Dictionary<string, string>();
            Stream body = req.InputStream;
            Encoding encoding = req.ContentEncoding;
            StreamReader reader = new System.IO.StreamReader(body, encoding);
            string rawData = reader.ReadToEnd();
            //Console.WriteLine(rawData);
            string[] rawParams = rawData.Split('&');
            foreach (string param in rawParams)
            {
                try{
                    string[] kvPair = param.Split('=');
                    string key = kvPair[0];
                    string value = UrlDecode(kvPair[1]);
                    postParams.Add(key, value);
                } catch(Exception e){
                    Console.WriteLine("Couldn't parse GET request");
                    Console.WriteLine(e);
                }
            }
            return postParams;
        }




        private static Dictionary<string, string> parseUSSDParams(HttpListenerRequest req){
            if (!req.HasEntityBody)
            {
                Console.WriteLine("No form data was found");
                return null;
            } else{
                // get post data
                Dictionary<string, string> postParams = new Dictionary<string, string>();
                Stream body = req.InputStream;
                Encoding encoding = req.ContentEncoding;
                StreamReader reader = new System.IO.StreamReader(body, encoding);
                string rawData = reader.ReadToEnd();
                //Console.WriteLine(rawData);
                string[] rawParams = rawData.Split('&');
                foreach (string param in rawParams)
                {
                    try{
                        string[] kvPair = param.Split('=');
                        string key = kvPair[0];
                        string value = UrlDecode(kvPair[1]);
                        // Console.WriteLine($"{key}: {value}");
                        postParams.Add(key, value);
                    } catch(Exception e){
                        Console.WriteLine("Couldn't parse GET request");
                        Console.WriteLine(e);
                    }
                }
                return postParams;
            }
        }


        private static (int, int) getPhoneFromToken(string token){
            if(usersLoggedIn.ContainsKey(token)){
                return (0, usersLoggedIn[token]);
            } else{
                return (-1, 0);
            }
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

                    var dict = new Dictionary<string, string>();
                    if(req.Url.AbsolutePath != "/ussd"){
                        dict = parsePostParams(req);
                    }
                    switch(req.Url.AbsolutePath){
                        case "/reset":
                        {
                                string response = "Reset accounts and transactions";
                                reset();
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
                            // handle ussd codes

                            var ussdDict = parseUSSDParams(req);
                            string response = handleUSSD(ussdDict);
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;





                        // Android
                        case "/android/login":
                        {
                            int pin;
                            int number;
                            string response = "";
                            if(!dict.ContainsKey("pin") || !dict.ContainsKey("number")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The pin code or phone number was not in the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            bool successPin = int.TryParse(dict["pin"], out pin);
                            bool successPh = int.TryParse(dict["number"], out number);
                            if(!successPh){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The phone number entered is not a valid phone number");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if(!successPin){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The PIN is not a valid PIN");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            User user = userList.SingleOrDefault(u => u.PhoneNumber == number && u.Pin == pin);
                            // user not found
                            if(user == null){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The provided phone number or PIN code was incorrect");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // user already logged in
                            var userl = usersLoggedIn.FirstOrDefault(u => u.Value == number);
                            try{
                                usersLoggedIn.Remove(userl.Key); // ensure user isn't stuck somewhere, i.e. they closed the app and they show as no longer online on client side
                            } catch(Exception){
                                Console.WriteLine($"Couldn't remove {number} from active sessions");
                                Console.WriteLine(userl);
                            }

                            // generate unique token
                            var token = Guid.NewGuid().ToString();
                            usersLoggedIn.Add(token, number);

                            // return token and success status
                            response = String.Format("{{err: {0}, token: \"{1}\" }}", 0, token);
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                        case "/android/register":
                        {
                            int pin;
                            int number;
                            string response = "";
                            if(!dict.ContainsKey("pin") || !dict.ContainsKey("number")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The pin code or phone number was not in the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            bool successPin = int.TryParse(dict["pin"], out pin);
                            bool successPh = int.TryParse(dict["number"], out number);
                            if(!successPh){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The phone number entered is not a valid phone number");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if(!successPin){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The PIN is not a valid PIN");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            User user = userList.SingleOrDefault(u => u.PhoneNumber == number);
                            // user already exists
                            if(user != null){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "User with this phonenumber already exists");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            NewUser(pin, number);
                            var token = Guid.NewGuid().ToString();
                            usersLoggedIn.Add(token, number);
                            response = String.Format("{{err: {0}, token: \"{1}\" }}", 0, token);
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                        case "/android/send":
                        {

                            // Ensure data is there and valid
                            decimal amount;
                            int recipient;
                            int pin;
                            string response = "";

                            // Check if amount exists
                            if(!dict.ContainsKey("amount")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter the amount of E-GNF you'd like to transfer");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if recipient exists
                            if(!dict.ContainsKey("recipient")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter the phone number of the recipient");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            bool successAmount = decimal.TryParse(dict["amount"], out amount);
                            bool successRec = int.TryParse(dict["recipient"], out recipient);

                            // Check if amount could be parsed
                            if(!successAmount){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The amount entered is not a valid amount.");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // Check if recipient could be parsed
                            if(!successRec){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The phone number entered is not a valid phone number");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if token exists
                            if(!dict.ContainsKey("token")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No token was provided with the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if token is valid
                            if (!usersLoggedIn.ContainsKey(dict["token"])){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -50, $"This session has expired, please log back in");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            var phone = usersLoggedIn[dict["token"]];

                            // check if sender exists
                            User from = userList.SingleOrDefault(u => u.PhoneNumber == phone);
                            if(from == null){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, $"Could not find an account with the number {recipient}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if recipient exists
                            User to = userList.SingleOrDefault(u => u.PhoneNumber == recipient);
                            if(to == null){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, $"Could not find an account with the number {recipient}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            if(recipient == from.PhoneNumber){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, $"You can't send money to yourself");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if balance is valid
                            if(from.Balance < amount){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "You don't have sufficient funds to complete this transfer");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Data was valid, go to next slide and enter PIN
                            if(dict.Count <= 4){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -2, "Data is valid, continue to enter PIN");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }


                            // Check if PIN exists
                            if(!dict.ContainsKey("pin")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter your PIN");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if PIN is valid
                            bool successPin = int.TryParse(dict["pin"], out pin);
                            if(!successPin){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The PIN is not a valid PIN.");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Parse reason if it exists
                            string reason = null;
                            if(dict.ContainsKey("reason")){
                                if(dict["reason"] != ""){
                                    reason = dict["reason"];
                                }
                            }

                            // Transfer
                            var (id, err) = Transfer(phone, recipient, amount, 0.00m, "Transfer", reason);
                            if(id == -1){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, err);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Confirm transfer
                            var (err2, msg) = Confirm(phone, id, pin);
                            if(err2 == -1){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, msg);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            } else{
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", 0, $"Successfully transfered {amount} E-GNF to {recipient}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            }

                        }
                        break;
                        case "/android/deposit":
                        {
                            string response = "";

                            // check if all input is there
                            if(!dict.ContainsKey("amount") || dict["amount"] == null || dict["amount"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter the amount of GNF you'd like to deposit");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if recipient exists
                            if(!dict.ContainsKey("agent") || dict["agent"] == null || dict["agent"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please choose an agent");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }


                            // parse input
                            decimal amount;
                            int agentId;
                            decimal fee;
                            bool successAmount = decimal.TryParse(dict["amount"], out amount);
                            bool successAgent = int.TryParse(dict["agent"], out agentId);
                            bool successFee = decimal.TryParse(dict["fee"], out fee);

                            // Check if amount could be parsed
                            if(!successAmount){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The amount entered is not a valid amount.");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // Check if agent could be parsed
                            if(!successAgent){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The requested agent id is not valid");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            if(!successFee){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The agents fee is invalid");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // check if token exists
                            if(!dict.ContainsKey("token")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No token was provided with the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if (!usersLoggedIn.ContainsKey(dict["token"])){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -50, $"This session has expired, please log back in");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // fetch token
                            var phone = usersLoggedIn[dict["token"]];
                            User agent = userList.SingleOrDefault(u => u.ID == agentId && u.MaxAmountEMoney > amount && u.EMoneyFee == fee);
                            if(agent == null){
                            response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, $"The requested agent could not be found or has changed their settings");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            var (err, msg) = Transfer(agent.PhoneNumber, phone, amount, agent.EMoneyFee, "Deposit", null);
                            if(err == -1){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, err);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            response = String.Format("{{err: {0}, message: \"{1}\" }}", 0, "Success");
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                        case "/android/withdraw":
                        {
                            string response = "";

                            // check if all input is there
                            if(!dict.ContainsKey("amount") || dict["amount"] == null || dict["amount"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter the amount of GNF you'd like to withdraw");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if recipient exists
                            if(!dict.ContainsKey("agent") || dict["agent"] == null || dict["agent"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please choose an agent");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }


                            // parse input
                            decimal amount;
                            int agentId;
                            decimal fee;
                            bool successAmount = decimal.TryParse(dict["amount"], out amount);
                            bool successAgent = int.TryParse(dict["agent"], out agentId);
                            bool successFee = decimal.TryParse(dict["fee"], out fee);

                            // Check if amount could be parsed
                            if(!successAmount){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The amount entered is not a valid amount.");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // Check if agent could be parsed
                            if(!successAgent){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The requested agent id is not valid");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            if(!successFee){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The agents fee is invalid");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // check if token exists
                            if(!dict.ContainsKey("token")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No token was provided with the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if (!usersLoggedIn.ContainsKey(dict["token"])){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -50, $"This session has expired, please log back in");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // fetch token
                            var phone = usersLoggedIn[dict["token"]];
                            User agent = userList.SingleOrDefault(u => u.ID == agentId && u.MaxAmountCash > amount && u.CashFee == fee);
                            if(agent == null){
                            response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, $"The requested agent could not be found or has changed their settings");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            var (err, msg) = Transfer(phone, agent.PhoneNumber, amount, agent.CashFee, "Withdrawal", null);
                            if(err == -1){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, err);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            response = String.Format("{{err: {0}, message: \"{1}\" }}", 0, "Success");
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                        case "/android/confirm":
                        {
                            string response = "";
                            if(!dict.ContainsKey("id") || dict["id"] == null || dict["id"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "no ID could be found");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if(!dict.ContainsKey("token")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No token was provided with the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if(!dict.ContainsKey("pin") || dict["id"] == null || dict["id"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter your PIN");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if (!usersLoggedIn.ContainsKey(dict["token"])){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -50, $"This session has expired, please log back in");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            int orderId;
                            bool successId = int.TryParse(dict["id"], out orderId);
                            if(!successId){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The order id is not valid");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            int pin;
                            bool successPin = int.TryParse(dict["pin"], out pin);
                            if(!successPin){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The PIN is not a valid PIN number");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // fetch token
                            var phone = usersLoggedIn[dict["token"]];
                            var (err, msg) = Confirm(phone, orderId, pin);
                            if(err == -1){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, msg);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            response = String.Format("{{err: {0}, message: \"{1}\" }}", 0, "Success");
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                        case "/android/decline":
                        {
                            string response = "";
                            if(!dict.ContainsKey("id") || dict["id"] == null || dict["id"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "no ID could be found");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if(!dict.ContainsKey("token")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No token was provided with the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if (!usersLoggedIn.ContainsKey(dict["token"])){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -50, $"This session has expired, please log back in");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            int orderId;
                            bool successId = int.TryParse(dict["id"], out orderId);
                            if(!successId){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The order id is not valid");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // fetch token
                            var phone = usersLoggedIn[dict["token"]];
                            var (err, msg) = Decline(phone, orderId);
                            if(err == -1){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, msg);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            response = String.Format("{{err: {0}, message: \"{1}\" }}", 0, "Success");
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();

                        }
                        break;
                        case "/android/stopAgent":
                        {
                            string response = "";

                            if(!dict.ContainsKey("delivertype") || dict["delivertype"] == null || dict["delivertype"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No deliver type was provided");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if(!dict.ContainsKey("token")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No token was provided with the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if token is valid
                            if (!usersLoggedIn.ContainsKey(dict["token"])){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -50, $"This session has expired, please log back in");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            var phone = usersLoggedIn[dict["token"]];
                            var err = -1;
                            var msg = "";

                            if(dict["delivertype"] == "cash"){
                                (err, msg) = SetAgentStatusCash(phone, 0, 0); // toggle status off
                            } else if(dict["delivertype"] == "emoney"){
                                (err, msg) = SetAgentStatusEMoney(phone, 0, 0); // toggle status off
                            } else{
                                msg = $"{dict["delivertype"]} is not a valid deliver type";
                            }
                            if(err == -1){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, msg);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            response = String.Format("{{err: {0}, message: \"{1}\" }}", 0, $"Success!");
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();

                        }
                        break;

                        case "/android/becomeAgent":
                        {
                            string response = "";

                            // Check if theres an amount
                            if(!dict.ContainsKey("amount") || dict["amount"] == null || dict["amount"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter the maximum amount you can transfer");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // Check if theres a fee
                            if(!dict.ContainsKey("fee") || dict["fee"] == null || dict["fee"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter your fee, e.g. 0.0");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if(!dict.ContainsKey("delivertype") || dict["delivertype"] == null || dict["delivertype"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No deliver type was provided");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // Check if token exists
                            if(!dict.ContainsKey("token")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No token was provided with the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if token is valid
                            if (!usersLoggedIn.ContainsKey(dict["token"])){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -50, $"This session has expired, please log back in");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            decimal amount;
                            decimal fee;
                            bool successAmount = decimal.TryParse(dict["amount"], out amount);
                            bool successFee = decimal.TryParse(dict["fee"], out fee);

                            // Check if amount could be parsed
                            if(!successAmount){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The amount entered is not a valid amount.");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if fee could be parsed
                            if(!successFee){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The fee is not a valid number");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            var phone = usersLoggedIn[dict["token"]];
                            var err = -1;
                            var msg = "";
                            if(dict["delivertype"] == "cash"){
                                (err, msg) = SetAgentStatusCash(phone, amount, fee);
                            } else if(dict["delivertype"] == "emoney"){
                                (err, msg) = SetAgentStatusEMoney(phone, amount, fee);
                            } else{
                                msg = $"{dict["delivertype"]} is not a valid deliver type";
                            }
                            if(err == -1){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, msg);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            response = String.Format("{{err: {0}, message: \"{1}\" }}", 0, $"Success!");
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                        case "/android/help":
                        {
                            string response = "";

                            // Check if theres a question
                            if(!dict.ContainsKey("question") || dict["question"] == null || dict["question"] == ""){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please ask a question or enter your phone number");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // Check if token exists
                            if(!dict.ContainsKey("token")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No token was provided with the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if token is valid
                            if (!usersLoggedIn.ContainsKey(dict["token"])){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -50, $"This session has expired, please log back in");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            var phone = usersLoggedIn[dict["token"]];

                            var (err, msg) = newQuestion(phone, dict["question"]);
                            if(err == -1) {
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, msg);
                            } else{
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", 0, $"Your question has been sent. You'll get a response shortly");
                            }
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
                        break;
                        case "/android/request":
                        {
                            // Ensure data is there and valid
                            decimal amount;
                            int sender;
                            string response = "";

                            // Check if amount exists
                            if(!dict.ContainsKey("amount")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter the amount of E-GNF you'd like to receive");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if sender exists
                            if(!dict.ContainsKey("sender")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "Please enter the phone number of the sender");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }


                            bool successAmount = decimal.TryParse(dict["amount"], out amount);
                            bool successSen = int.TryParse(dict["sender"], out sender);

                            // Check if amount could be parsed
                            if(!successAmount){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The amount entered is not a valid amount.");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if sender phone number could be parsed
                            if(!successSen){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The phone number entered is not a valid phone number");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if token exists
                            if(!dict.ContainsKey("token")){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "No token was provided with the request");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if token is valid
                            if (!usersLoggedIn.ContainsKey(dict["token"])){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -50, $"This session has expired, please log back in");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            var phone = usersLoggedIn[dict["token"]];

                            // Check if sender exists
                            User from = userList.SingleOrDefault(u => u.PhoneNumber == sender);
                            if(from == null){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, $"Could not find an account with the number {sender}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if(from.PhoneNumber == phone){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, $"Can't request money from yourself");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Check if recipient exists
                            User to = userList.SingleOrDefault(u => u.PhoneNumber == phone);
                            if(to == null){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, $"Could not find an account with the number {phone}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // Parse reason if it exists
                            string reason = null;
                            if(dict.ContainsKey("reason")){
                                if(dict["reason"] != ""){
                                    reason = dict["reason"];
                                }
                            }

                            // Send request
                            var (id, err) = Transfer(sender, phone, amount, 0.00m, "Request", reason);
                            if(id == -1){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, err);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            response = String.Format("{{err: {0}, message: \"{1}\" }}", 0, $"Successfully requested {amount} E-GNF from {sender}");
                            await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                            resp.Close();
                        }
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
                    Console.WriteLine($"{req.Url.ToString()} - GET");
                    switch(req.Url.AbsolutePath){
                        case "/":
                        {

                            // Write the response info
                            byte[] data = Encoding.UTF8.GetBytes(String.Format(pageData, formatUserHtml(), formatTransactionHtml(), formatQuestionHtml()));
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

                        // Android
                        case "/android/balance":
                            {
                                string response = "";
                                var token = req.QueryString["token"];
                                // look up token in login table and fetch phone number
                                if(token == null){
                                    response = String.Format("{{ err: -50, message: \"No token was provided with the request\" }}");
                                    await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                    resp.Close();
                                    break;
                                }
                                // Look up token
                                var (err, phone) = getPhoneFromToken(token);
                                if(err == -1){
                                    response = String.Format("{{ err: -50, message: \"No users logged in with the provided token\" }}");
                                    await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                    resp.Close();
                                    break;
                                }
                                var (errb, bal) = GetBalance(phone);
                                if(errb == -1){
                                    response = String.Format("{{ err: {0}, message: \"{1}\" }}", errb, "User could not be found");
                                }
                                else{
                                    response = String.Format("{{ err: {0}, balance: {1} }}", errb, bal);
                                }
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            }
                        break;
                        case "/android/agentstatus":
                            {
                                string response = "";
                                var token = req.QueryString["token"];
                                // look up token in login table and fetch phone number
                                if(token == null){
                                    response = String.Format("{{ err: -50, message: \"No token was provided with the request\" }}");
                                    await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                    resp.Close();
                                    break;
                                }
                                var (err, phone) = getPhoneFromToken(token);
                                if(err == -1){
                                    response = String.Format("{{ err: -50, message: \"No users logged in with the provided token\" }}");
                                    await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                    resp.Close();
                                    break;
                                }

                                User user = userList.SingleOrDefault(u => u.PhoneNumber == phone);
                                if(user == null){
                                    response = String.Format("{{ err: {0}, message: \"{1}\" }}", -1, "Could not a user with your phone number");
                                } else{
                                    response = String.Format("{{ err: {0}, deliverCash: \"{1}\", deliverEMoney: \"{2}\" }}", 0, user.ActiveAgentCash, user.ActiveAgentEMoney);
                                }
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            }
                        break;

                        case "/android/history":
                        {
                            string response = "";
                            var token = req.QueryString["token"];
                            // look up token in login table and fetch phone number
                            if(token == null){
                                response = String.Format("{{ err: -50, message: \"No token was provided with the request\" }}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // Look up token
                            var (err, phone) = getPhoneFromToken(token);
                            if(err == -1){
                                response = String.Format("{{ err: -50, message: \"No users logged in with the provided token\" }}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }

                            // fetch the latest 1000 transactions with a 0 offset
                            var (err2, ts) = ListTransactions(phone, 0, 1000);
                            if(err2 == -1){
                                response = String.Format("{{ err: -1, message: \"Something went wrong, please try again\" }}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            } else{
                                var jsonts = JsonSerializer.Serialize(ts);
                                response = String.Format("{{ err: {0}, transactions: {1} }}", 0, jsonts);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            }

                        }
                        break;
                        case "/android/agents":
                        {
                            string response = "";
                            decimal amount;
                            var dict = req.QueryString;


                            if(dict["amount"] == null || dict["amount"] == ""){
                                response = String.Format("{{ err: -1, message: \"No type was specified\" }}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            bool successAmount = decimal.TryParse(dict["amount"], out amount);

                            // Check if amount could be parsed
                            if(!successAmount){
                                response = String.Format("{{err: {0}, message: \"{1}\" }}", -1, "The amount entered is not a valid amount.");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            var token = dict["token"];
                            // look up token in login table and fetch phone number
                            if(token == null){
                                response = String.Format("{{ err: -1, message: \"No token was provided with the request\" }}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            // Look up token
                            var (err, phone) = getPhoneFromToken(token);
                            if(err == -1){
                                response = String.Format("{{ err: -50, message: \"No users logged in with the provided token\" }}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            if(dict["type"] == null || dict["type"] == ""){
                                response = String.Format("{{ err: -1, message: \"No type was specified\" }}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                                break;
                            }
                            var typ = dict["type"];
                            if(typ == "deposit"){
                                var agents = userList.Where(u => u.ActiveAgentEMoney && amount <= u.MaxAmountEMoney && u.PhoneNumber != phone).Select(u => new {u.ID, u.PhoneNumber, u.EMoneyFee}).OrderBy(u => u.EMoneyFee).ToList();
                                var jsonagents = JsonSerializer.Serialize(agents);
                                response = String.Format("{{ err: 0, agents: {0} }}", jsonagents);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            } else if(typ == "withdrawal"){
                                var agents = userList.Where(u => u.ActiveAgentCash && amount <= u.MaxAmountCash && u.PhoneNumber != phone).Select(u => new {u.ID, u.PhoneNumber, u.CashFee}).OrderBy(u => u.CashFee).ToList();
                                var jsonagents = JsonSerializer.Serialize(agents);
                                response = String.Format("{{ err: 0, agents: {0} }}", jsonagents);
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            } else{
                                response = String.Format("{{ err: -1, message: \"The type specified was neither of type deposit or withdrawal\" }}");
                                await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response, 0, response.Length));
                                resp.Close();
                            }
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
