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
                bibinfo.nbn = Settings.Sigla + "" + Metadata.Custom;
            }
            requestObject.bibinfo = bibinfo;
            string urlString = "https://www.obalkyknih.cz/api/book?book=";
            requestObject.permalink = @"http://aleph.mzk.cz/F?func=find-c&ccl_term=sys=" + Metadata.Sysno;
            
            string jsonData = JsonConvert.SerializeObject(requestObject, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore});
            
            urlString += Uri.EscapeDataString(jsonData);
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Referer", Settings.Z39Server ?? Settings.XServerUrl);
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
            string z39Server = Settings.Z39Server;
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
                    metadata.Title = record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataTitleField, Settings.MetadataTitleSubfield)
                        .TrimEnd('/', ' '); ;
                    metadata.Title += record_from_z3950.Get_Data_Subfield(
                        Settings.MetadataTitleField, Settings.MetadataTitleSubfield2)
                        .TrimEnd('/', ' ');

                    string authors = "";
                    if (record_from_z3950.has_Field(Settings.MetadataAuthorField))
                    {
                        List<MARC_Field> authorFields = record_from_z3950.Fields[Settings.MetadataAuthorField];
                        foreach (var authorField in authorFields)
                        {
                            //take only first occurence of subfield tag in the field, shouldn't be mixed more authors in 1 field
                            MARC_Subfield nameSubfield = authorField.Subfields_By_Code(Settings.MetadataAuthorSubfieldName)
                                .FirstOrDefault();
                            MARC_Subfield numerationSubfield = authorField.Subfields_By_Code(Settings.MetadataAuthorSubfieldNumeration)
                                .FirstOrDefault();

                            string nameTmp = (nameSubfield == null ? "" : nameSubfield.Data)
                                + " " + (numerationSubfield == null ? "" : numerationSubfield.Data);
                            
                            authors += nameTmp.Trim() + "|";
                        }
                    }
                    //parse authors from 700
                    if (record_from_z3950.has_Field(Settings.MetadataAuthorField700))
                    {
                        List<MARC_Field> authorFields = record_from_z3950.Fields[Settings.MetadataAuthorField700];
                        foreach (var authorField in authorFields)
                        {
                            //take only first occurence of subfield tag in the field, shouldn't be mixed more authors in 1 field
                            MARC_Subfield nameSubfield = authorField.Subfields_By_Code(Settings.MetadataAuthorSubfieldName)
                                .FirstOrDefault();
                            MARC_Subfield numerationSubfield = authorField.Subfields_By_Code(Settings.MetadataAuthorSubfieldNumeration)
                                .FirstOrDefault();

                            string nameTmp = (nameSubfield == null ? "" : nameSubfield.Data)
                                + " " + (numerationSubfield == null ? "" : numerationSubfield.Data);

                            authors += nameTmp.Trim() + "|";
                        }
                    }

                    metadata.Authors = ParseAuthors(authors);

                    metadata.Year = GetZ39Subfields(record_from_z3950, Settings.MetadataPublishYearField, 
                        Settings.MetadataPublishYearSubfield);

                    metadata.ISBN = GetNormalizedISBN(GetZ39Subfields(record_from_z3950, Settings.MetadataIsbnField,
                        Settings.MetadataIsbnSubfield));
                    if (metadata.ISBN.Contains('|'))
                    {
                        Warnings.Add("Záznam obsahuje více než 1 ISBN, vyberte správné, jsou oddělena zvislou čárou.");
                    }

                    metadata.ISSN = GetNormalizedISSN(GetZ39Subfields(record_from_z3950, Settings.MetadataIssnField,
                        Settings.MetadataIssnSubfield));
                    if (metadata.ISSN.Contains('|'))
                    {
                        Warnings.Add("Záznam obsahuje více než 1 ISSN, vyberte správné, jsou oddělena zvislou čárou.");
                    }
                    
                    metadata.CNB = GetZ39Subfields(record_from_z3950, Settings.MetadataCnbField, Settings.MetadataCnbSubfield);
                    if (metadata.CNB.Contains('|'))
                    {
                        Warnings.Add("Záznam obsahuje více než 1 ČNB, vyberte správné, jsou oddělena zvislou čárou.");
                    }

                    metadata.OCLC = GetZ39Subfields(record_from_z3950, Settings.MetadataOclcField, Settings.MetadataOclcSubfield);
                    if (metadata.OCLC.Contains('|'))
                    {
                        Warnings.Add("Záznam obsahuje více než 1 OCLC, vyberte správné, jsou oddělena zvislou čárou.");
                    }

                    string ean = "";
                    if (record_from_z3950.has_Field(Settings.MetadataEanField))
                    {
                        List<MARC_Field> eanFields = record_from_z3950.Fields[Settings.MetadataEanField];
                        foreach (var eanField in eanFields)
                        {
                            if (eanField.Indicator1 == Settings.MetadataEanFirstIndicator)
                            {
                                if (eanField.has_Subfield(Settings.MetadataEanSubfield))
                                {
                                    foreach (var eanSubfield in eanField.Subfields_By_Code(Settings.MetadataEanSubfield))
                                    {
                                        ean += eanSubfield.Data + "|";
                                    }
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



                string title = ParseMetadataFromOaiXml(doc, Settings.MetadataTitleField, Settings.MetadataTitleSubfield,
                    Settings.MetadataTitleSubfield2);
                metadata.Title = title;
                
                string authors = ParseMetadataFromOaiXml(doc, Settings.MetadataAuthorField, Settings.MetadataAuthorSubfieldName,
                    Settings.MetadataAuthorSubfieldNumeration);
                string authors700 = ParseMetadataFromOaiXml(doc, Settings.MetadataAuthorField700, Settings.MetadataAuthorSubfieldName,
                    Settings.MetadataAuthorSubfieldNumeration);
                if (authors700 != null)
                {
                    authors += "|" + authors700;
                }
                metadata.Authors = ParseAuthors(authors);
                metadata.Year = ParseMetadataFromOaiXml(doc, Settings.MetadataPublishYearField, Settings.MetadataPublishYearSubfield, null);
                ParseIdentifierFromOaiXml(doc, ref metadata);
            }
            this.Metadata = metadata;
        }

        // Parses authors - from format Surname1, Name1,|Surname2, Name2 to format Name1 Surname1, Name2 Surname2
        private string ParseAuthors(string authors)
        {
            authors = authors.Trim('|');
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
        private string ParseMetadataFromOaiXml(XDocument document, int varfieldCode, char subfieldCode, char? subfieldCode2)
        {
            string varfieldCodeString = varfieldCode.ToString().PadLeft(3,'0');
            var fieldElement = from varfield in document.Descendants("varfield")
                          where  varfieldCodeString.Equals(varfield.Attribute("id").Value)
                          select varfield;
            string result = "";
            foreach (var fe in fieldElement)
            {
                IEnumerable<XElement> resultElements = null;
                if (subfieldCode2 == null)
                {
                    resultElements = from subfield in fe.Elements("subfield")
                                     where subfieldCode.ToString().Equals(subfield.Attribute("label").Value)
                                     select subfield;
                }
                else
                {
                    resultElements = from subfield in fe.Elements("subfield")
                                     where subfieldCode.ToString().Equals(subfield.Attribute("label").Value)
                                     || subfieldCode2.ToString().Equals(subfield.Attribute("label").Value)
                                     select subfield;
                }
                foreach (var subfield in resultElements)
                {
                    result += subfield.Value.Trim(new char[]{' ','/'}) + " ";
                }
                result = result.Trim() + "|";
            }
            result = result.TrimEnd('|');
            return result;
        }

        // Retrieves identifiers from OAI-XML document and sets them to metadata object
        private void ParseIdentifierFromOaiXml(XDocument document, ref Metadata metadata)
        {
            metadata.ISBN = GetNormalizedISBN(ParseMetadataFromOaiXml(document,
                Settings.MetadataIsbnField, Settings.MetadataIsbnSubfield, null));

            if (metadata.ISBN.Contains('|'))
            {
                Warnings.Add("Záznam obsahuje více než 1 ISBN, vyberte správné, jsou oddělena zvislou čárou.");
            }

            metadata.ISSN = GetNormalizedISSN(ParseMetadataFromOaiXml(document,
                Settings.MetadataIssnField, Settings.MetadataIssnSubfield, null));

            if (metadata.ISSN.Contains('|'))
            {
                Warnings.Add("Záznam obsahuje více než 1 ISSN, vyberte správné, jsou oddělena zvislou čárou.");
            }

            metadata.CNB = ParseMetadataFromOaiXml(document, Settings.MetadataCnbField, Settings.MetadataCnbSubfield, null);

            if (metadata.CNB.Contains('|'))
            {
                Warnings.Add("Záznam obsahuje více než 1 ČNB, vyberte správné, jsou oddělena zvislou čárou.");
            }

            metadata.OCLC = ParseMetadataFromOaiXml(document, Settings.MetadataOclcField, Settings.MetadataOclcSubfield, null);

            if (metadata.OCLC.Contains('|'))
            {
                Warnings.Add("Záznam obsahuje více než 1 OCLC, vyberte správné, jsou oddělena zvislou čárou.");
            }

            //parse ean
            string ean = "";
            var eanFields = from varfield in document.Descendants("varfield")
                               where Settings.MetadataEanField.Equals(varfield.Attribute("id").Value)
                               && Settings.MetadataEanFirstIndicator.Equals(varfield.Attribute("i1").Value)
                               select varfield;
            foreach (var eanField in eanFields)
            {
                IEnumerable<string> subfields = from sf in eanField.Elements("subfield")
                                                  where Settings.MetadataEanSubfield.ToString().Equals(sf.Attribute("label").Value)
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
