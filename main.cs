using MyHelper;
using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Reflection;
using Newtonsoft.Json;

[assembly: AssemblyVersion("0.1.*")]

namespace ChatServer
{
   public class ChatRunner 
   {
      private static WebSocketServer webSocketServer;
      private static AuthServer authServer;

      public static void Main()
      {
         Console.WriteLine ("This exe was built on: " + MyBuildDate().ToString());

         //First set up the auth server
         authServer = new AuthServer(45696, true);
         if(!authServer.Start())
         {
            Console.WriteLine("ERROR! Authorization server could not be started!");
            Finish("Fatal error. Exiting");
         }
         else
         {
            Console.WriteLine("Authorization server running on port {0}",
                  authServer.Port);
         }

         //Now, set up websocket server
         webSocketServer = new WebSocketServer(45695);
         webSocketServer.AddWebSocketService<Chat> ("/chat", () => new Chat()
               {
                  Protocol = "chat",
                  IgnoreExtensions = true,
               });

         webSocketServer.WaitTime = TimeSpan.FromSeconds(3);
         webSocketServer.Start();

         if (webSocketServer.IsListening) 
         {
            Console.WriteLine ("Listening on port {0} with services:", 
                  webSocketServer.Port);

            foreach (var path in webSocketServer.WebSocketServices.Paths)
               Console.WriteLine ("- {0}", path);
         }

         //Link the auth server to the websocket server
         Chat.LinkAuthServer(authServer);

         Console.WriteLine("Press Q to stop the server...");

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
         authServer.Stop();
         webSocketServer.Stop();
         Console.WriteLine();
         Console.WriteLine(message);
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

         return buildDateTime;
      }
   }
}
