using System;
using System.Collections.Generic;
using System.Linq;

namespace ChatServer
{
   public class User
   {
      public const int SpamBlockSeconds = 20;

      private string username = "";
      private int spamScore = 0;
      private int globalSpamScore = 0;

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
         get { return (int)((blockedUntil - DateTime.Now).TotalSeconds + 0.99); }
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
         SpamScore += 10 + current.Length / 100
            + 2 * (myMessages.Count > 10 ? 10 : myMessages.Count)
            + 2 * (myMessages.Count(x => x.text == current))
            - 4 * (int)(DateTime.Now - lastPost).TotalSeconds;

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

