using System;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;


namespace ScannerClient_obalkyknih
{
    public class UpdateChecker
    {
        /// <summary>
        /// Path to downloaded update
        /// </summary>
        public string FilePath { get; private set; }
        
        /// <summary>
        /// Indicates whether current version is supported
        /// </summary>
        public bool IsSupportedVersion { get; private set; }
        
        /// <summary>
        /// Indicates whether newer version is available
        /// </summary>
        public bool IsUpdateAvailable { get; private set; }

        /// <summary>
        /// Gets latest available version number
        /// </summary>
        public Version AvailableVersion { get; private set; }

        /// <summary>
        /// Gets date of latest version
        /// </summary>
        public string AvailableVersionDate { get; private set; }

        /// <summary>
        /// Gets download link for latest available version
        /// </summary>
        public string DownloadLink { get; private set; }

        /// <summary>
        /// Gets checksum for latest available version setup file
        /// </summary>
        public string Checksum { get; private set; }

        /// <summary>
        /// Retrieves version from Assembly (can be set in Properties->Application->Assembly Version)
        /// </summary>
        public Version Version {
            get
            {
                return Assembly.GetEntryAssembly().GetName().Version;
            }
        }

        /// <summary>
        /// Retrieves all possible information about updates and saves them to properties
        /// </summary>
        public void RetrieveUpdateInfo()
        {
            try
            {
                XDocument xDocument = XDocument.Load(Settings.UpdateServer.TrimEnd('/') + "/update-info.xml");

                //check if current version is in list of unsupported versions
                var unsupportedVersionElements = xDocument.Root.Element("unsupported-versions").Elements("version");
                int count = unsupportedVersionElements.Count(
                    el => (int.Parse(el.Element("major").Value) == this.Version.Major && int.Parse(el.Element("minor").Value) == this.Version.Minor)
                    );
                if (count == 0)
                {
                    this.IsSupportedVersion = true;
                }
                else
                {
                    this.IsSupportedVersion = false;
                }

                //check if update is available
                var latestversionAvailable = xDocument.Root.Element("latest-version").Element("version");
                int major = int.Parse(latestversionAvailable.Element("major").Value);
                int minor = int.Parse(latestversionAvailable.Element("minor").Value);
                this.AvailableVersion = new Version(major, minor);
                this.DownloadLink = latestversionAvailable.Element("download").Value;
                this.AvailableVersionDate = latestversionAvailable.Element("date").Value;
                this.Checksum = latestversionAvailable.Element("checksum").Value;
                this.FilePath = Settings.TemporaryFolder.TrimEnd('\\') + "\\" + this.DownloadLink.Substring(this.DownloadLink.LastIndexOf('/') + 1);

                if (!this.IsSupportedVersion || Version.CompareTo(AvailableVersion) < 0)
                {
                    this.IsUpdateAvailable = true;
                }
            }
            catch (Exception ex)
            {
                if (ex is ArgumentNullException || ex is FormatException || ex is OverflowException)
                {
                    throw new FormatException("Wrong format of updateInfo on server.");
                }
                else
                {
                    throw;
                }
            }
        }

    }
}
