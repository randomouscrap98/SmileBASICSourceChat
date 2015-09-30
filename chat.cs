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
   public delegate void ChatEventHandler(object source, EventArgs e);

   //This is the thing that will get passed as the... controller to Websockets?
   public class Chat : WebSocketBehavior
   {
      public const int HeaderSize = 64;
      private int uid = -1;

      private readonly System.Timers.Timer userUpdateTimer = new System.Timers.Timer();
      private readonly ChatManager manager;

      //public event ChatEventHandler OnUserListChange;

      //Set up the logger when building the chat provider. Logging will go out to a file and the console
      //if possible. Otherwise, log to the default logger (which is like throwing them away)
      public Chat(int userUpdateInterval, ChatManager manager)
      {
         userUpdateTimer.Interval = userUpdateInterval * 1000;
         userUpdateTimer.Elapsed += UpdateActiveUserList;
         userUpdateTimer.Start();

         //Assume the manager that was given was good
         this.manager = manager;

         foreach (Module module in manager.GetModuleListCopy())
            module.OnExtraCommandOutput += DefaultOutputMessages;
      }

      public MyExtensions.Logging.Logger Logger
      {
         get { return manager.Logger; }
      }

      //This should be the ONLY place where the active state changes
      private void UpdateActiveUserList(object source, System.Timers.ElapsedEventArgs e)
      {
         if (ThisUser.StatusChanged)
         {
            ThisUser.SaveActiveState();
            manager.BroadcastUserList();
            Logger.LogGeneral(UserLogString + " became " + (ThisUser.Active ? "active" : "inactive"), MyExtensions.Logging.LogLevel.Debug);
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

      public void MySend(string message)
      {
         if (string.IsNullOrEmpty(message))
            return;
         
         manager.Bandwidth.AddOutgoing(message.Length + HeaderSize);

         try
         {
            Send(message);
         }
         catch (Exception e)
         {
            Logger.Warning("Cannot send message: " + message + " to user: " + UserLogString + " because: " + e);
         }
      }

      protected override void OnOpen()
      {
         //I don't know what we're doing for OnOpen yet.
      }

      //On closure of the websocket, remove ourselves from the list of active
      //chatters.
      protected override void OnClose(CloseEventArgs e)
      {
         Logger.Log ("Session disconnect: " + uid);

         bool authenticated = (uid > 0);
         string username = ThisUser.Username;

         manager.LeaveChat(this);

         if (authenticated)
         {
            if (!ThisUser.PerformOnChatLeave())
               Logger.Warning("User session timer is in an invalid state!");
            
            manager.Broadcast((new SystemMessageJSONObject() { message = username + " has left the chat." }).ToString());
         }
      }

      protected override void OnError(ErrorEventArgs e)
      {
         Logger.Error("UID: " + uid + " - " + e.Message, "WebSocket");
         Logger.Error(e.Exception.ToString(), "WebSocket");
         //base.OnError(e);
      }

      //I guess this is WHENEVER it receives a message?
      protected override void OnMessage(MessageEventArgs e)
      {
         ResponseJSONObject response = new ResponseJSONObject();
         response.result = false;
         dynamic json = new Object();
         string type = "";

         //You HAVE to do this, even though it seems pointless. Users need to show up as banned immediately.
         ThisUser.PullInfoFromQueryPage();

         //Before anything else, log the amount of incoming data
         if (!string.IsNullOrEmpty(e.Data))
         {
            manager.Bandwidth.AddIncoming(e.Data.Length + HeaderSize);
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
            if (uid > 0)
            {
               response.errors.Add("Received another bind message, but you've already been authenticated.");
            }
            else
            {
               try
               {
                  //First, gather information from the JSON. This is so that if
                  //the json is invalid, it will fail as soon as possible
                  string key = (string)json.key;
                  int newUser = (int)json.uid;

                  //Oops, username was invalid
                  if (newUser <= 0)
                  {
                     Logger.Log("Tried to bind a bad UID: " + newUser);
                     response.errors.Add("UID was invalid");
                  }
                  else
                  {
                     List<Chat> removals;
                     string error;

                     if (!manager.Authenticate(this, newUser, key, out removals, out error))
                     {
                        response.errors.Add(error);
                     }
                     else
                     {
                        //Before we do anything, remove other chatting sessions
                        foreach (Chat removeChat in removals)
                           Sessions.CloseSession(removeChat.ID);
                     
                        uid = newUser;

                        List<Chat> exclusion = new List<Chat>();
                        exclusion.Add(this);

                        //BEFORE adding, broadcast the "whatever has entered the chat" message
                        manager.Broadcast((new SystemMessageJSONObject() { 
                           message = ThisUser.Username + " has entered the chat."
                        }).ToString(),
                           exclusion);

                        //BEFORE sending out the user list, we need to perform onPing so that it looks like this user is active
                        if (!ThisUser.PerformOnChatEnter())
                           Logger.Warning("Invalid session entry. Sessions may be broken");

                        manager.BroadcastUserList();

                        Logger.Log("Authentication complete: UID " + uid + " maps to username " + ThisUser.Username
                        + (ThisUser.CanStaffChat ? "(staff)" : ""));
                        response.result = true;

                        List<JSONObject> outputs = new List<JSONObject>();
                        Dictionary<int, UserInfo> currentUsers = manager.UsersForModules();

                        //Also do some other crap
                        foreach (Module module in manager.GetModuleListCopy())
                        {
                           if (Monitor.TryEnter(module.Lock, TimeSpan.FromSeconds(manager.MaxModuleWaitSeconds)))
                           {
                              try
                              {
                                 outputs.AddRange(module.OnUserJoin(currentUsers[ThisUser.UID], currentUsers));
                              }
                              finally
                              {
                                 Monitor.Exit(module.Lock);
                              }
                           }
                           else
                           {
                              Logger.LogGeneral("Skipped " + module.ModuleName + " join processing", 
                                 MyExtensions.Logging.LogLevel.Warning);
                           }
                        }

                        OutputMessages(outputs, ThisUser.UID);
                     }
                  }
               }
               catch
               {
                  response.errors.Add("BIND message was missing fields");
               }
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

               //These first things don't increase spam score in any way
               if(string.IsNullOrWhiteSpace(message))
               {
                  response.errors.Add("No empty messages please");
               }
               else if(!manager.CheckKey(uid, key))
               {
                  Logger.LogGeneral("Got invalid key " + key + " from " + UserLogString);
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
               else if (!manager.AllAcceptedTags.Contains(tag))
               {
                  response.errors.Add("Your post has an unrecognized tag. Cannot display");
               }
               else
               {
                  //List<int> loggedIn = manager.LoggedInUsers().Keys();
                  Dictionary<int, UserInfo> currentUsers = manager.UsersForModules();
                  //Dictionary<int, User> currentUsers = manager.LoggedInUsers();
                  List<JSONObject> outputs = new List<JSONObject>();
                  UserMessageJSONObject userMessage = new UserMessageJSONObject(ThisUser, message, tag);
                  UserCommand userCommand;
                  Module commandModule;
                  string commandError = "";

                  //Step 1: parse a possible command. If no command is parsed, no module will be written.
                  if(TryCommandParse(userMessage, out commandModule, out userCommand, out commandError))
                  {
                     //We found a command. Send it off to the proper module and get the output
                     if(Monitor.TryEnter(commandModule.Lock, TimeSpan.FromSeconds(manager.MaxModuleWaitSeconds)))
                     {
                        try
                        {
                           outputs.AddRange(commandModule.ProcessCommand(userCommand, currentUsers[ThisUser.UID], currentUsers));
                        }
                        finally
                        {
                           Monitor.Exit(commandModule.Lock);
                        }
                     }
                     else
                     {
                        response.errors.Add("The chat server is busy and can't process your command right now");
                        userMessage.SetHidden();
                        userMessage.SetUnspammable();
                     }

                     //do not update spam score if command module doesn't want it
                     if(!userCommand.MatchedCommand.ShouldUpdateSpamScore)
                        userMessage.SetUnspammable();

                     //For now, simply capture all commands no matter what.
                     userMessage.SetHidden();

                     Logger.LogGeneral("Module " + commandModule.ModuleName + " processed command from " + UserLogString, 
                        MyExtensions.Logging.LogLevel.Debug);
                  }
                  else
                  {
                     //If an error was given, add it to our response
                     if(!string.IsNullOrWhiteSpace(commandError))
                     {
                        response.errors.Add("Command error: " + commandError);
                        userMessage.SetHidden();
                        userMessage.SetUnspammable();
                     }
                  }

                  WarningJSONObject warning = manager.SendMessage(userMessage);

                  if(warning != null)
                     outputs.Add(warning);
                  
                  response.result = response.errors.Count == 0;

                  //Now send out userlist if active status changed
                  UpdateActiveUserList(null, null);

                  //Since we added a new message, we need to broadcast.
                  if(response.result && userMessage.Display)
                     manager.BroadcastMessageList();

                  //Step 2: run regular message through all modules' regular message processor (probably no output?)
                  foreach(Module module in manager.GetModuleListCopy())
                  {
                     if(Monitor.TryEnter(module.Lock, TimeSpan.FromSeconds(manager.MaxModuleWaitSeconds)))
                     {
                        try
                        {
                           module.ProcessMessage(userMessage, currentUsers[ThisUser.UID], currentUsers);
                        }
                        finally
                        {
                           Monitor.Exit(module.Lock);
                        }
                     }
                     else
                     {
                        Logger.LogGeneral("Skipped " + module.ModuleName + " message processing", 
                           MyExtensions.Logging.LogLevel.Warning);
                     }
                  }

                  //Step 3: run all modules' post processor (no message required)
                  //Is this even necessary? It was necessary before because the bot ran on a timer. Without a timer,
                  //each module can just specify that it wants to do things at random points with its own timer.

                  //Step 4: iterate over returned messages and send them out appropriately
                  OutputMessages(outputs, ThisUser.UID, tag);
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
                  MySend(manager.ChatUserList());
                  response.result = true;
               }
               else if (wanted == "messageList")
               {
                  MySend(manager.ChatMessageList());
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

         Logger.LogGeneral ("Got message: " + e.Data, MyExtensions.Logging.LogLevel.Debug);
      }
         
      private void OutputMessages(List<JSONObject> outputs, int receiverUID, string defaultTag = "")
      {
         //We're not the ones you're looking for...
         if (ThisUser.UID != receiverUID)
            return;
         
         if (!manager.AllAcceptedTags.Contains(defaultTag))
            defaultTag = manager.GlobalTag;
         
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
                  tempJSON.tag = defaultTag;

               if(tempJSON.broadcast)
               { 
                  manager.Broadcast(tempJSON.ToString());
                  Logger.LogGeneral("Broadcast a module message", MyExtensions.Logging.LogLevel.Debug);
               }
               else
               {
                  //No recipients? You probably meant to send it to the current user.
                  if(tempJSON.recipients.Count == 0)
                     MySend(tempJSON.ToString());
                  else
                     manager.SendMessage(tempJSON);
               }
            }
         }
      }

      private void DefaultOutputMessages(List<JSONObject> outputs, int receiverUID)
      {
         OutputMessages(outputs, receiverUID);
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
         List<Module> modules = manager.GetModuleListCopy();

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
                        if (tempUserCommand.Arguments[i].StartsWith("??"))
                        {
                           tempUserCommand.Arguments[i] = StringExtensions.AutoCorrectionMatch(
                              tempUserCommand.Arguments[i].Replace("??", ""), 
                              manager.UsersForModules().Select(x => x.Value.Username).ToList());
                        }
                        else if (tempUserCommand.Arguments[i].StartsWith("?"))
                        {
                           tempUserCommand.Arguments[i] = StringExtensions.AutoCorrectionMatch(
                              tempUserCommand.Arguments[i].Replace("?", ""), 
                              manager.UsersForModules().Where(x => x.Value.LoggedIn).Select(x => x.Value.Username).ToList());
                        }

                        if (manager.UserLookup(tempUserCommand.Arguments[i]) < 0)
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

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();

         ModuleJSONObject output = new ModuleJSONObject();

         switch (command.Command)
         {
            case "about":
               output = new ModuleJSONObject();
               BandwidthContainer bandwidth = ChatRunner.Bandwidth; //Chat.GetBandwidth();
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
                  output.message = "Help for the " + command.Arguments[0] + " module:\n";

                  Module module = ChatRunner.ActiveModules.First(x => x.Nickname == command.Arguments[0]);

                  if (!string.IsNullOrWhiteSpace(module.GeneralHelp))
                     output.message += "\n" + module.GeneralHelp + "\n";

                  foreach(ModuleCommand moduleCommand in module.Commands)
                     output.message += "\n" + moduleCommand.DisplayString;

                  if(module.ArgumentHelp.Count > 0)
                     output.message += "\n\nSome argument regex (uses standard regex syntax):";

                  foreach (KeyValuePair<string, string> argHelp in module.ArgumentHelp)
                     output.message += "\n" + argHelp.Key + " - " + argHelp.Value;

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
