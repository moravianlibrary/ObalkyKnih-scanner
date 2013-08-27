using System;
using System.Text;
using System.Security.Cryptography;
using SobekCM.Bib_Package.MARC.Parsers;

namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Represents user settings, used for Serialization to persistent settings file,
    /// it should be used only for this purposes
    /// </summary>
    public class UserSettings
    {
        // Small random hash for harder decoding of password
        private const string randomHash = "Aqo3ojW6eQkTWLVI3FBvtBmKyOtYmYSiimhWFdf8";

        /// <summary>
        /// Login to ObalkyKnih
        /// </summary>
        public string UserName { get; set; }
        
        /// <summary>
        /// Password to ObalkyKnih
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Path to external image editor
        /// </summary>
        public string ExternalImageEditor { get; set; }

        /// <summary>
        /// Indicates whether XServer is default search engine
        /// </summary>
        public bool IsXServerEnabled { get; set; }

        /// <summary>
        /// Url to XServer
        /// </summary>
        public string XServerUrl { get; set; }

        /// <summary>
        /// Base where is searched unit in XServer
        /// </summary>
        public string XServerBase { get; set; }

        /// <summary>
        /// Indicates whether Z39.50 is default search engine
        /// </summary>
        public bool IsZ39Enabled { get; set; }

        /// <summary>
        /// URL of Z39.50 server
        /// </summary>
        public string Z39Server { get; set; }

        /// <summary>
        /// Port where is Z39.50 server available
        /// </summary>
        public int Z39Port { get; set; }

        /// <summary>
        /// Base where is searched unit in Z39.50
        /// </summary>
        public string Z39Base { get; set; }

        /// <summary>
        /// Code that represents requests through barcode in Z39.50 
        /// </summary>
        public int Z39BarcodeField { get; set; }

        /// <summary>
        /// Encoding used in Z39.50 server (utf8 / windows-1250)
        /// </summary>
        public Record_Character_Encoding Z39Encoding { get; set; }

        /// <summary>
        /// Login to Z39.50 (mostly not used)
        /// </summary>
        public string Z39UserName { get; set; }

        /// <summary>
        /// Password to Z39.50 (mostly not used)
        /// </summary>
        public string Z39Password { get; set; }

        /// <summary>
        /// Sigla of library
        /// </summary>
        public string Sigla { get; set; }

        // Returns decrypted version of password
        private string GetDecryptedPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return "";
            }
            string entropyString = randomHash + UserName ?? "";
            byte[] entropy = Encoding.Unicode.GetBytes(entropyString);
            byte[] encryptedData = Convert.FromBase64String(password);
            byte[] decryptedData = ProtectedData.Unprotect(encryptedData, entropy, DataProtectionScope.CurrentUser);
            return Encoding.Unicode.GetString(decryptedData);
        }

        // Returns encrypted version of password
        private string GetEncryptedPassword(string password)
        {
            if (password == null)
            {
                password = "";
            }
            string entropyString = randomHash + UserName ?? "";
            byte[] entropy = Encoding.Unicode.GetBytes(entropyString);
            byte[] encryptedData = ProtectedData.Protect(Encoding.Unicode.GetBytes(password), entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }

        private string EncodingToString(Encoding encoding)
        {
            return encoding.WebName;
        }

        // Converts string representation of Encoding to its Encoding object representation
        private Encoding StringToEncoding(string encodingString)
        {
            try
            {
                return Encoding.GetEncoding(encodingString);
            }
            catch(ArgumentException)
            {
                return Encoding.UTF8;
            }
        }

        /// <summary>
        /// Copies values into static Settings class
        /// </summary>
        public void SyncToSettings()
        {
            Settings.UserName = this.UserName;
            Settings.Password = GetDecryptedPassword(this.Password);
            Settings.IsXServerEnabled = this.IsXServerEnabled;
            Settings.XServerUrl = this.XServerUrl;
            Settings.XServerBase = this.XServerBase;
            Settings.IsZ39Enabled = this.IsZ39Enabled;
            Settings.Z39Server = this.Z39Server;
            Settings.Z39Port = this.Z39Port;
            Settings.Z39Base = this.Z39Base;
            Settings.Z39Encoding = this.Z39Encoding;
            Settings.Z39UserName = this.Z39UserName;
            Settings.Z39Password = this.Z39Password;
            Settings.Z39BarcodeField = this.Z39BarcodeField;
            Settings.Sigla = this.Sigla;
        }

        /// <summary>
        /// Copies values from static Settings class into this class
        /// </summary>
        public void SyncFromSettings()
        {
            this.UserName = Settings.UserName;
            this.Password = this.GetEncryptedPassword(Settings.Password);
            this.IsXServerEnabled = Settings.IsXServerEnabled;
            this.XServerUrl = Settings.XServerUrl;
            this.XServerBase = Settings.XServerBase;
            this.IsZ39Enabled = Settings.IsZ39Enabled;
            this.Z39Server = Settings.Z39Server;
            this.Z39Port = Settings.Z39Port;
            this.Z39Base = Settings.Z39Base;
            this.Z39Encoding = Settings.Z39Encoding;
            this.Z39UserName = Settings.Z39UserName;
            this.Z39Password = Settings.Z39Password;
            this.Z39BarcodeField = Settings.Z39BarcodeField;
            this.Sigla = Settings.Sigla;
        }
    }
}
