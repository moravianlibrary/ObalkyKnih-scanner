using System;
using System.Collections.Generic;


namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Represents metadata record of unit
    /// </summary>
    public class Metadata
    {
        /// <summary>
        /// System number of record
        /// </summary>
        public string Sysno { get; set; }
        
        /// <summary>
        /// Represents enumeration of all fixed Fields (Fields without subfields)
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> FixedFields { get; set; }

        /// <summary>
        /// Represents enumeration of all variable (non-fixed) Fields (with subfields)
        /// </summary>
        public IEnumerable<MetadataField> VariableFields { get; set; }

        /// <summary>
        /// Authors of processed unit for ObalkyKnih.cz
        /// </summary>
        public string Authors { get; set; }

        /// <summary>
        /// Title of processed unit for ObalkyKnih.cz
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Publish year of processed unit for ObalkyKnih.cz
        /// </summary>
        public string Year { get; set; }

        /// <summary>
        /// ISBN of processed unit for ObalkyKnih.cz
        /// </summary>
        public string ISBN { get; set; }

        /// <summary>
        /// ISSN of processed unit for ObalkyKnih.cz
        /// </summary>
        public string ISSN { get; set; }

        /// <summary>
        /// OCLC number of processed unit for ObalkyKnih.cz
        /// </summary>
        public string OCLC { get; set; }

        /// <summary>
        /// EAN of processed unit for ObalkyKnih.cz
        /// </summary>
        public string EAN { get; set; }
        
        /// <summary>
        /// Custom identifier, it should be sysno (sigla is automatically attached)
        /// </summary>
        public string Custom { get; set; }

        /// <summary>
        /// URN:NBN identifier of processed unit for ObalkyKnih.cz
        /// </summary>
        public string URN { get; set; }

        /// <summary>
        /// ČNB identifier of processed unit for ObalkyKnih.cz
        /// </summary>
        public string CNB { get; set; }
    }

    /// <summary>
    /// Represents one variable (non-fixed) field from metadata record with all subfields
    /// </summary>
    public class MetadataField
    {
        /// <summary>
        /// Name/number of Marc field
        /// </summary>
        public string TagName { get; set; }

        /// <summary>
        /// First indicator of Marc field
        /// </summary>
        public string Indicator1 { get; set; }

        /// <summary>
        /// Second indicator of Marc field
        /// </summary>
        public string Indicator2 { get; set; }

        /// <summary>
        /// Enumeration of all pairs of subfield code and value of that subfield
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Subfields { get; set; }
    }
}
