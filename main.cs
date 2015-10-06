using MyHelper;
using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using ModuleSystem;
using ChatEssentials;
using System.Diagnostics;

[assembly: AssemblyVersion("1.0")]
[assembly: AssemblyFileVersion("1.0")]

namespace ChatServer
{
   public class ChatRunner 
   {
      public const string Version = "0.8.0.0";

      private static WebSocketServer webSocketServer;
      private static AuthServer authServer;
      private static MyExtensions.Logging.Logger logger;
      private static ModuleLoader loader;
      private static ChatManager manager = null;
      private static MyExtensions.Options options = new MyExtensions.Options();

      public const string ConfigFile = "config.json";
      public const string ModuleConfigFile = "moduleConfig.json";
      public const string OptionTag = "main";
      public const string LogTag = "System";

      public static List<ModuleSystem.Module> ActiveModules
      {
         get { return loader.ActiveModules; }
      }

      public static BandwidthContainer Bandwidth
      {
         get
         {
            if (manager != null)
               return (BandwidthContainer)manager.Bandwidth;

            return new BandwidthContainer();
         }
      }

      public static void Main()
      {
         bool restart = false;

         do
         {
            Dictionary<string, object> defaultOptions = new Dictionary<string, object> {
               { "loggerBacklog", 100 },
               { "loggerFileDumpInterval", 10 },
               { "loggerFile", "log.txt" },
               { "authServerPort", 45696 },
               { "chatServerPort", 45695 },
               { "chatTimeout", 3 },
               { "messageBacklog", 1000 },
               { "messageSend", 10 },
               { "acceptedTags", "general, admin, offtopic" },
               { "globalTag", "any" },
               { "userUpdateInterval", 60 },
               { "inactiveMinutes", 5 },
               { "spamBlockSeconds", 20 },
               { "buildHourModifier", -4 },
               { "website", "http://development.smilebasicsource.com" },
               { "shutdownSeconds", 5 },
               { "saveFolder", "save" },
               { "saveInterval", 300 },
               { "moduleWaitSeconds", 5 },
               { "fakeAuthentication", false },
               { "consoleLogLevel", "Normal" },
               { "fileLogLevel", "Debug" },
               { "languageURL", "/languages/chatserver.json" }
            };

            //Set up and read options. We need to do this first so that the values can be used for init
            options = new MyExtensions.Options();
            options.AddOptions(OptionTag, defaultOptions);

            //Oops, can't load options from file. Just use defaults
            if (!options.LoadFromFile(ConfigFile))
            {
               Console.WriteLine("ERROR: Could not load main configuration file!");
               Console.WriteLine("Using default configuration");
            }

            //Oops, can't save configuration file? Wow, something really went wrong
            if (!options.WriteToFile(ConfigFile))
            {
               Console.WriteLine("ERROR: Could not write configuration file!");
               Console.WriteLine("Please check directory environment and file permissions");
            }

            //Before we do anything else, set up folders for data
            Directory.CreateDirectory(MyExtensions.StringExtensions.PathFixer(GetOption<string>("saveFolder")));

            //Set up the desired logging levels before setting up the logger
            MyExtensions.Logging.LogLevel fileLogLevel = MyExtensions.Logging.Logger.DefaultFileLogLevel;
            MyExtensions.Logging.LogLevel consoleLogLevel = MyExtensions.Logging.Logger.DefaultConsoleLogLevel;

            Enum.TryParse(GetOption<string>("fileLogLevel"), out fileLogLevel);
            Enum.TryParse(GetOption<string>("consoleLogLevel"), out consoleLogLevel);

            //Set up the logger
            logger = new MyExtensions.Logging.Logger(GetOption<int>("loggerBacklog"), GetOption<string>("loggerFile"),
               consoleLogLevel, fileLogLevel);
            logger.StartAutoDumping(GetOption<int>("loggerFileDumpInterval"));
            logger.StartInstantConsole();

            logger.Log("ChatServer v" + Version + ", built on " + MyBuildDate().ToString(), LogTag);

            //Set up the module system
            loader = new ModuleLoader(logger);
            List<Type> extraModules = new List<Type>(){ typeof(GlobalModule) };

            //Oops, couldn't load the modules. What the heck?
            if (!loader.Setup(ModuleConfigFile, extraModules))
            {
               logger.LogGeneral("Module loader failed. Cannot continue", 
                  MyExtensions.Logging.LogLevel.FatalError, LogTag);
               Finish();
               return;
            }

            //Set up the auth server
            if(GetOption<bool>("fakeAuthentication"))
               authServer = new AuthServerFake(GetOption<int>("authServerPort"), logger);
            else
               authServer = new AuthServer(GetOption<int>("authServerPort"), logger);

            if (!authServer.Start())
            {
               logger.LogGeneral("Authorization server could not be started!", MyExtensions.Logging.LogLevel.FatalError, LogTag);
               Finish("Fatal error. Exiting");
               return;
            }
            else
            {
               logger.Log("Authorization server running on port " + authServer.Port, LogTag);
            }

            //Set up languages
            LanguageTags languageTags = new LanguageTags(logger);

            if(!languageTags.InitFromURL(GetOption<string>("website") + GetOption<string>("languageURL")))
               logger.Warning("Couldn't load language tags! Hopefully the default will suffice");

            //Set up the chat manager (the program that provides data to each chat instance)
            manager = new ChatManager(loader, authServer, languageTags,
               GetOption<int>("messageBacklog"), GetOption<int>("messageSend"),
               GetOption<int>("saveInterval"), GetOption<int>("moduleWaitSeconds"),
               GetOption<string>("acceptedTags"), GetOption<string>("globalTag"),
               GetOption<string>("saveFolder"), logger);

            //Also set user parameters
            User.SetUserParameters(GetOption<int>("spamBlockSeconds"),
               GetOption<int>("inactiveMinutes"),
               GetOption<string>("website"));
         
            //Now, set up websocket server
            webSocketServer = new WebSocketServer(GetOption<int>("chatServerPort"));
            webSocketServer.AddWebSocketService<Chat>("/chat", () => new Chat(GetOption<int>("userUpdateInterval"), manager) {
               Protocol = "chat",
               IgnoreExtensions = true,
            });

            webSocketServer.WaitTime = TimeSpan.FromSeconds(GetOption<int>("chatTimeout"));
            webSocketServer.Start();

            //If chat server is running, show services
            if (webSocketServer.IsListening)
            {
               logger.Log("Chat Server listening on port " + webSocketServer.Port + " with services:", LogTag);

               foreach (var path in webSocketServer.WebSocketServices.Paths)
                  logger.Log("- " + path, LogTag);
            }

            int ShutdownSeconds = GetOption<int>("shutdownSeconds");
            Console.WriteLine(">> Press Q to nicely quit the server...");
            Console.WriteLine(">> Press R to nicely restart the server...");
            Console.WriteLine(">> Press X to forcibly stop the server...");

            restart = false;

            //Now enter the "server loop" and run forever?
            while (true)
            {
               if (Console.KeyAvailable)
               {
                  ConsoleKeyInfo key = Console.ReadKey();
                  if (key.Key == ConsoleKey.Q)
                  {
                     Console.WriteLine();
                     Console.WriteLine("Stopping server in " + ShutdownSeconds + " seconds...");
                     manager.Broadcast((new SystemMessageJSONObject() { 
                        message = "System is shutting down in " + ShutdownSeconds + " seconds for maintenance..."
                     }).ToString());
                     Thread.Sleep(ShutdownSeconds * 1000);
                     break;
                  }
                  else if (key.Key == ConsoleKey.X)
                  {
                     break;
                  }
                  else if (key.Key == ConsoleKey.R)
                  {
                     restart = true;
                     break;
                  }
               }
               Thread.Sleep(AuthServer.ThreadSleepMilliseconds);
            }
               
            if(!Finish())
            {
               return;
            }

         } while(restart);
      }

      private static T GetOption<T>(string optionTag)
      {
         return options.GetAsType<T>(OptionTag, optionTag);
      }

      //IDK a way to end everything.
      private static bool Finish(string message = "Done")
      {
         logger.Log("Stopping auth server...", LogTag);
         authServer.Stop();
         logger.Log("Stopping chat server...", LogTag);
         webSocketServer.Stop();
         logger.Log("Stopping chat manager...", LogTag);
         manager.Stop();
         logger.Log("Stopping logger...", LogTag);
         logger.Log(message);
         logger.DumpToFile();

         DateTime start = DateTime.Now;

         while (webSocketServer.IsListening || authServer.Running)
         {
            Thread.Sleep(100);

            if((DateTime.Now - start).TotalSeconds < 5)
            {
               Console.WriteLine("-Can't stop the server!");
               return false;
            }
         }

         return true;
      }

      //Something to show build time.
      private static DateTime RetrieveLinkerTimestamp()
      {
         string filePath = System.Reflection.Assembly.GetCallingAssembly().Location;
         const int c_PeHeaderOffset = 60;
         const int c_LinkerTimestampOffset = 8;
         byte[] b = new byte[2048];
         System.IO.Stream s = null;

         try
         {
            s = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            s.Read(b, 0, 2048);
         }
         finally
         {
            if (s != null)
            {
               s.Close();
            }
         }

         int i = System.BitConverter.ToInt32(b, c_PeHeaderOffset);
         int secondsSince1970 = System.BitConverter.ToInt32(b, i + c_LinkerTimestampOffset);
         DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
         dt = dt.AddSeconds(secondsSince1970);
         dt = dt.ToLocalTime();
         return dt;
      }

      public static string AssemblyVersion()
      {
         return Version; //FileVersion().ToString();
      }

      public static Version FileVersion()
      {
         FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
         return new Version(fvi.FileVersion);
      }

      public static DateTime MyBuildDate()
      {
         try
         {
            string date = File.ReadAllText("build.txt");
            DateTime buildDate = MyExtensions.DateExtensions.FromUnixTime((double)int.Parse(date));
            //buildDate = buildDate.AddHours(GetOption<int>("buildHourModifier"));
            return buildDate;
         }
         catch
         {
            return new DateTime(0);
         }
      }
   }
}
