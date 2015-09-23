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
   public class User
   {
      private static int SpamBlockSeconds = 1;
      private static int InactiveMinutes = 1;
      private static string Website = "";

      private string username = "";
      private int spamScore = 0;
      private int globalSpamScore = 0;

      private bool staffChat = false;
      private bool chatBanned = false;
      private DateTime bannedUntil = new DateTime(0);

      private bool lastActive = true;
      private DateTime lastPing = DateTime.Now;
      private DateTime lastPost = DateTime.Now;
      private DateTime lastSpam = new DateTime(0);
      private DateTime blockedUntil = new DateTime (0);

      private readonly Object Lock = new Object();

      public User(string name)
      {
         username = name;
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
         get { return chatBanned; }
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

      public bool PullInfoFromQueryPage()
      {
         try
         {
            using (WebClient client = new WebClient())
            {
               string url = Website + "/query/usercheck.php?getinfo=1&uid=";
               url += Uri.EscapeDataString(username);
               string htmlCode = client.DownloadString(url);

               //Console.WriteLine("URL: " + url);
               //Console.WriteLine("Username: " + username);
               //Console.WriteLine("Result: " + htmlCode);

               dynamic json = JsonConvert.DeserializeObject(htmlCode);

               lock(Lock)
               {
                  staffChat = json.result.permissions.staffchat;
                  chatBanned = json.result.chatbanned;
                  bannedUntil = DateExtensions.FromUnixTime((double)json.result.banneduntil);
               }
            }
         }
         catch
         {
            return false;
         }

         return true;
      }
      
      public WarningJSONObject UpdateSpam(List<Message> messages, string current)
      {
         WarningJSONObject warning = null;

         //Update spam score.
         //Look at all the messages and see how many messages in the 
         //last minute were theirs (up to 10). Increase spam score accordingly
         List<Message> myMessages = messages.Where(x => 
         (DateTime.Now - x.PostTime()).TotalMinutes < 1 &&
                                 x.username == username).ToList();

         //Update spam score based on last message length, how many previous
         //messages were theirs, and how long it has been since the last
         //message.
         SpamScore -= 3 * (int)(DateTime.Now - lastPost).TotalSeconds;
         SpamScore += 5 + current.Length / 100
         + 4 * current.Split("\n".ToCharArray()).Count(x => string.IsNullOrWhiteSpace(x))//empty line
         + (int)(1.5 * (myMessages.Count > 10 ? 10 : myMessages.Count))//previous messages
         + (int)(4.5 * (myMessages.Sum(x => 1 - StringExtensions.StringDifference(x.text, current))));  //similarity    

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
            warning.warning = "You have been blocked for " + seconds + " seconds for spamming.";
         }
         //Send warning if getting close
         else if (SpamScore > 60)
         {
            warning = new WarningJSONObject();
            warning.warning = "Warning: Your spam score is high. Please wait a bit before posting again";
         }

         return warning;
      }
   }
}

