using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Phonebook_MicroService.Models
{
    //this class define the the phonebook record collection
    public class PhoneBook
    {
        private string _name = "";
        private List<Entry> _entries = new List<Entry>();

        public string Name { get { return _name; } set { _name = value; } }
        public List<Entry> Entries { get { return _entries; } set { _entries = value; } }
    }

    //This class stores the physical phonebook data record entries
    public class Entry
    {
        private string _name = "";
        private string _phoneNumber = "";

        public string Name { get { return _name; } set { _name = value; } }
        public string PhoneNumber { get { return _phoneNumber; } set { _phoneNumber = value; } }
    }

    //This class represent and manage the result being returned through the API
    public class DataEnvelope
    {
        //This field represent the http response status code indicator
        public int StatusCode { get; set; }
        //If there is an error in a process this field will hold an error code reference
        public int ErrorCode { get; set; } 
        //Error message that will be display as part of the API output
        public string Message { get; set; }
        //The number of payload data records returned
        public int Records { get; set; }
        //The total number of the payload data records in the collection
        public int TotalRecords { get; set; }
        //This indicate a CRUD action to indicate where the error have occured ie. List, Create, Delete etc.
        public string Action { get; set; }
        //The HTTP resource being executed to identify where the error occured
        public string Resource { get; set; }
        //The total size of the JSON document beging served by the API
        public int PayloadSize { get; set; }
        //This field holds the actual data being requested
        public dynamic Payload { get; set; }

        public DataEnvelope()
        {
            this.StatusCode = 0;
            this.ErrorCode = 0;
            this.Message = "";
            this.Records = 0;
            this.TotalRecords = 0;
            this.Action = "";
            this.Resource = "";
            this.PayloadSize = 0;
            this.Payload = null;
        }

        //This function just return a pre populated envelope to return as the API output result
        public static DataEnvelope generate(
                int StatusCode,
                int Records,
                int TotalRecords,
                int PayloadSize,
                dynamic Payload,
                string Message = "",
                int ErrorCode = 0,
                string ExceptionMessage = "",
                string Action = "",
                string Resource = ""
            )
        {

            DataEnvelope Result = new DataEnvelope();

            Result.StatusCode = StatusCode;
            Result.ErrorCode = ErrorCode;
            Result.Message = Message;
            Result.Records = Records;
            Result.TotalRecords = TotalRecords;
            Result.Action = Action;
            Result.Resource = Resource;
            Result.Payload = Payload;
            Result.PayloadSize = PayloadSize;

            //If we detect that an error occured we automatically log a console message that will reflect in the AWS cloud watch logs
            if (ErrorCode > 0)
            {
                DataEnvelope.logConsoleError(Result.Resource, Result.Action, Result.StatusCode, Result.ErrorCode, ExceptionMessage);
            }

            return Result;

        }

        public static void logConsoleError(string Resource, string Action, int StatusCode, int ErrorCode, string Message)
        {
            string ErrorMessage = string.Format("Error!! {0}, {1} => StatusCode({2}), ErrorCode({3}): Message:{4}",
                    Resource,
                    Action,
                    StatusCode.ToString(),
                    ErrorCode.ToString(),
                    Message
                    );

            Console.WriteLine(ErrorMessage);
        }

    }
    
}
