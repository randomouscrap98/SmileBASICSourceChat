using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSystem;		//This allows you to create a module
using ChatEssentials;	//This allows you to create messages

//NOTE: If the comments clutter up everything, you may want to delete them. You should probably
//keep a copy of the code WITH the comments however, because they explain (hopefully) everything.
//If you learn better by just looking at code, the comments will probably just get in your way.
//This module isn't actually all that large; it's mostly comments.

namespace WhateverYouWant
{

	//This is your module class; it's like a blueprint for your module. I will use
	//the blueprint to create your module and run it in the chat server. The
	//:Module part at the end indicates that your class "is" a module
	//*You should always include "Module" at the end of your class name.
    public class CoinModule : Module
    {

		//Put any variables you will need here. We use a "dictionary" to store
		//user data because it's like an array where you set the index instead of
		//being forced to start from 0 and go up. This way, we can use a user's
		//UID (see the later function ProcessCommand) to index into the dictionary.
		//This dictionary will be able to map user UIDs to an object we define later:
		//a "UserCoinData". UserCoinData is a class we define to hold all the data
		//for a user. Look at the end of the module for the UserCoinData class declaration.
		Dictionary<int, UserCoinData> userdata = new Dictionary<int, UserCoinData>();
		Random random = new Random();	//In C#, the random number generator has to be an object.


		//This is your module constructor. It runs when your module is created.
		//You should NOT include any parameters!
		public CoinModule()
		{
			//In the constructor, you should add your commands. "Commands" is an 
			//existing variable which is a list of commands (starts out empty)
			Commands.Add(new ModuleCommand("coinget", new List<CommandArgument>(), 
				"get a random amount of coins", false));
			Commands.Add(new ModuleCommand("coingloat", new List<CommandArgument>(),
				"tell the world how many coins you have", true));
			Commands.Add(new ModuleCommand("coincheck", new List<CommandArgument>() 
				{ new CommandArgument("user", ArgumentType.User) },
				"see the coins for the given user", false));
			Commands.Add(new ModuleCommand("coingive", new List<CommandArgument>() 
				{ new CommandArgument("user", ArgumentType.User), new CommandArgument("coins", ArgumentType.Integer) },
				"give a user some coins", true));

			//new ModuleCommands are created with 4 fields:
			//-the command name (coinget, etc.). It SHOULD start with your module's nickname so it doesn't clash with other module commands
			//-the list of arguments for the command (coinget has no arguments, so it gets an empty list)
			//-the command description (dont' make it too long)
			//-true or false: increases spamscore. If your command creates a broadcast message, you should ALWAYS update spamscore

			//If your command has arguments, you fill up the "List<CommandArgument>" with new CommandArguments.
			//A "CommandArgument" requires just two things:
			//-The name of the argument (user, etc.)
			//-The type of argument (ArgumentType.User, etc.) The argument type is used to parse your command correctly.
		}


		//This is the function that the chatserver will call when it needs you to process a command. It will
		//give you the already parsed command; all you have to do is do whatever the command should do for your
		//module. You also get the UserInfo for the user who called the command, and a list of all users who
		//have ever visited the chat before. This function returns a list of messages for the chat server to
		//output. It's a list in case you need to send more than one message, although that's usually not necessary.
		//A JSONObject is what holds a chat message. It's pretty simple to use; check out the code within this function
		//to see how it works.
		public override List<MessageBaseJSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
		{
			List<MessageBaseJSONObject> outputs = new List<MessageBaseJSONObject>();

			//"JSONObject" is a base class; there are many JSONObject classes which derive from it.
			//Just like your module derives from Module, so do other classes derive from JSONObject.
			//You'll mostly be using a "ModuleJSONObject", but there's also "WarningJSONObject" which
			//is useful for errors. You almost 100% do NOT want a normal JSONObject; you'll see later
			//that although outputs is a list of JSONObjects, we fill it with ModuleJSONObjects. 

			//If this is confusing, just think of JSONObject as a "placeholder", and the real messages
			//are either a ModuleJSONObject (the chat server will display it as a module message) or a
			//WarningJSONObject (the chat server will display it as a warning message)


			//THIS IS IMPORTANT! Your dictionary will NOT contain all the users when it starts, right?
			//It's just an empty dictionary. This checks to see if this user exists yet, and if not, it adds them.
			if(!userdata.ContainsKey(user.UID))
				userdata.Add(user.UID, new UserCoinData());	//First parameter is the key, second is the data.


			//Here, we need to perform actions based on what the command was. You can do whatever you like here,
			//but you should always at least have some kind of output for each command
			if (command.Command == "coinget")
			{
				//This command demonstrates a simple command with no arguments. Here, we just do whatever it
				//is the command is supposed to do (generate random coins), then simply output the results.
				//Follow this guideline (particularly the outputs part) for simple commands.

				//Oops, you can only draw coins every minute
				if ((DateTime.Now - userdata[user.UID].lastDraw).TotalMinutes >= 1)
				{
					//The above check says "Take the time difference between Now and the last draw, and check
					//the total minutes". If 1 minute has passed, this will run.

					//Generate a random amount of coins from 1 - 10
					int coins = 1 + random.Next(10);

					//Once in a while, generate 10 times as many coins
					if (random.Next(100) == 0)
						coins = coins * 100;

					//Notice how we're using the dictionary like it's an array.
					userdata[user.UID].coins += coins;
					userdata[user.UID].totalCoins += coins;
					userdata[user.UID].coinDraws++;
					userdata[user.UID].lastDraw = DateTime.Now; //This tells us that the last coin draw was "now"

					//Now we add the message to our outputs
					outputs.Add(new ModuleJSONObject("You got " + coins + " coin(s). You now have " + userdata[user.UID].coins + " coin(s)."));
				}
				else
				{
					//Oops, the user has to wait! We use a "warning" object this time so the message gets
					//styled as an error type of thing.
					outputs.Add(new WarningMessageJSONObject("You can only draw every minute!"));
				}
			}
			else if (command.Command == "coingloat")
			{
				//This command demonstrates a broadcast command. Broadcast messages are sent to everyone
				//currently logged in to the chat. Normal messages (like the previous command) are sent to
				//just the user who performed the command. Broadcasting a message is as easy as setting
				//the broadcast field.

				//We're doing something a bit different than before. Now we need the ModuleJSONObject so we 
				//can set the "broadcast" field. This way, everyone in the chat gets the message.
				ModuleJSONObject message = new ModuleJSONObject("Hey losers, " + user.Username + " has " + userdata[user.UID].coins + " coin(s).");
				message.sendtype = MessageBaseSendType.Broadcast; //broadcast = true;

				outputs.Add(message);
			}
			else if (command.Command == "coincheck")
			{
				//This command demonstrates how to work with command arguments. All command arguments
				//are strings, so if you need a different type, you will have to convert. Here, our 
				//argument is a user. We use a built in function to convert the user argument to a
				//real user.

				UserInfo userToCheck;

				//Now we have an argument, so we'll need to do some stuff. The argument will be a username,
				//so we have to convert it to a UID. Luckily, there's a built in function for that:
				if (GetUserFromArgument(command.Arguments[0], users, out userToCheck) && 
					userdata.ContainsKey(userToCheck.UID))
				{
					//Tell us about the user
					outputs.Add(new ModuleJSONObject(userToCheck.Username + " has " + userdata[userToCheck.UID].coins +
						" coin(s) and has collected " + userdata[userToCheck.UID].totalCoins + " total coin(s). " +
						"They've drawn coins " + userdata[userToCheck.UID].coinDraws + " time(s)"));
				}
				else
				{
					//Oops, couldn't find the user 
					outputs.Add(new WarningMessageJSONObject(command.Arguments[0] + " isn't playing the coins game yet"));
				}

				//OK, how does this work? The "GetUserFromArgument" looks through the given dictionary of users
				//(the one the chatserver gives us) and tries to find a user with the given useraname
				//(the first argument to our command). If it finds it, it returns true and outputs the user
				//information into the "out" variable. If not, it returns false. This is why the "else"
				//outputs a warning telling us that the user isn't playing yet.

				//It is VERY important to perform both the generic user check (to see if they exist at all)
				//AND to check that the user exists in your game. Alternatively, you can just register all users
				//whenever you can and assume you always have the user available. This is unsafe though, so be careful.
			}
			else if (command.Command == "coingive")
			{
				//This command demonstrates how to send messages to other users, and how to parse
				//an argument to make an integer. When you give coins, you should send a message to
				//the other user as well to inform them of the coins they received.

				UserInfo userToGive;

				//First, just as before, check for user existence.
				//Now we have an argument, so we'll need to do some stuff. The argument will be a username,
				//so we have to convert it to a UID. Luckily, there's a built in function for that:
				if (GetUserFromArgument(command.Arguments[0], users, out userToGive) &&
					userdata.ContainsKey(userToGive.UID))
				{
					//Now, we try to parse the integer out of the second argument. It works almost EXACTLY
					//like parsing the user out of the user argument: a function that returns true or false
					//will store the parsed integer into the "out" variable.
					int coinsToGive;

					if (int.TryParse(command.Arguments[1], out coinsToGive))
					{
						//We need to make sure we have enough coins though!
						if (userdata[user.UID].coins >= coinsToGive)
						{
							//---------------------------------------------------
							//---THIS IS WHERE THE COMMAND ACTUALLY HAPPENS!!!---
							//---------------------------------------------------
							//OK, finally the meat of the command! When you give coins
							//to someone else, tell them about it! We also tell "ourselves"
							//about it, so we're going to add a regular message to the output 
							//for ourselves, but add a special message with the "recipients" field
							//set for the other user. Check it out:

							userdata[user.UID].coins -= coinsToGive;
							userdata[userToGive.UID].coins += coinsToGive;

							outputs.Add(new ModuleJSONObject("You gave " + coinsToGive + " coin(s) to " +
								userToGive.Username + ". You now have " + userdata[user.UID].coins + " coin(s)"));

							//Here is where we construct the message which gets sent to a different person.
							//All we have to do is add the user to the "recipients" field.
							ModuleJSONObject otherMessage = new ModuleJSONObject(user.Username + " gave you " +
								coinsToGive + " coin(s). You now have " + userdata[userToGive.UID].coins + " coin(s)");
							otherMessage.recipients.Add(userToGive.UID);
                     otherMessage.sendtype = MessageBaseSendType.OnlyRecipients;
							outputs.Add(otherMessage);
						}
						else
						{
							//Oops, didn't have enough coins!
							outputs.Add(new WarningMessageJSONObject("You don't have that many coins to give!"));
						}
					}
					else
					{
						//Oops, integer parsing failed.
						outputs.Add(new WarningMessageJSONObject("Couldn't parse coin amount"));
					}
				}
				else
				{
					//Oops, couldn't find the user 
					outputs.Add(new WarningMessageJSONObject(command.Arguments[0] + " isn't playing the coins game yet"));
				}
			}
			else
			{
				//This is what happens when no command was recognized. You don't have to put something like this,
				//but it's usually a good idea.
				outputs.Add(new WarningMessageJSONObject("Invalid command for coins module"));
			}

			return outputs;
		}


		//We have user data stored in the module using the dictionary "userdata", however whenever the server
		//restarts, your data will be lost! In order to stop that from happening, you tell the chat server
		//how to save and load your data. It's really as simple as it is here: I provide functions which convert
		//any object into a file, and vice-versa. All you have to do is call my functions and return the result.
		public override bool SaveFiles()
		{
			//This is called a "generic" function. You have to tell it the type of your data in the <> brackets.
			//All you have to do is repeat whatever you used to declare your user data type. You can call this
			//function more than once if you have lots of objects, but you should save them all to different files.
			//"DefaultSaveFile" is just a provided filename which is an easy way to name your file if you only have one. 
			return MyExtensions.MySerialize.SaveObject<Dictionary<int, UserCoinData>>(DefaultSaveFile, userdata);
		}

		public override bool LoadFiles()
		{
			//When saving and loading, make sure you use the same filename. Just like the SaveObject, you have
			//have to provide the type of the data you're loading. Unlike SaveObject however, you have to tell
			//it to store it "out" to the variable, so MAKE SURE you include the "out" before your data.
			return MyExtensions.MySerialize.LoadObject<Dictionary<int, UserCoinData>>(DefaultSaveFile, out userdata);
		}

		//This is a class which lets us store multiple bits of data about a user. We can put as much as we want
		//in here; this is how the dictionary maps a UID to all their data. All the data is within this "container"
		//that we've created here: the UserCoinData container.
		public class UserCoinData
		{
			public long coins = 0;
			public long totalCoins = 0;
			public long coinDraws = 0;
			public DateTime lastDraw = new DateTime(0);	//Use a DateTime to capture times of events and stuff.
														//We use 0 so it's "not a time" (kind of)
		}
    }
}
