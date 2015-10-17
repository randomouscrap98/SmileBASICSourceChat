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

      public readonly int MaxModuleWaitSeconds = 5;
      public readonly int MaxMessageKeep = 1;
      public readonly int MaxMessageSend = 1;
      public readonly List<string> AllAcceptedTags = new List<string>();
      public readonly string GlobalTag = "";      
      public readonly BandwidthMonitor Bandwidth = new BandwidthMonitor();
      public readonly string SaveFolder = "";

      private List<Module> modules = new List<Module>();
      private List<UserMessageJSONObject> messages = new List<UserMessageJSONObject>();

      private readonly ModuleLoader loader;
      private readonly AuthServer authServer;
      private readonly LanguageTags languageTags;
      private readonly HashSet<Chat> activeChatters = new HashSet<Chat>();
      private readonly Dictionary<int, User> users = new Dictionary<int, User>();
      private Dictionary<string, PMRoom> rooms = new Dictionary<string, PMRoom>();
      private readonly System.Timers.Timer saveTimer;

      public readonly MyExtensions.Logging.Logger Logger = MyExtensions.Logging.Logger.DefaultLogger;

      public readonly Object userLock = new Object();
      public readonly Object managerLock = new Object();
      public readonly Object fileLock = new Object();

      //Set up the logger when building the chat provider. Logging will go out to a file and the console
      //if possible. Otherwise, log to the default logger (which is like throwing them away)
      public ChatManager(ModuleLoader loader, AuthServer authServer, LanguageTags languageTags, 
         int maxMessageKeep, int maxMessageSend, int saveInterval, int maxModuleWait, 
         string acceptedTags, string globalTag, string saveFolder, 
         MyExtensions.Logging.Logger logger = null)
      {
         this.modules = loader.ActiveModules;
         this.authServer = authServer;
         this.loader = loader;
         this.languageTags = languageTags;

         MaxMessageKeep = maxMessageKeep;
         MaxMessageSend = maxMessageSend;
         MaxModuleWaitSeconds = maxModuleWait;

         GlobalTag = globalTag;
         AllAcceptedTags = acceptedTags.Split(",".ToCharArray(), 
            StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
         AllAcceptedTags.Add(GlobalTag);

         SaveFolder = MyExtensions.StringExtensions.PathFixer(saveFolder);

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

         //Attempt to load the users in the constructor. Again, not a big deal if it doesn't load.
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

         //now set up the save file timer
         saveTimer = new System.Timers.Timer(1000 * saveInterval);
         saveTimer.Elapsed += SaveTimerElapsed;
         saveTimer.Start();
      }

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

      //Start the save process
      private void SaveTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
      {
         SaveData();
         Log("Saved chat data to files");
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

               lock (userLock)
               {
                  Log("Enter user lock", MyExtensions.Logging.LogLevel.Locks);
                  if (MySerialize.SaveObject<Dictionary<int, User>>(SavePath(UserFile), users))
                     Log("Saved user data to file", MyExtensions.Logging.LogLevel.Debug);
                  else
                     Log("Couldn't save user data to file!", MyExtensions.Logging.LogLevel.Error);
                  Log("Exit user lock", MyExtensions.Logging.LogLevel.Locks);
               }

               if(Monitor.TryEnter(managerLock, MaxModuleWaitSeconds))
               {
                  try
                  {
                     Log("Enter manager save lock", MyExtensions.Logging.LogLevel.Locks);
                     if (MySerialize.SaveObject<List<UserMessageJSONObject>>(SavePath(MessageFile), messages))
                        Log("Saved messages to file", MyExtensions.Logging.LogLevel.Debug);
                     else
                        Log("Couldn't save messages to file!", MyExtensions.Logging.LogLevel.Error);
                     Log("Exit manager save lock", MyExtensions.Logging.LogLevel.Locks);
                  }
                  finally
                  {
                     Monitor.Exit(managerLock);
                  }
               }
               else
               {
                  Log("Couldn't save messages because the manager is locked up!", MyExtensions.Logging.LogLevel.Error);
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

      public User GetUser(int uid)
      {
         User user = new User(0);
         lock (userLock)
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
         lock (userLock)
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

         lock (userLock)
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
            else if (!users.ContainsKey(creator))
            {
               error = "You don't seem to exist... I'm not sure how to create the room";
            }
            else if (users[creator].Banned || users[creator].Blocked)
            {
               error = "You are banned or blocked and cannot create a room";
            }
//            else if (rooms.Count(x => x.Value.Creator == creator) >= 3)
//            {
//               error = "You've already have 3 PM rooms
//            }
            else
            {
               PMRoom newRoom = new PMRoom(newUsers, creator, TimeSpan.FromMinutes(10));
               rooms.Add(newRoom.Name, newRoom);
               result = true;
            }

            Log("Exit createpmroom lock", MyExtensions.Logging.LogLevel.Locks);
         }

         return result;
      }

      //ONLY the registration of a new user. It has nothing to do with authentication
      private bool TryRegister(int uid, string key)
      {
         bool good = false;
         lock (userLock)
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
         if (Monitor.TryEnter(managerLock, MaxModuleWaitSeconds))
         {
            try
            {
               Log("Enter leavechat lock", MyExtensions.Logging.LogLevel.Locks);
               activeChatters.Remove(chatSession);
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
         if (users.ContainsKey(chatSession.UID))
         {
            if (!users[chatSession.UID].PerformOnChatLeave())
               Logger.Warning("User session timer was in an invalid state!");

            if (users[chatSession.UID].ShowMessages)
               Broadcast(new LanguageTagParameters(ChatTags.Leave, users[chatSession.UID]), new SystemMessageJSONObject());
         }
      }

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
            activeUIDs = activeChatters.Select(x => x.UID).ToList();
            Log("exit loggedinactive lock", MyExtensions.Logging.LogLevel.Locks);
         }

         Dictionary<int, User> returns;

         lock (userLock)
         {
            Log("Enter loggedinuserget lock", MyExtensions.Logging.LogLevel.Locks);
            returns = users.Where(x => activeUIDs.Contains(x.Key)).ToDictionary(k => k.Key, v => v.Value);
            Log("exit loggedinuserget lock", MyExtensions.Logging.LogLevel.Locks);
         }

         return returns;
      }

      public Dictionary<int, UserInfo> UsersForModules()
      {
         List<int> loggedInUsers = LoggedInUsers().Keys.ToList();
         Dictionary<int, UserInfo> returns;

         lock (userLock)
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
            userList.users = LoggedInUsers().Select(x => new UserJSONObject(x.Value)).ToList();
            userList.rooms = rooms.Where(x => x.Value.Users.Contains(caller)).Select(x => new RoomJSONObject(x.Value, users)).ToList();
         }
         return userList.ToString();
      }

      //Get a JSON string representing a list of the last 10 messages
      public string ChatMessageList(int user)
      {
         MessageListJSONObject jsonMessages = new MessageListJSONObject();
         List<UserMessageJSONObject> visibleMessages; 

         lock (managerLock)
         {
            Log("Enter chatmessagelist lock", MyExtensions.Logging.LogLevel.Locks);
            //Messages are all readonly, so it's OK to have just references
            visibleMessages = messages.Where(x => x.Display && (DateTime.Now - x.PostTime()).TotalDays < 1.0).ToList();
            Log("Exit chatmessagelist lock", MyExtensions.Logging.LogLevel.Locks);
         }

         foreach (string tag in AllAcceptedTagsForUser(user))
         {
            List<UserMessageJSONObject> tagMessages = visibleMessages.Where(x => x.tag == tag).ToList();
            for (int i = 0; i < Math.Min(MaxMessageSend, tagMessages.Count); i++)
               jsonMessages.messages.Add(tagMessages[tagMessages.Count - 1 - i]);
         }

         //Oops, remember we added them in reverse order. Fix that
         jsonMessages.messages = jsonMessages.messages.OrderBy(x => x.id).ToList();

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

      public List<Module> GetModuleListCopy()
      {
         return new List<Module>(modules);
      }
   }
}
