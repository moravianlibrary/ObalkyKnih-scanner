﻿using System;
using System.Xml.Serialization;
using System.IO;
using System.Windows;
using System.Text;
using SobekCM.Bib_Package.MARC.Parsers;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;


namespace ScannerClient_obalkyknih
{
    /// <summary>Represents user and application settings</summary>
    public static class Settings
    {
        #region USER SETTINGS (SAVED IN REGISTRY)

        /// <summary>Sets given value for registry key with given name</summary>
        /// <param name="isAdminForced">indicates that the previous value was enforced by admin and can't be changed</param>
        /// <param name="name">name associated with value</param>
        /// <param name="value">value</param>
        /// <param name="rvk">type of registry value</param>
        private static void SetRegistryValue(bool isAdminForced, string name, object value, RegistryValueKind rvk)
        {
            if (!isAdminForced)
            {
                UserSettingsRegistryKey.SetValue(name, value, rvk);
            }
        }

        /// <summary>Receives DWORD registry value associated with specified name retrieved from admin or user key</summary>
        /// <param name="isAdminForced">indicates that user key value should be ignored if any</param>
        /// <param name="name">name associated with value</param>
        /// <returns>numeric value associated with given name</returns>
        private static int GetIntRegistryValue(bool isAdminForced, string name)
        {
            if (isAdminForced)
            {
                return (int)AdminSettingsRegistryKey.GetValue(name, 0);
            }
            return (int)UserSettingsRegistryKey.GetValue(name, 0);
        }

        /// <summary>Returns REG_SZ registry value with specified name retrieved from admin or user key</summary>
        /// <param name="isAdminForced">indicates that user key value should be ignored if any</param>
        /// <param name="name">name associated with value</param>
        /// <returns>string value associated with given name</returns>
        private static string GetStringRegistryValue(bool isAdminForced, string name)
        {
            if (isAdminForced)
            {
                return AdminSettingsRegistryKey.GetValue(name, null) as string;
            }
            return UserSettingsRegistryKey.GetValue(name, null) as string;
        }

        /// <summary>Login to system ObalkyKnih.cz</summary>
        internal static string UserName
        {
            get
            {
                return GetStringRegistryValue(false, "Username");
            }
            set
            {
                SetRegistryValue(false, "Username", value, RegistryValueKind.String);
            }
        }

        /// <summary>Password to system ObalkyKnih.cz</summary>
        internal static string Password
        {
            get
            {
                byte[] cryptoPassword = (byte[])UserSettingsRegistryKey.GetValue("Password", new byte[0]);
                if (cryptoPassword.Length == 0)
                {
                    return "";
                }
                byte[] entropy = Encoding.Unicode.GetBytes((UserName ?? "") + "Aqo3ojW6eQkTWLVI3FBvtBmKyOtYmYSiimhWFdf8");
                string password = "";
                try
                {
                    password = Encoding.Unicode.GetString(ProtectedData.Unprotect(cryptoPassword, entropy, DataProtectionScope.CurrentUser));
                }
                catch (Exception)
                {
                    MessageBoxDialogWindow.Show("Chyba dešifrování  hesla", "Nepovedlo se dešifrovat heslo.",
                        "OK", MessageBoxDialogWindow.Icons.Error);
                }
                return password;
            }
            set
            {
                byte[] password = Encoding.Unicode.GetBytes(value ?? "");
                byte[] entropy = Encoding.Unicode.GetBytes((UserName ?? "") + "Aqo3ojW6eQkTWLVI3FBvtBmKyOtYmYSiimhWFdf8");
                try
                {
                    byte[] cryptoPassword = ProtectedData.Protect(password, entropy, DataProtectionScope.CurrentUser);
                    SetRegistryValue(false, "Password", cryptoPassword, RegistryValueKind.Binary);
                }
                catch (Exception)
                {
                    MessageBoxDialogWindow.Show("Chyba šifrování  hesla", "Nepovedlo se uložit heslo.",
                        "OK", MessageBoxDialogWindow.Icons.Error);
                }
            }
        }

        /// <summary>Indicates that X-Server is default search engine</summary>
        internal static bool IsXServerEnabled
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(IsAdminIsXServerEnabled, "UseXServer"));
            }
            set
            {
                SetRegistryValue(IsAdminIsXServerEnabled, "UseXServer", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>Indicates that X-Server choice was made by admin and can't be changed in application</summary>
        internal static bool IsAdminIsXServerEnabled
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("useXServer", null) != null;
            }
        }

        /// <summary>URL of X-Server</summary>
        internal static string XServerUrl
        {
            get
            {
                return GetStringRegistryValue(IsAdminXServerUrl, "XServerUrl");
            }
            set
            {
                SetRegistryValue(IsAdminXServerUrl, "XServerUrl", value, RegistryValueKind.String);
            }
        }

        /// <summary>Indicates that X-Server URL was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminXServerUrl
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("XServerUrl", null) != null;
            }
        }

        /// <summary>Database which contains searched record</summary>
        internal static string XServerBase
        {
            get
            {
                return GetStringRegistryValue(IsAdminXServerBase, "XServerBase");
            }
            set
            {
                SetRegistryValue(IsAdminXServerBase, "XServerBase", value, RegistryValueKind.String);
            }
        }

        /// <summary>Indicates that X-Server base was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminXServerBase
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("XServerBase", null) != null;
            }
        }

        /// <summary>Indicates that Z39.50 is default search engine</summary>
        internal static bool IsZ39Enabled
        {
            get
            {
                return !IsXServerEnabled;
            }
            set
            {
                IsXServerEnabled = !value;
            }
        }

        /// <summary>Indicates that choice of Z39 was made by admin and can't be changed in application</summary>
        internal static bool IsAdminIsZ39Enabled
        {
            get
            {
                return AdminSettingsRegistryKey != null && IsAdminIsXServerEnabled;
            }
        }

        /// <summary>URL of Z39.50 server</summary>
        internal static string Z39ServerUrl
        {
            get
            {
                return GetStringRegistryValue(IsAdminZ39ServerUrl, "Z39ServerUrl");
            }
            set
            {
                SetRegistryValue(IsAdminZ39ServerUrl, "Z39ServerUrl", value, RegistryValueKind.String);
            }
        }

        /// <summary>Indicates that URL of Z39.50 server was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminZ39ServerUrl
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("Z39ServerUrl", null) != null;
            }
        }

        /// <summary>Port on which is Z39.50 server available</summary>
        internal static int Z39Port
        {
            get
            {
                return GetIntRegistryValue(IsAdminZ39Port, "Z39Port");
            }
            set
            {
                SetRegistryValue(IsAdminZ39Port, "Z39Port", value, RegistryValueKind.DWord);
            }
        }

        /// <summary>Indicates that port of Z39.50 server was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminZ39Port
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("Z39Port", null) != null;
            }
        }

        /// <summary>Database that contains searched record</summary>
        internal static string Z39Base
        {
            get
            {
                return GetStringRegistryValue(IsAdminZ39Base, "Z39Base");
            }
            set
            {
                SetRegistryValue(IsAdminZ39Base, "Z39Base", value, RegistryValueKind.String);
            }
        }

        /// <summary>Indicates that base for Z39.50 was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminZ39Base
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("Z39Base", null) != null;
            }
        }

        /// <summary>Encoding of Z39.50 server</summary>
        internal static Record_Character_Encoding Z39Encoding
        {
            get
            {
                string enc = GetStringRegistryValue(IsAdminZ39Encoding, "Z39Encoding");
                switch (enc)
                {
                    case "MARC":
                        return Record_Character_Encoding.MARC;
                    case "WINDOWS-1250":
                        return Record_Character_Encoding.Windows1250;
                    case "UNICODE":
                        return Record_Character_Encoding.Unicode;
                    default:
                        return Record_Character_Encoding.UNRECOGNIZED;
                }
            }
            set
            {
                string encoding = "";
                switch (value)
                {
                    case Record_Character_Encoding.MARC:
                        encoding = "MARC";
                        break;
                    case Record_Character_Encoding.Windows1250:
                        encoding = "WINDOWS-1250";
                        break;
                    case Record_Character_Encoding.Unicode:
                        encoding = "UNICODE";
                        break;
                    default:
                        encoding = "UNRECOGNIZED";
                        break;
                }
                SetRegistryValue(IsAdminZ39Encoding, "Z39Encoding", encoding, RegistryValueKind.String);
            }
        }

        /// <summary>Indicates that encoding for Z39.50 was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminZ39Encoding
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("Z39Encoding", null) != null;
            }
        }

        /// <summary>Optional login to Z39.50 server</summary>
        internal static string Z39UserName
        {
            get
            {
                return GetStringRegistryValue(IsAdminZ39UserName, "Z39UserName");
            }
            set
            {
                SetRegistryValue(IsAdminZ39UserName, "Z39UserName", value, RegistryValueKind.String);
            }
        }

        /// <summary>Indicates that user name for Z39.50 was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminZ39UserName
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("Z39UserName", null) != null;
            }
        }

        /// <summary>Optional password to Z39.50 server</summary>
        internal static string Z39Password
        {
            get
            {
                return GetStringRegistryValue(IsAdminZ39Password, "Z39Password");
            }
            set
            {
                SetRegistryValue(IsAdminZ39Password, "Z39Password", value, RegistryValueKind.String);
            }
        }

        /// <summary>Indicates that password for Z39.50 was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminZ39Password
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("Z39Password", null) != null;
            }
        }

        /// <summary>Search number for barcode attribute in Z39.50</summary>
        internal static int Z39BarcodeField
        {
            get
            {
                return GetIntRegistryValue(IsAdminZ39BarcodeField, "Z39BarcodeField");
            }
            set
            {
                SetRegistryValue(IsAdminZ39BarcodeField, "Z39BarcodeField", value, RegistryValueKind.DWord);
            }
        }

        /// <summary>Indicates that numeric code of barcode field for Z39.50 was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminZ39BarcodeField
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("Z39BarcodeField", null) != null;
            }
        }

        /// <summary>Sigla of library</summary>
        internal static string Sigla
        {
            get
            {
                return GetStringRegistryValue(IsAdminSigla, "Sigla");
            }
            set
            {
                SetRegistryValue(IsAdminSigla, "Sigla", value, RegistryValueKind.String);
            }
        }

        /// <summary>Indicates that sigla was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminSigla
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("Sigla", null) != null;
            }
        }

        /// <summary>Base of record - can be different than one in Z39.50 (MZK01-UTF vs MZK01)</summary>
        internal static string Base
        {
            get
            {
                return GetStringRegistryValue(IsAdminBase, "Base");
            }
            set
            {
                SetRegistryValue(IsAdminBase, "Base", value, RegistryValueKind.String);
            }
        }

        /// <summary>Indicates that base was filled by admin and can't be changed in application</summary>
        internal static bool IsAdminBase
        {
            get
            {
                return AdminSettingsRegistryKey != null && AdminSettingsRegistryKey.GetValue("Base", null) != null;
            }
        }

        /// <summary>Disables updates by ADMIN (can be set only in registry and only with admin rights on local machine)</summary>
        internal static bool DisableUpdate
        {
            get
            {
                return AdminSettingsRegistryKey != null && Convert.ToBoolean(GetIntRegistryValue(true, "DisableUpdate"));
            }
        }

        /// <summary>Forces updates - only newest version can be used (can be set only in registry and only with admin rights on local machine)</summary>
        internal static bool ForceUpdate
        {
            get
            {
                return AdminSettingsRegistryKey != null && Convert.ToBoolean(GetIntRegistryValue(true, "ForceUpdate"));
            }
        }

        /// <summary>Ignores missing author and publish year</summary>
        internal static bool DisableMissingAuthorYearNotification
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(false, "DisableMissingAuthorYearNotification"));
            }
            set
            {
                SetRegistryValue(false, "DisableMissingAuthorYearNotification", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>Ignores missing cover image</summary>
        internal static bool DisableWithoutCoverNotification
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(false, "DisableWithoutCoverNotification"));
            }
            set
            {
                SetRegistryValue(false, "DisableWithoutCoverNotification", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>Ignores missing toc image</summary>
        internal static bool DisableWithoutTocNotification
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(false, "DisableWithoutTocNotification"));
            }
            set
            {
                SetRegistryValue(false, "DisableWithoutTocNotification", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>deletes cover image without confirmation</summary>
        internal static bool DisableCoverDeletionNotification
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(false, "DisableCoverDeletionNotification"));
            }
            set
            {
                SetRegistryValue(false, "DisableCoverDeletionNotification", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>deletes toc image without confirmation</summary>
        internal static bool DisableTocDeletionNotification
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(false, "DisableTocDeletionNotification"));
            }
            set
            {
                SetRegistryValue(false, "DisableTocDeletionNotification", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>do not show custom identifier notification</summary>
        internal static bool DisableCustomIdentifierNotification
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(false, "DisableCustomIdentifierNotification"));
            }
            set
            {
                SetRegistryValue(false, "DisableCustomIdentifierNotification", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>closes application without confirmation</summary>
        internal static bool DisableClosingConfirmation
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(false, "DisableClosingConfirmation"));
            }
            set
            {
                SetRegistryValue(false, "DisableClosingConfirmation", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>Always downloads updates without confirmation</summary>
        internal static bool AlwaysDownloadUpdates
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(false, "AlwaysDownloadUpdates"));
            }
            set
            {
                if (value && NeverDownloadUpdates)
                {
                    NeverDownloadUpdates = false;
                }
                SetRegistryValue(false, "AlwaysDownloadUpdates", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>Doesn't ask for downloading of updates unless unsupported version</summary>
        internal static bool NeverDownloadUpdates
        {
            get
            {
                return Convert.ToBoolean(GetIntRegistryValue(false, "NeverDownloadUpdates"));
            }
            set
            {
                if (value && AlwaysDownloadUpdates)
                {
                    AlwaysDownloadUpdates = false;
                }
                SetRegistryValue(false, "NeverDownloadUpdates", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        /// <summary>Version of application used for information about new changes</summary>
        internal static string VersionInfo
        {
            get
            {
                return GetStringRegistryValue(false, "VersionInfo");
            }
            set
            {
                SetRegistryValue(false, "VersionInfo", value, RegistryValueKind.String);
            }
        }
        #endregion

        #region APPLICATION SETTINGS (COMPILED INTO CODE)

        // Registry key of obalkyknih - This shouldn't change, if changed, it has to be changed also in installer
        private static RegistryKey UserSettingsRegistryKey
        {
            get
            {
                return Registry.CurrentUser.CreateSubKey(@"Software\ObalkyKnih-scanner");
            }
        }

        private static RegistryKey AdminSettingsRegistryKey
        {
            get
            {
                return Registry.LocalMachine.OpenSubKey(@"Software\ObalkyKnih-scanner");
            }
        }
        /// <summary>Version of application</summary>
        internal static Version Version { get { return Assembly.GetEntryAssembly().GetName().Version; } }

        /// <summary>URL of folder containing update-info.xml file</summary>
        internal const string UpdateServer = "https://obalkyknih.cz/obalkyknih-scanner";

        /// <summary>URL of import function on obalkyknih.</summary>
        internal const string ImportLink = "https://obalkyknih.cz/api/import";

        /// <summary>Returns path to temporary folder, where are stored images opened in external editor and downloaded updates</summary>
        internal static string TemporaryFolder { get { return System.IO.Path.GetTempPath() + "ObalkyKnih-scanner\\"; } }

        /// <summary>List of Title fields in Marc21 (245a 245b 245n, 245p)</summary>
        internal static IEnumerable<KeyValuePair<int, IEnumerable<char>>> MetadataTitleFields
        {
            get
            {
                return new List<KeyValuePair<int, IEnumerable<char>>> 
                { new KeyValuePair<int, IEnumerable<char>>(245, new List<char> { 'a', 'b', 'n', 'p' }) };
            }
        }
        
        /// <summary>Tag of Author field in Marc21</summary>
        internal static IEnumerable<KeyValuePair<int, IEnumerable<char>>> MetadataAuthorFields
        {
            get
            {
                return new List<KeyValuePair<int, IEnumerable<char>>> 
                { 
                    new KeyValuePair<int, IEnumerable<char>>(100, new List<char> { 'a', 'b' }),
                    new KeyValuePair<int, IEnumerable<char>>(700, new List<char> { 'a', 'b' })
                };
            }
        }

        /// <summary>Publish Year field in Marc21 (field, subfield, ind1, ind2)</summary>
        internal static Tuple<int, char, int?, int?> MetadataPublishYearField
        {
            get
            {
                return new Tuple<int, char, int?, int?>(260, 'c', null, null);
            }
        }

        /// <summary>ISBN field in Marc21 (field, subfield, ind1, ind2)</summary>
        internal static Tuple<int, char, int?, int?> MetadataIsbnField
        {
            get
            {
                return new Tuple<int, char, int?, int?>(20, 'a', null, null);
            }
        }

        /// <summary>ISSN field in Marc21 (field, subfield, ind1, ind2)</summary>
        internal static Tuple<int, char, int?, int?> MetadataIssnField
        {
            get
            {
                return new Tuple<int, char, int?, int?>(22, 'a', null, null);
            }
        }

        /// <summary>ČNB field in Marc21 (field, subfield, ind1, ind2)</summary>
        internal static Tuple<int, char, int?, int?> MetadataCnbField
        {
            get
            {
                return new Tuple<int, char, int?, int?>(15, 'a', null, null);
            }
        }

        /// <summary>OCLC field in Marc21 (field, subfield, ind1, ind2)</summary>
        internal static Tuple<int, char, int?, int?> MetadataOclcField
        {
            get
            {
                return new Tuple<int, char, int?, int?>(35, 'a', null, null);
            }
        }

        /// <summary>EAN field in Marc21 (field, subfield, ind1, ind2)</summary>
        internal static Tuple<int, char, char?, char?> MetadataEanField
        {
            get
            {
                return new Tuple<int, char, char?, char?>(24, 'a', '3', null);
            }
        }

        /// <summary>PPI used for scanning of cover</summary>
        internal const int CoverDPI = 300;

        /// <summary>Color type used for scanning of cover (Color/Grey/Black and White)</summary>
        internal const ScanColor CoverScanType = ScanColor.Color;

        /// <summary>PPI used for scanning of cover</summary>
        internal const int TocDPI = 300;

        /// <summary>Color type used for scanning of cover (Color/Grey/Black and White)</summary>
        internal const ScanColor TocScanType = ScanColor.Color;
        #endregion

        /// <summary>Saves settings into registry if not already</summary>
        internal static void PersistSettings()
        {
            SetRegistryValue(false, "isMigrated", 1, RegistryValueKind.DWord);
            UserSettingsRegistryKey.Flush();
        }
    }
}