using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http.Extensions;
using System.Text;
using System.IO;
using Phonebook_MicroService.Models;

namespace Phonebook_MicroService.Controllers
{
    
    public class BaseController : Controller
    {
        public string SendResponse<T>(T responseObj, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
           
            string JSONDoc = JsonConvert.SerializeObject(responseObj);

            //Intercept and add the Payload Size automatically.
            if (responseObj is DataEnvelope)
                (responseObj as DataEnvelope).PayloadSize = JSONDoc.Length;

            //Update the JSON Doc again.
            JSONDoc = JsonConvert.SerializeObject(responseObj);

            //Manually adjust the response object.
            Response.StatusCode = (int)statusCode;
            Response.ContentType = "application/json";
            Response.ContentLength = JSONDoc.Length;

            //Add the JSON Doc to the response body to serve the information.
            byte[] byteArray = Encoding.UTF8.GetBytes(JSONDoc);
            Response.Body.Write(byteArray,0, byteArray.Length);

            return "";
        }
    }
}