using System;
using IrcDotNet;
using System.Linq;
using System.Collections.Generic;
using MyExtensions;
using System.Threading;
using System.Net;

namespace ChatServer
{
   public delegate void IRCRelayMessageHandler(string username, string message);
   public delegate void IRCRelayJoinLeaveHandler(string username);

   public class SimpleIRCRelay //: IDisposable
   {
      //public const bool DisableIRC = true;
      public const int DefaultUsernameCharacters = 16;
      public const string DefaultRelayTag = "_r";

      public static readonly SimpleIRCRelay DefaultRelay = new SimpleIRCRelay("default", "default", "default");

      public event IRCRelayMessageHandler IRCRelayMessageEvent;
      public event IRCRelayJoinLeaveHandler IRCRelayJoinEvent;
      public event IRCRelayJoinLeaveHandler IRCRelayLeaveEvent;

      private StandardIrcClient client = null;//new StandardIrcClient();
      private string ircServer = "";
      private string ircChannel = "";
      private string baseUsername = "";
      private bool fullConnect = false;
      private bool connectionFailed = false;
      private int nextRelayID = -1;

      private int maxUsernameCharacters = DefaultUsernameCharacters;
      private string relayTag = DefaultRelayTag;

      private MyExtensions.Logging.Logger logger = MyExtensions.Logging.Logger.DefaultLogger;

      public SimpleIRCRelay(string server, string channel, string username, MyExtensions.Logging.Logger logger = null)
      {
         ircServer = server;
         ircChannel = channel;
         baseUsername = username;

         if (logger != null)
            this.logger = logger;
      }
//
//      public void Dispose()
//      {
//         client.Dispose();
//      }

      ~SimpleIRCRelay()
      {
         DisposeClient();
      }

      public void DisposeClient()
      {
         if (client != null)
         {
            client.Dispose();
            client = null;
         }
      }

      public void OnJoin(string username)
      {
         if (IRCRelayJoinEvent != null)
            IRCRelayJoinEvent(username + "-irc");
      }

      public void OnLeave(string username)
      {
         if (IRCRelayLeaveEvent != null)
            IRCRelayLeaveEvent(username + "-irc");
      }

      public void OnMessageReceive(string username, string message)
      {
         if (IRCRelayMessageEvent != null)
            IRCRelayMessageEvent(username + "-irc", message);
      }

      public bool Connected
      {
         get { return client != null && fullConnect && !connectionFailed; }
      }

      public bool ConnectionFailed
      {
         get { return connectionFailed; }
      }

      public string Username
      {
         get
         {
            if (!Connected)
               return "";
            
            return client.LocalUser.NickName + "-irc"; 
         }
      }

      public string InternalIRCUsername
      {
         get
         {
            if (!Connected)
               return "";

            return client.LocalUser.NickName; 
         }
      }

      //Maximum characters allowed in a username for this IRC channel
      public int MaxUsernameCharacters
      {
         get { return maxUsernameCharacters; }
         set
         {
            if (value > 0 && value < 256)
               maxUsernameCharacters = value;
         }
      }

      //The tag which indicates that this is a relay user and not an actual IRC client.
      public string RelayTag
      {
         get { return relayTag; }
         set
         {
            if (value != null && value.Length < maxUsernameCharacters)
               relayTag = value;
         }
      }

      public void Log(string message, MyExtensions.Logging.LogLevel level = MyExtensions.Logging.LogLevel.Normal)
      {
         logger.LogGeneral(message, level, "IRC");
      }

      private void SetupEvents()
      {
         client.ConnectFailed += (object sender, IrcErrorEventArgs ev) => 
         {
            Log(baseUsername + ": Couldn't connect to IRC: " + ev.Error.Message, MyExtensions.Logging.LogLevel.Error);
         };
         client.Registered += (sender, ev) => 
         {
            Log(baseUsername + ": Successfully registered on IRC (whatever that is).", MyExtensions.Logging.LogLevel.Debug);
         };
         client.ErrorMessageReceived += (object sender, IrcErrorMessageEventArgs ev) => 
         {
            Log(ev.Message, MyExtensions.Logging.LogLevel.Debug);
         };
         client.Error += (object sender, IrcErrorEventArgs ev) => 
         {
            Log(ev.Error.ToString(), MyExtensions.Logging.LogLevel.Error);
         };
         client.ProtocolError += (object sender, IrcProtocolErrorEventArgs ev) => 
         {
            Log("IRC Protocol Error #" + ev.Code + ": " + ev.Message, MyExtensions.Logging.LogLevel.Debug);

            if(ev.Code == 433 && !connectionFailed)
            {
               Log(baseUsername + ": Username already in use! Trying again...", MyExtensions.Logging.LogLevel.Debug);

               //Wait 1 second for disconnect.
               using (var connectedEvent = new ManualResetEventSlim(false))
               {
                  client.Disconnected += (sender2, e2) => connectedEvent.Set();
                  client.Disconnect();
                  if (!connectedEvent.Wait(1000))
                  {
                     DisposeClient();
                     connectionFailed = false;
                     Log("Couldn't disconnect in time!", MyExtensions.Logging.LogLevel.Error);
                     return;
                  }
               }

               //Dispose anyway
               DisposeClient();
               client = new StandardIrcClient();
               SetupEvents();
               TryConnect(Interlocked.Increment(ref nextRelayID));
            }
         };
         client.Connected += (object sender, EventArgs ev) => 
         {
            Log(client.LocalUser.UserName+ ": Connected to IRC successfully! Now trying to join " + ircChannel, MyExtensions.Logging.LogLevel.Debug);
            client.LocalUser.JoinedChannel += (object sender2, IrcChannelEventArgs eve) => 
            {
               foreach(IrcChannel channel in client.Channels)
               {
                  channel.MessageReceived += (object sender3, IrcMessageEventArgs even) => 
                  {
                     OnMessageReceive(even.Source.Name, even.Text);
                     Log("Got IRC message from " + even.Source.Name + ": " + even.Text, MyExtensions.Logging.LogLevel.Debug);
                  };
                  channel.UserJoined += (object sender3, IrcChannelUserEventArgs even) => OnJoin(even.ChannelUser.User.NickName);
                  channel.UserLeft += (object sender3, IrcChannelUserEventArgs even) => OnLeave(even.ChannelUser.User.NickName);
                  channel.UserKicked += (object sender3, IrcChannelUserEventArgs even) => OnLeave(even.ChannelUser.User.NickName);
               }
               Log(baseUsername + ": Successfully joined " + ircChannel + "! IRC relay is open!");
               fullConnect = true;
            };
            client.RawMessageReceived += (object sender2, IrcRawMessageEventArgs eve) => 
            {
               if(eve.Message.ToString().ToUpper().Trim().StartsWith("QUIT"))
               {
                  OnLeave(eve.Message.Source + "-irc");
               }
//               if(eve.Message.Prefix.ToUpper() == "QUIT")
//                  OnLeave(eve.
//               Log("RAW MESSAGE: " + eve.Message.Prefix + ", " + eve.Message.Source.Name);
//               Log("RAW CONTENT: " + eve.RawContent);
            };
            client.LocalUser.NoticeReceived += (object sender2, IrcMessageEventArgs eve) => 
            {
               Log(eve.Text, MyExtensions.Logging.LogLevel.Debug);
            };
            client.LocalUser.LeftChannel += (object sender2, IrcChannelEventArgs eve) => 
            {
               Log("Left channel: " + eve.Comment, MyExtensions.Logging.LogLevel.Debug);
            };
            client.LocalUser.MessageReceived += (object sender2, IrcMessageEventArgs eve) =>
            {
               //Log("Got IRC message: " + eve.Text);
               //Log("IRC message source: " + eve.Source.Name);
               //e.Source.
            };
            client.Channels.Join(ircChannel);
         };
         client.Disconnected += (object sender, EventArgs ev) => 
         {
            Log(baseUsername + ": Disconnected from IRC...", MyExtensions.Logging.LogLevel.Debug);
         };
      }

      //Not actually an async function, but it returns and all that junk
      public void ConnectAsync()
      {
         client = new StandardIrcClient();
//         SetupEvents();
//         nextRelayID = -1;
//         TryConnect(nextRelayID);
      }

      public void Disconnect()
      {
         if (Connected && !ConnectionFailed)
         {
            client.Disconnect();
         }

         DisposeClient();
      }

      public void Ping()
      {
         if(!Connected || ConnectionFailed)
            return;
         
         client.Ping();
      }

      public bool SendMessage(string message)
      {
         if(!Connected || ConnectionFailed)
            return false;

         client.LocalUser.SendMessage(ircChannel, WebUtility.HtmlDecode(message));

         return true;
      }

      public List<Tuple<string, bool>> Users()
      {
         if(!Connected || ConnectionFailed)
            return new List<Tuple<string,bool>>();
         
         return client.Channels[0].Users.Select(x => Tuple.Create(x.User.NickName + "-irc", !x.User.IsAway)).ToList();
      }
         
      //Try a connection with the given ID as the extra user identifier. Maybe it'll work this time
      private void TryConnect(int tryID)
      {
         //Come on now, don't do this!
         if (client.IsConnected)
         {
            Log("Tried to connect while still connected", MyExtensions.Logging.LogLevel.Warning);
            return;
         }
         else if (connectionFailed)
         {
            Log("Tried to connect after failing", MyExtensions.Logging.LogLevel.Warning);
            return;
         }

         //Default to no ID section
         string idSection = "";

         //Only set ID section if a positive number (or zero) was given.
         if (tryID >= 0)
            idSection = tryID.ToString();

         //Figure out how long the username base will be (the part people will recognize)
         int usernameBaseCharacters = maxUsernameCharacters - relayTag.Length - idSection.Length;

         //If the username isn't long enough to be recognized, we need to fail!
         if (usernameBaseCharacters < 3)
         {
            Log("Couldn't find a suitable nickname! Last ID: " + tryID, MyExtensions.Logging.LogLevel.Error);
            connectionFailed = true;
            return;
         }

         //Woo, what a sexy username
         string tryUsername = baseUsername.Truncate(usernameBaseCharacters) + relayTag + idSection;

         //Finally set up the registration info used to connect!
         IrcUserRegistrationInfo userInfo = new IrcUserRegistrationInfo()
         {
            NickName = tryUsername,
            UserName = tryUsername,
            RealName = "SmileBASIC Source Relay for: " + baseUsername
         };

         client.Connect(ircServer, false, (IrcRegistrationInfo)userInfo);
      }
   }
}