// This monitor is a conversion tool for DocuSign Retrieve index.csv files to create many XML files as the output.
// The tool monitors a folder for the index.csv file, and when detected, it automatically processes the file.
// The tool creates an XML file for each row in the index.csv file, with the name of the XML file being the Envelope ID.
// The tool also logs the creation of the XML files and moves the processed index.csv file to a processed folder.
// The tool also logs any errors that occur during the processing of the index.csv file and creates a separate error log file called ProcessingErrors.txt.
// The tool uses a FileSystemWatcher to monitor the input folder for the index.csv file and triggers the processing of the file when it is detected.

// If an index.csv file contains an Envelope ID that already exists in the output folder, the tool will create a new XML file with a suffix of _1, _2, etc., to avoid overwriting existing files.
// The tool also checks if the output XML file already exists and skips creating the new XML file if it does.
// the index.csv file is moved to the processed folder with a timestamp appended to the filename once it is processed.



using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

class Program
{
    static void CreateXmlFile(string envelopeId, List<string> rowData, List<string> headers, string outputFolder, int duplicateCount, StreamWriter logWriter)
    {
        string fileName = duplicateCount > 0 ? $"{envelopeId}_{duplicateCount}.xml" : $"{envelopeId}.xml";
        string filePath = Path.Combine(outputFolder, fileName);

        // Check if the file already exists and skip creating the new XML file if it does
        if (File.Exists(filePath))
        {
            logWriter.WriteLine($"Skipped creating XML file for Envelope ID: {envelopeId}, File Path: {filePath} already exists.");
            return;
        }

        XElement envelopeElement = new XElement("Envelope");
        for (int i = 0; i < headers.Count; i++)
        {
            envelopeElement.Add(new XElement(headers[i].Replace(" ", ""), rowData[i]));
        }

        envelopeElement.Save(filePath);

        // Log the creation of the XML file
        logWriter.WriteLine($"Created XML file for Envelope ID: {envelopeId}, File Path: {filePath}");
    }

    static void EnsureDirectoriesExist(string inputFolder, string outputFolder, string loggingFolder, string processedFolder)
    {
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

    static void ProcessFile(string inputFilePath, string outputFolder, string loggingFolder, string processedFolder)
    {
        string logFilePath = Path.Combine(loggingFolder, $"csv-to-many-xml-log-{DateTime.Now:yyyyMMddHHmmss}.txt");
        string errorLogFilePath = Path.Combine(loggingFolder, $"ProcessingErrors-{DateTime.Now:yyyyMMddHHmmss}.txt");

        using (StreamWriter logWriter = new StreamWriter(logFilePath, true))
        using (StreamWriter errorLogWriter = new StreamWriter(errorLogFilePath, true))
        {
            try
            {
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
                    Console.WriteLine($"File processed and moved to: {processedFilePath}");
                    Console.WriteLine("DocuSign Retrieve Monitor - Listening for DocuSign Retreive index.csv file. Enter 'q' to quit.");
                }
            }
        }
    }

    static void Main()
    {
        string inputFolder = @"C:\DS Retrieve Monitor\inputFolder";
        string outputFolder = @"C:\DS Retrieve Monitor\outputFolder";
        string loggingFolder = @"C:\DS Retrieve Monitor\Logging";
        string processedFolder = @"C:\DS Retrieve Monitor\processedFolder";

        EnsureDirectoriesExist(inputFolder, outputFolder, loggingFolder, processedFolder);

        FileSystemWatcher watcher = new FileSystemWatcher
        {
            Path = inputFolder,
            Filter = "index.csv",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        watcher.Created += (source, e) =>
        {
            Console.WriteLine($"File detected: {e.FullPath}");
            Task.Run(() => PromptUserAndProcessFile(e.FullPath, outputFolder, loggingFolder, processedFolder));
        };

        watcher.EnableRaisingEvents = true;

        Console.WriteLine("DocuSign Retrieve Monitor - Listening for DocuSign Retreive index.csv files. Enter 'q' to quit.");
        while (Console.Read() != 'q') ;
    }

    static void PromptUserAndProcessFile(string inputFilePath, string outputFolder, string loggingFolder, string processedFolder)
    {
        Console.WriteLine($"[{DateTime.Now}] DocuSign Retreive index.csv file detected. Processing in 5 seconds");

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        Task.Run(() =>
        {
            if (Console.ReadKey(true).Key == ConsoleKey.Enter)
            {
                cts.Cancel();
                ProcessFile(inputFilePath, outputFolder, loggingFolder, processedFolder);
            }
        }, token);

        Task.Delay(5000).ContinueWith(t =>
        {
            if (!token.IsCancellationRequested)
            {
                ProcessFile(inputFilePath, outputFolder, loggingFolder, processedFolder);
            }
        });
    }
}
