namespace LegalDesktop.Services
{
    public static class AppConfig
    {
        public static string BaseApiUrl { get; set; } = "https://cncivil04.pjn.gov.ar/legaltrack/";
        public static string LoginEndpoint => $"{BaseApiUrl}api/Users/login";
        public static string DocumentationUrl { get; set; } = "https://ruta-a-la-documentacion";
        public static string SigPolicyUri { get; set; } = "http://pki.jgm.gov.ar/cps/cps.pdf";
        public static string Pkcs11LibraryPath { get; set; } = @"C:\\Windows\\SysWOW64\\aetpkss1.dll";
    }
}
