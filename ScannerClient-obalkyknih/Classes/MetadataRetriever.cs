using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Xml.Linq;
using System.IO;
using SobekCM;
using SobekCM.Bib_Package.MARC.Parsers;
using SobekCM.Bib_Package.MARC;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Class used for retrieval of metadata
    /// </summary>
    public class MetadataRetriever
    {
        // barcode used for retrieval of metadata
        private string barcode;

        /// <summary>
        /// Object holding resulting metadata
        /// </summary>
        public Metadata Metadata { get; private set; }

        /// <summary>
        /// Download link of cover from obalkyknih for this record
        /// </summary>
        public string OriginalCoverImageLink { get; private set; }

        /// <summary>
        /// Download link of toc pdf from obalkyknih for this record
        /// </summary>
        public string OriginalTocPdfLink { get; private set; }

        /// <summary>
        /// Download link for thumbnail of toc pfd from obalkyknih for this record
        /// </summary>
        public string OriginalTocThumbnailLink { get; private set; }

        /// <summary>
        /// Constructor, retrieves metadata for record with given barcode
        /// and downloads cover and toc thumbnails
        /// </summary>
        /// <param name="barcode">barcode of record</param>
        public MetadataRetriever(string barcode)
        {
            this.barcode = barcode;
            if (Settings.IsXServerEnabled)
            {
                RetrieveMetadataByBarcodeXServer();
            }
            else
            {
                RetrieveMetadataByBarcodeZ39();
            }
            RetrieveOriginalCoverAndTocInformation();
        }

        /// <summary>
        /// Retrieves download links for cover and toc in obalkyknih.cz
        /// </summary>
        public void RetrieveOriginalCoverAndTocInformation()
        {
            if (Metadata == null)
            {
                return;
            }
            RequestObject requestObject = new RequestObject();
            Bibinfo bibinfo = new Bibinfo();
            bibinfo.authors = new List<string>();
            bibinfo.authors.Add(Metadata.Authors);
            bibinfo.title = Metadata.Title;
            bibinfo.year = Metadata.Year;
            bibinfo.isbn = Metadata.ISBN;
            requestObject.bibinfo = bibinfo;
            string urlString = "https://www.obalkyknih.cz/api/book?book=";
            requestObject.permalink = @"http://aleph.mzk.cz/F?func=find-c&ccl_term=sys=" + Metadata.Sysno;
            
            string jsonData = JsonConvert.SerializeObject(requestObject, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore});
            
            urlString += Uri.EscapeDataString(jsonData);
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Referer", "https://aleph.mzk.cz");
                Stream stream = webClient.OpenRead(urlString);
                StreamReader reader = new StreamReader(stream);
                string responseJson = reader.ReadToEnd();
                char[] endTrimChars = { '\n', ')', ']', ';' };
                //remove unwanted characters from start remove string "obalky.callback([" and from end, it should remove string "]);\n"
                responseJson = responseJson.Replace("obalky.callback([", "").TrimEnd(endTrimChars);
                ResponseObject responseObject = JsonConvert.DeserializeObject<ResponseObject>(responseJson);
                //assign values
                this.OriginalCoverImageLink = responseObject.cover_medium_url;
                this.OriginalTocThumbnailLink = responseObject.toc_thumbnail_url;
                this.OriginalTocPdfLink = responseObject.toc_pdf_url;
            }
        }

        private void RetrieveMetadataByBarcodeZ39()
        {
            string z39Server = Settings.Z39Server;
            int z39Port = Settings.Z39Port;
            string z39Base = Settings.Z39Base;
            string z39UserName = Settings.Z39UserName;
            string z39Password = Settings.Z39Password;
            Encoding z39Encoding = Settings.Z39Encoding ?? Encoding.UTF8;
            int z39BarcodeField = Settings.Z39BarcodeField;

            //validate
            string errorText = "";
            if (string.IsNullOrEmpty(z39Server))
            {
                errorText += "Z39.50 Server URL; ";
            }
            if (z39Port <= 0)
            {
                errorText += "Z39.50 Sever Port; ";
            }
            if (string.IsNullOrEmpty(z39Base))
            {
                errorText += "Z39.50 Databáze; ";
            }
            if (z39BarcodeField <= 0)
            {
                errorText += "Vyhledávací atribut";
            }

            if (!string.IsNullOrEmpty(errorText))
            {
                throw new ArgumentException("V nastaveních chybí následující údaje: " + errorText);
            }

            string errorMessage = "";
            try
            {
                Z3950_Endpoint endpoint;
                if (string.IsNullOrEmpty(z39UserName))
                {
                    endpoint = new Z3950_Endpoint("Z39.50",
                        z39Server, (uint)z39Port, z39Base);
                }
                else
                {
                    endpoint = new Z3950_Endpoint("Z39.50",
                        z39Server, (uint)z39Port, z39Base, z39UserName);
                    endpoint.Password = z39Password ?? "";
                }

                // Retrieve the record by primary identifier
                MARC_Record record_from_z3950 = MARC_Record_Z3950_Retriever.Get_Record(
                    z39BarcodeField, this.barcode, endpoint, out errorMessage, z39Encoding);

                if (record_from_z3950 != null)
                {
                    Metadata metadata = new Metadata();
                    metadata.Title = record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataTitleField, Settings.MetadataTitleSubfield)
                        .TrimEnd('/', ' '); ;
                    metadata.Title += record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataTitleField, Settings.MetadataTitleSubfield2)
                        .TrimEnd('/', ' ');

                    string authors = record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataAuthorField, Settings.MetadataAuthorSubfield);
                    string authors2 = record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataAuthorField2, Settings.MetadataAuthorSubfield2);
                    if (authors2.Length != 0)
                    {
                        authors += "|" + authors2;
                    }
                    metadata.Authors = ParseAuthors(authors);

                    metadata.Year = record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataPublishYearField, Settings.MetadataPublishYearSubfield);

                    metadata.ISBN = GetNormalizedISBN(record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataIsbnField, Settings.MetadataIsbnSubfield));
                    metadata.ISSN = GetNormalizedISSN(record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataIssnField, Settings.MetadataIssnSubfield));
                    metadata.CNB = record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataCnbField, Settings.MetadataCnbSubfield);
                    metadata.OCLC = record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataOclcField, Settings.MetadataOclcSubfield)
                        .Replace("(OCoLC)", "");

                    metadata.FixedFields = MarcGetFixedFields(record_from_z3950);
                    metadata.VariableFields = MarcGetVariableFields(record_from_z3950);

                    this.Metadata = metadata;
                }
            }
            catch (Exception)
            {
                throw new Z39Exception("Nastala neočekávaná chyba během Z39.50 dotazu.");
            }
            // Display any error message encountered
            if (this.Metadata == null)
            {
                if (errorMessage.Length > 0)
                {
                    if (errorMessage.Contains("No matching record found in Z39.50 endpoint"))
                    {
                        errorMessage = "Nenalezen vhodný záznam.";
                    }
                    throw new Z39Exception(errorMessage);
                }
                else
                {
                    throw new Z39Exception("Nastala neznámá chyba během Z39.50 dotazu");
                }
            }
        }

        // Returns collection of fixed fields from Marc21 record
        private IEnumerable<KeyValuePair<string, string>> MarcGetFixedFields(MARC_Record record)
        {
            List<KeyValuePair<string, string>> fixedFields = new List<KeyValuePair<string, string>>();
            fixedFields.Add(new KeyValuePair<string, string>("LDR", record.Leader));
            
            foreach (int thisTag in record.Fields.Keys)
            {
                List<MARC_Field> matchingFields = record.Fields[thisTag];
                foreach (MARC_Field thisField in matchingFields)
                {
                    if (thisField.Subfield_Count == 0)
                    {
                        if (thisField.Control_Field_Value.Length > 0)
                        {
                            fixedFields.Add(new KeyValuePair<string, string>(
                                thisField.Tag.ToString().PadLeft(3, '0'),
                                thisField.Control_Field_Value));
                            
                        }
                    }
                }
            }
            return fixedFields;
        }

        // Returns collection of non-fixed (variable) fields from Marc21 record
        private IEnumerable<MetadataField> MarcGetVariableFields(MARC_Record record)
        {
            List<MetadataField> metadataFieldsList = new List<MetadataField>();
            
            // Step through each field in the collection
            foreach (int thisTag in record.Fields.Keys)
            {
                List<MARC_Field> matchingFields = record.Fields[thisTag];
                foreach (MARC_Field thisField in matchingFields)
                {
                    if (thisField.Subfield_Count != 0)
                    {
                        MetadataField metadataField = new MetadataField();
                        metadataField.TagName = thisField.Tag.ToString().PadLeft(3, '0');
                        metadataField.Indicator1 = thisField.Indicator1.ToString();
                        metadataField.Indicator2 = thisField.Indicator2.ToString();
                        List<KeyValuePair<string, string>> subfields = new List<KeyValuePair<string, string>>();
                        // Build the complete line
                        foreach (MARC_Subfield thisSubfield in thisField.Subfields)
                        {
                            subfields.Add(new KeyValuePair<string, string>
                                (thisSubfield.Subfield_Code.ToString(), thisSubfield.Data));
                        }
                        metadataField.Subfields = subfields;
                        metadataFieldsList.Add(metadataField);
                    }
                }
            }
            return metadataFieldsList;
        }

        // Retrieves metadata from X-Server
        private void RetrieveMetadataByBarcodeXServer()
        {
            //create X-Server request
            string xServerUrl = Settings.XServerUrl;
            string xServerBaseName = Settings.XServerBase;

            string errorText = "";
            if (string.IsNullOrWhiteSpace(xServerUrl))
            {
                errorText += "X-Server URL; ";
            }
            if (string.IsNullOrWhiteSpace(xServerBaseName))
            {
                errorText += "X-Server Database";
            }

            if (!string.IsNullOrEmpty(errorText))
            {
                throw new ArgumentException("V nastaveních chybí následující údaje: " + errorText);
            }

            string resultSetURLPart = "/X?op=find&code=BAR&request=" + this.barcode + "&base=" + xServerBaseName;
            string sysNoUrlPart = "/X?op=present&set_entry=1&set_number=";
            
            if (xServerUrl == null || "".Equals(xServerUrl)) throw new Z39Exception("URL of XServer is empty");
            if (!xServerUrl.StartsWith("http"))
            {
                xServerUrl = "https://" + xServerUrl;
            }
            xServerUrl.TrimEnd('/');

            Metadata metadata = new Metadata();
            using (WebClient webClient = new WebClient())
            {
                Stream stream = webClient.OpenRead(xServerUrl + resultSetURLPart);
                XDocument doc = XDocument.Load(stream);
                string resultSetNumber = null;
                if(doc.Descendants("set_number").Count() > 0)
                {
                    resultSetNumber = doc.Descendants("set_number").Single().Value;
                }
                else
                {
                    throw new Z39Exception("Nenalezen vhodný záznam.");
                }

                stream = webClient.OpenRead(xServerUrl + sysNoUrlPart + resultSetNumber);
                doc = XDocument.Load(stream);

                metadata.Sysno = doc.Descendants("doc_number").Single().Value;

                IEnumerable<XElement> fixedFieldsXml = from el in doc.Descendants("fixfield")
                                                    select el;
                IEnumerable<XElement> variableFieldsXml = from el in doc.Descendants("varfield")
                                                  select el;

                List<KeyValuePair<string, string>> fixedFields = new List<KeyValuePair<string, string>>();
                foreach (var field in fixedFieldsXml)
                {
                    var name = field.Attribute("id").Value;
                    var value = field.Value;
                    fixedFields.Add(new KeyValuePair<string, string>(name, value));
                }
                metadata.FixedFields = fixedFields;

                List<MetadataField> variableFields = new List<MetadataField>();
                foreach (var field in variableFieldsXml)
                {
                    MetadataField metadataField = new MetadataField();
                    metadataField.TagName = field.Attribute("id").Value;
                    metadataField.Indicator1 = field.Attribute("i1").Value;
                    metadataField.Indicator2 = field.Attribute("i2").Value;
                    IEnumerable<KeyValuePair<string, string>> subfields = from sf in field.Elements("subfield")
                                                                          select new KeyValuePair<string, string>(sf.Attribute("label").Value, sf.Value);
                    metadataField.Subfields = subfields;
                    variableFields.Add(metadataField);
                }
                metadata.VariableFields = variableFields;



                string title = ParseMetadataFromOaiXml(doc, Settings.MetadataTitleField, Settings.MetadataTitleSubfield);
                metadata.Title = title.Trim('/').Trim();
                title = ParseMetadataFromOaiXml(doc, Settings.MetadataTitleField, Settings.MetadataTitleSubfield2);
                if (title != null && !string.IsNullOrWhiteSpace(title))
                {
                    metadata.Title += " " + title.Trim('/').Trim();
                }
                
                string author1 = ParseMetadataFromOaiXml(doc, Settings.MetadataAuthorField, Settings.MetadataAuthorSubfield);
                string author2 = ParseMetadataFromOaiXml(doc, Settings.MetadataAuthorField2, Settings.MetadataAuthorSubfield2);
                if (author2 != null)
                {
                    author1 += author2;
                }
                metadata.Authors = ParseAuthors(author1);
                metadata.Year = ParseMetadataFromOaiXml(doc, Settings.MetadataPublishYearField, Settings.MetadataPublishYearSubfield);
                ParseIdentifierFromOaiXml(doc, ref metadata);
            }
            this.Metadata = metadata;
        }

        // Parses authors - from format Surname1, Name1,|Surname2, Name2,
        // to format Name1 Surname1, Name2 Surname2
        private string ParseAuthors(string authors)
        {
            string finalAuthors = "";
            string[] authorsArray = authors.Split('|');
            foreach (string a in authorsArray)
            {
                string[] nameParts = a.Split(',');
                string resultName = "";
                foreach (string part in nameParts)
                {
                    resultName = part + " " + resultName;
                }
                resultName = resultName.Trim() + ", ";
                finalAuthors += resultName;
            }
            return finalAuthors.Trim(new char[] { ',', ' ' });
        }

        // Parses metadata field from OAI-XML element into string representation of value
        private string ParseMetadataFromOaiXml(XDocument document, int varfieldCode, char subfieldCode)
        {
            string varfieldCodeString = varfieldCode.ToString().PadLeft(3,'0');
            var fieldElement = from varfield in document.Descendants("varfield")
                          where  varfieldCodeString.Equals(varfield.Attribute("id").Value)
                          select varfield;
            if (fieldElement.Count() != 1)
            {
                return null;
            }
            var resultElements = from subfield in fieldElement.Single().Elements("subfield")
                            where subfieldCode.ToString().Equals(subfield.Attribute("label").Value)
                            select subfield;
            
            if (resultElements.Count() < 1)
            {
                return null;
            }

            string result = "";

            if (varfieldCode == 700 || varfieldCode == 100)
            {
                foreach (var element in resultElements)
                {
                    result += element.Value + "|";
                }
            }
            else
            {
                result = resultElements.Single().Value;
            }

            return result;
        }

        // Retrieves identifiers from OAI-XML document and sets them to metadata object
        private void ParseIdentifierFromOaiXml(XDocument document, ref Metadata metadata)
        {
            metadata.ISBN = GetNormalizedISBN(ParseMetadataFromOaiXml(document,
                Settings.MetadataIsbnField, Settings.MetadataIsbnSubfield));
            metadata.ISSN = GetNormalizedISSN(ParseMetadataFromOaiXml(document,
                Settings.MetadataIssnField, Settings.MetadataIssnSubfield));
            metadata.CNB = ParseMetadataFromOaiXml(document, Settings.MetadataCnbField, Settings.MetadataCnbSubfield);
            metadata.OCLC = ParseMetadataFromOaiXml(document, Settings.MetadataOclcField, Settings.MetadataOclcSubfield).Replace("(OCoLC)", "");
        }

        // Removes all characters different from digits and characters 'x'and '-'
        private string GetNormalizedISBN(string isbn)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in isbn.ToCharArray())
            {
                if (char.IsDigit(c) || '-'.Equals(c) || 'x'.Equals(char.ToLower(c)))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        // Removes all characters different from digits and character '-'
        private string GetNormalizedISSN(string issn)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in issn.ToCharArray())
            {
                if (char.IsDigit(c) || '-'.Equals(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
