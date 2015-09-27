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
   //This is the manager for the chat. It includes information which all individual chat sessions
   //will probably need, and manages the saving/loading of resources.
   public class ChatManager
   {
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
      private readonly HashSet<Chat> activeChatters = new HashSet<Chat>();
      private readonly Dictionary<int, User> users = new Dictionary<int, User>();
      private readonly System.Timers.Timer saveTimer;

      public readonly MyExtensions.Logging.Logger Logger = MyExtensions.Logging.Logger.DefaultLogger;

      public readonly Object userLock = new Object();
      public readonly Object managerLock = new Object();
      public readonly Object fileLock = new Object();

      //Set up the logger when building the chat provider. Logging will go out to a file and the console
      //if possible. Otherwise, log to the default logger (which is like throwing them away)
      public ChatManager(ModuleLoader loader, AuthServer authServer, int maxMessageKeep, int maxMessageSend,
         int saveInterval, int maxModuleWait, string acceptedTags, string globalTag, string saveFolder, 
         MyExtensions.Logging.Logger logger = null)
      {
         this.modules = loader.ActiveModules;
         this.authServer = authServer;
         this.loader = loader;

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
            Logger.LogGeneral("Loaded bandwidth data from file", MyExtensions.Logging.LogLevel.Debug, LogTag);
         }
         else
         {
            Logger.Error("Couldn't load bandwidth data! Defaulting to empty bandwidth", LogTag);
         }

         //Attempt to load the users in the constructor. Again, not a big deal if it doesn't load.
         Dictionary<int, User> tempUsers;

         if (MySerialize.LoadObject<Dictionary<int, User>>(SavePath(UserFile), out tempUsers))
         {
            users = tempUsers;
            Logger.LogGeneral("Loaded user data from file", MyExtensions.Logging.LogLevel.Debug, LogTag);
         }
         else
         {
            Logger.Error("Couldn't load user data! Defaulting to empty user dictionary", LogTag);
         }

         //Attempt to load the users in the constructor. Again, not a big deal if it doesn't load.
         List<UserMessageJSONObject> tempMessages;

         if (MySerialize.LoadObject<List<UserMessageJSONObject>>(SavePath(MessageFile), out tempMessages))
         {
            messages = tempMessages;
            UserMessageJSONObject.FindNextID(messages);
            Logger.LogGeneral("Loaded messages from file", MyExtensions.Logging.LogLevel.Debug, LogTag);
         }
         else
         {
            Logger.Error("Couldn't load messages! Defaulting to empty message list", LogTag);
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

      //Start the save process
      private void SaveTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
      {
         Logger.Log("Started the save event", LogTag);
         SaveData();
      }

      //Forces file save for all pertinent manager data
      private void SaveData()
      {
         if (Monitor.TryEnter(fileLock, TimeSpan.FromSeconds(FileWaitSeconds)))
         {
            try
            {
               lock (Bandwidth.byteLock)
               {
                  if (MySerialize.SaveObject<BandwidthMonitor>(SavePath(BandwidthFile), Bandwidth))
                     Logger.LogGeneral("Saved bandwidth data to file", MyExtensions.Logging.LogLevel.Debug, LogTag);
                  else
                     Logger.Error("Couldn't save bandwidth data to file!", LogTag);
               }

               lock (userLock)
               {
                  
                  if (MySerialize.SaveObject<Dictionary<int, User>>(SavePath(UserFile), users))
                     Logger.LogGeneral("Saved user data to file", MyExtensions.Logging.LogLevel.Debug, LogTag);
                  else
                     Logger.Error("Couldn't save user data to file!", LogTag);
               }

               lock (managerLock)
               {
                  if (MySerialize.SaveObject<List<UserMessageJSONObject>>(SavePath(MessageFile), messages))
                     Logger.LogGeneral("Saved messages to file", MyExtensions.Logging.LogLevel.Debug, LogTag);
                  else
                     Logger.Error("Couldn't save messages to file!", LogTag);
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
         lock (userLock)
         {
            if (users.ContainsKey(uid))
               return users[uid];
         }

         return new User(0);
      }

      public bool UserExists(int uid)
      {
         lock (userLock)
         {
            return users.ContainsKey(uid);
         }
      }

      public int UserLookup(string username)
      {
         lock (userLock)
         {
            try
            {
               return users.First(x => x.Value.Username == username).Key;
            }
            catch
            {
               return -1;
            }
         }
      }

      //ONLY the registration of a new user. It has nothing to do with authentication
      private bool TryRegister(int uid, string key)
      {
         lock (userLock)
         {
            if (users.ContainsKey(uid))
            {
               return true;
            }
            else if (authServer.CheckAuth(uid, key))
            {
               users.Add(uid, new User(uid));
               return true;
            }
         }

         return false;
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
                  duplicates = activeChatters.Where(x => x.UID == uid).ToList();
                  activeChatters.RemoveWhere(x => x.UID == uid);
                  activeChatters.Add(chatSession);
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
         lock (managerLock)
         {
            activeChatters.Remove(chatSession);
            authServer.UpdateUserList(activeChatters.Select(x => x.UID).ToList());
            Broadcast(ChatUserList());
         }
      }

      public WarningJSONObject SendMessage(UserMessageJSONObject message)
      {
         WarningJSONObject warning = null;

         User user = GetUser(message.uid);

         //Update spam score
         if(message.Spammable)
         {
            lock(managerLock)
            {
               warning = user.UpdateSpam(messages, message.message);
            }
         }

         //Only add message to message list if we previously set that we should.
         if(user.BlockedUntil < DateTime.Now)
         {
            lock(managerLock)
            {
               messages.Add(message);
               messages = messages.Skip(Math.Max(0, messages.Count() - MaxMessageKeep)).ToList();
            }
            user.PerformOnPost();
         }

         return warning;
      }

      public void SendMessage(ModuleJSONObject message)
      {
         lock (managerLock)
         {
            foreach (int user in message.recipients.Distinct())
            {
               if (activeChatters.Any(x => x.UID == user))
                  activeChatters.First(x => x.UID == user).MySend(message.ToString());
               else
                  Logger.LogGeneral("Recipient " + user + " in module message was not found", MyExtensions.Logging.LogLevel.Warning);
            }
         }
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
            activeUIDs = activeChatters.Select(x => x.UID).ToList();
         }
         
         lock (userLock)
         {
            return users.Where(x => activeUIDs.Contains(x.Key)).ToDictionary(k => k.Key, v => v.Value);
         }
      }

      //Get a JSON string representing a list of users currently in chat. Do NOT call this while locking on userLock
      public string ChatUserList()
      {
         UserListJSONObject userList = new UserListJSONObject();
         userList.users = LoggedInUsers().Select(x => new UserJSONObject(x.Value)).ToList();
         return userList.ToString();
      }

      //Get a JSON string representing a list of the last 10 messages
      public string ChatMessageList()
      {
         MessageListJSONObject jsonMessages = new MessageListJSONObject();
         List<UserMessageJSONObject> visibleMessages; 

         lock (managerLock)
         {
            //Messages are all readonly, so it's OK to have just references
            visibleMessages = messages.Where(x => x.Display).ToList();
         }

         foreach (string tag in AllAcceptedTags.Where(x => x != GlobalTag))
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

         lock (managerLock)
         {
            foreach (Chat chatter in activeChatters.Except(exclude))
               chatter.MySend(message);
         }
      }

      public void BroadcastUserList()
      {
         Broadcast(ChatUserList());
      }

      public void BroadcastMessageList()
      {
         Broadcast(ChatMessageList());
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
