using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using MyExtensions;
using System.Linq;
using MyExtensions.Logging;
using System.Diagnostics;

namespace ChatServer
{
   //Debug module is specific to the system we're on. Don't worry about the bad design choice
   //of calling a static method from within the class containing Main... it's all part of the
   //chat system. This "module" could work without being a module, but since we have the module
   //system in place anyway, there's no point writing something new. This is not reusable code
   //because the particulars of debugging are specific to the system.
   public class DebugModule : Module
   {
      public DebugModule()
      {
         Commands.Add(new ModuleCommand("spamscore", new List<CommandArgument> (), "check personal spam score"));
         Commands.Add(new ModuleCommand("resetserver", new List<CommandArgument> {
            new CommandArgument("seconds", ArgumentType.Integer, RepeatType.ZeroOrOne)
         }, "reset the server in the specified amount of time (default 5 seconds)"));
         Commands.Add(new ModuleCommand("simulatelock", new List<CommandArgument>(), "simulate a server deadlock"));
         Commands.Add(new ModuleCommand("savemodules", new List<CommandArgument>(), "save all module data now"));
         Commands.Add(new ModuleCommand("checkserver", new List<CommandArgument>(), "See important stats for server"));
         Commands.Add(new ModuleCommand("debuginfo", new List<CommandArgument>()
         { new CommandArgument("user", ArgumentType.User, RepeatType.ZeroOrOne) }, "See debug information about yourself or another user"));
         //commands.Add(new ModuleCommand("myrooms", new List<CommandArgument>(), "show pm rooms you're currently in"));
      }

      public override List<MessageBaseJSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<MessageBaseJSONObject> outputs = new List<MessageBaseJSONObject>();
         ModuleJSONObject moduleOutput = new ModuleJSONObject();

         string message = "";
         UserInfo parsedInfo;

         try
         {
            switch(command.Command)
            {
               case "spamscore":
                  moduleOutput.message = "Your spam score is: " + user.SpamScore + ", offense score: " + user.GlobalSpamScore;
                  outputs.Add(moduleOutput);
                  break;

               case "resetserver":
                  int timeout = 5;

                  //Get the real timeout if one was given
                  if (command.Arguments.Count > 0 && !string.IsNullOrWhiteSpace(command.Arguments[0]))
                     timeout = int.Parse(command.Arguments[0]);

                  TimeSpan realTimeout = TimeSpan.FromSeconds(timeout);

                  //Make sure the user can even run such a high command
                  if(!user.ChatControl)
                     return FastMessage("You don't have access to this command!", true); 

                  //Request the reset we wanted
                  ChatRunner.PerformRequest(new SystemRequest(SystemRequests.Reset, realTimeout));

                  moduleOutput.message = user.Username + " is resetting the server in " + StringExtensions.LargestTime(realTimeout);
                  moduleOutput.sendtype = MessageBaseSendType.Broadcast;
                  //moduleOutput.broadcast = true;
                  outputs.Add(moduleOutput);

                  break;

               case "simulatelock":

                  //Make sure the user can even run such a high command
                  if(!user.ChatControlExtended)
                     return FastMessage("You don't have access to this command!", true); 
                  
                  //Request the lock we wanted
                  ChatRunner.PerformRequest(new SystemRequest(SystemRequests.LockDeath, TimeSpan.FromSeconds(5)));

                  moduleOutput.message = user.Username + " is simulating a server crash. The server WILL be unresponsive if successful";
                  moduleOutput.sendtype = MessageBaseSendType.Broadcast;
                  //moduleOutput.broadcast = true;
                  outputs.Add(moduleOutput);

                  break;

               case "savemodules":

                  //Make sure the user can even run such a high command
                  if(!user.ChatControlExtended)
                     return FastMessage("You don't have access to this command!", true); 

                  ChatRunner.PerformRequest(new SystemRequest(SystemRequests.SaveModules));

                  moduleOutput.message = user.Username + " is saving all module data. This may cause a small hiccup";
                  moduleOutput.sendtype = MessageBaseSendType.Broadcast;
                  //moduleOutput.broadcast = true;
                  outputs.Add(moduleOutput);

                  break;

               case "checkserver":
                  
                  //Make sure the user can even run such a high command
                  if(!user.ChatControl)
                     return FastMessage("You don't have access to this command!", true); 

                  message = "Information about the chat server:\n\n";

                  Dictionary<string, List<MessageBaseJSONObject>> history = ChatRunner.Server.GetHistory();
                  //List<MessageBaseJSONObject> messages = ChatRunner.Server.GetMessages();
                  List<LogMessage> logMessages = ChatRunner.Server.Settings.LogProvider.GetMessages();
                  List<LogMessage> logFileBuffer = ChatRunner.Server.Settings.LogProvider.GetFileBuffer();
                  List<Module> modules = ChatRunner.Server.GetModuleListCopy();

                  message += "Rooms: " + history.Keys.Count + "\n";
                  message += "History messages: " + history.Sum(x => x.Value.Count) + "\n";
                  message += "Registered users: " + users.Count + "\n";
                  message += "Total user sessions: " + users.Sum(x => (long)x.Value.SessionCount) + "\n";
                  //message += "Stored messages: " + messages.Count + "\n";
                  message += "Stored log: " + logMessages.Count + "\n";
                  message += "Log file buffer: " + logFileBuffer.Count + "\n";
                  message += "Modules loaded: " + string.Join(", ", modules.Select(x => x.GetType().Name)) + " (" + modules.Count + ")\n";
                  message += "Subscribed handlers for extra output: " + this.ExtraCommandHandlerCount + "\n";
                  message += "Registered connections: " + ChatRunner.Server.ConnectedUsers().Count + "\n";

                  using(Process process = Process.GetCurrentProcess())
                  {
                     message += "Virtual Memory: " + (process.PrivateMemorySize64 / 1048576) + "MiB\n";
                     message += "Heap Allocated: " + (GC.GetTotalMemory(true) / 1048576) + "MiB\n"; 
                     message += "Threads: " + process.Threads.Count;
                  }

                  moduleOutput.message = message;
                  outputs.Add(moduleOutput);

                  break;

               case "debuginfo":

                  //if we can't parse the user, use yourself. Otherwise if it parsed but they don't have
                  //access to this command, stop and let them know.
                  if(string.IsNullOrWhiteSpace(command.Arguments[0]) || !GetUserFromArgument(command.Arguments[0], users, out parsedInfo))
                     parsedInfo = user;
                  else if(!user.ChatControl && parsedInfo.UID != user.UID)
                     return FastMessage("You don't have access to this command!", true); 

                  message = parsedInfo.Username + "'s debug information: \n\n";

                  message += "Session ID: " + parsedInfo.LastSessionID + "\n";
                  message += "Open sessions: " + parsedInfo.OpenSessionCount + "\n";
                  message += "Bad sessions: " + parsedInfo.BadSessionCount + "\n";
                  message += "Current session time: " + StringExtensions.LargestTime(parsedInfo.CurrentSessionTime) + "\n";
                  message += "UID: " + parsedInfo.UID + "\n";
                  message += "Active: " + parsedInfo.Active + "\n";
                  message += "Last ping: " + StringExtensions.LargestTime(DateTime.Now - parsedInfo.LastPing) + "\n";
                  message += "Last post: " + StringExtensions.LargestTime(DateTime.Now - parsedInfo.LastPost) + "\n";
                  message += "Last entry: " + StringExtensions.LargestTime(DateTime.Now - parsedInfo.LastJoin) + "\n";
                  message += "Staff chat: " + parsedInfo.CanStaffChat + "\n";
                  message += "Global chat: " + parsedInfo.CanGlobalChat + "\n";
                  message += "Chat control: " + parsedInfo.ChatControl + "\n";
                  message += "Chat control extended: " + parsedInfo.ChatControlExtended + "\n";
                  message += "Avatar: " + user.Avatar + "\n";

                  moduleOutput.message = message;
                  outputs.Add(moduleOutput);

                  break;
            }
         }
         catch (Exception e)
         {
            return FastMessage("An error has occurred in the debug module: " + e.Message, true);
         }

         return outputs;
      }
   }
}