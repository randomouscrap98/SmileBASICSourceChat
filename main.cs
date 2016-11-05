using MyHelper;
using System;
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
using System.Threading.Tasks;
using System.ComponentModel;
using System.Linq;
using MyWebSocket;

[assembly: AssemblyVersion("1.0")]
[assembly: AssemblyFileVersion("1.0")]

namespace ChatServer
{
   public class ChatRunner 
   {
      public const string Version = "2.6.2";

      private static AuthServer authServer;
      private static ConnectionCacheServer proxyServer = null;
      private static MyExtensions.Logging.Logger logger;
      private static ModuleLoader loader;
      private static ChatServer chatServer = null;
      private static Task serverWaitable = null;
      private static MyExtensions.Options options = new MyExtensions.Options();

      private static bool ShouldDie = false;
      private static readonly Object Lock = new Object();

      public static readonly DateTime Startup = DateTime.Now;

      public const string ConfigFile = "config.json";
      public const string ModuleConfigFile = "moduleConfig.json";
      public const string OptionTag = "main";
      public const string LogTag = "System";

      public static List<ModuleSystem.Module> ActiveModules
      {
         get { return loader.ActiveModules; }
      }

      public static ChatServer Server
      {
         get { return chatServer; }
      }

      public static BandwidthContainer Bandwidth
      {
         get
         {
            if (chatServer != null)
               return (BandwidthContainer)chatServer.Bandwidth;

            return new BandwidthContainer();
         }
      }

//      //This will reinitialize (hopefully) the websocket server.
//      private static void SetupWebsocket()
//      {
//         //Now, set up websocket server
//         webSocketServer = new WebSocketServer(GetOption<int>("chatServerPort"));
//         webSocketServer.AddWebSocketService<Chat>("/chatserver", () => new Chat(GetOption<int>("userUpdateInterval"), manager) {
//            Protocol = "chat",
//            IgnoreExtensions = true,
//         });
//
//         /*System.Net.ServicePointManager.ServerCertificateValidationCallback += (o, certificate, chain, errors) => true;
//         webSocketServer.SslConfiguration.ClientCertificateValidationCallback += (sender, ICertificatePolicy, chain, sslPolicyErrors) => true;
//         webSocketServer.SslConfiguration.ClientCertificateRequired = false;
//         webSocketServer.SslConfiguration.CheckCertificateRevocation = false;
//         webSocketServer.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Default |
//         System.Security.Authentication.SslProtocols.Ssl2 | System.Security.Authentication.SslProtocols.Tls11 |
//         System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Ssl3 |
//         System.Security.Authentication.SslProtocols.Tls;
//         webSocketServer.SslConfiguration.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(GetOption<string>("sslCertificate"));//"/etc/nginx/ssl/bundle.crt");
//         */
//
//         //Console.WriteLine("Keep clean: " + webSocketServer.KeepClean);
//         webSocketServer.WaitTime = TimeSpan.FromSeconds(GetOption<int>("chatTimeout"));
//         webSocketServer.Start();
//
//         //If chat server is running, show services
//         if (webSocketServer.IsListening)
//         {
//            logger.Log("Chat Server listening on port " + webSocketServer.Port + " with services:", LogTag);
//
//            foreach (var path in webSocketServer.WebSocketServices.Paths)
//               logger.Log("- " + path, LogTag);
//         }
//      }

      private static void SetupChatManager()
      {
         ChatServerSettings settings = new ChatServerSettings(GetOption<int>("chatServerPort"), "chatserver",
            () => { return new Chat(GetOption<int>("userUpdateInterval"), Server); }, logger);
         
         //Set up languages
         LanguageTags languageTags = new LanguageTags(logger);

         if(!languageTags.InitFromURL(GetOption<string>("website") + GetOption<string>("languageURL")))
            logger.Warning("Couldn't load language tags! Hopefully the default will suffice");

         settings.MaxUserQueryFailures = GetOption<int>("maxUserQueryFailures");
         settings.MaxMessageKeep = GetOption<int>("messageBacklog");
         settings.MaxMessageSend = GetOption<int>("messageSend");
         settings.MaxModuleWait = TimeSpan.FromSeconds(GetOption<int>("moduleWaitSeconds"));
         settings.SaveFolder = GetOption<string>("saveFolder");
         settings.GlobalTag = GetOption<string>("globalTag");
         settings.SaveInterval = TimeSpan.FromSeconds(GetOption<int>("saveInterval"));
         settings.PingInterval = TimeSpan.FromSeconds(GetOption<int>("chatTimeout"));
         settings.MonitorThreads = true;
         settings.AcceptedTags = GetOption<string>("acceptedTags").Split(",".ToCharArray(), 
            StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

         //I want to see the settings that are hard to see while running!
         settings.DumpSettings();

         chatServer = new ChatServer(settings, loader, authServer, languageTags);
         serverWaitable = chatServer.StartAsync();
//         if (!chatServer.Start())
//         {
//            logger.LogGeneral("WebSocket server couldn't be started!", MyExtensions.Logging.LogLevel.FatalError);
//            return false;
//         }
//         else
//         {
//            return true;
//         }
      }

      //Delay a request so the control can return to the caller. Eventually we'll get back to this and do the real request.
      private static void DelayedRequest(SystemRequest request)
      {
         if (request.Request == SystemRequests.Reset)
         {
            logger.Log("Attempting to uncleanly quit the server");
            ShouldDie = true;
         }
         else if (request.Request == SystemRequests.LockDeath)
         {
            logger.Log("Attempting to simulate lock death");
            chatServer.Lockup();
         }
         else if (request.Request == SystemRequests.SaveModules)
         {
            chatServer.SaveData();
         }
         else
         {
            logger.Warning("Got an unknown system request: " + request.Request.ToString());
         }
      }

      //So when the chat manager gets some request, it'll call this thing. We know what to do with it (probably)
      public static void PerformRequest(SystemRequest request)
      {
         BackgroundWorker worker = new BackgroundWorker();
         worker.DoWork += (object sender, DoWorkEventArgs e) => { Thread.Sleep((int)request.Timeout.TotalMilliseconds); };
         worker.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) => { DelayedRequest(request); };
         worker.RunWorkerAsync();
      }

      public static void Main()
      {
         bool restart = false;

         do
         {
            Dictionary<string, object> defaultOptions = new Dictionary<string, object> {
               { "loggerBacklog", 1000 },
               { "loggerFileDumpInterval", 10 },
               { "loggerFile", "log.txt" },
               { "authServerPort", 45696 },
               { "chatServerPort", 45695 },
               { "chatTimeout", 15 },
               { "messageBacklog", 1000 },
               { "messageSend", 10 },
               { "acceptedTags", "general, admin, offtopic" },
               { "globalTag", "any" },
               { "userUpdateInterval", 60 },
               { "inactiveMinutes", 5 },
               { "spamBlockSeconds", 20 },
               { "buildHourModifier", -4 },
               { "website", "http://development.smilebasicsource.com" },
               { "chatrequest", "GUID" },
               { "shutdownSeconds", 5 },
               { "saveFolder", "save" },
               { "saveInterval", 300 },
               { "cleanInterval", 5 },
               { "moduleWaitSeconds", 5 },
               { "maxQueryWaitSeconds", 1.0 },
               { "maxUserQueryFailures", 3 },
               { "fakeAuthentication", false },
               { "consoleLogLevel", "Normal" },
               { "fileLogLevel", "Debug" },
               { "languageURL", "/languages/chatserver.json" },
               { "automaticBlocking", true },
               { "crashDetectionTimeout", 10 },
               { "sslCertificate", "chatcert.p12" },
               { "policyReminderHours", 24 },
               { "ircServer", "irc.freenode.net" },
               { "ircChannel", "#smilebasic" },
               { "ircTag", "general" },
               { "threadpoolMultiplier" , 4.0 },
               { "connectionpoolMultiplier", 2.0 },
               { "staticChatProxy", false },
               { "staticChatProxyPort", 45691 }
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
            logger.Log("WebSocket Library v" + ChatServer.Version, LogTag);
            logger.Log("Extension Library v" + MyExtensions.MyExtensionsData.Version, LogTag);

            int workerThreads, ioThreads;
            ThreadPool.SetMaxThreads((int)Math.Ceiling(Environment.ProcessorCount * GetOption<double>("threadpoolMultiplier")), 
               (int)Math.Ceiling(Environment.ProcessorCount * GetOption<double>("connectionpoolMultiplier")));
            ThreadPool.GetMaxThreads(out workerThreads, out ioThreads);
            logger.Log("Using " + workerThreads + " general threads and " + ioThreads + " IO threads");

            //Set up the module system
            loader = new ModuleLoader(logger);
            List<Type> extraModules = new List<Type>(){ typeof(GlobalModule), typeof(DebugModule), 
               typeof(PmModule), typeof(SneakyModule), typeof(AdminModule) };

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

            //Also set user parameters
            User.SetUserParameters(logger, GetOption<int>("spamBlockSeconds"), GetOption<int>("inactiveMinutes"),
               GetOption<string>("website"), GetOption<bool>("automaticBlocking"), 
               GetOption<int>("policyReminderHours"), GetOption<double>("maxQueryWaitSeconds"),
               GetOption<string>("chatrequest"));

            //This starts up the server!
            SetupChatManager();

            if(GetOption<bool>("staticChatProxy"))
            {
               logger.LogGeneral("Chat proxy enabled: setting up", MyExtensions.Logging.LogLevel.Debug, LogTag);
               proxyServer = new ConnectionCacheServer(GetOption<int>("staticChatProxyPort"), 
                  "ws://127.0.0.1:" + chatServer.ChatSettings.Port + "/chatserver", logger);

               if(!proxyServer.Start())
               {
                  logger.LogGeneral("Proxy server could not be started!", MyExtensions.Logging.LogLevel.FatalError, LogTag);
                  Finish("Fatal error. Exiting");
                  return;
               }
               else
               {
                  logger.Log("Proxy server running on port " + proxyServer.Port, LogTag);
               }
            }

            int ShutdownSeconds = GetOption<int>("shutdownSeconds");
            Console.WriteLine(">> Press Q to nicely quit the server...");
            Console.WriteLine(">> Press R to nicely restart the server...");
            Console.WriteLine(">> Press X to forcibly stop the server...");

            restart = false;

            //Just before we start, we should ummmm set up the "please try again" timer for the stupid library
            System.Timers.Timer timer = new System.Timers.Timer(GetOption<int>("chatTimeout") * 1000);
            timer.Elapsed += CheckServer;
            timer.Start();

            try
            {
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
                        chatServer.GeneralBroadcast((new SystemMessageJSONObject() { 
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

                  //hmm, someone wants us to die. OK then
                  if(ShouldDie)
                  {
                     throw new Exception("Server got an internal death signal");
                  }

                  Thread.Sleep(AuthServer.ThreadSleepMilliseconds);
               }
            }
            catch(Exception e)
            {
               logger.Log("Caught unhandled exception: " + e);
               logger.Log("Trying to die...");
               chatServer.SaveData();
               logger.Log("Dying...");
               logger.DumpToFile();
               System.Environment.Exit(99);
               return;
            }
               
            if(!Finish())
            {
               return;
            }

         } while(restart);


         System.Environment.Exit(0);
      }

      private static void CheckServer(object sender, System.Timers.ElapsedEventArgs e)
      {
         if (Monitor.TryEnter(Lock))
         {
            try
            {
               if(!AttemptLock(chatServer.managerLock))
                  logger.LogGeneral("Manager is the one in a deadlock", MyExtensions.Logging.LogLevel.Debug);
               else
                  logger.LogGeneral("Chat server seems OK", MyExtensions.Logging.LogLevel.SuperDebug);
            }
            finally
            {
               Monitor.Exit(Lock);
            }
         }
         else
         {
            logger.Warning("Couldn't check the status of the server. Maybe it's broken?");
         }
      }

      private static bool AttemptLock(object lockthing)
      {
         bool result = false;

         if(Monitor.TryEnter(lockthing, TimeSpan.FromSeconds(GetOption<int>("crashDetectionTimeout"))))
         {
            try
            {
               result = true;
            }
            finally
            {
               Monitor.Exit(lockthing);
            }
         }
         else
         {
            //OK here we need to wreck some stuff
            logger.Warning("The chat server appears unresponsive. Attempting reset now.");
            ShouldDie = true;
         }

         return result;
      }

      private static T GetOption<T>(string optionTag)
      {
         return options.GetAsType<T>(OptionTag, optionTag);
      }

      //IDK a way to end everything.
      private static bool Finish(string message = "Done")
      {
         if (authServer != null)
         {
            logger.Log("Stopping auth server...", LogTag);
            authServer.Stop();
         }
         if (proxyServer != null)
         {
            logger.Log("Stopping proxy server...", LogTag);
            proxyServer.Stop();
         }
         if (chatServer != null)
         {
            logger.Log("Stopping chat server...", LogTag);
            chatServer.SafeStop();
         }
         logger.Log("Stopping logger...", LogTag);
         logger.Log(message);
         logger.DumpToFile();

         DateTime start = DateTime.Now;
         TimeSpan wait = TimeSpan.FromSeconds(5);

         while (/*chatServer.Running ||*/ authServer.Running)
         {
            Thread.Sleep(100);

            if((DateTime.Now - start) >= wait)
            {
               Console.WriteLine("-Can't stop the authserver!");
               return false;
            }
         }

         //Now the simple shutdown for the websocket server
         if(serverWaitable != null && !serverWaitable.Wait(wait))
         {
            Console.WriteLine("-Can't stop the websocket server!");
            return false;
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
         return Version;
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
            return buildDate;
         }
         catch
         {
            return new DateTime(0);
         }
      }

      public static DateTime LastCrash()
      {
         try
         {
            string date = File.ReadAllText("crash.txt");
            DateTime crashDate = MyExtensions.DateExtensions.FromUnixTime((double)int.Parse(date));
            return crashDate;
         }
         catch
         {
            return new DateTime(0);
         }
      }
   }
}
