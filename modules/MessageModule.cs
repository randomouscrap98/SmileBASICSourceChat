//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Collections.ObjectModel;
//using MyExtensions;
//using System.IO;
//using System.Text.RegularExpressions;
//using ModuleSystem;
//using ChatEssentials;
//
//namespace WikiaChatLogger
//{
//	public class MessageModule : Module
//	{
//		//public readonly string UserMessagesBaseDirectory;
//      //public readonly bool FastFileWrite;
//		//public const string MessageFile = "messages.dat";
//		//public const string MessageIDFile = "messageID.dat";
//		private Dictionary<int, MessageBox> userMessages = new Dictionary<int, MessageBox>();
//		//List<int> alreadySent = new List<int>();
//		//Queue<string> backlog = new Queue<string>();
//
//		public MessageModule()
//		{
//         "/messages - Show your messages\n" +
//         "/messageview <#> - Look at message with ID thing" +
//         "/messageread <# # #> - Mark message(s) as read\n" +
//         "/messagereadall - Mark all messages as read\n" +
//         "/messagedelete <# # #> - Delete message(s)\n" +
//         "/messagedeleteall - Delete all messages\n" +
//         "/messagesend <user ~ user ~ etc.> ~ <message> - Send message to user(s)\n" +
//         "/messagereply <#> <message> - Reply to message # (can also use messagesend #)\n" +
//         "/messageblock <# # #> - Block message(s)\n" +
//         "/messageunblock <# # #> - Unblock message(s)\n";
//
//         CommandArgument messageID = new CommandArgument("messageID", ArgumentType.Integer);
//         commands.AddRange(new List<ModuleCommand> {
//            new ModuleCommand("messages", new List<CommandArgument>(), "Your message box"),
//            new ModuleCommand("messageview", new List<CommandArgument> {
//               messageID
//            }, "Open message for reading"),
//            new ModuleCommand("messageread", new List<CommandArgument> {
//               messageID
//            }, "Mark message as read"),
//            new ModuleCommand("messagereadall", new List<CommandArgument>(), "Mark all messages as read"),
//            new ModuleCommand("messagedelete", new List<CommandArgument> {
//               messageID
//            }, "Delete message"),
//            new ModuleCommand("messagedeleteall", new List<CommandArgument>(), "Delete all messages"),
//            //new ModuleCommand("messagesend", 
//         });
//			//UserMessagesBaseDirectory = options.GetAsType<string>(ModuleName, "messageDirectory");
//         //FastFileWrite = options.GetAsType<bool>(ModuleName, "fastFileWrite");
//			//RejectCheck(UserMessagesBaseDirectory);
//		}
//
////		public string MessagesLink(string user)
////		{
////			return DropboxLink + "/" + UserMessagesBaseDirectory + "/" + System.Uri.EscapeUriString(user + ".txt");
////		}
//
//
//		public override bool LoadFiles()
//		{
//			//long nextID;
//			if (!MySerialize.LoadObject<Dictionary<string, MessageBox>>(DefaultSaveFile, out userMessages))
//			{
//				return false;
//			}
//
//         //userMessages.Select(x => x.Value.
//
//         Message.InitializeNextID(userMessages.Max(x => x.Value.MaxMessageID) + 1);
//			return true;
//		}
//
////		public bool WriteUserMessages(string user)
////		{
////			try
////			{
////				Directory.CreateDirectory(DropboxPath + UserMessagesBaseDirectory);
////				File.WriteAllText(Module.PathFixer(DropboxPath + UserMessagesBaseDirectory) + user + ".txt",
////					userMessages[user].AllChainsAsString());
////				return true;
////			}
////			catch
////			{
////				return false;
////			}
////		}
//
//		public override bool SaveFiles()
//		{
//			if (!MySerialize.SaveObject<Dictionary<string, MessageBox>>(DefaultSaveFile, userMessages))
//			{
//				return false;
//			}
//
////            if (!FastFileWrite)
////            {
////                foreach (string user in userMessages.Keys)
////                {
////                    if (!WriteUserMessages(user))
////                        return false;
////                }
////            }
//
//			return true;
//		}
//
//      public override List<ChatEssentials.JSONObject> ProcessCommand(UserCommand command, ChatEssentials.UserInfo user, Dictionary<int, ChatEssentials.UserInfo> users)
//      {
//			Match match;
//
//         TryRegister(user.UID);
//
//			//alreadySent = alreadySent.Intersect(info.GetAllUsers()).ToList();
//
//			#region messages
//			if (Regex.IsMatch(chunk.Message, @"^\s*/messages\s*$"))
//			{
//				//WriteUserMessages(chunk.Username);
//				//alreadySent.Add(chunk.Username);
//				return Module.NoPostProcessSymbol + chunk.Username + ", your messagebox is here" 
//					+ (userMessages[chunk.Username].HasUnreadMessages() ? " (new!)" : "") 
//					+ ":\n" + MessagesLink(chunk.Username);
//			}
//			#endregion
//
//			if (userMessages.ContainsKey(chunk.Username))
//			{
//				if (userMessages[chunk.Username].HasUnreadMessages() &&
//					!alreadySent.Contains(chunk.Username))
//				{
//					alreadySent.Add(chunk.Username);
//					WriteUserMessages(chunk.Username);
//					backlog.Enqueue(Module.NoPostProcessSymbol + chunk.Username + ", you have unread messages! View them here:\n" + MessagesLink(chunk.Username));
//				}
//				else if (!userMessages[chunk.Username].HasUnreadMessages() &&
//					alreadySent.Contains(chunk.Username))
//				{
//					alreadySent.Remove(chunk.Username);
//				}
//			}
//
//			#region readdeleteall
//			if (Regex.IsMatch(chunk.Message, @"^\s*/messagereadall\s*$"))
//			{
//				userMessages[chunk.Username].MarkAllAsRead();
//                WriteUserMessages(chunk.Username);
//				return chunk.Username + ", you've marked all your messages as read";
//			}
//			if (Regex.IsMatch(chunk.Message, @"^\s*/messagedeleteall\s*$"))
//			{
//				userMessages[chunk.Username].DeleteAll();
//                WriteUserMessages(chunk.Username);
//				return chunk.Username + ", you've deleted all your messages...";
//			}
//			#endregion
//
//			#region read
//			match = Regex.Match(chunk.Message, @"^\s*/messageread((\s+\d+)+)\s*$");
//			if (match.Success)
//			{
//				List<long> messageIDs = new List<long>();
//				if (!TryParseList(match.Groups[1].Value, out messageIDs))
//					return chunk.Username + ", there is a problem with one of your message IDs";
//
//				int messageCount = 0;
//				string output = "";
//				foreach (long messageID in messageIDs)
//				{
//					if (!userMessages[chunk.Username].MarkAsRead(messageID))
//						output += "There was a problem with message ID " + messageID + "\n";
//					else
//						messageCount++;
//				}
//                WriteUserMessages(chunk.Username);
//				return output + chunk.Username + ", you marked " + messageCount + " message".Pluralify(messageCount) + " as read";
//			}
//			#endregion
//
//			#region delete
//			match = Regex.Match(chunk.Message, @"^\s*/messagedelete((\s+\d+)+)\s*$");
//			if (match.Success)
//			{
//				List<long> messageIDs = new List<long>();
//				if (!TryParseList(match.Groups[1].Value, out messageIDs))
//					return chunk.Username + ", there is a problem with one of your message IDs";
//
//				int messageCount = 0;
//				string output = "";
//				foreach (long messageID in messageIDs)
//				{
//					if (!userMessages[chunk.Username].Delete(messageID))
//						output += "There was a problem with message ID " + messageID + "\n";
//					else
//						messageCount++;
//				}
//                WriteUserMessages(chunk.Username);
//				return output + chunk.Username + ", you deleted " + messageCount + " message".Pluralify(messageCount);
//			}
//			#endregion
//
//			#region block
//			match = Regex.Match(chunk.Message, @"^\s*/messageblock((\s+\d+)+)\s*$");
//			if (match.Success)
//			{
//				List<long> messageIDs = new List<long>();
//				if (!TryParseList(match.Groups[1].Value, out messageIDs))
//					return chunk.Username + ", there is a problem with one of your message IDs";
//
//				int messageCount = 0;
//				string output = "";
//				foreach (long messageID in messageIDs)
//				{
//					if (!userMessages[chunk.Username].Block(messageID))
//						output += "There was a problem with message ID " + messageID + "\n";
//					else
//						messageCount++;
//				}
//                WriteUserMessages(chunk.Username);
//				return output + chunk.Username + ", you blocked " + messageCount + " message".Pluralify(messageCount);
//			}
//			#endregion
//
//			#region unblock
//			match = Regex.Match(chunk.Message, @"^\s*/messageunblock((\s+\d+)+)\s*$");
//			if (match.Success)
//			{
//				List<long> messageIDs = new List<long>();
//				if (!TryParseList(match.Groups[1].Value, out messageIDs))
//					return chunk.Username + ", there is a problem with one of your message IDs";
//
//				int messageCount = 0;
//				string output = "";
//				foreach (long messageID in messageIDs)
//				{
//					if (!userMessages[chunk.Username].Unblock(messageID))
//						output += "There was a problem with message ID " + messageID + "\n";
//					else
//						messageCount++;
//				}
//                WriteUserMessages(chunk.Username);
//				return output + chunk.Username + ", you unblocked " + messageCount + " message".Pluralify(messageCount);
//			}
//			#endregion
//
//			#region messagesend
//			match = Regex.Match(chunk.Message, @"^\s*/messagesend\s+([^~]+~\s*)+\s+(.+)\s*$");
//			if (match.Success)
//			{
//				List<string> users = new List<string>();
//
//				foreach (Capture capture in match.Groups[1].Captures)
//				{
//					string user = capture.Value.Replace("~", "").Trim();
//					if (!userMessages.ContainsKey(user))
//						return chunk.Username + ", I couldn't find one of the users in your receiver list";
//
//					users.Add(user);
//				}
//
//				users.Remove(chunk.Username);
//				users.Add(chunk.Username);
//
//				MessageChain sendChain = new MessageChain(chunk.Username, match.Groups[2].Value, users);
//
//				//!!WARNING!! This writes the user's messagebox for each user! This may be a bottleneck!
//				foreach (string user in users)
//				{
//					userMessages[user].ReceiveChain(sendChain, user == chunk.Username);
//					WriteUserMessages(user);
//				}
//
//				if (users.Count == 1)
//					return chunk.Username + ", you've sent a message to yourself.";
//
//				return chunk.Username + ", you've sent your message to:\n* " + string.Join("\n* ", users.Where(x => x != chunk.Username));
//			}
//			#endregion
//
//			#region messagereply
//			match = Regex.Match(chunk.Message, @"^\s*/message(?:reply|send)\s+(\d+)\s+(.+)\s*$");
//			if(match.Success)
//			{
//				long chainID;
//				if(!long.TryParse(match.Groups[1].Value, out chainID))
//					return chunk.Username + ", there is something wrong with your reply ID";
//
//				MessageChain chain = userMessages[chunk.Username].GetMessageChain(chainID);
//				if(chain == null)
//					return chunk.Username + ", I couldn't find a message chain with that ID";
//
//				chain.AddMessage(new Message(chunk.Username, match.Groups[2].Value));
//
//				Dictionary<string, MessageBox.ReceiveCodes> codes = new Dictionary<string, MessageBox.ReceiveCodes>();
//
//				foreach (string user in chain.Receivers)
//				{
//					codes[user] = userMessages[user].ReceiveChain(chain, user == chunk.Username);
//					WriteUserMessages(user);
//				}
//
//				if (chain.Receivers.Count == 1)
//					return chunk.Username + ", you've replied to a personal message";
//
//				return chunk.Username + ", your reply has been sent to:\n* " 
//					+ string.Join("\n* ", chain.Receivers.Where(x => x != chunk.Username)
//					.Select(x => x + (codes[x] == MessageBox.ReceiveCodes.ErrorBlocked ? " (blocked!)" : "")));
//			}
//			#endregion
//
//			return "";
//		}
//
////		public override string PostProcess()
////		{
////			if (backlog.Count > 0)
////				return backlog.Dequeue();
////
////			return "";
////		}
////
//		public bool TryParseList(string list, out List<long> parsedValues)
//		{
//			string[] parts = list.Split(" ".ToArray(), StringSplitOptions.RemoveEmptyEntries);
//
//			parsedValues = new List<long>();
//
//			foreach (string part in parts)
//			{
//				long parsedLong;
//
//				if (!long.TryParse(part, out parsedLong))
//					return false;
//
//				parsedValues.Add(parsedLong);
//			}
//
//			return true;
//		}
//
////		public override string DefaultConfig()
////		{
////			return base.DefaultConfig() + "messageDirectory=messages\n"
////                + "fastFileWrite=true\n";
////		}
//
//		public override string GetHelp()
//		{
//			return "/messages - Show your messages\n" +
//            "/messageview <#> - Look at message with ID thing" +
//				"/messageread <# # #> - Mark message(s) as read\n" +
//				"/messagereadall - Mark all messages as read\n" +
//				"/messagedelete <# # #> - Delete message(s)\n" +
//				"/messagedeleteall - Delete all messages\n" +
//				"/messagesend <user ~ user ~ etc.> ~ <message> - Send message to user(s)\n" +
//				"/messagereply <#> <message> - Reply to message # (can also use messagesend #)\n" +
//				"/messageblock <# # #> - Block message(s)\n" +
//				"/messageunblock <# # #> - Unblock message(s)\n";
//		}
//
////		public override string ReportStats()
////		{
////			return "Total messages sent: " + Message.NextID;
////		}
//
//		public bool TryRegister(int user)
//		{
//			if (!userMessages.ContainsKey(user))
//			{
//				userMessages.Add(user, new MessageBox());
//				return true;
//			}
//
//			return false;
//		}
//
////		public override List<string> ProcessInterModuleCommunication()
////		{
////			int newUsers = 0;
////			List<string> outputMessages = new List<string>();
////
////			foreach (ModuleMessagePackage message in PullModuleMessages())
////			{
////				string user = message.GetPart<string>("user");
////
////				if (TryRegister(user))
////					newUsers++;
////			}
////
////			if (newUsers > 0)
////				StatusUpdate(ModuleName + " registered " + newUsers + " new users");
////
////			return outputMessages;
////		}
//	}
//
//	[Serializable()]
//	public class MessageBox
//	{
//		public enum ReceiveCodes
//		{
//			AddedChain,
//			UpdatedChain,
//			ErrorBlocked
//		}
//
//		[Serializable()]
//		private class MessageChainExtended : MessageChain
//		{
//			public bool Read = false;
//
//			public MessageChainExtended(MessageChain chain) : base(chain) { }
//		}
//
//		private List<MessageChainExtended> allMessages;
//		private List<long> blocks;
//
//		public MessageBox()
//		{
//			allMessages = new List<MessageChainExtended>();
//			blocks = new List<long>();
//		}
//
//      public long MaxMessageID
//      {
//         get { return allMessages.Max(x => x.MaxMessageID); }
//      }
//
//		public ReceiveCodes ReceiveChain(MessageChain chain, bool selfSending = false)
//		{
//			if (blocks.Contains(chain.ID))
//				return ReceiveCodes.ErrorBlocked;
//
//			int oldChain = allMessages.FindIndex(x => x.ID == chain.ID);
//			if(oldChain >= 0)
//			{
//				allMessages[oldChain].Read = selfSending; // = new MessageChainExtended(chain);
//				return ReceiveCodes.UpdatedChain;
//			}
//
//			MessageChainExtended newChain = new MessageChainExtended(chain);
//			newChain.Read = selfSending;
//			allMessages.Add(newChain);
//
//			return ReceiveCodes.AddedChain;
//		}
//
//		public void MarkAllAsRead()
//		{
//			foreach (MessageChainExtended chain in allMessages)
//				chain.Read = true;
//		}
//		public bool MarkAsRead(long chainID)
//		{
//			MessageChainExtended thisMessageChain = allMessages.FirstOrDefault(x => x.ID == chainID);
//
//			if (thisMessageChain == null)
//				return false;
//
//			thisMessageChain.Read = true;
//
//			return true;
//		}
//
//		public void DeleteAll()
//		{
//			allMessages = new List<MessageChainExtended>();
//		}
//		public bool Delete(long chainID)
//		{
//			return allMessages.RemoveAll(x => x.ID == chainID) > 0;
//		}
//
//		public bool Block(long chainID)
//		{
//			if (GetMessageChain(chainID) == null)
//				return false;
//
//			if (!Delete(chainID))
//				throw new Exception("FATAL ERROR: Could not delete message chain during block even though chainID is valid!");
//
//			blocks.Add(chainID);
//			return true;
//		}
//		public bool Unblock(long chainID)
//		{
//			return blocks.Remove(chainID);
//		}
//
//		public bool HasUnreadMessages()
//		{
//			return allMessages.Any(x => !x.Read);
//		}
//
//		public string AllChainsAsString()
//		{
//			if (allMessages.Count == 0)
//				return "No messages";
//
//			string allChains = "";
//			foreach (MessageChainExtended chain in allMessages.OrderByDescending(x => x.LastMessageDate))
//			{
//				allChains += (chain.Read ? "" : "*NEW*\n") + chain.FormattedChain() + "\n\n\n";
//			}
//			return allChains;
//		}
//
//      public string MessageBoxOverview(Dictionary<int, UserInfo> users = null)
//      {
//         if (users == null)
//            users = new Dictionary<int, UserInfo>();
//         
//         int idWidth = allMessages.Max(x => x.ID.ToString().Length);
//
//         if (idWidth < 2)
//            idWidth = 2;
//
//         Dictionary<MessageChainExtended, string> lastPosters = allMessages.ToDictionary(x => x, y => (users.ContainsKey(y.LastMessagePoster) ? users[y.LastMessagePoster].Username : "???"));
//         int posterWidth = lastPosters.Max(x => x.Value.Length);
//
//         if (posterWidth < 4)
//            posterWidth = 4;
//
//         int postWidth = 60 - idWidth - posterWidth;
//
//         string output = allMessages.Count(x => x.Read) + " unread messages.\n\n";
//
//         output += "N ID" + new string(' ', idWidth - 2 + 1) + "User" + new string(' ', posterWidth - 4 + 1) + "Post\n";
//         output += new string('-', 64) + "\n";
//
//         foreach(MessageChainExtended chain in allMessages)
//            output += (chain.Read ? " " : "*") + " " + chain.ID.ToString().PadRight(idWidth) + " " + lastPosters[chain].PadRight(posterWidth) + " " +
//               (chain.LastMessage.Length > postWidth ? chain.LastMessage.Substring(0, postWidth - 3) + "..." : chain.LastMessage) + "\n";
//
//         return output;
//         //int posterWidth = allMessages.Max(x => x.
//      }
//
//		public MessageChain GetMessageChain(long chainID)
//		{
//			return allMessages.FirstOrDefault(x => x.ID == chainID);
//		}
//
//		public int MessageCount()
//		{
//			return allMessages.Sum(x => x.Messages.Count);
//		}
//	}
//
//	[Serializable()]
//	public class MessageChain
//	{
//		private List<int> receivers;
//		private List<Message> messages;
//
//		public MessageChain(Message firstMessage, List<int> receivers)
//		{
//			messages = new List<Message>();
//			messages.Add(firstMessage);
//         this.receivers = receivers;
//		}
//		public MessageChain(Message firstMessage, params string[] receivers) : this(firstMessage, receivers.ToList()) { }
//		public MessageChain(int sender, string message, List<int> receivers) : this(new Message(sender, message), receivers) { }
//		public MessageChain(int sender, string message, params int[] receivers) : this(new Message(sender, message), receivers) { }
//		
//      public long ID
//      {
//         get { return messages.First().ID; }
//      }
//
//      public long MaxMessageID
//      {
//         get { return messages.Max(x => x.ID); }
//      }
//
//      public List<int> Receivers
//      {
//         get { return new List<int>(receivers); }
//      }
//
//		/// <summary>
//		/// Create a SHALLOW copy of given chain
//		/// </summary>
//		/// <param name="copyChain"></param>
//		public MessageChain(MessageChain copyChain)
//		{
//			Receivers = copyChain.Receivers;
//			messages = copyChain.messages;
//		}
//
//      public string FormattedChain(Dictionary<int, UserInfo> users = null)
//		{
//         if (users == null)
//            users = new Dictionary<int, UserInfo>();
//         
//         string formattedChain = "#" + ID + " - Group: " + String.Join(", ", ReceiversFromDictionary(users)) + "\n" + new string('-', Message.MaxLineLength + 2) + "\n\n";
//
//			foreach (Message message in messages)
//			{
//				formattedChain += message.FormattedContents(users) + "\n\n";
//			}
//
//			return StringExtensions.ShiftLines(formattedChain.TrimEnd("\n".ToArray()) + "\n", "| ");
//		}
//
//      public List<string> ReceiversFromDictionary(Dictionary<int, UserInfo> users)
//      {
//         return receivers.Select(x => users.ContainsKey(x) ? users[x].Username : "???").ToList();
//      }
//
//		public void AddMessage(Message message)
//		{
//			messages.Add(message);
//		}
//
//		public ReadOnlyCollection<Message> Messages
//		{
//			get { return messages.AsReadOnly(); }
//		}
//		public DateTime LastMessageDate
//		{
//			get { return messages.Last().Date; }
//		}
//      public int LastMessagePoster
//      {
//         get { return messages.Last().Sender; }
//      }
//      public string LastMessage
//      {
//         get { return messages.Last().Contents; }
//      }
//	}
//
//	[Serializable()]
//	public class Message
//	{
//		public const string DateFormat = "MM/dd/yy";
//		public const int MaxLineLength = 60;
//
//		public readonly int Sender;
//		public readonly string Contents;
//		public readonly DateTime Date;
//		public readonly long ID = 0;
//
//		private static long nextID;
//		private static readonly object Locker = new object();
//
//		public Message(int sender, string message)
//		{
//			Date = DateTime.Now;
//			Sender = sender;
//			Contents = message.RemoveLinks();
//
//			//FormattedContents = Sender + " - " + Date.ToString(DateFormat) + "\n";
//			//FormattedContents += StringExtensions.ShiftLines(StringExtensions.WordWrap(Contents, MaxLineLength));
//
//			lock (Locker)
//			{
//				ID = nextID++;
//			}
//		}
//		public Message(Message copyMessage)
//		{
//			Sender = copyMessage.Sender;
//			Contents = copyMessage.Contents;
//			//FormattedContents = copyMessage.FormattedContents;
//			Date = copyMessage.Date;
//			ID = copyMessage.ID;
//		}
//
//		public static void InitializeNextID(long id)
//		{
//			lock (Locker)
//			{
//				nextID = id;
//			}
//		}
//
//      public string FormattedContents(Dictionary<int, UserInfo> users = null)
//		{
//         string username = "???";
//
//         if (users != null && users.ContainsKey(Sender))
//            username = users[Sender].Username;
//            
//			return username + " - " + StringExtensions.LargestTime(DateTime.Now - Date) + " ago • " + Date.ToString(DateFormat) + "\n" +
//				StringExtensions.ShiftLines(StringExtensions.WordWrap(Contents, MaxLineLength));
//		}
//
//		public static long NextID
//		{
//			get { return nextID; }
//		}
//	}
//}
