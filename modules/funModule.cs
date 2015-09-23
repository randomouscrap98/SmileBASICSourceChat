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
         commands.Add(new ModuleCommand("me", new List<CommandArgument> { 
            new CommandArgument("message", ArgumentType.FullString)
         }, "be silly", true));
         commands.Add(new ModuleCommand("emotes", new List<CommandArgument>(), "See all available emotes"));

         AddOptions(new Dictionary<string, object>{{"emoteLink", "http://development.smilebasicsource.com/emotes.json"}});
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, User user, Dictionary<int, User> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject moduleOutput;

         switch(command.Command)
         {
            case "me":
               moduleOutput = new ModuleJSONObject();
               moduleOutput.broadcast = true;
               moduleOutput.message = user.Username + " " + command.Arguments[0];
               outputs.Add(moduleOutput);
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