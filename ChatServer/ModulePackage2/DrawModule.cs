using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Linq;
using System.IO;

namespace ModulePackage2
{
   public class DrawModule: ModuleSystem.Module
   {
      private class DrawingInfo
      {
         public UserInfo User = null;
         public string Drawing = "";
         public DateTime postTime = new DateTime(0);
      }

      private List<DrawingInfo> unsavedMessages = new List<DrawingInfo>();

      public DrawModule()
      {
         Commands.Add(new ModuleCommand("drawsubmit", new List<CommandArgument> {
            new CommandArgument("encodedDrawing", ArgumentType.Word)
         }, "Submit an encoded drawing.", false));
      }

      public override string Nickname
      {
         get { return "draw"; }
      }

      public override bool SaveFiles()
      {
         bool result = true;
         Dictionary<string, List<DrawingInfo>> messageByDate = new Dictionary<string, List<DrawingInfo>>();

         foreach (DrawingInfo message in unsavedMessages)
         {
            string date = message.postTime.ToString("yy-MM-dd");
            if (!messageByDate.ContainsKey(date))
               messageByDate.Add(date, new List<DrawingInfo>());
            messageByDate[date].Add(message);
         }

         try
         {
            foreach(string date in messageByDate.Keys)
            {
               File.AppendAllLines(date + ".txt", messageByDate[date].Select(x => 
                  x.User.Username.PadLeft(20) + x.postTime.ToString("[HH:mm]") + ": " + 
                  String.Join("\n" + new String(' ', 30), x.Drawing.Split("\n".ToCharArray()))
                  ));
            }
         }
         catch(Exception e)
         {
            Log("Save error: " + e.Message, MyExtensions.Logging.LogLevel.Error);
            result = false;
         }
         finally
         {
            unsavedMessages.Clear();
         }

         return result;
      }

      public override List<MessageBaseJSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<MessageBaseJSONObject> outputs = new List<MessageBaseJSONObject>();

         if (command.Command == "drawsubmit")
         {
            //Don't save it if it's a pm drawing. This is VERY bad coding!
            if(!command.tag.StartsWith("room_"))
               unsavedMessages.Add(new DrawingInfo() { Drawing = command.Arguments[0], User = user, postTime = DateTime.Now });
            MessageJSONObject drawingMessage = new MessageJSONObject(command.Arguments[0], user, command.tag);
            drawingMessage.encoding = Nickname;
            drawingMessage.spamvalue = 0.50;
            drawingMessage.SetSpammable(false);
            outputs.Add(drawingMessage);
         }

         return outputs; //base.ProcessCommand(command, user, users);
      }
   }
}

