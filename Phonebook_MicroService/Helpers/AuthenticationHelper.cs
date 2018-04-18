using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phonebook_MicroService.Helpers
{
    public class AuthenticationHelper
    {
        //This function helps to decode the Basic Authorization HTTP Request Header in order to get the user name and password.
        public static bool DecodeCredentials(string basicAuthKey, ref string userMail, ref string userPassword)
        {
            try
            {
                if (!string.IsNullOrEmpty(basicAuthKey) && basicAuthKey.StartsWith("basic", StringComparison.OrdinalIgnoreCase))
                {
                    var token = basicAuthKey.Substring("Basic ".Length).Trim();
                    string originalString = Encoding.UTF8.GetString(Convert.FromBase64String(token));

                    // Gets username and password  
                    userMail = originalString.Split(':')[0];
                    userPassword = originalString.Split(':')[1];

                    if (userMail.Trim() == "" || userPassword.Trim() == "")
                        return false;

                    return true;
                }
                else return false;
            }catch
            {
                return false;
            }
        }
    }
}
