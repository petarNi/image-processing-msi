using System;
using System.IO;
using System.Threading.Tasks;

namespace MMS.Client.Services
{
    public class ClientLogger
    {
        private readonly string _logFilePath;

        public ClientLogger()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string logsDirectory = Path.Combine(baseDirectory, "logs");

            Directory.CreateDirectory(logsDirectory);

            _logFilePath = Path.Combine(logsDirectory, "client-log.txt");
        }

        public async Task InfoAsync(string user, string action, string details, long elapsedMs = 0)
        {
            string line =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] User={user} Action={action} ElapsedMs={elapsedMs} Details={details}";

            await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine);
        }

        public async Task ErrorAsync(string user, string action, string details, Exception ex, long elapsedMs = 0)
        {
            string line =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] User={user} Action={action} ElapsedMs={elapsedMs} Details={details} Exception={ex.Message}";

            await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine);
        }
    }
}