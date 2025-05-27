using LegalDesktop.Services;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

internal class TokenDetectorService
{
    public enum TokenType
    {
        None,
        StarSign,
        EPass2003
    }

    public TokenType DetectAvailableToken()
    {
        if (IsEPass2003Connected())
        {
            return TokenType.EPass2003;
        }

        if (IsStarSignConnected())
        {
            return TokenType.StarSign;
        }

        return TokenType.None;
    }

    private bool IsEPass2003Connected()
    {
        try
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                return store.Certificates
                    .OfType<X509Certificate2>()
                    .Any(c => c.HasPrivateKey && IsEPass2003Certificate(c));
            }
        }
        catch
        {
            return false;
        }
    }

    private bool IsStarSignConnected()
    {
        try
        {
            // Implementación específica para detectar StarSign
            if (!File.Exists(StarSignTokenService.Pkcs11LibraryPath))
                return false;

            var factories = new Pkcs11InteropFactories();
            using (var pkcs11Library = factories.Pkcs11LibraryFactory.LoadPkcs11Library(
                factories,
                StarSignTokenService.Pkcs11LibraryPath,
                AppType.SingleThreaded))
            {
                var slots = pkcs11Library.GetSlotList(SlotsType.WithTokenPresent);
                return slots.Count > 0;
            }
        }
        catch
        {
            return false;
        }
    }
    private bool IsEPass2003Certificate(X509Certificate2 cert)
    {
        if (cert == null || !cert.HasPrivateKey)
        {
            Debug.WriteLine("Certificado nulo o sin clave privada");
            return false;
        }

        try
        {
       
    

            // 2. Proveedor CNG moderno
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


}