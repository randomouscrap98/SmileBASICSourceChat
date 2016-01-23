using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.IO;
using MyExtensions;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using ModuleSystem;
using ChatEssentials;

namespace WikiaChatLogger
{
	public class CollectGameModule : Module
	{
		//This manages all the various transactions and trades that can occur
		private CollectionOffer offer = new CollectionOffer(CollectionManager.OfferTimeout);

		public const string ListTerminator = "_&&_";

		private Dictionary<int, CollectionPlayer> collectors = new Dictionary<int, CollectionPlayer>();

		private CollectionGenerator generator = new CollectionGenerator();
		private BackgroundWorker chanceSimulator = new BackgroundWorker();
      private Random localRandom = new Random();

		public CollectGameModule()
		{
         AddOptions(new Dictionary<string, object> {
            { "rankup", 50 },
            { "exchange", 500 },
            { "maxWithdraw", 100 },
            { "maxLovers", 20 },
            { "journalStyle", "style=\"font-family:'Courier New', Courier, monospace; line-height: 100%;\"" },
            { "jealousyHours", 24 },
            { "jealousyCoins", 1000 },
            { "hugSayings", "How nice!, You smile a bit!, You wonder why., How eccentric!, How lovely!, " +
               "You're pretty happy about it!, Your HP was restored by 10 points!, You became confused!, " +
               "Your defense fell!, Your spirits rose!, Maybe the world isn't so bad after all., " +
               "You cry a little., You cry a little inside., You burst into tears on their shoulder!, " +
               "You're a little uncomfortable., You want to return the favor., Your stress ebbs away., " +
               "You feel like you understand each other a bit better!, You feel warm inside., " + 
               "*doki doki desu!*, This isn't so bad., You get a little nostalgic., " +
               "You want another one!, You value their friendship a bit more!, You're overcome with a tidal wave of emotions!, " +
               "You wish more people were like this!, The world isn't such a dark place anymore., " +
               "You feel a bit better!" }
         });

         GeneralHelp = "In cgame, you collect items by using your hourly coins to draw random items. " +
         "Your goal is to collect all 100 items, which allows you to rank up and move through the leaderboards. " +
         "You can also trade items with other people, or simply be a nice person and give out free items.";

         CommandArgument price = new CommandArgument("price", ArgumentType.Integer);
         CommandArgument count = new CommandArgument("count", ArgumentType.Integer);
         CommandArgument multiplier = new CommandArgument("multiplier", ArgumentType.Custom, RepeatType.One, @"[xX]?[0-9]+");
         CommandArgument item = new CommandArgument("item", ArgumentType.Custom, RepeatType.One, @"[a-zA-Z][0-9]");
         CommandArgument itemList = new CommandArgument("itemList", ArgumentType.Custom, RepeatType.One, @"(?:[a-zA-Z][0-9]\s*)+");
         CommandArgument player = new CommandArgument("player", ArgumentType.User);

         Commands.AddRange(new List<ModuleCommand> {
            new ModuleCommand("cgamedraw", new List<CommandArgument> {
               count
            }, "Get item(s) (Max: " + MaxWithdraw + ")"),
            new ModuleCommand("cgamedraw", new List<CommandArgument> {
               count,
               multiplier
            }, "Get item(s) with increased luck (Max multiplier: " + CollectionManager.MaxMultiplier + ")"),
            new ModuleCommand("cgamechance", new List<CommandArgument> {
               multiplier
            }, "Chance of new item with given multiplier"),
            new ModuleCommand("cgamerestock", new List<CommandArgument>(), "Get your cgame coins for the hour"),
            new ModuleCommand("cgamestock", new List<CommandArgument>(), "See your current collection"),
            new ModuleCommand("cgamestockm", new List<CommandArgument>(), "Mobile friendly cgamestock"),
            new ModuleCommand("cgametopitems", new List<CommandArgument>(), "Top items by number"),
            new ModuleCommand("cgameamount", new List<CommandArgument> {
               item
            }, "Show amount of item"),
            new ModuleCommand("cgamecompare", new List<CommandArgument> {
               player
            }, "Compare your inventory against another's for possible trades."),
            new ModuleCommand("cgamequery", new List<CommandArgument> {
               itemList
            }, "See who has the given items"),
            new ModuleCommand("cgamequeryall", new List<CommandArgument>(), "See who has any item you need"),
            new ModuleCommand("cgamesell", new List<CommandArgument> {
               itemList,
               player,
               price
            }, "Sell item(s) to player for price."),
            new ModuleCommand("cgamebuy", new List<CommandArgument> {
               itemList,
               player,
               price
            }, "Buy item(s) from player for price."),
            new ModuleCommand("cgameshopsell", new List<CommandArgument> {
               itemList
            }, "Sell item(s) to shop (low trade price). All of the item is sold."),
            new ModuleCommand("cgameshopsellall", new List<CommandArgument>(), "Sell all items to shop (low trade price)"),
            new ModuleCommand("cgamegive", new List<CommandArgument> {
               itemList,
               player
            }, "Give item(s) to player (how nice!).", true),
            new ModuleCommand("cgametrade", new List<CommandArgument> {
               itemList,
               player,
               itemList
            }, "Trade your item(s) for player's item(s)"),
            new ModuleCommand("cgamequicktrade", new List<CommandArgument> {
               player
            }, "Automatic maximum even trade with player"),
            new ModuleCommand("cgamelove", new List<CommandArgument> {
               player
            }, "Give all items you can to player"),
            new ModuleCommand("cgameaddlove", new List<CommandArgument> {
               player
            }, "Add a player to your love list"),
            new ModuleCommand("cgameremovelove", new List<CommandArgument> {
               player
            }, "Remove a player from your love list"),
            new ModuleCommand("cgameloveall", new List<CommandArgument>(), "Love everyone on your list"),
            new ModuleCommand("cgamelovelist", new List<CommandArgument>(), "Players on your love list"),
            new ModuleCommand("cgamejealousy", new List<CommandArgument>(), "Take items from other players (careful!)"),
            new ModuleCommand("cgamecheckjealousy", new List<CommandArgument>(), "Check what you'll get from jealousy"),
            new ModuleCommand("cgamehug", new List<CommandArgument> {
               player
            }, "Give a hug to reduce jealousy", true),
            new ModuleCommand("cgameaccept", new List<CommandArgument>(), "Accept trade/sell/buy offer"),
            new ModuleCommand("cgamedecline", new List<CommandArgument>(), "Decline trade/sell/buy offer"),
            new ModuleCommand("cgamerankup", new List<CommandArgument>(), "Reset journal and go to next rank"),
            new ModuleCommand("cgametop", new List<CommandArgument>(), "See top players"),
            new ModuleCommand("cgameclose", new List<CommandArgument>(), "See your competitors"),
            new ModuleCommand("cgamestats", new List<CommandArgument>(), "See your lifetime stats (ranked only)")
         });

			chanceSimulator.DoWork += new DoWorkEventHandler(chanceSimulator_DoWork);
			chanceSimulator.RunWorkerCompleted += new RunWorkerCompletedEventHandler(chanceSimulator_RunWorkerCompleted);
		}

      public int RankupCoins
      {
         get { return GetOption<int>("rankup"); }
      }
      public int ExchangeRate
      {
         get { return GetOption<int>("exchange"); }
      }
      public int MaxWithdraw
      {
         get { return GetOption<int>("maxWithdraw"); }
      }
      public int MaxLovers
      {
         get { return GetOption<int>("maxLovers"); }
      }
      public string JournalStyle
      {
         get { return GetOption<string>("journalStyle"); }
      }
      public TimeSpan JealousyTime
      {
         get { return TimeSpan.FromHours(GetOption<double>("jealousyHours")); }
      }
      public int JealousyCoins
      {
         get { return GetOption<int>("jealousyCoins"); }
      }

      public List<string> HugSayings
      {
         get 
         { 
            return GetOption<string>("hugSayings").Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
               .Select(x => x.Trim()).ToList(); 
         }
      }

		// Perform the chance calculations on a background thread
		private void chanceSimulator_DoWork(object sender, DoWorkEventArgs e)
		{
			//CollectionGenerator generator = new CollectionGenerator();
			Tuple<int, int> argument = (Tuple<int, int>)e.Argument;
         int uid = argument.Item1;
			int multiplier = argument.Item2;

			double percent = (100 * CollectionGenerator.DrawChanceForPlayerParallel(collectors[uid], multiplier));

			e.Result = Tuple.Create("You have about a " + Math.Round(percent, 1) + "% chance for a new item" + 
            (multiplier >= CollectionManager.MinMultiplier ? " with a multiplier of: " + multiplier : "") + ".", uid);
		}

		//Queue result on the message backlog
		private void chanceSimulator_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
         Tuple<string, int> result = (Tuple<string, int>)e.Result;
         ExtraCommandOutput(new List<JSONObject> { new ModuleJSONObject() { message = result.Item1 } }, result.Item2);
		}

		public override bool LoadFiles()
		{
			return MySerialize.LoadObject<Dictionary<int, CollectionPlayer>>(DefaultSaveFile, out collectors);
		}
		public override bool SaveFiles()
		{
			return MySerialize.SaveObject<Dictionary<int, CollectionPlayer>>(DefaultSaveFile, collectors);
		}

//		public override string ReportStats()
//		{
//            Dictionary<string, int> LoveCounts = new Dictionary<string,int>();
//
//            foreach(var collector in collectors)
//            {
//                foreach(string lover in collector.Value.GetLovers())
//                {
//                    if(!LoveCounts.ContainsKey(lover))
//                        LoveCounts.Add(lover, 0);
//
//                    LoveCounts[lover]++;
//                }
//            }
//           
//			return "Total items collected: " + collectors.Sum(x => (long)x.Value.ForeverJournal.TotalObtained()) +
//				"\nTotal coins collected: " + collectors.Sum(x => (long)x.Value.TotalCoins) +
//                "\nTotal people on love lists: " + LoveCounts.Sum(x => x.Value) + 
//                "\nMost loved person (based on list): " + LoveCounts.OrderByDescending(x => x.Value).FirstOrDefault() +
//				"\n" + ProcessIncoming(new ChatChunk("StatsModule", "now", "/cgametop"), new ChatInfo());
//		}

//		public override List<string> SortedModuleItems()
//		{
//			return collectors.ToList().OrderByDescending(x => x.Value.Score()).Select(x => QuickInfo(x.Key)).ToList();
//		}

		public bool TryRegister(int user)
		{
			if (!collectors.ContainsKey(user))
			{
				collectors.Add(user, new CollectionPlayer());
				return true;
			}

			return false;
		}

      public List<JSONObject> QuickQuit(string message, bool warning = false, bool safe = true)
      {
         if (warning)
         {
            WarningJSONObject output = new WarningJSONObject();
            output.message = message;
            return new List<JSONObject> { output };
         }
         else
         {
            ModuleJSONObject output = new ModuleJSONObject();
            output.message = message;
            output.safe = safe;
            return new List<JSONObject> { output };
         }
      }

      public string OfferStanding(CollectionOffer offer, Dictionary<int, UserInfo> users)
      {
         return "There is a pending offer between " + users[offer.Offerer].Username + " and " + users[offer.Accepter].Username + ". Offer stands for another " + offer.RemainingSeconds + " second".Pluralify(offer.RemainingSeconds);
      }

      public string NotAPlayer(UserInfo user)
      {
         return user.Username + " isn't a player yet. You should ask them to play!";
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         try
         {
   			//Match match;
            string cmd = command.Command;
            int myUID = user.UID;

            UserInfo player;
            ModuleJSONObject mainOutput = new ModuleJSONObject();
            List<JSONObject> outputs = new List<JSONObject>();

            TryRegister(user.UID);

   			//Top collectors
   			#region topcollectors
            if (cmd == "cgametop")
   			{
   				var topCollectors = collectors.ToList().OrderByDescending(x => x.Value.Score()).ToList();
   				string output = "The top collectors are:";

   				for (int i = 0; i < 5; i++)
   					if (i < topCollectors.Count())
   						output += "\n" + (i + 1) + ": " + QuickInfo(users[topCollectors[i].Key]);

               return QuickQuit(output);
   			}
   			#endregion

   			//Close collectors
   			#region closecollectors
            else if (cmd == "cgameclose")
   			{
   				var topCollectors = collectors.ToList().OrderByDescending(x => x.Value.Score()).ToList();
               int index = topCollectors.FindIndex(x => x.Key == myUID);

               string output = "Your competitors are:";

   				for (int i = index - 2; i <= index + 2; i++)
   					if (i < topCollectors.Count() && i >= 0)
   						output += "\n" + (i + 1) + ": " + QuickInfo(users[topCollectors[i].Key]);

               return QuickQuit(output);
   			}
   			#endregion

   			//Collect your daily coins
   			#region coinrestock
            else if (cmd == "cgamerestock")
   			{
   				if (collectors[myUID].RestockCoins(DateTime.Now))
   				{
                  return QuickQuit("You received " + collectors[myUID].RestockCoinsAmount() + " coins for the " +
                     CollectionManager.RestockHours + "-hour reset. You now have " + collectors[myUID].Coins + " coins");
   				}
   				else
   				{
   					return QuickQuit("You've already received your coins for this " + CollectionManager.RestockHours + " hour period.\n" +
                     "Please wait another " + StringExtensions.LargestTime(collectors[myUID].RestockWait));
   				}
   			}
   			#endregion

   			//Try to rank up
   			#region rankup
            else if (cmd == "cgamerankup")
   			{
   				if (collectors[myUID].RankUp())
   				{
                  string output = "You've ranked up! Your inventory and journal have been cleared, and you are now rank " + collectors[myUID].Stars;
   //                    if (!StandardCalls_RequestCoinUpdate(RankupCoins, username))
   //                        output += Module.SendMessageBreak + "Unfortunately, the ChatCoin module seems to be malfunctioning, so you will not receive coins";

                  mainOutput.broadcast = true;
                  mainOutput.message = "Hey, " + user.Username + " has become rank " + collectors[myUID].Stars + "(" + collectors[myUID].StarString() + ") in the cgame!";

                  outputs = QuickQuit(output);
                  outputs.Add(mainOutput);
                  return outputs;

                  //return QuickQuit(output);
   				}
   				else
   				{
                  return QuickQuit("You can't rank up just yet. Complete your journal first!");
   				}
   			}
   			#endregion

   			//View your collection
   			#region mycollection
            else if (cmd == "cgamestock")
   			{
               return QuickQuit("-=Your collection=-\n" + collectors[myUID].Statistics(false, JournalStyle), false, false);
   			}
            else if (cmd == "cgamestockm")
            {
               return QuickQuit("-=Your collection=-\n" + collectors[myUID].Statistics(true, JournalStyle), false, false);
            }
   			#endregion

   			//View your top items
   			#region mytopitems
            else if (cmd == "cgametopitems")
   			{
   				var topItems = collectors[myUID].Inventory.AllItemCounts().ToList().OrderByDescending(x => x.Value).ToList();
   				string output = "The top items in your inventory are: ";

   				for (int i = 0; i < 5; i++)
   					if (i < topItems.Count)
   						output += "\n" + CollectionSymbols.GetPointAndSymbol(topItems[i].Key) + " - " + topItems[i].Value;

               return QuickQuit(output);
   			}
   			#endregion

   			//Get multiple items
   			#region drawitems
   			//match = Regex.Match(message, @"^\s*/cgamedraw\s*([0-9]+)?\s*([xX][0-9]+)?\s*$");
            else if (cmd == "cgamedraw")
   			{
   				int multiplier = 1;
   				int amount;

   				//Oops, bad amount number
   				if (!int.TryParse(command.Arguments[0], out amount))
                  return QuickQuit("The amount of items to draw is invalid", true);
               else if (amount <= 0 || amount > MaxWithdraw)
                  return QuickQuit("You can't withdraw that many items!", true);

               //We had a multiplier!
               if(command.Arguments.Count == 2)
               {
                  if (!int.TryParse(command.Arguments[1].ToLower().Replace("x", ""), out multiplier))
                     return QuickQuit("The multiplier is invalid", true);
                  else if (multiplier > CollectionManager.MaxMultiplier || multiplier < CollectionManager.MinMultiplier)
                     return QuickQuit("Your multiplier is out of range. Use values from " + 
                        CollectionManager.MinMultiplier + "-" + CollectionManager.MaxMultiplier, true);
               }

   				int cost = CollectionManager.LotteryCost * multiplier * amount;

   				//Oops, you don't have enough coins
   				if (collectors[myUID].Coins < cost)
                  return QuickQuit("You don't have enough coins. Drawing " + amount + " item".Pluralify(amount) + " with a multiplier of X" + multiplier + " costs " + cost + " coins.");

   				//Generate a list of items (based on the given amount)
   				List<SpecialPoint> items = new List<SpecialPoint>();
   				List<SpecialPoint> newItems = new List<SpecialPoint>();
   				for (int i = 0; i < amount; i++)
   				{
   					items.Add(generator.DrawItemForPlayer(collectors[myUID], multiplier));
   					if (!collectors[myUID].Journal.HasItem(items[i]))
   						newItems.Add(items[i]);

   					//Make the player "buy" the item
   					collectors[myUID].BuyItem(items[i], cost / amount);
   				}

   				string output = "You spent " + cost + " coins on item".Pluralify(items.Count) + ": ";
   				int newItemCount = 0;

   				//Go through each item and buy it/produce output
   				foreach (SpecialPoint drawnItem in items)
   				{
   					//Determine if the item is new. Update values if so
   					bool newItem = false;

   					if (newItems.Contains(drawnItem))
   					{
   						newItem = true;
   						newItems.RemoveAll(x => x == drawnItem);
   						newItemCount++;
   					}

   					//Update output
   					output += CollectionSymbols.GetPointAndSymbol(drawnItem) + (newItem ? "**" : "") + ", ";
   				}

   				output += ListTerminator + " (" + newItemCount + " new)";

               return QuickQuit(output.Replace(", " + ListTerminator, ""));
   			}
   			#endregion

   			//See the chance of getting a new item
   			#region drawchance
   			//match = Regex.Match(message, @"^\s*/cgamechance\s*([xX][0-9]+)?\s*$");
            else if (cmd == "cgamechance")
   			{
   				int multiplier = 1;

               //Oops, bad multiplier
               if (!int.TryParse(command.Arguments[0].ToLower().Replace("x", ""), out multiplier))
                  return QuickQuit("The multiplier is invalid!", true);
   				else if (multiplier > CollectionManager.MaxMultiplier || multiplier < CollectionManager.MinMultiplier)
                  return QuickQuit("Your multiplier is out of range. Use a value between " + 
                     CollectionManager.MinMultiplier + "-" + CollectionManager.MaxMultiplier, true);

   				//Oops, make sure we're not already using the chance calculator thread
   				if (chanceSimulator.IsBusy)
                  return QuickQuit("The chance calculator is busy right now.");
               //Calculate the chance on a background thread
   				else
   					chanceSimulator.RunWorkerAsync(Tuple.Create(myUID, multiplier));

               return new List<JSONObject>();
   			}
   			#endregion

   			//How much do you have of item (given)
   			#region amount
   			//match = Regex.Match(message, @"^\s*/cgameamount\s+([a-zA-Z][0-9])\s*$");
            else if (cmd == "cgameamount")
   			{
               SpecialPoint point = SpecialPoint.Parse(command.Arguments[0]);

   				if (!CollectionManager.ValidPoint(point))
                  return QuickQuit("That item is out of range.", true);

               return QuickQuit("You have " + collectors[myUID].Inventory[point] + " of item " + CollectionSymbols.GetPointAndSymbol(point));
   			}
   			#endregion

   			//Compare your inventory to another's
   			#region compare
   			//match = Regex.Match(message, @"^\s*/cgamecompare\s+(.+)\s*$");
            else if (cmd == "cgamecompare")
            {
               //Generic error (shouldn't happen, but just in case!)
               if(!GetUserFromArgument(command.Arguments[0], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }

   				//Oops, the user entered the wrong user
               if (!collectors.ContainsKey(player.UID))
                  return QuickQuit(NotAPlayer(player));

               if (player.UID == myUID)
                  return QuickQuit("You can't compare with yourself!", true);

   				//Get the unique journal entries
   				List<SpecialPoint> myUnique, theirUnique;
               GetUnique(myUID, player.UID, out myUnique, out theirUnique);

   				string output = "Note: Only the top 5 are shown, so there may be more.\nOnly items that you actually have in your inventory are shown\n-Your unique items are: ";

   				//List out items that only you have
   				for (int i = 0; i < 5; i++)
   					if (i < myUnique.Count)
   						output += CollectionSymbols.GetPointAndSymbol(myUnique[i]) + " ";

               output += "\n-" + player.Username + " has these unique items: ";

   				//List out items that only the other guy has
   				for (int i = 0; i < 5; i++)
   					if (i < theirUnique.Count)
   						output += CollectionSymbols.GetPointAndSymbol(theirUnique[i]) + " ";

               return QuickQuit(output);
   			}
   			#endregion

   			//Perform a quicktrade with the given user
   			#region quicktrade
   			//match = Regex.Match(message, @"^\s*/cgamequicktrade\s+(.+)\s*$");
            else if (cmd == "cgamequicktrade")
   			{
               if(!GetUserFromArgument(command.Arguments[0], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }
                  
   				//Oops, there is another offer still standing
               try
               {
      				if (offer.OfferStanding)
                     return QuickQuit(OfferStanding(offer, users));
               }
               catch
               {
                  AddError(outputs);
                  return outputs;
               }

   				//Oops, the user entered the wrong person
               if (!collectors.ContainsKey(player.UID))
                  return QuickQuit(NotAPlayer(player));

   				//Oops, can't trade with yourself!
               if (myUID == player.UID)
                  return QuickQuit("You can't trade with yourself!", true);

   				//Get the unique journal entries
   				List<SpecialPoint> myUnique, theirUnique;
               GetUnique(myUID, player.UID, out myUnique, out theirUnique);

   				//Oops, can't have a nice trade because one of you doesn't have any unique items
   				int tradeItems = Math.Min(myUnique.Count, theirUnique.Count);
   				if (tradeItems == 0)
                  return QuickQuit("There cannot be an even trade between you and " + player.Username);

   				myUnique = myUnique.Take(tradeItems).ToList();
   				theirUnique = theirUnique.Take(tradeItems).ToList();

               offer.CreateTrade(myUID, player.UID, myUnique, theirUnique);

               ModuleJSONObject playerOutput = new ModuleJSONObject();
               playerOutput.recipients.Add(player.UID);
               playerOutput.message = user.Username + " wants to perform a quick-trade with you.\nOffered item".Pluralify(myUnique.Count) + ": "
                  + PrintPointList(myUnique) + "\nDesired item".Pluralify(theirUnique.Count) + ": " + PrintPointList(theirUnique) + "\nYou have " + offer.OfferTimeout
                  + " seconds to decide (/cgameaccept or /cgamedecline). Note that you two will get the items in your journal, but the item is used up in the process."
                  + " You will not get the item in your inventory";

               outputs.Add(playerOutput);

               mainOutput.message = "Your trade of " + PrintPointList(myUnique) + " for " + PrintPointList(theirUnique) + " with " + player.Username +
                  " has been sent! You'll receive a message when they accept or decline";

               outputs.Add(mainOutput);

               return outputs;
   			}
   			#endregion

   			//Sell to player
   			#region sellPlayer
            else if (cmd == "cgamesell")
   			{
               if(!GetUserFromArgument(command.Arguments[1], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }

               if(player.UID == myUID)
                  return QuickQuit("You can't sell to yourself!", true);

   				//Set up some initial info
   				List<SpecialPoint> offeredItems;
   				int desiredCoins;

   				//Oops, there is another offer still standing
   				if (offer.OfferStanding)
                  return QuickQuit(OfferStanding(offer, users));

   				//Ugh, bad values
               if (!int.TryParse(command.Arguments[2], out desiredCoins))
                  return QuickQuit("Your sell amount is out of bounds", true);

   				//Oops, the user entered the wrong person
               if (!collectors.ContainsKey(player.UID))
                  return QuickQuit(NotAPlayer(player));

   				//Oops, the other user does not have that many coins
               if (collectors[player.UID].Coins < desiredCoins)
                  return QuickQuit("You don't have " + desiredCoins + " coin".Pluralify(desiredCoins) + " to spend.");

   				//Try to extract the points to sell
   				try
   				{
                  offeredItems = ExtractPoints(command.Arguments[0], user);
   				}
   				catch (Exception e)
   				{
                  return QuickQuit(e.Message, true);
   				}

   				//Create the sale
               offer.CreateSale(myUID, player.UID, desiredCoins, offeredItems);

               ModuleJSONObject playerOutput = new ModuleJSONObject();
               playerOutput.recipients.Add(player.UID);
               playerOutput.message = user.Username + " has offered to sell you " + PrintPointList(offer.SellerItems) + " for " + offer.BuyerCoins + " coin".Pluralify(offer.BuyerCoins) + ".\nYou have " +
                  offer.OfferTimeout + " seconds to decide (/cgameaccept or /cgamedecline). Note that you will get the item in your journal, but NOT your inventory.";

               outputs.Add(playerOutput);

               mainOutput.message = "Your offer of " + PrintPointList(offer.SellerItems) + " for " + offer.BuyerCoins + " coin".Pluralify(offer.BuyerCoins) + 
                  " with " + player.Username + " has been sent! You'll receive a message when they accept or decline";

               outputs.Add(mainOutput);

               return outputs;
   			}
   			#endregion

   			//Buy from player
   			#region buyPlayer
            else if (cmd == "cgamebuy")
   			{
               if(!GetUserFromArgument(command.Arguments[1], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }

   				//Set up some initial info
   				List<SpecialPoint> desiredItems;
   				int offeredCoins;

   				//Oops, there is another offer still standing
   				if (offer.OfferStanding)
                  return QuickQuit(OfferStanding(offer, users), true);

   				//Ugh, bad values
               if (!int.TryParse(command.Arguments[2], out offeredCoins))
                  return QuickQuit("Your buy amount is out of bounds", true);

   				//Oops, the user entered the wrong person
               if (!collectors.ContainsKey(player.UID))
                  return QuickQuit(NotAPlayer(player));

   				//Oops, this user does not have that many coins
               if (collectors[myUID].Coins < offeredCoins)
                  return QuickQuit("You do not have " + offeredCoins + " coin".Pluralify(offeredCoins) + " to spend.");

   				try
   				{
                  desiredItems = ExtractPoints(command.Arguments[0], player);
   				}
   				catch (Exception e)
   				{
                  return QuickQuit(e.Message, true);
   				}

   				//Create the offer
               offer.CreatePurchase(player.UID, myUID, offeredCoins, desiredItems);

               ModuleJSONObject playerOutput = new ModuleJSONObject();
               playerOutput.recipients.Add(player.UID);
               playerOutput.message = user.Username + " has offered to buy " + PrintPointList(offer.SellerItems) + " from you for " + offer.BuyerCoins + " coin".Pluralify(offer.BuyerCoins) + ".\nYou have " +
                  offer.OfferTimeout + " seconds to decide (/cgameaccept or /cgamedecline). ";

               outputs.Add(playerOutput);

               mainOutput.message = "Your offer of " + offer.BuyerCoins + " coin".Pluralify(offer.BuyerCoins) + " for " + PrintPointList(offer.SellerItems) + 
                  " with " + player.Username + " has been sent! You'll receive a message when they accept or decline";

               outputs.Add(mainOutput);

               return outputs;
   			}
   			#endregion

   			//Give away an item
   			#region give
            else if (cmd == "cgamegive")
   			{
               if(!GetUserFromArgument(command.Arguments[1], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }

   				//Set up some initial info
   				//string potentialReceiver = match.Groups[3].Value;
   				List<SpecialPoint> points;

   				try
   				{
                  points = ExtractPoints(command.Arguments[0], user);
   				}
   				catch (Exception e)
   				{
                  return QuickQuit(e.Message, true);
   				}

   				//Oops, the user entered the wrong player
               if (!collectors.ContainsKey(player.UID))
                  return QuickQuit(NotAPlayer(player));

               PerformGive(myUID, player.UID, points);

               ModuleJSONObject playerMessage = new ModuleJSONObject();
               playerMessage.recipients.Add(player.UID);
               playerMessage.message = user.Username + " has given you " + PrintPointList(points) + " for free! How nice!";
               outputs.Add(playerMessage);

               mainOutput.message = "You've given away " + PrintPointList(points) + " to " + player.Username + ". You're nice!";
               outputs.Add(mainOutput);

               return outputs;
   			}
   			#endregion

   			//Trade items with another player
   			#region trade
            else if (cmd == "cgametrade")
   			{
   				//Get the other user
               if(!GetUserFromArgument(command.Arguments[1], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }

   				//Oops, there is another offer still standing
   				if (offer.OfferStanding)
                  return QuickQuit(OfferStanding(offer, users));

   				//Oops, the user entered the wrong person
               if (!collectors.ContainsKey(player.UID))
                  return QuickQuit(NotAPlayer(player));

   				//Oops, can't trade with yourself!
               if (player.UID == myUID)
                  return QuickQuit("You can't trade with yourself!");

   				//Get the points you want to give to the other user
   				List<SpecialPoint> myPoints;
   				List<SpecialPoint> theirPoints;

   				try
   				{
   					myPoints = ExtractPoints(command.Arguments[0], user);
                  theirPoints = ExtractPoints(command.Arguments[2], player);
   				}
   				catch (Exception e)
   				{
                  return QuickQuit(e.Message);
   				}

               offer.CreateTrade(myUID, player.UID, myPoints, theirPoints);

               ModuleJSONObject playerOutput = new ModuleJSONObject();
               playerOutput.recipients.Add(player.UID);
               playerOutput.message = user.Username + " wants to perform a trade with you.\nOffered item".Pluralify(myPoints.Count) + ": "
                  + PrintPointList(myPoints) + "\nDesired item".Pluralify(theirPoints.Count) + ": " + PrintPointList(theirPoints) + "\nYou have " + offer.OfferTimeout
                  + " seconds to decide (/cgameaccept or /cgamedecline). Note that you two will get the items in your journal, but the item is used up in the process."
                  + " You will not get the item in your inventory";

               outputs.Add(playerOutput);

               mainOutput.message = "Your trade of " + PrintPointList(myPoints) + " for " + PrintPointList(theirPoints) + " with " + player.Username +
                  " has been sent! You'll receive a message when they accept or decline";

               outputs.Add(mainOutput);

               return outputs;
   			}

   			#endregion

   			//Sell all of the given items
   			#region sellall
   			//match = Regex.Match(message, @"^\s*/cgamesellall\s+(([a-zA-Z][0-9]\s*)+)\s*$");
            else if (cmd == "cgameshopsell")
   			{
   				//Build up the list of captured points
   				List<SpecialPoint> points;

   				try
   				{
                  points = ExtractPoints(command.Arguments[0], user);
   				}
   				catch (Exception e)
   				{
                  return QuickQuit(e.Message, true);
   				}

   				int totalAmount = 0;

   				//Now for the actual selling
   				foreach (SpecialPoint point in points)
   				{
   					//Get the total amount of that item we can sell
   					int pointAmount = collectors[myUID].Inventory[point];
   					//totalAmount += pointAmount;

   					//Sell it all
   					for (int i = 0; i < pointAmount; i++)
                  {
                     int sellPrice = CollectionManager.SellPrice(point, collectors[myUID]);
                     collectors[myUID].SellItem(point, sellPrice);
                     totalAmount += sellPrice;
                  }
   				}

               return QuickQuit("You sold all " + PrintPointList(points) + " for " + totalAmount + " coins.");
   			}
   			#endregion

   			//Sell ALL of the items
   			#region sellallall
            if (cmd == "cgameshopsellall") //Regex.IsMatch(message, @"^\s*/cgamesellallall\s*$"))
   			{
   				int totalAmount = 0;

   				//Now for the actual selling
   				foreach (SpecialPoint point in collectors[myUID].Inventory.AllItemCounts().Where(x => x.Value > 0).Select(x => x.Key).ToList())
   				{
   					//Get the total amount of that item we can sell
   					int pointAmount = collectors[myUID].Inventory[point];
   					//totalAmount += pointAmount;

   					//Sell it all
   					for (int i = 0; i < pointAmount; i++)
                  {
                     int sellPrice = CollectionManager.SellPrice(point, collectors[myUID]);
                     collectors[myUID].SellItem(point, sellPrice);
                     totalAmount += sellPrice;
                  }
   				}

               return QuickQuit("You sold everything for " + totalAmount + " coins.");
   			}
   			#endregion

   			//Accept a selling offer
   			#region acceptoffer
            else if (cmd == "cgameaccept") //Regex.IsMatch(message, @"^\s*/cgameaccept\s*$"))
            {
   				//Pfft, the sale isn't for you!
   				if (!offer.OfferStanding || myUID != offer.Accepter)
                  return QuickQuit("There are no offers for you right now");

   				string output = "";

   				if (offer.Type == CollectionOffer.OfferType.Buy || offer.Type == CollectionOffer.OfferType.Sell)
   				{
   					//This variable is used so that only the first item in the sale exchanges money. After that, they're basically a trade
   					bool firstItem = true;

   					foreach (SpecialPoint sellItem in offer.SellerItems)
   					{
   						//Complete the sale
   						collectors[offer.Seller].SellItem(sellItem, (firstItem ? offer.BuyerCoins : 0));
   						collectors[offer.Buyer].BuyItem(sellItem, (firstItem ? offer.BuyerCoins : 0));
   						collectors[offer.Buyer].Inventory.UnobtainItem(sellItem);

   						firstItem = false;
   					}

   					output = users[offer.Buyer].Username + " has bought a journal entry for " + PrintPointList(offer.SellerItems) + " from " + users[offer.Seller].Username + " for " + offer.BuyerCoins + " coin".Pluralify(offer.BuyerCoins) + ".";
   				}
   				else if (offer.Type == CollectionOffer.OfferType.Trade)
   				{
   					output = users[offer.Offerer].Username + " gave these items to " + users[offer.Accepter].Username + ": \n";

   					//Perform the trade
   					foreach (SpecialPoint item in offer.OffererItems)
   					{
   						output += CollectionSymbols.GetPointAndSymbol(item) + " ";
   						collectors[offer.Offerer].SellItem(item, 0);
   						collectors[offer.Accepter].BuyItem(item, 0);
   						collectors[offer.Accepter].Inventory.UnobtainItem(item);
   					}
   					output += "\nAnd got these items in return: \n";
   					foreach (SpecialPoint item in offer.AccepterItems)
   					{
   						output += CollectionSymbols.GetPointAndSymbol(item) + " ";
   						collectors[offer.Accepter].SellItem(item, 0);
   						collectors[offer.Offerer].BuyItem(item, 0);
   						collectors[offer.Offerer].Inventory.UnobtainItem(item);
   					}
   				}

               mainOutput.message = output;
               mainOutput.recipients.Add(offer.Accepter);
               mainOutput.recipients.Add(offer.Offerer);
               outputs.Add(mainOutput);

   				//Reset the sell information
   				offer.Reset();

   				//We done yo.
   				return outputs;
   			}
   			#endregion

   			//Decline a selling offer
   			#region declineoffer
            else if (cmd == "cgamedecline")//Regex.IsMatch(message, @"^\s*/cgamedecline\s*$"))
   			{
   				//Pfft, the sale isn't for you!
   				if (!offer.OfferStanding || myUID != offer.Accepter)
                  return QuickQuit("There are no offers for you right now");

               ModuleJSONObject playerOutput = new ModuleJSONObject();
               playerOutput.message = user.Username + " has declined the offer";
               playerOutput.recipients.Add(offer.Offerer);
               outputs.Add(playerOutput);

               mainOutput.message = "You have declined the offer";
               outputs.Add(mainOutput);
   				
   				offer.Reset();

   				return outputs;
   			}
   			#endregion

   			//View your forever statistics (like total coins, total items collected, etc.
   			#region cgameforeverstats
            else if (cmd == "cgamestats")//Regex.IsMatch(message, @"^\s*/cgamestats\s*$"))
   			{
   				if (collectors[myUID].Stars < 1)
                  return QuickQuit("You must be at least rank 1 to see your permanent collection game stats");

               return QuickQuit(collectors[myUID].ForeverStats());
   			}
   			#endregion

   			#region cgamequery
            //match = Regex.Match(message, @"^\s*/cgamequery\s+(([a-zA-Z][0-9]\s*)+)\s*$");
            if (cmd == "cgamequery")//match.Success)
   			{
               List<SpecialPoint> points;

               try
   				{
                  points = ExtractPoints(command.Arguments[0]).Distinct().ToList();
   				}
   				catch (Exception e)
   				{
                  return QuickQuit(e.Message, true);
   				}

               return QuickQuit(GetQuery(points, users));
   			}

            if(cmd == "cgamequeryall")
            {
               List<SpecialPoint> points = collectors[myUID].Journal.Unobtained();

               return QuickQuit(GetQuery(points, users));
            }
   			#endregion

   			#region cgamelove
   			//match = Regex.Match(message, @"^\s*/cgamelove\s+(.+)\s*$");
            else if (cmd == "cgamelove") //match.Success)
   			{
               if(CheckIfJealous(myUID, out outputs))
                  return outputs;
               
               //Get the other user
               if(!GetUserFromArgument(command.Arguments[0], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }

               return PerformLove(user, player);
   			}
   			#endregion

            #region cgameaddlove
            //match = Regex.Match(message, @"^\s*/cgameaddlove\s+(.+)\s*$");
            else if(cmd == "cgameaddlove")//match.Success)
            {
               if(CheckIfJealous(myUID, out outputs))
                  return outputs;
               
               //Get the other user
               if(!GetUserFromArgument(command.Arguments[0], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }

               if (!collectors.ContainsKey(player.UID))
                  return QuickQuit(NotAPlayer(player));

               if (myUID == player.UID)
                  return QuickQuit("You can't add yourself to the love list!", true);

               if (collectors[myUID].GetLovers().Count >= MaxLovers)
                  return QuickQuit("You're already at your love limit (" + MaxLovers + ")");

               if (!collectors[myUID].AddLover(player.UID))
                  return QuickQuit(player.Username + " is already in your love list!");

               return QuickQuit("You've added " + player.Username + " to your love list <3");
            }
            #endregion

            #region cgameremovelove
            //match = Regex.Match(message, @"^\s*/cgameremovelove\s+(.+)\s*$");
            else if (cmd == "cgameremovelove")//match.Success)
            {
               if(CheckIfJealous(myUID, out outputs))
                  return outputs;
               
               //Get the other user
               if(!GetUserFromArgument(command.Arguments[0], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }

               if (!collectors[myUID].RemoveLover(player.UID))
                  return QuickQuit(player.Username + " is not in your love list!");

               return QuickQuit("You've removed " + player.Username + " from your love list </3");
            }
            #endregion

            #region cgamelovelist
            else if (cmd == "cgamelovelist")//Regex.IsMatch(message, @"^\s*/cgamelovelist\s*$"))
            {
               if(CheckIfJealous(myUID, out outputs))
                  return outputs;
               
               List<int> lovers = collectors[myUID].GetLovers();

               if(lovers.Count == 0)
               return QuickQuit("There is nobody on your love list ='(");

               string output = "These are all the people on your love list:";

               foreach (int lover in lovers)
                  if(users.ContainsKey(lover))
                     output += "\n* " + users[lover].Username;

               return QuickQuit(output);
            }
            #endregion

            #region cgameloveall
            if (cmd == "cgameloveall")//Regex.IsMatch(message, @"^\s*/cgameloveall\s*$"))
            {
               if(CheckIfJealous(myUID, out outputs))
                  return outputs;
               
               List<Tuple<int, int>> giveCounts = new List<Tuple<int,int>>();
               List<int> lovers = collectors[myUID].GetLovers().Where(x => collectors.ContainsKey(x) && users.ContainsKey(x)).ToList();

               if (lovers.Count == 0)
                  return QuickQuit("There is nobody on your love list ='(");

               foreach (int lover in lovers)
               {
                  List<SpecialPoint> myUnique, theirUnique;
                  GetUnique(myUID, lover, out myUnique, out theirUnique);
                  giveCounts.Add(Tuple.Create(lover, myUnique.Count));
               }

               if(giveCounts.Any(x => x.Item2 == 0))
               {
                  mainOutput.message = "Love-all skipped: " + string.Join(", ", giveCounts.Where(x => x.Item2 == 0).Select(x => users[x.Item1].Username)) + "\n";
                  outputs.Add(mainOutput);
               }

               foreach (int lover in giveCounts.Where(x => x.Item2 != 0).OrderBy(x => x.Item2).Select(x => x.Item1))
                  outputs.AddRange(PerformLove(user, users[lover]));

               return outputs;
            }
            #endregion

            if(cmd == "cgamecheckjealousy")
            {
               if(CheckIfJealous(myUID, out outputs))
                  return outputs;

               var canSteal = GetJealousy(myUID);

               return QuickQuit("If you tap into the darkness in your twisted heart now, you'll get " + canSteal.Count + " item".Pluralify(canSteal.Count) +
                  " plus " + JealousyCoins * users.Count(x => x.Value.LoggedIn) + " coins (1000 coins for each logged in user)");
            }

            if(cmd == "cgamejealousy")
            {
               if(CheckIfJealous(myUID, out outputs))
                  return outputs;

               Dictionary<SpecialPoint, int> gonnaSteal = GetJealousy(myUID);
               HashSet<int> usersToInform = new HashSet<int>();

               if(gonnaSteal.Count == 0)
                  return QuickQuit("Your jealousy couldn't change the fact that nobody had any items you needed");

               foreach(KeyValuePair<SpecialPoint, int> steal in gonnaSteal)
               {
                  PerformGive(steal.Value, myUID, new List<SpecialPoint> { steal.Key });
                  usersToInform.Add(steal.Value);
               }

               double multiplier = 0.5 + 1.5 * collectors.ToList().OrderBy(x => x.Value.Score()).Select(x => x.Key).ToList().IndexOf(myUID) / (double)collectors.Count;
               collectors[myUID].JealousyPerformed(gonnaSteal.Count, multiplier);

               foreach(int userID in usersToInform)
               {
                  ModuleJSONObject userOutput = new ModuleJSONObject();
                  int itemCount = gonnaSteal.Count(x => x.Value == userID);
                  userOutput.message = user.Username + " took " + itemCount + " item".Pluralify(itemCount) + " from you!";
                  userOutput.recipients.Add(userID);
                  outputs.Add(userOutput);
               }

               int getCoins = JealousyCoins * users.Count(x => x.Value.LoggedIn);
               collectors[myUID].GetCoins(getCoins);

               //WarningJSONObject warnOutput = new WarningJSONObject();
               mainOutput.message = "In a fit of jealousy, " + user.Username + " used the darkness in their twisted heart to " +
                  "generate " + getCoins + " coins and steal " + gonnaSteal.Count + " item".Pluralify(gonnaSteal.Count) + 
                  " from unsuspecting users. 8(>_<)8";
               mainOutput.broadcast = true;
               outputs.Add(mainOutput);

               return outputs;
            }

            if(cmd == "cgamehug")
            {
               if(CheckIfJealous(myUID, out outputs))
                  return outputs;

               //Get the other user
               if(!GetUserFromArgument(command.Arguments[0], users, out player))
               {
                  AddError(outputs);
                  return outputs;
               }

               if (!collectors.ContainsKey(player.UID))
                  return QuickQuit(NotAPlayer(player));

               if(myUID == player.UID)
                  return QuickQuit("You can't hug yourself!", true);

               if(CollectionManager.PerformHug(collectors, myUID, player.UID, JealousyTime))
               {
                  ModuleJSONObject playerOutput = new ModuleJSONObject();
                  playerOutput.recipients.Add(player.UID);
                  playerOutput.message = "You were hugged by " + user.Username + ". The darkness in your heart receded a bit!";
                  mainOutput.message = "You gave " + player.Username + " a hug and cheered them up a bit!";
                  outputs.Add(mainOutput);
                  outputs.Add(playerOutput);
                  return outputs;
               }
               else
               {
                  ModuleJSONObject playerOutput = new ModuleJSONObject();
                  playerOutput.recipients.Add(player.UID);
                  playerOutput.message = "You were hugged by " + user.Username + ". " + HugSayings[localRandom.Next(HugSayings.Count)];
                  mainOutput.message = "You gave " + player.Username + " a hug. What a nice person!";
                  outputs.Add(mainOutput);
                  outputs.Add(playerOutput);
                  return outputs;
               }
            }
         }
         catch(Exception e)
         {
            ModuleJSONObject error = new ModuleJSONObject();
            error.broadcast = true;
            error.message = "The CGAME module has encountered an unknown error. Please tell staff\n\n" +
            "Error message: " + e.ToString();

            return new List<JSONObject> { error };
         }

         return new List<JSONObject>();
		}

      public string GetQuery(List<SpecialPoint> points, Dictionary<int, UserInfo> users)
      {
         Dictionary<int, List<SpecialPoint>> whoHasPoints = new Dictionary<int,List<SpecialPoint>>();

         foreach (var collector in collectors)
         {
            whoHasPoints.Add(collector.Key, new List<SpecialPoint>());
            foreach (SpecialPoint point in points)
            {
               if (collector.Value.Inventory.HasItem(point))
                  whoHasPoints[collector.Key].Add(point);
            }
         }

         var whoReallyHasPoints = whoHasPoints.ToList().Where(x => x.Value.Count > 0).OrderByDescending(x => x.Value.Count).ToList();

         if (whoReallyHasPoints.Count == 0)
            return "Nobody has item".Pluralify(points.Count) + " " + PrintPointList(points);

         string output = "These people have item".Pluralify(points.Count) + " " + PrintPointList(points) + ":";
         for (int i = 0; i < Math.Min(whoReallyHasPoints.Count, 5); i++)
            output += "\n* " + users[whoReallyHasPoints[i].Key].Username + " - " + PrintPointList(whoReallyHasPoints[i].Value);

         return output;
      }

      public Dictionary<SpecialPoint, int> GetJealousy(int uid)
      {
         Dictionary<SpecialPoint, int> gonnaSteal = new Dictionary<SpecialPoint, int>();

         foreach(SpecialPoint point in collectors[uid].RareList.Select(x => CollectionManager.PointFrom1DIndex(x)))
         {
            //we don't care about items we already have
            if(collectors[uid].Journal.HasItem(point))
               continue;

            try
            {
               gonnaSteal.Add(point, collectors.Where(x => x.Value.Inventory[point] >= 1).OrderByDescending(x => x.Value.Inventory[point]).First().Key);
            }
            catch
            {
               continue;
            }
         }

         return gonnaSteal;
      }

      public bool CheckIfJealous(int uid, out List<JSONObject> outputs)
      {
         outputs = new List<JSONObject>();

         if (!collectors.ContainsKey(uid))
            return false;

         if (collectors[uid].JealousTimeLeft.Ticks <= 0)
            return false;

         outputs = QuickQuit("Your twisted heart is still filled with hatred and jealousy. You should be fine in " +
            StringExtensions.LargestTime(collectors[uid].JealousTimeLeft));
         return true;
      }

      public List<JSONObject> PerformLove(UserInfo user, UserInfo player)
      {
         List<JSONObject> outputs = new List<JSONObject>();

         if (CheckIfJealous(player.UID, out outputs))
            return QuickQuit("The intense dark aura surrounding " + player.Username + " prevents you from loving them :'(");
         
         //Oops, the user entered the wrong player
         if (!collectors.ContainsKey(player.UID))
            return QuickQuit(NotAPlayer(player));

         List<SpecialPoint> myUnique, theirUnique;
         GetUnique(user.UID, player.UID, out myUnique, out theirUnique);

         if (myUnique.Count == 0)
            return QuickQuit("It was a nice gesture, but you don't have any unique items to give to " + player.Username + ".");

         PerformGive(user.UID, player.UID, myUnique);
         collectors[user.UID].LoveGiven(myUnique.Count);
         collectors[player.UID].GotLoved(myUnique.Count);

         ModuleJSONObject playerOutput = new ModuleJSONObject();
         playerOutput.recipients.Add(player.UID);
         playerOutput.message = user.Username + " generously gave you " + myUnique.Count + " item".Pluralify(myUnique.Count) + 
            " - " + PrintPointList(myUnique);
         outputs.Add(playerOutput);

         ModuleJSONObject mainOutput = new ModuleJSONObject();
         mainOutput.message = "You generously gave " + myUnique.Count + " item".Pluralify(myUnique.Count) + 
            " to " + player.Username + " - " + PrintPointList(myUnique);
         outputs.Add(mainOutput);

         return outputs;
      }
         

//		public override List<string> ProcessInterModuleCommunication()
//		{
//			List<string> output = new List<string>();
//			int newRegistrations = 0;
//
//			foreach (ModuleMessagePackage message in PullModuleMessages())
//			{
//				string user = message.GetPart<string>("user");
//				int coins = message.GetPart<int>("coins");
//				bool exchange = message.GetPart<bool>("exchange");
//
//				//If we receive a message from the User module, update the list of users with the given information
//				if (TryRegister(user))
//					newRegistrations++;
//
//                //Oh no, a swap user thing
//                if (message.ResponseType == ModuleMessage.Responses.SwapUser)
//                {
//                    string user1 = message.GetPart<string>("user1");
//                    string user2 = message.GetPart<string>("user2");
//
//                    if (!collectors.ContainsKey(user1) || !collectors.ContainsKey(user2))
//                    {
//                        output.Add(StandardCalls_SwapUserFailOutput());
//                    }
//                    else
//                    {
//                        CollectionPlayer tempData = collectors[user1];
//                        collectors[user1] = collectors[user2];
//                        collectors[user2] = tempData;
//                        output.Add(StandardCalls_SwapUserSuccessOutput(user1, user2));
//                    }
//                }
//
//                //User removal!
//                if (message.ResponseType == ModuleMessage.Responses.Remove)
//                {
//                    if (collectors.Remove(user))
//                        output.Add(StandardCalls_RemoveUserSuccessOutput(user));
//                }
//
//                //Someone's requesting a restock
//                if (message.SendModule.Name.Contains("Restock"))
//                {
//                    if (!string.IsNullOrWhiteSpace(message.GetPart<string>("restock")))
//                        output.Add(ProcessIncoming(new ChatChunk(user, "0", "/cgamerestock"), new ChatInfo()));
//                }
//
//				//We receive a lot of stuff from the ChatCoins module
//				if (message.SendModule.Name.Contains("ChatCoins"))
//				{
//					//Success acknowledgement from chatcoins for ranking up
//					if(message.ResponseType == ModuleMessage.Responses.Success && coins == RankupCoins)
//						output.Add(user + ", you've received " + coins + " chat coins from ranking up!");
//					//If the chatcoins module is polling us, that means it's looking for exchange data
//					else if(message.ResponseType == ModuleMessage.Responses.Poll)
//						output.Add(NickName + " exchange rate: 1 - " + ExchangeRate);
//
//					//The chatcoins module is trying to perform an exchange with us. 
//					if (exchange)
//					{
//						collectors[user].GetCoins(ExchangeRate * coins);
//						StandardCalls_AcknowledgeExchange(coins, user);
//						output.Add(user + ", you've received " + ExchangeRate * coins + " " + NickName + " coins!");
//					}
//				}
//			}
//
//			if (newRegistrations > 0)
//				StatusUpdate(ModuleName + " registered " + newRegistrations + " new users");
//
//			return output;
//		}

      public override string Nickname
      {
         get
         {
            return "cgame";
         }
      }

		// For a given player, get their quick information (used for "top 5" lists and junk)
		public string QuickInfo(UserInfo user)
		{
         if (!collectors.ContainsKey(user.UID))
				return "";

         return user.Username + " " + collectors[user.UID].StarString() + " " + collectors[user.UID].Journal.CompletionCount() + "% ";
		}

      public List<SpecialPoint> ExtractPoints(string points)
      {
         List<SpecialPoint> thePoints = new List<SpecialPoint>();
         foreach (Match match in Regex.Matches(points, @"[a-zA-Z][0-9]"))
             thePoints.Add(SpecialPoint.Parse(match.Groups[0].Value));

         foreach (SpecialPoint point in thePoints.Distinct())
         {
             if (!CollectionManager.ValidPoint(point))
                 throw new Exception("Error: point " + point + " is out of range.");
         }

         return thePoints;
      }

		public List<SpecialPoint> ExtractPoints(string points, UserInfo player)
		{
         List<SpecialPoint> thePoints = ExtractPoints(points);

			//Now, let's make sure that the base user has these items. This will look at the counts of each
			//unique item in the list of items to trade, so you can place multiple instances of the same
			//item in your list and it should detect the correct amount. It will also check for the 
			//validity of the point
			foreach (SpecialPoint point in thePoints.Distinct())
			{
            if (thePoints.Count(x => x.Equals(point)) > collectors[player.UID].Inventory[point])
               throw new Exception(player.Username + " doesn't have enough " + CollectionSymbols.GetPointAndSymbol(point) + " for this command.");
			}

			return thePoints;
		}

		public string PrintPointList(List<SpecialPoint> points)
		{
			string output = "";

			foreach (SpecialPoint point in points)
				output += CollectionSymbols.GetPointAndSymbol(point) + " ";

			return output.Trim();
		}

		public void GetUnique(int user, int other, out List<SpecialPoint> userUnique, out List<SpecialPoint> otherUnique)
		{
         if (!collectors.ContainsKey(user) || !collectors.ContainsKey(other))
         {
             userUnique = new List<SpecialPoint>();
             otherUnique = new List<SpecialPoint>();
             ExtraCommandOutput(QuickQuit("A serious error has occurred with the previous command. Please report the command to staff!", true), user);
             return;
         }

			userUnique = collectors[user].Journal.Compare(collectors[other].Journal).Where(x => collectors[user].Inventory.HasItem(x)).ToList();
			otherUnique = collectors[other].Journal.Compare(collectors[user].Journal).Where(x => collectors[other].Inventory.HasItem(x)).ToList();
			userUnique.Sort((x, y) => -collectors[user].Inventory[x].CompareTo(collectors[user].Inventory[y]));
			otherUnique.Sort((x, y) => -collectors[other].Inventory[x].CompareTo(collectors[other].Inventory[y]));
		}

		public void PerformGive(int giver, int receiver, List<SpecialPoint> items)
		{
			foreach (SpecialPoint point in items)
			{
				//Give the item away
				collectors[giver].SellItem(point, 0);						//Remove the item
				collectors[receiver].BuyItem(point, 0);						//Get the item in the given person's journal
				collectors[receiver].Inventory.UnobtainItem(point);			//Remove the physical item
			}
		}

	}

	#region Extraclasses
	/// <summary>
	/// Represents an offer of any type (trading, buying, selling) between two people
	/// </summary>
	public class CollectionOffer
	{
		public enum OfferType
		{
			Trade, Buy, Sell, None
		}

		private int offerer = 0;
		private int accepter = 0;
		private int offererCoins = 0;
		private int accepterCoins = 0;
		private List<SpecialPoint> offererItems = new List<SpecialPoint>();
		private List<SpecialPoint> accepterItems = new List<SpecialPoint>();
		private Stopwatch offerTimer = new Stopwatch();
		private int offerTimeout = 0;
		private OfferType type;

		/// <summary>
		/// Initialize a CollectionOffer object with the given timeout.
		/// </summary>
		/// <param name="timeout"></param>
		public CollectionOffer(int timeout)
		{
			offerTimeout = timeout;
			Reset();
		}

		/// <summary>
		/// Clear the offer
		/// </summary>
		public void Reset()
		{
			offerTimer.Reset();
			type = OfferType.None;
			offerer = 0;
			accepter = 0;
			offererCoins = 0;
			accepterCoins = 0;
			offererItems.Clear();
			accepterItems.Clear();
		}

		/// <summary>
		/// Set up this collection offer to be a sale
		/// </summary>
		/// <param name="seller"></param>
		/// <param name="buyer"></param>
		/// <param name="desiredCoins"></param>
		/// <returns></returns>
		public void CreateSale(int seller, int buyer, int desiredCoins, List<SpecialPoint> offeredItems)
		{
			Reset();

			type = OfferType.Sell;
			offerer = seller;
			accepter = buyer;
			accepterCoins = desiredCoins;

			foreach (SpecialPoint offeredItem in offeredItems)
				offererItems.Add(offeredItem);

			offerTimer.Start();
		}

		/// <summary>
		/// Set up this collection offer to be a purchase
		/// </summary>
		/// <param name="seller"></param>
		/// <param name="buyer"></param>
		/// <param name="desiredCoins"></param>
		/// <returns></returns>
		public void CreatePurchase(int seller, int buyer, int offeredCoins, List<SpecialPoint> desiredItems)
		{
			Reset();

			type = OfferType.Buy;
			offerer = buyer;
			accepter = seller;
			offererCoins = offeredCoins;

			foreach (SpecialPoint desiredItem in desiredItems)
				accepterItems.Add(desiredItem);

			offerTimer.Start();
		}

		/// <summary>
		/// Set up a trade between the given people for the given items
		/// </summary>
		/// <param name="tradeOfferer"></param>
		/// <param name="tradeAccepter"></param>
		/// <param name="tradeOffererItems"></param>
		/// <param name="tradeAccepterItems"></param>
		public void CreateTrade(int tradeOfferer, int tradeAccepter, List<SpecialPoint> tradeOffererItems, List<SpecialPoint> tradeAccepterItems)
		{
			Reset();

			type = OfferType.Trade;
			offerer = tradeOfferer;
			accepter = tradeAccepter;
			offererItems.AddRange(tradeOffererItems);
			accepterItems.AddRange(tradeAccepterItems);
			offerTimer.Start();
		}

		public int Seller
		{
			get
			{
				if (type == OfferType.Sell)
					return offerer;
				else if (type == OfferType.Buy)
					return accepter;
				else
					return 0;
			}
		}
		public int Buyer
		{
			get
			{
				if (type == OfferType.Buy)
					return offerer;
				else if (type == OfferType.Sell)
					return accepter;
				else
					return 0;
			}
		}
		public List<SpecialPoint> SellerItems
		{
			get
			{
				if (type == OfferType.Sell)
					return offererItems;
				else if (type == OfferType.Buy)
					return accepterItems;
				else
					return null;
			}
		}
		public int BuyerCoins
		{
			get
			{
				if (type == OfferType.Sell)
					return accepterCoins;
				else if (type == OfferType.Buy)
					return offererCoins;
				else
					return -1;
			}
		}
		public int Offerer
		{
			get { return offerer; }
		}
		public int Accepter
		{
			get { return accepter; }
		}
		public List<SpecialPoint> OffererItems
		{
			get { return offererItems; }
		}
		public List<SpecialPoint> AccepterItems
		{
			get { return accepterItems; }
		}
		public bool OfferStanding
		{
			get { return offerTimer.IsRunning && offerTimer.Elapsed.TotalSeconds < offerTimeout; }
		}
		public int OfferTimeout
		{
			get { return offerTimeout; }
		}
		public int RemainingSeconds
		{
			get { return (int)(offerTimeout - offerTimer.Elapsed.TotalSeconds); }
		}
		public OfferType Type
		{
			get { return type; }
		}
	}

	/// <summary>
	/// Used to generate various items for the Collection game
	/// </summary>
	public class CollectionGenerator
	{
		private static readonly Object Locker = new object();
		private Random random;

		/// <summary>
		/// Used only for functions which require a sum and individual generators, such as the DrawChanceForPlayer function
		/// </summary>
		private class ParallelLocal
		{
			public CollectionGenerator generator;
			public int sum;
		}

		/// <summary>
		/// The constructor can be called in a threaded environment due to the fact that the random generator is seeded in a lock
		/// </summary>
		public CollectionGenerator()
		{
			//Seed the random number generator in a lock
			lock (Locker)
			{
				random = new Random(MathExtensions.UniformRandom(int.MaxValue));
			}
		}

		/// <summary>
		/// Returns an index which should be used to index into a player's random list.
		/// </summary>
		/// <returns></returns>
		public int GenerateItemIndex()
		{
			int itemIndex = CollectionManager.TotalGridSpace;

			do
			{
				itemIndex = (int)Math.Floor(MathExtensions.ExponentialRandom(CollectionManager.LambdaCurve, random) / CollectionManager.CurveScale * CollectionManager.TotalGridSpace);
			} while (itemIndex >= CollectionManager.TotalGridSpace);

			return itemIndex;
		}

      public static double ItemRarity(SpecialPoint item, CollectionPlayer player)
      {
         if (!CollectionManager.ValidPoint(item))
            return 0;
         
         int index = player.RareList.Select(x => CollectionManager.PointFrom1DIndex(x)).ToList().IndexOf(item);

         if(index < 0)
            return 0;

         double totalRarity = CumulativeRarity(CollectionManager.TotalGridSpace);

         return (CumulativeRarity(index + 1) - CumulativeRarity(index)) / totalRarity;
      }

      private static double CumulativeRarity(int x)
      {
         return 1 - Math.Pow(Math.E, -CollectionManager.LambdaCurve * x * CollectionManager.CurveScale / CollectionManager.TotalGridSpace);
      }

		/// <summary>
		/// Based on the player and multiplier given, draw an item. This method does NOT add the item to the player; it just gives
		/// a random item based on the player. Basically a wrapper around GenerateItemIndex
		/// </summary>
		/// <param name="player"></param>
		/// <param name="multiplier"></param>
		/// <returns></returns>
		public SpecialPoint DrawItemForPlayer(CollectionPlayer player, int multiplier)
		{
			multiplier--;

			int newItem = GenerateItemIndex();

			List<int> seenPoints = new List<int>();
			seenPoints.Add(newItem);

			//Apply the rarity increase multiplier.
			for (int i = 0; i < multiplier * CollectionManager.MultiplyMultiplier; i++)
			{
				//Oh, but if it's a new item, just return it immediately
				if (!player.Journal.HasItem(CollectionManager.PointFrom1DIndex(player.RareList[newItem])))
					break;

				//While we've already generated the given item, keep on generating. This might have to go through like 1000 iterations, but whatever.
				while (seenPoints.Contains(newItem))
					newItem = GenerateItemIndex();

				seenPoints.Add(newItem);
			}

			return CollectionManager.PointFrom1DIndex(player.RareList[newItem]);
		}

		/// <summary>
		/// Get the chance of getting a new item for a given player and a given multiplier
		/// </summary>
		/// <param name="player"></param>
		/// <param name="multiplier"></param>
		/// <returns></returns>
		public double DrawChanceForPlayer(CollectionPlayer player, int multiplier)
		{
			int newitems = 0;

			for (int i = 0; i < CollectionManager.DrawChanceSimulations; i++)
				if (!player.Journal.HasItem(DrawItemForPlayer(player, multiplier)))
					newitems++;

			return (double)newitems / CollectionManager.DrawChanceSimulations;
		}

		/// <summary>
		/// Get the chance of getting a new item for a given player and a given multiplier, but in parallel
		/// </summary>
		/// <param name="player"></param>
		/// <param name="multiplier"></param>
		/// <returns></returns>
		public static double DrawChanceForPlayerParallel(CollectionPlayer player, int multiplier)
		{
			int newitems = 0;

			Parallel.For(0, CollectionManager.DrawChanceSimulations, () => new ParallelLocal { generator = new CollectionGenerator(), sum = 0 }, (i, loop, local) =>
			{
				if (!player.Journal.HasItem(local.generator.DrawItemForPlayer(player, multiplier)))
					local.sum++;

				return local;
			},

				partial => Interlocked.Add(ref newitems, partial.sum)
			);

			return (double)newitems / CollectionManager.DrawChanceSimulations;
		}
	}

	/// <summary>
	/// Contains constants and special static functions to help with managing the Collection system.
	/// This class is most likely referenced by all other Collection classes.
	/// </summary>
	public static class CollectionManager
	{
		//Dimensions
		public const int Width = 20;
		public const int Height = 5;
		public const int TotalGridSpace = Width * Height;

		//Shop and game flow constants
		public const int CoinRestockAmount = 1000;
		public const int LotteryCost = 50;
		public const int CollectionCoinIncrease = 40;
		public const int IndividualSellPrice = 20;
      public const int ExtraSellPrice = 130;
		public const int RestockHours = 1;
		public const int OfferTimeout = 60;
		public const int MultiplyMultiplier = 4;
		public const int MaxMultiplier = 15;
		public const int MinMultiplier = 1;
      public const double HugReduction = 1.0 / 4.0;

		//Random number generation
		public const int DrawChanceSimulations = 10000;
		public const double LambdaCurve = 2.0;
		public const double CurveScale = 4.0;
		public const double BaseDotChance = 1 / 500.0;
		public const double DotChanceCurveEase = 10.0;

		//Star constants
		public static readonly List<string> StarTypes = new List<string> { "☆", "★", "♚", "♛", "♆" };
		public const int StarRollover = 3;

		/// <summary>
		/// The SpecialPoint class is a very general class, so we need to make sure it falls within our collection guidelines
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		public static bool ValidPoint(SpecialPoint point)
		{
			return (point != null && point.Across >= 0 && point.Across < Width && point.Down >= 0 && point.Down < Height);
		}

		/// <summary>
		/// Convert a one dimensional index into a SpecialPoint
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public static SpecialPoint PointFrom1DIndex(int index)
		{
			return new SpecialPoint(index % Width, index / Width);
		}

		/// <summary>
		/// Used for testing journal output, this creates a fully complete journal
		/// </summary>
		/// <returns></returns>
		public static CollectionJournal CompleteJournal()
		{
			CollectionJournal journal = new CollectionJournal();

			for (int i = 0; i < Width; i++)
				for (int j = 0; j < Height; j++)
					journal.ObtainItem(new SpecialPoint(i, j));

			return journal;
		}

      public static int SellPrice(SpecialPoint item, CollectionPlayer player)
      {
         return (int)(IndividualSellPrice + ExtraSellPrice * (player.RareList.Select(x => CollectionManager.PointFrom1DIndex(x)).ToList().IndexOf(item) / (double)CollectionManager.TotalGridSpace));
//         double leastRare = CollectionGenerator.ItemRarity(CollectionManager.PointFrom1DIndex(player.RareList[0]), player);
//
//         return (int)(IndividualSellPrice + ExtraSellPrice * (1 - CollectionGenerator.ItemRarity(item, player) / leastRare));
      }

      public static bool PerformHug(Dictionary<int, CollectionPlayer> players, int hugger, int reciever, TimeSpan JealousyTime)
      {
         if (players[reciever].TryHug(hugger))
         {
            players[hugger].GaveHug();
            //players[reciever].ReduceJealousy(new TimeSpan((long)((JealousyTime - (DateTime.Now - players[reciever].LastJealousy)).Ticks * HugReduction)));
            return true;
         }

         return false;
      }
	}

	/// <summary>
	/// A player instance in the Collection game
	/// </summary>
	[Serializable()]
	public class CollectionPlayer
	{
		private CollectionStorage inventory = new CollectionStorage();
		private CollectionJournal journal = new CollectionJournal();
		private CollectionJournal foreverJournal = new CollectionJournal();
		private List<int> rareList = new List<int>();
		private int coins = CollectionManager.CoinRestockAmount;
		private int totalCoinsCollected = CollectionManager.CoinRestockAmount;
		private int stars = 0;
		private DateTime lastRestock = DateTime.Now;
      //private DateTime lastJealousy = new DateTime(0);
      private DateTime jealousUntil = new DateTime(0);
      private HashSet<int> huggers = new HashSet<int>();

      //Extra stuff to keep track of
      private int loveGiven = 0;
      private int loveItemsGiven = 0;
      private int loveReceived = 0;
      private int loveItemsReceived = 0;
      private int jealousyCount = 0;
      private int jealousyTaken = 0;
      private int hugsGiven = 0;
      private int hugsReceived = 0;

      [OptionalField]
      private List<int> loveList;

      [OnDeserializing]
      private void SetDefaults(StreamingContext sc)
      {
         ResetOptionals();
      }
      private void ResetOptionals()
      {
         loveList = new List<int>();
      }

		public CollectionPlayer()
		{
			ResetRarities();
            ResetOptionals();
		}

		/// <summary>
		/// Copy constructor
		/// </summary>
		/// <param name="copyPlayer"></param>
		public CollectionPlayer(CollectionPlayer copyPlayer) : this()
		{
			coins = copyPlayer.coins;
			totalCoinsCollected = copyPlayer.totalCoinsCollected;
			stars = copyPlayer.stars;
			lastRestock = copyPlayer.lastRestock;
			inventory = new CollectionStorage(copyPlayer.inventory);
			journal = new CollectionJournal(copyPlayer.journal);
			foreverJournal = new CollectionJournal(copyPlayer.foreverJournal);
         loveList = new List<int>(copyPlayer.loveList);
		}

		/// <summary>
		/// Create a new rarity list
		/// </summary>
		public void ResetRarities()
		{
			rareList.Clear();

			for (int i = 0; i < CollectionManager.TotalGridSpace; i++)
				rareList.Add(i);

			//Knuth shuffle
			for (int i = CollectionManager.TotalGridSpace - 1; i >= 0; i--)
			{
				int randomIndex = MathExtensions.UniformRandom(i);
				int temp = rareList[randomIndex];
				rareList[randomIndex] = rareList[i];
				rareList[i] = temp;
			}
		}

      public void GotLoved(int itemCount)
      {
         loveReceived++;
         loveItemsReceived += itemCount;
      }

      public void LoveGiven(int itemCount)
      {
         loveGiven++;
         loveItemsGiven += itemCount;
      }

      public void JealousyPerformed(int itemCount, double multiplier)
      {
         huggers = new HashSet<int>();
         //lastJealousy = DateTime.Now;
         jealousUntil = DateTime.Now.AddHours(multiplier * (itemCount / 10.0 + journal.CompletionCount() / 10.0));
         jealousyTaken += itemCount;
         jealousyCount++;
      }

      public void GaveHug()
      {
         hugsGiven++;
      }

      public TimeSpan JealousTimeLeft
      {
         get { return (jealousUntil - DateTime.Now); }
      }

      public bool TryHug(int hugger)
      {
         if (jealousUntil > DateTime.Now && huggers.Add(hugger))
         {
            hugsReceived++;
            jealousUntil = jealousUntil.Add(new TimeSpan((long)(JealousTimeLeft.Ticks * -CollectionManager.HugReduction)));
            return true;
         }

         return false;
      }

      public int HugCount
      {
         get { return huggers.Count; }
      }

		/// <summary>
		/// Get your daily coins! Returns false if you can't do it today
		/// </summary>
		/// <returns></returns>
		public bool RestockCoins(DateTime today)
		{
			if ((today - lastRestock).TotalHours < CollectionManager.RestockHours)
				return false;

			GetCoins(RestockCoinsAmount());
			lastRestock = today;

			return true;
		}

		/// <summary>
		/// How many coins do you get per day?
		/// </summary>
		/// <returns></returns>
		public int RestockCoinsAmount()
		{
			return CollectionManager.CoinRestockAmount + CollectionManager.CollectionCoinIncrease * journal.CompletionCount();
		}

		/// <summary>
		/// The player score (for sorting purposes)
		/// </summary>
		/// <returns></returns>
		public int Score()
		{
			return stars * (CollectionManager.TotalGridSpace + 1) + journal.CompletionCount();
		}

		/// <summary>
		/// Return your stars as a string
		/// </summary>
		/// <returns></returns>
		public string StarString()
		{
			string starString = "";

			for (int startype = CollectionManager.StarTypes.Count - 1; startype >= 0; startype--)
			{
				//You CUT out the upper values that you don't need, and you count the BLOCKS with the given number of stars.
				int starCut = MathExtensions.IntPow(CollectionManager.StarRollover, startype + 1);
				int starBlock = MathExtensions.IntPow(CollectionManager.StarRollover, startype);

				//If this is the last type of star, we don't cut
				if (startype == CollectionManager.StarTypes.Count - 1)
					starCut = int.MaxValue - 1;

				for (int i = 0; i < (stars % starCut) / starBlock; i++)
					starString += CollectionManager.StarTypes[startype];
			}

			return starString;
		}

		/// <summary>
		/// Attempt to rank up. Returns false if unsuccessful
		/// </summary>
		/// <returns></returns>
		public bool RankUp()
		{
			if (!journal.JournalComplete())
				return false;

			ResetRarities();
			journal = new CollectionJournal();
			inventory = new CollectionStorage();
			coins = CollectionManager.CoinRestockAmount;
			lastRestock = DateTime.Now;
			stars++;

			return true;
		}

		/// <summary>
		/// Sell an item in your inventory for the price given. You lose the item, but get (price) coins.
		/// Returns false if the transaction cannot go through
		/// </summary>
		/// <param name="itemPoint"></param>
		/// <param name="price"></param>
		/// <returns></returns>
		public bool SellItem(SpecialPoint itemPoint, int price)
		{
			if (!inventory.HasItem(itemPoint) || !CollectionManager.ValidPoint(itemPoint))
				return false;

			GetCoins(price);
			inventory.UnobtainItem(itemPoint);

			return true;
		}

		/// <summary>
		/// Buy the given item for the given price. You lose (price) coins, but get the item
		/// Returns false if the transaction cannot go through.
		/// </summary>
		/// <param name="itemPoint"></param>
		/// <param name="price"></param>
		/// <returns></returns>
		public bool BuyItem(SpecialPoint itemPoint, int price)
		{
			if (coins < price || !CollectionManager.ValidPoint(itemPoint))
				return false;

			coins -= price;
			GetItem(itemPoint);

			return true;
		}

		/// <summary>
		/// Use this function to increase both player coins and total coin amount
		/// </summary>
		/// <param name="amount"></param>
		public void GetCoins(int amount)
		{
			coins += amount;
			totalCoinsCollected += amount;
		}

		/// <summary>
		/// Like GetCoins, GetItem should be used whenever an item is obtained. It performs both the inventory
		/// item obtainment and the journal obtainment. It also keeps track of the "forever" journal
		/// </summary>
		/// <param name="itemPoint"></param>
		private void GetItem(SpecialPoint itemPoint)
		{
			foreverJournal.ObtainItem(itemPoint);
			journal.ObtainItem(itemPoint);
			inventory.ObtainItem(itemPoint);
		}

		/// <summary>
		/// Print out player statistics (including full journal)
		/// </summary>
		/// <param name="simple"></param>
		/// <returns></returns>
      public string Statistics(bool simple = true, string style = "")
		{
         return "<user-journal " + style + ">" + journal.AsString(simple) + "</user-journal>\n<user-info>" + (stars > 0 ? "Rank: " + StarString() + " (" + stars + ")\n" : "")
				+ "Complete: " + journal.CompletionCount() + "%\nCoins: " + coins + "\n" + RestockString + 
            "</user-info>";
		}

      public string ForeverStats()
      {
         return "Your permanent stats for the collection game: \n\n Total Coins Collected: " + TotalCoins +
         "\n Total Items Collected: " + ForeverJournal.TotalObtained() + "\n" +
            "Love given: " + loveGiven + " time".Pluralify(loveGiven) + " (" + loveItemsGiven + " items)\n" +
            "Love received: " + loveReceived + " time".Pluralify(loveReceived) + " (" + loveItemsReceived + " items)\n" +
            "Jealousy used: " + jealousyCount + " time".Pluralify(jealousyCount) + " (" + jealousyTaken + " items taken)\n" +
         "Hugs given: " + hugsGiven + "\nHugs received: " + hugsReceived;
      }

      public bool AddLover(int lover)
      {
         if (loveList.Contains(lover))
             return false;

         loveList.Add(lover);

         return true;
      }
      public bool RemoveLover(int lover)
      {
         if (!loveList.Contains(lover))
             return false;

         loveList.Remove(lover);

         return true;
      }
      public List<int> GetLovers()
      {
         return new List<int>(loveList);
      }

		public int Coins
		{
			get { return coins; }
		}
		public int TotalCoins
		{
			get { return totalCoinsCollected; }
		}
		public int Stars
		{
			get { return stars; }
		}
		public CollectionJournal Journal
		{
			get { return journal; }
		}
		public CollectionJournal ForeverJournal
		{
			get { return foreverJournal; }
		}
		public CollectionStorage Inventory
		{
			get { return inventory; }
		}
		public int[] RareList
		{
			get { return rareList.ToArray(); }
		}
		public DateTime LastRestock
		{
			get { return lastRestock; }
		}
		public TimeSpan RestockWait
		{
			get
			{
				return TimeSpan.FromHours(CollectionManager.RestockHours).Subtract(DateTime.Now - lastRestock);
			}
		}
		public string RestockString
		{
			get
			{
				if (RestockWait.Ticks <= 0)
					return "Available now!";

				return "Restock: " + StringExtensions.LargestTime(RestockWait);
			}
		}
	}

	public static class CollectionSymbols
	{
        public const string AllSymbols =
            "abcdefghijklmnopqrst" +
            "uvwxyz?!;:.,'@#$%^&*" +
            "0123456789~`-_+=/|\\\"" +
            "ŠŒŽÃŸÊÐÑÖÙÞß()[]{}<>" +
            "¼½¾÷„™ƒ€¿•±¶£©®«§»“”";
            //~`!@#$%^&*-_+=" +
            //"ABCDEFGHIJKLMNOPQRST" +
            //"UVWXYZ()[]{}<>|,.:;'" +
            //"0123456789?/";

            /*"abcdefghijklmnopqrst" +
            "uvwxyz~`!@#$%^&*-_+=" +
            "ABCDEFGHIJKLMNOPQRST" +
            "UVWXYZ()[]{}<>|,.:;'" +
            "0123456789?/™ƒ€¿•±¶£";*/

			/*"#$%&*+=@€†‡Œ•™œ¢£¤¥§" +
			"©«®°±¶»×ØÞßð÷ĐĦŊŋŧſƃ" +
			"ƈƍƐƔƛƢƩƪƱƺƾƿǂǖȝȢȭȴȹȿ" +
			"Ɂɕɚɠɤɮɷɸʃʘʢʬʭ٨٧ΞΨπϔϟ" +
			"ϠϨЖѼζ٪ՃწҴҿӪⅎֆ∞⍣⑃⑄⑆▴◌";*/

		public const string EmptyGridSymbol = "▒";
		public const string SimpleObtainedSymbol = "#";
		public const string SimpleEmptySymbol = "-";

		/// <summary>
		/// Get the symbol for a given point
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		public static string GetSymbol(SpecialPoint point)
		{
			string symbol = "";

			//Only get the symbol if the point was valid
			if (CollectionManager.ValidPoint(point))
				symbol = AllSymbols[point.Across + point.Down * CollectionManager.Width].ToString();

			return symbol;
		}

		/// <summary>
		/// Get both the point and the symbol (useful for chat output)
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		public static string GetPointAndSymbol(SpecialPoint point)
		{
			return point + "(" + GetSymbol(point) + ")";
		}
	}

	/// <summary>
	/// Lol a lazy class to implement an inventory from the tracker. 
	/// </summary>
	[Serializable()]
	public class CollectionStorage : CollectionJournal
	{
		public CollectionStorage() { }

		/// <summary>
		/// Copy Constructor
		/// </summary>
		/// <param name="copyStorage"></param>
		public CollectionStorage(CollectionStorage copyStorage)
		{
			for (int i = 0; i < width; i++)
				for (int j = 0; j < height; j++)
					obtained[i, j] = copyStorage.obtained[i, j];
		}

		/// <summary>
		/// To keep the interface similar between the tracker and the inventory, this is the method
		///	to remove something from the inventory: "unobtain"
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		public bool UnobtainItem(SpecialPoint point)
		{
			if (CollectionManager.ValidPoint(point) && obtained[point.Across, point.Down] > 0)
			{
				obtained[point.Across, point.Down]--;
				return true;
			}

			return false;
		}
	}

	/// <summary>
	/// A class which tracks the completion information for the collection game
	/// </summary>
	[Serializable()]
	public class CollectionJournal
	{
		protected int width = CollectionManager.Width;
		protected int height = CollectionManager.Height;
		protected int[,] obtained;

		public CollectionJournal()
		{
			obtained = new int[width, height];
		}

		/// <summary>
		/// Copy Constructor
		/// </summary>
		/// <param name="copyJournal"></param>
		public CollectionJournal(CollectionJournal copyJournal)
			: this()
		{
			for (int i = 0; i < width; i++)
				for (int j = 0; j < height; j++)
					obtained[i, j] = copyJournal.obtained[i, j];
		}

		/// <summary>
		/// Mark an object as obtained. Return whether or not this was the first time it was obtained
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		public bool ObtainItem(SpecialPoint point)
		{
			if (CollectionManager.ValidPoint(point) && obtained[point.Across, point.Down]++ == 0)
				return true;

			return false;
		}

		/// <summary>
		/// Does this collection have the given item at the point?
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		public bool HasItem(SpecialPoint point)
		{
			if (CollectionManager.ValidPoint(point) && obtained[point.Across, point.Down] > 0)
				return true;

			return false;
		}

		/// <summary>
		/// How many slots have been completed?
		/// </summary>
		/// <returns></returns>
		public int CompletionCount()
		{
			int completed = 0;

			for (int i = 0; i < width; i++)
				for (int j = 0; j < height; j++)
					if (obtained[i, j] > 0)
						completed++;

			return completed;
		}

		/// <summary>
		/// Is this journal fully complete?
		/// </summary>
		/// <returns></returns>
		public bool JournalComplete()
		{
			return CompletionCount() == CollectionManager.TotalGridSpace;
		}

		/// <summary>
		/// How many total items have been obtained ever?
		/// </summary>
		/// <returns></returns>
		public int TotalObtained()
		{
			int total = 0;

			for (int i = 0; i < width; i++)
				for (int j = 0; j < height; j++)
					total += obtained[i, j];

			return total;
		}

		/// <summary>
		/// Returns a list of items that are in this journal, but not the "other"
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public List<SpecialPoint> Compare(CollectionJournal other)
		{
			List<SpecialPoint> unique = new List<SpecialPoint>();

			for (int i = 0; i < width; i++)
				for (int j = 0; j < height; j++)
					if (obtained[i, j] > 0 && other.obtained[i, j] == 0)
						unique.Add(new SpecialPoint(i, j));

			return unique;
		}

      public List<SpecialPoint> Unobtained()
      {
         return CollectionManager.CompleteJournal().Compare(this);
      }

		public override string ToString()
		{
			return AsString(false);
		}

		/// <summary>
		/// Return the journal as a nice formatted grid of symbols
		/// </summary>
		/// <param name="simple"></param>
		/// <returns></returns>
		public string AsString(bool simple)
		{
			string output = "*|";

			//The top row of letters to describe the horizontal index
			for (int i = 0; i < CollectionManager.Width; i++)
				output += ((char)('A' + i)).ToString();

			//A spacer between the index and the symbols
			output += "\n-+";
			for (int i = 0; i < CollectionManager.Width; i++)
				output += "-";

			//The actual symbols (and vertical index
			for (int i = 0; i < CollectionManager.Height; i++)
			{
				//Vertical index
				output += "\n" + i + "|";

				//Symbols for this row
				for (int j = 0; j < CollectionManager.Width; j++)
				{
					SpecialPoint thisPoint = new SpecialPoint(j, i);
					if (HasItem(thisPoint))
					{
						if (simple)
							output += CollectionSymbols.SimpleObtainedSymbol;
						else
							output += CollectionSymbols.GetSymbol(thisPoint);
					}
					else
					{
						if (simple)
							output += CollectionSymbols.SimpleEmptySymbol;
						else
							output += CollectionSymbols.EmptyGridSymbol;
					}
				}
			}

			return output;
		}

		/// <summary>
		/// Get all the item counts as a dictionary of points (used for sorting and stuff)
		/// </summary>
		/// <returns></returns>
		public Dictionary<SpecialPoint, int> AllItemCounts()
		{
			Dictionary<SpecialPoint, int> counts = new Dictionary<SpecialPoint, int>();

			for (int i = 0; i < CollectionManager.Width; i++)
				for (int j = 0; j < CollectionManager.Height; j++)
					counts.Add(new SpecialPoint(i, j), obtained[i, j]);

			return counts;
		}

		/// <summary>
		/// Accessor for individual values of collection
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		public int this[SpecialPoint point]
		{
			get
			{
				if (!CollectionManager.ValidPoint(point))
					return -1;

				return obtained[point.Across, point.Down];
			}
		}
	}
	#endregion
}
