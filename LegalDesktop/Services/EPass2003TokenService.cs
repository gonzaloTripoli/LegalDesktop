using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LegalDesktop.Services
{
    public class EPass2003TokenService
    {
        public List<string> ListCertificates()
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            var certs = store.Certificates
                .OfType<X509Certificate2>()
                .Where(c => c.HasPrivateKey && IsEPass2003Certificate(c))
                .Select(c => c.Subject)
                .ToList();

            return certs;
        }

        private bool IsEPass2003Certificate(X509Certificate2 cert)
        {
            if (cert == null || !cert.HasPrivateKey)
                return false;

            try
            {
    
      

                // Proveedor CNG moderno
                if (cert.GetRSAPrivateKey() is RSACng rsaCng)
                {
                    string providerName = rsaCng.Key?.Provider?.ToString() ?? "";
                    Debug.WriteLine($"[CNG] Proveedor: {providerName}");

                    return providerName.Contains("ePass2003", StringComparison.OrdinalIgnoreCase) ||
                           providerName.Contains("EnterSafe", StringComparison.OrdinalIgnoreCase) ||
                           providerName.Contains("Feitian", StringComparison.OrdinalIgnoreCase);
                }

                if (cert.GetECDsaPrivateKey() is ECDsaCng ecdsaCng)
                {
                    string providerName = ecdsaCng.Key?.Provider?.ToString() ?? "";
                    Debug.WriteLine($"[CNG ECDSA] Proveedor: {providerName}");

                    return providerName.Contains("ePass2003", StringComparison.OrdinalIgnoreCase) ||
                           providerName.Contains("EnterSafe", StringComparison.OrdinalIgnoreCase) ||
                           providerName.Contains("Feitian", StringComparison.OrdinalIgnoreCase);
                }

                Debug.WriteLine("No se pudo determinar el proveedor (ni CSP ni CNG)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al detectar el proveedor del certificado: {ex.Message}");
            }

            return false;
        }

        public byte[] SignPdf(byte[] pdfData, string certificateSubject, string reason = "Firma legal")
        {
            var cert = FindCertificateBySubject(certificateSubject);
            if (cert == null)
                throw new Exception($"No se encontró un certificado con subject: {certificateSubject}");

            if (!cert.HasPrivateKey)
                throw new Exception("El certificado no tiene clave privada asociada");

            var bcCert = new X509CertificateParser().ReadCertificate(cert.RawData);

            using var reader = new PdfReader(pdfData);
            using var ms = new MemoryStream();
            {
                var stamper = PdfStamper.CreateSignature(reader, ms, '\0', null, true);
                var appearance = stamper.SignatureAppearance;

                appearance.Reason = reason;
                appearance.Location = "Firmado digitalmente";
                appearance.Certificate = bcCert;
                appearance.SignatureCreator = "LegalDesktop";
                appearance.SetVisibleSignature(
                    new iTextSharp.text.Rectangle(100, 100, 300, 80),
                    reader.NumberOfPages,
                    $"Signature_{Guid.NewGuid():N}"
                );

                appearance.Layer2Text = $"Firmado por: {cert.Subject}\nFecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\nMotivo: {reason}";
                appearance.Acro6Layers = true;
                appearance.SignatureRenderingMode = PdfSignatureAppearance.RenderingMode.DESCRIPTION;

                // Modificación clave: Usar la implementación personalizada de IExternalSignature
                IExternalSignature signature = new X509Certificate2SignatureWrapper(cert, "SHA-256");

                MakeSignature.SignDetached(
                    appearance,
                    signature,
                    new[] { bcCert },
                    null, null, null, 12288,  // Cambiado de 12288 a 0 para mejor compatibilidad
                    CryptoStandard.CADES  // Cambiado de CADES a CMS
                );

                return ms.ToArray();
            }
        }

        private X509Certificate2 FindCertificateBySubject(string subject)
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            return store.Certificates
                .OfType<X509Certificate2>()
                .FirstOrDefault(c =>
                    c.HasPrivateKey &&
                    
                    c.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase));
        }
        public class X509Certificate2SignatureWrapper : IExternalSignature
        {
            private readonly X509Certificate2 _certificate;
            private readonly string _hashAlgorithm;

            public X509Certificate2SignatureWrapper(X509Certificate2 certificate, string hashAlgorithm)
            {
                _certificate = certificate;
                _hashAlgorithm = hashAlgorithm;
            }

            public string GetEncryptionAlgorithm()
            {
                return "RSA";
            }

            public string GetHashAlgorithm()
            {
                return _hashAlgorithm;
            }

            public byte[] Sign(byte[] message)
            {
                using (var privateKey = _certificate.GetRSAPrivateKey())
                {
                    if (privateKey == null)
                        throw new InvalidOperationException("No se puede acceder a la clave privada");

                    return privateKey.SignData(
                        message,
                        GetHashAlgorithmName(_hashAlgorithm),
                        RSASignaturePadding.Pkcs1
                    );
                }
            }

            private HashAlgorithmName GetHashAlgorithmName(string hashAlgorithm)
            {
                switch (hashAlgorithm)
                {
                    case "SHA-1": return HashAlgorithmName.SHA1;
                    case "SHA-256": return HashAlgorithmName.SHA256;
                    case "SHA-384": return HashAlgorithmName.SHA384;
                    case "SHA-512": return HashAlgorithmName.SHA512;
                    default: throw new ArgumentException($"Algoritmo hash no soportado: {hashAlgorithm}");
                }
            }
        }

    }
}
