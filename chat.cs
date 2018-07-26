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
using Microsoft.CSharp.RuntimeBinder;

namespace ChatServer
{
   public delegate void ChatEventHandler(object source, EventArgs e);

   //This is the thing that will get passed as the... controller to Websockets?
   public class Chat : WebSocketUser
   {
      public const int HeaderSize = 64;

      private int uid = -1;
      private int queryFailures = 0;
      private long sessionID = 0;
      private string lastAvatar = "";
      private bool minimalData = true;
      private DateTime lastBan = new DateTime(0);

      public readonly DateTime Startup = DateTime.Now;
      private readonly System.Timers.Timer userUpdateTimer = new System.Timers.Timer();
      private readonly ChatServer manager;

      private DateTime lastPing = DateTime.Now;

      //Set up the logger when building the chat provider. Logging will go out to a file and the console
      //if possible. Otherwise, log to the default logger (which is like throwing them away)
      public Chat(int userUpdateInterval, ChatServer manager)
      {
         userUpdateTimer.Interval = userUpdateInterval * 1000;
         userUpdateTimer.Elapsed += UpdateActiveUserList;
         userUpdateTimer.Start();

         //Assume the manager that was given was good
         this.manager = manager;
      }

      public void Log(string message, LogLevel level = LogLevel.Normal)
      {
         string tag = "Chatuser";

         if (UID > 0)
            tag += UID;
         else
            tag += "X";
         
         manager.ChatSettings.LogProvider.LogGeneral(message, level, tag);
      }

      //This should be the ONLY place where the active state changes
      private void UpdateActiveUserList(object source, System.Timers.ElapsedEventArgs e)
      {
         if (ThisUser.StatusChanged)
         {
            ThisUser.SaveActiveState();
            manager.BroadcastUserList();
            Log(UserLogString + " became " + (ThisUser.Active ? "active" : "inactive"), MyExtensions.Logging.LogLevel.Debug);
         }
      }

      //The UID for this session
      public int UID
      {
         get { return uid; }
      }

      public TimeSpan TimeSincePing
      {
         get
         {
            return DateTime.Now - lastPing;
         }
      }

      //The user attached to this session
      public User ThisUser
      {
         get
         {
            return manager.GetUser(UID);
         }
      }

      public string UserLogString
      {
         get
         {
            return ThisUser.Username + " (" + ThisUser.UID + ")";
         }
      }

      public bool MinimalData
      {
         get { return minimalData; }
      }

      public void MyHandle(MessageBaseJSONObject message, bool forceSend = false)
      {
         MessageListJSONObject list = new MessageListJSONObject();
         list.messages = new List<MessageBaseJSONObject>() { message };
         MySend(list.ToString(), forceSend);
      }

      public void MySend(string message, bool forceSend = false)
      {
         if (string.IsNullOrEmpty(message))
            return;

         //Do nothing until user has accepted policy
         if (!ThisUser.AcceptedPolicy && !forceSend)
         {
            Log("Cannot send message because user has not accepted policy yet. ForceSend: " + forceSend,
               MyExtensions.Logging.LogLevel.SuperDebug);
            return;
         }
            
         manager.Bandwidth.AddOutgoing(message.Length + HeaderSize);
         Send(message);
      }

      public LanguageConvertibleWarningJSONObject NewWarningFromTag(LanguageTagParameters parameters, MessageBaseSendType sendType)
      {
         return manager.NewWarningFromTag(parameters, sendType);
      }
      public LanguageConvertibleSystemJSONObject NewSystemMessageFromTag(LanguageTagParameters parameters, MessageBaseSendType sendType)
      {
         return manager.NewSystemMessageFromTag(parameters, sendType);
      }

      public LanguageTagParameters QuickParams(ChatTags tag)
      {
         return new LanguageTagParameters(tag, ThisUser);
      }

      public List<MessageBaseJSONObject> AddModuleTags(List<MessageBaseJSONObject> outputs, Module module)
      {
         foreach(MessageBaseJSONObject jsonMessage in outputs)
            if(jsonMessage is ModuleJSONObject)
               ((ModuleJSONObject)jsonMessage).module = module.Nickname;

         return outputs;
      }

      //On closure of the websocket, remove ourselves from the list of active
      //chatters.
      public override void ClosedConnection()
      {
         Log ("Session disconnect: " + uid);

         //Only perform special leaving messages and processing if the user was real
         if (UID > 0)
         {
            ThisUser.PerformOnChatLeave(sessionID);

            if (ThisUser.ShowMessages && !ThisUser.ShadowBanned)
               SendFromMe(NewSystemMessageFromTag(QuickParams(ChatTags.Leave), MessageBaseSendType.BroadcastExceptSender));
         }

         //Now "technically" remove the user from lists
         int tempID = uid;
         uid = -1;
         manager.UpdateAuthUserlist();
         manager.BroadcastUserList();

         //Uh now put it back?
         uid = tempID;

         //Now get rid of events
         userUpdateTimer.Elapsed -= UpdateActiveUserList;
      }

      public void SendFromMe(MessageBaseJSONObject message)
      {
         SetFromMeIfNothing(new List<MessageBaseJSONObject>() { message });
         manager.HandleMessage(message, this.UID);
      }

      //This blocks ALL commands that appear as regular messages if you're
      //hiding. Ouch, some people will be upsettles.
      public void SendAndProcessFromMe(MessageJSONObject message)
      {
         if(!manager.IsPMTag(message.tag) && ThisUser.Hiding)
         {
            SendFromMe(new WarningMessageJSONObject("You're hiding! Don't send messages!"));
            return;
         }

         SendFromMe(message);

         if(!manager.IsPMTag(message.tag))
            ProcessMessage(message, manager.UsersForModules());
      }

      public void SendModuleMessagesFromMe(List<MessageBaseJSONObject> messages, Module module) 
      {
         foreach (MessageBaseJSONObject message in AddModuleTags(messages, module))
         {
            if(message is MessageJSONObject)
               SendAndProcessFromMe((MessageJSONObject)message);
            else
               SendFromMe(message);
         }
      }

      //I guess this is WHENEVER it receives a message?
      public override void ReceivedMessage(string rawMessage)
      {
         ResponseJSONObject response = new ResponseJSONObject();
         List<string> warnings = new List<string>();
         response.result = false;
         dynamic json = new Object();
         string type = "";

         //Before anything else, log the amount of incoming data
         if (!string.IsNullOrEmpty(rawMessage))
         {
            manager.Bandwidth.AddIncoming(rawMessage.Length + HeaderSize);
         }
            
         //First, just try to parse the JSON they gave us. If it's absolute
         //garbage (or just not JSON), let them know and quit immediately.
         try
         {
            json = JsonConvert.DeserializeObject(rawMessage);
            type = json.type;
            response.from = type;
         }
         catch
         {
            response.errors.Add("Could not parse JSON");
            response.errors.Add("Given string: " + rawMessage);
         }

         //If we got a bind message, let's try to authorize this channel.
         if (type == "bind")
         {
            response.extras.Add("modules", manager.GetModuleListCopy(uid).Select(x => x.Nickname).ToList());
            response.extras.Add("version", ChatRunner.AssemblyVersion());
            response.extras.Add("basicTags", manager.ChatSettings.AcceptedTags);
            if (uid > 0)
            {
               response.result = true;
               Log("Received another bind message from UID: " + uid, LogLevel.Debug);
            }
            else
            {
               try
               {
                  //First, gather information from the JSON. This is so that if
                  //the json is invalid, it will fail as soon as possible
                  string key = (string)json.key;
                  int newUser = (int)json.uid;

                  try { minimalData = (bool)json.lessData; }
                  catch { minimalData = true; }

                  //Oops, username was invalid
                  if (newUser <= 0)
                  {
                     Log("Tried to bind a bad UID: " + newUser);
                     response.errors.Add("UID was invalid");
                  }
                  else
                  {
                     string error;

                     if (!manager.CheckAuthentication(newUser, key, out error))
                     {
                        response.errors.Add(error);
                     }
                     else
                     {
                        //Before we do anything, remove other chatting sessions
                        foreach (Chat removeChat in GetAllUsers().Select(x => (Chat)x).Where(x => x.UID == newUser && x != this))
                           removeChat.CloseSelf();
                     
                        uid = newUser;

                        int bindQueryFailures = 0;

                        while(!ThisUser.PullInfoFromQueryPage(out warnings) && 
                              ++bindQueryFailures < manager.ChatSettings.MaxUserQueryFailures);

                        if(bindQueryFailures >= manager.ChatSettings.MaxUserQueryFailures)
                        {
                           Log("Couldn't pull user data during bind after " + bindQueryFailures + " tries");
                           response.errors.Add("Couldn't pull user data from website");
                        }
                        else
                        {
                           //BEFORE adding, broadcast the "whatever has entered the chat" message
                           if (ThisUser.ShowMessages && !ThisUser.ShadowBanned)
                              SendFromMe(NewSystemMessageFromTag(QuickParams(ChatTags.Join), MessageBaseSendType.BroadcastExceptSender));

                           manager.AddMessageBase(NewSystemMessageFromTag(QuickParams(ChatTags.Welcome), MessageBaseSendType.IncludeSender));
                           ChatTags enterSpamWarning = ThisUser.JoinSpam();

                           if (enterSpamWarning != ChatTags.None)
                              SendFromMe(NewWarningFromTag(QuickParams(enterSpamWarning), MessageBaseSendType.IncludeSender));

                           //BEFORE sending out the user list, we need to perform onPing so that it looks like this user is active
                           sessionID = ThisUser.PerformOnChatEnter();

                           manager.BroadcastUserList();

                           Log("Authentication complete: UID " + uid + " maps to username " + ThisUser.Username +
                                 (ThisUser.CanStaffChat ? "(staff)" : ""));

                           response.result = true;

                           Dictionary<int, UserInfo> currentUsers = manager.UsersForModules();

                           //Also do some other crap
                           foreach (Module module in manager.GetModuleListCopy())
                           {
                              if (Monitor.TryEnter(module.Lock, manager.ChatSettings.MaxModuleWait))
                              {
                                 try
                                 {
                                    SendModuleMessagesFromMe(module.OnUserJoin(currentUsers[ThisUser.UID], currentUsers), module);
                                 }
                                 finally
                                 {
                                    Monitor.Exit(module.Lock);
                                 }
                              }
                              else
                              {
                                 Log("Skipped " + module.ModuleName + " join processing", 
                                       MyExtensions.Logging.LogLevel.Warning);
                              }
                           }

                           //Finally, output the "Yo accept dis" thing if they haven't already.
                           if(!ThisUser.AcceptedPolicy)
                           {
                              MessageListJSONObject emptyMessages = new MessageListJSONObject();
                              UserListJSONObject emptyUsers = new UserListJSONObject();
                              ModuleJSONObject policy = new ModuleJSONObject(ChatServer.Policy);
                              ModuleJSONObject accept = new ModuleJSONObject("\nYou must accept this chat policy before " +
                                    "using the chat. Type /accept if you accept the chat policy\n");
                              MySend(emptyMessages.ToString(), true);
                              MySend(emptyUsers.ToString(), true);
                              MyHandle(policy, true);
                              MyHandle(accept, true);
                           }
                           else if(ThisUser.ShouldPolicyRemind)
                           {
                              ModuleJSONObject policy = new ModuleJSONObject(ChatServer.Policy);
                              MyHandle(policy);
                              ThisUser.PerformOnReminder();
                           }
                        }
                     }
                  }
               }
               catch(RuntimeBinderException)
               {
                  response.errors.Add("BIND message was missing fields");
               }
               catch(Exception ex)
               {
                  response.errors.Add("Internal server error while binding: " + ex.Message);
                  Log("Exception while binding: " + ex.ToString(), LogLevel.Error);
               }
            }
         }
         else if (type == "ping")
         {
            lastPing = DateTime.Now;

            bool active = true;

            try
            {
               active = (bool)json.active;
            }
            catch (Exception messageError)
            {
               response.errors.Add("Internal server error: " + messageError/*.Message*/);
            }

            ThisUser.PerformOnPing(active);
            UpdateActiveUserList(null, null);
         }
         else if (type == "message")
         {
            try
            {
               //First, gather information from the JSON. This is so that if
               //the json is invalid, it will fail as soon as possible
               string key = json.key;
               string message = (string)json.text; 
               string tag = json.tag;

               //You HAVE to do this, even though it seems pointless. Users need to show up as banned immediately.
               if(uid > 0)
               {
                  if(!ThisUser.PullInfoFromQueryPage(out warnings))
                  {
                     queryFailures++;

                     Log("User " + ThisUser.Username + " has " + queryFailures + " query failure(s)", 
                        MyExtensions.Logging.LogLevel.Warning);
                  }
                  else
                  {
                     queryFailures = 0;
                  }
               }

               //Oh and if the user's avatar was updated, we should probably broadcast the userlist.
               if((!string.IsNullOrWhiteSpace(lastAvatar) && lastAvatar != ThisUser.Avatar) ||
                  (lastBan.Ticks != 0 && lastBan != ThisUser.BannedUntil))
               {
                  if(lastAvatar != ThisUser.Avatar)
                     Log(ThisUser.Username + " updated avatar", LogLevel.Debug);
                  else if (lastBan != ThisUser.BannedUntil)
                     Log(ThisUser.Username + " ban status changed", LogLevel.Debug);
                  manager.BroadcastUserList();
               }

               lastAvatar = ThisUser.Avatar;
               lastBan = ThisUser.BannedUntil;

               //These first things don't increase spam score in any way
               if (queryFailures >= manager.ChatSettings.MaxUserQueryFailures)
               {
                  Log(ThisUser.Username + " reached the query failure limit at " + 
                        queryFailures + " failure(s)", LogLevel.Warning);

                  foreach (string warning in warnings)
                     SendFromMe(new WarningMessageJSONObject("Chat Warning: " + warning));
               }
               else if (string.IsNullOrWhiteSpace(message))
               {
                  response.errors.Add("No empty messages please");
               }
               else if (!manager.CheckKey(uid, key))
               {
                  Log("Got invalid key " + key + " from " + UserLogString);
                  response.errors.Add("Your key is invalid");
               }
               else if (!ThisUser.AcceptedPolicy)
               {
                  if (message != "/accept")
                  {
                     response.errors.Add("The only command available right now is /accept");
                  }
                  else
                  {
                     ModuleJSONObject acceptSuccess = new ModuleJSONObject("You have accepted the SmileBASIC Source " +
                                                   "chat policy. Please use the appropriate chat tab for discussion about SmileBASIC or off-topic " +
                                                   "subjects!");
                     MyHandle(acceptSuccess, true);

                     ThisUser.AcceptPolicy();
                     ThisUser.PerformOnReminder();
                     response.result = true;

                     //Send these ONLY to the user.
                     MySend(manager.ChatUserList(UID));
                     MySend(manager.ChatMessageList(UID, 20));
                  }
               }
               else if (ThisUser.Blocked)
               {
                  response.errors.Add(manager.ConvertTag(QuickParams(ChatTags.Blocked))); 
               }
               else if (ThisUser.Banned)
               {
                  response.errors.Add("You are banned from chat for " +
                  StringExtensions.LargestTime(ThisUser.BannedUntil - DateTime.Now) + ". Reason: " + ThisUser.BanReason);
               }
               else if (tag == "admin" && !ThisUser.CanStaffChat ||
                     tag == manager.ChatSettings.GlobalTag && !ThisUser.CanGlobalChat)
               {
                  response.errors.Add("You can't post messages here. I'm sorry.");
               }
               else if (!manager.ValidTagForUser(UID, tag))
               {
                  response.errors.Add("Your post has an unrecognized tag. Cannot display");
               }
               else
               {
                  Dictionary<int, UserInfo> currentUsers = manager.UsersForModules();
                  MessageJSONObject userMessage = new MessageJSONObject(message, new UserInfo(ThisUser,true), tag);
                  UserCommand userCommand;
                  Module commandModule;
                  string commandError = "";

                  //Step 1: parse a possible command. If no command is parsed, no module will be written.
                  if (TryCommandParse(userMessage, out commandModule, out userCommand, out commandError))
                  {
                     Log("Trying to use module " + commandModule.ModuleName + " to process command " +
                     userCommand.message + " from " + ThisUser.Username, MyExtensions.Logging.LogLevel.SuperDebug);

                     //We found a command. Send it off to the proper module and get the output
                     if (Monitor.TryEnter(commandModule.Lock, manager.ChatSettings.MaxModuleWait))
                     {
                        try
                        {
                           SendModuleMessagesFromMe(commandModule.ProcessCommand(userCommand, currentUsers[ThisUser.UID], currentUsers), commandModule);
                        }
                        finally
                        {
                           Monitor.Exit(commandModule.Lock);
                        }
                     }
                     else
                     {
                        response.errors.Add("The chat server is busy and can't process your command right now");
                        userMessage.SetSpammable(false);
                     }

                     //do not update spam score if command module doesn't want it
                     if (!userCommand.MatchedCommand.ShouldUpdateSpamScore)
                        userCommand.SetSpammable(false);

                     //For now, simply capture all commands no matter what.
                     userCommand.SetNoRecipients();
                     //Send the message as a command, since it was parsed.
                     SendFromMe(userCommand);

                     Log("Module " + commandModule.ModuleName + " processed command from " + UserLogString, 
                        MyExtensions.Logging.LogLevel.Debug);
                  }
                  else
                  {
                     //If an error was given, add it to our response
                     if (!string.IsNullOrWhiteSpace(commandError))
                     {
                        response.errors.Add("Command error: " + commandError);
                     }
                     else
                     {
                        SendAndProcessFromMe(userMessage);
                     }
                  }

                  response.result = response.errors.Count == 0;

                  //Now send out userlist if active status changed
                  if(!ThisUser.Hiding)
                     UpdateActiveUserList(null, null);
               }
            }
            catch (Exception messageError)
            {
               response.errors.Add("Internal server error: " + messageError);
            }
         }
         else if (type == "request")
         {
            try
            {
               string wanted = json.request;

               if(wanted == "userList")
               {
                  MySend(manager.ChatUserList(UID));
                  response.result = true;
               }
               else if (wanted == "messageList")
               {
                  //Again, send only to the user. Pump up the number so it's nice (eventually will pull number from request)
                  MySend(manager.ChatMessageList(UID, 20));
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
         MySend(response.ToString(), true);
      }
         
      private void ProcessMessage(MessageJSONObject message, Dictionary<int, UserInfo> currentUsers)
      {
         //Step 2: run regular message through all modules' regular message processor (probably no output?)
         if (manager.ChatSettings.AcceptedTags.Contains(message.tag))
         {
            foreach (Module module in manager.GetModuleListCopy().Where(x => x.DoesProcessMessage))
            {
               if (Monitor.TryEnter(module.Lock, manager.ChatSettings.MaxModuleWait))
               {
                  try
                  {
                     module.ProcessMessage(message, currentUsers[ThisUser.UID], currentUsers);
                  }
                  finally
                  {
                     Monitor.Exit(module.Lock);
                  }
               }
               else
               {
                  Log("Skipped " + module.ModuleName + " message processing", 
                     MyExtensions.Logging.LogLevel.Warning);
               }
            }
         }
      }

      private void SetFromMeIfNothing(List<MessageBaseJSONObject> messages)
      {
         foreach (MessageBaseJSONObject message in messages)
            if (!message.HasSender())
               message.sender = new UserJSONObject(new UserInfo(ThisUser, true));
      }

      private string ParseUser(string user)
      {
         if (user.StartsWith("??"))
         {
            return StringExtensions.AutoCorrectionMatch(user.Replace("??", ""), 
               manager.UsersForModules().Select(x => x.Value.Username).ToList());
         }
         else if (user.StartsWith("?"))
         {
            return StringExtensions.AutoCorrectionMatch(user.Replace("?", ""), 
               manager.UsersForModules().Where(x => x.Value.LoggedIn).Select(x => x.Value.Username).ToList());
         }
         else if (user.StartsWith("#"))
         {
            int id = 0;
            string username = "no";

            if (int.TryParse(user.Replace("#", ""), out id) &&
               manager.UsersForModules().ContainsKey(id))
            {
               username = manager.UsersForModules()[id].Username;
            }

            return username;
         }

         return user;
      }

      /// <summary>
      /// Parse and build command if possible. Assign module which will handle command. 
      /// </summary>
      /// <returns>Successful command parse</returns>
      /// <param name="message">Message.</param>
      /// <param name="commandModule">Command module.</param>
      /// <param name="userCommand">User command.</param>
      private bool TryCommandParse(MessageJSONObject message, out Module commandModule, out UserCommand userCommand, out string error)
      {
         userCommand = null;
         commandModule = null;
         error = "";

         UserCommand tempUserCommand = null;
         List<Module> modules = manager.GetModuleListCopy(UID);

         string realMessage = message.GetRawMessage(); 

         //Check through all modules for possible command match
         foreach(Module module in modules)
         {
            //We already found the module, so get out.
            if(commandModule != null)
               break;

            //Check through this module's command for possible match
            foreach(ModuleCommand command in module.Commands)
            {
               Match match = Regex.Match(realMessage, command.FullRegex, RegexOptions.Singleline);

               //This command matched, so preparse the command and get out of here.
               if (match.Success)
               {
                  //Build arguments from regex.
                  List<string> arguments = new List<string>();
                  for (int i = 2; i < match.Groups.Count; i++)
                     arguments.Add(match.Groups[i].Value.Trim());

                  //We have a user command. Cool, but will it parse? Ehhhh.
                  tempUserCommand = new UserCommand(match.Groups[1].Value, arguments, message, command);

                  //Now preprocess the command to make sure certain standard fields check out (like username)
                  for (int i = 0; i < command.Arguments.Count; i++)
                  {
                     //Users need to exist. If not, throw error.
                     if (command.Arguments[i].Type == ArgumentType.User)
                     {
                        tempUserCommand.Arguments[i] = ParseUser(tempUserCommand.Arguments[i]);

                        for (int j = 0; j < tempUserCommand.ArgumentParts[i].Count; j++)
                           tempUserCommand.ArgumentParts[i][j] = ParseUser(tempUserCommand.ArgumentParts[i][j]);

                        if (!((tempUserCommand.MatchedCommand.Arguments[i].Repeat == RepeatType.ZeroOrOne ||
                               tempUserCommand.MatchedCommand.Arguments[i].Repeat == RepeatType.ZeroOrMore) &&
                              string.IsNullOrWhiteSpace(tempUserCommand.Arguments[i])) && 
                           (tempUserCommand.ArgumentParts[i].Count == 0 && manager.UserLookup(tempUserCommand.Arguments[i]) < 0 ||
                              tempUserCommand.ArgumentParts[i].Any(x => manager.UserLookup(x) < 0)))
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

         if (commandModule == null && ModuleCommand.IsACommand(message.message))
         {
            //OOPS! A command was parsed but it wasn't parsed correctly. Doop
            error = "\"" + message.message + "\" was not recognized. Maybe something was misspelled, or you were missing arguments";
            return false;
         }

         userCommand = new UserCommand(tempUserCommand);
         return (commandModule != null);
      }
   }
      
   public class GlobalModule : Module
   {
      public GlobalModule()
      {
         Commands.AddRange(new List<ModuleCommand> {
            new ModuleCommand("about", new List<CommandArgument>(), "See information about the chat server"),
            new ModuleCommand("policy", new List<CommandArgument>(), "View the SmileBASIC Source chat policy"),
            new ModuleCommand("uactest", new List<CommandArgument> {
               new CommandArgument("shortuser", ArgumentType.User)
            }, "Test user autocorrection (use ? on username to enable shorthand)"),
            new ModuleCommand("help", new List<CommandArgument>(), "See all modules which you can get help with"),
            new ModuleCommand("help", new List<CommandArgument>() {
               new CommandArgument("module", ArgumentType.Module)
            }, "Get help about a particular module"),
            new ModuleCommand("helpregex", new List<CommandArgument>() {
               new CommandArgument("module", ArgumentType.Module)
            }, "Get help about a particular module + regex")
         });
      }

      public override List<MessageBaseJSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<MessageBaseJSONObject> outputs = new List<MessageBaseJSONObject>();

         ModuleJSONObject output = new ModuleJSONObject();

         switch (command.Command)
         {
            case "about":
               output = new ModuleJSONObject();
               BandwidthContainer bandwidth = ChatRunner.Bandwidth; //Chat.GetBandwidth();
               DateTime built = ChatRunner.MyBuildDate();
               DateTime crashed = ChatRunner.LastCrash();
               string crashedString = "never";

               if (crashed.Ticks > 0)
                  crashedString = StringExtensions.LargestTime(DateTime.Now - crashed) + " ago";
                  
               output.message = 
                  "---Build info---\n" +
                  "Version: " + ChatRunner.AssemblyVersion() + " (Lib: " + 
                     MyExtensions.MyExtensionsData.Version + ")\n" +
                  "Runtime: " + StringExtensions.LargestTime(DateTime.Now - ChatRunner.Startup) + "\n" +
                  "Built " + StringExtensions.LargestTime(DateTime.Now - built) + " ago (" + built.ToString("R") + ")\n" +
                  "---Data usage---\n" +
                  "Outgoing: " + bandwidth.GetTotalBandwidthOutgoing() + " (1h: " + bandwidth.GetHourBandwidthOutgoing() + ")\n" +
                  "Incoming: " + bandwidth.GetTotalBandwidthIncoming() + " (1h: " + bandwidth.GetHourBandwidthIncoming() + ")\n" +
                  "---Websocket---\n" +
                  "Library Version: " + ChatServer.Version + "\n" +
                  "Last full crash: " + crashedString;
               outputs.Add(output);
               break;

            case "policy":
               output = new ModuleJSONObject(ChatServer.Policy);
               outputs.Add(output);
               break;

            case "help":
               output = new ModuleJSONObject();
               if (command.Arguments.Count == 0)
               {
                  output.message = "Which module would you like help with?\n";

                  foreach (Module module in ChatRunner.Server.GetModuleListCopy(user.UID))
                     output.message += "\n" + module.Nickname;

                  output.message += "\n\nRerun help command with a module name to see commands for that module";
                  outputs.Add(output);
               }
               else
               {
                  output.message = GetModuleHelp(command.Arguments[0], user);
                  outputs.Add(output);
               }
               break;
            case "helpregex":
               output.message = GetModuleHelp(command.Arguments[0], user, true);
               outputs.Add(output);
               break;
            case "uactest":
               output = new ModuleJSONObject();
               output.message = "User " + command.OriginalArguments[0] + " corrects to " + command.Arguments[0];
               outputs.Add(output);
               break;
         }

         return outputs;
      }

      public string GetModuleHelp(string moduleString, UserInfo user, bool showRegex = false)
      {
         string message = "Help for the " + moduleString + " module:\n";

         Module module = null;

         try
         {
            module = ChatRunner.Server.GetModuleListCopy(user.UID).First(x => x.Nickname == moduleString);

            if (!string.IsNullOrWhiteSpace(module.GeneralHelp))
               message += "\n" + module.GeneralHelp + "\n";

            foreach(ModuleCommand moduleCommand in module.Commands)
               message += "\n" + moduleCommand.DisplayString + (showRegex ? " : " + moduleCommand.FullRegex : "");

            if (showRegex)
            {
               if (module.ArgumentHelp.Count > 0)
                  message += "\n\nSome argument regex (uses standard regex syntax):";

               foreach (KeyValuePair<string, string> argHelp in module.ArgumentHelp)
                  message += "\n" + argHelp.Key + " - " + argHelp.Value;
            }
         }
         catch
         {
            message += "\nThis module is hidden or does not exist.";
         }

         return message;
      }
   }
}
