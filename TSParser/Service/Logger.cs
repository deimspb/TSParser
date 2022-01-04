// Copyright 2021 Eldar Nizamutdinov 
//  
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at 
//
//     http://www.apache.org/licenses/LICENSE-2.0 
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace TSParser.Service
{
    public class LogMessage
    {
        public LogStatus LogStatus { get; }
        public DateTime EventDateTime => DateTime.Now;
        public string? Message { get; }
        public Exception? Exception { get; }
        public LogMessage(LogStatus status, string? message, Exception? exception = null)
        {
            LogStatus = status;
            Message = message;
            Exception = exception;
        }
        public override string ToString()
        {
            if (Exception is not null)
                return $"[{EventDateTime:dd.MM.yyy HH:mm:ss.fff}] [{LogStatus}] [{Message}] [{Exception?.TargetSite?.DeclaringType}] [{Exception?.TargetSite?.Name}] [{Exception?.Message}] \r\n";
            return $"[{EventDateTime:dd.MM.yyy HH:mm:ss.fff}] [{LogStatus}] [{Message}] \r\n";
        }
    }
    public enum LogStatus
    {
        INFO,
        NotImplement,
        ETSI,
        WARNING,
        EXCEPTION,
        FATAL                
    }
    public class Logger
    {
        public delegate void LogHandler(LogMessage message);
        public static event LogHandler OnLogMessage = null!;

        public static void Send(LogStatus status, string? additionalInfo = null, Exception? ex = null)
        {
            try
            {
                PrintLog(new LogMessage(status, additionalInfo, ex));                
            }
            catch
            {
                // nothing to do
            }
        }       
        private static void PrintLog(LogMessage message)
        {
            Debug.Write(message);
#if (!DEBUG)            
            OnLogMessage?.Invoke(message);
#endif
        }
    }
}
