using System;
using System.Web;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using MyExtensions;

namespace ChatServer
{
   public class User
   {
      public const int SpamBlockSeconds = 20;

      private string username = "";
      private int spamScore = 0;
      private int globalSpamScore = 0;

      private bool staffChat = false;

      private DateTime lastPost = DateTime.Now;
      private DateTime lastSpam = new DateTime(0);
      private DateTime blockedUntil = new DateTime (0);

      public User(string name)
      {
         username = name;
      }

      public DateTime BlockedUntil 
      {
         get { return blockedUntil; }
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
            if (value < 0)
               globalSpamScore = 0;
            else
               globalSpamScore = value;
         }
      }

      public int SpamScore
      {
         get { return spamScore; }
         private set 
         {
            if (value < 0)
               spamScore = 0;
            else
               spamScore = value;
         }
      }

      //This function should be called when posts are made.
      public void PerformOnPost()
      {
         lastPost = DateTime.Now;
      }

      public bool PullInfoFromQueryPage()
      {
         try
         {
            using (WebClient client = new WebClient())
            {
               string url = "http://development.smilebasicsource.com/query/usercheck.php?getinfo=1&username=";
               url += HttpUtility.UrlEncode(username);
               string htmlCode = client.DownloadString(url);

               dynamic json = JsonConvert.DeserializeObject(htmlCode);
               staffChat = json.result.permissions.staffchat;
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
         SpamScore -= 4 * (int)(DateTime.Now - lastPost).TotalSeconds;
         SpamScore += 10 + current.Length / 100 
            + 2 * current.Split("\n".ToCharArray()).Count(x => string.IsNullOrWhiteSpace(x))       //empty line
            + 2 * (myMessages.Count > 10 ? 10 : myMessages.Count)                                  //previous messages
            + (int)(2 * (myMessages.Sum(x => 1 - StringExtensions.StringDifference(x.text, current))));  //similarity    

         //Console.WriteLine(current + " -likeness- " + (int)(2 * myMessages.Sum(x => 1 - StringExtensions.StringDifference(x.text, current))));
         //Update global spam score if they've been good
         if((DateTime.Now - lastSpam).TotalHours > 24)
            GlobalSpamScore -= 2;
         
         //Block if spam score is too high
         if(SpamScore >= 100)
         {
            GlobalSpamScore++;
            lastSpam = DateTime.Now;

            int seconds = GlobalSpamScore * SpamBlockSeconds;
            blockedUntil = DateTime.Now.AddSeconds(seconds);

            warning = new WarningJSONObject();
            warning.warning = "You have been blocked for " + seconds + " seconds for spamming.";
         }
         //Send warning if getting close
         else if(SpamScore > 60)
         {
            warning = new WarningJSONObject();
            warning.warning = "Warning: Your spam score is high. Please wait a bit before posting again";
         }

         return warning;
      }
   }
}

