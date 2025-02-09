using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Xml.Linq;

class Program
{
    static void CreateXmlFile(string envelopeId, List<string> rowData, List<string> headers, string outputFolder, int duplicateCount, StreamWriter logWriter)
    {
        string fileName = duplicateCount > 0 ? $"{envelopeId}_{duplicateCount}.xml" : $"{envelopeId}.xml";
        string filePath = Path.Combine(outputFolder, fileName);

        XElement envelopeElement = new XElement("Envelope");
        for (int i = 0; i < headers.Count; i++)
        {
            envelopeElement.Add(new XElement(headers[i].Replace(" ", ""), rowData[i]));
        }

        envelopeElement.Save(filePath);

        // Log the creation of the XML file
        logWriter.WriteLine($"Created XML file for Envelope ID: {envelopeId}, File Path: {filePath}");
    }

    static void EnsureDirectoriesExist(string inputFilePath, string outputFolder, string loggingFolder, string processedFolder)
    {
        string? inputFolder = Path.GetDirectoryName(inputFilePath);
        if (inputFolder == null)
        {
            throw new ArgumentException("The input file path does not contain a directory.");
        }

        if (!Directory.Exists(inputFolder))
        {
            Directory.CreateDirectory(inputFolder);
        }

        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        if (!Directory.Exists(loggingFolder))
        {
            Directory.CreateDirectory(loggingFolder);
        }

        if (!Directory.Exists(processedFolder))
        {
            Directory.CreateDirectory(processedFolder);
        }
    }

    static void CleanUpLogFiles(string logFilePath, string errorLogFilePath)
    {
        if (new FileInfo(logFilePath).Length == 0)
        {
            File.Delete(logFilePath);
        }

        if (new FileInfo(errorLogFilePath).Length == 0)
        {
            File.Delete(errorLogFilePath);
        }
    }

    static void Main()
    {
        string inputFilePath = @"C:\DS Retrieve\inputFolder\index.csv";
        string outputFolder = @"C:\DS Retrieve\outputFolder";
        string loggingFolder = @"C:\DS Retrieve\Logging";
        string processedFolder = @"C:\DS Retrieve\processedFolder";
        string logFilePath = Path.Combine(loggingFolder, $"csv-to-many-xml-log-{DateTime.Now:yyyyMMddHHmmss}.txt");
        string errorLogFilePath = Path.Combine(loggingFolder, $"ProcessingErrors-{DateTime.Now:yyyyMMddHHmmss}.txt");

        EnsureDirectoriesExist(inputFilePath, outputFolder, loggingFolder, processedFolder);

        using (StreamWriter logWriter = new StreamWriter(logFilePath, true))
        using (StreamWriter errorLogWriter = new StreamWriter(errorLogFilePath, true))
        {
            try
            {
                while (!File.Exists(inputFilePath))
                {
                    Console.WriteLine($"Unable to open file: {inputFilePath}");
                    Console.WriteLine("Press ENTER to try again from C:\\DS Retrieve\\inputFolder\\index.csv, or enter the correct path for the index.csv file:");
                    string userInput = Console.ReadLine();
                    inputFilePath = string.IsNullOrEmpty(userInput) ? @"C:\DS Retrieve\inputFolder\index.csv" : userInput;
                }

                string[] lines = File.ReadAllLines(inputFilePath);
                if (lines.Length == 0)
                {
                    string errorMessage = "The input file is empty.";
                    logWriter.WriteLine(errorMessage);
                    Console.WriteLine(errorMessage);
                    return;
                }

                List<string> headers = new List<string>();
                Dictionary<string, int> envelopeIdCount = new Dictionary<string, int>();

                // Read the header line
                string[] headerLine = lines[0].Split(',');
                foreach (string header in headerLine)
                {
                    headers.Add(header.Trim().Replace("\"", ""));
                }

                // Read the data lines
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] dataLine = lines[i].Split(',');
                    List<string> rowData = new List<string>();
                    for (int j = 0; j < dataLine.Length; j++)
                    {
                        rowData.Add(dataLine[j].Trim().Replace("\"", ""));
                    }

                    string envelopeId = rowData[0];
                    if (!string.IsNullOrEmpty(envelopeId))
                    {
                        if (envelopeIdCount.ContainsKey(envelopeId))
                        {
                            envelopeIdCount[envelopeId]++;
                        }
                        else
                        {
                            envelopeIdCount[envelopeId] = 0;
                        }

                        CreateXmlFile(envelopeId, rowData, headers, outputFolder, envelopeIdCount[envelopeId], logWriter);
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                logWriter.WriteLine($"Exception: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                logWriter.WriteLine($"Exception: {ex.Message}");
                throw;
            }
            finally
            {
                logWriter.Close();
                errorLogWriter.Close();
                CleanUpLogFiles(logFilePath, errorLogFilePath);

                // Move the index.csv file to the processedFolder with a timestamp
                if (File.Exists(inputFilePath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    string processedFilePath = Path.Combine(processedFolder, $"index_{timestamp}.csv");
                    File.Move(inputFilePath, processedFilePath);
                }
            }
        }
    }
}

