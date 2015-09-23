using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading;

namespace ChatEssentials
{
   public abstract class JSONObject
   {
      public readonly string type;

      public JSONObject(string type)
      {
         this.type = type;
      }

      public override string ToString ()
      {
         JsonSerializerSettings settings = new JsonSerializerSettings();
         settings.StringEscapeHandling = StringEscapeHandling.EscapeHtml;
         return JsonConvert.SerializeObject(this, settings);
      }
   }

   //Responses from the server should have this format.
   public class ResponseJSONObject : JSONObject
   {
      public ResponseJSONObject() : base("response") {}
      public string from = "unknown";
      public bool result = false;
      public List<string> errors = new List<string>();
   }

   //Warnings to be sent to (usually one) users
   public class WarningJSONObject : JSONObject
   {
      public WarningJSONObject() : base("warning") {}
      public string warning = "";
   }

   //System messages have a similar format to warnings.
   public class SystemMessageJSONObject : JSONObject
   {
      public SystemMessageJSONObject() : base("system") {}
      public string message = "";
   }

   //The list of messages sent out in JSON should follow this format
   public class MessageListJSONObject : JSONObject
   {
      public MessageListJSONObject() : base("messageList") {}
      public List<Message> messages = new List<Message>();
   }

   //Sending out a list of users should follow this format
   public class UserListJSONObject : JSONObject
   {
      public UserListJSONObject() : base("userList") {}
      public List<UserJSONObject> users = new List<UserJSONObject>();
   }

   //A single user within the UserList JSON object
   public class UserJSONObject
   {
      public readonly int uid = 0;
      public bool active = false;

      public UserJSONObject(int uid, bool active)
      {
         this.uid = uid;
         this.active = active;
      }
   }

   public class ModuleJSONObject : JSONObject
   {
      public ModuleJSONObject() : base("module") {}
      public string message = "";
      public int uid = 0;
      public bool broadcast = false;
      public string tag = "";
      public List<int> recipients = new List<int>();
   }


   //A message. It SHOULD be readonly, honestly.
   public class Message
   {
      public readonly int uid;
      public readonly string text;
      public readonly long id;
      public readonly string tag;
      private readonly DateTime postTime;
      private static long NextID = 0;

      private bool display = true;

      public Message(int uid, string message, string tag)
      {
         this.uid = uid;
         this.text = message;
         this.tag = tag;
         this.postTime = DateTime.Now;
         this.id = Interlocked.Increment(ref NextID);
      }

      public Message(Message copy)
      {
         if (copy != null)
         {
            uid = copy.uid;
            text = copy.text;
            id = copy.id;
            tag = copy.tag;
            postTime = copy.postTime;
         }
      }

      public string time
      {
         get { return postTime.ToString() + " UTC"; }
      }

      //This is ONLY because we don't want to serialize it.
      public DateTime PostTime()
      {
         return postTime;
      }

      public void SetHidden()
      {
         display = false;
      }

      public void SetVisible()
      {
         display = true;
      }

      public bool Display
      {
         get { return display; }
      }
   }
}