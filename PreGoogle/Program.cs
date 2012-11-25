using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using log4net;

namespace PreGoogle
{
    internal class Program
    {
        private const string Simulate = "simulate";
        protected static readonly ILog log = LogManager.GetLogger(typeof (Program));

        private static string GetExifToolFullName()
        {
            NameValueCollection appSettings = ConfigurationManager.AppSettings;
            string[] strings = appSettings.GetValues("ExifTool");
            string exifToolFullName = strings[0];
            if (!File.Exists(exifToolFullName))
            {
                log.FatalFormat("Path to ExifTool ({0}) specified in App.Settings doesn't exist", exifToolFullName);
                throw new FileNotFoundException();
            }
            return exifToolFullName;
        }

        private static void Main(string[] args)
        {
            if (args.Count() == 0)
            {
                Console.WriteLine("PreGoogle filename [exiftool] [simulate]");
            }
            else
            {
                InitilizeLogging();
                Console.Clear();

                ProcessFile processFile = new ProcessFile(log);
                SetParams(args, processFile);

                HandleFiles(processFile, args[0]);
            }
        }

        private static void SetParams(string[] args, ProcessFile processFile)
        {
            if (args.Count() == 1)
            {
                processFile.ExifTool = GetExifToolFullName();
            }
            else
            {
                if (args.Count() == 2)
                {
                    if (args[1].Equals(Simulate, StringComparison.InvariantCultureIgnoreCase))
                    {
                        processFile.ExifTool = GetExifToolFullName();
                        processFile.Simulate = true;
                    }
                    else
                        processFile.ExifTool = args[1];
                }
                else if (args.Count() == 3)
                {
                    processFile.ExifTool = args[1];
                    processFile.Simulate = args[2].Equals(Simulate, StringComparison.InvariantCultureIgnoreCase);
                }
            }
        }

        private static void HandleFiles(ProcessFile processFile, string filePattern)
        {
            if (filePattern.Contains("*"))
            {
                ProcessAsterix(filePattern, processFile);
            }
            else if (filePattern.Equals("-r"))
            {
                Stack<string> stack = new Stack<string>();
                stack.Push(Directory.GetCurrentDirectory());
                while (stack.Count > 0)
                {
                    string dir = stack.Pop();
                    DirectoryInfo dirInfo = new DirectoryInfo(dir);
                    string pattern = Path.Combine(dirInfo.FullName, "*.jpg");
                    ProcessAsterix(pattern, processFile);
                    try
                    {
                        foreach (string dn in Directory.GetDirectories(dir))
                        {
                            stack.Push(dn);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.WarnFormat("Failed to find directory: {0}", ex);
                    }
                }
            }
            else
            {
                ProcessOneFile(filePattern, processFile);
            }
        }

        private static void ProcessOneFile(string fileName, ProcessFile processFile)
        {
            processFile.FileName = fileName;
            processFile.Start();
        }

        private static void ProcessAsterix(string filePattern, ProcessFile processFile)
        {
            FileInfo[] files = GetFiles(filePattern);
            foreach (FileInfo file in files)
            {
                processFile.FileName = file.FullName;
                processFile.Start();
            }
        }

        private static FileInfo[] GetFiles(string filePattern)
        {
            string path;
            string searchPattern;
            if (filePattern.IndexOf("\\") <= 0)
            {
                path = Directory.GetCurrentDirectory();
                searchPattern = filePattern;
            }
            else
            {
                path = Path.GetDirectoryName(filePattern);
                searchPattern = Path.GetFileName(filePattern);
            }
            DirectoryInfo dirInfo = new DirectoryInfo(path);

            return dirInfo.GetFiles(searchPattern);
        }

        private static void InitilizeLogging()
        {
            log4net.Config.XmlConfigurator.Configure();
        }
    }
}