using System;
using MyExtensions;
using System.Net;
using System.Collections.Generic;
using System.Net.Sockets;
using Newtonsoft.Json;
using MyExtensions.Logging;

namespace ChatServer
{
   public class ConnectionCacheServer : SimpleTCPPinger
   {
      public const string LocalAddress = "127.0.0.1";
      private string chatAddress;
      private Dictionary<string, WebSocketInstance> connections;

      public ConnectionCacheServer(int port, string chatAddress, MyExtensions.Logging.Logger logger = null) : 
         base(IPAddress.Parse(LocalAddress), port, logger)
      {
         connections = new Dictionary<string, WebSocketInstance>();
         this.chatAddress = chatAddress;
      }

      protected override List<Action<byte[], System.Net.Sockets.NetworkStream>> MessageActions
      {
         get 
         {
            return new List<Action<byte[], NetworkStream>>() { GetProxyMessage };
         }
      }

      /// <summary>
      /// What happens when a message is received by the TCP pinger thingy
      /// </summary>
      /// <param name="message">Message.</param>
      /// <param name="stream">Stream.</param>
      private void GetProxyMessage(byte[] data, NetworkStream stream)
      {
         string message = "";

         try
         {
            message = System.Text.Encoding.UTF8.GetString(data);
         }
         catch(Exception e)
         {
            Error("Proxy message not in UTF8 format! : " + e);
         }

         Log("Got proxy message: " + message.Substring(0, 100), MyExtensions.Logging.LogLevel.SuperDebug);

         try
         {
            dynamic json = JsonConvert.DeserializeObject(message);
            string type = json.type;
            string key = json.key;

            if(!connections.ContainsKey(key))
            {
               connections.Add(key, new WebSocketInstance(chatAddress, logger));
            }

            if(type == "proxy_getMessages")
            {
               List<string> messages = connections[key].FlushInboundBacklog();
               string serializedMessages = JsonConvert.SerializeObject(messages);
               byte[] messagesData = System.Text.Encoding.UTF8.GetBytes(serializedMessages);
               stream.Write(messagesData, 0, messagesData.Length);
            }
            else
            {
               connections[key].Send(message);
            }
         }
         catch(Exception e)
         {
            Log("Couldn't parse proxy message " + message + " : " + e, MyExtensions.Logging.LogLevel.Warning);
         }
      }

      private string CreateBindMessage(int uid, string key)
      {
         Dictionary<string, string> message = new Dictionary<string, string>();
         message.Add("type", "bind");
         message.Add("key", key);
         message.Add("uid", uid.ToString());
         return JsonConvert.SerializeObject(message);
      }

      //Use this to cleanup the cached websocket connections.
      protected override void Cleanup()
      {
         foreach (WebSocketInstance connection in connections.Values)
         {
            connection.Connection.Close();
         }
      }

   }

   public class WebSocketInstance
   {
      public WebSocketSharp.WebSocket Connection
      {
         get { return connection; }
      }

      private WebSocketSharp.WebSocket connection;
      private List<string> inboundBacklog;
      private List<string> outboundBacklog;
      private bool bound;
      private MyExtensions.Logging.Logger logger;
      private readonly object instanceLock;

      public WebSocketInstance(string url, MyExtensions.Logging.Logger logger)
      {
         this.logger = logger;
         bound = false;
         instanceLock = new object();
         inboundBacklog = new List<string>();
         outboundBacklog = new List<string>();
         connection = new WebSocketSharp.WebSocket(url);
         connection.OnMessage += OnMessage;
      }

      public void Log(string message, LogLevel level = LogLevel.Normal)
      {
         logger.LogGeneral(message, level, "WebSocketInstance");
      }

      public void Send(string message)
      {
         //First, always add the message to the backlog. If we're bound, we can dump the whole backlog
         //(which has the effect of just sending the message in general if bound)
         lock (instanceLock)
         {
            outboundBacklog.Add(message);
         }

         if (bound)
         {
            //If bound, dump out the whole outbound backlog and then clear it.
            FlushOutboundBacklog();
         }
      }

      private void FlushOutboundBacklog()
      {
         foreach(string backloggedMessage in outboundBacklog)
            connection.Send(backloggedMessage);

         lock (instanceLock)
         {
            outboundBacklog.Clear();
         }
      }

      public List<string> FlushInboundBacklog()
      {
         List<string> backlog;

         lock (instanceLock)
         {
            backlog = new List<string>(inboundBacklog);
            inboundBacklog.Clear();
         }

         return backlog;
      }

      public void OnMessage(object sender, WebSocketSharp.MessageEventArgs messageEvent)
      {
         try
         {
            dynamic json = JsonConvert.DeserializeObject(messageEvent.Data);
            string type = json.type;

            if(type == "result")
            {
               string from = json.from;
               bool result = json.result;

               if(result == true && from == "bind")
               {
                  bound = true;
                  FlushOutboundBacklog();
               }
            }

            lock(instanceLock)
            {
               inboundBacklog.Add(messageEvent.Data);
            }
         }
         catch(Exception e)
         {
            Log("Could not parse chat message: " + messageEvent.Data + ", Ex: " + e, LogLevel.Error);
         }
      }
   }
}

