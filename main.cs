using MyHelper;
using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace ChatServer
{
   public class ChatRunner 
   {
      private static WebSocketServer webSocketServer;
      private static AuthServer authServer;

      public static void Main()
      {
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
   }
}
