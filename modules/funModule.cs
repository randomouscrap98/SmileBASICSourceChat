using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Net;
using Newtonsoft.Json;

namespace ModulePackage1
{
   public class FunModule : Module
   {
      public FunModule()
      {
         Commands.Add(new ModuleCommand("me", new List<CommandArgument> { 
            new CommandArgument("message", ArgumentType.FullString)
         }, "be silly", true));
         Commands.Add(new ModuleCommand("code", new List<CommandArgument> { 
            new CommandArgument("message", ArgumentType.FullString)
         }, "output formatted code", true));
         Commands.Add(new ModuleCommand("emotes", new List<CommandArgument>(), "See all available emotes"));

         AddOptions(new Dictionary<string, object>{{"emoteLink", "http://development.smilebasicsource.com/emotes.json"}});
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject moduleOutput;

         switch(command.Command)
         {
            case "me":
               moduleOutput = new ModuleJSONObject();
               moduleOutput.broadcast = true;
               moduleOutput.message = user.Username + " " + command.Arguments[0]; //System.Security.SecurityElement.Escape(command.Arguments[0]);
               moduleOutput.tag = command.tag;
               outputs.Add(moduleOutput);
               break;

            case "code":
               UserMessageJSONObject codeMesssage = new UserMessageJSONObject(user, 
                     System.Security.SecurityElement.Escape(command.Arguments[0]), command.tag);
               codeMesssage.encoding = "code";
               codeMesssage.spamValue = 0.30;
               codeMesssage.SetUnspammable();
               outputs.Add(codeMesssage);
               break;

            case "emotes":
               moduleOutput = new ModuleJSONObject();
               moduleOutput.message = "Emote list:\n";
             
               try
               {
                  string htmlCode;

                  using (WebClient client = new WebClient())
                  {
                     htmlCode = client.DownloadString(GetOption<string>("emoteLink"));
                  }

                  EmoteJSONObject emotes = JsonConvert.DeserializeObject<EmoteJSONObject>(htmlCode);

                  foreach(string emote in emotes.mapping.Keys)
                     moduleOutput.message += "\n" + emotes.format.Replace("emote", emote);
               }
               catch (Exception e)
               {
                  moduleOutput.message = "Sorry, could not retrieve emote list!\nException: " + e.ToString();
               }

               outputs.Add(moduleOutput);
               break;
         }

         return outputs;
      }

      //The format of the emote JSON object returned from the webserver
      public class EmoteJSONObject
      {
         public string format = "";
         public string location = "";
         public Dictionary<string, string> mapping = new Dictionary<string, string>();
      }
   }
}
