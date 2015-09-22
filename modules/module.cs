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
   public abstract class Module
   {
      protected List<ModuleCommand> commands = new List<ModuleCommand>();
      private Options options = new Options();
      private List<Logger> loggers = new List<Logger>(); 

      public readonly Object Lock = new Object();

      public Module()
      {
         AddOptions(new Dictionary<string, object> { { "enabled",false } });
      }

      public List<ModuleCommand> Commands
      {
         get { return commands; }
      }

      public Options ModuleOptions
      {
         get { return options; }
      }

      public string ModuleName
      {
         get { return this.GetType().Name; }
      }

      public T GetOption<T>(string optionName)
      {
         return options.GetAsType<T>(ModuleName, optionName);
      }

      public void AddLogger(Logger logger)
      {
         loggers.Add(logger);
      }

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

      public virtual List<JSONObject> ProcessCommand(UserCommand command, User user, Dictionary<string, User> users)
      {
         List<JSONObject> output = new List<JSONObject>();

         return output;
      }

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
            {"moduleDLLFolder", "plugins"}
         };

         options.AddOptions(LoaderName, loaderOptions);
         this.logger = logger;
      }

      public List<Module> ActiveModules
      {
         get { return activeModules; }
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

         //Step two: load all DLLs in user specified folder
         try
         {
            string[] files = Directory.GetFiles(dllDirectory, "*.dll");

            dlls.Clear();

            foreach(string file in files)
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
               tempModule.AddLogger(logger);    //Add our global logger to the module
               activeModules.Add(tempModule);
               logger.Log("Module activated: " + tempModule.ModuleName, LoaderName);
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
               module.ModuleOptions.AddOptions(moduleOptions[module.ModuleName]);
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

      public CommandArgument(string name, ArgumentType type, string customRegex = "")
      {
         Type = type;
         Name = name;
         this.customRegex = customRegex;
      }

      public string Regex
      {
         get
         {
            switch (Type)
            {
               case ArgumentType.User:
                  return @"\??\??[0-9a-zA-Z_]+";
               case ArgumentType.FullString:
                  return ".*";
               case ArgumentType.Word:
                  return @"[^\s]+";
               case ArgumentType.Integer:
                  return @"[0-9]+";
               case ArgumentType.Module:
                  return @"[^\s]+";
               case ArgumentType.Custom:
                  return customRegex;
               default:
                  return "";
            }
         }
      }
   }

   public enum ArgumentType
   {
      User,
      FullString,
      Integer,
      Word,
      Module,
      Custom
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
               display += " [" + (argument.Type == ArgumentType.User ? "?" : "") + argument.Name + "]";

            display += " => " + Description;

            return display;
         }
      }

      //Return the regex that will be able to parse this command
      public string FullRegex
      {
         get
         {
            return @"^\s*" + CommandStart + "(" + Command + @")" + 
               string.Join("", Arguments.Select(x => @"\s+(" + x.Regex + ")")) + @"\s*$";
         }
      }
   }

   public class UserCommand : Message
   {
      public readonly string Command;
      public readonly List<string> Arguments;
      public readonly List<string> OriginalArguments;
      public readonly ModuleCommand MatchedCommand;

      public UserCommand(string command, List<string> parts, Message originalMessage, ModuleCommand matched) : base(originalMessage)
      {
         Command = command;
         Arguments = parts;
         OriginalArguments = new List<string>(parts);
         MatchedCommand = matched;
      }
   }


}