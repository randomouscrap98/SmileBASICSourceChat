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

[assembly: AssemblyVersion("0.3.*")]

namespace ChatServer
{
   public class ChatRunner 
   {
      private static WebSocketServer webSocketServer;
      private static AuthServer authServer;
      private static MyExtensions.Logging.Logger logger;
      private static ModuleLoader loader;
      private static MyExtensions.Options options = new MyExtensions.Options();

      public const string ConfigFile = "config.xml";
      public const string ModuleConfigFile = "moduleConfig.xml";
      public const string OptionTag = "main";

      public static List<ModuleSystem.Module> ActiveModules
      {
         get { return loader.ActiveModules; }
      }

      public static void Main()
      {
         Dictionary<string, object> defaultOptions = new Dictionary<string, object> {
            {"loggerBacklog", 100},
            {"loggerFileDumpInterval", 10},
            {"loggerFile", "log.txt"},
            {"authServerPort", 45696},
            {"chatServerPort", 45695},
            {"chatTimeout", 3},
            {"messageBacklog", 1000},
            {"messageSend", 10},
            {"acceptedTags", "general, admin, offtopic"},
            {"globalTag", "any"},
            {"userUpdateInterval", 60},
            {"inactiveMinutes", 5},
            {"spamBlockSeconds", 20},
            {"buildHourModifier", 4},
            {"website", "http://development.smilebasicsource.com"}
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

         //Set up the logger
         logger = new MyExtensions.Logging.Logger (options.GetAsType<int>(OptionTag, "loggerBacklog"), 
            options.GetAsType<string>(OptionTag, "loggerFile"));
         logger.StartAutoDumping(options.GetAsType<int>(OptionTag, "loggerFileDumpInterval"));
         logger.StartInstantConsole();

         logger.Log("This exe was built on: " + MyBuildDate().ToString());

         //Set up the module system
         loader = new ModuleLoader(logger);
         List<Type> extraModules = new List<Type>(){typeof(GlobalModule)};

         //Oops, couldn't load the modules. What the heck?
         if (!loader.Setup("moduleConfig.xml", extraModules))
         {
            logger.LogGeneral("Module loader failed. Cannot continue", MyExtensions.Logging.LogLevel.FatalError);
            Finish();
            return;
         }

         //First set up the auth server
         authServer = new AuthServer(options.GetAsType<int>(OptionTag, "authServerPort"), logger);

         if(!authServer.Start())
         {
            logger.LogGeneral("Authorization server could not be started!",
               MyExtensions.Logging.LogLevel.FatalError, "Auth");
            Finish("Fatal error. Exiting");
            return;
         }
         else
         {
            logger.Log ("Authorization server running on port " + authServer.Port);
         }

         //As an in-between, set some chat parameters
         Chat.SetChatParameters(options.GetAsType<int>(OptionTag, "messageBacklog"),
            options.GetAsType<int>(OptionTag, "messageSend"), 
            options.GetAsType<string>(OptionTag, "acceptedTags"),
            options.GetAsType<string>(OptionTag, "globalTag"));

         //Also set user parameters
         User.SetUserParameters(options.GetAsType<int>(OptionTag, "spamBlockSeconds"),
            options.GetAsType<int>(OptionTag, "inactiveMinutes"),
            options.GetAsType<string>(OptionTag, "website"));
         
         //Now, set up websocket server
         webSocketServer = new WebSocketServer(options.GetAsType<int>(OptionTag, "chatServerPort"));
         webSocketServer.AddWebSocketService<Chat> ("/chat", () => new Chat(loader.ActiveModules, 
            options.GetAsType<int>(OptionTag, "userUpdateInterval"), logger)
               {
                  Protocol = "chat",
                  IgnoreExtensions = true,
               });

         webSocketServer.WaitTime = TimeSpan.FromSeconds(options.GetAsType<int>(OptionTag, "chatTimeout"));
         webSocketServer.Start();

         if (webSocketServer.IsListening) 
         {
            logger.Log("Listening on port " + webSocketServer.Port + " with services:");

            foreach (var path in webSocketServer.WebSocketServices.Paths)
               logger.Log ("- " + path);
         }

         //Link the auth server to the websocket server
         Chat.LinkAuthServer(authServer);

         Console.WriteLine(">> Press Q to stop the server...");

         //Now enter the "server loop" and run forever?
         while(true)
         {
            if(Console.KeyAvailable)
            {
               ConsoleKeyInfo key = Console.ReadKey();
               if(key.Key == ConsoleKey.Q)
                  break;
            }
            Thread.Sleep(AuthServer.ThreadSleepMilliseconds);
         }

         Finish();
      }

      //IDK a way to end everything.
      private static void Finish(string message = "Done")
      {
         logger.Log("Stopping auth server...");
         authServer.Stop();
         logger.Log("Stopping chat server...");
         webSocketServer.Stop();
         logger.Log("Stopping logger...");
         logger.Log(message);
         logger.DumpToFile();
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
         var version = Assembly.GetEntryAssembly().GetName().Version;
         return version.ToString();
      }

      public static DateTime MyBuildDate()
      {
         var version = Assembly.GetEntryAssembly().GetName().Version;
         var buildDateTime = new DateTime(2000, 1, 1).Add(new TimeSpan(
            TimeSpan.TicksPerDay * version.Build + // days since 1 January 2000
            TimeSpan.TicksPerSecond * 2 * version.Revision)); // seconds since midnight, (multiply by 2 to get original)

         buildDateTime = buildDateTime.AddHours(options.GetAsType<int>(OptionTag, "buildHourModifier"));

         return buildDateTime;
      }
   }
}
