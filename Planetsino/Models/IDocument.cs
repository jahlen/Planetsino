using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Planetsino.Models
{
    public interface IDocument
    {
        /// <summary>
        /// Make sure to add a [JsonIgnore] in front of this member in the class implementing this interface
        /// </summary>
        string ClientName { get; set; }
    }
}