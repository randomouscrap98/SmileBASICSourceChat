using System;
using System.Net;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using MyExtensions;
using ChatEssentials;
using ModuleSystem;
using MyWebSocket;
using MyExtensions.Logging;
using System.Diagnostics;

namespace ChatServer
{
   //This is what other functions should look like when subscribing to our event
   //public delegate void ChatEvent(SystemRequest request);
   public class ChatServerSettings : WebSocketSettings
   {
      public TimeSpan MaxModuleWait = TimeSpan.FromSeconds(5);
      public TimeSpan MaxFileWait = TimeSpan.FromSeconds(10);
      public TimeSpan SaveInterval = TimeSpan.FromSeconds(30);
      //public int MaxMessageKeep = 100;
      public int MaxMessageSend = 30;
      public int MaxUserQueryFailures = 3;
      public string GlobalTag = "";
      public bool MonitorThreads = false;

      private List<string> acceptedTags = new List<string>();
      private string saveFolder = "";

      /// <summary>
      /// A list of all accepted tags for chat messages. Will always include global tag, even
      /// if you don't add it.
      /// </summary>
      /// <value>The accepted tags.</value>
      public List<string> AcceptedTags
      {
         get
         {
            List<string> acceptedTagsCopy = new List<string>(acceptedTags);

            if (!acceptedTags.Contains(GlobalTag))
               acceptedTagsCopy.Add(GlobalTag);

            return acceptedTagsCopy;
         }
         set
         {
            acceptedTags = value;
         }
      }

      /// <summary>
      /// The folder where the chat server will save all its data. 
      /// </summary>
      /// <value>The save folder.</value>
      public string SaveFolder
      {
         get { return StringExtensions.PathFixer(saveFolder); }
         set { saveFolder = value; }
      }

      public ChatServerSettings(int port, string service, Func<WebSocketUser> generator, Logger logger = null) 
         : base(port, service, generator, logger)
      {
         
      }

      public void DumpSettings()
      {
         Dictionary<string, string> settingValues = new Dictionary<string, string>();

         settingValues.Add("fileWait", this.MaxFileWait.ToString());
         settingValues.Add("moduleWait", this.MaxModuleWait.ToString());
         settingValues.Add("maxReceiveSize", this.MaxReceiveSize.ToString());
         settingValues.Add("pingInterval", this.PingInterval.ToString());
         settingValues.Add("readWriteTimeout", this.ReadWriteTimeout.ToString());
         settingValues.Add("receiveBuffer", this.ReceiveBufferSize.ToString());
         settingValues.Add("sendBuffer", this.SendBufferSize.ToString());
         settingValues.Add("shutdownTimeout", this.ShutdownTimeout.ToString());
         settingValues.Add("monitorThreads", this.MonitorThreads.ToString());
         settingValues.Add("maxUserQueryFailures", this.MaxUserQueryFailures.ToString());

         LogProvider.LogGeneral("Settings dump: " + 
            string.Join(" ", settingValues.Select(x => "[" + x.Key + "] " + x.Value)), LogLevel.Debug, "ChatSettings");
      }
   }

   //This is the manager for the chat. It includes information which all individual chat sessions
   //will probably need, and manages the saving/loading of resources.
   public class ChatServer : WebSocketServerAsync
   {
      public const int HeaderSize = 64;
      public const string LogTag = "ChatServer";
      public const string BandwidthFile = "bandwidth.json";
      public const string UserFile = "users.json";
      public const string MessageFile = "messages.json";
      public const string HistoryFile = "history.json";
      public const string RoomFile = "rooms.json";
           
      public readonly BandwidthMonitor Bandwidth = new BandwidthMonitor();
      /*public readonly string IrcServer = "";
      public readonly string IrcChannel = "";
      public readonly string IrcTag = "";*/

      private List<Module> modules = new List<Module>();
      //private List<MessageBaseJSONObject> messages = new List<MessageBaseJSONObject>();
      private Dictionary<string, List<MessageBaseJSONObject>> history = new Dictionary<string, List<MessageBaseJSONObject>>();

      private readonly ModuleLoader loader;
      private readonly AuthServer authServer;
      private readonly LanguageTags languageTags;
      private readonly Dictionary<int, User> users = new Dictionary<int, User>();
      private Dictionary<string, PMRoom> rooms = new Dictionary<string, PMRoom>();
      private readonly System.Timers.Timer saveTimer;
      private readonly System.Timers.Timer threadMonitorTimer;
      private int lastThreadCount = 0;

      public readonly Object managerLock = new Object();
      public readonly Object fileLock = new Object();

//      public List<MessageBaseJSONObject> GetMessages()
//      {
//         lock (managerLock)
//         {
//            return messages.Select(x => new MessageBaseJSONObject(x)).ToList();
//         }
//      }

      /// <summary>
      /// Return a COPY of the message history. Note that this COMPLETELY REMOVES any higher 
      /// types; all messages get converted to the base type MessageBaseJSONObject
      /// </summary>
      /// <returns>The history.</returns>
      public Dictionary<string, List<MessageBaseJSONObject>> GetHistory()
      {
         lock (managerLock)
         {
            Dictionary<string, List<MessageBaseJSONObject>> historyCopy = new Dictionary<string, List<MessageBaseJSONObject>>();

            foreach (string key in history.Keys)
            {
               historyCopy.Add(key, history[key].Select(x => new MessageBaseJSONObject(x)).ToList());
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

      public ChatServerSettings ChatSettings
      {
         get { return (ChatServerSettings)Settings; }
      }

      public ChatServer(ChatServerSettings settings, ModuleLoader loader, AuthServer authServer,
         LanguageTags languageTags) : base(settings)
      {
         this.modules = loader.ActiveModules;
         this.authServer = authServer;
         this.loader = loader;
         this.languageTags = languageTags;

         foreach (Module module in modules)
            module.OnExtraCommandOutput += DefaultOutputMessages;

         #region BandwidthLoad
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
         #endregion

         #region UserLoad
         //Attempt to load the users in the constructor. Again, not a big deal if it doesn't load.
         Dictionary<int, User> tempUsers;

         if (MySerialize.LoadObject<Dictionary<int, User>>(SavePath(UserFile), out tempUsers))
         {
            users = tempUsers.Where(x => !String.IsNullOrWhiteSpace(x.Value.Username)).ToDictionary(x => x.Key, y => y.Value);

            if(users.Count > 0)
               UserSession.SetNextID(users.Max(x => x.Value.MaxSessionID) + 1);
            
            Log("Loaded user data from file", MyExtensions.Logging.LogLevel.Debug);
         }
         else
         {
            Log("Couldn't load user data! Defaulting to empty user dictionary", MyExtensions.Logging.LogLevel.Error);
         }
         #endregion

//         #region MessageLoad
//         //Attempt to load the messages in the constructor. Again, not a big deal if it doesn't load.
//         List<MessageBaseJSONObject> tempMessages;
//
//         //Note that loading/saving the history in this way loses all information about what kind of message
//         //it is. If this becomes a problem in the future, you can change the serializer to store types.
//         if (MySerialize.LoadObject<List<MessageBaseJSONObject>>(SavePath(MessageFile), out tempMessages))
//         {
//            messages = tempMessages;
//            //MessageBaseJSONObject.FindNextID(messages);
//            PMRoom.FindNextID(messages.Select(x => x.tag).ToList());
//            Log("Loaded messages from file", MyExtensions.Logging.LogLevel.Debug);
//         }
//         else
//         {
//            Log("Couldn't load messages! Defaulting to empty message list", MyExtensions.Logging.LogLevel.Error);
//         }
//         #endregion

         #region HistoryLoad
         Dictionary<string, List<MessageJSONObject>> tempHistory;

         /*try
         {
            tempHistory = MySerialize2.LoadObject<Dictionary<string, List<MessageBaseJSONObject>>>(
                  SavePath(HistoryFile));
            PMRoom.FindNextID(tempHistory.SelectMany(x => x.Value.Select(y => y.tag)).ToList());
            history = tempHistory;
            Log("Loaded history from file", MyExtensions.Logging.LogLevel.Debug);
         }
         catch(Exception ex)
         {
            Log("Couldn't load history! Defaulting to empty history. Reason: " + ex.ToString(), 
                  MyExtensions.Logging.LogLevel.Error);
         }*/

         if (MySerialize.LoadObject<Dictionary<string, List<MessageJSONObject>>>
               (SavePath(HistoryFile), out tempHistory) && tempHistory != null)
         {
            history = tempHistory.ToDictionary(x => x.Key, y =>
                  y.Value.Select(z => (MessageBaseJSONObject)z).ToList());
            PMRoom.FindNextID(history.SelectMany(x => x.Value.Select(y => y.tag)).ToList());
            Log("Loaded history from file", MyExtensions.Logging.LogLevel.Debug);
         }
         else
         {
            Log("Couldn't load history! Defaulting to empty history", MyExtensions.Logging.LogLevel.Error);
         }
         #endregion

         #region RoomLoad
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
         #endregion

         //now set up the save file timer
         saveTimer = new System.Timers.Timer(settings.SaveInterval.TotalMilliseconds);//1000 * managerOptions.GetAsType<int>("saveInterval"));
         saveTimer.Elapsed += SaveTimerElapsed;
         saveTimer.Start();

         if (settings.MonitorThreads)
         {
            threadMonitorTimer = new System.Timers.Timer(1000);
            threadMonitorTimer.Elapsed += MonitorThreadsElapsed;
            threadMonitorTimer.Start();
         }
      }
         
      private void DefaultOutputMessages(List<MessageBaseJSONObject> outputs, int receiverUID)
      {
         //ApplyTagIfDefault(outputs, manager.ChatSettings.GlobalTag);

         foreach (var message in outputs)
            HandleMessage(message, receiverUID);
         //OutputMessages(outputs, receiverUID);
      }

      public void SafeStop()
      {
         Stop();
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

      //Start the save process
      private void SaveTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
      {
         SaveData();
         Log("Automatically saved chat data to files", MyExtensions.Logging.LogLevel.Debug);
      }

      //Check on the thread count to see when it increases.
      private void MonitorThreadsElapsed(object source, System.Timers.ElapsedEventArgs e)
      {
         int thisThreadCount;
         using(Process process = Process.GetCurrentProcess())
         {
            thisThreadCount = process.Threads.Count;
         }

         //If we've never run before, chill. Set the value so we'll have it for NEXT time.
         if (lastThreadCount == 0)
            lastThreadCount = thisThreadCount;

         if (lastThreadCount < thisThreadCount)
            Log("Threads +increased from " + lastThreadCount + " to " + thisThreadCount, LogLevel.Debug);
         else if (lastThreadCount > thisThreadCount)
            Log("Threads -decreased from " + lastThreadCount + " to " + thisThreadCount, LogLevel.Debug);
         
         lastThreadCount = thisThreadCount;
      }

      //Forces file save for all pertinent manager data
      public void SaveData()
      {
         if (Monitor.TryEnter(fileLock, ChatSettings.MaxFileWait))
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

               if(Monitor.TryEnter(managerLock, ChatSettings.MaxModuleWait))
               {
                  try
                  {
                     Log("Enter manager save lock", MyExtensions.Logging.LogLevel.Locks);

                     //Save users
                     if (MySerialize.SaveObject<Dictionary<int, User>>(SavePath(UserFile), users))
                        Log("Saved user data to file", MyExtensions.Logging.LogLevel.Debug);
                     else
                        Log("Couldn't save user data to file!", MyExtensions.Logging.LogLevel.Error);

//                     //Save messages
//                     if (MySerialize.SaveObject<List<UserMessageJSONObject>>(SavePath(MessageFile), messages))
//                        Log("Saved messages to file", MyExtensions.Logging.LogLevel.Debug);
//                     else
//                        Log("Couldn't save messages to file!", MyExtensions.Logging.LogLevel.Error);

                     //Save only messages which are real user messages into the file
                     var onlyMessagesHistory = history.ToDictionary(x => x.Key, y => y.Value.Where(z => z is MessageJSONObject).Select(z => (MessageJSONObject)z).ToList());

                     /*try
                     {
                        MySerialize2.SaveObject<Dictionary<string, List<MessageBaseJSONObject>>>(
                              SavePath(HistoryFile), onlyMessagesHistory);
                        Log("Saved history to file", MyExtensions.Logging.LogLevel.Debug);
                     }
                     catch(Exception ex)
                     {
                        Log("Couldn't save history to file! Reason: " + ex.ToString(), 
                              MyExtensions.Logging.LogLevel.Error);
                     }*/
                     //Save history
                     if(MySerialize.SaveObject<Dictionary<string, List<MessageJSONObject>>>(SavePath(HistoryFile), onlyMessagesHistory))
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
                  Log("Couldn't save general manager data because the manager is locked up! (wait: " + ChatSettings.MaxModuleWait.TotalSeconds + ")", MyExtensions.Logging.LogLevel.Error);
               }

               //Save all module data
               foreach (Module module in modules)
               {
                  if(Monitor.TryEnter(module.Lock, ChatSettings.MaxFileWait))
                  {
                     try
                     {
                        if(loader.SaveWrapper(module))
                           Log("Saved " + module.ModuleName + " data", MyExtensions.Logging.LogLevel.Debug);
                        else
                           Log("Couldn't save " + module.ModuleName + " data!", LogLevel.Error);
                     }
                     finally
                     {
                        Monitor.Exit(module.Lock);
                     }
                  }
                  else
                  {
                     Log("Couldn't save " + module.ModuleName + " data; it appears to be busy", LogLevel.Warning);
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
            Log("Couldn't save files... another process may still be writing", LogLevel.Warning);
         }
      }

      public string SavePath(string filename)
      {
         return ChatSettings.SaveFolder + filename;
      }

//      public int RegisterIrcUser(string username)
//      {
//         //Do NOT register relay users!
//         if (IrcSkipUsers().Contains(username))
//            return -1;
//         
//         lock (userLock)
//         {
//            int lookupID = UserLookup(username);
//
//            if (lookupID < 0)
//            {
//               int ID = users.Max(x => x.Key);
//
//               if (ID < 1000000000)
//                  ID = 1000000000;
//               
//               User newUser = new User(++ID);
//               newUser.SetIRC(username, true);
//               users.Add(newUser.UID, newUser);
//
//               Log("Registered new IRC user #" + newUser.UID + ": " + newUser.Username);
//
//               return newUser.UID;
//            }
//            else if (lookupID < 1000000000)
//            {
//               Log("Tried to register a real user as an IRC user!", MyExtensions.Logging.LogLevel.Error);
//            }
//            else
//               return lookupID;
//         }
//
//         return -1;
//      }

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
            allTags = ChatSettings.AcceptedTags.Union(rooms.Where(x => x.Value.Users.Contains(user)).Select(x => x.Key)).ToList();
            //allTags.Add(MessageBaseJSONObject.DefaultTag);
            Log("Exit allacceptedtagsforuser lock", MyExtensions.Logging.LogLevel.Locks);
         }
         return allTags;
      }

      public bool ValidTagForUser(int user, string tag)
      {
         return AllAcceptedTagsForUser(user).Contains(tag);
      }

      public bool IsPMTag(string tag)
      {
         lock(managerLock)
         {
            return rooms.Any(x => x.Key == tag);
         }
      }

      public List<UserInfo> UsersInPMRoom(string room)
      {
         lock(managerLock)
         {
            List<UserInfo> pmUsers = new List<UserInfo>();

            if(rooms.ContainsKey(room))
            {
               var loggedInUsers = LoggedInUsers();
               pmUsers = rooms[room].Users.Select(x => 
                     new UserInfo(users[x], loggedInUsers.ContainsKey(x))).ToList();
            }

            return pmUsers;
         }
      }

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
            else if (ChatSettings.AcceptedTags.Contains(room))//!rooms.ContainsKey(room))
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

               if (rooms[room].Users.Count < 1)
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

            if (newUsers.Count < 1)
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
      public bool CheckAuthentication(int uid, string key, out string error)
      {
         error = "";
         List<string> warnings = new List<string>();

         if (authServer.CheckAuth(uid, key))
         {
            if (TryRegister(uid, key))
            {
               if (!GetUser(uid).PullInfoFromQueryPage(out warnings))
               {
                  error = "Couldn't authenticate because your user information couldn't be found";
                  Log("Authentication failed: Couldn't get user information from website", LogLevel.Warning);
                  return false;
               }

               return true;
            }
         }
         else
         {
            Log("User " + uid + " tried to bind with bad auth code: " + key);
            error = "Key was invalid";
         }

         return false;
      }

      /// <summary>
      /// When a (hopefully) authenticated user leaves chat, this crap happens. LOCKS MANAGER!
      /// </summary>
      /// <param name="chatSession">Current Chat Session</param>
      public void UpdateAuthUserlist()
      {
         if (Monitor.TryEnter(managerLock, ChatSettings.MaxModuleWait))
         {
            try
            {
               Log("Enter leavechat lock", MyExtensions.Logging.LogLevel.Locks);
               //didLeave = activeChatters.Remove(chatSession);
               authServer.UpdateUserList(ConnectedUsers().Select(x => ((Chat)x).UID).Where(x => x > 0).ToList());//activeChatters.Select(x => x.UID).ToList());
               Log("Exit leavechat lock", MyExtensions.Logging.LogLevel.Locks);
            }
            finally
            {
               Monitor.Exit(managerLock);
            }
         }
      }

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

      public ChatTags AddMessage(MessageJSONObject message)
      {
         ChatTags warning = ChatTags.None, warning2 = ChatTags.None;
         User user = null;

         if(message.HasSender())
         {
            user = GetUser(message.sender.uid);

            //Update spam score
            if(message.IsSpammable())
            {
               lock(managerLock)
               {
                  Log("Enter messagespam lock", MyExtensions.Logging.LogLevel.Locks);
                  warning = user.MessageSpam(history.SelectMany(x => x.Value).Where(x => x is MessageJSONObject).Take(1000), message.message);
                  Log("Exit messagespam lock", MyExtensions.Logging.LogLevel.Locks);
               }
            }

            //Update spam score directly. If warning2 actually is something, update the real warning.
            lock (managerLock)
            {
               if(message.spamvalue > 0)
               {
                  warning2 = user.DirectSpam(message.spamvalue);
                  if (warning2 != ChatTags.None)
                     warning = warning2;
               }
            }
         }

         if (user.BlockedUntil < DateTime.Now)
         {
            AddMessageBase(message);

            if(message.HasSender())
               user.PerformOnPost();
         }

         return warning;
      }

      public void AddMessageBase(MessageBaseJSONObject message)
      {
         lock(managerLock)
         {
            //First add to history
            if (!history.ContainsKey(message.tag))
               history.Add(message.tag, new List<MessageBaseJSONObject>());
            //if(message.IsSendable())
            history[message.tag].Add(message);

            //Then, get rid of history we don't need anymore
            foreach(string key in history.Keys.ToList())
               history[key] = history[key].Where(x => !x.HasExpired()).OrderByDescending(x => x.GetCreationTime()).Take(ChatSettings.MaxMessageSend).ToList();

            //Remove messages from expired rooms
            history = history.Where(x => ChatSettings.AcceptedTags.Contains(x.Key) || rooms.ContainsKey(x.Key)/* || x.Key == MessageBaseJSONObject.DefaultTag*/).ToDictionary(x => x.Key, y => y.Value);

            Log("Enter message add lock", MyExtensions.Logging.LogLevel.Locks);
            //messages.Add(message);
            //messages = messages.Skip(Math.Max(0, messages.Count() - ChatSettings.MaxMessageKeep)).ToList();

            if (rooms.ContainsKey(message.tag))
               rooms[message.tag].OnMessage();

            Log("Exit message add lock", MyExtensions.Logging.LogLevel.Locks);
         }
      }

//      public void SendMessage(ModuleJSONObject message)
//      {
//         List<Chat> recipients = ConnectedUsers().Select(x => (Chat)x).Where(x => message.recipients.Contains(x.UID)).ToList();
//
//         foreach(Chat recipient in recipients)
//            recipient.MySend(message.ToString());
//      }
         
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
         Dictionary<int, User> returns;

         lock (managerLock)
         {
            Log("Enter loggedinuserget lock", MyExtensions.Logging.LogLevel.Locks);
            returns = ConnectedUsers().Select(x => (Chat)x).Where(x => users.ContainsKey(x.UID) && !users[x.UID].Hiding).DistinctBy(x => x.UID).ToDictionary(k => k.UID, v => users[v.UID]); //users.Where(x => activeUIDs.Contains(x.Key)).ToDictionary(k => k.Key, v => v.Value);
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

      //Get a JSON string representing a list of users currently in chat.
      public string ChatUserList(int caller, bool minimalData = true)
      {
         UserListJSONObject userList = new UserListJSONObject();

         lock (managerLock)
         {
            //First, let's get rid of old rooms
            rooms = rooms.Where(x => !x.Value.HasExpired).ToDictionary(x => x.Key, y => y.Value);
            var loggedInUsers = LoggedInUsers();

            //Now we can finally do the user thing
            userList.users = loggedInUsers
               .Where(x => !x.Value.ShadowBanned || x.Value.UID == caller)
               .OrderBy(x => x.Value.LastJoin)
               .Select(x => new UserJSONObject(new UserInfo(x.Value, true))).ToList();
            userList.rooms = rooms
               .Where(x => x.Value.Users.Contains(caller) && 
                     users.ContainsKey(x.Value.Creator) &&
                     (!users[x.Value.Creator].ShadowBanned || x.Value.Creator == caller))
               .Select(x => new RoomJSONObject(x.Value, UsersForModules())).ToList();

            if(!GetUser(caller).AnimatedAvatars)
            {
               foreach(UserJSONObject user in userList.users.Union(userList.rooms.SelectMany(x => x.users)))
                  user.avatar = GetUser(user.uid).AvatarStatic;
//               foreach(UserJSONObject user in userList.rooms.SelectMany(x => x.users))
//                  user.avatar = GetUser(user.uid).AvatarStatic;
            }

            if(minimalData)
            {
               foreach(UserJSONObject user in userList.users)
                  user.badges = new List<Badge>();
            }
         }
         return userList.ToString();
      }

      //Get a JSON string representing a list of the last X messages from ALL history.
      public string ChatMessageList(int user, int messageCount = 2,
            IEnumerable<string> desiredTags = null, bool minimalData = true)
      {
         if(desiredTags == null)
            desiredTags = AllAcceptedTagsForUser(user);
         else
            desiredTags = desiredTags.Intersect(AllAcceptedTagsForUser(user));

         MessageListJSONObject jsonMessages = new MessageListJSONObject();

         //This may change to a legacy number, like 10
         if (messageCount < 1)
            messageCount = ChatSettings.MaxMessageSend;

         lock (managerLock)
         {
            var loggedInUserKeys = LoggedInUsers().Keys.ToList();
            var actualUser = GetUser(user);

            jsonMessages.messages = history
               //Only get messages in the tags that we want
               .Where(x => desiredTags.Contains(x.Key))
               //Glop them all together. 
               .SelectMany(x => x.Value.Where(
                  //Oh, but we only want to glop stuff that we're allowed to receive!
                  y => y.IsSendable() && y.RealRecipientList(loggedInUserKeys).Contains(user)) //&&
                     //(!y.HasSender() || (users.ContainsKey(y.sender.uid) && 
                     //(!users[y.sender.uid].ShadowBanned || y.sender.uid == user))))
                  //Only take the amount that the user wants
                  .Take(messageCount))
               .OrderBy(x => x.id).ToList();

            //Log("Messages: " + jsonMessages.messages.Count);
            //Convert language tags. This alters the REAL language object, but mmmm that probably shouldn't be an issue, right?
            //No, because I ONLY save user messages in history to the disk, so in memory, these guys will always be typed correctly.
            foreach(MessageBaseJSONObject message in jsonMessages.messages)
            {
               if (message is ILanguageConvertibleBaseJSONObject)
               {
                  Log("Getting parameters");
                  var parameters = ((ILanguageConvertibleBaseJSONObject)message).Parameters;
                  Log("Parameters: " + parameters);
                  parameters.UpdateUser(actualUser);
                  Log("Updated parameter user");
                  message.message = ConvertTag(parameters);
                  Log("Converted tag to: " + message.message);
               }
            }

            bool animatedAvatars = actualUser.AnimatedAvatars;

            //A HORRIBLE hack! This will make the avatars bounce back and forth!
            foreach(MessageBaseJSONObject message in jsonMessages.messages.Where(x => x.HasSender()))
            {
               message.sender.avatar = animatedAvatars ? GetUser(message.sender.uid).Avatar : GetUser(message.sender.uid).AvatarStatic;
               if(minimalData) message.sender.badges = new List<Badge>();
            }
         }

         if(jsonMessages.messages.Count == 0)
            return null;

         return jsonMessages.ToString();
      }
         
//      public void BroadcastExclude(string message, string tag = "", List<Chat> exclude = null)
//      {
//         if (string.IsNullOrEmpty(message))
//            return;
//
//         if (exclude == null)
//            exclude = new List<Chat>();
//         
//         foreach (Chat chatter in ConnectedUsers().Select(x => (Chat)x)
//            .Where(x => string.IsNullOrWhiteSpace(tag) || AllAcceptedTagsForUser(x.UID).Contains(tag)).Except(exclude))
//         {
//            chatter.MySend(message);
//         }
//      }
//
//      public void Broadcast(LanguageTagParameters parameters, JSONObject container, List<Chat> exclude = null)
//      {
//         if (exclude == null)
//            exclude = new List<Chat>();
//
//         Log("Just before tag broadast", MyExtensions.Logging.LogLevel.SuperDebug);
//         foreach (Chat chatter in ConnectedUsers().Select(x => (Chat)x).Except(exclude))
//         {
//            parameters.UpdateUser(chatter.ThisUser);  //Update the message to reflect user preferences
//
//            //Send a tag message by filling the container with the tag parameters
//            if(!parameters.RawSendingUser.ShadowBanned || parameters.RawSendingUser.UID == chatter.ThisUser.UID)
//               chatter.MySend(parameters, container);    
//         }
//         Log("Just after tag broadcast", MyExtensions.Logging.LogLevel.SuperDebug);
//      }

//      private void ApplyTagIfDefault(List<MessageBaseJSONObject> messages, string tag)
//      {
//         if (!ChatSettings.AcceptedTags.Contains(tag))
//            tag = ChatSettings.GlobalTag;
//
//         foreach (MessageBaseJSONObject message in messages)
//            if (message.tag == MessageBaseJSONObject.DefaultTag)
//               message.tag = tag;
//      }

      public LanguageConvertibleWarningJSONObject NewWarningFromTag(LanguageTagParameters parameters, MessageBaseSendType sendType)
      {
         return new LanguageConvertibleWarningJSONObject(
            new WarningMessageJSONObject(ConvertTag(parameters), parameters.SendingUser) 
            { subtype = parameters.Tag.ToString().ToLower(), sendtype = sendType,
              tag = ChatSettings.GlobalTag}){ Parameters = parameters };
      }

      public LanguageConvertibleSystemJSONObject NewSystemMessageFromTag(LanguageTagParameters parameters, MessageBaseSendType sendType)
      {
         return new LanguageConvertibleSystemJSONObject(
            new SystemMessageJSONObject(ConvertTag(parameters), parameters.SendingUser) 
            { subtype = parameters.Tag.ToString().ToLower(), sendtype = sendType, 
              tag = ChatSettings.GlobalTag}){ Parameters = parameters };
//         return new SystemMessageJSONObject(manager.ConvertTag(parameters)) 
//         { subtype = parameters.Tag.ToString().ToLower(), sender = parameters.SendingUser };
      }

      /// <summary>
      /// Perform full processing on message, including adding it to history and sending it to the appropriate people
      /// </summary>
      /// <param name="message">Message.</param>
      /// <param name="user">User.</param>
      public void HandleMessage(MessageBaseJSONObject message, int user)
      {
         var realUser = GetUser(user);

         //Make sure that any messages without a tag at least have a valid tag (the global)
         if (message.tag == MessageBaseJSONObject.DefaultTag)
            message.tag = ChatSettings.GlobalTag;
         
         if (message is MessageJSONObject)
         {
            var warning = AddMessage((MessageJSONObject)message);
            if (warning != ChatTags.None)
            {
               HandleMessage(NewWarningFromTag(new LanguageTagParameters(warning, realUser), 
                        MessageBaseSendType.IncludeSender), user);
            }
            //new LanguageConvertibleWarningJSONObject() { Parameters = 
            //new LanguageTagParameters(warning, realUser, realUser), tag = ChatSettings.GlobalTag}, user);
         }
         else
         {
            AddMessageBase(message);
         }

         var recipients = message.RealRecipientList(LoggedInUsers().Keys.ToList());

         if (realUser.ShadowBanned)
            recipients.RemoveAll(x => x != user);

         //Send the message out to all the proper recipients.
         foreach (Chat chatter in ConnectedUsers().Select(x => (Chat)x).Where(x => recipients.Contains(x.UID)))
            chatter.MySend(ChatMessageList(chatter.UID, 1, new[] { message.tag }, true));
      }

      public string ConvertTag(LanguageTagParameters parameters)
      {
         return languageTags.GetTag(parameters);
      }

      public void BroadcastUserList()
      {
         Log("Just before userlist broadast", MyExtensions.Logging.LogLevel.SuperDebug);
         foreach (Chat chatter in ConnectedUsers().Select(x => (Chat)x))
            chatter.MySend(ChatUserList(chatter.UID, chatter.MinimalData));
         Log("Just after userlist broadcast", MyExtensions.Logging.LogLevel.SuperDebug);
      }

//      public void BroadcastMessageList(IEnumerable<string> desiredTags = null)
//      {
//         Log("Just before messagelist broadast", MyExtensions.Logging.LogLevel.SuperDebug);
//         foreach (Chat chatter in ConnectedUsers().Select(x => (Chat)x))
//            chatter.MySend(ChatMessageList(chatter.UID, 3, desiredTags, chatter.MinimalData));
//         Log("Just after messagelist broadast", MyExtensions.Logging.LogLevel.SuperDebug);
//      }

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
