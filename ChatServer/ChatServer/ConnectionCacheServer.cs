using System;
using MyExtensions;
using System.Net;
using System.Collections.Generic;
using System.Net.Sockets;
using Newtonsoft.Json;
using MyExtensions.Logging;
using System.Linq;
using System.Text;

namespace ChatServer
{
   public class ConnectionCacheServer : SimpleTCPPinger
   {
      public const string LocalAddress = "127.0.0.1";
      private string chatAddress;
      private Dictionary<string, WebSocketInstance> connections;
      private readonly object connectionLock;
      private System.Timers.Timer connectionCleanup;

      //This is how the pinger identifies itself in the logs
      public override string Identifier
      {
         get { return "ChatProxy"; }
      }

      public override int ThreadSleepMilliseconds
      {
         get { return 100; }
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="ChatServer.ConnectionCacheServer"/> class.
      /// </summary>
      /// <param name="port">The port you want the cacher to run on</param>
      /// <param name="chatAddress">The fully qualified address that points to the chat websocket service</param>
      /// <param name="logger">The logger to use (defaults to none)</param>
      public ConnectionCacheServer(int port, string chatAddress, MyExtensions.Logging.Logger logger = null) : 
         base(IPAddress.Parse(LocalAddress), port, logger)
      {
         connections = new Dictionary<string, WebSocketInstance>();
         this.chatAddress = chatAddress;
         connectionLock = new object();
         connectionCleanup = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
         connectionCleanup.Elapsed += CleanupConnections;
         connectionCleanup.Enabled = true;
         //connectionCleanup = new System.Threading.Timer(
      }

      //Every time the pinger receives a message, these are the actions that are performed.
      //We perform everything in one action.
      protected override List<Action<byte[], System.Net.Sockets.NetworkStream>> MessageActions
      {
         get
         {
            return new List<Action<byte[], NetworkStream>>() { GetProxyMessage };
         }
      }

      private void CleanupConnections(Object sender, System.Timers.ElapsedEventArgs e)
      {
         IEnumerable<KeyValuePair<string, WebSocketInstance>> endedConnections;

         lock(connectionLock)
         {
            endedConnections = connections.Where(x => x.Value.TimeSinceLastRequest > TimeSpan.FromMinutes(1));
         }

         //Because I don't trust the websocket library, I do NOT close inside a lock. NO WAY.
         //But yeah, this goes through and closes all the websocket connections that haven't been flushed in a while.
         foreach (var keyValuePair in endedConnections)
         {
            Log("Cached connection \"" + keyValuePair.Key + "\" hasn't been serviced in a while. We're removing it now", LogLevel.Warning);
            keyValuePair.Value.Connection.Close();
         }

         //And then THIS loop removes all closed connections 
         lock (connectionLock)
         {
            foreach (string key in connections.Keys.ToList())
            {
               if (!connections[key].Connected)
               {
                  if(!endedConnections.Any(x => x.Key == key))
                     Log("Cached connection \"" + key + "\" closed by itself. We're removing it now", LogLevel.Warning);
                  connections.Remove(key);
               }
            }
         }
      }

      /// <summary>
      /// Write the given result object to the given stream. It is assumed that the stream
      /// is the PHP proxy page (for our system)
      /// </summary>
      /// <param name="result">The result object to write</param>
      /// <param name="stream">The stream to write it to.</param>
      private void WriteResultToStream(ProxyResult result, ref NetworkStream stream)
      {
         //First, serialize result. Then convert to bytes. Finally write.
         WriteToStream(JsonConvert.SerializeObject(result), ref stream);
         //Log("Result length: " + resultData.Length + ", Serialized: " + serializedResult, LogLevel.SuperDebug);
      }

      /// <summary>
      /// Write the given string data to the given stream. It is assumed that the stream
      /// is the PHP proxy page (for our system)
      /// </summary>
      /// <param name="data">The string to write</param>
      /// <param name="stream">The stream to write it to.</param>
      private void WriteToStream(string data, ref NetworkStream stream)
      {
         byte[] resultData = System.Text.Encoding.UTF8.GetBytes(data);
         stream.Write(resultData, 0, resultData.Length);
      }

      /// <summary>
      /// Close and remove the cached connection with the given proxyID
      /// </summary>
      /// <returns><c>true</c>, if connection was closed, <c>false</c> otherwise.</returns>
      /// <param name="proxyID">Proxy I.</param>
      private bool CloseConnection(string proxyID)
      {
         bool hasConnection = AtomicAction<bool>(() => connections.ContainsKey(proxyID));

         if (!hasConnection)
         {
            Log("Tried to close nonexistent connection: " + proxyID, LogLevel.Warning);
            return false;
         }

         WebSocketInstance connection = AtomicAction<WebSocketInstance>(() => connections[proxyID]);
         connection.Connection.Close();

         Log("Closed proxy connection: " + proxyID);

         return AtomicAction<bool>(() => connections.Remove(proxyID));
      }

      private void AtomicAction(Action atomicAction)
      {
         lock (connectionLock)
         {
            atomicAction();
         }
      }

      private T AtomicAction<T>(Func<T> atomicAction)
      {
         lock (connectionLock)
         {
            return atomicAction();
         }
      }

      private ProxyResult GetErrorResult(string message)
      {
         ProxyResult result = new ProxyResult(false);
         result.errors.Add(message);
         return result;
      }

      /// <summary>
      /// What happens when a message is received by the TCP pinger thingy
      /// </summary>
      /// <param name="message">Message.</param>
      /// <param name="stream">Stream.</param>
      private void GetProxyMessage(byte[] data, NetworkStream stream)
      {
         //Log("Got proxy message of length: " + data.Length, LogLevel.SuperDebug);

         CleanupConnections(null, null);

         string message = "";

         try
         {
            message = System.Text.Encoding.UTF8.GetString(data); //WebUtility.HtmlDecode(System.Text.Encoding.UTF8.GetString(data));
         }
         catch(Exception e)
         {
            Error("Proxy message not in UTF8 format! : " + e);
            WriteResultToStream(GetErrorResult("Message not in UTF8 format!"), ref stream);
            return;
         }

         try
         {
            dynamic json = JsonConvert.DeserializeObject(message);
            string type = json.type;
            string id = json.proxyID;
            bool connectionCached = AtomicAction<bool>(() => connections.ContainsKey(id));

            //ProxyStart is the only message type that does not need the connection to be cached, so it is unique.
            if(type == "proxyStart")
            {
               if(connectionCached)
               {
                  ProxyResult result = new ProxyResult(-1);
                  result.warnings.Add("You have already started a proxy session with this id");
                  WriteResultToStream(result, ref stream);
                  Log("Tried to start proxy connection when connection already started. ID: " + id);
               }
               else
               {
                  //Create a new websocket instance, open the websocket to the chat, and add it to our cache.
                  //Tell the proxy page (PHP query whatever) that everything is good.
                  WebSocketInstance newInstance = new WebSocketInstance(chatAddress, logger);
                  newInstance.Connection.Connect();
                  AtomicAction(() => connections.Add(id, newInstance));
                  WriteResultToStream(new ProxyResult(id), ref stream);
                  Log("Started new proxy connection with id: " + id);
               }
            }
            else
            {
               //All other messages require a valid proxy ID
               if(!connectionCached)
               {
                  List<string> keys = AtomicAction<List<string>>(() => connections.Keys.ToList());
                  WriteResultToStream(GetErrorResult("There is no open proxy session with this id"), ref stream);
                  Log("Tried to send message on proxy which was closed or does not exist: " + id);
                  Log("Available proxies: " + String.Join(",", keys));
               }
               else
               {
                  if (type == "proxySend")
                  {
                     string encoding = "text";

                     try 
                     { 
                        encoding = json.encoding; 
                     }
                     catch 
                     { 
                        encoding = "text"; 
                        Log("proxySend did not pass an encoding. Assuming 'text'", LogLevel.SuperDebug);
                     }

                     string chatData = json.data;

                     if(encoding == "base64")
                     {
                        byte[] decodedData = Convert.FromBase64String(chatData);
                        chatData = Encoding.UTF8.GetString(decodedData);
                     }

                     AtomicAction(() => connections[id].Connection.Send(chatData));
                     WriteResultToStream(new ProxyResult(true), ref stream);
                     Log("Got proxy sendthrough: " + message.Truncate(100), MyExtensions.Logging.LogLevel.Debug);
                  }
                  else if (type == "proxyReceive")
                  {
                     /*long lastRetrieveTicks = -1;

                     try 
                     { 
                        lastRetrieveTicks = json.lastretrieve; 
                     }
                     catch 
                     { 
                        lastRetrieveTicks = -1; 
                        Log("proxyReceive did not pass a lastretrieve. Assuming -1 (get all + flush)", LogLevel.SuperDebug);
                     }*/

                     //bool flush;
                     TimeSpan desiredBacklogRange; //= TimeSpan.FromMinutes(1);

                     try 
                     { 
                        desiredBacklogRange = TimeSpan.FromMilliseconds((int)json.backlog); 
                     }
                     catch 
                     { 
                        desiredBacklogRange = TimeSpan.FromMinutes(1); 
                        Log("proxyReceive did not pass a desired backlog. Assuming 60000 (1 minute)", 
                              LogLevel.SuperDebug);
                     }

                     try
                     {
                        flush = (bool)json.flush;
                     }
                     catch
                     {

                     }

                     List<Tuple<string,DateTime>> backlog = AtomicAction<List<Tuple<string,DateTime>>>(() =>
                        {
                           connections[id].FlushInboundBacklogBefore(DateTime.Now.AddMinutes(-10));
                           return connections[id].GetInboundBacklogSince(DateTime.Now.Subtract(desiredBacklogRange));
                        });
                     //If they're using the new system where you can give the
                     //tick count of the last message retrieved, do so.
                     //Otherwise, do the old flushing system.
                     if (lastRetrieveTicks >= 0)
                     {
                        SpecialRetrieve backlog = AtomicAction<SpecialRetrieve>(() => 
                           {
                              //Retrieve any messages since the APPARENT last retrieval time. Also store 
                              //the current retrieval time in case no messages are retrieved
                              DateTime messageRetrievalTime = DateTime.Now;
                              List<Tuple<string,DateTime>> messages = 
                                 connections[id].GetInboundBacklogSince(new DateTime(lastRetrieveTicks));

                              //Get rid of any old messages (older than 10 minutes)
                              connections[id].FlushInboundBacklogBefore(DateTime.Now.AddMinutes(-10));

                              return new SpecialRetrieve 
                              { 
                                 messages = messages.Select(x => x.Item1).ToList(), 
                                 lastretrieve = messages.Count > 0 ? 
                                    messages.Max(x => x.Item2.Ticks) : messageRetrievalTime.Ticks
                              };
                           });
                        WriteResultToStream(new ProxyResult(backlog), ref stream);
                     }
                     else
                     {
                        List<string> backlog = AtomicAction<List<string>>(() => 
                           {
                              List<Tuple<string,DateTime>> messages = 
                                 connections[id].GetInboundBacklogSince(new DateTime(0));
                              connections[id].FlushInboundBacklogBefore(DateTime.Now);
                              return messages.Select(x => x.Item1).ToList();
                           });
                        WriteResultToStream(new ProxyResult(backlog), ref stream);
                     }

                  }
                  else if (type == "proxyEventStream")
                  {
                     TimeSpan desiredBacklogRange; //= TimeSpan.FromMinutes(1);

                     try 
                     { 
                        desiredBacklogRange = TimeSpan.FromMilliseconds((int)json.backlog); 
                     }
                     catch 
                     { 
                        desiredBacklogRange = TimeSpan.FromMinutes(1); 
                        Log("proxyEventStream did not pass a desired backlog. Assuming 60000 (1 minute)", LogLevel.SuperDebug);
                     }

                     List<Tuple<string,DateTime>> backlog = AtomicAction<List<Tuple<string,DateTime>>>(() =>
                        {
                           connections[id].FlushInboundBacklogBefore(DateTime.Now.AddMinutes(-10));
                           return connections[id].GetInboundBacklogSince(DateTime.Now.Subtract(desiredBacklogRange));
                        });

                     StringBuilder output = new StringBuilder("retry: 2000\n");

                     foreach(Tuple<string,DateTime> messageData in backlog)
                     {
                        output.AppendFormat("id: {0}\n", messageData.Item2.Ticks);
                        output.AppendFormat("data: {0}\n\n", 
                              Convert.ToBase64String(Encoding.UTF8.GetBytes(messageData.Item1)));
                     }

                     WriteToStream(output.ToString(), ref stream);
                  }
                  else if (type == "proxyEnd")
                  {
                     CloseConnection(id);
                     WriteResultToStream(new ProxyResult(id), ref stream);
                  }
               }
            }
         }
         catch(Exception e)
         {
            Log("Couldn't parse proxy message " + message + " : " + e, MyExtensions.Logging.LogLevel.Warning);
            ProxyResult proxyError = new ProxyResult(false);
            proxyError.errors.Add("Could not parse message (if proxySend works, it's probably your data format. It must be an encoded JSON string)");
            proxyError.errors.Add("Your sent message: " + message);
            proxyError.errors.Add("The exact error: " + e.Message);
            WriteResultToStream(proxyError, ref stream);
         }
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

   public class ProxyResult 
   {
      public object result = true;
      public List<string> errors = new List<string>();
      public List<string> warnings = new List<string>();

      public ProxyResult() { }

      public ProxyResult(object result)
      {
         this.result = result;
      }
   }

   public class SpecialRetrieve
   {
      public List<string> messages = new List<string>();
      public long lastretrieve = 0;
   }

   public class WebSocketInstance
   {
      public WebSocketSharp.WebSocket Connection
      {
         get { return connection; }
      }

      public bool Connected
      {
         get { return connected; }
      }

      public DateTime LastRequest
      {
         get { return lastRequest; }
      }

      public TimeSpan TimeSinceLastRequest
      {
         get { return (DateTime.Now - lastRequest); }
      }

      private WebSocketSharp.WebSocket connection;
      private List<Tuple<string,DateTime>> backlog;
      private bool connected;
      private MyExtensions.Logging.Logger logger;
      private readonly object instanceLock;
      private DateTime lastRequest = DateTime.Now;

      public WebSocketInstance(string url, MyExtensions.Logging.Logger logger)
      {
         this.logger = logger;
         instanceLock = new object();
         backlog = new List<Tuple<string,DateTime>>();
         connected = true;
         connection = new WebSocketSharp.WebSocket(url);
         connection.OnMessage += OnMessage;
         connection.OnClose += OnClose;
      }

      public void Log(string message, LogLevel level = LogLevel.Normal)
      {
         logger.LogGeneral(message, level, "WebSocketInstance");
      }

      public List<Tuple<string,DateTime>> GetInboundBacklogSince(DateTime lastCheck)
      {
         List<Tuple<string,DateTime>> returnList;
         lastRequest = DateTime.Now;

         lock (instanceLock)
         {
            returnList = backlog.Where(x => x.Item2 > lastCheck).ToList(); 
         }

         return returnList;
      }

      public void FlushInboundBacklogBefore(DateTime endTime)
      {
         lock (instanceLock)
         {
            backlog = backlog.Where(x => x.Item2 > endTime).ToList();
         }
      }

      public void OnMessage(object sender, WebSocketSharp.MessageEventArgs messageEvent)
      {
         lock(instanceLock)
         {
            backlog.Add(Tuple.Create(messageEvent.Data, DateTime.Now));
         }
      }

      public void OnClose(object sender, WebSocketSharp.CloseEventArgs closeEvent)
      {
         connected = false;
      }
   }
}

