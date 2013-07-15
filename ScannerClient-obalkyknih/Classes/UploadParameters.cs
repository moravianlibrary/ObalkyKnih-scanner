using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.IO;

namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Represents parameters for upload to obalkyknih.cz
    /// </summary>
    class UploadParameters
    {
        /// <summary>
        /// URL where will be files uploaded
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Stream containing cover image
        /// </summary>
        public Stream CoverStream { get; set; }

        /// <summary>
        /// Stream containing tiff with toc images
        /// </summary>
        public Stream TocStream { get; set; }

        /// <summary>
        /// Stream containing meta informations
        /// </summary>
        public Stream MetaStream { get; set; }

        /// <summary>
        /// Collection of string parameters (Title, Author, Year, Identifiers, Login, Password)
        /// </summary>
        public NameValueCollection Nvc { get; set; }
    }
}
