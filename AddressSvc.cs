﻿namespace AvaTaxCalcREST
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Xml.Serialization;

    public enum AddressType
    {
        F, // Firm or company address
        G, // General Delivery address
        H, // High-rise or business complex
        P, // PO box address
        R, // Rural route address
        S // Street or residential address
    }

    // Request for address/validate is parsed into the URI query parameters.
    [Serializable]
    public partial class ValidateResult
    {
        public Address Address { get; set; }

        public SeverityLevel ResultCode { get; set; }

        public Message[] Messages { get; set; }
    }

    [Serializable]
    public partial class Address
    {
        // Address can be determined for tax calculation by Line1, City, Region, PostalCode, Country OR Latitude/Longitude OR TaxRegionId
        public string AddressCode { get; set; } // Input for GetTax only, not by address validation

        public string Line1 { get; set; }

        public string Line2 { get; set; }

        public string Line3 { get; set; }

        public string City { get; set; }

        public string Region { get; set; }

        public string PostalCode { get; set; }

        public string Country { get; set; }

        public string County { get; set; } // Output for ValidateAddress only

        public string FipsCode { get; set; } // Output for ValidateAddress only

        public string CarrierRoute { get; set; } // Output for ValidateAddress only

        public string PostNet { get; set; } // Output for ValidateAddress only

        public AddressType? AddressType { get; set; } // Output for ValidateAddress only

        public decimal? Latitude { get; set; } // Input for GetTax only

        public decimal? Longitude { get; set; } // Input for GetTax only

        public string TaxRegionId { get; set; } // Input for GetTax only
    }

    public partial class AddressSvc
    {
        private static string accountNum;
        private static string license;
        private static string svcURL;

        public AddressSvc(string accountNumber, string licenseKey, string serviceURL)
        {
            accountNum = accountNumber;
            license = licenseKey;
            svcURL = serviceURL.TrimEnd('/') + "/1.0/";
        }

        public ValidateResult Validate(Address address)
        {
            // Convert input address data to query string
            string querystring = string.Empty;
            querystring += (address.Line1 == null) ? string.Empty : "Line1=" + address.Line1.Replace(" ", "+");
            querystring += (address.Line2 == null) ? string.Empty : "&Line2=" + address.Line2.Replace(" ", "+");
            querystring += (address.Line3 == null) ? string.Empty : "&Line3=" + address.Line3.Replace(" ", "+");
            querystring += (address.City == null) ? string.Empty : "&City=" + address.City.Replace(" ", "+");
            querystring += (address.Region == null) ? string.Empty : "&Region=" + address.Region.Replace(" ", "+");
            querystring += (address.PostalCode == null) ? string.Empty : "&PostalCode=" + address.PostalCode.Replace(" ", "+");
            querystring += (address.Country == null) ? string.Empty : "&Country=" + address.Country.Replace(" ", "+");

            // Call the service
            Uri webAddress = new Uri(svcURL + "address/validate.xml?" + querystring);
            HttpWebRequest request = WebRequest.Create(webAddress) as HttpWebRequest;
            request.Headers.Add(HttpRequestHeader.Authorization, "Basic " + Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(accountNum + ":" + license)));
            request.Method = "GET";

            ValidateResult result = new ValidateResult();
            try
            {
                WebResponse response = request.GetResponse();
                XmlSerializer r = new XmlSerializer(result.GetType());
                result = (ValidateResult)r.Deserialize(response.GetResponseStream());
                address = result.Address; // If the address was validated, take the validated address.
            }
            catch (WebException ex)
            {
                Stream responseStream = ((HttpWebResponse)ex.Response).GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                string responseString = reader.ReadToEnd();

                // The service returns some error messages in JSON for authentication/unhandled errors.
                if (responseString.StartsWith("{")  || responseString.StartsWith("["))
                {
                    result = new ValidateResult();
                    result.ResultCode = SeverityLevel.Error;
                    Message msg = new Message();
                    msg.Severity = result.ResultCode;
                    msg.Summary = "The request was unable to be successfully serviced, please try again or contact Customer Service.";
                    msg.Source = "Avalara.Web.REST";
                    if (!((HttpWebResponse)ex.Response).StatusCode.Equals(HttpStatusCode.InternalServerError))
                    {
                        msg.Summary = "The user or account could not be authenticated.";
                        msg.Source = "Avalara.Web.Authorization"; 
                    }

                    result.Messages = new Message[1] { msg };
                }
                else
                {
                    XmlSerializer r = new XmlSerializer(result.GetType());
                    byte[] temp = Encoding.ASCII.GetBytes(responseString);
                    MemoryStream stream = new MemoryStream(temp);
                    result = (ValidateResult)r.Deserialize(stream); // Inelegant, but the deserializer only takes streams, and we already read ours out.
                }
            }

            return result;
        }
    }
}