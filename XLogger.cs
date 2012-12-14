using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Globalization;

public static class XLogger
{
    #region internals

    private delegate void LogMessageHandler(LogEntry entry, ILogFormatter formatter);
    private static event LogMessageHandler LogMessage;

    internal static bool Enabled = true;
    internal static bool Split = true;
    internal static bool Async = false;
    internal static LogType Type = LogType.File | LogType.Console | LogType.Context;
    internal static LogStatus Sensitivity = LogStatus.Info;
    internal static string Target = @"c:\X\logs.txt";
    internal static LogFormat Format = LogFormat.Text;

    private static ILogFormatter _Formatter;

    #endregion

    static XLogger()
    {
        // initialize log targets ...
        if ((Type & LogType.File) == LogType.File) LogMessage += (new FileLog()).Log;
        if ((Type & LogType.Console) == LogType.Console) LogMessage += (new ConsoleLog()).Log;
        if ((Type & LogType.EventLog) == LogType.EventLog) LogMessage += (new EventLog()).Log;

        // initialize formatter
        switch (Format)
        {
            case LogFormat.Text:
                _Formatter = new TextFormatter();
                break;
            case LogFormat.Xml:
                _Formatter = new XmlFormatter();
                break;
        }
    }

    #region Logging ...

    public static void Sep()
    {
        string sep = "----------------------------------------------------------------------------------------------";
        Log(new LogEntry(DateTime.Now, sep, LogStatus.Info));
    }

    public static void ShortSep()
    {
        string sep = "---------------------------------------------";
        Log(new LogEntry(DateTime.Now, sep, LogStatus.Info));
    }

    public static bool Info(string message)
    {
        Log(new LogEntry(DateTime.Now, message, LogStatus.Info));
        return true;
    }
    public static bool Info(string message, params string[] parameters)
    {
        try
        {
            if (parameters != null && parameters.Length != 0)
                message = string.Format(message, parameters);
        }
        catch { }

        return Info(message);
    }

    public static bool Error(Exception ex)
    {
        string message = String.Format("{0}: \n{1}", ex.Source, ex);
        Log(new LogEntry(DateTime.Now, message, LogStatus.Error));
        return false;
    }
    public static bool Error(string message)
    {
        Log(new LogEntry(DateTime.Now, message, LogStatus.Error));
        return false;
    }
    public static bool Error(string message, params string[] parameters)
    {
        try
        {
            if (parameters != null && parameters.Length != 0)
                message = string.Format(message, parameters);
        }
        catch { }

        return Error(message);
    }

    private static bool Log(IList<LogEntry> logEntries)
    {
        if (logEntries == null) return true;

        for (int i = 0; i < logEntries.Count; i++)
        {
            Log(new LogEntry(logEntries[i].Time, logEntries[i].Message, logEntries[i].Status));
        }

        return true;
    }
    private static void Log(LogEntry logEntry)
    {
        if (!Enabled || logEntry.Status < Sensitivity || LogMessage == null) return;
        LogMessage(logEntry, _Formatter);
    }

    #endregion

    #region Helper Classes

    private class LogEntry
    {
        public LogEntry(DateTime time, string message, LogStatus status)
        {
            _Time = time;
            _Message = message;
            _Status = status;
        }

        private DateTime _Time;
        public DateTime Time { get { return _Time; } set { _Time = value; } }

        private string _Message;
        public string Message { get { return _Message; } set { _Message = value; } }

        private LogStatus _Status;
        public LogStatus Status { get { return _Status; } set { _Status = value; } }
    }
    private class LogContext
    {
        //private static List<string> _Logs = new List<string>();
    }

    private interface ILogTarget
    {
        void Log(LogEntry entry, ILogFormatter formatter);
    }
    private interface ILogFormatter
    {
        string Format(LogEntry logEntry);
    }

    private class FileLog : ILogTarget
    {
        #region Fields

        private bool _Enabled = true;
        private string _Location = @"c:\X\logs.txt";

        #endregion

        public FileLog()
        {
            _Enabled = Initialize();
        }

        public void Log(LogEntry entry, ILogFormatter formatter)
        {
            if (!_Enabled) return;
            SplitLog();

            try
            {
                string logLine = formatter.Format(entry) + "\r\n";
                File.AppendAllText(_Location, logLine);
            }
            catch { return; }
        }

        private bool Initialize()
        {
            try
            {
                FileInfo _File = new FileInfo(_Location);
                if (!_File.Directory.Exists) _File.Directory.Create();
                if (!_File.Exists) using (_File.Create()) { }

                return true;
            }
            catch { return false; }
        }
        private void SplitLog()
        {
            try
            {
                FileInfo _File = new FileInfo(_Location);
                if (_Enabled && _File.Exists && _File.CreationTime.Date.CompareTo(DateTime.Now.Date) != 0)
                {
                    string backupFileName = string.Format(@"{0}\logs_{1}.txt", _File.DirectoryName, _File.CreationTime.Date.ToString("yyyyMMdd"));
                    _File.MoveTo(backupFileName);

                    Initialize();
                }
            }
            catch { return; }
        }
    }
    private class EventLog : ILogTarget
    {
        #region Fields

        private System.Diagnostics.EventLog _EventLog;
        private const string _SourceName = "XLogger";
        private const string _LogName = "XLogger";

        #endregion

        public EventLog()
        {
            Initialize(_SourceName, _LogName);
        }
        //public EventLog( string sourceName , string logName )
        //{
        //    Initialize( sourceName , logName );
        //}

        public void Log(LogEntry entry, ILogFormatter formatter)
        {
            switch (entry.Status)
            {
                case LogStatus.Info:
                    Log(entry, EventLogEntryType.Information, formatter);
                    break;
                case LogStatus.Error:
                    Log(entry, EventLogEntryType.Error, formatter);
                    break;
                default:
                    Log(entry, EventLogEntryType.Warning, formatter);
                    break;
            }
        }

        #region Helpers

        private bool Initialize(string sourceName, string logName)
        {
            if (string.IsNullOrEmpty(sourceName) || string.IsNullOrEmpty(logName)) return false;

            try
            {
                EventSourceCreationData eventSourceCreationData = new EventSourceCreationData(sourceName, logName);
                if (!System.Diagnostics.EventLog.SourceExists(sourceName)) System.Diagnostics.EventLog.CreateEventSource(eventSourceCreationData);

                _EventLog = new System.Diagnostics.EventLog("Application", ".", sourceName);

                return true;
            }
            catch (Exception x)
            {
                XLogger.Error(x.ToString());
                Console.WriteLine("XLogger.EventLog.Initialize ... Exception: " + x);
                return false;
            }
        }
        private void Log(LogEntry entry, EventLogEntryType logType, ILogFormatter formatter)
        {
            string logLine = formatter.Format(entry);

            try { _EventLog.WriteEntry(logLine, logType); }
            catch { return; }
        }

        #endregion
    }
    private class ConsoleLog : ILogTarget
    {
        public void Log(LogEntry entry, ILogFormatter formatter)
        {
            Console.WriteLine(formatter.Format(entry));
        }
    }
    //private class ContextLog : ILogTarget
    //{
    //    [ThreadStaticAttribute]
    //    private static LogContext _LogContext;

    //    public void Log( LogEntry entry , ILogFormatter formatter )
    //    {
    //        Console.WriteLine( formatter.Format( entry ) );
    //    }
    //}

    private class TextFormatter : ILogFormatter
    {
        public string Format(LogEntry logEntry)
        {
            if (logEntry == null) return "";

            return string.Format(CultureInfo.InvariantCulture, "{0}[{1}] [{2}] {3}{4}"
                , (logEntry.Status == LogStatus.Error) ? "\n" : String.Empty
                , logEntry.Time.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)
                , logEntry.Status
                , logEntry.Message
                , (logEntry.Status == LogStatus.Error) ? "\n" : String.Empty);
        }
    }
    private class XmlFormatter : ILogFormatter
    {
        public string Format(LogEntry logEntry)
        {
            throw new NotImplementedException();
        }
    }

    #endregion

    #region Enums

    [Flags]
    public enum LogType { File = 1, EventLog = 2, Console = 4, Context = 5, }
    public enum LogStatus { Info, Error, }
    public enum LogFormat { Text, Xml, }

    #endregion
}

///src: XCore library
///http://xcore.codeplex.com/