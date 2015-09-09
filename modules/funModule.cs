using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;

namespace ModulePackage1
{
   public class FunModule : Module
   {
      public FunModule()
      {
         commands.Add(new ModuleCommand("me", new List<CommandArgument> { 
            new CommandArgument("message", ArgumentType.FullString)
         }, "be silly", true));
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, User user, Dictionary<string, User> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();

         switch(command.Command)
         {
            case "me":
               ModuleJSONObject moduleOutput = new ModuleJSONObject();
               moduleOutput.broadcast = true;
               moduleOutput.message = command.username + " " + command.Arguments[0];
               outputs.Add(moduleOutput);
               break;
         }

         return outputs;
      }
   }
}