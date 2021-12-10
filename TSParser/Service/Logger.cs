using System.Collections.ObjectModel;
using System.Diagnostics;

namespace TSParser.Service
{
    public enum LogStatus
    {
        Info,
        Warning,
        Exception,
        Fatal,
        ETSI
    }
    public class Logger
    {
        public delegate void LogHandler(string msg);
        public static event LogHandler OnLogMessage;

        public static ObservableCollection<string> Log = new ObservableCollection<string>();

        public static void Send(LogStatus status, string additionalInfo = null, Exception ex = null)
        {
            try
            {
                switch (status)
                {
                    case LogStatus.Info:
                        {
                            string fulltext = $"[{DateTime.Now:dd.MM.yyy HH:mm:ss.fff}] [INFO] [{ additionalInfo}] \r\n";
                            PrintLog(fulltext);
                        }
                        break;
                    case LogStatus.Warning:
                        {
                            string fulltext = $"[{DateTime.Now:dd.MM.yyy HH:mm:ss.fff}] [WARNING] [{additionalInfo}] \r\n";
                            PrintLog(fulltext);
                        }
                        break;
                    case LogStatus.Exception:
                        {
                            string fulltext = $"[{DateTime.Now:dd.MM.yyy HH:mm:ss.fff}] [EXCEPTION] in [{additionalInfo}] [{ex?.TargetSite.DeclaringType}] [{ex?.TargetSite.Name}] [{ex?.Message}] \r\n";
                            PrintLog(fulltext);
                        }
                        break;
                    case LogStatus.Fatal:
                        {
                            string fulltext = $"[{DateTime.Now:dd.MM.yyy HH:mm:ss.fff}] [FATAL] in [{additionalInfo}] [{ex?.TargetSite.DeclaringType}] [{ex?.TargetSite.Name}] [{ex?.Message}] \r\n";
                            PrintLog(fulltext);
                        }
                        break;
                    case LogStatus.ETSI:
                        {
                            string fulltext = $"[{DateTime.Now:dd.MM.yyy HH:mm:ss.fff}] [ETSI EN 101290] [{additionalInfo}] \r\n";
                            PrintLog(fulltext);
                            break;
                        }
                    default:
                        break;
                }
            }
            catch
            {
                // nothing to do
            }
        }

        private static void PrintLog(string fulltext)
        {
            Debug.Write($"{fulltext}");
            Log.Add(fulltext);
            OnLogMessage?.Invoke(fulltext);
        }
    }
}
