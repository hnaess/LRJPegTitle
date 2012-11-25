using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using PreGoogle.Properties;

namespace PreGoogle
{
    internal class ImageInfo
    {
        private readonly XmlDocument _doc;
        private readonly XmlNamespaceManager _nsmgr;

        private List<string> _tidyKeyWordsList;
        private List<string> _acceptedKeyWords;


        public ImageInfo(XmlDocument doc, XmlNamespaceManager nsmgr)
        {
            _doc = doc;
            _nsmgr = nsmgr;
        }

        private string ByLine
        {
            get { return GetValue("//IPTC:By-line"); }
        }

        private string City
        {
            get { return GetValue("//IPTC:City"); }
        }

        private string Caption
        {
            get { return GetValue("//IPTC:Caption-Abstract"); }
        }

        private string DateCreated
        {
            get { return GetValue("//ExifIFD:CreateDate"); }
        }

        private List<string> Keywords
        {
            get { return GetBagItemsValues("//IPTC:Keywords"); }
        }

        private string ObjectName
        {
            get { return GetValue("//IPTC:ObjectName"); }
        }

        private string Headline
        {
            get { return GetValue("//IPTC:Headline"); }
        }

        private string Province
        {
            get { return GetValue("//IPTC:Province-State"); }
        }

        private string PrimaryLocationName
        {
            get { return GetValue("//Country-PrimaryLocationName"); }
        }

        private string Sublocation
        {
            get { return GetValue("//IPTC:Sub-location"); }
        }

        private string ComputedLocation
        {
            get
            {
                if (!string.IsNullOrEmpty(Sublocation))
                    return Sublocation;

                if (!string.IsNullOrEmpty(City))
                    return City;

                if (!string.IsNullOrEmpty(Province))
                    return Province;

                return string.Empty;
            }
        }

        private string ComputedDate
        {
            get
            {
                string readable = String.Empty;
                string dateCreated = DateCreated;
                if (!String.IsNullOrEmpty(dateCreated))
                {
                    var date = new DateTime(Convert.ToInt32(dateCreated.Substring(0, 4)), Convert.ToInt32(dateCreated.Substring(5, 2)), Convert.ToInt32(dateCreated.Substring(8, 2)));
                    readable = date.ToString("D", new CultureInfo("nb-NO"));
                }
                return readable;
            }
        }

        private string ComputedKeywords
        {
            get
            {
                // Get all accepted keywords from file
                var filteredKeywords = new Dictionary<int, string>();
                foreach (string keyword in Keywords)
                {
                    int indexOf = AcceptedKeywords.IndexOf(keyword);
                    if (!filteredKeywords.ContainsValue(keyword) && indexOf >= 0)
                    {
                        filteredKeywords[indexOf] = keyword;
                    }
                }

                // Sort keywords to get them in right order
                IOrderedEnumerable<KeyValuePair<int, string>> sortedKeywords = (from entry in filteredKeywords orderby entry.Key ascending select entry);

                var output = new StringBuilder();
                int i = 0;
                foreach (var keywordPair in sortedKeywords)
                {
                    string keyword = keywordPair.Value;

                    // tidy keyword
                    foreach (string tidyKeyword in TidyKeywords)
                    {
                        if(keyword.EndsWith(tidyKeyword))
                        {
                            keyword = (keyword.Remove(keyword.Length - tidyKeyword.Length)).Trim();
                            break;
                        }
                    }

                    if (i == 0)
                        output.Append(keyword);
                    else if (i == filteredKeywords.Count - 1)
                        output.AppendFormat(" & {0}", keyword);
                    else
                        output.AppendFormat(", {0}", keyword);
                    i++;
                }


                return output.ToString();
            }
        }

        private List<string> AcceptedKeywords
        {
            get
            {
                if (_acceptedKeyWords == null)
                {
                    _acceptedKeyWords = new List<string>();
                    string keywordsResources = Resources.KeywordsInclusionList;
                    string[] keywords = keywordsResources.Split(Environment.NewLine.ToCharArray());

                    foreach (string keyword in keywords)
                    {
                        if (keyword.Length > 0)
                            _acceptedKeyWords.Add(keyword);
                    }
                }
                return _acceptedKeyWords;
            }
        }


        private List<string> TidyKeywords
        {
            get
            {
                if (_tidyKeyWordsList == null)
                {
                    _tidyKeyWordsList = new List<string>();
                    string keywordsResources = Resources.TidyKeywordsList;
                    string[] keywords = keywordsResources.Split(Environment.NewLine.ToCharArray());

                    foreach (string keyword in keywords)
                    {
                        if (keyword.Length > 0)
                            _tidyKeyWordsList.Add(keyword);
                    }
                }
                return _tidyKeyWordsList;
            }
        }

        public string GetComputedTitle()
        {
            var title = new StringBuilder();
            // title and headline
            if (Caption.Length > 0)
                title.Append(Caption);
            else if (ObjectName.Length > 0)
                title.Append(ObjectName);
            else // keywords
            {
                title.Append(ComputedKeywords);
            }

            if (Headline.Length > 0)
            {
                if (title.Length > 0)
                    title.AppendFormat(" ({0})", Headline);
                else
                    title.Append(Headline);
            }

            // location
            string location = ComputedLocation;
            if (location.Length > 0)
            {
                if (title.Length > 0)
                    title.AppendFormat(" * ");

                title.Append(location);
            }

            // date
            string date = ComputedDate;
            if (date.Length > 0)
            {
                if (title.Length > 0)
                    title.AppendFormat(" * ");

                title.Append(date);
            }

            title = title.Replace("\"", "'");
            return title.ToString();
        }

        private string GetValue(string field)
        {
            XmlNode node = _doc.SelectSingleNode(field, _nsmgr);
            string text = ParseValue(node);
            return text;
        }

        private List<string> GetBagItemsValues(string propertyBag)
        {
            var values = new List<string>();

            XmlNode node = _doc.SelectSingleNode(propertyBag, _nsmgr);
            if (node != null)
            {
                XmlNodeList keywords = node.ChildNodes[0].ChildNodes;
                foreach (XmlNode keyword in keywords)
                {
                    string value = ParseValue(keyword);
                    values.Add(value);
                }
            }
            return values;
        }

        private static string ParseValue(XmlNode node)
        {
            string text = string.Empty;
            if (node != null)
            {
                text = node.InnerText;
                if (node.Attributes != null && node.Attributes.Count > 0 && node.Attributes[0].Value == "http://www.w3.org/2001/XMLSchema#base64Binary")
                {
                    text = Encoding.Default.GetString(Convert.FromBase64String(text));
                }
                if(!String.IsNullOrWhiteSpace(text))
                {
                    text = ProcessFile.LocalizedText(text);
                }
            }
            return text;
        }
    }
}