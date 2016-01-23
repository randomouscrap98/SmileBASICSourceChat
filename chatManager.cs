using System;
using System.Net;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using MyExtensions;
using ChatEssentials;
using ModuleSystem;

namespace ChatServer
{
   //This is what other functions should look like when subscribing to our event
   //public delegate void ChatEvent(SystemRequest request);

   //This is the manager for the chat. It includes information which all individual chat sessions
   //will probably need, and manages the saving/loading of resources.
   public class ChatManager
   {
      //Other things can dump their crap into this event, and when I call it in here,
      //it'll call all of them. Nifty.
      //public event ChatEvent Request;

      public const int FileWaitSeconds = 10;
      public const int HeaderSize = 64;
      public const string LogTag = "ChatManager";
      public const string BandwidthFile = "bandwidth.json";
      public const string UserFile = "users.json";
      public const string MessageFile = "messages.json";
      public const string HistoryFile = "history.json";
      public const string RoomFile = "rooms.json";

      public readonly int MaxModuleWaitSeconds = 5;
      public readonly int MaxMessageKeep = 1;
      public readonly int MaxMessageSend = 1;
      public readonly List<string> AllAcceptedTags = new List<string>();
      public readonly string GlobalTag = "";      
      public readonly BandwidthMonitor Bandwidth = new BandwidthMonitor();
      public readonly string SaveFolder = "";
      /*public readonly string IrcServer = "";
      public readonly string IrcChannel = "";
      public readonly string IrcTag = "";*/

      private List<Module> modules = new List<Module>();
      private List<UserMessageJSONObject> messages = new List<UserMessageJSONObject>();
      private Dictionary<string, List<UserMessageJSONObject>> history = new Dictionary<string, List<UserMessageJSONObject>>();

      private readonly ModuleLoader loader;
      private readonly AuthServer authServer;
      private readonly LanguageTags languageTags;
      private readonly HashSet<Chat> activeChatters = new HashSet<Chat>();
      private readonly Dictionary<int, User> users = new Dictionary<int, User>();
      private Dictionary<string, PMRoom> rooms = new Dictionary<string, PMRoom>();
      private readonly System.Timers.Timer saveTimer;
      private readonly System.Timers.Timer cleanupTimer;
      //private readonly System.Timers.Timer pingTimer;
      //private readonly SimpleIRCRelay relay;

      public readonly MyExtensions.Logging.Logger Logger = MyExtensions.Logging.Logger.DefaultLogger;

      //public readonly Object userLock = new Object();
      public readonly Object managerLock = new Object();
      public readonly Object fileLock = new Object();

      public List<UserMessageJSONObject> GetMessages()
      {
         lock (managerLock)
         {
            return messages.Select(x => new UserMessageJSONObject(x)).ToList();
         }
      }

      public Dictionary<string, List<UserMessageJSONObject>> GetHistory()
      {
         lock (managerLock)
         {
            Dictionary<string, List<UserMessageJSONObject>> historyCopy = new Dictionary<string, List<UserMessageJSONObject>>();

            foreach (string key in history.Keys)
            {
               historyCopy.Add(key, history[key].Select(x => new UserMessageJSONObject(x)).ToList());
            }

            return historyCopy;
         }
      }

      public static string Policy
      {
         get 
         {
            return @"
The SmileBASIC Source chat is mostly self-moderated. This means we expect you to conduct yourselves in a civil
manner and deal with conflicts on your own. However, should extreme behavior arise, the admins can take action
and enact punishment at their discretion. If a user is bothersome, there is an ""ignore"" option available.
If there are no moderators available and you wish to report a situation that isn't getting resolved, you can
send an email to smilebasicsource@gmail.com, and a moderator should show up shortly if they're available.
You use the chat at your own risk; neither SmileBASIC Source nor any member of its staff are responsible for
the actions of any user within the chat.".Replace("\n", " ");
         }
      }

      //Set up the logger when building the chat provider. Logging will go out to a file and the console
      //if possible. Otherwise, log to the default logger (which is like throwing them away)
      public ChatManager(ModuleLoader loader, AuthServer authServer, LanguageTags languageTags, 
         Options managerOptions, MyExtensions.Logging.Logger logger = null)
         /*int maxMessageKeep, int maxMessageSend, int saveInterval, int maxModuleWait, 
         string acceptedTags, string globalTag, string saveFolder,
         string ircServer, string ircChannel, string ircTag,
         MyExtensions.Logging.Logger logger = null)*/
      {
         /*GetOption<int>("messageBacklog"), GetOption<int>("messageSend"),
            GetOption<int>("saveInterval"), GetOption<int>("moduleWaitSeconds"),
            GetOption<string>("acceptedTags"), GetOption<string>("globalTag"),
            GetOption<string>("saveFolder"), GetOption<string>("ircServer"), 
            GetOption<string>("ircChannel"), GetOption<string>("ircTag"), logger);*/
         
         this.modules = loader.ActiveModules;
         this.authServer = authServer;
         this.loader = loader;
         this.languageTags = languageTags;

         MaxMessageKeep = managerOptions.GetAsType<int>("messageBacklog");
         MaxMessageSend = managerOptions.GetAsType<int>("messageSend");
         MaxModuleWaitSeconds = managerOptions.GetAsType<int>("moduleWaitSeconds");
         /*IrcServer = managerOptions.GetAsType<string>("ircServer");
         IrcChannel = managerOptions.GetAsType<string>("ircChannel");
         IrcTag = managerOptions.GetAsType<string>("ircTag");*/

         GlobalTag = managerOptions.GetAsType<string>("globalTag");
         AllAcceptedTags = managerOptions.GetAsType<string>("acceptedTags").Split(",".ToCharArray(), 
            StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
         AllAcceptedTags.Add(GlobalTag);

         SaveFolder = MyExtensions.StringExtensions.PathFixer(managerOptions.GetAsType<string>("saveFolder"));

         if (logger != null)
            Logger = logger;

         //Attempt to load the bandwidth monitor in the constructor. It's not a big deal if it doesn't load
         //though, so just fall back to the nice default empty monitor if we fail.
         BandwidthMonitor tempMonitor;

         if (MySerialize.LoadObject<BandwidthMonitor>(SavePath(BandwidthFile), out tempMonitor))
         {
            Bandwidth = tempMonitor;
            Log("Loaded bandwidth data from file", MyExtensions.Logging.LogLevel.Debug);
         }
         else
         {
            Log("Couldn't load bandwidth data! Defaulting to empty bandwidth", MyExtensions.Logging.LogLevel.Error);
         }

         //Attempt to load the users in the constructor. Again, not a big deal if it doesn't load.
         Dictionary<int, User> tempUsers;

         if (MySerialize.LoadObject<Dictionary<int, User>>(SavePath(UserFile), out tempUsers))
         {
            users = tempUsers;
            Log("Loaded user data from file", MyExtensions.Logging.LogLevel.Debug);
         }
         else
         {
            Log("Couldn't load user data! Defaulting to empty user dictionary", MyExtensions.Logging.LogLevel.Error);
         }

         //Attempt to load the messages in the constructor. Again, not a big deal if it doesn't load.
         List<UserMessageJSONObject> tempMessages;

         if (MySerialize.LoadObject<List<UserMessageJSONObject>>(SavePath(MessageFile), out tempMessages))
         {
            messages = tempMessages;
            UserMessageJSONObject.FindNextID(messages);
            PMRoom.FindNextID(messages.Select(x => x.tag).ToList());
            Log("Loaded messages from file", MyExtensions.Logging.LogLevel.Debug);
         }
         else
         {
            Log("Couldn't load messages! Defaulting to empty message list", MyExtensions.Logging.LogLevel.Error);
         }

         Dictionary<string, List<UserMessageJSONObject>> tempHistory;

         if (MySerialize.LoadObject<Dictionary<string, List<UserMessageJSONObject>>>(SavePath(HistoryFile), out tempHistory))
         {
            history = tempHistory;
            PMRoom.FindNextID(history.SelectMany(x => x.Value.Select(y => y.tag)).ToList());
            Log("Loaded history from file", MyExtensions.Logging.LogLevel.Debug);
         }
         else
         {
            Log("Couldn't load history! Defaulting to empty history", MyExtensions.Logging.LogLevel.Error);
         }

         Dictionary<string, PMRoom> tempRooms;

         if (MySerialize.LoadObject<Dictionary<string, PMRoom>>(SavePath(RoomFile), out tempRooms))
         {
            rooms = tempRooms;
            PMRoom.FindNextID(rooms.Select(x => x.Key).ToList());
            Log("Loaded rooms from file", MyExtensions.Logging.LogLevel.Debug);
         }
         else
         {
            Log("Couldn't load rooms! Defaulting to no rooms", MyExtensions.Logging.LogLevel.Error);
         }

         //Connect to the IRC as the relay bot
         /*relay = new SimpleIRCRelay(IrcServer, IrcChannel, "sbs_relay_bot", this.Logger);
         relay.IRCRelayMessageEvent += IrcRelayMessage;
         relay.IRCRelayJoinEvent += IrcRelayJoin;
         relay.IRCRelayLeaveEvent += IrcRelayLeave;
         relay.ConnectAsync();*/

         //now set up the save file timer
         saveTimer = new System.Timers.Timer(1000 * managerOptions.GetAsType<int>("saveInterval"));
         saveTimer.Elapsed += SaveTimerElapsed;
         saveTimer.Start();

         cleanupTimer = new System.Timers.Timer(1000 * managerOptions.GetAsType<int>("cleanInterval"));
         cleanupTimer.Elapsed += CleanupTimerElapsed;
         cleanupTimer.Start();

         /*pingTimer = new System.Timers.Timer(30000);
         pingTimer.Elapsed += PingTimer_Elapsed;
         pingTimer.Start();*/
      }

      /*void PingTimer_Elapsed (object sender, ElapsedEventArgs e)
      {
         relay.Ping();
      }*/

      //When the server shuts down, do this.
      public void Stop()
      {
         SaveData();
      }

      //Force the manager to lock itself up. Warning: this really does lock the server up!
      public void Lockup()
      {
         lock (managerLock)
         {
            //Now sit here for a while and think about what you've done!
            Thread.Sleep(100000);   //Just 100 seconds; not too bad.
         }
      }

      private void Log(string message, MyExtensions.Logging.LogLevel level = MyExtensions.Logging.LogLevel.Normal)
      {
         Logger.LogGeneral(message, level, LogTag);
      }

      void CleanupTimerElapsed (object sender, ElapsedEventArgs e)
      {
         List<Chat> leaveChatters = new List<Chat>();

         if(Monitor.TryEnter(managerLock, TimeSpan.FromSeconds(1)))
         {
            try
            {
               foreach(Chat chatter in new HashSet<Chat>(activeChatters))
               {
                  List<string> reasons = new List<string>();

                  if(chatter.State == WebSocketState.Closing)
                     reasons.Add("closing state");
                  if(chatter.State == WebSocketState.Closed)
                     reasons.Add("closed state");
//                  if(!chatter.Context.WebSocket.IsAlive)
//                     reasons.Add("not alive?");
                  if(chatter.TimeSincePing.TotalSeconds > 90)
                     reasons.Add("not pinging enough");
                  
                  if(reasons.Count > 0)
                  {
                     Log("Found a dangling chatter (" + chatter.ThisUser.Username + ")? Trying to remove now", 
                        MyExtensions.Logging.LogLevel.Warning);
                     Log("Dangle Reasons: " + string.Join(",", reasons), MyExtensions.Logging.LogLevel.Debug);
                     leaveChatters.Add(chatter);
                  }
               }
            }
            finally
            {
               Monitor.Exit(managerLock);
            }
         }
         else
         {
            Logger.LogGeneral("Couldn't run the cleanup... probably just saving module data", 
               MyExtensions.Logging.LogLevel.SuperDebug, LogTag);
         }

         foreach (Chat chatter in leaveChatters)
         {
            LeaveChat(chatter);
            chatter.Context.WebSocket.CloseAsync();
            Log("Successfully (?) removed dangling chatter", MyExtensions.Logging.LogLevel.Warning);
         }
      }

      //Start the save process
      private void SaveTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
      {
         SaveData();
         Log("Automatically saved chat data to files", MyExtensions.Logging.LogLevel.Debug);
      }

      //Forces file save for all pertinent manager data
      public void SaveData()
      {
         if (Monitor.TryEnter(fileLock, TimeSpan.FromSeconds(FileWaitSeconds)))
         {
            try
            {
               lock (Bandwidth.byteLock)
               {
                  Log("Enter bandwidth lock", MyExtensions.Logging.LogLevel.Locks);
                  if (MySerialize.SaveObject<BandwidthMonitor>(SavePath(BandwidthFile), Bandwidth))
                     Log("Saved bandwidth data to file", MyExtensions.Logging.LogLevel.Debug);
                  else
                     Log("Couldn't save bandwidth data to file!", MyExtensions.Logging.LogLevel.Error);
                  Log("Exit bandwidth lock", MyExtensions.Logging.LogLevel.Locks);
               }

               if(Monitor.TryEnter(managerLock, TimeSpan.FromSeconds(MaxModuleWaitSeconds)))
               {
                  try
                  {
                     Log("Enter manager save lock", MyExtensions.Logging.LogLevel.Locks);

                     //Save users
                     if (MySerialize.SaveObject<Dictionary<int, User>>(SavePath(UserFile), users))
                        Log("Saved user data to file", MyExtensions.Logging.LogLevel.Debug);
                     else
                        Log("Couldn't save user data to file!", MyExtensions.Logging.LogLevel.Error);

                     //Save messages
                     if (MySerialize.SaveObject<List<UserMessageJSONObject>>(SavePath(MessageFile), messages))
                        Log("Saved messages to file", MyExtensions.Logging.LogLevel.Debug);
                     else
                        Log("Couldn't save messages to file!", MyExtensions.Logging.LogLevel.Error);

                     //Save history
                     if(MySerialize.SaveObject<Dictionary<string, List<UserMessageJSONObject>>>(SavePath(HistoryFile), history))
                        Log("Saved history to file", MyExtensions.Logging.LogLevel.Debug);
                     else
                        Log("Couldn't save history to file!", MyExtensions.Logging.LogLevel.Error);

                     //Save rooms
                     if (MySerialize.SaveObject<Dictionary<string, PMRoom>>(SavePath(RoomFile), rooms))
                        Log("Saved rooms to file", MyExtensions.Logging.LogLevel.Debug);
                     else
                        Log("Couldn't save rooms to file!", MyExtensions.Logging.LogLevel.Error);

                     Log("Exit manager save lock", MyExtensions.Logging.LogLevel.Locks);
                  }
                  finally
                  {
                     Monitor.Exit(managerLock);
                  }
               }
               else
               {
                  Log("Couldn't save general manager data because the manager is locked up! (wait: " + MaxModuleWaitSeconds + ")", MyExtensions.Logging.LogLevel.Error);
               }

               //Save all module data
               foreach (Module module in modules)
               {
                  if(Monitor.TryEnter(module.Lock, TimeSpan.FromSeconds(FileWaitSeconds)))
                  {
                     try
                     {
                        if(loader.SaveWrapper(module))
                           Logger.LogGeneral("Saved " + module.ModuleName + " data", MyExtensions.Logging.LogLevel.Debug, LogTag);
                        else
                           Logger.Error("Couldn't save " + module.ModuleName + " data!", LogTag);
                     }
                     finally
                     {
                        Monitor.Exit(module.Lock);
                     }
                  }
                  else
                  {
                     Logger.Warning("Couldn't save " + module.ModuleName + " data; it appears to be busy", LogTag);
                  }
               }
            }
            finally
            {
               Monitor.Exit(fileLock);
            }
         }
         else
         {
            Logger.Warning("Couldn't save files... another process may still be writing");
         }
      }

      public string SavePath(string filename)
      {
         return SaveFolder + filename;
      }

      /*public int RegisterIrcUser(string username)
      {
         //Do NOT register relay users!
         if (IrcSkipUsers().Contains(username))
            return -1;
         
         lock (userLock)
         {
            int lookupID = UserLookup(username);

            if (lookupID < 0)
            {
               int ID = users.Max(x => x.Key);

               if (ID < 1000000000)
                  ID = 1000000000;
               
               User newUser = new User(++ID);
               newUser.SetIRC(username, true);
               users.Add(newUser.UID, newUser);

               Log("Registered new IRC user #" + newUser.UID + ": " + newUser.Username);

               return newUser.UID;
            }
            else if (lookupID < 1000000000)
            {
               Log("Tried to register a real user as an IRC user!", MyExtensions.Logging.LogLevel.Error);
            }
            else
               return lookupID;
         }

         return -1;
      }*/

      public User GetUser(int uid)
      {
         User user = new User(0);
         lock (managerLock)
         {
            Log("Enter getuser lock", MyExtensions.Logging.LogLevel.Locks);
            if (users.ContainsKey(uid))
               user = users[uid];
            Log("Exit getuser lock", MyExtensions.Logging.LogLevel.Locks);
         }

         return user;
      }

      public bool UserExists(int uid)
      {
         bool userExists = false;
         lock (managerLock)
         {
            Log("Enter userexists lock", MyExtensions.Logging.LogLevel.Locks);
            userExists = users.ContainsKey(uid);
            Log("Exit userexists lock", MyExtensions.Logging.LogLevel.Locks);
         }

         return userExists;
      }

      public int UserLookup(string username)
      {
         int userID = -1;

         lock (managerLock)
         {
            Log("Enter userlookup lock", MyExtensions.Logging.LogLevel.Locks);

            try
            {
               userID = users.First(x => x.Value.Username == username).Key;
            }
            catch
            {
               userID = -1;
            }

            Log("Exit userlookup lock", MyExtensions.Logging.LogLevel.Locks);
         }

         return userID;
      }

      public List<string> AllAcceptedTagsForUser(int user)
      {
         List<string> allTags = new List<string>();
         lock (managerLock)
         {
            Log("Enter allacceptedtagsforuser lock", MyExtensions.Logging.LogLevel.Locks);
            allTags = AllAcceptedTags.Union(rooms.Where(x => x.Value.Users.Contains(user)).Select(x => x.Key)).ToList();
            Log("Exit allacceptedtagsforuser lock", MyExtensions.Logging.LogLevel.Locks);
         }
         return allTags;
      }

      public bool ValidTagForUser(int user, string tag)
      {
         return AllAcceptedTagsForUser(user).Contains(tag);
      }

      /*public List<Module> VisibleModulesForUser(int user)
      {
         lock (userLock)
         {
            List<Module> returnModules = new List<Module>();

            if (users.ContainsKey(user))
            {
               UserInfo realUser = new UserInfo(users[user]);
               returnModules = modules.Where(x => !x.Hidden(realUser)).ToList();
            }

            return returnModules;
         }
      }*/

      public bool LeavePMRoom(int user, string room, out string error)
      {
         error = "";
         bool result = false;

         lock (managerLock)
         {
            Log("Enter leavepmroom lock", MyExtensions.Logging.LogLevel.Locks);

            if (!users.ContainsKey(user))
            {
               error = "You don't seem to exist... I'm not sure how to leave the room";
            }
            else if (AllAcceptedTags.Contains(room))//!rooms.ContainsKey(room))
            {
               error = "You can't leave this room!";
            }
            else if (!rooms.ContainsKey(room))
            {
               error = "This room doesn't exist?";
            }
            else if (!rooms[room].Users.Contains(user))
            {
               error = "You're not in that room.";
            }
            else
            {
               rooms[room].Users.Remove(user);

               if (rooms[room].Users.Count < 2)
                  rooms.Remove(room);

               result = true;
            }

            Log("Exit leavepmroom lock", MyExtensions.Logging.LogLevel.Locks);
         }

         if (result)
            BroadcastUserList();

         return result;
      }

      public bool CreatePMRoom(HashSet<int> newUsers, int creator, out string error)
      {
         error = "";
         bool result = false;

         lock(managerLock)
         {
            Log("Enter createpmroom lock", MyExtensions.Logging.LogLevel.Locks);

            if (newUsers.Count < 2)
            {
               error = "There's not enough people to make the room";
            }
            else if (rooms.Any(x => x.Value.Users.SetEquals(newUsers)))
            {
               error = "There's already a room with this exact set of people!";
            }
            else if (!newUsers.All(x => users.ContainsKey(x)))
            {
               error = "One or more of the given users doesn't exist!";
            }
            /*else if (newUsers.Any(x => users[x].IrcUser))
            {
               error = "You can't include IRC users in a PM room!";
            }*/
            else if (!users.ContainsKey(creator))
            {
               error = "You don't seem to exist... I'm not sure how to create the room";
            }
            else if (users[creator].Banned || users[creator].Blocked)
            {
               error = "You are banned or blocked and cannot create a room";
            }
            else
            {
               PMRoom newRoom = new PMRoom(newUsers, creator, TimeSpan.FromDays(1));
               rooms.Add(newRoom.Name, newRoom);
               result = true;
            }

            Log("Exit createpmroom lock", MyExtensions.Logging.LogLevel.Locks);
         }

         if (result)
            BroadcastUserList();

         return result;
      }

      //ONLY the registration of a new user. It has nothing to do with authentication
      private bool TryRegister(int uid, string key)
      {
         bool good = false;
         lock (managerLock)
         {
            Log("Enter register lock", MyExtensions.Logging.LogLevel.Locks);

            if (users.ContainsKey(uid))
            {
               good = true;
            }
            else if (authServer.CheckAuth(uid, key))
            {
               users.Add(uid, new User(uid));
               good = true;
            }

            Log("Exit register lock", MyExtensions.Logging.LogLevel.Locks);
         }

         return good;
      }
         
      /// <summary>
      /// Attempt to authenticate the given user with the given key. LOCKS MANAGER!
      /// </summary>
      /// <param name="chatSession">Chat session.</param>
      /// <param name="uid">Uid.</param>
      /// <param name="key">Key.</param>
      public bool Authenticate(Chat chatSession, int uid, string key, out List<Chat> duplicates, out string error)
      {
         duplicates = new List<Chat>();
         error = "";

         if (authServer.CheckAuth(uid, key))
         {
            if (TryRegister(uid, key))
            {
               if (!GetUser(uid).PullInfoFromQueryPage())
               {
                  error = "Couldn't authenticate because your user information couldn't be found";
                  Logger.Warning("Authentication failed: Couldn't get user information from website");
                  return false;
               }
               lock (managerLock)
               {
                  Log("Enter authenticate lock", MyExtensions.Logging.LogLevel.Locks);
                  duplicates = activeChatters.Where(x => x.UID == uid).ToList();
                  foreach (Chat chatter in activeChatters.Where(x => x.UID == uid))
                     chatter.Context.WebSocket.CloseAsync();
                  activeChatters.RemoveWhere(x => x.UID == uid);
                  activeChatters.Add(chatSession);
                  Log("Exit authenticate lock", MyExtensions.Logging.LogLevel.Locks);
               }
               return true;
            }
         }
         else
         {
            Logger.Log("User " + uid + " tried to bind with bad auth code: " + key);
            error = "Key was invalid";
         }

         return false;
      }

      /// <summary>
      /// When a (hopefully) authenticated user leaves chat, this crap happens. LOCKS MANAGER!
      /// </summary>
      /// <param name="chatSession">Current Chat Session</param>
      public void LeaveChat(Chat chatSession)
      {
         bool didLeave = false;

         if (Monitor.TryEnter(managerLock, TimeSpan.FromSeconds(MaxModuleWaitSeconds)))
         {
            try
            {
               Log("Enter leavechat lock", MyExtensions.Logging.LogLevel.Locks);
               didLeave = activeChatters.Remove(chatSession);
               authServer.UpdateUserList(activeChatters.Select(x => x.UID).ToList());

               Log("Exit leavechat lock", MyExtensions.Logging.LogLevel.Locks);
            }
            finally
            {
               Monitor.Exit(managerLock);
            }
         }
         else
         {
            Log("Session " + chatSession.UID + " could not leave properly! Data may be inconsistent!", MyExtensions.Logging.LogLevel.Error);
         }

         BroadcastUserList();

         //Only perform special leaving messages and processing if the user was real
         if (users.ContainsKey(chatSession.UID) && didLeave)
         {
            if (!users[chatSession.UID].PerformOnChatLeave())
               Logger.Warning("User session timer was in an invalid state!");

            if (users[chatSession.UID].ShowMessages)
               Broadcast(new LanguageTagParameters(ChatTags.Leave, users[chatSession.UID]), new SystemMessageJSONObject());
         }

         //SimpleLeave(chatSession.UID);
//         BroadcastUserList();
//
//         //Only perform special leaving messages and processing if the user was real
//         if (users.ContainsKey(chatSession.UID))
//         {
//            if (!users[chatSession.UID].PerformOnChatLeave())
//               Logger.Warning("User session timer was in an invalid state!");
//
//            if (users[chatSession.UID].ShowMessages)
//               Broadcast(new LanguageTagParameters(ChatTags.Leave, users[chatSession.UID]), new SystemMessageJSONObject());
//         }
      }

      public void SimpleEnter(int uid)
      {
         BroadcastUserList();

         //Only perform special leaving messages and processing if the user was real
         if (users.ContainsKey(uid))
         {
            if (!users[uid].PerformOnChatEnter())
               Logger.Warning("User session timer was in an invalid state!");

            if (users[uid].ShowMessages)
               Broadcast(new LanguageTagParameters(ChatTags.Join, users[uid]), new SystemMessageJSONObject());
         }
      }

//      public void SimpleLeave(int uid)
//      {
//         BroadcastUserList();
//
//         //Only perform special leaving messages and processing if the user was real
//         if (users.ContainsKey(uid))
//         {
//            if (!users[uid].PerformOnChatLeave())
//               Logger.Warning("User session timer was in an invalid state!");
//
//            if (users[uid].ShowMessages)
//               Broadcast(new LanguageTagParameters(ChatTags.Leave, users[uid]), new SystemMessageJSONObject());
//         }
//      }

      /*public void IrcRelayJoin(string username)
      {
         Log("IRC Join: " + username);
         SimpleEnter(UserLookup(username));
      }

      public void IrcRelayLeave(string username)
      {
         Log("IRC Leave: " + username);
         SimpleLeave(UserLookup(username));
      }

      public void IrcRelayMessage(string sender, string message)
      {
         //Skip this buttneck
         if (IrcSkipUsers().Contains(sender))
            return;
         
         User ircUser = GetUser(UserLookup(sender));

         if (!ircUser.IrcUser || ircUser.UID <= 0)
            return;

         UserMessageJSONObject ircMessage = new UserMessageJSONObject(ircUser, System.Security.SecurityElement.Escape(message), IrcTag);

         AddMessage(ircMessage);
         BroadcastMessageList();
      }*/

      public ChatTags AddMessage(UserMessageJSONObject message)
      {
         ChatTags warning = ChatTags.None;

         User user = GetUser(message.uid);

         //Update spam score
         if(message.Spammable)
         {
            lock(managerLock)
            {
               Log("Enter messagespam lock", MyExtensions.Logging.LogLevel.Locks);
               warning = user.MessageSpam(messages, message.message);
               Log("Exit messagespam lock", MyExtensions.Logging.LogLevel.Locks);
            }
         }

         //Only add message to message list if we previously set that we should.
         if(user.BlockedUntil < DateTime.Now)
         {
            lock(managerLock)
            {
               //First add to history
               if (!history.ContainsKey(message.tag))
                  history.Add(message.tag, new List<UserMessageJSONObject>());
               if(message.Display)
                  history[message.tag].Add(message);

               //Then, get rid of history we don't need anymore
               foreach(string key in history.Keys.ToList())
                  history[key] = history[key].Where(x => (DateTime.Now - x.PostTime()).TotalDays <= 1).OrderByDescending(x => x.PostTime()).Take(MaxMessageSend).ToList();
               history = history.Where(x => AllAcceptedTags.Contains(x.Key) || rooms.ContainsKey(x.Key)).ToDictionary(x => x.Key, y => y.Value);

               Log("Enter message add lock", MyExtensions.Logging.LogLevel.Locks);
               messages.Add(message);
               messages = messages.Skip(Math.Max(0, messages.Count() - MaxMessageKeep)).ToList();

               if (rooms.ContainsKey(message.tag))
                  rooms[message.tag].OnMessage();
               
               Log("Exit message add lock", MyExtensions.Logging.LogLevel.Locks);
            }
            user.PerformOnPost();
         }

         return warning;
      }

      public void SendMessage(ModuleJSONObject message)
      {
         List<Chat> recipients = new List<Chat>();
         lock (managerLock)
         {
            Log("Enter sendmessage lock", MyExtensions.Logging.LogLevel.Locks);
            foreach (int user in message.recipients.Distinct())
            {
               if (activeChatters.Any(x => x.UID == user))
                  recipients.Add(activeChatters.First(x => x.UID == user));
               else
                  Logger.LogGeneral("Recipient " + user + " in module message was not found", MyExtensions.Logging.LogLevel.Warning);
            }
            Log("Leave sendmessage lock", MyExtensions.Logging.LogLevel.Locks);
         }

         foreach(Chat recipient in recipients)
            recipient.MySend(message.ToString());
      }
         
      /*public List<string> IrcSkipUsers()
      {
         lock (managerLock)
         {
            List<string> skips = activeChatters.Select(x => x.IrcUsername).ToList();
            skips.Add(relay.Username);
            return skips;
         }
      }*/

      /*public List<int> ActiveIRCUsers()
      {
         List<int> activeIrc = new List<int>();

         lock (managerLock)
         {
            foreach (string username in relay.Users().Select(x => x.Item1).Except(IrcSkipUsers()))
               activeIrc.Add(RegisterIrcUser(username));
         }

         return activeIrc.Where(x => x >= 1000000000).ToList();
      }*/

      /// <summary>
      /// Get only logged in users. LOCKS MANAGER!
      /// </summary>
      /// <returns>The logged in users</returns>
      public Dictionary<int, User> LoggedInUsers()
      {
         List<int> activeUIDs = new List<int>();

         lock (managerLock)
         {
            Log("Enter loggedinactive lock", MyExtensions.Logging.LogLevel.Locks);
            activeUIDs = activeChatters.Select(x => x.UID)/*.Union(ActiveIRCUsers())*/.ToList();
            Log("exit loggedinactive lock", MyExtensions.Logging.LogLevel.Locks);
         }

         Dictionary<int, User> returns;

         lock (managerLock)
         {
            Log("Enter loggedinuserget lock", MyExtensions.Logging.LogLevel.Locks);
            returns = users.Where(x => activeUIDs.Contains(x.Key)).ToDictionary(k => k.Key, v => v.Value);
            Log("exit loggedinuserget lock", MyExtensions.Logging.LogLevel.Locks);
         }

         return returns;
      }

//      public Dictionary<int, User> IrcUsers()
//      {
//         Dictionary<int, User> ircUsers = new Dictionary<int, User>();
//         int nextID = 1000000000;
//
//         foreach (Tuple<string, bool> userInfo in relay.Users())
//         {
//            User newUser = new User(nextID++);
//            newUser.SetIRC(userInfo.Item1, userInfo.Item2);
//            ircUsers.Add(newUser.UID, newUser);
//         }
//
//         return ircUsers;
//      }

      public Dictionary<int, UserInfo> UsersForModules()
      {
         List<int> loggedInUsers = LoggedInUsers().Keys.ToList();
         Dictionary<int, UserInfo> returns;

         lock (managerLock)
         {
            Log("Enter usersformodules lock", MyExtensions.Logging.LogLevel.Locks);
            returns = users.Select(x => new UserInfo(x.Value, loggedInUsers.Contains(x.Key))).ToDictionary(k => k.UID, v => v);
            Log("Exit usersformodules lock", MyExtensions.Logging.LogLevel.Locks);
         }

         return returns;
      }

      //When you get some kind of request, it goes through here. Then we call all the events that subscribed to us
//      public void OnRequest(SystemRequest request)
//      {
//         if (Request != null)
//         {
//            //This calls all our subscribed events. They'll probably know what to do with this request.
//            Request(request);
//         }
//      }

      //Get a JSON string representing a list of users currently in chat. Do NOT call this while locking on userLock
      public string ChatUserList(int caller)
      {
         UserListJSONObject userList = new UserListJSONObject();

         lock (managerLock)
         {
            //First, let's get rid of old rooms
            rooms = rooms.Where(x => !x.Value.HasExpired).ToDictionary(x => x.Key, y => y.Value);

            //Now we can finally do the user thing
            userList.users = LoggedInUsers().OrderByDescending(x => x.Value.CurrentSessionTime).Select(x => new UserJSONObject(x.Value)).ToList();
            userList.rooms = rooms.Where(x => x.Value.Users.Contains(caller)).Select(x => new RoomJSONObject(x.Value, users)).ToList();
         }
         return userList.ToString();
      }

      //Get a JSON string representing a list of the last 10 messages
      public string ChatMessageList(int user)
      {
         MessageListJSONObject jsonMessages = new MessageListJSONObject();
//         List<UserMessageJSONObject> visibleMessages; 
//
//         lock (managerLock)
//         {
//            Log("Enter chatmessagelist lock", MyExtensions.Logging.LogLevel.Locks);
//            //Messages are all readonly, so it's OK to have just references
//            visibleMessages = messages.Where(x => x.Display && (DateTime.Now - x.PostTime()).TotalDays < 1.0).ToList();
//            Log("Exit chatmessagelist lock", MyExtensions.Logging.LogLevel.Locks);
//         }

         jsonMessages.messages = history.Where(x => AllAcceptedTagsForUser(user).Contains(x.Key)).SelectMany(x => x.Value).OrderBy(x => x.id).ToList();
//
//         foreach (string tag in AllAcceptedTagsForUser(user))
//         {
//            List<UserMessageJSONObject> tagMessages = visibleMessages.Where(x => x.tag == tag).ToList();
//            for (int i = 0; i < Math.Min(MaxMessageSend, tagMessages.Count); i++)
//               jsonMessages.messages.Add(tagMessages[tagMessages.Count - 1 - i]);
//         }

         //Oops, remember we added them in reverse order. Fix that
         //jsonMessages.messages = jsonMessages.messages.OrderBy(x => x.id).ToList();

         return jsonMessages.ToString();
      }

      public void Broadcast(string message, List<Chat> exclude = null)
      {
         if (string.IsNullOrEmpty(message))
            return;

         if (exclude == null)
            exclude = new List<Chat>();

         List<Chat> receivers = new List<Chat>();
         lock (managerLock)
         {
            Log("Enter broadcast lock", MyExtensions.Logging.LogLevel.Locks);
            receivers = activeChatters.Except(exclude).ToList();
            Log("Exit broadcast lock", MyExtensions.Logging.LogLevel.Locks);
         }

         Log("Just before broadast", MyExtensions.Logging.LogLevel.SuperDebug);
         foreach (Chat chatter in receivers)
            chatter.MySend(message);
         Log("Just after broadcast", MyExtensions.Logging.LogLevel.SuperDebug);
      }

      public void SelectiveBroadcast(string message, string tag, List<Chat> exclude = null)
      {
         if (string.IsNullOrEmpty(message))
            return;

         if (exclude == null)
            exclude = new List<Chat>();

         List<Chat> receivers = new List<Chat>();
         lock (managerLock)
         {
            Log("Enter selective broadcast lock", MyExtensions.Logging.LogLevel.Locks);
            receivers = activeChatters.Except(exclude).ToList();
            receivers = receivers.Where(x => AllAcceptedTagsForUser(x.UID).Contains(tag)).ToList();
            Log("Exit selective broadcast lock", MyExtensions.Logging.LogLevel.Locks);
         }

         Log("Just before broadast", MyExtensions.Logging.LogLevel.SuperDebug);
         foreach (Chat chatter in receivers)
            chatter.MySend(message);
         Log("Just after broadcast", MyExtensions.Logging.LogLevel.SuperDebug);
      }

      public void Broadcast(LanguageTagParameters parameters, JSONObject container, List<Chat> exclude = null)
      {
         if (exclude == null)
            exclude = new List<Chat>();

         List<Chat> receivers = new List<Chat>();
         lock (managerLock)
         {
            Log("Enter broadcast tag lock", MyExtensions.Logging.LogLevel.Locks);
            receivers = activeChatters.Except(exclude).ToList();
            Log("Exit broadcast tag lock", MyExtensions.Logging.LogLevel.Locks);
         }

         Log("Just before tag broadast", MyExtensions.Logging.LogLevel.SuperDebug);
         foreach (Chat chatter in receivers)
         {
            parameters.UpdateUser(chatter.ThisUser);  //Update the message to reflect user preferences
            chatter.MySend(parameters, container);    //Send a tag message by filling the container with the tag parameters
         }
         Log("Just after tag broadcast", MyExtensions.Logging.LogLevel.SuperDebug);
      }

      public string ConvertTag(LanguageTagParameters parameters)
      {
         return languageTags.GetTag(parameters);
      }

      public void BroadcastUserList()
      {
         List<Chat> receivers = new List<Chat>();
         lock (managerLock)
         {
            Log("Enter broadcast userlist lock", MyExtensions.Logging.LogLevel.Locks);
            receivers = activeChatters.ToList();
            Log("Exit broadcast userlist lock", MyExtensions.Logging.LogLevel.Locks);
         }
            
         Log("Just before userlist broadast", MyExtensions.Logging.LogLevel.SuperDebug);
         foreach (Chat chatter in receivers)
            chatter.MySend(ChatUserList(chatter.UID));
         Log("Just after userlist broadcast", MyExtensions.Logging.LogLevel.SuperDebug);
      }

      public void BroadcastMessageList()
      {
         List<Chat> receivers = new List<Chat>();
         lock (managerLock)
         {
            Log("Enter broadcast messagelist lock", MyExtensions.Logging.LogLevel.Locks);
            receivers = activeChatters.ToList();
            Log("Exit broadcast messagelist lock", MyExtensions.Logging.LogLevel.Locks);
         }
            
         Log("Just before messagelist broadast", MyExtensions.Logging.LogLevel.SuperDebug);
         foreach (Chat chatter in receivers)
            chatter.MySend(ChatMessageList(chatter.UID));
         Log("Just after messagelist broadast", MyExtensions.Logging.LogLevel.SuperDebug);
      }

      public bool CheckKey(int uid, string key)
      {
         return authServer.CheckAuth(uid, key);
      }

      //Get a copy of the list of modules. If a user is given, only get the modules that this user can see.
      public List<Module> GetModuleListCopy(int user = 0)
      {
         //If a user was given, only return modules that the user can see or use
         if (user > 0)
         {
            UserInfo realUser = new UserInfo(GetUser(user), LoggedInUsers().ContainsKey(user));
            List<Module> returnModules = modules.Where(x => !x.Hidden(realUser)).ToList();

            return returnModules;
         }

         return new List<Module>(modules);
      }
   }
}
