using NLog;

namespace LPS.Core.Debug
{
    public static class Logger
    {
        private static readonly NLog.Logger Default = LogManager.GetLogger("Default");

        public static void Init(string logFileName)
        {
            try
            {
                var path = Path.Join(Directory.GetCurrentDirectory(), "Config", "nlog.config");
                var logDirPath = Path.Join(Directory.GetCurrentDirectory(), "logs");

                LogManager.LoadConfiguration(path);
                LogManager.Configuration.Variables["logDir"] = logDirPath;
                LogManager.Configuration.Variables["fileName"] = logFileName;
            }
            catch (Exception)
            {
                Console.WriteLine("Error Initializing Logger");
                throw;
            }
        }

        public static void Info(params object[] msg)
        {
            Default.Info(String.Join("", msg));
        }

        public static void Debug(params object[] msg)
        {
            Default.Debug(String.Join("", msg));
        }

        public static void Warn(params object[] msg)
        {
            Default.Warn(String.Join("", msg));
        }

        public static void Error(Exception e, params object[] msg)
        {
            Default.Error(e, String.Join("", msg));
        }

        public static void Fatal(Exception e, params object[] msg)
        {
            Default.Fatal(e, String.Join("", msg));
        }
    }
}
