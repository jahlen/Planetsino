using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Planetsino.Models
{
    public class DbClientInfo
    {
        public string Name;
        public bool IsPrimaryClient;
        public DocumentClient DocumentClient;
    }
}