using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servidor {
    public static class Logger {
         
        public enum LogType {
            INFO,
            WARNING,
            ERROR
           
        }
        public static void WriteLog(LogType type, string message) {
            string logPath = ConfigurationManager.AppSettings["logPath"];
            
            using(StreamWriter sw = new StreamWriter(logPath, true)) {
                sw.WriteLine(DateTime.Now.ToString() + " - [" + type.ToString() + "] - " + message);
            }
        }
    }
}
