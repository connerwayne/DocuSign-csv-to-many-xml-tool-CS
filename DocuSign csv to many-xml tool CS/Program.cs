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

    static void EnsureDirectoriesExist(string inputFilePath, string outputFolder, string loggingFolder)
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
    }

    //static void SendEmailNotification(string errorLogFilePath)   // Uncomment this method to enable email notifications /// also look for "static void SendEmailNotification(string errorLogFilePath)" method
    //{
    //    try
    //    {
    //        MailMessage mail = new MailMessage();
    //        SmtpClient smtpServer = new SmtpClient("smtp.your-email.com");

    //        mail.From = new MailAddress("your-email@your-domain.com");
    //        mail.To.Add("recipient-email@domain.com");
    //        mail.Subject = "Processing Error Notification";
    //        mail.Body = $"A processing error has occurred. Please check the log file at: {errorLogFilePath}";

    //        smtpServer.Port = 587;
    //        smtpServer.Credentials = new NetworkCredential("your-email@your-domain.com", "your-email-password");
    //        smtpServer.EnableSsl = true;

    //        smtpServer.Send(mail);
    //        Console.WriteLine("Email sent successfully.");
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"Failed to send email: {ex.Message}");
    //    }
    //}

    static void Main()
    {
        string inputFilePath = @"C:\DS Retrieve\inputFolder\index.csv";
        string outputFolder = @"C:\DS Retrieve\outputFolder";
        string loggingFolder = @"C:\DS Retrieve\Logging";
        string logFilePath = Path.Combine(loggingFolder, $"csv-to-many-xml-log-{DateTime.Now:yyyyMMddHHmmss}.txt");
        string errorLogFilePath = Path.Combine(loggingFolder, $"ProcessingErrors-{DateTime.Now:yyyyMMddHHmmss}.txt");

        EnsureDirectoriesExist(inputFilePath, outputFolder, loggingFolder);

        using (StreamWriter logWriter = new StreamWriter(logFilePath, true))
        using (StreamWriter errorLogWriter = new StreamWriter(errorLogFilePath, true))
        {
            try
            {
                if (!File.Exists(inputFilePath))
                {
                    string errorMessage = $"Unable to open file: {inputFilePath}";
                    errorLogWriter.WriteLine($"{DateTime.Now}: {errorMessage}");
                    errorLogWriter.Flush(); // Ensure the message is written to the file
                    //SendEmailNotification(errorLogFilePath); // Send email notification --- also look for "static void SendEmailNotification(string errorLogFilePath)" method
                    throw new FileNotFoundException(errorMessage);
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
        }
    }
}

