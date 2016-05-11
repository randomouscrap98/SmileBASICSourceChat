using Explorer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MyExtensions;
using System.Diagnostics;
using System.Threading;
using ModuleSystem;
using ChatEssentials;

namespace WikiaChatLogger
{
	public class ExplorerModule : Module
	{
		//public readonly bool Cheating;

		public const int ExchangeRate = 10;
		public const int MaxProfiles = 10000;

		//public const string HelpFile = "ex\\help.txt";
		public const string WorldFile = "worlds.dat";
		public const string PlayerFile = "players.dat";
		public const string WorldIDFile = "worldID.dat";

		private List<WorldInstance> worlds = new List<WorldInstance>();
		private Dictionary<int, ExplorerPlayer> allPlayers = new Dictionary<int, ExplorerPlayer>();
      private DateTime lastRefresh = new DateTime(0);
      private DateTime lastCommand = new DateTime(0);
		private int generateCode = -1;
		private int closeCode = -1;
		private int closeWorld = -1;
      private bool oneTimeRun = false;

		private Queue<ExplorerTimeProfile> timeProfiles = new Queue<ExplorerTimeProfile>();
		private object ProfileLocker = new object();

		public ExplorerModule()
		{
         AddOptions(new Dictionary<string, object> {
            {"cheating", false },
            {"fullMap", false },
            {"treeHours", 1.0 }, 
            {"stoneHours", 0.15},
            {"fruitHours",1.0},
            {"superFruitHours", 1.2},
            //{"fullnessIncrease", 5.0},
            {"fruitPerFullness", 20.0 },
            {"fullnessDecayMinutes", 0.6},
            {"extraFruitPerMagicStone", 0.5},
            {"torchSteps",200 },
            {"hourlyStamina", 100},
            {"caveChance", 0.020},
            {"player","¥"},
            {"mapFolder", "maps"},
            {"mapLink", "nolink"}
         });

         List<CommandArgument> NoArguments = new List<CommandArgument>();
         Commands.AddRange(new List<ModuleCommand> 
         {
            new ModuleCommand("exsetworld", new List<CommandArgument> 
            { 
               new CommandArgument("world", ArgumentType.Integer)
            }, "Set the world you want to play on"),
            new ModuleCommand("exworldlist", NoArguments, "See the list of all worlds and basic info"),
            new ModuleCommand("exgo", new List<CommandArgument>
            {
               new CommandArgument("actions (udlrpes^v<>)", ArgumentType.FullString, RepeatType.ZeroOrOne)
            }, "Perform actions (move up, down, left, right; pick up; use equipped; toggle strafing; face up, down, left, right)"),
            new ModuleCommand("exitemlist", NoArguments, "Get the list of item names"),
            new ModuleCommand("exitems", NoArguments, "See the list of all your items"),
            new ModuleCommand("exstorage", NoArguments, "See the list of all your stored items"),
            new ModuleCommand("exstore", new List<CommandArgument> 
            { 
               new CommandArgument("item", ArgumentType.Word),
               new CommandArgument("amount", ArgumentType.Integer, RepeatType.ZeroOrOne)
            }, "Put amount of item in storage (by name, like Fn)"),
            new ModuleCommand("extake", new List<CommandArgument> 
            { 
               new CommandArgument("item", ArgumentType.Word),
               new CommandArgument("amount", ArgumentType.Integer, RepeatType.ZeroOrOne)
            }, "Take amount of item out of storage (by name, like Fn)"),
            new ModuleCommand("exequip", new List<CommandArgument> 
            {
               new CommandArgument("item", ArgumentType.Word)
            }, "Equip given item (by name, like Fn)"),
            new ModuleCommand("excraft", new List<CommandArgument> 
            { 
               new CommandArgument("item", ArgumentType.Word),
               new CommandArgument("amount", ArgumentType.Integer, RepeatType.ZeroOrOne)
            }, "Craft amount of item (by name, like Fn)"),
            new ModuleCommand("excraftlist", NoArguments, "Get the crafting recipes"),
            new ModuleCommand("exmyacres", NoArguments, "Get a list of your owned acres in this world"),
            new ModuleCommand("exteleport", new List<CommandArgument> 
            { 
               new CommandArgument("acreX", ArgumentType.Integer),
               new CommandArgument("acreY", ArgumentType.Integer)
            }, "Teleport to an acre you own"),
            new ModuleCommand("exmap", new List<CommandArgument> 
            { 
               new CommandArgument("world", ArgumentType.Integer)
            }, "View map of given world", true),
            new ModuleCommand("extop", NoArguments, "See the top explorers"),
            new ModuleCommand("exclose", NoArguments, "See your rank in the top explorers"),
            new ModuleCommand("exrespawn", NoArguments, "Go back to the spawn (if you get stuck)"),
            new ModuleCommand("exmastergenerate", new List<CommandArgument> 
            { 
               new CommandArgument("mastercode", ArgumentType.Integer, RepeatType.ZeroOrOne)
            }, "Generate a new world (only top admins)"),
            new ModuleCommand("exmastertest", new List<CommandArgument>
            {
               new CommandArgument("worldtype", ArgumentType.Integer)
            }, "Test world generation (only top admins)"),
            new ModuleCommand("exmasterclose", new List<CommandArgument> 
            { 
               new CommandArgument("world", ArgumentType.Integer),
               new CommandArgument("mastercode", ArgumentType.Integer, RepeatType.ZeroOrOne)
            }, "Close an old world (only top admins)"),
            new ModuleCommand("exmasterflagscatter", new List<CommandArgument> 
            { 
               new CommandArgument("amount", ArgumentType.Integer),
               new CommandArgument("world", ArgumentType.Integer)
            }, "Scatter some flags throughout the world (only top admins)")
         });

         Commands.AddRange(Player.DefaultOptions.Select(x => new ModuleCommand("extoggle" + x.Key.ToString().ToLower(),
            NoArguments, x.Value.Item2)));


         //string toggleOptions = string.Join("\n", Player.DefaultOptions.Select(x => "/extoggle" + x.Key.ToString().ToLower() + " - " + x.Value.Item2));

         GeneralHelp = (@"Ex is short for the ""Exploration Game"", where the goal is to gather resources
in order to claim land. The more land you claim and explore, the higher your score. @^@^
-Extended /exgo syntax- @^
You can do more with the /exgo command than simply typing the things you want
to do a million times. If you follow a command (such as u) with a number, it'll
repeat the previous command that many times (up to 100). You can also surround
a bunch of commands with parenthesis followed by a number, and it'll repeat all
the commands in parenthesis. Here's some example /exgo commands: @^
 /exgo uurre - Move up twice, then right twice, then use your equipped item @^
 /exgo d10<p - Move down 10 times, face left, then pick up the item you're facing @^
 /exgo ^se(re)14 - Place 15 items in a row in front of you extending to the right @^
 /exgo ((>pd)10r(u>p)10)5 - Pick up a 5x10 rectangle of objects extending down (10) and to the right (5) @^
If you take the time to come up with some useful commands, you'll be able to fly right 
through your exploration! @^@^
-Getting Started- @^
You don't necessarily have to be the ""winner"" of the exploration game, but
there is a scoring and leaderboard system. You get points by exploring (small score),
claiming acres (large score), and building statues on your land (medium score).
To claim an acre, you must place a tower anywhere within it. To build a tower, you need to gather the materials. Materials
can be found throughout the world, and many can be automatically picked up (if the option
is toggled on. It's off by default). Things like wood, stone, and fruit can be picked up
simply by walking into it, but stuff like fences, planks, ungrown plants, etc. must be
manually picked up by using the 'p' action in /exgo. If you're confused about what an item is,
use the /exitemlist command to see the full names.
All actions require stamina. You'll get " + ExplorerConstants.Player.HourlyStaminaRegain + @" stamina back 
over the course of an hour, but you can also eat fruit to regain it. You can set the option to automatically eat fruit when your
stamina is low (thus removing the need to equip and eat it). All items must be used through the equipped item slot,
so if you want to use or place anything, it must be equipped.
Once you've collected enough resources to build a tower, you should find a spot to claim
as soon as possible! With a claimed acre, you can build fences and gates to keep people
out of your area. You may want to set up some farms on your owned land so you can have
a steady supply of resources. @^@^
-Suggestions- @^
Your first goal should be to claim an acre. Without a claimed acre, any resources you try
to grow (such as saplings or seeds) could be taken by anyone. A tower requires some of nearly
all the resources, but the most important item (the Star Crystal) can only be found in caves.
Make sure you explore every cave you can and search for a star crystal. You'll also need a fair
amount of stone and lots of wood, so just focus on collecting these. If you run out of stamina
but wish to continue playing, you can use chat coins to get your stamina back. Eventually you
won't need to do this as you'll probably have a fruit farm. If you trust the other players,
you can grow an unprotected fruit farm to keep your stamina up.
Once you claim your first acre, you should focus on walling a section (or the whole thing) so that
you have a protected area to do what you please with. A good thing to grow at first is
fruit, since it'll enable you to do more actions all at once. Once you have a nice base set up,
you're basically all ready to start expanding. Go claim as many acres as you can! Use any
excess resources like stone and wood to create statues, which'll increase your score. @^@^
-Obstacles- @^
One of the biggest obstacles in the beginning is your stamina. You start off with a very small
amount, and without a supply of fruit, you'll be tired out pretty quickly. In the beginning,
you can either hunt for as much fruit as you can, or simply bite the bullet and refill
using chat coins. The reliance on either chat coins or waiting for your stamina to come back
will lessen more and more as you claim more acres and fill them with farms. Once you have enough
fruit to allow you to perform all the actions you want, you're ready to take over!
Another obstacle is water. If you need to cross it, the best way is to craft planks
and place them over the water. Planks are pretty cheap, so if you need to cross quickly,
this is the best way. However, if you own an acre and want to get rid of or change the water,
it's probably a better idea to fill it in using grass, sand, or dirt. These are more expensive
to craft, but once you fill in the water you can place stuff on top of it. You can't place stuff
on planks.
Stone and Trees can also be a significant obstacle in terms of stamina drain. It costs double
stamina to pick up trees, and it costs triple stamina to pick up stone. If you have the autopickup
option on, you'll automatically collect stone and trees as you walk through them. This can be a 
problem early on, as trying to cross the map without regards to these obstacles will drain your
stamina very quickly. Just keep that in mind as you're exploring. If you don't have the autopickup
command on, you don't have to worry about this, as trees and stone will simply block your path\
without taking up your stamina, and you can simply walk around them. @^@^
-Caves-@^
You can find special items in caves. One of them, the Star Crystal, is required to build towers.
The rest are just helpful, such as Magic Stones which increase your stamina, and Acre Lockers which
remove the need for fences on your acre. Caves randomly spawn when you mine up stone. Simply land your
player on the cave entrance to enter the cave. Caves are pitch black though, so make sure you craft a
torch beforehand.").Replace("\n", " ").Replace("@^", "\n");
         GeneralHelp = Regex.Replace(GeneralHelp, "[ ]+", " ");
		}

		public override bool LoadFiles()
		{
         return MySerialize.LoadObject<List<WorldInstance>>(WorldFile, out worlds) && 
				MySerialize.LoadObject<Dictionary<int, ExplorerPlayer>>(PlayerFile, out allPlayers) &&
				MySerialize.LoadObject<int>(WorldIDFile, out WorldInstance.nextWorldID);
		}
		public override bool SaveFiles()
		{
			bool success = false;
			Stopwatch totalTimer = new Stopwatch();
			totalTimer.Start();

			//First, set up the profiler and set the number of worlds to process
			ExplorerTimeProfile profile = new ExplorerTimeProfile();
			profile.WorldProcesses += worlds.Count(x => x.CanPlay);
         
			try
			{
				//Now, simulate all worlds
				//Directory.CreateDirectory(Path.GetDirectoryName(DropboxPath + HelpFile));
				Parallel.ForEach(worlds.Where(x => x.CanPlay), world =>
				{
					Stopwatch simulateTimer = new Stopwatch();
					simulateTimer.Start();
					world.WorldData.Simulate();
					simulateTimer.Stop();

					lock (ProfileLocker)
					{
						profile.TotalWorldSimulationTime += simulateTimer.ElapsedMilliseconds;
					}
				});

				//Next, start on the serialization
				Thread thread = new Thread(x =>
				{
					Stopwatch fileTimer = new Stopwatch();
					fileTimer.Start();
					success = MySerialize.SaveObject<List<WorldInstance>>(WorldFile, worlds) &&
                  MySerialize.SaveObject<Dictionary<int, ExplorerPlayer>>(PlayerFile, allPlayers) &&
                  MySerialize.SaveObject<int>(WorldIDFile, WorldInstance.nextWorldID);
					fileTimer.Stop();

					lock (ProfileLocker)
					{
						profile.SerializationTime = fileTimer.ElapsedMilliseconds;
					}
				});

				thread.Start();

            Directory.CreateDirectory(GetOption<string>("mapFolder"));
				//While we're serializing, we can also produce the images
				Parallel.ForEach(worlds.Where(x => x.CanPlay), world =>
				{
					Stopwatch imageTimer = new Stopwatch();
					imageTimer.Start();
					SaveWorldImage(world);
					imageTimer.Stop();

					lock (ProfileLocker)
					{
						profile.TotalWorldImageTime += imageTimer.ElapsedMilliseconds;
					}
				});

				//Now that we're done, we need to WAIT for the serialization to finish
				thread.Join();
			}
			catch
			{
				return false;
			}

			//This'll hopefull fix glitches
			if (WorldInstance.nextWorldID == 0)
				WorldInstance.nextWorldID++;

			//Finalize the profile information
			totalTimer.Stop();
			profile.TotalTime += totalTimer.ElapsedMilliseconds;

			//...And add it to the profile queue
			AddProfile(profile);

			return success;
		}

		public void AddProfile(ExplorerTimeProfile profile)
		{
			timeProfiles.Enqueue(profile);

			while (timeProfiles.Count > MaxProfiles)
				timeProfiles.Dequeue();
		}

      /// <summary>
      /// Must reimplement this!
      /// </summary>
      /// <param name="world">World.</param>
      public void SaveWorldImage(WorldInstance world, bool fullMap = false)
		{
         if (GetOption<bool>("fullMap"))
            fullMap = true;
         
         world.WorldData.GetFullMapImage(!fullMap).Save(StringExtensions.PathFixer(GetOption<string>("mapFolder")) + "world" + world.WorldID + ".png");
		}

      public List<string> SortedModuleItems(Dictionary<int, ChatEssentials.UserInfo> users)
		{
         Func<int, string> GetUserQuick = x => {
            if(users.ContainsKey(x))
               return users[x].Username;
            return "UnknownUser";
         };
         return allPlayers.Select(x => Tuple.Create(x.Key, worlds.Sum(y => y.GetScore(x.Key)))).OrderByDescending(x => x.Item2).Select(x => GetUserQuick(x.Item1) + " • " + x.Item2).ToList();
		}

      public List<JSONObject> StyledMessage(string message)//, bool warning = false)
      {
         ModuleJSONObject output = new ModuleJSONObject();
         output.message = "<span style=\"display: block; font-family:'Courier New', Courier, monospace; line-height: 100%;\">" + message + "</span>";
         output.safe = false;
         return new List<JSONObject> { output };
      }

      public override List<ChatEssentials.JSONObject> ProcessCommand(UserCommand command, ChatEssentials.UserInfo user, Dictionary<int, ChatEssentials.UserInfo> users)
      {
         #region oneTimeProcess
         if(!oneTimeRun)
         {
            oneTimeRun = true;
            //Cheating = 
            ExplorerConstants.Simulation.FruitGrowthHours = GetOption<double>("fruitHours");
            ExplorerConstants.Simulation.TreeGrowthHours = GetOption<double>("treeHours");
            ExplorerConstants.Simulation.StoneGrowthHours = GetOption<double>("stoneHours");
            ExplorerConstants.Simulation.SuperFruitGrowthHours = GetOption<double>("superFruitHours");
            ExplorerConstants.Items.TorchSteps = GetOption<int>("torchSteps");
            ExplorerConstants.Probability.CaveChance = GetOption<double>("caveChance");
            ExplorerConstants.Player.HourlyStaminaRegain = GetOption<int>("hourlyStamina");
            //ExplorerConstants.Player.FruitFullnessIncrease = GetOption<double>("fullnessIncrease");
            ExplorerConstants.Player.FruitPerFullness = GetOption<double>("fruitPerFullness");
            ExplorerConstants.Player.FullnessDecayMinutes = GetOption<double>("fullnessDecayMinutes");
            ExplorerConstants.Items.ExtraFruitPerMagicStone = GetOption<double>("extraFruitPerMagicStone");

            string temp = GetOption<string>("player");
            if (temp.Length > 0)
               ExplorerConstants.Player.CurrentPlayerToken = temp[0];

            if (GetOption<bool>("cheating"))
               Commands.Add(new ModuleCommand("excheat", new List<CommandArgument>(), "Get a crapload of resources"));
         }
         #endregion

         #region fromPostProcess
         if (worlds.Count == 0)
         {
            GenerateWorld();
         }
            
         foreach (WorldInstance world in worlds)
            world.RefreshFullness((DateTime.Now - lastCommand).TotalMinutes / ExplorerConstants.Player.FullnessDecayMinutes);

         lastCommand = DateTime.Now;

         if ((DateTime.Now - lastRefresh).TotalHours >= 1.0 / ExplorerConstants.Player.HourlyStaminaRegain)
         {
            foreach (WorldInstance world in worlds)
               world.RefreshStamina((int)Math.Ceiling((DateTime.Now - lastRefresh).TotalHours * ExplorerConstants.Player.HourlyStaminaRegain));

            lastRefresh = DateTime.Now;
         }

         foreach (WorldInstance world in worlds)
            world.SimulateTime();
         #endregion

			Match match;
			Tuple<WorldInstance, string> results;

         TryRegister(user.UID);

			try
			{
            if(command.Command == "exmastertest")
            {
               if(!user.ChatControlExtended)
                  return FastMessage("You don't have access to this command", true);

               int worldType;
               if (!int.TryParse(command.Arguments[0], out worldType))
                  worldType = 0;

               World world = new World();
               world.Generate(worldType % ExplorerConstants.Generation.PresetBases.Count);
               world.GetFullMapImage(false).Save(StringExtensions.PathFixer(GetOption<string>("mapFolder")) + "test" + worldType + ".png");

               return FastMessage("Test " + worldType + " map: " + GetOption<string>("mapLink") + "/test" + worldType + ".png");
            }

            if(command.Command == "exmasterflagscatter")
            {
               if(!user.ChatControlExtended)
                  return FastMessage("You don't have access to this command", true);

               int amount, world;
               if (!int.TryParse(command.Arguments[0], out amount) || amount > 10000)
                  return FastMessage("You can't spawn that many flags!");
               if (!int.TryParse(command.Arguments[1], out world) || !worlds.Any(x => x.Operating && x.WorldID == world))
                  return FastMessage("The world you gave was invalid!");

               int actualCount = worlds.First(x => x.Operating && x.WorldID == world).WorldData.ScatterFlags(amount);

               ModuleJSONObject broadcast = new ModuleJSONObject("Hey, " + user.Username + " has just spawned " + actualCount +
                  " flags in exgame world " + world + "!");
               broadcast.broadcast = true;
               
               return new List<JSONObject>{ broadcast };
            }

				#region mastergenerate
				//match = Regex.Match(chunk.Message, @"^\s*/exmastergenerate\s*([0-9]+)?\s*$");
            if (command.Command == "exmastergenerate")
				{
               if(!user.ChatControlExtended)
                  return FastMessage("You don't have access to this command", true);

					int givenCode;
               if (!int.TryParse(command.Arguments[0], out givenCode))
						givenCode = 0;

					if (generateCode == -1)
					{
						generateCode = (int)(DateTime.Now.Ticks % 10000);
                  return FastMessage("If you wish to generate a new world, type in the same command with this code: " + generateCode);
					}
					else
					{
						if (generateCode != givenCode)
						{
							generateCode = -1;
                     return FastMessage("That was the wrong code. Code has been reset", true);
						}
						GenerateWorld();
						generateCode = -1;
                  return FastMessage("You've generated a new world!");
					}
				}
				#endregion

				#region masterclose
				//match = Regex.Match(chunk.Message, @"^\s*/exmasterclose\s+([0-9]+)\s*([0-9]+)?\s*$");
            if (command.Command == "exmasterclose")//match.Success)
				{
//					if (chunk.Username != Module.AllControlUsername)
//						return chunk.Username + ", you don't have access to this command";
               if(!user.ChatControlExtended)
                  return FastMessage("You don't have access to this command", true);

					int givenCode, givenWorld;

					if (!int.TryParse(command.Arguments[0], out givenWorld) || !worlds.Any(x => x.Operating && x.WorldID == givenWorld))
                  return FastMessage("The world you gave was invalid!", true);

               if (!int.TryParse(command.Arguments[1], out givenCode))
						givenCode = 0;

					if (closeCode == -1)
					{
						closeCode = (int)(DateTime.Now.Ticks % 10000);
						closeWorld = givenWorld;
                  return FastMessage("If you wish to close world " + closeWorld + " , type in the same command with this code: " + closeCode);
					}
					else
					{
						if (closeCode != givenCode || closeWorld != givenWorld)
						{
							closeCode = -1;
							closeWorld = -1;
                     return FastMessage("That was the wrong code or world. Code has been reset", true);
						}

						string output = "You've closed world " + closeWorld;
                  bool warning = false;
						WorldInstance world = worlds.FirstOrDefault(x => x.WorldID == closeWorld);
						if (world == null)
                  {
							output = "Something went wrong. No worlds have been closed";
                     warning = true;
                  }
						else
                  {
							world.CloseWorld();
                  }

						closeWorld = -1;
						closeCode = -1;

                  return FastMessage(output, warning);
					}
				}
				#endregion

				#region worldlist
            if (command.Command == "exworldlist")//Regex.IsMatch(chunk.Message, @"^\s*/exworldlist\s*$"))
				{
					string output = "List of all worlds:\n-------------------------------------";

					foreach (WorldInstance world in worlds)
					{
						if (world.CanPlay)
							output += "\n-World " + world.WorldID + " * " + world.GetOwnedAcres().Count + " acre".Pluralify(world.GetOwnedAcres().Count);
					}

               return StyledMessage(output);
				}
				#endregion

				#region setworld
				//match = Regex.Match(chunk.Message, @"^\s*/exsetworld\s+([0-9]+)\s*$");
            if (command.Command == "exsetworld")//match.Success)
				{
					int setWorld;

               if (!int.TryParse(command.Arguments[0], out setWorld))
                  return FastMessage("This is an invalid world selection", true);
					else if (!worlds.Any(x => x.WorldID == setWorld))
                  return FastMessage("There are no worlds with this ID", true);
               else if (allPlayers[user.UID].PlayingWorld == setWorld)
                  return FastMessage("You're already playing on this world", true);

					WorldInstance world = worlds.FirstOrDefault(x => x.WorldID == setWorld);
					if (!world.CanPlay)
                  return FastMessage("This world is unplayable", true);

               allPlayers[user.UID].PlayingWorld = setWorld;

               if (world.StartGame(user.UID, user.Username))
                  return FastMessage("You've entered World " + setWorld + " for the first time!");
					else
                  return FastMessage("You've switched over to World " + setWorld);
				}
				#endregion

				#region toggle
            match = Regex.Match(command.Command, @"extoggle([^\s]+)");
				if (match.Success)
				{
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

               return FastMessage(results.Item1.ToggleOption(user.UID, match.Groups[1].Value));
				}
				#endregion

				#region go
				//match = Regex.Match(chunk.Message, @"^\s*/exgo\s*(.*)\s*$");
            if (command.Command == "exgo")//match.Success)
				{
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

					int chatCoinGet = 0;
               string output = results.Item1.PerformActions(user.UID, command.Arguments[0]/*match.Groups[1].Value*/, out chatCoinGet);
//
//					if (chatCoinGet > 0)
//						StandardCalls_RequestCoinUpdate(chatCoinGet, chunk.Username);

               return StyledMessage(output);
				}
				#endregion

				#region cheat
            if (command.Command == "excheat")//Regex.IsMatch(chunk.Message, @"^\s*/excheat\s*$") && Cheating)
				{
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

               results.Item1.Cheat(user.UID);

               return FastMessage("You cheated some resources into existence!");
				}
				#endregion

				#region itemlist
            if (command.Command == "exitemlist")//Regex.IsMatch(chunk.Message, @"^\s*/exitemlist\s*$"))
				{
					string output = "";

					foreach (ItemBlueprint item in ExplorerConstants.Items.AllBlueprints
						.Where(x => x.Key != ExplorerConstants.Items.IDS.ChatCoins).Select(x => x.Value)
						.Where(x => x.CanPickup && x.CanObtain || ExplorerConstants.Items.CraftingRecipes.ContainsKey(x.ID)))
					{
						output += item.ShorthandName + " (" + item.DisplayCharacter + ") - " + item.DisplayName + "\n";
					}

               return StyledMessage(output);
				}
				#endregion

				#region equip
				//match = Regex.Match(chunk.Message, @"^\s*/exequip\s+([a-zA-Z]+)\s*$");
            if (command.Command == "exequip")//match.Success)
				{
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

               return FastMessage(results.Item1.EquipItem(user.UID, command.Arguments[0].Trim()));//match.Groups[1].Value.Trim());
				}
				#endregion

				#region craft
				//match = Regex.Match(chunk.Message, @"^\s*/excraft\s+([^0-9]+)\s*([0-9]*)\s*$");
            if (command.Command == "excraft")//match.Success)
				{
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

					int amount;

               if (!int.TryParse(/*match.Groups[2].Value*/command.Arguments[1], out amount))
						amount = 1;

               return FastMessage(results.Item1.CraftItem(user.UID, command.Arguments[0].Trim()/*match.Groups[1].Value.Trim()*/, amount));
				}
				#endregion

				#region craftlist
            if (command.Command == "excraftlist")//Regex.IsMatch(chunk.Message, @"^\s*/excraftlist\s*$"))
				{
					string output = "";
					foreach (var craftRecipe in ExplorerConstants.Items.CraftingRecipes.OrderBy(x => x.Value.Values.Sum()))
					{
						output += ExplorerConstants.Items.AllBlueprints[craftRecipe.Key].ShorthandName + " - ";

						foreach (var craftIngredient in craftRecipe.Value)
						{
							output += ExplorerConstants.Items.AllBlueprints[craftIngredient.Key].ShorthandName + "*" + craftIngredient.Value + " ";
						}

						output += "\n";
					}

               return StyledMessage(output);
				}
				#endregion

				#region map
				//match = Regex.Match(chunk.Message, @"^\s*/exmap\s+([0-9]+)\s*$");
            if (command.Command == "exmap")//match.Success)
				{
               //return FastMessage("Not supported right now!", true);

					int worldID;

               if (!int.TryParse(command.Arguments[0], out worldID))
                  return FastMessage("That's not a valid number", true);

					if (!worlds.Any(x => x.WorldID == worldID))
                  return FastMessage("There's no world with this ID", true);

					try
					{
						SaveWorldImage(worlds.FirstOrDefault(x => x.WorldID == worldID));
					}
               catch (Exception e)
					{
                  return FastMessage("An error occurred while generating the map image! Please report this error to an admin." +
                     "\nError: " + e, true);
					}

               return FastMessage("World " + worldID + " map: " + GetOption<string>("mapLink") + "/world" + worldID + ".png");
				}
				#endregion

				#region top
            if (command.Command == "extop")//Regex.IsMatch(chunk.Message, @"^\s*/extop\s*$"))
				{
					List<string> scores = SortedModuleItems(users);
					//List<Tuple<string, int>> scores = allPlayers.Select(x => Tuple.Create(x.Key, worlds.Sum(y => y.GetScore(x.Key)))).ToList();
					string output = "The top explorers are:";

					for (int i = 0; i < 5; i++)
						if (i < scores.Count())
							output += "\n" + (i + 1) + ": " + scores[i];

               return FastMessage(output);
				}
				#endregion

				#region close
            if (command.Command == "exclose")//Regex.IsMatch(chunk.Message, @"^\s*/exclose\s*$"))
				{
					List<string> scores = SortedModuleItems(users);
					//List<Tuple<string, int>> scores = allPlayers.Select(x => Tuple.Create(x.Key, worlds.Sum(y => y.GetScore(x.Key)))).ToList();
               int index = scores.FindIndex(x => x.Contains(user.Username + " "));

					string output = "Your exploration competitors are:";

					for (int i = index - 2; i <= index + 2; i++)
						if (i < scores.Count() && i >= 0)
							output += "\n" + (i + 1) + ": " + scores[i];

               return FastMessage(output);
				}
				#endregion

				#region respawn
            if (command.Command == "exrespawn")//Regex.IsMatch(chunk.Message, @"^\s*/exrespawn\s*$"))
				{
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

               return StyledMessage(results.Item1.Respawn(user.UID));
				}
				#endregion

				#region myacres
            if (command.Command == "exmyacres")//Regex.IsMatch(chunk.Message, @"^\s*/exmyacres\s*$"))
				{
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

               return FastMessage(results.Item1.PlayerAcres(user.UID));
				}
				#endregion

				#region items
            if (command.Command == "exitems")//Regex.IsMatch(chunk.Message, @"^\s*/exitems\s*$"))
				{
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

               return StyledMessage(results.Item1.PlayerItems(user.UID));
				}
				#endregion

            #region storage
            if (command.Command == "exstorage")//Regex.IsMatch(chunk.Message, @"^\s*/exitems\s*$"))
            {
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

               return StyledMessage(results.Item1.PlayerStorage(user.UID));
            }
            #endregion

            #region store
            //match = Regex.Match(chunk.Message, @"^\s*/excraft\s+([^0-9]+)\s*([0-9]*)\s*$");
            if (command.Command == "exstore")//match.Success)
            {
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

               int amount;

               if (!int.TryParse(/*match.Groups[2].Value*/command.Arguments[1], out amount))
                  amount = int.MaxValue;

               return FastMessage(results.Item1.StoreItems(user.UID, command.Arguments[0].Trim()/*match.Groups[1].Value.Trim()*/, amount));
            }
            #endregion

            #region store
            //match = Regex.Match(chunk.Message, @"^\s*/excraft\s+([^0-9]+)\s*([0-9]*)\s*$");
            if (command.Command == "extake")//match.Success)
            {
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

               int amount;

               if (!int.TryParse(/*match.Groups[2].Value*/command.Arguments[1], out amount))
                  amount = int.MaxValue;

               return FastMessage(results.Item1.TakeItems(user.UID, command.Arguments[0].Trim()/*match.Groups[1].Value.Trim()*/, amount));
            }
            #endregion

				#region teleport
				//match = Regex.Match(chunk.Message, @"^\s*/exteleport\s+([0-9]+)\s*-\s*([0-9]+)\s*$");
            if (command.Command == "exteleport")//match.Success)
				{
               if (!WorldCommandCheck(user.UID, out results))
                  return FastMessage(results.Item2, true);

					int x, y;
               if (!int.TryParse(command.Arguments[0], out x) || !int.TryParse(command.Arguments[1], out y))
                  return FastMessage("Your acre was formatted incorrectly!", true);

               return StyledMessage(results.Item1.TeleportToTower(user.UID, Tuple.Create(x, y)));
				}
				#endregion
			}
			catch (Exception e)
			{
				return FastMessage("An exception occurred: " + e.Message + ". Please report to an admin\n" +
               "Stack trace: \n" + e.StackTrace, true);
			}

         return new List<JSONObject>();
		}

//		public override string PostProcess()
//		{
//			if (worlds.Count == 0)
//			{
//				GenerateWorld();
//			}
//
//			if ((DateTime.Now - lastRefresh).TotalHours >= 1.0 / ExplorerConstants.Player.HourlyStaminaRegain)
//			{
//				lastRefresh = DateTime.Now;
//				foreach (WorldInstance world in worlds)
//					world.RefreshStamina(1);
//			}
//
//			foreach (WorldInstance world in worlds)
//				world.SimulateTime();
//
//			return "";
//		}

//		public override string GetHelp()
//		{
//			return DropboxLink + "/" + HelpFile.Replace("\\", "/");
//		}
//		public override string DefaultConfig()
//		{
//			return base.DefaultConfig() + "cheating=false\n" + "treeHours=1.0\n" + "stoneHours=0.15\n" + "fruitHours=1.0\n" + "superFruitHours=1.2\n" +
//                "torchSteps=200\n" + "hourlyStamina=100\n" + "caveChance = 0.020\n" + "player=¥\n";
//		}
//		public override string ReportStats()
//		{
//			return GetProfileInformation(10) + "\n" + GetProfileInformation(100) + "\n" + GetProfileInformation(1000);
//		}
		public override string Nickname
		{
			get
			{
				return "ex";
			}
		}

		public bool TryRegister(int user)
		{
         if (user > 0 && !allPlayers.ContainsKey(user))
			{
				allPlayers.Add(user, new ExplorerPlayer());
				return true;
			}

			return false;
		}
		public bool ValidWorld(int user)
		{
         return worlds.Where(x => x.CanPlay).Any(x => x.WorldID == allPlayers[user].PlayingWorld);
		}
		public WorldInstance GetWorld(int user)
		{
			return worlds.FirstOrDefault(x => x.WorldID == allPlayers[user].PlayingWorld);
		}
		public bool WorldCommandCheck(int user, out Tuple<WorldInstance, string> results)
		{
			results = new Tuple<WorldInstance, string>(null, "");
			if (!ValidWorld(user))
			{
				results = Tuple.Create((WorldInstance)null, "You're not playing in a valid world!");
				return false;
			}

			WorldInstance world = GetWorld(user);
			if (!world.CanPlay)
			{
				results = Tuple.Create((WorldInstance)null, "You're playing in a closed world!");
				return false;
			}

			results = Tuple.Create(world, "");
			return true;
		}
		public void GenerateWorld()
		{
			worlds.Add(new WorldInstance());
			worlds.Last().GenerateWorld();
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
//				Tuple<WorldInstance, string> results;
//
//				//If we receive a message from the User module, update the list of users with the given information
//				if (TryRegister(user))
//					newRegistrations++;
//
//				//Users cannot be swapped
//
//				//Users cannot be removed (for now)
//
//				//We receive a lot of stuff from the ChatCoins module
//				if (message.SendModule.Name.Contains("ChatCoins"))
//				{
//					//If the chatcoins module is polling us, that means it's looking for exchange data
//					if (message.ResponseType == ModuleMessage.Responses.Poll)
//						output.Add(NickName + " exchange rate: 1 - " + ExchangeRate + "% stamina");
//
//					//The chatcoins module is trying to perform an exchange with us. 
//					if (exchange && WorldCommandCheck(user, out results))
//					{
//						if(coins > 100 / ExchangeRate + 1)
//						{
//							output.Add(user + ", that's too many coins! You'll be wasting them!");
//						}
//						else
//						{
//							int stamina = results.Item1.RestoreStamina(user, ExchangeRate * coins, true);
//							StandardCalls_AcknowledgeExchange(coins, user);
//							output.Add(user + ", you now have " + stamina + " stamina on World " + results.Item1.WorldID);
//						}
//					}
//				}
//			}
//
//			if (newRegistrations > 0)
//				StatusUpdate(ModuleName + " registered " + newRegistrations + " new users");
//
//			return output;
//		}

		public string GetProfileInformation(int count)
		{
			List<ExplorerTimeProfile> info = timeProfiles.Skip(Math.Max(0, timeProfiles.Count() - count)).Take(count).ToList();
			long totalWorldProcesses = info.Sum(x => x.WorldProcesses);

			if (totalWorldProcesses == 0)
				return "No worlds";

			string output = "Profile info on last " + info.Count + " profiles:\n";
			output += "-Average overall processing time: " + info.Sum(x => x.TotalTime) / info.Count + "ms\n";
			output += "-Average serialization time: " + info.Sum(x => x.SerializationTime) / info.Count + "ms\n";
			output += "-Average worlds processed: " + totalWorldProcesses / info.Count + "\n";
			output += "--Average world simulation time: " + info.Sum(x => x.TotalWorldSimulationTime) / totalWorldProcesses + "ms\n";
			output += "--Average world image creation time: " + info.Sum(x => x.TotalWorldImageTime) / totalWorldProcesses + "ms\n";

			return output;
		}
	}

	public class ExplorerTimeProfile
	{
		public long TotalTime = 0;
		public long WorldProcesses = 0;
		public long TotalWorldSimulationTime = 0;
		public long TotalWorldImageTime = 0;
		public long SerializationTime = 0;
	}

	[Serializable()]
	public class ExplorerPlayer
	{
		public int PlayingWorld = -1;
	}
}
