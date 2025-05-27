using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace LegalDesktop.Services.Interfaces
{
    public interface ITokenSignerService : IDisposable
    {
        bool Initialize();
        bool Login(string pin);
         List<string> ListCertificates();
        byte[] SignPdf(byte[] pdfBytes, X509Certificate2 certificate, string pin);
    }
}
