using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Newtonsoft.Json;
using Phonebook_MicroService.Models;
using System.Dynamic;
using System.Security.Cryptography;
using Amazon.S3.Transfer;
using System.IO;
using System.Text;
using System.IO.Compression;

namespace Phonebook_MicroService.Helpers
{
    public class PhoneBookS3ProcessHelper
    {
        //This section can be loaded and set from a configuration database or data store when the class loads.
        private const string AccessKey = "<AWS Access Key>"; //Put own AWS Access Key here.
        private const string SecretKey = "<AWS Secret Key>"; //Put own Secret Key here.
        private const string BucketName = "app-phonebook-loc"; //The name of the S3 bucket where all the phonebook records needs to be stored.
        private const string PhoneBookRecordFileName = "Records.dat"; //Name of the phonebook record enry collection file
        //----

        IAmazonS3 _client;
        public bool _clientSuccess = false; //Fail check to determine if the class can operate on method process functions.
        public string _userMail = ""; //User Mail that got sent through the API header.
        public string _userPassword = ""; //User Password that got sent through the API header.
        public string _userAccessKey = ""; //User secret key for file encryption and decryption
        public DataEnvelope _dataEnvelope = null;
        public string _cloudWatchException = "";

        public PhoneBookS3ProcessHelper(string UserMail, string UserPassword)
        {

            _userMail = UserMail.Trim().ToLower();
            _userPassword = UserPassword.Trim();

            //Do a validation check to make sure that we receive a valid user mail and password as this is vital for the process.
            if (_userMail == "" || _userPassword == "")
            {
                _cloudWatchException = "S3Utils - Class Failed to Instantiate!! - User Credentials are Invalid!!"; //Write to console logs so that we can pickup the error in AWS CloudWatch
                _clientSuccess = false; //Force class failure (sanity check and prevention)
                return;
            }

            try
            {
                this._client = new AmazonS3Client(AccessKey, SecretKey, Amazon.RegionEndpoint.EUWest1); //Put the correct AWS Region Endpoint here..
                _clientSuccess = true;

                //Lets check if the required bucket exists .. if not then we need to create it.
                if (!(AmazonS3Util.DoesS3BucketExistAsync(_client, BucketName).Result))
                {
                    PutBucketRequest CreateBucketRequest = new PutBucketRequest
                    {
                        BucketName = BucketName,
                        UseClientRegion = true
                    };

                    PutBucketResponse CreateBucketResponse = _client.PutBucketAsync(CreateBucketRequest).Result;
                }

                string DefaultUserPhoneBook = HashUserCredentials() + "/Default/" + PhoneBookRecordFileName;

                //Check if we have a default PhoneBook if not then we need to create it.
                if (!S3ObjectExists(BucketName, DefaultUserPhoneBook))
                {

                    PhoneBook newPhoneBook = new PhoneBook();
                    newPhoneBook.Name = "Default";

                    Entry phoneBookEntry = new Entry();
                    phoneBookEntry.Name = "";
                    phoneBookEntry.PhoneNumber = "";

                    newPhoneBook.Entries.Add(phoneBookEntry);

                    string EmptyDefaultPhoneBook = JsonConvert.SerializeObject(newPhoneBook);

                    UploadDataToS3Bucket("Default", EmptyDefaultPhoneBook);
                }


            }
            catch (Exception Ex)
            {
                //Some error control and notification can be applied here..

                _cloudWatchException = "S3Utils - Class Failed to Instantiate!! - Exception:" + Ex.Message; //Write to console logs so that we can pickup the error in AWS CloudWatch

                _clientSuccess = false; //Force class failure (sanity check and prevention)
            }
        }

        public bool S3ObjectExists(string bucket, string key)
        {

            GetObjectRequest request = new GetObjectRequest();
            request.BucketName = bucket;
            request.Key = key;

            try
            {
                GetObjectResponse response = _client.GetObjectAsync(request).Result;
                if (response.ResponseStream != null)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        internal static string GetStringSha256Hash(string text)
        {
            if (String.IsNullOrEmpty(text))
                return String.Empty;

            using (var sha = new System.Security.Cryptography.SHA256Managed())
            {
                byte[] textData = System.Text.Encoding.UTF8.GetBytes(text);
                byte[] hash = sha.ComputeHash(textData);
                return BitConverter.ToString(hash).Replace("-", String.Empty);
            }
        }

        private string HashUserCredentials()
        {
            return PhoneBookS3ProcessHelper.GetStringSha256Hash(_userMail + "#" + _userPassword).Replace("/", "");
        }

        public DataEnvelope ListUserPhoneBooks()
        {

            List<string> phoneBookList = new List<string>();

            try
            {

                ListObjectsV2Request request = new ListObjectsV2Request
                {
                    BucketName = BucketName,
                    MaxKeys = 50,
                    Prefix = HashUserCredentials() + "/"
                };

                ListObjectsV2Response response;

                do
                {
                    response = _client.ListObjectsV2Async(request).Result;

                    // Process response.
                    foreach (S3Object entry in response.S3Objects)
                    {
                        //Debug Log
                        Console.WriteLine("key = {0} size = {1}",
                            entry.Key, entry.Size);

                        //We trim out the account root path and the records data file so that can only obtain the phone book name
                        phoneBookList.Add(entry.Key.Replace(HashUserCredentials() + "/", "").Replace("/" + PhoneBookRecordFileName, ""));

                    }
                    Console.WriteLine("Next Iteration: {0}", response.NextContinuationToken); //Debug Log
                    request.ContinuationToken = response.NextContinuationToken;
                } while (response.IsTruncated == true);

                int phoneBookListStatus = 0;

                if (phoneBookList.Count() > 0) phoneBookListStatus = 200; else phoneBookListStatus = 204;

                return DataEnvelope.generate(phoneBookListStatus,
                    phoneBookList.Count(),
                    phoneBookList.Count(),
                    0,
                    phoneBookList
                    );

            }
            catch (Exception Ex)
            {
                return DataEnvelope.generate(500,
                    0,
                    0,
                    0,
                    null,
                    "Exception has occured!!",
                    100,
                    Ex.Message,
                    "List",
                    "Phonebooks"
                    );
            }
        }

        public DataEnvelope AddUserPhoneBook(string PhonebookName)
        {

            string DefaultUserPhoneBook = HashUserCredentials() + "/" + PhonebookName + "/" + PhoneBookRecordFileName;

            try
            {
                //This check will avoid creating duplicate Phone Book entries.
                if (!S3ObjectExists(BucketName, DefaultUserPhoneBook))
                {

                    PhoneBook newPhoneBook = new PhoneBook();
                    newPhoneBook.Name = PhonebookName;

                    Entry phoneBookEntry = new Entry();
                    phoneBookEntry.Name = "";
                    phoneBookEntry.PhoneNumber = "";

                    newPhoneBook.Entries.Add(phoneBookEntry);

                    string EmptyDefaultPhoneBook = JsonConvert.SerializeObject(newPhoneBook);

                    UploadDataToS3Bucket(PhonebookName, EmptyDefaultPhoneBook);

                    return DataEnvelope.generate(201,
                    1,
                    1,
                    0,
                    newPhoneBook
                    );
                }
                else return DataEnvelope.generate(200,
                    0,
                    0,
                    0,
                    null,
                    "Phonebook already exists with name: " + PhonebookName + "!!",
                    110
                    );

            }
            catch (Exception Ex)
            {
                return DataEnvelope.generate(500,
                    0,
                    0,
                    0,
                    null,
                    "Exception has occured!!",
                    100,
                    Ex.Message,
                    "List",
                    "Phonebooks"
                    );
            }
        }

        public DataEnvelope ListPhoneBookRecords(string PhoneBookName)
        {

            List<Entry> DBContext = new List<Entry>();

            string DefaultUserPhoneBook = HashUserCredentials() + "/" + PhoneBookName + "/" + PhoneBookRecordFileName;

            try
            {

                String content = DownloadDataFromS3Bucket(PhoneBookName);

                //Convert text data to object
                PhoneBook PhoneBookRecordList = JsonConvert.DeserializeObject<PhoneBook>(content);

                if (PhoneBookRecordList.Entries.Count() > 0)
                {
                    DBContext = PhoneBookRecordList.Entries;
                    return DataEnvelope.generate(200,
                    DBContext.Count(),
                    DBContext.Count(),
                    0,
                    DBContext
                    );
                }
                else
                {
                    DBContext = PhoneBookRecordList.Entries;
                    return DataEnvelope.generate(204,
                    DBContext.Count(),
                    DBContext.Count(),
                    0,
                    DBContext
                    );
                }
            }
            catch (Exception Ex)
            {
                return DataEnvelope.generate(500,
                    0,
                    0,
                    0,
                    null,
                    "Exception has occured!!",
                    100,
                    Ex.Message,
                    "List",
                    "Phonebooks"
                    );
            }
        }

        public DataEnvelope AddPhoneBookRecord(string phoneBookName, string name, string phoneNumber)
        {
            try
            {
                string DefaultUserPhoneBook = HashUserCredentials() + "/" + phoneBookName + "/" + PhoneBookRecordFileName;
                List<Entry> PhoneBookList = (List<Entry>)ListPhoneBookRecords(phoneBookName).Payload;

                //Do check to make sure the phone number is unique
                var results = PhoneBookList.FindAll(x => x.PhoneNumber.Trim() == phoneNumber.Trim());

                //Check if there is already an item with the same phone number.
                if (results != null && results.Count() > 0)
                {
                    return DataEnvelope.generate(200,
                    0,
                    0,
                    0,
                    null,
                    "Phonebook record already exists with phone number: " + phoneNumber + "!!",
                    110
                    );
                }

                var phoneBookRecord = new Entry();
                phoneBookRecord.Name = name;
                phoneBookRecord.PhoneNumber = phoneNumber;

                PhoneBookList.Add(phoneBookRecord);

                PhoneBook newPhoneBook = new PhoneBook();
                newPhoneBook.Name = phoneBookName;
                newPhoneBook.Entries = PhoneBookList;

                string EmptyDefaultPhoneBook = JsonConvert.SerializeObject(newPhoneBook);

                UploadDataToS3Bucket(phoneBookName, EmptyDefaultPhoneBook);

                return DataEnvelope.generate(201,
                    1,
                    1,
                    0,
                    phoneBookRecord
                    );
            }
            catch (Exception Ex)
            {
                return DataEnvelope.generate(500,
                    0,
                    0,
                    0,
                    null,
                    "Exception has occured!!",
                    100,
                    Ex.Message,
                    "List",
                    "Phonebooks"
                    );
            }

        }

        private void UploadDataToS3Bucket(string phoneBookName, string data)
        {
            try
            {
                data = Compress(data);

                string DefaultUserPhoneBook = HashUserCredentials() + "/" + phoneBookName + "/" + PhoneBookRecordFileName;

                byte[] byteArray = Encoding.UTF8.GetBytes(data);
                MemoryStream stream = new MemoryStream(byteArray);

                TransferUtility transferUtil = new TransferUtility(_client);
                transferUtil.Upload(stream, BucketName, DefaultUserPhoneBook);

            }
            catch (Exception Ex)
            {
                throw new Exception(Ex.Message);
            }
        }

        private string DownloadDataFromS3Bucket(string phoneBookName)
        {
            string DefaultUserPhoneBook = HashUserCredentials() + "/" + phoneBookName + "/" + PhoneBookRecordFileName;

            try
            {

                var getResponse = _client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = BucketName,
                    Key = DefaultUserPhoneBook
                }).Result;

                StreamReader reader = new StreamReader(getResponse.ResponseStream);
                String content = reader.ReadToEnd();

                return Decompress(content);
            }
            catch (Exception Ex)
            {
                throw new Exception(Ex.Message);
            }
        }

        public string Compress(string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            MemoryStream ms = new MemoryStream();
            using (GZipStream zip = new GZipStream(ms, CompressionMode.Compress, true))
            {
                zip.Write(buffer, 0, buffer.Length);
            }

            ms.Position = 0;
            MemoryStream outStream = new MemoryStream();

            byte[] compressed = new byte[ms.Length];
            ms.Read(compressed, 0, compressed.Length);

            byte[] gzBuffer = new byte[compressed.Length + 4];
            System.Buffer.BlockCopy(compressed, 0, gzBuffer, 4, compressed.Length);
            System.Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gzBuffer, 0, 4);
            return Convert.ToBase64String(gzBuffer);
        }

        public string Decompress(string compressedText)
        {
            byte[] gzBuffer = Convert.FromBase64String(compressedText);
            using (MemoryStream ms = new MemoryStream())
            {
                int msgLength = BitConverter.ToInt32(gzBuffer, 0);
                ms.Write(gzBuffer, 4, gzBuffer.Length - 4);

                byte[] buffer = new byte[msgLength];

                ms.Position = 0;
                using (GZipStream zip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    zip.Read(buffer, 0, buffer.Length);
                }

                return Encoding.UTF8.GetString(buffer);
            }

        }
    }
}
