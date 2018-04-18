using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Phonebook_MicroService.Helpers;
using Phonebook_MicroService.Models;

namespace Phonebook_MicroService.Controllers
{
    
    [Route("api/v1/phonebook")] //This sets the main route for the AWS Micro Service
    public class PhoneBookController : BaseController
    {
        private PhoneBookS3ProcessHelper _phoneBookCab = null;
        private bool _executeAction = false;
        private string _phoneBookCabException = "";
        private bool _authorized = false;
        private string _userMail = "";
        private string _userPass = "";

        public PhoneBookController()
        {
           
        }

        private void AuthIntercept()
        {
            try
            {
                var authHeader = HttpContext.Request.Headers["Authorization"];

                var DecodeAuthResult = AuthenticationHelper.DecodeCredentials(authHeader, ref _userMail, ref _userPass);

                if (!DecodeAuthResult)
                {
                    _authorized = false;
                    _executeAction = false;
                    return;
                } else _authorized = true;

                this._phoneBookCab = new PhoneBookS3ProcessHelper(_userMail, _userPass);
                _executeAction = this._phoneBookCab._clientSuccess;
                _phoneBookCabException = this._phoneBookCab._cloudWatchException;
            }
            catch (Exception Ex)
            {
                DataEnvelope.logConsoleError("PhoneBookController", "Constructor", 500, 100, Ex.Message);
                _executeAction = false;
                _phoneBookCabException = "Phonebook process class could not be instantiated!!";
            }
        }

        [Route("create")]
        [HttpPost]
        public string CreatePhoneBook([FromForm]string phonebook_name)
        {//This service enable a phone book collection to be created to store phonebook entries on AWS S3

            AuthIntercept();
            if (!_authorized)
            {
                return SendResponse<DataEnvelope>(null, HttpStatusCode.Unauthorized);
            }

            if (_executeAction)
            {
                //We can safly carry on with the process task.
                try
                {
                    var Result = _phoneBookCab.AddUserPhoneBook(phonebook_name);
                    return SendResponse<DataEnvelope>(Result, (HttpStatusCode)Result.StatusCode);

                } catch (Exception Ex)
                {
                    DataEnvelope.logConsoleError("Phonebook", "Create", 500, 100, Ex.Message);
                    return SendResponse<DataEnvelope>(null, HttpStatusCode.BadRequest);
                }
            } else
            {
                DataEnvelope.logConsoleError("Phonebook", "Create", 500, 101, _phoneBookCabException);
                return SendResponse<DataEnvelope>(null, HttpStatusCode.BadRequest);
            }
        }

        [Route("records/add/{phonebook_name}")]
        [HttpPost]
        public string AddPhoneBookRecord(string phonebook_name, [FromForm]string name, [FromForm]string phone_number)
        {//This service enable phonebook record entries to be created within a phonebook collection

            AuthIntercept();
            if (!_authorized)
            {
                return SendResponse<DataEnvelope>(null, HttpStatusCode.Unauthorized);
            }

            if (_executeAction)
            {
                //We can safly carry on with the process task.

                try
                {
                    var Result = _phoneBookCab.AddPhoneBookRecord(phonebook_name, name, phone_number);
                    return SendResponse<DataEnvelope>(Result, (HttpStatusCode)Result.StatusCode);

                }
                catch (Exception Ex)
                {
                    DataEnvelope.logConsoleError("Phonebook Records", "Add", 500, 100, Ex.Message);
                    return SendResponse<DataEnvelope>(null, HttpStatusCode.BadRequest);
                }
            }
            else
            {
                DataEnvelope.logConsoleError("Phonebook Records", "Add", 500, 101, _phoneBookCabException);
                return SendResponse<DataEnvelope>(null, HttpStatusCode.BadRequest);
            }

        }

        [Route("records/list/{phonebook_name}")]
        [HttpGet]
        public string ListPhoneBookRecords(string phonebook_name)
        {//This service allows to display all phonebook record entries in the phonebook collection with search capability

            AuthIntercept();
            if (!_authorized)
            {
                return SendResponse<DataEnvelope>(null, HttpStatusCode.Unauthorized);
            }

            if (_executeAction)
            {
                //We can safly carry on with the process task.

                try
                {
                    var Result = _phoneBookCab.ListPhoneBookRecords(phonebook_name);

                    //----Intercept the Result to apply a quick search filter -- This is a very quick hack for now!!
                    string dataFilter = "";

                    try
                    {
                        dataFilter = HttpContext.Request.Query["search"];
                    }
                    catch
                    {
                        dataFilter = "";
                    }

                    IEnumerable<Entry> qryEntries = null;

                    //Just a sanity check to make sure we dont deal with null values.
                    if (dataFilter is null) dataFilter = "";

                    if (dataFilter.Trim() != "")
                        qryEntries = from entry in (Result.Payload as List<Entry>)
                                     where entry.Name.Trim().ToLower().Contains(dataFilter.Trim().ToLower()) || 
                                        entry.PhoneNumber.Trim().ToLower().Contains(dataFilter.Trim().ToLower())
                                     select entry;

                    if (qryEntries != null)
                    {
                        //Update the Payload Data
                        Result.Payload = qryEntries;
                        Result.Records = qryEntries.Count();
                    }

                    //------------------------------------------------------------------------------------------------

                    return SendResponse<DataEnvelope>(Result,(HttpStatusCode)Result.StatusCode);
                }
                catch (Exception Ex)
                {
                    DataEnvelope.logConsoleError("Phonebook Records", "List", 500, 100, Ex.Message);
                    return SendResponse<DataEnvelope>(null, HttpStatusCode.BadRequest);
                }
            }
            else
            {
                DataEnvelope.logConsoleError("Phonebook Records", "List", 500, 101, _phoneBookCabException);
                return SendResponse<DataEnvelope>(null, HttpStatusCode.BadRequest);
            }

        }

        [Route("list")]
        [HttpGet]
        public string ListPhoneBooks()
        {//This service allows to list all phonebook collections being created

            AuthIntercept();
            if (!_authorized)
            {
                return SendResponse<DataEnvelope>(null, HttpStatusCode.Unauthorized);
            }

            if (_executeAction)
            {
                //We can safly carry on with the process task.

                try
                {
                    var Result = _phoneBookCab.ListUserPhoneBooks();
                    return SendResponse<DataEnvelope>(Result, (HttpStatusCode)Result.StatusCode);

                }
                catch (Exception Ex)
                {
                    DataEnvelope.logConsoleError("Phonebook", "List", 500, 100, Ex.Message);
                    return SendResponse<DataEnvelope>(null, HttpStatusCode.BadRequest);
                }
            }
            else
            {
                DataEnvelope.logConsoleError("Phonebook", "List", 500, 101, _phoneBookCabException);
                return SendResponse<DataEnvelope>(null, HttpStatusCode.BadRequest);
            }

        }

    }
}