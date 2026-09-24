using System;
using System.IO;
using System.Windows.Forms;

namespace GodexIndustrial
{
    public class EventLogger
    {
        private readonly TextBox _outputTextBox;
        private readonly string _logDirectory;

        public EventLogger(TextBox outputTextBox)
        {
            _outputTextBox = outputTextBox;
            _logDirectory = AppStorage.EnsureDirectory("Logs");
        }

        public void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";

            // Log to UI
            if (_outputTextBox != null)
            {
                if (_outputTextBox.InvokeRequired)
                {
                    _outputTextBox.Invoke(new Action(() => AppendToTextBox(logEntry)));
                }
                else
                {
                    AppendToTextBox(logEntry);
                }
            }

            // Log to File
            LogToFile(logEntry);
        }

        private void AppendToTextBox(string logEntry)
        {
            _outputTextBox.AppendText(logEntry + Environment.NewLine);
        }

        private void LogToFile(string logEntry)
        {
            try
            {
                string fileName = $"log_{DateTime.Now:yyyyMMdd}.txt";
                string filePath = Path.Combine(_logDirectory, fileName);
                File.AppendAllText(filePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // In case of file access error, we can't do much but maybe log to console
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }
    }
}

