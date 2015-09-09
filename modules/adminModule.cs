using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Text.RegularExpressions;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace ModulePackage1
{
   public class AdminModule : Module
   {
      public AdminModule()
      {
         //AddOptions(new Dictionary<string, object>{ {"enabled", true} });

         commands.Add(new ModuleCommand("ban", new List<CommandArgument> { 
            new CommandArgument("user", ArgumentType.User),
            new CommandArgument("time", ArgumentType.Custom, @"[0-9]+[hd]")
         }, "Ban a user for the given time (ex: /ban ?random 5h)"));
         commands.Add(new ModuleCommand("unban", new List<CommandArgument> { 
            new CommandArgument("user", ArgumentType.User)
         }, "Remove a user's ban"));
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, User user, Dictionary<string, User> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject moduleOutput;

         switch(command.Command)
         {
            case "ban":
               moduleOutput = new ModuleJSONObject();
               int hours = 0;

               Match match = Regex.Match(command.Arguments[1], @"([0-9]+)([hd])");

               if (!match.Success)
               {
                  moduleOutput.message = "An internal error occurred: time regex did not match command regex";
                  outputs.Add(moduleOutput);
                  break;
               }
               else if (!int.TryParse(match.Groups[1].Value, out hours))
               {
                  moduleOutput.message = "An internal error occurred: time value too big";
                  outputs.Add(moduleOutput);
                  break;
               }

               hours *= (match.Groups[2].Value == "d" ? 24 : 1);

               string banError;

               if (!TryBanQuery(new Dictionary<string, string> {
                  { "username", command.username },
                  { "time", hours.ToString() } }, out banError))
               {
                  moduleOutput.message = banError;
                  outputs.Add(moduleOutput);
                  break;
               }

               moduleOutput.message = command.username + " " + command.Arguments[0];
               outputs.Add(moduleOutput);
               break;
         }

         return outputs;
      }

      private bool TryBanQuery(Dictionary<string, string> queryParameters, out string error)
      {
         string banResult = "";
         error = "";
         bool result = false;

         using (WebClient client = new WebClient())
         {
            System.Collections.Specialized.NameValueCollection reqparm = new System.Collections.Specialized.NameValueCollection();

            foreach(var queryParameter in queryParameters)
               reqparm.Add(queryParameter.Key, queryParameter.Value);

            byte[] responsebytes = client.UploadValues("http://development.smilebasicsource.com/query/ban.php", "POST", reqparm);
            banResult = Encoding.UTF8.GetString(responsebytes);
         }

         try
         {
            dynamic json = JsonConvert.DeserializeObject(banResult);
            result = json.result;

            foreach(string jsonError in json.errors)
               error += "Ban Error: " + jsonError + "\n";
         }
         catch
         {
            error = "An internal error has occurred: The ban page returned an invalid object";
            return false;
         }

         return result;
      }
   }
}