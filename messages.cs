using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading;
using System.Linq;

namespace ChatEssentials
{

   /// <summary>
   /// A generic object sent from the server. All things sent from the server must have at 
   /// least an ID and a type. 
   /// </summary>
   [Serializable]
   public abstract class JSONObject
   {
      private static long LastID = DateTime.Now.Ticks;
      private static readonly Object IDLock = new Object();

      /// <summary>
      /// Use this if you need to reorder messages; the ID is automatically generated
      /// based on the last JSON message created.
      /// </summary>
      public void SetIDNow()
      {
         lock (IDLock)
         {
            id = DateTime.Now.Ticks;

            if (id <= LastID)
               id = LastID + 1;

            LastID = id;
         }
      }

      public readonly string type;
      public long id = 0;

      public JSONObject(string type)
      {
         this.type = type;
         SetIDNow();
      }

      public JSONObject(JSONObject copy)
      {
         if (copy != null)
         {
            type = copy.type;
            id = copy.id;
         }
      }

      public override string ToString ()
      {
         JsonSerializerSettings settings = new JsonSerializerSettings();
         settings.StringEscapeHandling = StringEscapeHandling.EscapeHtml;
         return JsonConvert.SerializeObject(this, settings);
      }
   }

   public enum MessageBaseSendType
   {
      OnlyRecipients,
      IncludeSender,
      BroadcastExceptSender,
      Broadcast
   }

   /// <summary>
   /// Any JSON sent from the server that contains a message should derive from this class.
   /// </summary>
   public class MessageBaseJSONObject : JSONObject
   {
      public const string DefaultTag = "none";
      public const string DefaultEncoding = "text";
      public const string DefaultSubtype = "";

      public string tag;
      public string encoding;
      public string subtype;
      public bool safe;
      public UserJSONObject sender;
      protected string rawmessage;
      protected DateTime rawtime;
      protected DateTime expiration;

      /// <summary>
      /// Determines how the message will be sent. Defaults to recipients only
      /// </summary>
      public MessageBaseSendType sendtype;

      /// <summary>
      /// Set the recipients of this message. If the sendtype is "Broadcast", this 
      /// field is completely ignored. If sendtype is "IncludeSender", the sender 
      /// will be included in the list of recipients.
      /// </summary>
      public List<int> recipients;

      public string message
      {
         get
         {
            if (safe)
               return System.Security.SecurityElement.Escape(rawmessage); 
            else
               return rawmessage;
         }
         set
         {
            rawmessage = value;
         }
      }

      public string time
      {
         get { return rawtime.ToString("o"); }// + " UTC"; }
         set { }
      }

      public DateTime GetCreationTime()
      {
         return rawtime;
      }

      public string GetRawMessage()
      {
         return rawmessage;
      }

      public List<int> RealRecipientList(List<int> allUsers)
      {
         if (sendtype == MessageBaseSendType.Broadcast)
         {
            return new List<int>(allUsers);
         }
         else if (sendtype == MessageBaseSendType.BroadcastExceptSender)
         {
            if(HasSender())
               return allUsers.Except(new[] { sender.uid }).ToList();
            else
               return new List<int>(allUsers);
         }
         else if (sendtype == MessageBaseSendType.OnlyRecipients)
         {
            return new List<int>(recipients);
         }
         else if (sendtype == MessageBaseSendType.IncludeSender)
         {
            //Only include the sender if they're valid and they're not already in the recipient list.
            if (HasSender() && !recipients.Contains(sender.uid))
               return recipients.Concat(new[] { sender.uid }).ToList();
            else
               return new List<int>(recipients);
         }
         else
         {
            //Send to nobody if you can't make up your goddamn mind
            return new List<int>();
         }
      }

      public void SetNoRecipients()
      {
         recipients = new List<int>();
         sendtype = MessageBaseSendType.OnlyRecipients;
      }

      public bool IsSendable()
      {
         return RealRecipientList(new List<int>(){ 1 }).Count > 0;
      }

      public bool HasSender()
      {
         return sender != null && sender.uid > 0;
      }

      public bool HasExpired()
      {
         return DateTime.Now > expiration;
      }

      public MessageBaseJSONObject() : this("none") {} 
      public MessageBaseJSONObject(string type, string message = "", UserInfo user = null) : base(type)
      {
         this.encoding = DefaultEncoding;
         this.tag = DefaultTag;
         this.subtype = DefaultTag;
         this.message = message;
         this.sender = new UserJSONObject(user);
         this.sendtype = MessageBaseSendType.IncludeSender;
         this.safe = true;
         this.recipients = new List<int>();
         //this.moduleRecipients = new List<string>();
         this.rawtime = DateTime.Now;
         this.expiration = DateTime.Now.AddHours(23.9);
      }

      public MessageBaseJSONObject(MessageBaseJSONObject copy) : base(copy)
      {
         if(copy != null)
         {
            tag = copy.tag;
            encoding = copy.encoding;
            subtype = copy.subtype;
            rawmessage = copy.rawmessage;
            safe = copy.safe;
            sender = new UserJSONObject(copy.sender);
            sendtype = copy.sendtype;
            recipients = new List<int>(copy.recipients);
            rawtime = copy.rawtime;
         }
      }
   }

   //Responses from the server should have this format.
   public class ResponseJSONObject : JSONObject
   {
      public ResponseJSONObject() : base("response") {}
      public string from = "unknown";
      public bool result = false;
      public List<string> errors = new List<string>();
      public Dictionary<string, object> extras = new Dictionary<string, object>();
   }

   //Warnings to be sent to (usually one) users
   public class WarningMessageJSONObject : MessageBaseJSONObject
   {
      private void Setup()
      {
         expiration = DateTime.Now.AddSeconds(60);
         //sendtype = MessageBaseSendType.IncludeSender;
      }

      public WarningMessageJSONObject(string message = "", UserInfo user = null) : base("warning", message, user) 
      {
         Setup();
      }

      public WarningMessageJSONObject(WarningMessageJSONObject copy) : base(copy) 
      {
         Setup();
      }
   }

   //System messages have a similar format to warnings.
   public class SystemMessageJSONObject : MessageBaseJSONObject
   {
      private void Setup()
      {
         expiration = DateTime.Now.AddSeconds(5);
         //sendtype = MessageBaseSendType.IncludeSender;
      }

      public SystemMessageJSONObject(string message = "", UserInfo user = null) : base("system", message, user) 
      {
         Setup();
      }

      public SystemMessageJSONObject(SystemMessageJSONObject copy) : base(copy) 
      {
         Setup();
      }
   }

   //The list of messages sent out in JSON should follow this format
   public class MessageListJSONObject : JSONObject
   {
      public MessageListJSONObject() : base("messageList") {}
      public List<MessageBaseJSONObject> messages = new List<MessageBaseJSONObject>();
   }

   //Sending out a list of users should follow this format
   public class UserListJSONObject : JSONObject
   {
      public UserListJSONObject() : base("userList") {}
      public List<UserJSONObject> users = new List<UserJSONObject>();
      public List<RoomJSONObject> rooms = new List<RoomJSONObject>();
   }

   //A single user within the UserList JSON object
   public class UserJSONObject
   {
      public readonly string username = "";
      public readonly string stars = "";
      public readonly int level = 0;
      public readonly int uid = 0;
      public readonly long joined = 0;
      public string avatar = "";
      public bool active = false;
      public bool banned = false;
      public List<Badge> badges = new List<Badge>();

      //This stupid thing is JUST for dumb libraries that require a default
      //constructor (even though... nevermind)
      public UserJSONObject() : this((UserInfo)null) {}

      //public UserJSONObject
      public UserJSONObject(UserInfo user)
      {
         if (user != null)
         {
            uid = user.UID;
            username = user.Username;
            avatar = user.Avatar;
            stars = user.StarString;
            level = user.Level;
            active = user.Active;
            joined = user.UnixJoinDate;
            banned = user.Banned;
            badges = user.Badges;
         }
      }

      public UserJSONObject(UserJSONObject copy)
      {
         if(copy != null)
         {
            uid = copy.uid;
            username = copy.username;
            avatar = copy.avatar;
            stars = copy.stars;
            level = copy.level;
            active = copy.active;
            joined = copy.joined;
            banned = copy.banned;
            badges = copy.badges;
         }
      }

//      public void StripUnimportantData()
//      {
//         badges = new List<Badge>();
//      }
   }

   //A single PM room within the UserList JSON Object
   public class RoomJSONObject
   {
      public readonly string name = "";
      public readonly List<UserJSONObject> users = new List<UserJSONObject> ();
      //public readonly List<RoomUserJSONObject> users = new List<RoomUserJSONObject>();

      public RoomJSONObject(PMRoom room, Dictionary<int, UserInfo> users)
      {
         name = room.Name;
         this.users = room.Users.ToList().Select(x => new UserJSONObject(users[x])).ToList();
      }
   }

//   public class RoomUserJSONObject
//   {
//      public readonly string username = "";
//      public string avatar = "";
//      public readonly int uid = 0;
//      public readonly bool active = false;
//      public readonly bool banned = false;
//
//      public RoomUserJSONObject(User user)
//      {
//         username = user.Username;
//         avatar = user.Avatar;
//         uid = user.UID;
//         active = user.Active;
//         banned = user.Banned;
//      }
//   }

   public class ModuleJSONObject : MessageBaseJSONObject
   {
      //public UserJSONObject user = null;
      public ModuleJSONObject(string message = "", UserInfo user = null) : base("module", message, user) 
      {
         this.module = "";
         //this.sendtype = MessageBaseSendType.IncludeSender;
      }
//      public ModuleJSONObject(string message) : base("module") 
//      {
//         this.rawMessage = message;
//      }
      //private string rawMessage = "";
      //public int uid = 0;

      //public string tag = "";
      public string module = "";
      //public List<int> recipients = new List<int>();

//      public string message
//      {
//         get
//         {
//            if (safe)
//               return System.Security.SecurityElement.Escape(System.Net.WebUtility.HtmlDecode(rawMessage));
//            else
//               return rawMessage;
//         }
//         set
//         {
//            rawMessage = value;
//         }
//      }
   }

   public class SystemRequest
   {
      public readonly SystemRequests Request;
      public readonly TimeSpan Timeout;

      public SystemRequest(SystemRequests request) : this(request, TimeSpan.FromTicks(0)) {}

      public SystemRequest(SystemRequests request, TimeSpan timeout)
      {
         this.Request = request;
         this.Timeout = timeout;
      }

      public static implicit operator SystemRequest(SystemRequests request)
      {
         return new SystemRequest(request);
      }
   }

   public enum SystemRequests
   {
      Reset,
      LockDeath,
      SaveModules
   }


   //A message. It SHOULD be readonly, honestly.
   [Serializable]
   public class MessageJSONObject : MessageBaseJSONObject
   {
      private bool spamupdate;

      //This may become private.
      public double spamvalue = 0;

      public MessageJSONObject() : base("message") {}

      public MessageJSONObject(string message, UserInfo user, string tag = "") : base("message", message, user)
      {
         this.tag = tag;
         this.spamupdate = true;
         this.spamvalue = 0;
         this.sendtype = MessageBaseSendType.Broadcast;
      }

      public MessageJSONObject(MessageJSONObject copy) : base(copy)
      {
         if (copy != null)
         {
            tag = copy.tag;
            spamupdate = copy.spamupdate;
            spamvalue = copy.spamvalue;
         }
      }

      public void SetSpammable(bool spammable)
      {
         spamupdate = spammable;
      }

      public bool IsSpammable()
      {
         return spamupdate;
      }
   }
}
