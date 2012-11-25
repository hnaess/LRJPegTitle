using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using log4net;

namespace PreGoogle
{
    public class ProcessFile
    {
        private enum ProcessResult
        {
            Update, NoChange, Simulate, Unexpected
        }
        
        private readonly ILog _log;
        private StringBuilder _output;

        public ProcessFile(ILog log)
        {
            _log = log;
        }

        public bool Simulate { get; set; }
        public string ExifTool { get; set; }
        public string FileName { get; set; }

        public void Start()
        {
            //_log.InfoFormat("{0} processing: {1}", (Simulate ? "Simulate" : "Process "), FileName);
            try
            {
                if (!File.Exists(FileName))
                {
                    _log.WarnFormat("File {0} is missing, cancelled", FileName);
                    return;
                }

                GetImageInfoUsingExifTool();
                if (_output == null)
                {
                    _log.ErrorFormat("Failed to load image data file for {0}", FileName);
                    return;
                }
                XmlDocument doc;
                XmlNamespaceManager nsmgr;
                LoadImageData(out doc, out nsmgr);
                var imageInfo = new ImageInfo(doc, nsmgr);

                string computedTitle = imageInfo.GetComputedTitle();
                UpdateImageWithComputedTitle(computedTitle);
            }
            catch (Exception ex)
            {
                _log.InfoFormat("{0} failed: {1}", FileName, ex);
                throw;
            }
            _output = null;
        }

        private void ExifToolOutputHandler(object sender, DataReceivedEventArgs e)
        {
            _output.AppendLine(e.Data);
        }

        private void GetImageInfoUsingExifTool()
        {
            string arguments = string.Format("\"{0}\" -charset iptc=utf8 = -ex -X -f", FileName);
            RunExifTool(arguments);
        }

        private void LoadImageData(out XmlDocument doc, out XmlNamespaceManager nsmgr)
        {
            doc = new XmlDocument();
            try
            {
                doc.LoadXml(_output.ToString());
            }
            catch (XmlException ex)
            {
                _log.FatalFormat("Failed to load xml data: {0}", ex);
                _log.Debug(_output.Length + " : " + _output);
                throw;
            }

            nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("rdf", "http://ns.exiftool.ca/IPTC/IPTC/1.0/");
            nsmgr.AddNamespace("IPTC", "http://ns.exiftool.ca/IPTC/IPTC/1.0/");
            nsmgr.AddNamespace("ExifIFD", "http://ns.exiftool.ca/EXIF/ExifIFD/1.0/");
        }

        private void RunExifTool(string arguments)
        {
            _output = new StringBuilder();
            var process = new Process
                              {
                                  StartInfo =
                                      {
                                          Arguments = arguments,
                                          CreateNoWindow = true,
                                          FileName = ExifTool,
                                          LoadUserProfile = false,
                                          RedirectStandardOutput = true,
                                          UseShellExecute = false,
                                      }
                              };

            process.OutputDataReceived += ExifToolOutputHandler;

            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();
        }

        private void UpdateImageWithComputedTitle(string title)
        {
            string state = (Simulate ? "Simulate: " : String.Empty);
            if (String.IsNullOrWhiteSpace(title))
            {
                _log.InfoFormat("{0}Image {1} not changed, title empty", state, FileName);
            }
            else
            {
                ProcessResult result = SetTitleOnImageUsingExifTool(ref title);
                switch (result)
                {
                    case ProcessResult.Update:
                        _log.InfoFormat("Image {0} new title {1}", FileName, title);
                        break;
                    case ProcessResult.NoChange:
                        _log.InfoFormat("Image {0} no changes", FileName);
                        break;
                    case ProcessResult.Simulate:
                        _log.InfoFormat("Simulate: Image {0} new title {1}", FileName, title);
                        break;
                    case ProcessResult.Unexpected:
                        _log.ErrorFormat("{0}Image {1} failed to set title. Output: {2} ",
                                         state, FileName, _output.ToString().Replace("\r\n", ""));
                        break;
                }
            }
        }

        private ProcessResult SetTitleOnImageUsingExifTool(ref string description)
        {
            description = LocalizedText(description);
            // TODO: Log if different?
            
            ProcessResult result = ProcessResult.Unexpected;
            string arguments = string.Format("\"{0}\" -overwrite_original_in_place -P -charset cp1252 -ex -XMP:Description=\"{1}\"", FileName, description);
            if (Simulate)
            {
                _log.DebugFormat("Simulate ExifTool: " + arguments);
                result = ProcessResult.Simulate;
            }            
            else
            {
                RunExifTool(arguments);                
                bool updated = _output.ToString().EndsWith("1 image files updated\r\n\r\n");
                bool nochange = _output.ToString().Contains(" 0 image files updated\r\n    1 image files unchanged");

                if(updated)
                    result = ProcessResult.Update;
                else if (nochange)
                    result = ProcessResult.NoChange;
                else
                    Debugger.Break();
            }
            return result;
        }

        public static string LocalizedText(string text)
        {
            string r = text;
            ReplaceChar(ref r, 209, "å");
            ReplaceChar(ref r, 169, "ø");
            ReplaceChar(ref r, 170, "æ");
            return r;
        }

        private static void ReplaceChar(ref string r, int ascii, string newValue)
        {
            string aa = Convert.ToChar(9500).ToString() + Convert.ToChar(ascii).ToString();
            while(r.Contains(aa))
            {
                r = r.Replace(aa, newValue);
            }
        }
    }
}