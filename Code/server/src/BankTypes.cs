using System;

namespace BankTypes
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
}