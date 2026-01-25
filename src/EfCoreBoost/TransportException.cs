// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost
{

    public class TransportException : Exception
    {
        public TransportException(Exception inner, string url, string origin = "", string content = "", HttpStatusCode status = HttpStatusCode.OK) : base(inner.Message, inner)
        {
            TransportUrl = url;
            Status = status;
            Origin = origin;
            
            if (string.IsNullOrWhiteSpace(content))
                content = ExceptRecurse(inner);
            Content = content;
            if (status == HttpStatusCode.NotFound)
                IsNotFoundError = true;
            else if (status == HttpStatusCode.RequestTimeout)
                IsTimeOutError = true;
            else if (status == HttpStatusCode.Unauthorized || status == HttpStatusCode.Forbidden)
                IsDenyError = true;
            else 
                IsRemoteErrror = true;
        }

        public TransportException(string url, string origin = "", string content = "", HttpStatusCode status = HttpStatusCode.OK) : base()
        {
            TransportUrl = url;
            Status = status;
            Origin = origin;
            Content = content;

            if (status == HttpStatusCode.NotFound)
                IsNotFoundError = true;
            else if (status == HttpStatusCode.RequestTimeout)
                IsTimeOutError = true;
            else if (status == HttpStatusCode.Unauthorized || status == HttpStatusCode.Forbidden)
                IsDenyError = true;
            else
                IsRemoteErrror = true;
        }


        public string ExceptDetails()
        {
            return ExceptRecurse(this);
        }
        protected string ExceptRecurse(TransportException e)
        {
            string details = "Url: " + e.TransportUrl + "\r\n" + e.Message + " \r\n";
            if (!string.IsNullOrWhiteSpace(e.Origin))
                details += "Origin: " + e.Origin + " \r\n";
            if (e.InnerException != null)
                details += "Inner excepton: " + ExceptRecurse(e.InnerException);
            if (e.IsTimeOutError)
                details = "Service timeout, " + details;
            else if (e.IsNotFoundError)
                details = "Url not found, " + details;
            else if (e.IsRemoteErrror)
                details = "Remote service error, " + details;
            else if (e.IsDbError)
                details = "Database error, " + details;
            if (!string.IsNullOrWhiteSpace(Content))
                details += "Message: " + Content;
            return details;
        }

        protected string ExceptRecurse(Exception e)
        {
            string details = e.Message + " \r\n";
            if (e.InnerException != null)
                details += "Inner excepton: " + ExceptRecurse(e.InnerException);
            return details;
        }

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string TransportUrl { get; set; } = "";
        public bool IsNotFoundError { get; set; } = false;
        public bool IsTimeOutError { get; set; } = false;
        public bool IsRemoteErrror { get; set; } = false;
        public bool IsDbError { get; set; } = false;
        public bool IsDenyError { get; set; } = false;
        public string Origin { get; set; } = "";
        public string Content { get; set; } = "";
        
    }
}
