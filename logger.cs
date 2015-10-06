using System;
using System.Collections.Generic;
using System.Timers;
using System.IO;

namespace MyExtensions.Logging
{
   //This lets you instantiate a logging class, which you can push log messages to. You can
   //then force dumps to the console or a file yourself, or set up the auto dumping function,
   //which should be able to handle most use cases.
   public class Logger
   {
      public const LogLevel DefaultConsoleLogLevel = LogLevel.Normal;
      public const LogLevel DefaultFileLogLevel = LogLevel.Normal;

      private Queue<LogMessage> messages = new Queue<LogMessage>();
      private readonly object messageLock = new object();
      private Timer autoDumpTimer = new Timer();
      private readonly string logFile = "";
      public readonly int MaxMessages = 1000;
      private bool instantConsole = false;
      private LogLevel minConsoleLevel;
      private LogLevel minFileLevel;

      public static readonly Logger DefaultLogger = new Logger(100);

      //If you want to log to a file, you must specify it in the constructor
      public Logger(int maxMessages = -1, string logFile = "", LogLevel minConsoleLevel = DefaultConsoleLogLevel, 
         LogLevel minFileLevel = DefaultFileLogLevel)
      {
         this.minConsoleLevel = minConsoleLevel;
         this.minFileLevel = minFileLevel;

         //First, try to create the file if it doesn't exist
         if (!File.Exists (logFile) && !string.IsNullOrWhiteSpace(logFile))
            File.Create (logFile).Close();

         //If it still doesn't exist, that means it's not accessible
         if (File.Exists (logFile))
         {
            this.logFile = logFile;
         }
         else
         {
            Error("Cannot open logfile: " + logFile, "System");
         }

         if (maxMessages > 0)
            MaxMessages = maxMessages;
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
               if (!message.WasLoggedBy ("console") && message.Level >= minConsoleLevel)
               {
                  Console.WriteLine (message.ToString ());
                  message.SetLoggedBy ("console");
               }
            }

            if (tossMessages)
               messages.Clear ();
         }
      }

      //Dump all log messages out to file
      public bool DumpToFile(bool tossMessages = false)
      {
         if (!CanLogToFile)
            return false;
         
         lock (messageLock)
         {
            foreach (LogMessage message in messages)
            {
               if (!message.WasLoggedBy ("file") && message.Level >= minFileLevel)
               {
                  File.AppendAllText (logFile, message.ToString () + Environment.NewLine);
                  message.SetLoggedBy ("file");
               }
            }

            if (tossMessages)
               messages.Clear ();
         }

         return true;
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
            messages.Enqueue (new LogMessage(message, level, tag));

            //Remove messages if we have too many
            while (messages.Count > MaxMessages)
               messages.Dequeue ();
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
      private HashSet<string> loggedBy = new HashSet<string>();

      public LogMessage(string message, LogLevel level = LogLevel.Normal, string tag = "")
      {
         this.message = message;
         this.tag = tag;
         this.level = level;
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

      public bool WasLoggedBy(string loggerType)
      {
         return loggedBy.Contains (loggerType);
      }

      public void SetLoggedBy(string loggerType)
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
}
