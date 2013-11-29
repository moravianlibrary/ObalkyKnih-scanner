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
    /// <summary> Class used for retrieval of metadata </summary>
    public class MetadataRetriever
    {
        // barcode used for retrieval of metadata
        private string barcode;

        /// <summary> Object holding resulting metadata </summary>
        public Metadata Metadata { get; private set; }

        /// <summary> List of containing warning messages connected to metadata e.g. multiple ISBN </summary>
        public List<string> Warnings { get; private set; }

        /// <summary> Download link of cover from obalkyknih for this record </summary>
        public string OriginalCoverImageLink { get; private set; }

        /// <summary> Download link of toc pdf from obalkyknih for this record </summary>
        public string OriginalTocPdfLink { get; private set; }

        /// Download link for thumbnail of toc pfd from obalkyknih for this record </summary>
        public string OriginalTocThumbnailLink { get; private set; }

        /// <summary>
        /// Constructor for creating MetadataRetriever with given barcode and metadata (useful for retrieving cover and toc info)
        /// </summary>
        /// <param name="barcode">barcode of record</param>
        /// <param name="metadata">metadata connected with record</param>
        public MetadataRetriever(string barcode, Metadata metadata)
        {
            this.barcode = barcode;
            this.Metadata = metadata;
            this.Warnings = new List<string>();
        }
        
        /// <summary> Constructor, retrieves metadata for record with given barcode and downloads cover and toc thumbnails </summary>
        /// <param name="barcode">barcode of record</param>
        public MetadataRetriever(string barcode)
        {
            this.barcode = barcode;
            this.Warnings = new List<string>();
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

        /// <summary> Retrieves download links for cover and toc in obalkyknih.cz </summary>
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
            bibinfo.title = (string.IsNullOrWhiteSpace(Metadata.Title)) ? null : Metadata.Title;
            bibinfo.year = (string.IsNullOrWhiteSpace(Metadata.Year)) ? null : Metadata.Year;
            bibinfo.isbn = (string.IsNullOrWhiteSpace(Metadata.ISBN)) ? null : Metadata.ISBN;
            bibinfo.issn = (string.IsNullOrWhiteSpace(Metadata.ISSN)) ? null : Metadata.ISSN;
            bibinfo.ean = (string.IsNullOrWhiteSpace(Metadata.EAN)) ? null : Metadata.EAN;
            bibinfo.oclc = (string.IsNullOrWhiteSpace(Metadata.OCLC)) ? null : Metadata.OCLC;
            if (!string.IsNullOrWhiteSpace(Metadata.CNB))
            {
                bibinfo.nbn = Metadata.CNB;
            }
            else if (!string.IsNullOrWhiteSpace(Metadata.URN))
            {
                bibinfo.nbn = Metadata.URN;
            }
            else if (!string.IsNullOrWhiteSpace(Metadata.Custom))
            {
                bibinfo.nbn = Settings.Sigla + "-" + Metadata.Custom;
            }
            requestObject.bibinfo = bibinfo;
            string urlString = "https://www.obalkyknih.cz/api/book?book=";
            requestObject.permalink = @"http://aleph.mzk.cz/F?func=find-c&ccl_term=sys=" + Metadata.Sysno;
            
            string jsonData = JsonConvert.SerializeObject(requestObject, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore});
            
            urlString += Uri.EscapeDataString(jsonData);
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Referer", Settings.Z39ServerUrl ?? Settings.XServerUrl);
                Stream stream = webClient.OpenRead(urlString);
                StreamReader reader = new StreamReader(stream);
                string responseJson = reader.ReadToEnd();
                char[] endTrimChars = { '\n', ')', ']', ';' };
                //remove unwanted characters, from beginning remove string "obalky.callback([" and from end, it should remove string "]);\n"
                responseJson = responseJson.Replace("obalky.callback([", "").TrimEnd(endTrimChars);
                ResponseObject responseObject = JsonConvert.DeserializeObject<ResponseObject>(responseJson);
                if (responseObject != null)
                {
                    //assign values
                    this.OriginalCoverImageLink = responseObject.cover_medium_url;
                    this.OriginalTocThumbnailLink = responseObject.toc_thumbnail_url;
                    this.OriginalTocPdfLink = responseObject.toc_pdf_url;
                }
            }
        }

        // Retrieves metadata from Z39.50 server
        private void RetrieveMetadataByBarcodeZ39()
        {
            string z39Server = Settings.Z39ServerUrl;
            int z39Port = Settings.Z39Port;
            string z39Base = Settings.Z39Base;
            string z39UserName = Settings.Z39UserName;
            string z39Password = Settings.Z39Password;
            Record_Character_Encoding z39Encoding = Settings.Z39Encoding;
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
                    // Sysno
                    metadata.Sysno = record_from_z3950.Control_Number;
                    // Custom identifier
                    if (!string.IsNullOrWhiteSpace(metadata.Sysno))
                    {
                        metadata.Custom = Settings.Base + metadata.Sysno;
                    }
                    // Title
                    foreach (var field in Settings.MetadataTitleFields)
                    {
                        if (!record_from_z3950.has_Field(field.Key))
                        {
                            continue;
                        }
                        foreach (var subfield in field.Value)
                        {
                            metadata.Title += record_from_z3950.Get_Data_Subfield(field.Key, subfield).TrimEnd('/', ' ') + " ";
                        }
                    }
                    // Authors
                    string authors = "";
                    foreach (var field in Settings.MetadataAuthorFields)
                    {
                        if (!record_from_z3950.has_Field(field.Key))
                        {
                            continue;
                        }
                        List<MARC_Field> authorFields = record_from_z3950.Fields[field.Key];
                        foreach (var authorField in authorFields)
                        {
                            foreach (var subfield in field.Value)
                            {
                                MARC_Subfield authorSubfield = authorField.Subfields_By_Code(subfield).FirstOrDefault();
                                authors += authorSubfield == null ? "" : authorSubfield.Data;
                                authors += "@";
                            }
                            authors = authors.Trim() + "|";
                        }
                    }
                    metadata.Authors = ParseAuthors(authors, '@');
                    // Publish year
                    metadata.Year = GetZ39Subfields(record_from_z3950, Settings.MetadataPublishYearField.Item1, 
                        Settings.MetadataPublishYearField.Item2);
                    // ISBN
                    metadata.ISBN = GetNormalizedISBN(GetZ39Subfields(record_from_z3950, Settings.MetadataIsbnField.Item1,
                        Settings.MetadataIsbnField.Item2));
                    if (metadata.ISBN.Contains('|'))
                    {
                        Warnings.Add("Záznam obsahuje více než 1 ISBN, vyberte správné, jsou oddělena zvislou čárou.");
                    }
                    // ISSN
                    metadata.ISSN = GetNormalizedISSN(GetZ39Subfields(record_from_z3950, Settings.MetadataIssnField.Item1,
                        Settings.MetadataIssnField.Item2));
                    if (metadata.ISSN.Contains('|'))
                    {
                        Warnings.Add("Záznam obsahuje více než 1 ISSN, vyberte správné, jsou oddělena zvislou čárou.");
                    }
                    // CNB
                    metadata.CNB = GetZ39Subfields(record_from_z3950, Settings.MetadataCnbField.Item1, Settings.MetadataCnbField.Item2);
                    if (metadata.CNB.Contains('|'))
                    {
                        Warnings.Add("Záznam obsahuje více než 1 ČNB, vyberte správné, jsou oddělena zvislou čárou.");
                    }
                    // OCLC
                    metadata.OCLC = GetZ39Subfields(record_from_z3950, Settings.MetadataOclcField.Item1, Settings.MetadataOclcField.Item2);
                    if (metadata.OCLC.Contains('|'))
                    {
                        Warnings.Add("Záznam obsahuje více než 1 OCLC, vyberte správné, jsou oddělena zvislou čárou.");
                    }
                    // EAN
                    string ean = "";
                    if (record_from_z3950.has_Field(Settings.MetadataEanField.Item1))
                    {
                        List<MARC_Field> eanFields = record_from_z3950.Fields[Settings.MetadataEanField.Item1];
                        foreach (var eanField in eanFields)
                        {
                            if (eanField.Indicator1 != Settings.MetadataEanField.Item3)
                            {
                                continue;
                            }
                            if (eanField.has_Subfield(Settings.MetadataEanField.Item2))
                            {
                                foreach (var eanSubfield in eanField.Subfields_By_Code(Settings.MetadataEanField.Item2))
                                {
                                    ean += eanSubfield.Data + "|";
                                }
                            }
                        }
                    }
                    ean = ean.TrimEnd('|');
                    if (!string.IsNullOrWhiteSpace(ean))
                    {
                        metadata.EAN = ean;
                        if (metadata.EAN.Contains('|'))
                        {
                            this.Warnings.Add("Záznam obsahuje více než 1 EAN, vyberte správné, jsou oddělena zvislou čárou");
                        }
                    }

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

                    else if (errorMessage.Contains("Connection could not be made to"))
                    {
                        errorMessage = "Nebylo možné navázat spojení s " + 
                            errorMessage.Substring(errorMessage.LastIndexOf(' '));
                    }

                    throw new Z39Exception(errorMessage);
                }
                else
                {
                    throw new Z39Exception("Nastala neznámá chyba během Z39.50 dotazu");
                }
            }
        }

        // get subfield values even from multiple same subfields
        private string GetZ39Subfields(MARC_Record record_from_z3950,int fieldCode, char subfieldCode)
        {
            string tmpId = "";
            List<MARC_Field> idFields = (record_from_z3950.has_Field(fieldCode)) ?
                record_from_z3950.Fields[fieldCode] : new List<MARC_Field>();
            foreach (var idField in idFields)
            {
                if (idField.has_Subfield(subfieldCode))
                {
                    foreach (var idSubfield in idField.Subfields_By_Code(subfieldCode))
                    {
                        tmpId += idSubfield.Data + "|";
                    }
                }
            }
            return tmpId.Trim('|');
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
                errorText += "X-Server URL, ";
            }
            if (string.IsNullOrWhiteSpace(xServerBaseName))
            {
                errorText += "X-Server Database";
            }
            errorText = errorText.TrimEnd(new char[] { ' ', ',' });

            if (!string.IsNullOrEmpty(errorText))
            {
                throw new ArgumentException("V nastaveních chybí následující údaje: " + errorText);
            }

            string resultSetURLPart = "/X?op=find&code=BAR&request=" + this.barcode + "&base=" + xServerBaseName;
            string sysNoUrlPart = "/X?op=present&set_entry=1&set_number=";
            
            if (!xServerUrl.StartsWith("http"))
            {
                xServerUrl = "https://" + xServerUrl;
            }
            // if /X is already in name, remove 'X'
            if (xServerUrl.EndsWith("/X"))
            {
                xServerUrl.TrimEnd('X');
            }
            // remove trailing '/'
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
                
                if (!string.IsNullOrWhiteSpace(metadata.Sysno))
                {
                    metadata.Custom = Settings.Base + metadata.Sysno;
                }

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

                // Title
                foreach (var field in Settings.MetadataTitleFields)
                {
                    foreach (var subfield in field.Value)
                    {
                        metadata.Title += ParseMetadataFromOaiXml(doc, new Tuple<int, char, int?, int?>(field.Key, subfield, null, null)) + " ";
                    }
                    metadata.Title = metadata.Title.Trim();
                }

                // Authors
                string authors = "";
                foreach (var field in Settings.MetadataAuthorFields)
                {
                    foreach (var subfield in field.Value)
                    {
                        authors += ParseMetadataFromOaiXml(doc, new Tuple<int, char, int?, int?>(field.Key, subfield, null, null)) + "@";
                    }
                    authors = authors.Trim(new char[]{'@', ' '}) + "|";
                }
                metadata.Authors = ParseAuthors(authors, '@');
                metadata.Year = ParseMetadataFromOaiXml(doc, Settings.MetadataPublishYearField);
                ParseIdentifierFromOaiXml(doc, ref metadata);
            }
            this.Metadata = metadata;
        }

        // Parses authors - from format Surname1, Name1,|Surname2, Name2 to format Name1 Surname1, Name2 Surname2
        private string ParseAuthors(string authors, char numerationSplitter)
        {
            authors = authors.Trim('|');
            string finalAuthors = "";
            string[] authorsArray = authors.Split('|');
            foreach (string a in authorsArray)
            {
                string[]parts =  a.Split(numerationSplitter);
                string numerationPart = parts.Length > 1 ? " " + a.Split(numerationSplitter)[1] : "";
                string[] nameParts = a.Split(numerationSplitter)[0].Split(',');
                string resultName = "";
                foreach (string part in nameParts)
                {
                    resultName = part + " " + resultName;
                }
                resultName = resultName.Trim() + numerationPart + ", ";
                finalAuthors += resultName;
            }
            return finalAuthors.Trim(new char[] { ',', ' ', '@' });
        }

        // Parses metadata field from OAI-XML element into string representation of value
        private string ParseMetadataFromOaiXml(XDocument document, Tuple<int, char, int?, int?> field)
        {
            string result = "";
            string varfieldCodeString = field.Item1.ToString().PadLeft(3, '0');
            var fieldElement = from varfield in document.Descendants("varfield")
                               where varfieldCodeString.Equals(varfield.Attribute("id").Value)
                               select varfield;
            foreach (var fe in fieldElement)
            {
                var resultSubfields = from subfield in fe.Elements("subfield")
                                      where field.Item2.ToString().Equals(subfield.Attribute("label").Value)
                                      select subfield;
                foreach (var subfield in resultSubfields)
                {
                    result += subfield.Value.Trim(new char[] { ' ', '/' }) + " ";
                }
                result = result.Trim() + "|";
            }
            return result.TrimEnd('|');
        }

        // Retrieves identifiers from OAI-XML document and sets them to metadata object
        private void ParseIdentifierFromOaiXml(XDocument document, ref Metadata metadata)
        {
            metadata.ISBN = GetNormalizedISBN(ParseMetadataFromOaiXml(document, Settings.MetadataIsbnField));

            if (metadata.ISBN.Contains('|'))
            {
                Warnings.Add("Záznam obsahuje více než 1 ISBN, vyberte správné, jsou oddělena zvislou čárou.");
            }

            metadata.ISSN = GetNormalizedISSN(ParseMetadataFromOaiXml(document, Settings.MetadataIssnField));

            if (metadata.ISSN.Contains('|'))
            {
                Warnings.Add("Záznam obsahuje více než 1 ISSN, vyberte správné, jsou oddělena zvislou čárou.");
            }

            metadata.CNB = ParseMetadataFromOaiXml(document, Settings.MetadataCnbField);

            if (metadata.CNB.Contains('|'))
            {
                Warnings.Add("Záznam obsahuje více než 1 ČNB, vyberte správné, jsou oddělena zvislou čárou.");
            }

            metadata.OCLC = ParseMetadataFromOaiXml(document, Settings.MetadataOclcField);

            if (metadata.OCLC.Contains('|'))
            {
                Warnings.Add("Záznam obsahuje více než 1 OCLC, vyberte správné, jsou oddělena zvislou čárou.");
            }

            //parse ean
            string ean = "";
            var eanFields = from varfield in document.Descendants("varfield")
                               where Settings.MetadataEanField.Item1.Equals(varfield.Attribute("id").Value)
                               && Settings.MetadataEanField.Item3.Equals(varfield.Attribute("i1").Value)
                               select varfield;
            foreach (var eanField in eanFields)
            {
                IEnumerable<string> subfields = from sf in eanField.Elements("subfield")
                                                  where Settings.MetadataEanField.Item2.ToString().Equals(sf.Attribute("label").Value)
                                                  select sf.Value;
                ean +=  "|" + string.Join("|", subfields);
                ean.Trim(new char[] { ' ', '/', '|' });
            }
            metadata.EAN = ean;
            if (metadata.EAN.Contains('|'))
            {
                Warnings.Add("Záznam obsahuje více než 1 EAN, vyberte správné, jsou oddělena zvislou čárou.");
            }
        }

        // Removes all characters between character different from digits, 'x' or '-' and '|'
        private string GetNormalizedISBN(string isbn)
        {
            bool write = true;
            StringBuilder sb = new StringBuilder();
            foreach (char c in isbn.ToCharArray())
            {
                if ('|'.Equals(c))
                {
                    write = true;
                }
                else if (!(char.IsDigit(c) || '-'.Equals(c) || 'x'.Equals(char.ToLower(c))))
                {
                    write = false;
                }
                if (write)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        // Removes all characters different from digits and character '-'
        private string GetNormalizedISSN(string issn)
        {
            bool write = true;
            StringBuilder sb = new StringBuilder();
            foreach (char c in issn.ToCharArray())
            {
                if ('|'.Equals(c))
                {
                    write = true;
                }
                else if (!(char.IsDigit(c) || '-'.Equals(c) || 'x'.Equals(char.ToLower(c))))
                {
                    write = false;
                }
                if (write)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
