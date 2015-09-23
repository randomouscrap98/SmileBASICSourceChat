using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;

namespace ModulePackage1
{
   public class DebugModule : Module
   {
      public DebugModule()
      {
         commands.Add(new ModuleCommand("spamscore", new List<CommandArgument> (), "check personal spam score"));
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, User user, Dictionary<int, User> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();

         switch(command.Command)
         {
            case "spamscore":
               ModuleJSONObject moduleOutput = new ModuleJSONObject();
               moduleOutput.message = "Your spam score is: " + user.SpamScore + ", offense score: " + user.GlobalSpamScore;
               outputs.Add(moduleOutput);
               break;
         }

         return outputs;
      }
   }
}