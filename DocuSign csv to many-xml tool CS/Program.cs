using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

class Program
{
    static void CreateXmlFile(string envelopeId, List<string> rowData, List<string> headers, string outputFolder, int duplicateCount)
    {
        string fileName = duplicateCount > 0 ? $"{envelopeId}_{duplicateCount}.xml" : $"{envelopeId}.xml";
        string filePath = Path.Combine(outputFolder, fileName);

        XElement envelopeElement = new XElement("Envelope");
        for (int i = 0; i < headers.Count; i++)
        {
            envelopeElement.Add(new XElement(headers[i].Replace(" ", ""), rowData[i]));
        }

        envelopeElement.Save(filePath);
    }

    static void Main()
    {
        string inputFilePath = @"C:\DS Retrieve\inputFolder\index.csv";
        string outputFolder = @"C:\DS Retrieve\outputFolder";

        if (!File.Exists(inputFilePath))
        {
            Console.WriteLine($"Unable to open file: {inputFilePath}");
            return;
        }

        string[] lines = File.ReadAllLines(inputFilePath);
        if (lines.Length == 0)
        {
            Console.WriteLine("The input file is empty.");
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

                CreateXmlFile(envelopeId, rowData, headers, outputFolder, envelopeIdCount[envelopeId]);
            }
        }
    }
}
