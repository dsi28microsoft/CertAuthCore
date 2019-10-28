using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TodoApi.Models
{
    public class CertificateValidationConfig
    {
        public string Subject { get; set; }
        public string IssuerCN { get; set; }
        public string Thumbprint { get; set; }
    }
}
