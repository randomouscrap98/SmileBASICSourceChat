using System;
using System.Linq;
using System.Collections.Generic;
using MyExtensions;
using MyExtensions.Logging;
using System.IO;
using System.Reflection;
using ChatEssentials;
using System.Text.RegularExpressions;

namespace ModuleSystem
{
   public delegate void CommandCallback(List<JSONObject> output, int callerUID);

   public abstract class Module
   {
      protected string generalHelp = "";
      protected List<ModuleCommand> commands = new List<ModuleCommand>();

      private Options options = new Options();
      private List<Logger> loggers = new List<Logger>(); 

      public readonly Object Lock = new Object();
      public event CommandCallback OnExtraCommandOutput;

      public Module()
      {
         AddOptions(new Dictionary<string, object> { { "enabled",false } });
      }

      public List<ModuleCommand> Commands
      {
         get { return commands; }
      }

      public Dictionary<string, string> ArgumentHelp
      {
         get
         {
            return commands.SelectMany(x => x.Arguments.Where(y => y.Type == ArgumentType.Custom)).GroupBy(x => x.Name)
               .Select(x => x.First()).ToDictionary(x => x.Name, y => y.Regex);
         }
      }

      public string GeneralHelp
      {
         get { return generalHelp; }
      }

      public Options ModuleOptions
      {
         get { return options; }
      }

      public string ModuleName
      {
         get { return this.GetType().Name; }
      }

      public string DefaultSaveFile
      {
         get { return ModuleName + ".json"; }
      }

      /// <summary>
      /// Retrieves an option with the given name. If you have options you want loaded from the module config file,
      /// you can get the values from here.
      /// </summary>
      /// <returns>The option.</returns>
      /// <param name="optionName">Option name.</param>
      /// <typeparam name="T">The 1st type parameter.</typeparam>
      public T GetOption<T>(string optionName)
      {
         return options.GetAsType<T>(ModuleName, optionName);
      }

      /// <summary>
      /// Adds a logger to the list of loggers which are written with the Log() function. You don't
      /// need to use this if you're not using some fancy shmancy custom logging.
      /// </summary>
      /// <param name="logger">Logger.</param>
      public void AddLogger(Logger logger)
      {
         loggers.Add(logger);
      }

      /// <summary>
      /// Easy way for modules to write to a log. The log file is set up automatically, so if you don't 
      /// want to manage logs yourself (you shouldn't), just dump the output here. This also writes to any
      /// logger you add with AddLogger()
      /// </summary>
      /// <param name="message">Message.</param>
      /// <param name="level">Level.</param>
      protected void Log(string message, LogLevel level = LogLevel.Normal)
      {
         foreach (Logger logger in loggers)
            logger.LogGeneral(message, level, ModuleName);
      }

      //Allows modules to add their own options in an easy manner.
      protected void AddOptions(Dictionary<string, object> options)
      {
         this.options.AddOptions(ModuleName, options);
      }

      /// <summary>
      /// Given a username as a string, get the User object that matches this username.
      /// </summary>
      /// <returns><c>true</c>, if user from argument was found, <c>false</c> otherwise.</returns>
      /// <param name="argument">username as string</param>
      /// <param name="users">all users</param>
      /// <param name="user">matched user</param>
      protected bool GetUserFromArgument(string argument, Dictionary<int, UserInfo> users, out UserInfo user)
      {
         user = null;
         try
         {
            user = users.First(x => x.Value.Username == argument).Value;
            return true;
         }
         catch
         {
            return false;
         }
      }

      public List<JSONObject> FastMessage(string message, bool warning = false)
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
            return new List<JSONObject> { output };
         }
      }

      /// <summary>
      /// Modules can use this to add a generic error to their return from ProccessCommand.
      /// </summary>
      /// <param name="output">The data you'll be returning from ProcessCommand</param>
      protected void AddError(List<JSONObject> output)
      {
         ModuleJSONObject error = new ModuleJSONObject();
         error.message = "An internal error occurred for the " + Nickname + " module";
         output.Add(error);
      }

      protected void ExtraCommandOutput(List<JSONObject> outputs, int callerUID)
      {
         OnExtraCommandOutput(outputs, callerUID);
      }

      /// <summary>
      /// This is performed when the modules are loaded. You should load any necessary files here,
      /// such as save data/etc. This is only called when the module is loaded, but could be called 
      /// multiple times. 
      /// </summary>
      /// <returns><c>true</c>, if files were loaded, <c>false</c> otherwise.</returns>
      public virtual bool LoadFiles()
      {
         return true;
      }

      /// <summary>
      /// This is performed when the modules are saved. You should save any file data here, such
      /// as save data/etc. This is called in regular intervals by the module manager, so make sure
      /// you're not doing anything too time consuming here. 
      /// </summary>
      /// <returns><c>true</c>, if files were saved, <c>false</c> otherwise.</returns>
      public virtual bool SaveFiles()
      {
         return true;
      }

      /// <summary>
      /// All messages that come through the server are passed through here. If you want to perform analysis or 
      /// something (that isn't part of a command), do it here.
      /// </summary>
      /// <param name="message">Message.</param>
      /// <param name="user">User.</param>
      /// <param name="users">Users.</param>
      public virtual void ProcessMessage(UserMessageJSONObject message, UserInfo user, Dictionary<int, UserInfo> users)
      {
         //this function does nothing right now.
      }

      /// <summary>
      /// This is the function you will be using most. If you set up your list of commands properly, the module manager
      /// will call this function automatically when that command is parsed. You will get a copy of the user's command
      /// containing all their arguments, the user that sent the command, and a list of all users in the chat right now.
      /// Your function should return any output you'd like to produce for that command. I recommend doing a switch on the
      /// command.Command field and using the cases for the command output logic.
      /// </summary>
      /// <returns>The command.</returns>
      /// <param name="command">Command.</param>
      /// <param name="user">User.</param>
      /// <param name="users">Users.</param>
      public virtual List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<JSONObject> output = new List<JSONObject>();

         return output;
      }

      /// <summary>
      /// Occurs when a user joins the chat. Do not perform processing intensive things here, as the user has to wait
      /// for this to finish before entering.
      /// </summary>
      /// <param name="user">User joining the chat</param>
      /// <param name="users">User session.</param>
      public virtual List<JSONObject> OnUserJoin(UserInfo user, Dictionary<int, UserInfo> users)
      {
         return new List<JSONObject>();
      }

      /// <summary>
      /// The nickname of your module for users to see; defaults to your module name (without "module"). If you
      /// name it the same as another module, your module will not load.
      /// </summary>
      /// <value>The nickname.</value>
      public virtual string Nickname
      {
         get { return ModuleName.ToLower().Replace("module", ""); }
      }
   }

   public class ModuleLoader
   {
      public const string LoaderName = "ModuleLoader";

      //Global options file
      private Options options = new Options();

      //All the instantiated modules we want to use.
      private List<Module> activeModules = new List<Module>();
      private List<Type> allModuleTypes = new List<Type>();
      private List<Assembly> dlls = new List<Assembly>();

      //Logging object (use default unless given in constructor)
      private Logger logger = Logger.DefaultLogger;

      public ModuleLoader(Logger logger)
      {
         //Set some default options.
         Dictionary<string, object> loaderOptions = new Dictionary<string, object>() 
         {
            {"moduleDLLFolder", "plugins"},
            {"saveFolder", "save"}
         };

         options.AddOptions(LoaderName, loaderOptions);
         this.logger = logger;
      }

      public List<Module> ActiveModules
      {
         get { return activeModules; }
      }

      //Wrap module file loading so that it uses a custom directory.
      public bool LoadWrapper(Module module)
      {
         try
         {
            string saveDirectory = StringExtensions.PathFixer(StringExtensions.PathFixer(
               options.GetAsType<string>(LoaderName, "saveFolder")) + module.ModuleName);
            string currentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(saveDirectory);
            Directory.SetCurrentDirectory(saveDirectory);

            bool result = module.LoadFiles();

            Directory.SetCurrentDirectory(currentDirectory);

            return result;
         }
         catch
         {
            return false;
         }
      }

      //Wrap module file saving so that it uses a custom directory
      public bool SaveWrapper(Module module)
      {
         try
         {
            string saveDirectory = StringExtensions.PathFixer(StringExtensions.PathFixer(
               options.GetAsType<string>(LoaderName, "saveFolder")) + module.ModuleName);
            string currentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(saveDirectory);
            Directory.SetCurrentDirectory(saveDirectory);

            bool result = module.SaveFiles();

            Directory.SetCurrentDirectory(currentDirectory);

            return result;
         }
         catch
         {
            return false;
         }
      }
         
      //Run through the whole module setup process
      public bool Setup(string optionsFile, List<Type> extraModules = null)
      {
         if (extraModules == null)
            extraModules = new List<Type>();
         
         //Step one: get the user options.
         if (!options.LoadFromFile(optionsFile))
         {
            logger.Error("Cannot get options file: " + optionsFile, LoaderName);
            logger.Log("Using default loader options", LoaderName);
         }

         string dllDirectory = 
            StringExtensions.PathFixer(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)) +
            StringExtensions.PathFixer(options.GetAsType<string>(LoaderName, "moduleDLLFolder"));

         Directory.CreateDirectory(dllDirectory);

         if (Directory.Exists(dllDirectory))
         {
            //Step two: load all DLLs in user specified folder
            try
            {
               string[] files = Directory.GetFiles(dllDirectory, "*.dll");

               dlls.Clear();

               foreach (string file in files)
               {
                  logger.Log("Found plugin: " + file);
                  dlls.Add(Assembly.LoadFile(file));
               }
            }
            catch (Exception e)
            {
               logger.Error("Cannot load modules from directory: " + dllDirectory, LoaderName);
               logger.Error("Exception: " + e);
               return false;
            }
         }
         else
         {
            logger.Error("Plugin directory could not be found: " + dllDirectory);
         }

         allModuleTypes.Clear();

         //Step 3: get all Module types from dlls
         foreach (Assembly assembly in dlls)
            allModuleTypes.AddRange(assembly.GetTypes().Where(x => x.IsSubclassOf(typeof(Module))));

         //Add extra modules to module types
         foreach(Type moduleType in extraModules)
            if(moduleType.IsSubclassOf(typeof(Module)))
               allModuleTypes.Add(moduleType);

         //Step 4: instantiate modules that we will use. We're actually instantiating all of them,
         //but we throw away the ones that are disabled. This is to build up the options.
         foreach (Type type in allModuleTypes)
         {
            logger.LogGeneral("Found module: " + type.Name, LogLevel.Debug, LoaderName);
            Module tempModule = (Module)Activator.CreateInstance(type);

            //Add this module's options to the global module option file
            options.AddMissing(tempModule.ModuleOptions);

            //Only add this module if it is enabled
            if (options.GetAsType<bool>(tempModule.ModuleName, "enabled"))
            {
               //Only add this module if the files could be loaded.
               if (LoadWrapper(tempModule) || (SaveWrapper(tempModule) && LoadWrapper(tempModule)))
               {
                  tempModule.AddLogger(logger);    //Add our global logger to the module
                  activeModules.Add(tempModule);
                  logger.Log("Module activated: " + tempModule.ModuleName, LoaderName);
               }
               else
               {
                  logger.LogGeneral("Could not activate module " + tempModule.ModuleName + ", LoadFiles() failed",
                     MyExtensions.Logging.LogLevel.Error, LoaderName);
               }
            }
         }

         //Step 5: write out our combined options file and hope it's correct. Also, dish out the
         //options loaded from the file to the different modules
         if (!options.WriteToFile(optionsFile))
            logger.Error("Cannot write out module options. Each module will have defaults", LoaderName);

         Dictionary<string, Options> moduleOptions = options.BreakOptions();
         foreach(Module module in activeModules)
         {
            if (moduleOptions.ContainsKey(module.ModuleName))
            {
               module.ModuleOptions.AddOptions(moduleOptions[module.ModuleName]);
            }
         }

         //Step 6: check for command or nickname collisions
         HashSet<Tuple<Module, string>> tempCommands = new HashSet<Tuple<Module, string>>();
         foreach (Module module in activeModules)
         {
            foreach (ModuleCommand command in module.Commands)
            {
               Tuple<Module, string> commandLink = Tuple.Create(module, command.Command);

               if (tempCommands.Any(x => x.Item1 != commandLink.Item1 && x.Item2 == commandLink.Item2))
               {
                  logger.LogGeneral("Module " + module.ModuleName + " had a command collision for command: " + 
                     command.Command + "!", LogLevel.FatalError, LoaderName);
                  Reset();
                  return false;
               }

               tempCommands.Add(commandLink);
            }
         }

         List<string> nicknames = activeModules.Select(x => x.Nickname).ToList();

         if (nicknames.Count != nicknames.Distinct().Count())
         {
            logger.LogGeneral("Two modules had the same nickname. They cannot be loaded at the same time!");
            Reset();
            return false;
         }

         return true;
      }

      private void Reset()
      {
         activeModules.Clear();
         allModuleTypes.Clear();
         dlls.Clear();
      }
         
   }

   public class CommandArgument
   {
      private readonly string customRegex;
      public readonly ArgumentType Type;
      public readonly string Name;
      public readonly RepeatType Repeat;

      public CommandArgument(string name, ArgumentType type, RepeatType repeat = RepeatType.One, string customRegex = "")
      {
         Type = type;
         Name = name;
         Repeat = repeat;
         this.customRegex = customRegex;
      }

      private string BaseRegex
      {
         get
         {
            string regex = "";

            switch (Type)
            {
               case ArgumentType.User:
                  regex = @"\??\??[0-9a-zA-Z_]+";
                  break;
               case ArgumentType.FullString:
                  regex = ".*";
                  break;
               case ArgumentType.Word:
                  regex = @"[^\s]+";
                  break;
               case ArgumentType.Integer:
                  regex = @"[0-9]+";
                  break;
               case ArgumentType.Module:
                  regex = @"[^\s]+";
                  break;
               case ArgumentType.Keyword:
                  regex = Name;
                  break;
               case ArgumentType.Custom:
                  regex = customRegex;
                  break;
               default:
                  return "";
            }

            return regex;
         }
      }

      public string Regex
      {
         get
         {
            string repeatCharacter = RepeatCharacter(Repeat);

            if (!string.IsNullOrWhiteSpace(repeatCharacter))
               return @"(?:" + BaseRegex + @"\s*)" + repeatCharacter;
            else
               return BaseRegex;
         }
      }

      public string GroupCaptureRegex
      {
         get
         {
            string repeatCharacter = RepeatCharacter(Repeat);

            if (!string.IsNullOrWhiteSpace(repeatCharacter))
               return @"(" + BaseRegex + @"\s*)" + repeatCharacter;
            else
               return BaseRegex;
         }
      }

      public static string RepeatCharacter(RepeatType repeat)
      {
         if (repeat == RepeatType.OneOrMore)
            return "+";
         else if (repeat == RepeatType.ZeroOrMore)
            return "*";
         else if (repeat == RepeatType.ZeroOrOne)
            return "?";
         else
            return "";
      }
   }

   public enum ArgumentType
   {
      User,
      FullString,
      Integer,
      Word,
      Module,
      Keyword,
      Custom
   }

   public enum RepeatType
   {
      ZeroOrOne,
      One,
      OneOrMore,
      ZeroOrMore
   }

   public class ModuleCommand
   {
      public const string CommandStart = "/";

      public readonly string Command;
      public readonly List<CommandArgument> Arguments = new List<CommandArgument>();
      public readonly string Description;
      public readonly bool ShouldUpdateSpamScore;

      public ModuleCommand(string command, List<CommandArgument> arguments, string description, bool shouldUpdateSpamScore = false)
      {
         Command = command;
         Arguments = arguments;
         Description = description;
         ShouldUpdateSpamScore = shouldUpdateSpamScore;
      }

      //Return a human readable explanation of the command
      public string DisplayString
      {
         get
         {
            string display = CommandStart + Command;

            foreach (CommandArgument argument in Arguments)
            {
               if (argument.Type == ArgumentType.Keyword)
                  display += " " + argument.Name;
               else
                  display += " [" + (argument.Type == ArgumentType.User ? "?" : "") + argument.Name + "]";

               if (argument.Repeat == RepeatType.ZeroOrOne)
                  display += "(optional)";
               else if (argument.Repeat == RepeatType.OneOrMore)
                  display += "(repeat)";
               else if (argument.Repeat == RepeatType.ZeroOrMore)
                  display += "(optional,repeat)";
            }

            display += " => " + Description;

            return display;
         }
      }

      public static bool IsACommand(string message)
      {
         return Regex.IsMatch(message, @"^\s*" + CommandStart + @"\S");
      }

      //Return the regex that will be able to parse this command
      public string FullRegex
      {
         get
         {
            return @"^\s*" + CommandStart + "(" + Command + @")" + 
               string.Join("", Arguments.Select(x => 
                  ((x.Repeat == RepeatType.ZeroOrOne && Arguments.Last() == x) ? @"\s*(" : @"\s+(") + x.Regex + ")")) + @"\s*$";
         }
      }
   }

   public class UserCommand : UserMessageJSONObject
   {
      public readonly string Command;
      public readonly List<string> Arguments;
      public readonly List<List<string>> ArgumentParts;
      public readonly List<string> OriginalArguments;
      public readonly ModuleCommand MatchedCommand;

      public UserCommand(string command, List<string> parts, UserMessageJSONObject originalMessage, ModuleCommand matched) : base(originalMessage)
      {
         Command = command;
         Arguments = parts;
         OriginalArguments = new List<string>(parts);
         MatchedCommand = matched;

         ArgumentParts = new List<List<string>>();

         for (int i = 0; i < matched.Arguments.Count; i++)
         {
            List<string> argParts = new List<string>();

            foreach(Capture capture in Regex.Match(parts[i], matched.Arguments[i].GroupCaptureRegex).Groups[1].Captures)
               argParts.Add(capture.Value.Trim());

            ArgumentParts.Add(argParts);
         }
      }

      public UserCommand(UserCommand copy) : base(copy)
      {
         if (copy != null)
         {
            Command = copy.Command;
            Arguments = new List<string>(copy.Arguments);
            OriginalArguments = new List<string>(copy.OriginalArguments);
            ArgumentParts = new List<List<string>>();

            foreach (List<string> parts in copy.ArgumentParts)
               ArgumentParts.Add(new List<string>(parts));
            
            MatchedCommand = copy.MatchedCommand;
         }
      }
   }


}