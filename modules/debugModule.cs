using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using MyExtensions;

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
         commands.Add(new ModuleCommand("spamscore", new List<CommandArgument> (), "check personal spam score"));
         commands.Add(new ModuleCommand("resetserver", new List<CommandArgument> {
            new CommandArgument("seconds", ArgumentType.Integer, RepeatType.ZeroOrOne)
         }, "reset the server in the specified amount of time (default 5 seconds)"));
         commands.Add(new ModuleCommand("simulatelock", new List<CommandArgument>(), "simulate a server deadlock"));
         //commands.Add(new ModuleCommand("myrooms", new List<CommandArgument>(), "show pm rooms you're currently in"));
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject moduleOutput = new ModuleJSONObject();

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
                  moduleOutput.broadcast = true;
                  outputs.Add(moduleOutput);

                  break;

               case "simulatelock":

                  //Make sure the user can even run such a high command
                  if(!user.ChatControlExtended)
                     return FastMessage("You don't have access to this command!", true); 
                  
                  //Request the lock we wanted
                  ChatRunner.PerformRequest(new SystemRequest(SystemRequests.LockDeath, TimeSpan.FromSeconds(5)));

                  moduleOutput.message = user.Username + " is simulating a server crash. The server WILL be unresponsive if successful";
                  moduleOutput.broadcast = true;
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