using System;
using System.Collections.Generic;
using System.Timers;
using System.IO;
using System.Linq;

namespace MyExtensions.Logging
{
   //This lets you instantiate a logging class, which you can push log messages to. You can
   //then force dumps to the console or a file yourself, or set up the auto dumping function,
   //which should be able to handle most use cases.
   public class Logger
   {
      public const int MaxFileBuffer = 10000;
      public const LogLevel DefaultConsoleLogLevel = LogLevel.Normal;
      public const LogLevel DefaultFileLogLevel = LogLevel.Normal;

      private Queue<LogMessage> messages = new Queue<LogMessage>();
      private Queue<LogMessage> fileBuffer = new Queue<LogMessage>();
      private readonly object messageLock = new object();
      private Timer autoDumpTimer = new Timer();
      private readonly string logFile = "";
      public readonly int MaxMessages = 1000;
      private bool instantConsole = true;
      private LogLevel minConsoleLevel;
      private LogLevel minFileLevel;

      public static readonly Logger DefaultLogger = new Logger(100);

      //If you want to log to a file, you must specify it in the constructor
      public Logger(int maxMessages = -1, string logFile = "", LogLevel minConsoleLevel = DefaultConsoleLogLevel, 
         LogLevel minFileLevel = DefaultFileLogLevel)
      {
         this.minConsoleLevel = minConsoleLevel;
         this.minFileLevel = minFileLevel;

         //First, try to create the file if it doesn't exist (and a file was given)
         if (!string.IsNullOrWhiteSpace(logFile))
         {
            if (!File.Exists(logFile))
               File.Create(logFile).Close();

            //If it still doesn't exist, that means it's not accessible
            if (File.Exists(logFile))
               this.logFile = logFile;
            else
               Error("Cannot open logfile: " + logFile, "System");
         }

         if (maxMessages > 0)
            MaxMessages = maxMessages;
      }

      public List<LogMessage> GetMessages()
      {
         lock (messageLock)
         {
            return messages.Select(x => new LogMessage(x)).ToList();
         }
      }

      public List<LogMessage> GetFileBuffer()
      {
         lock (messageLock)
         {
            return fileBuffer.Select(x => new LogMessage(x)).ToList();
         }
      }

      public bool CanLogToFile
      {
         get { return !string.IsNullOrWhiteSpace (logFile); }
      }

      //Begin the timer event for dumping to console and file. Given interval must be in seconds.
      public void StartAutoDumping(int secondInterval)
      {
         autoDumpTimer.Interval = 1000 * secondInterval;
         autoDumpTimer.Elapsed += AutoDump;
         autoDumpTimer.Start ();
         Log("Will start auto-dumping logs", "System");
      }

      //Stop dumping everywhere, jeez
      public void StopAutoDumping()
      {
         autoDumpTimer.Stop ();
      }

      //start dumping to console immediately when we get a log
      public void StartInstantConsole()
      {
         instantConsole = true;
      }

      //Stop dumping immediately to console
      public void StopInstantConsole()
      {
         instantConsole = false;
      }

      //Perform an auto dump. Will dump to both console and file.
      private void AutoDump(object source, ElapsedEventArgs e)
      {
         DumpToConsole ();
         DumpToFile ();
      }

      //Dump all log messages out to console.
      public void DumpToConsole(bool tossMessages = false)
      {
         lock (messageLock)
         {
            foreach (LogMessage message in messages)
            {
               if (!message.WasLoggedBy (LoggerType.Console) && message.Level >= minConsoleLevel)
               {
                  Console.WriteLine (message.ToString ());
                  message.SetLoggedBy (LoggerType.Console);
               }
            }

            if (tossMessages)
               messages.Clear ();
         }
      }

      //Dump all log messages out to file
      public bool DumpToFile(bool tossMessages = false)
      {
         lock (messageLock)
         {
            //Only mess with messages and files if we can actually write to a file
            if (CanLogToFile)
            {
               foreach (LogMessage message in messages.Union(fileBuffer).OrderBy(x => x.TimeStamp))
               {
                  if (!message.WasLoggedBy(LoggerType.File) && message.Level >= minFileLevel)
                  {
                     File.AppendAllText(logFile, message.ToString() + Environment.NewLine);
                     message.SetLoggedBy(LoggerType.File);
                  }
               }

               if (tossMessages)
                  messages.Clear ();
            }

            //Unconditionally toss the files in the file buffer
            fileBuffer.Clear();
         }

         return CanLogToFile;
      }

      public void BufferFileMessage(LogMessage message)
      {
         lock (messageLock)
         {
            fileBuffer.Enqueue(messages.Peek());

            //If our file buffer gets too large to even consider, it's probably
            //because we can't log to the file. Just throw out everything
            if (fileBuffer.Count > MaxFileBuffer)
               fileBuffer.Clear();
         }
      }

      //Add a message to the log
      public void Log(string message, string tag = "")
      {
         LogGeneral (message, LogLevel.Normal, tag);
      }

      //Add an error to the log
      public void Error(string message, string tag = "")
      {
         LogGeneral (message, LogLevel.Error, tag);
      }

      //Add a warning to the log
      public void Warning(string message, string tag = "")
      {
         LogGeneral (message, LogLevel.Warning, tag);
      }

      //General logging (try not to use this)
      public void LogGeneral(string message, LogLevel level = LogLevel.Normal, string tag = "")
      {
         lock (messageLock)
         {
            //Don't log unnecessary files
            if((int)level >= Math.Min((int)minFileLevel, (int)minConsoleLevel))
               messages.Enqueue (new LogMessage(message, level, tag));

            //Remove messages if we have too many
            while (messages.Count > MaxMessages)
            {
               //If we're about to dequeue a message that has not been logged by the file logger,
               //this is a problem. Log all messages to the file, then dequeue.
               if (!messages.Peek().WasLoggedBy(LoggerType.File) && CanLogToFile)
                  BufferFileMessage(messages.Peek());
               
               messages.Dequeue();
            }
         }

         if (instantConsole)
            DumpToConsole ();
      }
   }

   public enum LogLevel
   {
      Locks = 1,
      SuperDebug,
      Debug,
      Normal,
      Warning,
      Error,
      FatalError
   }

   //This class represents one message in the log. You can simply print this; the default format
   //for toString should suffice for most things.
   public class LogMessage
   {
      private string message = "";
      private string tag = "";
      private DateTime timestamp = DateTime.Now;
      private LogLevel level = LogLevel.Normal;
      private HashSet<LoggerType> loggedBy = new HashSet<LoggerType>();

      public LogMessage(string message, LogLevel level = LogLevel.Normal, string tag = "")
      {
         this.message = message;
         this.tag = tag;
         this.level = level;
      }

      public LogMessage(LogMessage copyMessage)
      {
         this.message = copyMessage.message;
         this.tag = copyMessage.tag;
         this.level = copyMessage.level;
         this.timestamp = copyMessage.timestamp;
         this.loggedBy = new HashSet<LoggerType>(copyMessage.loggedBy);
      }

      //Accessors
      public string Message
      {
         get { return message; }
      }

      public string Tag
      {
         get { return tag; }
      }

      public DateTime TimeStamp
      {
         get { return timestamp; }
      }

      public LogLevel Level
      {
         get { return level; }
      }

      public bool HasTag
      {
         get { return !string.IsNullOrWhiteSpace (tag); }
      }

      public bool WasLoggedBy(LoggerType loggerType)
      {
         return loggedBy.Contains (loggerType);
      }

      public void SetLoggedBy(LoggerType loggerType)
      {
         loggedBy.Add (loggerType);
      }

      //When dumping to a file or the console, you probably want this format
      public override string ToString ()
      {
         return string.Format ("[{0}]{1}{2}: {3}", TimeStamp.ToString("MM/dd/yy hh:mm:ss"),
            (level == LogLevel.Normal ? "" : "{" + level.ToString() + "}"), (HasTag ? "(" + Tag + ")" : ""), Message);
      }
   }

   public enum LoggerType
   {
      Console,
      File
   }
}
