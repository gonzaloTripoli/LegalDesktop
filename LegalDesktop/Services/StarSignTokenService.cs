using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Net.Pkcs11Interop.HighLevelAPI.Factories;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace LegalDesktop.Services
{
    public class StarSignTokenService : IDisposable
    {
        public const string Pkcs11LibraryPath = @"C:\Windows\SysWOW64\aetpkss1.dll";
        private readonly Pkcs11InteropFactories _factories = new Pkcs11InteropFactories();
        private IPkcs11Library _pkcs11Library;
        private ISlot _slot;
        private ISession _session;

        public bool Initialize()
        {
            if (!File.Exists(Pkcs11LibraryPath))
                throw new FileNotFoundException("DLL de StarSign no encontrada.");

            _pkcs11Library = _factories.Pkcs11LibraryFactory.LoadPkcs11Library(
                _factories,
                Pkcs11LibraryPath,
                AppType.SingleThreaded
            );

            var slots = _pkcs11Library.GetSlotList(SlotsType.WithTokenPresent);
            if (slots.Count == 0)
            {
                System.Windows.MessageBox.Show("Recuerde ingresar el token correspondiente.", "Falta de token", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            _slot = slots[0];
            return true;
        }

        public bool Login(string pin)
        {
            try
            {
                _session = _slot.OpenSession(SessionType.ReadWrite);
                _session.Login(CKU.CKU_USER, pin);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }

        }

        public List<string> ListCertificates()
        {
            var attrFactory = _factories.ObjectAttributeFactory;
            var searchTemplate = new List<IObjectAttribute>
            {
                attrFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE)
            };

            _session.FindObjectsInit(searchTemplate);
            var certs = _session.FindObjects(10);
            _session.FindObjectsFinal();

            return certs.Select(cert =>
            {
                var labelAttr = _session.GetAttributeValue(cert, new List<CKA> { CKA.CKA_LABEL });
                return labelAttr[0].GetValueAsString();
            }).ToList();
        }


        private static class AdobeExtension
        {
            public static readonly PdfName ADBE = new PdfName("ADBE");
        }
        public byte[] SignPdf(byte[] pdfData, string certificateLabel, string pin, string reason = "Firma legal")
        {
     
            var certificate = FindCertificate(certificateLabel)
                ?? throw new Exception($"Certificado '{certificateLabel}' no encontrado.");

            var certValue = _session.GetAttributeValue(certificate, new List<CKA> { CKA.CKA_VALUE });
            var x509Cert = new X509CertificateParser().ReadCertificate(certValue[0].GetValueAsByteArray());

            using (var reader = new PdfReader(pdfData))
            using (var ms = new MemoryStream())
            {
                var stamper = PdfStamper.CreateSignature(reader, ms, '\0', null, true);
                var appearance = stamper.SignatureAppearance;

                appearance.Reason = reason;
                appearance.Location = "Firmado digitalmente";
                appearance.Certificate = x509Cert;
                appearance.SignatureCreator = "LegalDesktop";

                string signatureFieldName = $"Signature_{Guid.NewGuid().ToString("N")}";

                appearance.SetVisibleSignature(
                    new iTextSharp.text.Rectangle(100, 100, 300, 80),
                    reader.NumberOfPages,
                    signatureFieldName
                );
                appearance.Layer2Text = $"Firmado por: {x509Cert.SubjectDN}\nFecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\nMotivo: {reason}";
                appearance.Acro6Layers = true;
                appearance.SignatureRenderingMode = PdfSignatureAppearance.RenderingMode.DESCRIPTION;

                stamper.Writer.SetPdfVersion(PdfWriter.PDF_VERSION_1_7);

                var signatureDic = new PdfDictionary();

                var sigPolicy = new PdfDictionary();
                sigPolicy.Put(new PdfName("SigPolicyId"), new PdfString("2.16.32.1.1.3")); // OID
                sigPolicy.Put(new PdfName("SigPolicyDescription"), new PdfString("Política de Firma Digital Argentina"));
                sigPolicy.Put(new PdfName("SigPolicyUri"), new PdfString("http://pki.jgm.gov.ar/cps/cps.pdf"));

                signatureDic.Put(PdfName.FILTER, PdfName.ADOBE_PPKLITE);
                signatureDic.Put(PdfName.SUBFILTER, PdfName.ADBE_PKCS7_DETACHED);
                signatureDic.Put(new PdfName("SigPolicy"), sigPolicy); // Clave crítica

                appearance.CryptoDictionary = signatureDic;

              

                // 6. Firma Digital
                var signature = new TokenSignature(_session, certificate, pin);
                MakeSignature.SignDetached(
                    appearance,
                    signature,
                    new[] { x509Cert },
                    null, // CRLs
                    null, // OCSP
                    null, // TSA
                    12288, // Espacio reservado para /Contents
                    CryptoStandard.CADES
                );

       
                return ms.ToArray();
            }
        }

        private class TokenSignature : IExternalSignature
        {
            private readonly ISession _session;
            private readonly IObjectHandle _privateKey;
            private readonly string _pin;

            public TokenSignature(ISession session, IObjectHandle certificate, string pin)
            {
                _session = session;
                _pin = pin;

                // Obtener clave privada asociada al certificado
                var certId = _session.GetAttributeValue(certificate, new List<CKA> { CKA.CKA_ID })[0].GetValueAsByteArray();

                var searchTemplate = new List<IObjectAttribute>
                {
                    _session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY),
                    _session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, certId)
                };

                _session.FindObjectsInit(searchTemplate);
                var keys = _session.FindObjects(1);
                _session.FindObjectsFinal();

                if (keys.Count == 0)
                    throw new Exception("No se encontró clave privada asociada");

                _privateKey = keys[0];
            }

            public string GetEncryptionAlgorithm() => "RSA";
            public string GetHashAlgorithm() => "SHA-256";

            public byte[] Sign(byte[] message)
            {
                try
                {
                    // Primero intentar con SHA256
                    var mechanism = _session.Factories.MechanismFactory.Create(CKM.CKM_SHA256_RSA_PKCS);
                    return _session.Sign(mechanism, _privateKey, message);
                }
                catch
                {
                    try
                    {
                        // Fallback a PKCS#1 v1.5 básico
                        var mechanism = _session.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS);
                        return _session.Sign(mechanism, _privateKey, message);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error al firmar: {ex.Message}");
                    }
                }
            }
        }

        private IObjectHandle FindCertificate(string label)
        {
            var attrFactory = _factories.ObjectAttributeFactory;
            var searchTemplate = new List<IObjectAttribute>
            {
                attrFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE),
                attrFactory.Create(CKA.CKA_LABEL, label)
            };

            _session.FindObjectsInit(searchTemplate);
            var certs = _session.FindObjects(1);
            _session.FindObjectsFinal();

            return certs.Count > 0 ? certs[0] : null;
        }

        public void Dispose()
        {
            _session?.Logout();
            _session?.Dispose();
            _pkcs11Library?.Dispose();
        }
    }
}