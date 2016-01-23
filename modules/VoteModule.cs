using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Linq;
using MyExtensions;

namespace ModulePackage1
{
   public class VoteBallot
   {
      private static long NextID = 1;
      private static readonly Object Lock = new Object();

      private HashSet<string> choices = new HashSet<string>();
      private Dictionary<int, string> votes = new Dictionary<int, string>();
     
      public readonly string Title;
      public readonly long ID;
      public readonly DateTime CreatedOn = DateTime.Now;

      public VoteBallot(string title, HashSet<string> choices)
      {
         this.choices = choices;
         Title = title;

         lock (Lock)
         {
            ID = NextID++;
         }
      }

      public static void SetNextID(List<VoteBallot> oldBallots)
      {
         lock (Lock)
         {
            long highestID = NextID - 1;

            foreach(long id in oldBallots.Select(x => x.ID))
               if(id > highestID)
                  highestID = id;

            NextID = highestID + 1;
         }
      }

      public int TotalVotes
      {
         get { return votes.Count; }
      }

      public HashSet<string> GetChoices()
      {
         return new HashSet<string>(choices);
      }

      public Dictionary<string, double> GetResults()
      {
         double total = TotalVotes;

         return choices.Select(x => Tuple.Create(x, total == 0 ? 0.0 : votes.Count(y => y.Value == x) / total)).ToDictionary(x => x.Item1, y => y.Item2);
      }

      public string GetResultString(int forUser = 0)
      {
         string output = "Poll #" + ID + ": " + Title + "\n";
         string userChoice = votes.ContainsKey(forUser) ? votes[forUser] : "";

         foreach (KeyValuePair<string, double> result in GetResults())
         {
            output += "\n " + (userChoice == result.Key ? "#" : "-") + " " + result.Key + " : " + StringExtensions.ShortDecimal(result.Value * 100, 1) + "%";
         }

         return output + "\n\nVotes: " + votes.Count;
      }

      public bool AddVote(int user, string vote, out string error)
      {
         error = "";

         if (!choices.Contains(vote))
         {
            error = "Invalid vote choice";
            return false;
         }
         else if (votes.ContainsKey(user))
         {
            error = "Already voted on this ballot";
            return false;
         }

         votes.Add(user, vote);
         return true;
      }

      public bool DidVote(int user)
      {
         return votes.ContainsKey(user);
      }

      public override string ToString()
      {
         return "Poll #" + ID + ": " + Title + "\n" + string.Join("\n", choices.Select(x => " - " + x));
      }
   }

   public class VoteModule : Module
   {
      private Dictionary<int, List<VoteBallot>> userBallots = new Dictionary<int, List<VoteBallot>>();
      private Dictionary<int, List<VoteBallot>> archivedBallots = new Dictionary<int, List<VoteBallot>>();

      public VoteModule()
      {
         CommandArgument title = new CommandArgument("title", ArgumentType.Custom, RepeatType.One, @"""[^""]+""");
         CommandArgument choiceList = new CommandArgument("choices", ArgumentType.Custom, RepeatType.OneOrMore, @"\S+"); 
         CommandArgument poll = new CommandArgument("poll#", ArgumentType.Integer);
         CommandArgument choice = new CommandArgument("choice", ArgumentType.Word);
         //CommandArgument pageNumber = new CommandArgument("page", ArgumentType.Integer, RepeatType.ZeroOrOne);
         CommandArgument search = new CommandArgument("search", ArgumentType.FullString);

         Commands.Add(new ModuleCommand("polls", new List<CommandArgument>(), "See the top open polls"));
         Commands.Add(new ModuleCommand("poll", new List<CommandArgument> {
            poll
         }, "See data for poll#"));
         Commands.Add(new ModuleCommand("pollcreate", new List<CommandArgument>{
            title, choiceList
         }, "Create a poll with given options.", true));
         Commands.Add(new ModuleCommand("vote", new List<CommandArgument> {
            choice, poll
         }, "Vote on poll#."));
         Commands.Add(new ModuleCommand("pollclose", new List<CommandArgument> {
            poll
         }, "Close the given poll", true));
         Commands.Add(new ModuleCommand("pollsearch", new List<CommandArgument> {
            search
         }, "Search for poll with given title"));
         Commands.Add(new ModuleCommand("pollsopen", new List<CommandArgument>(), "See your open polls"));

         AddOptions(new Dictionary<string, object> {
            { "maxUserPolls", 1 },
            { "maxPollChoices", 10 },
            { "archivesPerPage", 20 },
            { "searchResults", 5 }
         });

         GeneralHelp = "Quickstart: Do /polls to see the list of polls you can vote on. " +
            "Do /poll 1 to see the voting options for poll #1." +
            "Do /vote yes 1 to vote \"yes\" on poll #1. " +
            "Do /pollsearch Blah blah blah to search for a poll by title. " +
            "To create a poll, do /pollcreate \"This is my poll\" option1 option2 etc... " +
            "Options do not have to be yes or no, they can be anything. Options cannot have spaces.";
      }

      public override string Nickname
      {
         get
         {
            return "polls";
         }
      }

      public int MaxUserPolls
      {
         get { return GetOption<int>("maxUserPolls"); }
      }
      public int MaxPollChoices
      {
         get { return GetOption<int>("maxPollChoices"); }
      }
      public int ArchivesPerPage
      {
         get { return GetOption<int>("archivesPerPage"); }
      }
      public int SearchResults
      {
         get { return GetOption<int>("searchResults"); }
      }

      public override bool LoadFiles()
      {
         bool result = MySerialize.LoadObject<Dictionary<int, List<VoteBallot>>>("current_" + DefaultSaveFile, out userBallots) &&
                       MySerialize.LoadObject<Dictionary<int, List<VoteBallot>>>("archive_" + DefaultSaveFile, out archivedBallots);

         if (result)
            VoteBallot.SetNextID(userBallots.SelectMany(x => x.Value).Union(archivedBallots.SelectMany(x => x.Value)).ToList());

         return result;
      }

      public override bool SaveFiles()
      {
         return MySerialize.SaveObject<Dictionary<int, List<VoteBallot>>>("current_" + DefaultSaveFile, userBallots) &&
            MySerialize.SaveObject<Dictionary<int, List<VoteBallot>>>("archive_" + DefaultSaveFile, archivedBallots);
      }

      public bool GetBallot(long pollNumber, bool useArchive, out VoteBallot ballot)
      {
         ballot = null;

         try
         {
            if(useArchive)
               ballot = userBallots.SelectMany(x => x.Value).Union(archivedBallots.SelectMany(x => x.Value)).First(x => x.ID == pollNumber);
            else
               ballot = userBallots.SelectMany(x => x.Value).First(x => x.ID == pollNumber);
            return true;
         }
         catch
         {
            return false;
         }
      }

      public string PrintList(IEnumerable<VoteBallot> ballots)
      {
         string output = "";

         foreach (VoteBallot topBallot in ballots)
         {
            output += "\n #" + topBallot.ID + " - " + topBallot.Title + (archivedBallots.SelectMany(x => x.Value).Contains(topBallot) ? " [closed]" : "");
            output += " - (" + string.Join(", ", topBallot.GetChoices()) + ")";
         }

         return output;
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject moduleOutput = new ModuleJSONObject();
         VoteBallot ballot;
         string error = "";
         string output = "";
         long pollNumber = 0;

         if (!userBallots.ContainsKey(user.UID))
            userBallots.Add(user.UID, new List<VoteBallot>());

         try
         {
            switch(command.Command)
            {
               case "pollcreate":
                  int maxPolls = MaxUserPolls * (user.CanStaffChat ? 5 : 1);
                  if (userBallots[user.UID].Count >= maxPolls)
                     return FastMessage("You've reached the maximum amount of allowed polls (" + maxPolls + ") and cannot post a new one", true);
                  else if (command.ArgumentParts[1].Count > MaxPollChoices)
                     return FastMessage("There are too many choices in your poll! The max is " + MaxPollChoices, true);

                  VoteBallot newBallot = new VoteBallot(command.Arguments[0], new HashSet<string>(command.ArgumentParts[1]));

                  if (newBallot.GetChoices().Count < 2)
                     return FastMessage("Your poll must have at least 2 options", true);

                  userBallots[user.UID].Add(newBallot);
                  moduleOutput.broadcast = true;
                  moduleOutput.message = "A new poll has been created by " + user.Username + ":\n\n" + userBallots[user.UID].Last();
                  outputs.Add(moduleOutput);

                  break;

               case "vote":

                  if(!long.TryParse(command.Arguments[1], out pollNumber))
                     return FastMessage("Your poll number is out of bounds!", true);

                  if(!GetBallot(pollNumber, false, out ballot))
                  {
                     return FastMessage("There is no open poll with ID " + pollNumber);
                  }
                  else
                  {
                     if(!ballot.AddVote(user.UID, command.Arguments[0], out error))
                        return FastMessage(error);
                     else
                        return FastMessage("You voted on this poll: \n\n" + ballot.GetResultString(user.UID));
                  }

               case "polls":
                  
                  output = "The top polls right now: \n";
                  output += PrintList(userBallots.SelectMany(x => x.Value).OrderByDescending(x => x.TotalVotes).Take(10));

                  return FastMessage(output);

               case "poll":
                  
                  if(!long.TryParse(command.Arguments[0], out pollNumber))
                     return FastMessage("Your poll number is out of bounds!", true);

                  if(!GetBallot(pollNumber, true, out ballot))
                  {
                     return FastMessage("There is no open poll with ID " + pollNumber);
                  }
                  else
                  {
                     output = "";
                     bool closed = archivedBallots.SelectMany(x => x.Value).Contains(ballot);

                     if(ballot.DidVote(user.UID) || closed)
                        output = ballot.GetResultString(user.UID);
                     else
                        output = ballot.ToString();

                     int ballotCreator = userBallots.Union(archivedBallots).First(x => x.Value.Contains(ballot)).Key;
                     output += "\nPoll by: " + users[ballotCreator].Username + (closed ? " (closed)" : "");

                     return FastMessage(output);
                  }

               case "pollclose":

                  if(!long.TryParse(command.Arguments[0], out pollNumber))
                     return FastMessage("Your poll number is out of bounds!", true);

                  if(!userBallots[user.UID].Any(x => x.ID == pollNumber))
                     return FastMessage("You don't have any open polls with this ID!");

                  if(!GetBallot(pollNumber, false, out ballot))
                  {
                     return FastMessage("There is no open poll with ID " + pollNumber);
                  }
                  else
                  {
                     if(!archivedBallots.ContainsKey(user.UID))
                        archivedBallots.Add(user.UID, new List<VoteBallot>());

                     archivedBallots[user.UID].Add(ballot);
                     userBallots[user.UID].Remove(ballot);

                     moduleOutput.broadcast = true;
                     moduleOutput.message = "The poll " + ballot.Title + " has just been closed by " + user.Username + ". The results:\n\n" + ballot.GetResultString();
                     outputs.Add(moduleOutput);
                  }

                  break;

               case "pollsearch":

                  List<Tuple<double, VoteBallot>> sortedBallots = new List<Tuple<double, VoteBallot>>();

                  foreach(VoteBallot searchBallot in userBallots.SelectMany(x => x.Value).Union(archivedBallots.SelectMany(x => x.Value)))
                     sortedBallots.Add(Tuple.Create(StringExtensions.StringDifference(command.Arguments[0].ToLower(), searchBallot.Title.ToLower()), searchBallot));

                  output = "These ballots have a similar title: \n";
                  output += PrintList(sortedBallots.OrderBy(x => x.Item1).Select(x => x.Item2).Take(SearchResults));

                  return FastMessage(output);

               case "pollsopen":

                  output = "Your open polls right now are: \n" + PrintList(userBallots[user.UID]);
                  return FastMessage(output);
            }
         }
         catch(Exception e)
         {
            return new List<JSONObject>() { 
               new ModuleJSONObject() { 
                  message = "Something terrible happened in the Vote module: " + e, broadcast = true 
               } };
         }

         return outputs;
      }
   }
}