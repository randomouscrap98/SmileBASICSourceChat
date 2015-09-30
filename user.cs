using System;
using System.Web;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using MyExtensions;
using ChatEssentials;

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
      public readonly long UnixJoinDate;
      public readonly DateTime LastPost;
      public readonly DateTime LastPing;
      public readonly bool Active;
      public readonly bool Banned;
      public readonly DateTime BannedUntil;
      public readonly DateTime BlockedUntil;
      public readonly int SpamScore;
      public readonly int GlobalSpamScore;
      public readonly int SecondsToUnblock;
      public readonly bool CanStaffChat;
      public readonly TimeSpan TotalChatTime;
      public readonly TimeSpan AverageSessionTime;

      public UserInfo(User user, bool loggedIn)
      {
         LoggedIn = loggedIn;
         UID = user.UID;
         Username = user.Username;
         Avatar = user.Avatar;
         StarString = user.StarString;
         UnixJoinDate = user.UnixJoinDate;
         LastPing = user.LastPing;
         LastPost = user.LastPost;
         Active = user.Active;
         Banned = user.Banned;
         BannedUntil = user.BannedUntil;
         BlockedUntil = user.BlockedUntil;
         SpamScore = user.SpamScore;
         GlobalSpamScore = user.GlobalSpamScore;
         SecondsToUnblock = user.SecondsToUnblock;
         CanStaffChat = user.CanStaffChat;
         TotalChatTime = user.TotalChatTime;
         AverageSessionTime = user.AverageSessionTime;
      }
   }

   [Serializable]
   public class User
   {
      private static int SpamBlockSeconds = 1;
      private static int InactiveMinutes = 1;
      private static string Website = "";

      private readonly int uid = 0;
      private string username = "default";
      private string avatar = "";
      private string stars = "";
      private DateTime joinDate = DateTime.Now;

      private int spamScore = 0;
      private int globalSpamScore = 0;

      private bool staffChat = false;
      private DateTime bannedUntil = new DateTime(0);

      private bool lastActive = true;
      private DateTime lastPing = DateTime.Now;
      private DateTime lastPost = DateTime.Now;
      private DateTime lastSpam = new DateTime(0);
      private DateTime blockedUntil = new DateTime (0);
      private List<UserSession> sessions = new List<UserSession>();

      public readonly Object Lock = new Object();

      public User(int uid)
      {
         this.uid = uid;
      }

      public string Username
      {
         get { return username; }
      }

      public string Avatar
      {
         get { return avatar; }
      }

      public string StarString
      {
         get { return stars; }
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
      public static void SetUserParameters(int spamBlockSeconds, int inactiveMinutes, string website)
      {
         SpamBlockSeconds = spamBlockSeconds;
         InactiveMinutes = inactiveMinutes;
         Website = website;
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
         get { return lastPing > DateTime.Now.AddMinutes(-InactiveMinutes); }
      }

      public bool StatusChanged
      {
         get { return lastActive != Active; }
      }

      public DateTime BlockedUntil 
      {
         get { return blockedUntil; }
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

      public int SpamScore
      {
         get { return spamScore; }
         private set 
         {
            lock (Lock)
            {
               if (value < 0)
                  spamScore = 0;
               else
                  spamScore = value;
            }
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

      //This function should be called when posts are made.
      public void PerformOnPost()
      {
         lock (Lock)
         {
            lastPost = DateTime.Now;
         }

         PerformOnPing();
      }

      public void PerformOnPing()
      {
         lock (Lock)
         {
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
         PerformOnPing();
         bool result = false;

         lock (Lock)
         {
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

         return result && (sessions.Count(x => !x.Left) == 1);
      }

      public bool PerformOnChatLeave()
      {
         bool result = true;

         lock (Lock)
         {
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
               string url = Website + "/query/usercheck.php?getinfo=1&uid=" + uid;
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
                  bannedUntil = DateExtensions.FromUnixTime((double)json.result.banneduntil);
                  joinDate = DateExtensions.FromUnixTime((double)json.result.joined);
               }
            }
         }
         catch
         {
            return false;
         }

         return true;
      }
      
      public WarningJSONObject UpdateSpam(List<UserMessageJSONObject> messages, string current)
      {
         WarningJSONObject warning = null;

         //Update spam score.
         //Look at all the messages and see how many messages in the 
         //last minute were theirs (up to 10). Increase spam score accordingly
         List<UserMessageJSONObject> myMessages = messages.Where(x => 
            (DateTime.Now - x.PostTime()).TotalMinutes < 1 && x.uid == uid).ToList();

         //Update spam score based on last message length, how many previous
         //messages were theirs, and how long it has been since the last
         //message.
         SpamScore -= 3 * (int)(DateTime.Now - lastPost).TotalSeconds;
         SpamScore += 5 + current.Length / 100
         + 4 * current.Split("\n".ToCharArray()).Count(x => string.IsNullOrWhiteSpace(x))//empty line
         + (int)(1.5 * (myMessages.Count > 10 ? 10 : myMessages.Count))//previous messages
         + (int)(4.5 * (myMessages.Sum(x => 1 - StringExtensions.StringDifference(x.message, current))));  //similarity    

         //Console.WriteLine(current + " -likeness- " + (int)(2 * myMessages.Sum(x => 1 - StringExtensions.StringDifference(x.text, current))));
         //Update global spam score if they've been good
         if ((DateTime.Now - lastSpam).TotalHours > 24)
            GlobalSpamScore -= 2;
      
         //Block if spam score is too high
         if (SpamScore >= 100)
         {
            GlobalSpamScore++;
            int seconds = GlobalSpamScore * SpamBlockSeconds;

            lock (Lock)
            {
               blockedUntil = DateTime.Now.AddSeconds(seconds);
               lastSpam = DateTime.Now;
            }

            warning = new WarningJSONObject();
            warning.message = "You have been blocked for " + seconds + " seconds for spamming.";
         }
         //Send warning if getting close
         else if (SpamScore > 60)
         {
            warning = new WarningJSONObject();
            warning.message = "Warning: Your spam score is high. Please wait a bit before posting again";
         }

         return warning;
      }
   }
}

