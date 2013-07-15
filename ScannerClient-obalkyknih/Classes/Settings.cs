using System;
using System.Xml.Serialization;
using System.IO;
using System.Windows;
using System.Text;


namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Represents user and application settings
    /// </summary>
    public static class Settings
    {
        #region USER SETTINGS

        /// <summary>
        /// Login to system ObalkyKnih.cz
        /// </summary>
        public static string UserName { get; set; }

        /// <summary>
        /// Password to system ObalkyKnih.cz
        /// </summary>
        public static string Password { get; set; }
        
        /// <summary>
        /// Path to external image editor used for optional editing of cover and toc images
        /// </summary>
        public static string ExternalImageEditor { get; set; }

        /// <summary>
        /// Indicates that X-Server is default search engine
        /// </summary>
        public static bool IsXServerEnabled { get; set; }

        /// <summary>
        /// URL of X-Server
        /// </summary>
        public static string XServerUrl { get; set; }

        /// <summary>
        /// Database which contains searched record
        /// </summary>
        public static string XServerBase { get; set; }

        /// <summary>
        /// Indicates that Z39.50 is default search engine
        /// </summary>
        public static bool IsZ39Enabled { get; set; }

        /// <summary>
        /// URL of Z39.50 server
        /// </summary>
        public static string Z39Server { get; set; }

        /// <summary>
        /// Port on which is Z39.50 server available
        /// </summary>
        public static int Z39Port { get; set; }

        /// <summary>
        /// Database that contains searched record
        /// </summary>
        public static string Z39Base { get; set; }

        /// <summary>
        /// Encoding of Z39.50 server
        /// </summary>
        public static Encoding Z39Encoding { get; set; }

        /// <summary>
        /// Optional login to Z39.50 server
        /// </summary>
        public static string Z39UserName { get; set; }

        /// <summary>
        /// Optional password to Z39.50 server
        /// </summary>
        public static string Z39Password { get; set; }

        /// <summary>
        /// Search number for barcode attribute in Z39.50
        /// </summary>
        public static int Z39BarcodeField { get; set; }

        /// <summary>
        /// Sigla of library
        /// </summary>
        public static string Sigla { get; set; }
        #endregion

        #region APPLICATION SETTINGS
        
        // Path to file with settings
        // This shouldn't change, if changed, it has to be changed also in installer
        private static string SettingsFile
        {
            get
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    + @"\ObalkyKnih-scanner\settings.config";
            }
        }
        
        /// <summary>
        /// URL of folder containing update-info.xml file
        /// </summary>
        public const string UpdateServer = "https://obalkyknih.cz/obalkyknih-scanner";

        /// <summary>
        /// URL of import function on obalkyknih.cz
        /// </summary>
        public const string ImportLink = "https://obalkyknih.cz/api/import";

        /// <summary>
        /// Returns path to temporary folder, where are stored images opened in external editor
        /// and downloaded updates
        /// </summary>
        public static string TemporaryFolder { get { return System.IO.Path.GetTempPath(); } }
        
        /// <summary>
        /// Tag of Title field in Marc21
        /// </summary>
        public const int MetadataTitleField = 245;

        /// <summary>
        /// Tag of Title subfield in Marc21
        /// </summary>
        public const char MetadataTitleSubfield = 'a';

        /// <summary>
        /// Tag of Additional Title subfield in Marc21
        /// </summary>
        public const char MetadataTitleSubfield2 = 'b';

        /// <summary>
        /// Tag of Author field in Marc21
        /// </summary>
        public const int MetadataAuthorField = 100;

        /// <summary>
        /// Tag of Author subfield in Marc21
        /// </summary>
        public const char MetadataAuthorSubfield = 'a';

        /// <summary>
        /// Tag of Additional Authors field in Marc21
        /// </summary>
        public const int MetadataAuthorField2 = 700;

        /// <summary>
        /// Tag of Additional Authors subfield in Marc21
        /// </summary>
        public const char MetadataAuthorSubfield2 = 'a';

        /// <summary>
        /// Tag of Publish Year field in Marc21
        /// </summary>
        public const int MetadataPublishYearField = 260;

        /// <summary>
        /// Tag of Publih Year subfield in Marc21
        /// </summary>
        public const char MetadataPublishYearSubfield = 'c';

        /// <summary>
        /// Tag of ISBN field in Marc21
        /// </summary>
        public const int MetadataIsbnField = 20;

        /// <summary>
        /// Tag of ISBN subfield in Marc21
        /// </summary>
        public const char MetadataIsbnSubfield = 'a';

        /// <summary>
        /// Tag of ISSN field in Marc21
        /// </summary>
        public const int MetadataIssnField = 22;

        /// <summary>
        /// Tag of ISSN subfield in Marc21
        /// </summary>
        public const char MetadataIssnSubfield = 'a';

        /// <summary>
        /// Tag of ČNB field in Marc21
        /// </summary>
        public const int MetadataCnbField = 15;

        /// <summary>
        /// Tag of ČNB subfield in Marc21
        /// </summary>
        public const char MetadataCnbSubfield = 'a';

        /// <summary>
        /// Tag of OCLC field in Marc21
        /// </summary>
        public const int MetadataOclcField = 35;

        /// <summary>
        /// Tag of OCLC subfield in Marc21
        /// </summary>
        public const char MetadataOclcSubfield = 'a';

        /// <summary>
        /// PPI used for scanning of cover
        /// </summary>
        public const int CoverDPI = 300;

        /// <summary>
        /// Color type used for scanning of cover (Color/Grey/Black and White)
        /// </summary>
        public const ScanColor CoverScanType = ScanColor.Color;

        /// <summary>
        /// PPI used for scanning of cover
        /// </summary>
        public const int TocDPI = 300;

        /// <summary>
        /// Color type used for scanning of cover (Color/Grey/Black and White)
        /// </summary>
        public const ScanColor TocScanType = ScanColor.Color;
        #endregion

        /// <summary>
        /// Saves settings to disk
        /// </summary>
        public static void PersistSettings()
        {
            try
            {
                Directory.CreateDirectory(SettingsFile.Remove(SettingsFile.LastIndexOf('\\')));
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(UserSettings));
                UserSettings us = new UserSettings();
                using (FileStream fs = new FileStream(SettingsFile, FileMode.Create))
                {
                    us.SyncFromSettings();
                    xmlSerializer.Serialize(fs, us);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Zápis nastavení se nezdařil.", "Nastavení neuloženo", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Reloads settings from values saved on disk
        /// </summary>
        public static void ReloadSettings()
        {
            if (!File.Exists(SettingsFile))
            {
                MessageBox.Show("Soubor s uživatelskými nastaveními neexistuje.",
                    "Nastavení neexistují", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(UserSettings));
                using (TextReader xml = new StreamReader(SettingsFile))
                {
                    UserSettings us = xmlSerializer.Deserialize(xml) as UserSettings;
                    us.SyncToSettings();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Soubor s uživatelskými nastaveními je poškozen.",
                    "Poškozené nastavení", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
