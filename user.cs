using System;
using System.Web;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using MyExtensions;
using ChatEssentials;
using System.Timers;

namespace ChatEssentials
{
   public class UserSession
   {
      private DateTime enterDate = new DateTime(0);
      private DateTime leaveDate = new DateTime(0);

      //user just entered
      public void SetEnterNow()
      {
         enterDate = DateTime.Now;
         leaveDate = new DateTime(enterDate.Ticks);
      }

      //user just left
      public void SetLeaveNow()
      {
         leaveDate = DateTime.Now;
      }

      public DateTime EnterDate
      {
         get { return enterDate; }
      }

      public DateTime LeaveDate
      {
         get { return leaveDate; }
      }

      public TimeSpan Time
      {
         get 
         { 
            if (Left)
               return (leaveDate - enterDate);
            else
               return (DateTime.Now - enterDate);
         }
      }

      public bool Entered
      {
         get { return enterDate.Ticks != 0; }
      }

      public bool Left
      {
         get { return leaveDate.Ticks != 0 && leaveDate.Ticks != enterDate.Ticks; }
      }
   }

   //Just information on User
   public class UserInfo
   {
      public readonly bool LoggedIn;
      public readonly int UID;
      public readonly string Username;
      public readonly string Avatar;
      public readonly string StarString;
      public readonly string Language;
      public readonly long UnixJoinDate;
      public readonly DateTime LastPost;
      public readonly DateTime LastPing;
      public readonly bool Active;
      public readonly bool Banned;
      public readonly bool Blocked;
      public readonly DateTime BannedUntil;
      public readonly DateTime BlockedUntil;
      public readonly int SpamScore;
      public readonly int GlobalSpamScore;
      public readonly int SecondsToUnblock;
      public readonly bool CanStaffChat;
      public readonly bool CanGlobalChat;
      public readonly bool ChatControl;
      public readonly bool ChatControlExtended;
      public readonly TimeSpan TotalChatTime;
      public readonly TimeSpan AverageSessionTime;
      public readonly TimeSpan CurrentSessionTime;
      public readonly int SessionCount;

      public UserInfo(User user, bool loggedIn)
      {
         LoggedIn = loggedIn;
         UID = user.UID;
         Username = user.Username;
         Avatar = user.Avatar;
         StarString = user.StarString;
         Language = user.Language;
         UnixJoinDate = user.UnixJoinDate;
         LastPing = user.LastPing;
         LastPost = user.LastPost;
         Active = user.Active;
         Banned = user.Banned;
         BannedUntil = user.BannedUntil;
         BlockedUntil = user.BlockedUntil;
         user.RequestDecayUpdate();
         SpamScore = user.SpamScore;
         GlobalSpamScore = user.GlobalSpamScore;
         SecondsToUnblock = user.SecondsToUnblock;
         CanStaffChat = user.CanStaffChat;
         CanGlobalChat = user.CanGlobalChat;
         TotalChatTime = user.TotalChatTime;
         AverageSessionTime = user.AverageSessionTime;
         Blocked = user.Blocked;
         CurrentSessionTime = user.CurrentSessionTime;
         ChatControl = user.ChatControl;
         ChatControlExtended = user.ChatControlExtended;
         SessionCount = user.SessionCount;
      }
   }

   [Serializable]
   public class User
   {
      public const double JoinSpamMinutes = 2.0;
      //public const string IrcAppendTag = "-irc";

      private static TimeSpan PolicyReminderTime = TimeSpan.FromDays(1);
      private static bool AutomaticBlocking = true;
      private static int SpamBlockSeconds = 1;
      private static int InactiveMinutes = 1;
      private static string Website = "";

      private readonly int uid = 0;
      private string username = "default";
      //private string ircUsername = "";
      private string avatar = "";
      private string stars = "";
      private string language = "";
      private DateTime joinDate = DateTime.Now;

      private double spamScore = 0;
      private int globalSpamScore = 0;

      private bool staffChat = false;
      private bool globalChat = false;
      private bool chatControl = false;
      private bool chatControlExtended = false;
      private DateTime bannedUntil = new DateTime(0);

      private bool lastActive = true;
      private bool acceptedPolicy = false;
      /*private bool isIrcUser = false;
      private bool ircActive = false;*/
      private DateTime lastPolicyReminder = new DateTime(0);
      private DateTime lastPing = DateTime.Now;
      private DateTime lastPost = DateTime.Now;
      private DateTime lastDecay = DateTime.Now;
      private DateTime lastBlock = new DateTime(0);
      private DateTime lastGlobalReduce = new DateTime(0);
      //private DateTime lastJoin = DateTime.Now;
      private DateTime blockedUntil = new DateTime(0);
      private List<UserSession> sessions = new List<UserSession>();
      private List<DateTime> recentJoins = new List<DateTime>();

//      [JsonIgnore]
//      private readonly Timer userUpdate = new Timer(1000);

      public readonly Object Lock = new Object();

      public User(int uid)
      {
         this.uid = uid;
//         userUpdate.Elapsed += SpamDecay;
//         userUpdate.Start();
      }

      public string Username
      {
         get { return username; }
      }

//      public string IrcUsername
//      {
//         get { return ircUsername; }
//      }

      public string Avatar
      {
         get { return avatar; }
      }

      public string StarString
      {
         get { return stars; }
      }

      public string Language
      {
         get { return language; }
      }

      public int UID
      {
         get { return uid; }
      }

      public long UnixJoinDate
      {
         get { return MyExtensions.DateExtensions.ToUnixTime(joinDate); }
      }

      //Set static user parameters (constants, probably from an options file)
      public static void SetUserParameters(int spamBlockSeconds, int inactiveMinutes, string website, bool automaticBlocking,
         double reminderHours)
      {
         SpamBlockSeconds = spamBlockSeconds;
         InactiveMinutes = inactiveMinutes;
         Website = website;
         AutomaticBlocking = automaticBlocking;
         PolicyReminderTime = TimeSpan.FromHours(reminderHours);
      }

      public DateTime LastPost
      {
         get { return lastPost; }
      }

      public DateTime LastPing
      {
         get { return lastPing; }
      }

      public bool Active
      {
         get 
         { 
            /*if (isIrcUser)
               return ircActive;*/
            
            return lastPing > DateTime.Now.AddMinutes(-InactiveMinutes); 
         }
      }

      public bool StatusChanged
      {
         get { return lastActive != Active; }
      }

      public bool AcceptedPolicy
      {
         get { return acceptedPolicy; }
      }

      public DateTime BlockedUntil 
      {
         get { return blockedUntil; }
      }

      public bool Blocked
      {
         get { return DateTime.Now < blockedUntil; }
      }

      public bool Banned
      {
         get { return bannedUntil > DateTime.Now; }
      }
      public DateTime BannedUntil
      {
         get { return bannedUntil; }
      }
      public int SecondsToUnblock
      {
         get { return (int)((blockedUntil - DateTime.Now).TotalSeconds + 0.9999); }
      }

      public bool CanStaffChat 
      {
         get { return staffChat; }
      }

      public bool CanGlobalChat
      {
         get { return globalChat; }
      }

      public bool ChatControl
      {
         get { return chatControl; }
      }

      public bool ChatControlExtended
      {
         get { return chatControlExtended; }
      }

      public bool ShowMessages
      {
         get { return !Banned && !Blocked; }
      }

      public int GlobalSpamScore
      {
         get { return globalSpamScore; }
         private set 
         {
            lock (Lock)
            {
               if (value < 0)
                  globalSpamScore = 0;
               else
                  globalSpamScore = value;
            }
         }
      }

      public double RealSpamScore
      {
         get 
         {
            return spamScore; 
         }
         private set 
         {
            lock (Lock)
            {
               if (value < 0)
                  spamScore = 0;
               else if (value > 1000)
                  spamScore = 1000;
               else
                  spamScore = value;

               if (double.IsNaN(spamScore) || double.IsInfinity(spamScore))
                  spamScore = 0;
            }
         }
      }

      public int SpamScore
      {
         get 
         { 
            
            return (int)RealSpamScore; 
         }
      }

      public TimeSpan TotalChatTime
      {
         get
         {
            lock (Lock)
            {
               return new TimeSpan(sessions.Sum(x => x.Time.Ticks));
            }
         }
      }

      public TimeSpan AverageSessionTime
      {
         get
         {
            lock (Lock)
            {
               if (sessions.Count == 0)
                  return new TimeSpan(0);
               
               return new TimeSpan(TotalChatTime.Ticks / sessions.Count);
            }
         }
      }

      public TimeSpan CurrentSessionTime
      {
         get
         {
            lock (Lock)
            {
               try
               {
                  return sessions.Last(x => x.Left == false).Time;
               }
               catch
               {
                  return new TimeSpan(0);
               }
            }
         }
      }

      public int SessionCount
      {
         get
         {
            lock (Lock)
            {
               return sessions.Count;
            }
         }
      }

      public bool ShouldPolicyRemind
      {
         get { return (DateTime.Now - lastPolicyReminder) > PolicyReminderTime; }
      }

      /*public bool IrcUser
      {
         get { return isIrcUser; }
      }

      public void SetIRC(string username, bool active)
      {
         isIrcUser = true;
         //this.ircUsername = username;
         this.username = username; //+ IrcAppendTag;
         this.avatar = "/user_uploads/avatars/tdefault.png";
         ircActive = active;
      }*/

      public void AcceptPolicy()
      {
         lock (Lock)
         {
            acceptedPolicy = true;
         }
      }

      public void PerformOnReminder()
      {
         lock (Lock)
         {
            lastPolicyReminder = DateTime.Now;
         }
      }

      //This function should be called when posts are made.
      public void PerformOnPost()
      {
         lock (Lock)
         {
            lastPost = DateTime.Now;
         }

         PerformOnPing(true);
      }

      public void PerformOnPing(bool active)
      {
         lock (Lock)
         {
            //For now, this is all we do. Maybe in the future, we can do something special with the 
            //"active" state.
            if(active)
               lastPing = DateTime.Now;
         }
      }

      public void SaveActiveState()
      {
         lock (Lock)
         {
            lastActive = Active;
         }
      }

      public bool PerformOnChatEnter()
      {
         //No, don't do anything for these guys.
         /*if (isIrcUser)
            return true;*/
         
         PerformOnPing(true);
         bool result = false;

         lock (Lock)
         {
            //Our last join was now (assuming this is called correctly)
            recentJoins.Add(DateTime.Now);
            recentJoins = recentJoins.Where(x => x > DateTime.Now.AddDays(-1)).ToList();

            //If it's empty or we have no open sessions, add a new one
            if (sessions.Count == 0 || sessions.All(x => x.Entered))
            {
               sessions.Add(new UserSession());
               result = true;
            }

            try
            {
               //Look for the last session that hasn't entered yet and set the entry to now.
               sessions.Last(x => !x.Entered).SetEnterNow();
            }
            catch
            {
               return false;
            }
         }

         //We're only good if we succeeded AND there's only one session open
         return result && (sessions.Count(x => !x.Left) == 1);
      }

      public bool PerformOnChatLeave()
      {
         bool result = true;

         //No, don't do anything for these guys.
         /*if (isIrcUser)
            return true;*/

         lock (Lock)
         {
            //This is to set the "inactive" state on logout
            lastPing = new DateTime(0);

            //You shouldn't be leaving in this case. Come on now
            if (sessions.Count == 0 || sessions.All(x => x.Left))
            {
               result = false;
            }
            else
            {
               //If we have more than one open session, this is bad
               if (sessions.Count(x => !x.Left) > 1)
                  result = false;
               
               try
               {
                  //Close ALLL open sessions.
                  foreach(UserSession session in sessions.Where(x => !x.Left))
                     session.SetLeaveNow();
               }
               catch
               {
                  return false;
               }
            }
         }

         return result && sessions.All(x => x.Left);
      }

      public bool PullInfoFromQueryPage()
      {
         try
         {
            using (WebClient client = new WebClient())
            {
               string url = Website + "/query/usercheck?getinfo=1&uid=" + uid;
               string htmlCode = client.DownloadString(url);

               //Console.WriteLine("URL: " + url);
               //Console.WriteLine("Username: " + username);
               //Console.WriteLine("Result: " + htmlCode);

               dynamic json = JsonConvert.DeserializeObject(htmlCode);

               lock(Lock)
               {
                  username = json.result.username;
                  avatar = json.result.avatar;
                  stars = json.result.starlevel;
                  staffChat = json.result.permissions.staffchat;
                  globalChat = json.result.permissions.chatany;
                  chatControl = json.result.permissions.chatcontrol;
                  chatControlExtended = json.result.permissions.chatcontrolextended;
                  bannedUntil = DateExtensions.FromUnixTime((double)json.result.banneduntil);
                  joinDate = DateExtensions.FromUnixTime((double)json.result.joined);
                  language = json.result.language;
               }
            }
         }
         catch
         {
            return false;
         }

         return true;
      }

      public ChatTags JoinSpam()
      {
         double points = 0;

         lock (Lock)
         {
            foreach (DateTime joinTime in recentJoins)
            {
               if((DateTime.Now - joinTime).TotalMinutes < JoinSpamMinutes)
                  points += (30 * (1 - (DateTime.Now - joinTime).TotalMinutes / JoinSpamMinutes));
            }
         }

         if (points < 0)
            points = 0;
         
         return UpdateSpam(points);
      }

      public ChatTags MessageSpam(List<UserMessageJSONObject> messages, string current)
      {
         //Get all messages from the last 10 minutes (hopefully we still have them)
         List<UserMessageJSONObject> myMessages = messages.Where(x => (DateTime.Now - x.PostTime()).TotalMinutes < 10 && 
            x.uid == uid && x.Spammable).ToList();

         double lastPostTimePenalty = (5 - (DateTime.Now - LastPost).TotalSeconds) / 5.0;

         //Penalize spam score based on last message length and empty line count.
         double tempSpamScore = 5 + (lastPostTimePenalty > 0 ? 20 * Math.Pow(lastPostTimePenalty, 1.2) : 0) +
            current.Length / 100.0 + 5 * current.Split("\n".ToCharArray()).Count(x => string.IsNullOrWhiteSpace(x));   //empty line

         foreach(UserMessageJSONObject message in myMessages)
         {
            double messageDifference = 1 - StringExtensions.StringDifference(message.message, current, 0.5);
            double timeDifference = 1 - (DateTime.Now - message.PostTime()).TotalMinutes / 10.0;

            //First is the "time-ignoring" long spamathon penalty, then the "level headed" fast spamming
            tempSpamScore += 6.0 * (Math.Pow(timeDifference, 0.1) * Math.Pow(messageDifference, 6.0));
            tempSpamScore += 2.0 * (Math.Pow(messageDifference, 1.0) * Math.Pow(timeDifference, 8.0)); 
         }

         return UpdateSpam(tempSpamScore);
      }

      //Decay the spamscore based on time.
      public void RequestDecayUpdate()
      {
         RealSpamScore -= 4.0 * (DateTime.Now - lastDecay).TotalSeconds;
         lastDecay = DateTime.Now;
      }

      //Generic spam score update (with given point count)
      private ChatTags UpdateSpam(double points)
      {
         //Only update spamscore if they're not already blocked. Otherwise, leave them alone!
         if (!Blocked && points != double.NaN)
         {
            RequestDecayUpdate();      //Always decay before updating
            RealSpamScore += points;

            //Update global spam score if they've been good (and we haven't updated for a day)
            if ((DateTime.Now - lastBlock).TotalHours > 24 && (DateTime.Now - lastGlobalReduce).TotalHours > 24)
            {
               GlobalSpamScore -= 2;
               lastGlobalReduce = DateTime.Now;
            }

            if (AutomaticBlocking)
            {
               //Block if spam score is too high
               if (RealSpamScore >= 100)
               {
                  RealSpamScore = 0;   //Just reset spamscore; they're blocked anyway.
                  GlobalSpamScore++;
                  int seconds = GlobalSpamScore * SpamBlockSeconds;

                  lock (Lock)
                  {
                     blockedUntil = DateTime.Now.AddSeconds(seconds);
                     lastBlock = DateTime.Now;
                  }

                  return ChatTags.Blocked;
               }
               //Send warning if getting close
               else if (RealSpamScore > 60)
               {
                  return ChatTags.Warning;
               }
            }
         }

         return ChatTags.None;
      }
   }
}

