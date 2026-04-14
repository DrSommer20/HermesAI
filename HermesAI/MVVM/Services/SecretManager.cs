using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HermesAI.MVVM.Services
{
    public static class SecretManager
    {
        // Speicherort: C:\Users\[Name]\AppData\Roaming\HermesAI\secrets.dat
        private static readonly string SecretFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HermesAI",
            "secrets.dat");

        public static void SaveApiKey(string apiKey)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SecretFilePath));

            byte[] clearBytes = Encoding.UTF8.GetBytes(apiKey);

            //Nativ über Windows auf Basis des angemeldeten Users verschlüsseln (DataProtectionScope.CurrentUser)
            byte[] encryptedBytes = ProtectedData.Protect(clearBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllText(SecretFilePath, Convert.ToBase64String(encryptedBytes));
        }

        public static string LoadApiKey()
        {
            if (!File.Exists(SecretFilePath))
                return null;

            try
            {
                string encryptedText = File.ReadAllText(SecretFilePath);
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

                byte[] clearBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clearBytes);
            }
            catch
            {
                return null;
            }
        }
    }
}