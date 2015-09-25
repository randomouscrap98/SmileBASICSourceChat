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
   //This is the thing that will get passed as the... controller to Websockets?
   public class Chat : WebSocketBehavior
   {
      public const int HeaderSize = 64;

      private static int MaxMessageKeep = 1;
      private static int MaxMessageSend = 1;
      private static List<string> AllAcceptedTags = new List<string>();
      private static string GlobalTag = "";

      private static BandwidthMonitor bandwidth = new BandwidthMonitor();
      private static readonly Object bandwidthLock = new Object();
      private static AuthServer authServer;
      private static readonly Object authLock = new Object();
      private static List<UserMessageJSONObject> messages = new List<UserMessageJSONObject>();
      private static readonly Object messageLock = new Object();
      private static HashSet<Chat> activeChatters = new HashSet<Chat>();
      private static readonly Object chatLock = new Object();
      private static Dictionary<int, User> users = new Dictionary<int, User>();
      private static readonly Object userLock = new Object();

      private int uid = -1;

      private readonly System.Timers.Timer userUpdateTimer = new System.Timers.Timer();
      private readonly MyExtensions.Logging.Logger logger = MyExtensions.Logging.Logger.DefaultLogger;
      private readonly List<Module> modules = new List<Module>();


      //Set up the logger when building the chat provider. Logging will go out to a file and the console
      //if possible. Otherwise, log to the default logger (which is like throwing them away)
      public Chat(List<Module> modules, int userUpdateInterval, MyExtensions.Logging.Logger logger = null)
      {
         this.modules = modules;

         userUpdateTimer.Interval = userUpdateInterval * 1000;
         userUpdateTimer.Elapsed += UpdateActiveUserList;
         userUpdateTimer.Start();

         if(logger != null)
            this.logger = logger;
      }

      public static void SetChatParameters(int maxMessageKeep, int maxMessageSend, string acceptedTags, string globalTag)
      {
         MaxMessageKeep = maxMessageKeep;
         MaxMessageSend = maxMessageSend;

         GlobalTag = globalTag;
         AllAcceptedTags = acceptedTags.Split(",".ToCharArray(), 
            StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
         AllAcceptedTags.Add(GlobalTag);
      }

      public static BandwidthContainer GetBandwidth()
      {
         return (BandwidthContainer)bandwidth;
      }

      //This should be the ONLY place where the active state changes
      private void UpdateActiveUserList(object source, System.Timers.ElapsedEventArgs e)
      {
         if (ThisUser.StatusChanged)
         {
            ThisUser.SaveActiveState();
            MyBroadcast(GetUserList());
            logger.LogGeneral(UserLogString + " became " + (ThisUser.Active ? "active" : "inactive"), MyExtensions.Logging.LogLevel.Debug);
         }
      }

      //The UID for this session
      public int UID
      {
         get { return uid; }
      }

      //The user attached to this session
      public User ThisUser
      {
         get
         {
            lock (userLock)
            {
               if (!users.ContainsKey(uid) && uid > 0)
                  users.Add(uid, new User(uid));

               if (users.ContainsKey(uid))
               {
                  return users[uid];
               }
               else
               {
                  //Huh, this shouldn't happen!
                  if(uid > 0)
                     logger.LogGeneral("\"ThisUser\" access failed for uid " + uid, MyExtensions.Logging.LogLevel.Error);
                  
                  return new User(0);
               }
            }
         }
      }

      public string UserLogString
      {
         get
         {
            return ThisUser.Username + " (" + ThisUser.UID + ")";
         }
      }

      //Assign an authentication server 
      public static void LinkAuthServer(AuthServer linked)
      {
         lock(authLock)
         {
            authServer = linked;
         }
      }

      //Check authorization with the given user.
      public bool CheckAuth(string key, int uidCheck)
      {
         lock(authLock)
         {
            return authServer.GetAuth(uidCheck) == key;
         }
      }

      //Check authorization for this user's session
      public bool CheckAuth(string key)
      {
         return CheckAuth(key, uid);
      }

      //Update the authenticated users. You should do this on chat join and
      //leave, although it may be unnecessary for joins.
      public void UpdateAuthUsers()
      {
         lock(authLock)
         {
            authServer.UpdateUserList(GetUsers());
         }
      }

      //Get JUST a list of users currently in chat (useful for auth)
      public static List<int> GetUsers()
      {
         lock(chatLock)
         {
            return activeChatters.Select(x => x.UID).ToList();    
         }
      }

      //Get a JSON string representing a list of users currently in chat. Do NOT call this while locking on userLock
      public static string GetUserList()
      {
         UserListJSONObject userList = new UserListJSONObject();
         List<int> activeUsers = GetUsers();

         lock (userLock)
         {
            userList.users = activeUsers.Select(x => new UserJSONObject(users[x])).ToList();
         }

         return JsonConvert.SerializeObject(userList);
      }

      //Get a JSON string representing a list of the last 10 messages
      public string GetMessageList()
      {
         MessageListJSONObject jsonMessages = new MessageListJSONObject();
         List<UserMessageJSONObject> visibleMessages; 

         lock (messageLock)
         {
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
         //jsonMessages.messages.Reverse();

         return JsonConvert.SerializeObject(jsonMessages);
      }

      protected void MySend(string message)
      {
         if (string.IsNullOrEmpty(message))
            return;
         
         lock (bandwidthLock)
         {
            bandwidth.AddOutgoing(message.Length + HeaderSize);
         }

         try
         {
            Send(message);
         }
         catch (Exception e)
         {
            logger.Warning("Cannot send message: " + message + " to user: " + UserLogString + " because: " + e);
         }
      }

      public static void MyBroadcast(string message)
      {
         if (string.IsNullOrEmpty(message))
            return;

         lock (chatLock)
         {
            foreach (Chat chatter in activeChatters)
               chatter.MySend(message);
         }
//         try
//         {
//            int sessionCount = Sessions.Count;
//            lock (bandwidthLock)
//            {
//               bandwidth.AddOutgoing(sessionCount * (message.Length + HeaderSize));
//            }
//         }
//         catch
//         {
//            logger.Warning("Couldn't update bandwidth");
//         }
//
//         try
//         {
//            Sessions.Broadcast(message);
//         }
//         catch (Exception e)
//         {
//            logger.Warning("Cannot broadcast message: " + message + " because: " + e);
//         }
      }

      protected override void OnOpen()
      {
         //I don't know what we're doing for OnOpen yet.
      }

      //On closure of the websocket, remove ourselves from the list of active
      //chatters.
      protected override void OnClose(CloseEventArgs e)
      {
         logger.Log ("Session disconnect: " + uid);

         bool authenticated = (uid > 0);
         string username = ThisUser.Username;

         lock(chatLock)
         {
            uid = -1;
            activeChatters.Remove(this);
         }

         MyBroadcast(GetUserList());
         UpdateAuthUsers();

         if (authenticated)
            MyBroadcast((new SystemMessageJSONObject() { message = username + " has left the chat." }).ToString());
      }

      //I guess this is WHENEVER it receives a message?
      protected override void OnMessage(MessageEventArgs e)
      {
         ResponseJSONObject response = new ResponseJSONObject();
         response.result = false;
         dynamic json = new Object();
         string type = "";

         //Before anything else, log the amount of incoming data
         if (!string.IsNullOrEmpty(e.Data))
         {
            lock (bandwidthLock)
            {
               bandwidth.AddIncoming(e.Data.Length + HeaderSize);
            }
         }

         //First, just try to parse the JSON they gave us. If it's absolute
         //garbage (or just not JSON), let them know and quit immediately.
         try
         {
            json = JsonConvert.DeserializeObject(e.Data);
            type = json.type;
            response.from = type;
         }
         catch
         {
            response.errors.Add("Could not parse JSON");
         }

         //If we got a bind message, let's try to authorize this channel.
         if (type == "bind")
         {
            try
            {
               //First, gather information from the JSON. THis is so that if
               //the json is invalid, it will fail as soon as possible
               string key = (string)json.key;
               int newUser = (int)json.uid;

               //Oops, username was invalid
               if (newUser <= 0)
               {
                  logger.Log("Tried to bind a bad UID: " + newUser);
                  response.errors.Add("UID was invalid");
               }
               else
               {
                  //Oops, auth key was invalid
                  if (!CheckAuth(key, newUser))
                  {
                     logger.Log("User " + newUser + " tried to bind with bad auth code: " + key);
                     response.errors.Add("Key was invalid");
                  }
                  else
                  {
                     //Before we do anything, remove other chatting sessions
                     List<Chat> removals = activeChatters.Where(
                                              x => x.UID == newUser).Distinct().ToList();

                     foreach (Chat removeChat in removals)
                        Sessions.CloseSession(removeChat.ID);
                     
                     //OK, I may trust you, but there's one more step to do. Can we get your information from the webserver?
                     uid = newUser;

                     if (!ThisUser.PullInfoFromQueryPage())
                     {
                        uid = -1;
                        logger.Warning("Couldn't get user information from website");
                        response.errors.Add("Couldn't authenticate because your user information couldn't be found");
                     }
                     else
                     {
                        //BEFORE adding, broadcast the "whatever has entered the chat" message
                        MyBroadcast((new SystemMessageJSONObject() { 
                           message = ThisUser.Username + " has entered the chat." }).ToString());

                        //All is well.
                        activeChatters.Add(this);

                        //BEFORE sending out the user list, we need to perform onPing so that it looks like this user is active
                        ThisUser.PerformOnPing();
                        MyBroadcast(GetUserList());
                        UpdateAuthUsers();

                        logger.Log("Authentication complete: UID " + uid + " maps to username " + ThisUser.Username
                           + (ThisUser.CanStaffChat ? "(staff)" : ""));
                        response.result = true;

                     }
                  }
               }
            }
            catch
            {
               response.errors.Add("BIND message was missing fields");
            }
         }
         else if (type == "ping")
         {
            ThisUser.PerformOnPing();
            UpdateActiveUserList(null, null);
         }
         else if (type == "message")
         {
            try
            {
               //First, gather information from the JSON. This is so that if
               //the json is invalid, it will fail as soon as possible
               string key = json.key;
               string message = System.Security.SecurityElement.Escape((string)json.text);
               string tag = json.tag;
               //WarningJSONObject tempWarning = new WarningJSONObject();
               //ThisUser.PullInfoFromQueryPage();

               //These first things don't increase spam score in any way
               if(string.IsNullOrWhiteSpace(message))
               {
                  response.errors.Add("No empty messages please");
               }
               else if(!CheckAuth(key))
               {
                  logger.LogGeneral("Got invalid key " + key + " from " + UserLogString);
                  response.errors.Add("Your key is invalid");
               }
               else if (ThisUser.BlockedUntil >= DateTime.Now)
               {
                  response.errors.Add("You are blocked for " + 
                     ThisUser.SecondsToUnblock + " second(s) for spamming");
               }
               else if (ThisUser.Banned)
               {
                  response.errors.Add("You are banned from chat for " + 
                     StringExtensions.LargestTime(ThisUser.BannedUntil - DateTime.Now));
               }
               else if (tag == "admin" && !ThisUser.CanStaffChat)
               {
                  response.errors.Add("You can't post messages here. I'm sorry.");
               }
               else if (!AllAcceptedTags.Contains(tag))
               {
                  response.errors.Add("Your post has an unrecognized tag. Cannot display");
               }
               else
               {
                  List<JSONObject> outputs = new List<JSONObject>();
                  UserMessageJSONObject userMessage = new UserMessageJSONObject(ThisUser, message, tag);
                  UserCommand userCommand;
                  Module commandModule;
                  string commandError = "";
                  bool updateSpamScore = true;

                  //Step 1: parse a possible command. If no command is parsed, no module will be written.
                  if(TryCommandParse(userMessage, out commandModule, out userCommand, out commandError))
                  {
                     Dictionary<int, User> tempUsers = new Dictionary<int, User>();

                     lock(userLock)
                     {
                        tempUsers = new Dictionary<int, User>(users);
                     }

                     //We found a command. Send it off to the proper module and get the output
                     lock(commandModule.Lock)
                     {
                        outputs.AddRange(commandModule.ProcessCommand(userCommand, ThisUser, tempUsers));
                     }

                     //do not update spam score if command module doesn't want it
                     if(!userCommand.MatchedCommand.ShouldUpdateSpamScore)
                        updateSpamScore = false;

                     //For now, simply capture all commands no matter what.
                     userMessage.SetHidden();

                     logger.LogGeneral("Module " + commandModule.ModuleName + " processed command from " + UserLogString, 
                        MyExtensions.Logging.LogLevel.Debug);
                  }
                  else
                  {
                     //If an error was given, add it to our response
                     if(!string.IsNullOrWhiteSpace(commandError))
                     {
                        response.errors.Add("Command error: " + commandError);
                        userMessage.SetHidden();
                        updateSpamScore = false;
                     }
                  }

                  //Update spam score
                  if(updateSpamScore)
                  {
                     lock(messageLock)
                     {
                        WarningJSONObject warning = ThisUser.UpdateSpam(messages, message);
                        if(warning != null)
                           outputs.Add(warning);
                     }
                  }

                  //Only add message to message list if we previously set that we should.
                  if(ThisUser.BlockedUntil < DateTime.Now)
                  {
                     lock(messageLock)
                     {
                        messages.Add(userMessage);
                        messages = messages.Skip(Math.Max(0, messages.Count() - MaxMessageKeep)).ToList();
                     }
                     ThisUser.PerformOnPost();
                     response.result = response.errors.Count == 0;
                  }

                  //Now send out userlist if active status changed
                  UpdateActiveUserList(null, null);

                  //Since we added a new message, we need to broadcast.
                  if(response.result && userMessage.Display)
                     MyBroadcast(GetMessageList());

                  //Step 2: run regular message through all modules' regular message processor (probably no output?)

                  //Step 3: run all modules' post processor (no message required)

                  //Step 4: iterate over returned messages and send them out appropriately
                  foreach(JSONObject jsonMessage in outputs)
                  {
                     //System messages are easy: just send to user.
                     if(jsonMessage is SystemMessageJSONObject)
                     {
                        MySend(jsonMessage.ToString());
                     }
                     else if (jsonMessage is WarningJSONObject)
                     {
                        MySend(jsonMessage.ToString());
                     }
                     else if (jsonMessage is ModuleJSONObject)
                     {
                        ModuleJSONObject tempJSON = jsonMessage as ModuleJSONObject;
                        tempJSON.uid = uid;

                        if(string.IsNullOrWhiteSpace(tempJSON.tag))
                           tempJSON.tag = tag;

                        if(tempJSON.broadcast)
                        { 
                           MyBroadcast(tempJSON.ToString());
                           logger.LogGeneral("Broadcast a module message", MyExtensions.Logging.LogLevel.Debug);
                        }
                        else
                        {
                           
                           foreach(int user in tempJSON.recipients.Distinct())
                           {
                              if(activeChatters.Any(x => x.UID == user))
                                 activeChatters.First(x => x.UID == user).MySend(tempJSON.ToString());
                              else
                                 logger.LogGeneral("Recipient " + user + " in module message was not found", MyExtensions.Logging.LogLevel.Warning);
                           }

                           //No recipients? You probably meant to send it to the current user.
                           if(tempJSON.recipients.Count == 0)
                              MySend(tempJSON.ToString());
                        }
                     }
                  }
                  //end of regular message processing
               }
            }
            catch (Exception messageError)
            {
               response.errors.Add("Internal server error: " + messageError/*.Message*/);
               //response.errors.Add("Message was missing fields");
            }
         }
         else if (type == "request")
         {
            try
            {
               string wanted = json.request;

               if(wanted == "userList")
               {
                  MySend(GetUserList());
                  response.result = true;
               }
               else if (wanted == "messageList")
               {
                  MySend(GetMessageList());
                  response.result = true;
               }
               else
               {
                  response.errors.Add("Invalid request field");
               }
            }
            catch
            {
               response.errors.Add("Request was missing fields");
            }
         }

         //Send the "OK" message back.
         MySend(response.ToString());

         logger.LogGeneral ("Got message: " + e.Data, MyExtensions.Logging.LogLevel.Debug);
      }
         
      /// <summary>
      /// Parse and build command if possible. Assign module which will handle command. 
      /// </summary>
      /// <returns>Successful command parse</returns>
      /// <param name="message">Message.</param>
      /// <param name="commandModule">Command module.</param>
      /// <param name="userCommand">User command.</param>
      private bool TryCommandParse(UserMessageJSONObject message, out Module commandModule, out UserCommand userCommand, out string error)
      {
         userCommand = null;
         commandModule = null;
         error = "";

         UserCommand tempUserCommand = null;

         //Check through all modules for possible command match
         foreach(Module module in modules)
         {
            //We already found the module, so get out.
            if(commandModule != null)
               break;

            //Check through this module's command for possible match
            foreach(ModuleCommand command in module.Commands)
            {
               Match match = Regex.Match(message.message, command.FullRegex, RegexOptions.Singleline);

               //This command matched, so preparse the command and get out of here.
               if(match.Success)
               {
                  //Build arguments from regex.
                  List<string> arguments = new List<string>();
                  for(int i = 2; i < match.Groups.Count; i++)
                     arguments.Add(match.Groups[i].Value.Trim());

                  //We have a user command. Cool, but will it parse? Ehhhh.
                  tempUserCommand = new UserCommand(match.Groups[1].Value, arguments, message, command);

                  //Now preprocess the command to make sure certain standard fields check out (like username)
                  for( int i = 0; i < command.Arguments.Count; i++)
                  {
                     //Users need to exist. If not, throw error.
                     if (command.Arguments[i].Type == ArgumentType.User)
                     {
                        if (tempUserCommand.Arguments[i].StartsWith("?"))
                        {
                           var loggedInUsers = GetUsers();
                           tempUserCommand.Arguments[i] = StringExtensions.AutoCorrectionMatch(
                              tempUserCommand.Arguments[i].Replace("?", ""), 
                              users.Where(x => loggedInUsers.Contains(x.Key)).Select(x => x.Value.Username).ToList());
                        }
                        if (!users.Any(x => x.Value.Username == tempUserCommand.Arguments[i]))
                        {
                           error = "User does not exist";
                           return false;
                        }
                     }
                     else if (command.Arguments[i].Type == ArgumentType.Module)
                     {
                        if (!modules.Any(x => x.Nickname == arguments[i].ToLower()))
                        {
                           error = "Module does not exist";
                           return false;
                        }
                     }
                  }

                  commandModule = module;
                  break;
               }
            }
         }

         userCommand = new UserCommand(tempUserCommand);
         return (commandModule != null);
      }
   }
      
   public class GlobalModule : Module
   {

      public GlobalModule()
      {
         commands.AddRange(new List<ModuleCommand> {
            new ModuleCommand("about", new List<CommandArgument>(), "See information about the chat server"),
            new ModuleCommand("uactest", new List<CommandArgument> {
               new CommandArgument("shortuser", ArgumentType.User)
            }, "Test user autocorrection (use ? on username to enable shorthand)"),
            new ModuleCommand("help", new List<CommandArgument>(), "See all modules which you can get help with"),
            new ModuleCommand("help", new List<CommandArgument>() {
               new CommandArgument("module", ArgumentType.Module)
            }, "Get help about a particular module")
         });
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, User user, Dictionary<int, User> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();

         ModuleJSONObject output = new ModuleJSONObject();

         switch (command.Command)
         {
            case "about":
               output = new ModuleJSONObject();
               BandwidthContainer bandwidth = Chat.GetBandwidth();
               DateTime built = ChatRunner.MyBuildDate();
               output.message = 
                  "---Build info---\n" +
                  "Version: " + ChatRunner.AssemblyVersion() + "\n" +
                  "Built " + StringExtensions.LargestTime(DateTime.Now - built) + " ago (" + built.ToString("R") + ")\n" +
                  "---Data usage---\n" +
                  "Outgoing: " + bandwidth.GetTotalBandwidthOutgoing() + " (1h: " + bandwidth.GetHourBandwidthOutgoing() + ")\n" +
                  "Incoming: " + bandwidth.GetTotalBandwidthIncoming() + " (1h: " + bandwidth.GetHourBandwidthIncoming() + ")\n";
               outputs.Add(output);
               break;
            case "help":
               output = new ModuleJSONObject();
               if (command.Arguments.Count == 0)
               {
                  output.message = "Which module would you like help with?\n";

                  foreach (Module module in ChatRunner.ActiveModules)
                     output.message += "\n" + module.Nickname;

                  output.message += "\n\nRerun help command with a module name to see commands for that module";
                  outputs.Add(output);
               }
               else
               {
                  output.message = "Commands for the " + command.Arguments[0] + " module:\n";

                  foreach(ModuleCommand moduleCommand in ChatRunner.ActiveModules.First(x => x.Nickname == command.Arguments[0]).Commands)
                     output.message += "\n" + moduleCommand.DisplayString;

                  outputs.Add(output);
               }
               break;
            case "uactest":
               output = new ModuleJSONObject();
               output.message = "User " + command.OriginalArguments[0] + " corrects to " + command.Arguments[0];
               outputs.Add(output);
               break;
         }

         return outputs;
      }
   }
}
