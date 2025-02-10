// This is the DocuSign Retrieve index.csv monitor. Its a conversion tool that operates in a terminal window for DocuSign Retrieve index.csv files
// and is used to create many XML files as the output.
// The tool is designed to be run in a terminal window and will continue to monitor the input folder for index.csv files until the user enters 'q' to quit the application.

// The tool monitors a folder for the index.csv file, and when detected, it automatically processes the file.
// The tool creates an XML file for each row in the index.csv file, with the name of the XML file being the Envelope ID.
// The tool also logs the creation of the XML files and moves the processed index.csv file to a processed folder.
// The tool also logs any errors that occur during the processing of the index.csv file and creates a separate error log file called ProcessingErrors.txt.
// The tool uses a FileSystemWatcher to monitor the input folder for the index.csv file and triggers the processing of the file when it is detected.

// If an index.csv file contains an Envelope ID that already exists in the output folder, the tool will create a new XML file with a suffix of _1, _2, etc., to avoid overwriting existing files.
// The tool also checks if the output XML file already exists and skips creating the new XML file if it does.
// the index.csv file is moved to the processed folder with a timestamp appended to the filename once it is processed.

// The tool also includes a watchdog timer that monitors the health of the application and sends an email notification if the application stops responding.
// The watchdog timer checks the health of the application every 10 seconds and sends a notification if the application has not responded for 30 seconds.
// The notification is sent via email using the SmtpClient class to a specified recipient email address.
// The email contains the message "The DocuSign Retrieve Monitor has stopped responding." and is sent from the specified sender email address.
// The email credentials are provided in the code, and the SMTP server settings are configured to use SSL on port 587.
// The notification is logged to the console, indicating whether the notification was sent successfully or if there was an error.

//  The tool also includes a method to clean up log files by deleting empty log files after processing.

//Email and WatchDog methods are not tested as they require a valid email address and SMTP server to send notifications.

// Once the index.csv is processed and xml output files created, the tool moves the output XML files from C:\ DS Retreive Monitor\outputFolder to C:\DS Retreive\Ingest Folder for further processing into other systems.



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
    private static DateTime lastHealthCheck = DateTime.Now;
    private static readonly TimeSpan healthCheckInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan watchdogTimeout = TimeSpan.FromSeconds(30);

    static void CreateXmlFile(string envelopeId, List<string> rowData, List<string> headers, string outputFolder, string ingestFolder, int duplicateCount, StreamWriter logWriter)
    {
        string fileName = duplicateCount > 0 ? $"{envelopeId}_{duplicateCount}.xml" : $"{envelopeId}.xml";
        string filePath = Path.Combine(outputFolder, fileName);

        // Check if a file with the same first 36 characters of the filename and Envelope ID exists in the ingest folder
        foreach (var file in Directory.GetFiles(ingestFolder, "*.xml"))
        {
            string existingFileName = Path.GetFileNameWithoutExtension(file);
            if (existingFileName.Length >= 36 && envelopeId.Length >= 36 && existingFileName.Substring(0, 36) == envelopeId.Substring(0, 36))
            {
                logWriter.WriteLine($"Skipped creating XML file for Envelope ID: {envelopeId}, matching file already exists in Ingest Folder.");
                return;
            }
        }

        // Check if the file already exists in the output folder and skip creating the new XML file if it does
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

    static void EnsureDirectoriesExist(params string[] directories)
    {
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
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

    static void MoveOutputFiles(string outputFolder, string ingestFolder)
    {
        EnsureDirectoriesExist(ingestFolder);

        foreach (var file in Directory.GetFiles(outputFolder, "*.xml"))
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(ingestFolder, fileName);

            // Check if the file already exists in the ingest folder and delete the new file if it does
            if (File.Exists(destFile))
            {
                File.Delete(file);
                Console.WriteLine($"Deleted new XML file: {fileName} in Output Folder because it already exists in Ingest Folder.");
                continue;
            }

            File.Move(file, destFile);
            Console.WriteLine($"Moved XML file to: {destFile}");
        }
    }

    static void ProcessFile(string inputFilePath, string outputFolder, string loggingFolder, string processedFolder, string ingestFolder)
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

                        CreateXmlFile(envelopeId, rowData, headers, outputFolder, ingestFolder, envelopeIdCount[envelopeId], logWriter);
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
                }

                // Move the output XML files to the ingest folder
                MoveOutputFiles(outputFolder, ingestFolder);
                Console.WriteLine("DocuSign Retrieve Monitor - Listening for DocuSign Retreive index.csv file. Enter 'q' to quit.");
            }
        }
    }

    static void Main()
    {
        string inputFolder = @"C:\DS Retrieve Monitor\inputFolder";
        string outputFolder = @"C:\DS Retrieve Monitor\outputFolder";
        string loggingFolder = @"C:\DS Retrieve Monitor\Logging";
        string processedFolder = @"C:\DS Retrieve Monitor\processedFolder";
        string ingestFolder = @"C:\DS Retrieve\Ingest Folder";

        EnsureDirectoriesExist(inputFolder, outputFolder, loggingFolder, processedFolder, ingestFolder);

        FileSystemWatcher watcher = new FileSystemWatcher
        {
            Path = inputFolder,
            Filter = "index.csv",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        watcher.Created += (source, e) =>
        {
            Console.WriteLine($"File detected: {e.FullPath}");
            Task.Run(() => PromptUserAndProcessFile(e.FullPath, outputFolder, loggingFolder, processedFolder, ingestFolder));
        };

        watcher.EnableRaisingEvents = true;

        // Start the watchdog timer
        Task.Run(() => WatchdogTimer());

        Console.WriteLine("DocuSign Retrieve Monitor - Listening for DocuSign Retreive index.csv files. Enter 'q' to quit.");
        while (Console.Read() != 'q')
        {
            // Update the health check timestamp
            lastHealthCheck = DateTime.Now;
        }
    }

    static void PromptUserAndProcessFile(string inputFilePath, string outputFolder, string loggingFolder, string processedFolder, string ingestFolder)
    {
        Console.WriteLine($"[{DateTime.Now}] DocuSign Retreive index.csv file detected. Processing in 1 second");

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        Task.Run(() =>
        {
            if (Console.ReadKey(true).Key == ConsoleKey.Enter)
            {
                cts.Cancel();
                ProcessFile(inputFilePath, outputFolder, loggingFolder, processedFolder, ingestFolder);
            }
        }, token);

        Task.Delay(1000).ContinueWith(t =>
        {
            if (!token.IsCancellationRequested)
            {
                ProcessFile(inputFilePath, outputFolder, loggingFolder, processedFolder, ingestFolder);
            }
        });
    }

    static void WatchdogTimer()
    {
        while (true)
        {
            Task.Delay(healthCheckInterval).Wait();

            if (DateTime.Now - lastHealthCheck > watchdogTimeout)
            {
                SendNotification("The DocuSign Retrieve Monitor has stopped responding.");
                break;
            }
        }
    }

    static void SendNotification(string message)
    {
        try
        {
            MailMessage mail = new MailMessage();
            SmtpClient smtpServer = new SmtpClient("smtp.your-email.com");

            mail.From = new MailAddress("your-email@your-domain.com");
            mail.To.Add("recipient-email@domain.com");
            mail.Subject = "DocuSign Retrieve Monitor Alert";
            mail.Body = message;

            smtpServer.Port = 587;
            smtpServer.Credentials = new NetworkCredential("your-email@your-domain.com", "your-email-password");
            smtpServer.EnableSsl = true;

            smtpServer.Send(mail);
            Console.WriteLine("Notification sent successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send notification: {ex.Message}");
        }
    }
}