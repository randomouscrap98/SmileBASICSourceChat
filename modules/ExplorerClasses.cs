using MyExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WikiaChatLogger;

namespace Explorer
{
	public static class ExplorerConstants
	{
		private static Random random = new Random();

		public static class Light
		{
			public enum Levels
			{
				FullDark,
				HalfLight,
				FullLight
			}

			public class LightValue
			{
				public readonly int MapValue;
				public readonly SolidBrush MapBrush;

				public LightValue(int mapValue)
				{
					MapValue = mapValue;
					MapBrush = new SolidBrush(Color.FromArgb(mapValue));
				}
			}

			public static readonly Dictionary<Levels, LightValue> ColorValues = new Dictionary<Levels, LightValue>
			{
				{Levels.FullDark, new LightValue(unchecked((int)0xCC000000))},
				{Levels.HalfLight, new LightValue(unchecked((int)0x88000000))},
				{Levels.FullLight, new LightValue(unchecked((int)0x00000000))},
			};

			public static Dictionary<Levels, LightValue> GetOwnableColorValues()
			{
				Dictionary<Levels, LightValue> newValues = new Dictionary<Levels, LightValue>();

				foreach (var colorValue in ColorValues)
					newValues.Add(colorValue.Key, new LightValue(/*colorValue.Value.ColorValue,*/ colorValue.Value.MapValue));

				return newValues;
			}

			public static readonly Dictionary<Items.IDS, int> Sources = new Dictionary<Items.IDS, int>()
			{
				{Items.IDS.Torch, 11},
				{Items.IDS.Tower, 17},
				{Items.IDS.ChatCoins, 3},
				{Items.IDS.MagicStone, 3},
				{Items.IDS.AreaLocker, 3},
				{Items.IDS.CaveExit, 5},
				{Items.IDS.StarCrystal, 5},
			};

			public const float LightReduction = 4.0f / 5;
		}
		public static class Simulation
		{
            //These can be set
			public static double TreeGrowthHours = 1.0;
			public static double FruitGrowthHours = 1.0;
			public static double SuperFruitGrowthHours = 1.2;
			public static double StoneGrowthHours = 1.0;

			public const double StoneMinimumPercent = 0.005;
			public const double IronMinimumPercent = 0.0005;
			public const double CoalMinimumPercent = 0.0005;
			public const double TreeMinimumPercent = 0.0005;
			public const double FruitMinimumPercent = 0.0005;
			public const double FlowerMinimumPercent = 0.0005;
			public const int WorldTimeSpeedup = 30;

			public const double MeteorWaitHours = 24;

			public const double MeteorStoneGrain = 0.2;
			public const double MeteorIronGrain = 1.0;
			public const double MeteorCoalGrain = 0.4;

			public const int StoneColor = unchecked((int)0xFF0000FF);
			public const int CoalColor = unchecked((int)0xFF00FF00);
			public const int IronColor =unchecked((int)0xFFFF0000);
		}
		public static class Player
		{
            public static int HourlyStaminaRegain = 100;
			public static char CurrentPlayerToken = '¥';

			public const int ExploreScore = 1;
			public const int StatueScore = 5;
			public const int TowerScore = 25;
			public const int NewWorldScore = 200;

			public const int StartingStamina = 100;
			public const int PlayerIDBits = 20;
			public static int MaxPlayerID
			{
				get { return MathExtensions.IntPow(2, PlayerIDBits) - 1; }
			}

			public enum Actions
			{
				Pickup,
				MoveUp,
				MoveDown,
				MoveLeft,
				MoveRight,
				LookUp,
				LookDown,
				LookLeft,
				LookRight,
				Strafe,
				UseEquippedItem	//Note that some items are used automatically while equipped
			}

			public static readonly Dictionary<char, Actions> ActionMapping = new Dictionary<char,Actions>()
			{
				{'p', Actions.Pickup},
				{'u', Actions.MoveUp},
				{'d', Actions.MoveDown},
				{'r', Actions.MoveRight},
				{'l', Actions.MoveLeft},
				{'e', Actions.UseEquippedItem},
				{'s', Actions.Strafe},
				{'^', Actions.LookUp},
				{'v', Actions.LookDown},
				{'<', Actions.LookLeft},
				{'>', Actions.LookRight}
			};

			public enum Directions
			{
				Up,
				Down,
				Left,
				Right
			}
		}
		public static class Map
		{
			public const int AcreWidth = 15;
			public const int AcreHeight = 15;
			public const int MapWidthAcres = 50;
			public const int MapHeightAcres = 50;

			public const int CaveWidthAcres = 3;
			public const int CaveHeightAcres = 3;

			public const int MapLayers = 4;
			public enum Layers
			{
				AllLayers = -1,
				PermaLayer = 0,
				ObjectLayer = 1,
				PlayerLayer = 2,
			}

			public const int MaxContiguousOwnership = 9;

			public static int MapWidthFull
			{
				get { return AcreWidth * MapWidthAcres; }
			}
			public static int MapHeightFull
			{
				get { return AcreHeight * MapHeightAcres; }
			}
			public static string MapWidthFullHex
			{
				get { return MapWidthFull.ToString("X8"); }
			}
			public static string MapHeightFullHex
			{
				get { return MapHeightFull.ToString("X8"); }
			}
			public static string MapDimensionsHex
			{
				get { return MapWidthFullHex + MapHeightFullHex; }
			}
		}
		public static class Probability
		{
			public static double CaveChance = 0.02;
			public static int ChatCoinGetLow = 15;	//These can be changed from outside if needed
			public static int ChatCoinGetHigh = 25;
			public static int ChatCoinRange
			{
				get { return ChatCoinGetHigh - ChatCoinGetLow; }
			}
		}
		public static class Generation
		{
			public class GenerationData
			{
				public readonly double Lower;
				public readonly double Upper;
				/// <summary>
				/// Higher values mean sharper dropoffs. 
				/// </summary>
				public readonly double Curve;
				public readonly double Sparseness;
				public readonly bool Inverted;

				public GenerationData(double lower, double upper, double curve, double sparseness, bool inversion)
				{
					Lower = lower;
					Upper = upper;
					Curve = curve;
					Sparseness = sparseness;
					Inverted = inversion;
				}

				public double Range
				{
					get { return Upper - Lower; }
				}
			}

			public static readonly List<Tuple<Items.IDS, GenerationData>> BasicGenerationData = new List<Tuple<Items.IDS, GenerationData>>
			{
				Tuple.Create(Items.IDS.Fruit, new GenerationData(SandLevel, 0.31, 1.5, 0.22, false)),
				Tuple.Create(Items.IDS.Fruit, new GenerationData(0.31, 0.35, 1.5, 0.18, true)),
				Tuple.Create(Items.IDS.Flower, new GenerationData(0.32, 0.37, 1.0, 0.10, false)),
				Tuple.Create(Items.IDS.Flower, new GenerationData(0.37, 0.42, 1.0, 0.10, true)),
				//Tuple.Create(Items.IDS.Flower, new GenerationData(0.38, 0.40, 8.0, 0.75, false)),
				Tuple.Create(Items.IDS.Wood, new GenerationData(0.38, 0.50, 1.3, 1.0, false)),
				Tuple.Create(Items.IDS.Stone, new GenerationData(0.50, 1.0, 0.0, 1.0, false)),
				Tuple.Create(Items.IDS.Iron, new GenerationData(0.60, 1.0, 1.5, 0.95, false)),
				Tuple.Create(Items.IDS.Coal, new GenerationData(0.50, 0.80, 1.35, 0.65, false)),
				Tuple.Create(Items.IDS.Coal, new GenerationData(0.80, 1.0, 1.35, 0.65, true)),
			};

			public const double WaterLevel = 0.25;
			public const double SandLevel = 0.267;
			public const double SuperFruitPinpoint = 0.35;
			public const double SuperFruitDeviation = 0.0001;

			public static string TimeSeedHex
			{
				get { return ((int)Math.Abs(Environment.TickCount)).ToString("X8"); }
			}
			public static string GetFullPreset(int preset)
			{
				//Just use the default preset if the given preset is garbage
				if (preset < 0 || preset >= PresetBases.Count)
					preset = 0;

				return TimeSeedHex + PresetBases[preset];
			}
			public static string GetRandomFullPreset()
			{
				return TimeSeedHex + PresetBases[random.Next(PresetBases.Count)];
			}

			public static readonly List<string> PresetBases = new List<string>()
			{
				//MinRad  MaxRad   Circles  Noise    Level       Dimensions        Island\/Flatten
				"0000000A.00000014.00002710.00000000.00000000" + Map.MapDimensionsHex + "00",
				"00000006.00000014.00002710.00000000.0000000F" + Map.MapDimensionsHex + "10",
				"00000004.0000000A.00005510.00000000.0000000A" + Map.MapDimensionsHex + "00",
				"00000010.00000020.00002000.00000000.0000000F" + Map.MapDimensionsHex + "10",
				"00000010.00000030.00002000.00000000.00000000" + Map.MapDimensionsHex + "00",
				"00000090.000000AF.00000FFF.00000000.00000000" + Map.MapDimensionsHex + "00",
			};

			public static readonly List<int> CaveSurviveStates = new List<int> { 3, 4, 5, 6, 7, 8 };
			public static readonly List<int> CaveBornStates = new List<int> { 6, 7, 8 };
			public const double CaveDensity = 0.43;
			public const int CaveSimulations = 20;
		}
		public static class Items
		{
            public const int MagicStoneStaminaIncrease = 10;
            public const int FruitStaminaRestore = 10;
            public static int TorchSteps = 200;
            public const int SuperFruitStaminaRestore = 25;

			public enum Types
			{
				None = 0,			//This item is nothing
				Pickup = 1,			//Can it be picked up?
				Placeable = 2,		//Can it be placed in the overworld?
				Solid = 4,			//Can it be walked on?
				SemiSolid = 8,		//It can be passed through but not stood on
				Immortal = 16,		//Is this a permanent block?
				CanAutoPickup = 32,	//Does this item require that you enter a command to pick it up?
				SpecialAction = 64,	//Does this item cause a special action when it is picked up or stood on?
				Replaceable = 128,	//Can placed items overwrite this item?
				Owned = 256,		//Is this an object that is specifically owned by someone?
				RequiresOwner = 512,//Does the acre have to be owned before placing this?
				SetTimeStamp = 1024,//Does this object set a timestamp in metadata when placed?
				Unobtainable = 2048,//You may be able to pick it up, but it won't go in your actual inventory
				NonOwned = 4096,	//It must NOT be placed on an owned acre (includes spawn)
			}

			public enum IDS
			{
				Empty = 0,
				Grass = 1, Water,										//Indestructible tiles
				Wood, Stone, Iron, Coal, StarCrystal,					//Basic materials
				Sapling, Seed, Fruit, 									//Organics
				WaterBucket, Fence, Gate, Planks, StoneGrower, Torch,	//Craftables
				Statue, Tower, Boat,									//Special crafting
				WorldSeed, ChatCoins, MagicStone, CaveEntrance,			//Special
				CaveExit, BlockingBlock, PlayerToken, 
				CaveStone, CaveFloor, CaveFloor2,
				Dirt, SuperSeed, SuperFruit, Flower, Sand,
				AreaLocker, MeteorSummoner
			}

			public static IDS IntToID(int id)
			{
				if (Enum.IsDefined(typeof(IDS), id))
					return (IDS)id;

				return IDS.Empty;
			}
			public static bool CanReplace(IDS mapItem, IDS newItem)
			{
				return AllBlueprints[mapItem].CanPlaceHere ||
					(SelectiveReplacements.ContainsKey(mapItem) && SelectiveReplacements[mapItem].Contains(newItem));
			}

			public static readonly List<IDS> CaveLoot = new List<IDS>()
			{
				IDS.StarCrystal,
				IDS.ChatCoins, 
				IDS.MagicStone,
				IDS.AreaLocker
			};

			public static readonly Dictionary<IDS, ItemBlueprint> AllBlueprints = new Dictionary<Items.IDS, ItemBlueprint>()
			{
				{ IDS.Empty, new ItemBlueprint(IDS.Empty, "Empty Space", "", (int)(Types.Replaceable), ' ', 0x000000, 0, 0, 0, Map.Layers.AllLayers)},
				{ IDS.Grass, new ItemBlueprint(IDS.Grass, "Grass", "Gr", (int)(Types.Replaceable | Types.Placeable | Types.Pickup), '`', 0x248F24, 1, 0, 0, Map.Layers.PermaLayer)},
				{ IDS.Dirt, new ItemBlueprint(IDS.Dirt, "Dirt", "Dt", (int)(Types.Replaceable | Types.Placeable | Types.Pickup), '.', 0x866F58, 1, 0, 0, Map.Layers.PermaLayer)},
				{ IDS.Sand, new ItemBlueprint(IDS.Sand, "Sand", "Sn", (int)(Types.Replaceable | Types.Placeable | Types.Pickup), '~', 0xD4C18A, 1, 0, 0, Map.Layers.PermaLayer)},
				{ IDS.Water, new ItemBlueprint(IDS.Water, "Water", "", (int)(Types.Solid), '≈', 0x002E8A, 2, 0, 0, Map.Layers.PermaLayer)},
				{ IDS.Stone, new ItemBlueprint(IDS.Stone, "Stone", "St", (int)(Types.Solid | Types.Pickup | Types.Placeable | Types.CanAutoPickup), '░', 0x666666, 3)},
				{ IDS.Iron, new ItemBlueprint(IDS.Iron, "Iron", "Ir", (int)(Types.Solid | Types.Pickup | Types.Placeable | Types.CanAutoPickup), '▒', 0x7A2900, 4)},
				{ IDS.Coal, new ItemBlueprint(IDS.Coal, "Coal", "Cl", (int)(Types.Solid | Types.Pickup | Types.Placeable | Types.CanAutoPickup), '▓', 0x3D3D3D, 3)},
				{ IDS.StarCrystal, new ItemBlueprint(IDS.StarCrystal, "Star Crystal", "SC", (int)(Types.Pickup | Types.Placeable), '☼', 0xFFCC00)},
				{ IDS.Sapling, new ItemBlueprint(IDS.Sapling, "Sapling", "Sp", (int)(Types.Placeable | Types.Pickup | Types.SetTimeStamp), 'τ', 0x6BB224)},
				{ IDS.Wood, new ItemBlueprint(IDS.Wood, "Wood", "Wd", (int)(Types.Solid | Types.Pickup | Types.CanAutoPickup), '▲', 0x196419, 2, 1, 3)},
				{ IDS.Seed, new ItemBlueprint(IDS.Seed, "Seed", "Sd", (int)(Types.Placeable | Types.Pickup | Types.SetTimeStamp), ',', 0xCCFF99)},
				{ IDS.Fruit, new ItemBlueprint(IDS.Fruit, "Fruit", "Fr", (int)(Types.Pickup | Types.CanAutoPickup), '∞', 0xFFFFCC)},
				{ IDS.SuperSeed, new ItemBlueprint(IDS.SuperSeed, "Super Seed", "SS", (int)(Types.Placeable | Types.Pickup | Types.SetTimeStamp), '"', 0x5CE62E)},
				{ IDS.SuperFruit, new ItemBlueprint(IDS.SuperFruit, "Super Fruit", "SF", (int)(Types.Pickup | Types.CanAutoPickup), '♣', 0xFFDD00)},
				{ IDS.Flower, new ItemBlueprint(IDS.Flower, "Flower", "Fl", (int)(Types.Placeable | Types.Pickup), '*', 0xCCCCFF)},
				{ IDS.WaterBucket, new ItemBlueprint(IDS.WaterBucket, "Water Bucket", "WB", (int)(Types.Pickup | Types.Placeable), '?', 0xFFFFFF/*0x1947A3*/)},
				{ IDS.Fence, new ItemBlueprint(IDS.Fence, "Fence", "Fn", (int)(Types.Solid | Types.Placeable | Types.Pickup | Types.Owned | Types.RequiresOwner), '≡', 0x7a5229)},
				{ IDS.Gate, new ItemBlueprint(IDS.Gate, "Gate", "Gt", (int)(Types.Solid | Types.Placeable | Types.Pickup | Types.Owned | Types.RequiresOwner), '=', 0x957554)},
				{ IDS.Planks, new ItemBlueprint(IDS.Planks, "Plank", "Pl", (int)(Types.Pickup | Types.Placeable), '═', 0x754719)},
				{ IDS.StoneGrower, new ItemBlueprint(IDS.StoneGrower, "Stone Grower", "SG", (int)(Types.Solid | Types.Pickup | Types.Placeable | Types.SetTimeStamp), '•', 0xfcfcfc)},
				{ IDS.Torch, new ItemBlueprint(IDS.Torch, "Torch", "Tc", (int)(Types.Pickup | Types.Placeable), 'i', 0xEA8F25)},
				{ IDS.Statue, new ItemBlueprint(IDS.Statue, "Statue", "Su", (int)(Types.Pickup | Types.Placeable | Types.Solid | Types.Owned | Types.RequiresOwner), 'Ω', 0xE6E6E6)},
				{ IDS.Tower, new ItemBlueprint(IDS.Tower, "Tower", "Tw", (int)(Types.Pickup | Types.Placeable | Types.Solid | Types.Immortal), '⌂', 0xAA22FF)},
				//{ IDS.Boat, new ItemBlueprint(IDS.Boat, "Boat", "Bt", (int)(Types.Pickup), '?', 0xFFFFFF)},
				{ IDS.WorldSeed, new ItemBlueprint(IDS.WorldSeed, "World Seed", "", (int)(Types.Immortal | Types.SpecialAction), '¤', 0xFF33CC)},
				{ IDS.ChatCoins, new ItemBlueprint(IDS.ChatCoins, "Lucky Items", "", (int)(Types.Pickup | Types.SpecialAction | Types.Unobtainable), '$', 0xFFFF00)},
				{ IDS.MagicStone, new ItemBlueprint(IDS.MagicStone, "Magic Stone", "MS", (int)(Types.Pickup), '○', 0x66FF66)},
				{ IDS.CaveEntrance, new ItemBlueprint(IDS.CaveEntrance, "Cave Entrance", "", (int)(Types.SpecialAction | Types.Immortal), '█', 0x1A1A1A)},
				{ IDS.CaveExit, new ItemBlueprint(IDS.CaveExit, "Cave Exit", "", (int)(Types.SpecialAction | Types.Immortal), '║', 0xFFFFFF)},
				{ IDS.BlockingBlock, new ItemBlueprint(IDS.BlockingBlock, "Blocking Block", "", (int)(Types.Immortal | Types.Solid), 'X', 0x996600)},
				{ IDS.PlayerToken, new ItemBlueprint(IDS.PlayerToken, "Player Token", "", (int)(Types.Immortal | Types.Owned | Types.SemiSolid), '☺', 0xff0066, 1, 0, 0, Map.Layers.PlayerLayer)},
				{ IDS.CaveStone, new ItemBlueprint(IDS.CaveStone, "Cave Stone", "CS", (int)(Types.Pickup | Types.Solid | Types.Placeable | Types.Unobtainable), '▓', 0x3D1F00, 10) },
				{ IDS.CaveFloor, new ItemBlueprint(IDS.CaveFloor, "Cave Floor", "CF", (int)(Types.Immortal), '▒', 0x503519, 1, 0, 0, Map.Layers.PermaLayer) },
				{ IDS.CaveFloor2, new ItemBlueprint(IDS.CaveFloor2, "Cave Floor 2", "C2", (int)(Types.Immortal), '░', 0x624930, 1, 0, 0, Map.Layers.PermaLayer) },
				{ IDS.AreaLocker, new ItemBlueprint(IDS.AreaLocker, "Area Locker", "AL", (int)(Types.Pickup | Types.Placeable | Types.Solid | Types.Owned | Types.RequiresOwner), '¢', 0x006666)},
				{ IDS.MeteorSummoner, new ItemBlueprint(IDS.MeteorSummoner, "Sacrificial Altar", "SA", (int)(Types.Pickup | Types.Placeable | Types.Solid | Types.NonOwned), '!', 0x00FFFF)},
			};

			public static readonly Dictionary<IDS, Tuple<IDS, int, int>> GetExtra = new Dictionary<IDS, Tuple<IDS, int, int>>()
			{
				{IDS.Wood, Tuple.Create(IDS.Sapling, 1, 2)},
				{IDS.Fruit, Tuple.Create(IDS.Seed, 1, 2)},
				{IDS.SuperFruit, Tuple.Create(IDS.SuperSeed, 1, 1)},
			};
			//These (the keys) should ALL be things which the player can hold in their inventory and place! Other types of 
			//transformations are discouraged and should not go here. Only the player can change the world, so only player 
			//based transformations should be allowed
			public static readonly Dictionary<IDS, IDS> Transformations = new Dictionary<IDS, IDS>()
			{
				{IDS.WaterBucket, IDS.Water},
			};
			public static readonly Dictionary<IDS, Tuple<IDS, int>> AfterUseLeftovers = new Dictionary<IDS, Tuple<IDS, int>>()
			{
				{IDS.WaterBucket, Tuple.Create(IDS.Iron, 2)},
			};
			public static readonly Dictionary<IDS, Dictionary<IDS, int>> CraftingRecipes = new Dictionary<IDS, Dictionary<IDS, int>>()
			{
				{IDS.Tower, new Dictionary<IDS, int>{
					{IDS.Wood, 100},
					{IDS.Stone, 40},
					{IDS.Iron, 4},
					{IDS.StarCrystal, 1},
					{IDS.Coal, 4}}},
				{IDS.Fence, new	Dictionary<IDS, int>{
					{IDS.Wood, 5}}},
				{IDS.Statue, new Dictionary<IDS, int>{
					{IDS.Stone, 20},
					{IDS.Wood, 20}}},
				{IDS.StoneGrower, new Dictionary<IDS, int>{
					{IDS.Stone, 10},
					{IDS.Sapling, 7},
					{IDS.WaterBucket, 1},
					{IDS.Seed, 7}}},
				{IDS.Planks, new Dictionary<IDS, int>{
					{IDS.Wood, 2}}},
				{IDS.Torch, new Dictionary<IDS, int>{
					{IDS.Wood, 3},
					{IDS.Coal, 1}}},
				{IDS.Gate, new Dictionary<IDS, int>{
					{IDS.Wood, 10},
					{IDS.Stone, 3}}},
				{IDS.WaterBucket, new Dictionary<IDS, int>{
					{IDS.Iron, 2}}},
				{IDS.Grass, new Dictionary<IDS, int>{
					{IDS.Stone, 2},
					{IDS.Seed, 2},
					{IDS.Sapling, 5}}},
				{IDS.Sand, new Dictionary<IDS, int>{
					{IDS.Stone, 5}}},
				{IDS.Dirt, new Dictionary<IDS, int>{
					{IDS.Stone, 3},
					{IDS.Sapling, 3}}},
				{IDS.SuperSeed, new Dictionary<IDS, int>{
					{IDS.Sapling, 10},
					{IDS.Seed, 10},
					{IDS.WaterBucket, 1},
					/*{IDS.Coal, 2}*/}},
				{IDS.MeteorSummoner, new Dictionary<IDS, int>{
					{IDS.Wood, 100},
					{IDS.Fruit, 100},
					{IDS.Seed, 100},
					{IDS.Sapling, 100},
					}}
			};
			public static readonly Dictionary<IDS, IDS> CraftingProximityRequirements = new Dictionary<IDS, IDS>()
			{
				{IDS.WaterBucket, IDS.Water}
			};
			private static readonly Dictionary<IDS, List<IDS>> SelectiveReplacements = new Dictionary<IDS, List<IDS>>()
			{
				{IDS.Water, new List<IDS>(){IDS.Planks, IDS.Grass, IDS.Sand, IDS.Dirt}}
			};
		}
	}
	public class ItemBlueprint
	{
		public readonly char DisplayCharacter;
		public readonly string DisplayName;
		public readonly string ShorthandName;
		public readonly int StaminaRequired;
		public readonly int Types;
		public readonly int MinYield;
		public readonly int MaxYield;
		public readonly ExplorerConstants.Map.Layers Layer;
		public readonly ExplorerConstants.Items.IDS ID;
		public readonly Color PixelColor;

		public ItemBlueprint(ExplorerConstants.Items.IDS id, string displayName, string shorthand, int types, char displayCharacter = '?', int color = 0x0000000, 
			int stamina = 1, int minYield = 1, int maxYield = 1, ExplorerConstants.Map.Layers layer = ExplorerConstants.Map.Layers.ObjectLayer)
		{
			DisplayName = displayName;
			ShorthandName = shorthand;
			DisplayCharacter = displayCharacter;
			Types = types;
			Layer = layer;
			StaminaRequired = stamina;
			MinYield = minYield;
			MaxYield = maxYield;
			ID = id;

			//Assume 0 alpha is actually full alpha
			if (color > 0 && (color >> 24) == 0)
				color += unchecked((int)0xFF000000);

			PixelColor = Color.FromArgb(color);

			CheckAttributes();
		}

		public bool CanObtain
		{
			get { return ((Types & (int)ExplorerConstants.Items.Types.Unobtainable) == 0); }
		}
		public bool CanPickup
		{
			get { return ((Types & (int)(ExplorerConstants.Items.Types.Immortal | ExplorerConstants.Items.Types.Pickup)) == (int)ExplorerConstants.Items.Types.Pickup); }
		}
		public bool CanAutoPickup
		{
			get { return CanPickup && ((Types & (int)ExplorerConstants.Items.Types.CanAutoPickup) > 0); }
		}
		public bool Replaceable
		{
			get { return (Types & (int)ExplorerConstants.Items.Types.Replaceable) > 0; }
		}
		public bool Removable
		{
			get { return (Types & (int)ExplorerConstants.Items.Types.Immortal) == 0; }
		}
		public bool CanPlayerPass
		{
			get { return (Types & (int)ExplorerConstants.Items.Types.Solid) == 0; }
		}
		public bool CanPlayerHalt
		{
			get { return CanPlayerPass && (Types & (int)ExplorerConstants.Items.Types.SemiSolid) == 0; }
		}
		public bool CanPlaceHere
		{
			get { return Replaceable; }
		}
		public bool CanPlace
		{
			get { return (Types & (int)ExplorerConstants.Items.Types.Placeable) > 0; }
		}
		public bool RequiresOwnedAcre
		{
			get { return (Types & (int)ExplorerConstants.Items.Types.RequiresOwner) > 0; }
		}
		public bool OwnedItem
		{
			get { return (Types & (int)ExplorerConstants.Items.Types.Owned) > 0; }
		}
		public bool CannotOwn
		{
			get { return (Types & (int)ExplorerConstants.Items.Types.NonOwned) > 0; }
		}
		public bool ShouldSetMetaTimestamp
		{
			get { return (Types & (int)ExplorerConstants.Items.Types.SetTimeStamp) > 0; }
		}
		public bool DoesSpecialAction
		{
			get { return (Types & (int)ExplorerConstants.Items.Types.SpecialAction) > 0; }
		}
		public void CheckAttributes()
		{
			int checker = (int)(ExplorerConstants.Items.Types.Immortal | ExplorerConstants.Items.Types.Replaceable);
			if ((Types & checker) == checker)
				throw new Exception("An item blueprint cannot have both the immortal and replaceable flags set!");
			else if (ExplorerConstants.Items.CaveLoot.Contains(ID) && CanAutoPickup)
				throw new Exception("You can't make cave loot have the AutoPickup flag!");
		}
	}

   public class WorldPoint
   {
      public static string PointToKey(int x, int y)
      {
         return x + "," + y;
      }

      public static Tuple<int, int> KeyToPoint(string key)
      {
         Match match = Regex.Match(key, @"^\s*(\d+)\s*,\s*(\d+)\s*$");

         int x = -1;
         int y = -1;

         if (int.TryParse(match.Groups[1].Value, out x) && int.TryParse(match.Groups[2].Value, out y))
            return Tuple.Create(x, y);
         else
            return Tuple.Create(-1, -1);
      }
   }

	[Serializable()]
	public class World
	{
		private Random random = new Random();
		private byte[,,] worldData = new byte[ExplorerConstants.Map.MapWidthFull, ExplorerConstants.Map.MapHeightFull, ExplorerConstants.Map.MapLayers];
		private long[,] blockMeta = new long[ExplorerConstants.Map.MapWidthFull, ExplorerConstants.Map.MapHeightFull];
		private long[,] acreMeta = new long[ExplorerConstants.Map.MapWidthAcres, ExplorerConstants.Map.MapHeightAcres];
		private Dictionary<string, World> caves = new Dictionary<string, World>();
		private int spawnAcreX;
		private int spawnAcreY;
		private int spawnAcreWidth;
		private int spawnAcreHeight;
		private bool generated;
		private bool cave;
		//private bool completed;

		[OptionalField]
		private Dictionary<string, long> blockMetas;
		[OptionalField]
		private TimeSpan worldTime;
		[OptionalField]
		private DateTime lastSimulate;
		[OptionalField]
		private DateTime lastMeteor;

		[OnDeserializing]
		private void SetDefaults(StreamingContext sc)
		{
			blockMetas = new Dictionary<string, long>();
			worldTime = new TimeSpan(9,0,0);
			lastSimulate = DateTime.Now;
			lastMeteor = new DateTime(0);
		}

		public World(bool isCave = false, int spawnAcreWidth = 3, int spawnAcreHeight = 3)
		{
			SetDefaults(new StreamingContext());

			cave = isCave;
			this.spawnAcreWidth = spawnAcreWidth;
			this.spawnAcreHeight = spawnAcreHeight;

			if (cave)
			{
				worldData = new byte[ExplorerConstants.Map.AcreWidth * ExplorerConstants.Map.CaveWidthAcres, ExplorerConstants.Map.AcreHeight * ExplorerConstants.Map.CaveHeightAcres, ExplorerConstants.Map.MapLayers];
				blockMeta = new long[ExplorerConstants.Map.AcreWidth * ExplorerConstants.Map.CaveWidthAcres, ExplorerConstants.Map.AcreHeight * ExplorerConstants.Map.CaveHeightAcres];
				acreMeta = new long[ExplorerConstants.Map.CaveWidthAcres, ExplorerConstants.Map.CaveHeightAcres];
			}

			CleanReset();
		}

      public static World NewEmptyWorld()
      {
         World empty = new World();
         empty.worldData = new byte[0,0,0];
         empty.blockMeta = new long[0, 0];
         empty.acreMeta = new long[0, 0];
         return empty;
      }

      public bool IsEmpty
      {
         get { return worldData.Length == 0; }
      }

		public void CleanReset()
		{
			for (int i = 0; i < WidthFull; i++)
			{
				for (int j = 0; j < HeightFull; j++)
				{
					for (int k = 0; k < ExplorerConstants.Map.MapLayers; k++)
						worldData[i, j, k] = 0;

					SetBlockMeta(i, j, 0);
				}
			}

			for (int i = 0; i < WidthAcres; i++)
			{
				for (int j = 0; j < HeightAcres; j++)
				{
					acreMeta[i, j] = 0;
				}
			}

			caves.Clear();
			spawnAcreX = -1;
			spawnAcreY = -1;
			generated = false;
			//completed = false;
		}
		public void Generate(int preset = 0)
		{
			if (cave)
			{
				GenerateCave();
				return;
			}

			float[,] baseMap = HillGenerator.Generate(new HillGeneratorOptions(ExplorerConstants.Generation.GetFullPreset(preset)));

			//The most basic of generation
			for (int i = 0; i < WidthFull; i++)
			{
				for (int j = 0; j < HeightFull; j++)
				{
					//The ground
					if (baseMap[i, j] <= ExplorerConstants.Generation.WaterLevel)
						ForcePut(ExplorerConstants.Items.IDS.Water, i, j);
					else if (baseMap[i, j] <= ExplorerConstants.Generation.SandLevel)
						ForcePut(ExplorerConstants.Items.IDS.Sand, i, j);
					else
						ForcePut(ExplorerConstants.Items.IDS.Grass, i, j);

					foreach (var genData in ExplorerConstants.Generation.BasicGenerationData)
					{
						if (baseMap[i, j] > genData.Item2.Lower && baseMap[i, j] <= genData.Item2.Upper && ShouldSpawn(baseMap[i, j], genData.Item2))
						{
							ForcePut(genData.Item1, i, j);

							if (genData.Item1 == ExplorerConstants.Items.IDS.Stone)
								ForcePut(ExplorerConstants.Items.IDS.Dirt, i, j);
						}
					}

					if (Math.Abs(baseMap[i, j] - ExplorerConstants.Generation.SuperFruitPinpoint) < ExplorerConstants.Generation.SuperFruitDeviation)
					{
						ForcePut(ExplorerConstants.Items.IDS.SuperFruit, i, j);
					}
				}
			}

			List<Tuple<Tuple<int, int>, int>> emptySpaceCounts = new List<Tuple<Tuple<int, int>, int>>();
			int spawnBlocksAcross = WidthAcres / spawnAcreWidth;
			int spawnBlocksDown = HeightAcres / spawnAcreHeight;
			int blockWidth = ExplorerConstants.Map.AcreWidth * spawnAcreWidth;
			int blockHeight = ExplorerConstants.Map.AcreHeight * spawnAcreHeight;

			//Find a suitable spawn acre
			for (int i = 0; i < spawnBlocksAcross; i++)
			{
				for (int j = 0; j < spawnBlocksDown; j++)
				{
					int emptyCount = 0;

					for (int k = 0; k < blockWidth; k++)
						for (int l = 0; l < blockHeight; l++)		
							if (PlayerCanHalt(i * blockWidth + k, j * blockHeight + l))
								emptyCount++;

					emptySpaceCounts.Add(Tuple.Create(Tuple.Create(i * spawnAcreWidth, j * spawnAcreHeight), emptyCount));
				}
			}

			Tuple<int, int> bestAcre = emptySpaceCounts.OrderByDescending(x => x.Item2).FirstOrDefault().Item1;
			spawnAcreX = bestAcre.Item1;
			spawnAcreY = bestAcre.Item2;

			generated = true;
		}
		private bool ShouldSpawn(double mapData, ExplorerConstants.Generation.GenerationData data)//double sparseness, double curve, double lowestValue, double range, bool inverted)
		{
			/*Math.Pow(random.NextDouble(), data.Curve)*/
			//MathExtensions.ExponentialRandomFinite(data.Curve, random)
			bool curveSpawn = true;

			if (data.Curve != 0)
			{
				if (data.Inverted)
					curveSpawn = random.NextDouble() < (1 - Math.Pow(((mapData - data.Lower) / data.Range), 1 / data.Curve));
				else if (!data.Inverted)
					curveSpawn = random.NextDouble() < Math.Pow(((mapData - data.Lower) / data.Range), data.Curve);
			}

			return curveSpawn && random.NextDouble() < data.Sparseness;
		}
		private void GenerateCave()
		{
			CellularAutomaton cave = new CellularAutomaton(WidthFull, HeightFull);
			cave.Simulate(ExplorerConstants.Generation.CaveSurviveStates, ExplorerConstants.Generation.CaveBornStates,
				CellularAutomaton.BorderType.Solid, ExplorerConstants.Generation.CaveDensity, ExplorerConstants.Generation.CaveSimulations);

			byte[,] caveData = cave.GetGrid();

			//First, convert basic cave data to actual map data
			for (int i = 0; i < WidthFull; i++)
			{
				for (int j = 0; j < HeightFull; j++)
				{
					ForcePut(ExplorerConstants.Items.IDS.CaveFloor, i, j);

					if (caveData[i, j] > 0)
						ForcePut(ExplorerConstants.Items.IDS.CaveStone, i, j);
				}
			}
			
			//Gather up the 3x3 empty spaces
			List<Tuple<int, int>> emptyBlocks = GetEmptyBlocks(2);

			//Not empty enough yo
			if (emptyBlocks.Count == 0)
			{
				GenerateCave();
				return;
			}

			//Now find a nice place to put the spawn and jam it there.
			Tuple<int, int> spawnTuple = emptyBlocks[random.Next(emptyBlocks.Count)];
			emptyBlocks.RemoveAll(x => Math.Abs(x.Item1 - spawnTuple.Item1) < 5 && Math.Abs(x.Item2 - spawnTuple.Item2) < 5);

			//Eww, we're repurposing the spawn parameters as block spawn!
			spawnAcreX = spawnTuple.Item1;
			spawnAcreY = spawnTuple.Item2 + 1;

			//Set up the area
			for(int i = -1; i <= 1; i++)
				for(int j = -1; j <= 1; j++)
					ForcePut(ExplorerConstants.Items.IDS.CaveFloor2, spawnTuple.Item1 + i, spawnTuple.Item2 + j);

			//Put the "ladder"
			ForcePut(ExplorerConstants.Items.IDS.CaveExit, spawnTuple.Item1, spawnTuple.Item2);

			//Now pick 2/3 of the random cave items and stick them in there too
			List<ExplorerConstants.Items.IDS> leftoverLoot = new List<ExplorerConstants.Items.IDS>(ExplorerConstants.Items.CaveLoot);
			for (int i = 0; i < ExplorerConstants.Items.CaveLoot.Count * 2 / 3; i++)
			{
				Tuple<int, int> lootTuple = emptyBlocks[random.Next(emptyBlocks.Count)];
				emptyBlocks.RemoveAll(x => Math.Abs(x.Item1 - lootTuple.Item1) < 5 && Math.Abs(x.Item2 - lootTuple.Item2) < 5);
				if (emptyBlocks.Count == 0)
				{
					GenerateCave();
					return;
				}

				//Set up the area
				for (int x = -1; x <= 1; x++)
					for (int y = -1; y <= 1; y++)
						ForcePut(ExplorerConstants.Items.IDS.CaveFloor2, lootTuple.Item1 + x, lootTuple.Item2 + y);

				//Put the loot
				ExplorerConstants.Items.IDS loot = (i == 0 ? ExplorerConstants.Items.IDS.StarCrystal : leftoverLoot[random.Next(leftoverLoot.Count)]);
				ForcePut(loot, lootTuple.Item1,lootTuple.Item2);
				leftoverLoot.Remove(loot);
			}
		}
		public void SimulateTime()
		{
			//Simulate world time
			worldTime = worldTime.Add(new TimeSpan((DateTime.Now - lastSimulate).Ticks * ExplorerConstants.Simulation.WorldTimeSpeedup));
			lastSimulate = DateTime.Now;

			//If world time goes past midnight for today, rewind one day
			if (worldTime.TotalDays >= 1.0)
				worldTime = worldTime.Subtract(new TimeSpan(24, 0, 0));

			//Oops, something weird happened and we have a negative timespan. Let's just bring it up to
			//a nice 9 oclock.
			if (worldTime.TotalDays < 0)
				worldTime = new TimeSpan(9, 0, 0);

			//Now for the stick
			//worldTime = new TimeSpan(23, 33, 33);
		}
		public void Simulate()
		{
			SimulateTime();

			int totalBlocks = 0;
			int totalIron = 0;
			int totalStone = 0;
			int totalCoal = 0;
			int totalFlowers = 0;
			int totalFruit = 0;
			int totalTrees = 0;
			for (int i = 0; i < WidthFull; i++)
			{
				for (int j = 0; j < HeightFull; j++)
				{
					ExplorerConstants.Items.IDS item = ExplorerConstants.Items.IntToID(worldData[i, j, (int)ExplorerConstants.Map.Layers.ObjectLayer]);

					if (item == ExplorerConstants.Items.IDS.Sapling && (DateTime.Now - new DateTime(GetBlockMeta(i, j))).TotalHours >= ExplorerConstants.Simulation.TreeGrowthHours)
					{
						worldData[i, j, (int)ExplorerConstants.Map.Layers.ObjectLayer] = (byte)ExplorerConstants.Items.IDS.Wood;
						SetBlockMeta(i, j, 0);
					}
					else if (item == ExplorerConstants.Items.IDS.Seed && (DateTime.Now - new DateTime(GetBlockMeta(i, j))).TotalHours >= ExplorerConstants.Simulation.FruitGrowthHours)
					{
						worldData[i, j, (int)ExplorerConstants.Map.Layers.ObjectLayer] = (byte)ExplorerConstants.Items.IDS.Fruit;
						SetBlockMeta(i, j, 0);
					}
					else if (item == ExplorerConstants.Items.IDS.SuperSeed && (DateTime.Now - new DateTime(GetBlockMeta(i, j))).TotalHours >= ExplorerConstants.Simulation.SuperFruitGrowthHours)
					{
						worldData[i, j, (int)ExplorerConstants.Map.Layers.ObjectLayer] = (byte)ExplorerConstants.Items.IDS.SuperFruit;
						SetBlockMeta(i, j, 0);
					}
					else if (item == ExplorerConstants.Items.IDS.StoneGrower && (DateTime.Now - new DateTime(GetBlockMeta(i, j))).TotalHours >= ExplorerConstants.Simulation.StoneGrowthHours)
					{
						Tuple<int, int> stoneBlock = FindCloseHaltable(i, j);
						if(Math.Abs(stoneBlock.Item1 - i) <= 1 && Math.Abs(stoneBlock.Item2 - j) <= 1)
							worldData[stoneBlock.Item1, stoneBlock.Item2, (int)ExplorerConstants.Map.Layers.ObjectLayer] = (byte)ExplorerConstants.Items.IDS.Stone;

						SetBlockMeta(i, j, DateTime.Now.Ticks);
					}
					else if (item == ExplorerConstants.Items.IDS.MeteorSummoner)
					{
						SmashMeteor(i, j);

						if(GetObjectUnsafe(i, j, ExplorerConstants.Map.Layers.ObjectLayer) == ExplorerConstants.Items.IDS.MeteorSummoner)
							ForceRemoval(ExplorerConstants.Map.Layers.ObjectLayer, i, j);
					}
//					else if (item == ExplorerConstants.Items.IDS.CaveEntrance && HasCave(i, j))
//					{
//						caves[Tuple.Create(i, j)] = null;
//					}

					switch (item)
					{
						case ExplorerConstants.Items.IDS.Coal:
							totalCoal++;
							break;
						case ExplorerConstants.Items.IDS.Iron:
							totalIron++;
							break;
						case ExplorerConstants.Items.IDS.Stone:
							totalStone++;
							break;
						case ExplorerConstants.Items.IDS.Flower:
							totalFlowers++;
							break;
						case ExplorerConstants.Items.IDS.Fruit:
							totalFruit++;
							break;
						case ExplorerConstants.Items.IDS.Wood:
							totalTrees++;
							break;
					}

					totalBlocks++;
				}
			}

			//Drop a meteor if we don't have enough resources in the world
			if ((DateTime.Now - lastMeteor).TotalHours >= ExplorerConstants.Simulation.MeteorWaitHours &&
				((double)totalCoal / totalBlocks <= ExplorerConstants.Simulation.CoalMinimumPercent ||
				(double)totalStone / totalBlocks <= ExplorerConstants.Simulation.StoneMinimumPercent ||
				(double)totalIron / totalBlocks <= ExplorerConstants.Simulation.IronMinimumPercent))
			{
				Tuple<int, int> acre;
				int blockX, blockY;

				//Pick a random unowned acre (and make sure the center of the meteor is on land)
				do
				{
					acre = GetRandomExploredUnownedAcre();
					blockX = -ExplorerConstants.Map.AcreWidth / 2 + random.Next(ExplorerConstants.Map.AcreWidth) + (int)((acre.Item1 + 0.5) * ExplorerConstants.Map.AcreWidth);
					blockY = -ExplorerConstants.Map.AcreHeight / 2 + random.Next(ExplorerConstants.Map.AcreWidth)  + (int)((acre.Item2 + 0.5) * ExplorerConstants.Map.AcreHeight);
				} while (GetObjectUnsafe(blockX, blockY, ExplorerConstants.Map.Layers.PermaLayer) == ExplorerConstants.Items.IDS.Water);

				SmashMeteor(blockX, blockY);

				if(ValidAcre(acre.Item1, acre.Item2))
					lastMeteor = DateTime.Now;
			}

			//Grow a field if we don't have enough natural resources
			if ((double)totalFruit / totalBlocks <= ExplorerConstants.Simulation.FruitMinimumPercent ||
				(double)totalFlowers / totalBlocks <= ExplorerConstants.Simulation.FlowerMinimumPercent ||
				(double)totalTrees / totalBlocks <= ExplorerConstants.Simulation.TreeMinimumPercent)
			{
				Tuple<int, int> acre = GetRandomExploredUnownedAcre();
				Tuple<int, int> centerBlock = Tuple.Create((int)((acre.Item1 + 0.5) * ExplorerConstants.Map.AcreWidth), (int)((acre.Item2 + 0.5) * ExplorerConstants.Map.AcreHeight));

				//Generate... Eh, a 3x3 acre circle of crap.
				for (int i = 0; i < 200; i++)
				{
					int randX = random.Next(ExplorerConstants.Map.AcreWidth * 3) + (acre.Item1 - 1) * ExplorerConstants.Map.AcreWidth;
					int randY = random.Next(ExplorerConstants.Map.AcreHeight * 3) + (acre.Item2 - 1) * ExplorerConstants.Map.AcreHeight;
					double radius = Math.Pow(randX - centerBlock.Item1, 2) + Math.Pow(randY - centerBlock.Item2, 2);

					//If there's something here, skip to another location
					if(GetObjectUnsafe(randX, randY, ExplorerConstants.Map.Layers.ObjectLayer) != ExplorerConstants.Items.IDS.Empty ||
						!CanPutObject(ExplorerConstants.Items.IDS.Flower, randX, randY))
						continue;

					//These are trees
					if (radius < Math.Pow(5, 2))
						SafePutCannotOwn(ExplorerConstants.Items.IDS.Wood, randX, randY);
					else if (radius < Math.Pow(9, 2))
						SafePutCannotOwn(ExplorerConstants.Items.IDS.Flower, randX, randY);
					else if (radius < Math.Pow(13, 2))
						SafePutCannotOwn(ExplorerConstants.Items.IDS.Fruit, randX, randY);
				}
			}
		}

		public void SmashMeteor(int blockX, int blockY)
		{
			Bitmap meteorImage = new Bitmap(ExplorerConstants.Map.AcreWidth, ExplorerConstants.Map.AcreHeight);

			Pen stonePen = new Pen(new SolidBrush(Color.FromArgb(ExplorerConstants.Simulation.StoneColor)));
			Pen coalPen = new Pen(new SolidBrush(Color.FromArgb(ExplorerConstants.Simulation.CoalColor)));
			Pen ironPen = new Pen(new SolidBrush(Color.FromArgb(ExplorerConstants.Simulation.IronColor)));
			double radius;
			int maxRadius = Math.Min(ExplorerConstants.Map.AcreHeight / 2, ExplorerConstants.Map.AcreWidth / 2);
			int midX = ExplorerConstants.Map.AcreWidth / 2;
			int midY = ExplorerConstants.Map.AcreHeight / 2;

			//Draw the meteor
			using (Graphics g = Graphics.FromImage(meteorImage))
			{
				g.FillRectangle(new SolidBrush(Color.FromArgb(0)), 0, 0, ExplorerConstants.Map.AcreWidth, ExplorerConstants.Map.AcreHeight);
				for (double i = random.NextDouble() * ExplorerConstants.Simulation.MeteorStoneGrain; i < Math.PI * 2; i += ExplorerConstants.Simulation.MeteorStoneGrain)
				{
					radius = random.Next(maxRadius * 5 / 10, maxRadius * 9 / 10);
					g.DrawLine(stonePen, (float)midX, (float)midY, (float)(midX + radius * Math.Cos(i)), (float)(midY + radius * Math.Sin(i)));
				}
				for (double i = random.NextDouble() * ExplorerConstants.Simulation.MeteorCoalGrain; i < Math.PI * 2; i += ExplorerConstants.Simulation.MeteorCoalGrain)
				{
					radius = random.Next(maxRadius * 7 / 10, maxRadius);
					g.DrawLine(coalPen, (float)midX, (float)midY, (float)(midX + radius * Math.Cos(i)), (float)(midY + radius * Math.Sin(i)));
				}
				for (double i = random.NextDouble() * ExplorerConstants.Simulation.MeteorIronGrain; i < Math.PI * 2; i += ExplorerConstants.Simulation.MeteorIronGrain)
				{
					radius = random.Next(maxRadius * 3 / 10, maxRadius * 6 / 10);
					g.DrawLine(ironPen, (float)midX, (float)midY, (float)(midX + radius * Math.Cos(i)), (float)(midY + radius * Math.Sin(i)));
				}
			};

			//Convert meteor drawing to the acre data
			for (int i = 0; i < ExplorerConstants.Map.AcreWidth; i++)
			{
				for (int j = 0; j < ExplorerConstants.Map.AcreHeight; j++)
				{
					//BlockX and BlockY are the center of the meteor. To start in the upper left corner, we must move over
					//half an acre up and to the left
					int realX = -ExplorerConstants.Map.AcreWidth / 2 + i + blockX;
					int realY = -ExplorerConstants.Map.AcreHeight / 2 + j + blockY;
					switch (meteorImage.GetPixel(i, j).ToArgb())
					{
						case ExplorerConstants.Simulation.StoneColor:
							SafePutCannotOwn(ExplorerConstants.Items.IDS.Stone, realX, realY);
							ForcePut(ExplorerConstants.Items.IDS.Dirt, realX, realY);
							break;
						case ExplorerConstants.Simulation.CoalColor:
							SafePutCannotOwn(ExplorerConstants.Items.IDS.Coal, realX, realY);
							ForcePut(ExplorerConstants.Items.IDS.Dirt, realX, realY);
							break;
						case ExplorerConstants.Simulation.IronColor:
							SafePutCannotOwn(ExplorerConstants.Items.IDS.Iron, realX, realY);
							ForcePut(ExplorerConstants.Items.IDS.Dirt, realX, realY);
							break;
					}
				}
			}
		}

		public void CheckAll(int ID, int acreX, int acreY)
		{
			CheckAcre(acreX, acreY);
			CheckID(ID);
		}
		public void CheckAcre(int acreX, int acreY)
		{
			if (!ValidAcre(acreX, acreY))
				throw new Exception("Given acre(s) are bad! AcreX: " + acreX + ", AcreY: " + acreY);
		}
		public void CheckID(int ID)
		{
			if (ID > ExplorerConstants.Player.MaxPlayerID || ID < 0)
				throw new Exception("Given explorer ID is out of range! ID: " + ID);
		}
		public void CheckBlock(int blockX, int blockY)
		{
			if (!ValidBlock(blockX, blockY))
				throw new Exception("Given block(s) are bad! blockX: " + blockX + ", blockY: " + blockY);
		}
		public void CheckLayer(ExplorerConstants.Map.Layers layer)
		{
			if (!ValidLayer(layer))
				throw new Exception("Cannot retrieve this layer: " + layer);
		}

		public bool SetExplorer(int explorer, int acreX, int acreY)
		{
			CheckID(explorer);
			ulong explorerID = (ulong)explorer;

			//Don't set a new explorer if there's already one OR the acre is garbage
			//OR the grid is a cave
			if (!ValidAcre(acreX, acreY) || ((acreMeta[acreX, acreY] >> 20) & 0xFFFFF) > 0 || IsCave)
				return false;

			acreMeta[acreX, acreY] &= unchecked((long)0xFFFFFF00000FFFFF);
			acreMeta[acreX, acreY] |= unchecked((long)(explorerID << ExplorerConstants.Player.PlayerIDBits));

			return true;
		}
		public int GetExplorerID(int acreX, int acreY)
		{
			CheckAcre(acreX, acreY);	//Can't read from a crap acre
			return (int)((acreMeta[acreX, acreY] >> ExplorerConstants.Player.PlayerIDBits) & 0xFFFFF);
		}
		public bool SetOwner(int owner, int acreX, int acreY)
		{
			CheckID(owner);
			ulong ownerID = (ulong)owner;

			//Don't set a new owner if there already is one or the acre is garbage
			if (!ValidAcre(acreX, acreY) || (acreMeta[acreX, acreY] & 0xFFFFF) > 0)
				return false;

			acreMeta[acreX, acreY] &= unchecked((long)0xFFFFFFFFFFF00000);
			acreMeta[acreX, acreY] |= unchecked((long)ownerID);
			return true;
		}
		public int GetOwner(int acreX, int acreY)
		{
			CheckAcre(acreX, acreY);
			return (int)(acreMeta[acreX, acreY]  & 0xFFFFF);
		}
		public bool SetLocked(int acreX, int acreY, bool locked)
		{
			//Don't set a lock if there's no owner (or it's an invalid acre
			if (!ValidAcre(acreX, acreY) || GetOwner(acreX, acreY) == 0)
				return false;

			//Set the lock bit
			
			if(locked)
				acreMeta[acreX, acreY] |= unchecked((long)0x0000010000000000);
			else
				acreMeta[acreX, acreY] &= unchecked((long)0xFFFFFEFFFFFFFFFF);
			
			return true;
		}
		public bool GetLocked(int acreX, int acreY)
		{
			CheckAcre(acreX, acreY);

			if ((acreMeta[acreX, acreY] & unchecked((long)0x0000010000000000)) > 0)
				return true;

			return false;
		}
		
		//Returns whether or not the given player is in a locked acre (locked by someone else)
		public bool InLockedArea(int player, int acreX, int acreY)
		{
			//If you own the acre, it can't be locked, even if someone close has a lock. Also,
			//unowned acres can't be locked anyway. Finally, the spawn cannot be locked.
			if(ValidAcre(acreX, acreY) && (GetOwner(acreX, acreY) == player || GetOwner(acreX, acreY) == 0 || IsSpawn(acreX, acreY)))
				return false;

			for(int i = -1; i <= 1; i++)
			{
				for(int j = -1; j <= 1; j++)
				{
					int realX = acreX + i;
					int realY = acreY + j;

					//If a block around us (or underneath us) is owned by someone else and that acre is locked,
					//we're in the locked zone.
					if (ValidAcre(realX, realY) && GetOwner(realX, realY) != player &&
						GetLocked(realX, realY))
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Check if the acre can be claimed. Return will contain error message if 
		/// it can't, otherwise it'll be empty
		/// </summary>
		/// <param name="acreX"></param>
		/// <param name="acreY"></param>
		/// <returns></returns>
		public string CanClaim(int acreX, int acreY, int owner)
		{
			if (!ValidAcre(acreX, acreY))
				return "Acre is invalid!";

			if (IsSpawn(acreX, acreY))
				return "Acre is too close to spawn";

			if (GetOwner(acreX, acreY) > 0)
				return "Acre is already owned";

			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					int x = i + acreX;
					int y = j + acreY;

					if (ValidAcre(x, y) && (x != acreX || y != acreY) && GetOwner(x, y) > 0 &&
						GetOwner(x, y) != owner)
						return "Acre is too close to another acre with a different owner.";
				}
			}

			if (MakesLoop(acreX, acreY, owner))
			{
				return "You can't make continuous loops with your acres. Please stick to blocks of acres instead";
			}

			//if (CountContiguousOwner(acreX, acreY, owner) > ExplorerConstants.Map.MaxContiguousOwnership)
			//{
			//	return "You have too many contiguous acres. The max is " + ExplorerConstants.Map.MaxContiguousOwnership;
			//}

			return "";
		}
		public int CountContiguousOwner(int acreX, int acreY, int owner)
		{
			return GetContiguous(acreX, acreY, owner).Count;
		}
		public List<Tuple<int, int>> GetContiguous(int acreX, int acreY, int owner)
		{
			List<Tuple<int, int>> seen = new List<Tuple<int, int>>();
			Queue<Tuple<int, int>> toSearch = new Queue<Tuple<int, int>>();

			seen.Add(Tuple.Create(acreX, acreY));
			toSearch.Enqueue(Tuple.Create(acreX, acreY));

			while (toSearch.Count > 0)
			{
				for (int i = -1; i <= 1; i++)
				{
					for (int j = -1; j <= 1; j++)
					{
						Tuple<int, int> newPoint = Tuple.Create(toSearch.Peek().Item1 + i, toSearch.Peek().Item2 + j);
						if (ValidAcre(newPoint.Item1, newPoint.Item2) && GetOwner(newPoint.Item1, newPoint.Item2) == owner &&
							!seen.Contains(newPoint))
						{
							seen.Add(newPoint);
							toSearch.Enqueue(newPoint);
						}
					}
				}

				toSearch.Dequeue();
			}

			return seen;
		}
		public bool MakesLoop(int acreX, int acreY, int owner)
		{
			//Stopwatch timer = new Stopwatch();
			//timer.Start();
			byte[,] loopMap = new byte[WidthAcres, HeightAcres];

			for (int i = 0; i < WidthAcres; i++)
				for (int j = 0; j < HeightAcres; j++)
					loopMap[i, j] = 0;

			foreach (Tuple<int, int> point in GetContiguous(acreX, acreY, owner))
				loopMap[point.Item1, point.Item2] = 1;

			Queue<Tuple<int, int>> nextVisit = new Queue<Tuple<int, int>>();

			//Find a good point to enqueue first (it can be anything)
			bool getOut = false;
			for (int i = 0; i < WidthAcres; i++)
			{
				for (int j = 0; j < HeightAcres; j++)
				{
					if (loopMap[i, j] == 0)
					{
						nextVisit.Enqueue(Tuple.Create(i, j));
						getOut = true;
						break;
					}
				}

				if(getOut)
					break;
			}

			int width = WidthAcres;
			int height = HeightAcres;

			Action<int, int> blockCheckEnqueue = (acreI, acreJ) =>
			{
				if (acreI >= 0 && acreJ >= 0 && acreI < width && acreJ < height &&
					loopMap[acreI, acreJ] == 0)
				{
					nextVisit.Enqueue(Tuple.Create(acreI, acreJ));
					loopMap[acreI, acreJ] = 1;
				}
			};

			//int iterations = 0;
			while (nextVisit.Count > 0)
			{
				//iterations++;
				blockCheckEnqueue(nextVisit.Peek().Item1 + 1, nextVisit.Peek().Item2);
				blockCheckEnqueue(nextVisit.Peek().Item1 - 1, nextVisit.Peek().Item2);
				blockCheckEnqueue(nextVisit.Peek().Item1, nextVisit.Peek().Item2 + 1);
				blockCheckEnqueue(nextVisit.Peek().Item1, nextVisit.Peek().Item2 - 1);
				nextVisit.Dequeue();					
			}

			for (int i = 0; i < WidthAcres; i++)
				for (int j = 0; j < HeightAcres; j++)
					if (loopMap[i, j] == 0)
						return true;

			//timer.Stop();

			return false;
		}

		public long GetBlockMeta(int blockX, int blockY)
		{
			FixupMeta();

         string block = WorldPoint.PointToKey(blockX, blockY);//Tuple.Create(blockX, blockY);
			if (blockMetas.ContainsKey(block))
				return blockMetas[block];

			return 0;
		}
		public bool SetBlockMeta(int blockX, int blockY, long meta)
		{
			FixupMeta();
         string block = WorldPoint.PointToKey(blockX, blockY); //Tuple.Create(blockX, blockY);

			//If the meta is 0, remove the meta
			if(meta == 0)
				return blockMetas.Remove(block);

			//Otherwise update or create the meta
			if (!blockMetas.ContainsKey(block))
				blockMetas.Add(block, meta);
			else
				blockMetas[block] = meta;

			return true;
		}
		private void FixupMeta()
		{
			if (blockMeta != null)
			{
				blockMetas.Clear();

            for (int i = 0; i < WidthFull; i++)
               for (int j = 0; j < HeightFull; j++)
                  if (blockMeta[i, j] != 0)
                     blockMetas.Add(WorldPoint.PointToKey(i, j), blockMeta[i, j]);
							//blockMetas.Add(Tuple.Create(i, j), blockMeta[i, j]);

				blockMeta = null;
			}
		}

		///// <summary>
		///// Just in case you need the raw acre meta data. STRONGLY advise against use
		///// </summary>
		//public long[,] RawAcreMeta
		//{
		//	get { return acreMeta; }
		//}
		///// <summary>
		///// Just in case you need access to the raw block meta data. STRONGLY advice against use
		///// </summary>
		//public long[,] RawBlockMeta
		//{
		//	get { return blockMeta; }
		//}
		public bool Generated
		{
			get { return generated; }
		}
		public bool IsCave
		{
			get { return cave; }
		}
		public int WidthFull
		{
			get { return worldData.GetLength(0); }
		}
		public int HeightFull
		{
			get { return worldData.GetLength(1); }
		}
		public int WidthAcres
		{
			get { return WidthFull / ExplorerConstants.Map.AcreWidth; }
		}
		public int HeightAcres
		{
			get { return HeightFull / ExplorerConstants.Map.AcreHeight; }
		}
		public int SpawnAcreX
		{
			get { return spawnAcreX; }
		}
		public int SpawnAcreY
		{
			get { return spawnAcreY; }
		}
		public TimeSpan WorldTime
		{
			get { return worldTime; }
		}

		public ExplorerConstants.Items.IDS GetTopObject(int blockX, int blockY)
		{
			CheckBlock(blockX, blockY);

			for(int i = ExplorerConstants.Map.MapLayers - 1; i >= 0; i--)
				if(worldData[blockX, blockY, i] > 0)
					return (ExplorerConstants.Items.IDS)worldData[blockX, blockY,i];

			return ExplorerConstants.Items.IDS.Empty;
		}
		public ExplorerConstants.Items.IDS GetTopObjectUnsafe(int blockX, int blockY)
		{
			if (!ValidBlock(blockX, blockY))
				return ExplorerConstants.Items.IDS.Empty;

			for (int i = ExplorerConstants.Map.MapLayers - 1; i >= 0; i--)
				if (worldData[blockX, blockY, i] > 0)
					return (ExplorerConstants.Items.IDS)worldData[blockX, blockY, i];

			return ExplorerConstants.Items.IDS.Empty;
		}
		public ExplorerConstants.Items.IDS GetObject(int blockX, int blockY, ExplorerConstants.Map.Layers layer)
		{
			CheckBlock(blockX, blockY);
			CheckLayer(layer);

			return ExplorerConstants.Items.IntToID(worldData[blockX, blockY, (int)layer]);
		}
		//This will not throw any exceptions, so if you're accessing bad areas, you'll get default data.
		//This may not be what you want; suggest using the safe version in order to catch bad accesses.
		public ExplorerConstants.Items.IDS GetObjectUnsafe(int blockX, int blockY, ExplorerConstants.Map.Layers layer)
		{
			if (ValidBlock(blockX, blockY))
				return ExplorerConstants.Items.IntToID(worldData[blockX, blockY, (int)layer]);
			else
				return ExplorerConstants.Items.IDS.Empty;
		}
		public World GetCave(int blockX, int blockY)
		{
			CheckBlock(blockX, blockY);
			//Tuple<int, int> caveSpot = Tuple.Create(blockX, blockY);
         string caveSpot = WorldPoint.PointToKey(blockX, blockY);

         if (!caves.ContainsKey(caveSpot))
			{
				throw new Exception("There is no cave here");
			}

			if (caves[caveSpot].IsEmpty)
			{
				World cave = new World(true);
				cave.Generate();
				caves[caveSpot] = cave;
			}

			return caves[caveSpot];
		}
		public bool HasCave(int blockX, int blockY)
		{
         return ValidBlock(blockX, blockY) && caves.ContainsKey(WorldPoint.PointToKey(blockX, blockY));
            //Tuple.Create(blockX, blockY));
		}
		public bool RemoveCave(int blockX, int blockY)
		{
			if (HasCave(blockX, blockY))
			{
				//Tuple<int, int> caveSpot = Tuple.Create(blockX, blockY);
            string caveSpot = WorldPoint.PointToKey(blockX, blockY);
				if (caves.ContainsKey(caveSpot))
				{
					caves.Remove(caveSpot);
					return true;
				}
			}

			return false;
		}

		//A nasty function for completely removing whatever was in the given layer at the given position.
		//You're probably looking for something else, like "PickupObject"
		public bool ForceRemoval(ExplorerConstants.Map.Layers layer, int blockX, int blockY)
		{
			if (!ValidBlock(blockX, blockY) || !ValidLayer(layer))
				return false;

			worldData[blockX, blockY, (int)layer] = 0;
			return true;
		}
		public bool ForcePut(ExplorerConstants.Items.IDS item, int blockX, int blockY)
		{
			if (!ValidBlock(blockX, blockY))
				return false;

			worldData[blockX, blockY, (int)ExplorerConstants.Items.AllBlueprints[item].Layer] = (byte)item;
			return true;
		}
		
		//This function will NOT place objects in any of the following: owned acres, caves, the spawn,
		//invalid blocks,
		public bool SafePutCannotOwn(ExplorerConstants.Items.IDS item, int blockX, int blockY)
		{
			Tuple<int, int> acre = ConvertToAcre(blockX, blockY);
			if (!ValidBlock(blockX, blockY) || !ValidAcre(acre.Item1, acre.Item2) ||
				GetOwner(acre.Item1, acre.Item2) != 0 || IsSpawn(acre.Item1, acre.Item2) ||
				IsCaveEntrance(blockX, blockY))
				return false;

			worldData[blockX, blockY, (int)ExplorerConstants.Items.AllBlueprints[item].Layer] = (byte)item;
			return true;
		}

		//NOTE! None of these functions take into account player restrictions! This is handled by
		//the player class! The world doesn't care if a player owns an acre or not, it's only going
		//to try to place or remove items based on the basic placeability or removability of the item
		public bool PickupObject(int blockX, int blockY)
		{
			ExplorerConstants.Map.Layers layer = ExplorerConstants.Map.Layers.ObjectLayer;
			ExplorerConstants.Items.IDS currentObject = GetObjectUnsafe(blockX, blockY, layer);

			if (!ValidBlock(blockX, blockY) || !ExplorerConstants.Items.AllBlueprints[currentObject].CanPickup)
				return false;

			if (currentObject == ExplorerConstants.Items.IDS.Stone &&
				random.NextDouble() <= ExplorerConstants.Probability.CaveChance)
			{
				//Generate a cave entrance
				ForcePut(ExplorerConstants.Items.IDS.CaveEntrance, blockX, blockY);
				//World cave = new World(true);
				//cave.Generate();

            //caves.Add(Tuple.Create(blockX, blockY), World.NewEmptyWorld());
            caves.Add(WorldPoint.PointToKey(blockX, blockY), World.NewEmptyWorld());
			}
			else
			{
				worldData[blockX, blockY, (int)layer] = 0;
			}

			if (ExplorerConstants.Items.AllBlueprints[currentObject].ShouldSetMetaTimestamp)
				SetBlockMeta(blockX, blockY, 0);

			return true;
		}
		public bool PutObject(ExplorerConstants.Items.IDS item, int blockX, int blockY)
		{
			if (!CanPutObject(item, blockX, blockY))
				return false;

			int layer = (int)ExplorerConstants.Items.AllBlueprints[item].Layer;

			//Now just set the world data
			worldData[blockX, blockY, layer] = (byte)item;

			if (ExplorerConstants.Items.AllBlueprints[item].ShouldSetMetaTimestamp)
				SetBlockMeta(blockX, blockY, DateTime.Now.Ticks);

			return true;
		}
		public bool CanPutObject(ExplorerConstants.Items.IDS item, int blockX, int blockY)
		{
			if (!ValidBlock(blockX, blockY) || cave)
				return false;

			//It MUST be the top object. For instance, if a statue is currently on grass and then you place a solid
			//block (such as water) underneath it, how does that work? It doesn't
			ExplorerConstants.Items.IDS topObject = GetTopObject(blockX, blockY);
			ItemBlueprint itemInfo = ExplorerConstants.Items.AllBlueprints[item];

			return (itemInfo.CanPlace || ExplorerConstants.Items.Transformations.Where(x => x.Value == item).Any(x => ExplorerConstants.Items.AllBlueprints[x.Key].CanPlace)) && 
				ExplorerConstants.Items.CanReplace(topObject, item);
		}
		public bool MovePlayerToken(int blockX, int blockY, int newX, int newY)
		{
			if (!ValidBlock(blockX, blockY) || !ValidBlock(newX, newY))
				return false;

			ExplorerConstants.Items.IDS playerToken = GetObject(blockX, blockY, ExplorerConstants.Map.Layers.PlayerLayer);

			if (playerToken != ExplorerConstants.Items.IDS.PlayerToken)
				return false;

			ForceRemoval(ExplorerConstants.Map.Layers.PlayerLayer, blockX, blockY);

			if (GetObject(newX, newY, ExplorerConstants.Map.Layers.PlayerLayer) != ExplorerConstants.Items.IDS.Empty)
				return false;

			ForcePut(ExplorerConstants.Items.IDS.PlayerToken, newX, newY);
			return true;
		}
		public bool PlayerCanPass(int blockX, int blockY)
		{
			return ValidBlock(blockX, blockY) && ExplorerConstants.Items.AllBlueprints[GetTopObject(blockX, blockY)].CanPlayerPass;
		}
		public bool PlayerCanHalt(int blockX, int blockY)
		{
			return ValidBlock(blockX, blockY) && ExplorerConstants.Items.AllBlueprints[GetTopObject(blockX, blockY)].CanPlayerHalt;
		}	
		public bool IsClose(ExplorerConstants.Items.IDS item, int blockX, int blockY)
		{
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					if (ValidBlock(blockX + i, blockY + j) && GetObject(blockX + i, blockY + j, ExplorerConstants.Items.AllBlueprints[item].Layer) == item)
						return true;
				}
			}

			return false;
		}
		private bool IsCaveEntrance(int blockX, int blockY)
		{
			return ValidBlock(blockX, blockY) && GetObject(blockX, blockY, ExplorerConstants.Items.AllBlueprints[ExplorerConstants.Items.IDS.CaveEntrance].Layer) == ExplorerConstants.Items.IDS.CaveEntrance;
		}
		public bool IsSpawn(int acreX, int acreY)
		{
			return acreX >= spawnAcreX && acreX < spawnAcreX + spawnAcreWidth && acreY >= spawnAcreY && acreY < spawnAcreY + spawnAcreHeight;
		}

		public Tuple<int, int> ConvertToAcre(int blockX, int blockY)
		{
			if (ValidBlock(blockX, blockY))
				return Tuple.Create(blockX / ExplorerConstants.Map.AcreWidth, blockY / ExplorerConstants.Map.AcreHeight);

			return Tuple.Create(-1, -1);
		}
		public Tuple<int, int> FindCloseHaltable(int blockX, int blockY)
		{
			Tuple<int, int> found = Tuple.Create(-1, -1);

			int scanSquare = 1;
			do
			{
				int x, y;
				for (int i = -scanSquare; i <= scanSquare; i++)
				{
					x = i + blockX;
					y = blockY - scanSquare;
					if (ValidBlock(x, y) && PlayerCanHalt(x, y) && !IsCaveEntrance(x, y))
						found = Tuple.Create(x, y);

					y = blockY + scanSquare;
					if (ValidBlock(x, y) && PlayerCanHalt(x, y) && !IsCaveEntrance(x, y))
						found = Tuple.Create(x, y);

					x = blockX - scanSquare;
					y = i + blockY;
					if (ValidBlock(x, y) && PlayerCanHalt(x, y) && !IsCaveEntrance(x, y))
						found = Tuple.Create(x, y);

					x = blockX + scanSquare;
					if (ValidBlock(x, y) && PlayerCanHalt(x, y) && !IsCaveEntrance(x, y))
						found = Tuple.Create(x, y);
				}

				scanSquare += 1;
			} while (found.Item1 == -1 || found.Item2 == -1);

			return found;
		}
		public Tuple<int, int> FindFirstItem(ExplorerConstants.Items.IDS item, int acreX, int acreY)
		{
			Tuple<int, int> location = Tuple.Create(-1, -1);

			if (ValidAcre(acreX, acreY))
			{
				for (int i = ExplorerConstants.Map.AcreWidth - 1; i >= 0; i--)
				{
					for (int j = ExplorerConstants.Map.AcreHeight - 1; j >= 0; j--)
					{
						int x = acreX * ExplorerConstants.Map.AcreWidth + i;
						int y = acreY * ExplorerConstants.Map.AcreHeight + j;
						if (GetObject(x, y, ExplorerConstants.Items.AllBlueprints[item].Layer) == item)
							location = Tuple.Create(x, y);
					}
				}
			}

			return location;
		}
		public Tuple<int, int> GetASpawn()
		{
			int spawnX = 0;
			int spawnY = 0;
			int retries = 0;

			do
			{
				spawnX = spawnAcreX * ExplorerConstants.Map.AcreWidth + random.Next(ExplorerConstants.Map.AcreWidth * spawnAcreWidth);
				spawnY = spawnAcreY * ExplorerConstants.Map.AcreHeight + random.Next(ExplorerConstants.Map.AcreHeight * spawnAcreHeight);
				retries++;

				if (retries >= 1000 || !generated)
					return Tuple.Create(-1, -1);

			} while (!PlayerCanHalt(spawnX, spawnY));

			return Tuple.Create(spawnX, spawnY);
		}
		public Tuple<int, int> GetRandomExploredUnownedAcre()
		{
			int acreX, acreY, repeat = 0;
			do
			{
				acreX = random.Next(WidthAcres);
				acreY = random.Next(HeightAcres);
				repeat++;

				if (repeat > WidthFull * HeightFull)
					return Tuple.Create(-10, -10);

			} while (GetOwner(acreX, acreY) != 0 || GetExplorerID(acreX, acreY) == 0 || IsSpawn(acreX, acreY));

			return Tuple.Create(acreX, acreY);
		}
		public string[] GetFullMapText()
		{
			string[] map = new string[ExplorerConstants.Map.MapHeightFull];

			for (int i = 0; i < WidthFull; i++)
			{
				map[i] = "";
				for (int j = 0; j < HeightFull; j++)
				{
					map[i] += ExplorerConstants.Items.AllBlueprints[GetTopObject(j, i)].DisplayCharacter;
				}
			}

			return map;
		}
		public string[] GetAcreText(int blockX, int blockY, bool hasLight = true)
		{
			CheckBlock(blockX, blockY);
			string[] acreText = new string[ExplorerConstants.Map.AcreHeight];

			Tuple<int, int> acre = ConvertToAcre(blockX, blockY);
			int offsetX = blockX % ExplorerConstants.Map.AcreWidth;
			int offsetY = blockY % ExplorerConstants.Map.AcreHeight;

			//Bitmap lightImage = new Bitmap(ExplorerConstants.Map.AcreWidth, ExplorerConstants.Map.AcreHeight);
			//GetAcreLightMap(acre.Item1, acre.Item2, lightImage, (hasLight ? Tuple.Create(offsetX, offsetY) : Tuple.Create(-10000, -10000)));
			byte[,] lightMap = new byte[ExplorerConstants.Map.AcreWidth, ExplorerConstants.Map.AcreHeight];
			GetAcreLightMap(acre.Item1, acre.Item2, lightMap, (hasLight ? Tuple.Create(offsetX, offsetY) : Tuple.Create(-10000, -10000)));

			for (int i = 0; i < ExplorerConstants.Map.AcreHeight; i++)
			{
				acreText[i] = "";
				for (int j = 0; j < ExplorerConstants.Map.AcreWidth; j++)
				{
					int realX = acre.Item1 * ExplorerConstants.Map.AcreWidth + j;
					int realY = acre.Item2 * ExplorerConstants.Map.AcreHeight + i;

					if (realX == blockX && realY == blockY)
					{
						acreText[i] += ExplorerConstants.Player.CurrentPlayerToken;
					}
					else
					{
						ExplorerConstants.Items.IDS displayItem = GetTopObject(realX, realY);
						char display = ExplorerConstants.Items.AllBlueprints[displayItem].DisplayCharacter;

						switch ((ExplorerConstants.Light.Levels)lightMap[j, i])
						{
							case ExplorerConstants.Light.Levels.FullDark:
								display = '█';
								break;
							case ExplorerConstants.Light.Levels.HalfLight:
								if (ExplorerConstants.Items.AllBlueprints[displayItem].CanPlayerPass)
									display = '▓';
								else
									display = '█';
								break;
						}
						//int pixel = lightImage.GetPixel(j, i).ToArgb();

						//if(pixel == ExplorerConstants.Light.ColorValues[ExplorerConstants.Light.Levels.FullDark].ColorValue)
						//{
						//	display = '█';
						//}
						//else if (pixel == ExplorerConstants.Light.ColorValues[ExplorerConstants.Light.Levels.HalfLight].ColorValue)
						//{
						//	if (ExplorerConstants.Items.AllBlueprints[displayItem].CanPlayerPass)
						//		display = '▓';
						//	else
						//		display = '█';
						//}

						acreText[i] += display;
					}
				}
			}

			return acreText;
		}
		public void GetAcreLightMap(int acreX, int acreY, byte[,] lightMap, bool doublePass = false)
		{
			GetAcreLightMap(acreX, acreY, lightMap, Tuple.Create(-100000, -100000), ExplorerConstants.Items.IDS.Torch, doublePass);
		}
		public void GetAcreLightMap(int acreX, int acreY, byte[,] lightMap, Tuple<int, int> extraLightSourceLocation, ExplorerConstants.Items.IDS extraLightSource = ExplorerConstants.Items.IDS.Torch, bool doublePass = false)
		{
			//Assume infinitely dark (for caves)
			byte overallLevel = (byte)ExplorerConstants.Light.Levels.FullDark;
			int acresAcross = lightMap.GetLength(0) / ExplorerConstants.Map.AcreWidth;
			int acresDown = lightMap.GetLength(1) / ExplorerConstants.Map.AcreHeight;

			//If it's not a cave, increase the light level depending on the time of day
			if (!cave)
			{
				if (worldTime.TotalHours >= 23 || worldTime.TotalHours < 3)
					overallLevel = (byte)ExplorerConstants.Light.Levels.FullDark;
				else if (worldTime.TotalHours >= 20 || worldTime.TotalHours < 6)
					overallLevel = (byte)ExplorerConstants.Light.Levels.HalfLight;
				else
					overallLevel = (byte)ExplorerConstants.Light.Levels.FullLight;
			}

			for (int i = lightMap.GetLength(0) - 1; i >= 0; --i)
				for (int j = lightMap.GetLength(1) - 1; j >= 0; --j)
					lightMap[i, j] = overallLevel;

			//We're working with just two (no, actually one) light level for now. This may change later
			for (int i = 0; i < (doublePass ? 2 : 1); i++)
			{
				float fullness = 1.0f;
				byte lightLevel = (byte)(doublePass ? ExplorerConstants.Light.Levels.HalfLight : ExplorerConstants.Light.Levels.FullLight);

				if (i == 1)
				{
					fullness = ExplorerConstants.Light.LightReduction;
					lightLevel = (byte)ExplorerConstants.Light.Levels.FullLight;
				}

				if (lightLevel > overallLevel)
				{
					for (int acreI = -1; acreI < acresAcross + 1; acreI++)
					{
						for (int acreJ = -1; acreJ < acresDown + 1; acreJ++)
						{
							for (int blockX = 0; blockX < ExplorerConstants.Map.AcreWidth; blockX++)
							{
								for (int blockY = 0; blockY < ExplorerConstants.Map.AcreHeight; blockY++)
								{
									ExplorerConstants.Items.IDS ID = GetObjectUnsafe((acreX + acreI) * ExplorerConstants.Map.AcreWidth + blockX, (acreY + acreJ) * ExplorerConstants.Map.AcreHeight + blockY, ExplorerConstants.Map.Layers.ObjectLayer);

									//WARNING! If any light sources can be stood on, this will OVERWRITE them with the given
									//light source! Use with caution!
									if (acreI == 0 && acreJ == 0 && blockX == extraLightSourceLocation.Item1 && blockY == extraLightSourceLocation.Item2)
										ID = extraLightSource;

									if (ExplorerConstants.Light.Sources.ContainsKey(ID))
									{
										float sourceWidth = (int)(ExplorerConstants.Light.Sources[ID] * fullness);
										for (int x = -(int)(sourceWidth / 2); x <= (int)(sourceWidth / 2); x++)
										{
											for (int y = -(int)(sourceWidth / 2); y <= (int)(sourceWidth / 2); y++)
											{
												if((Math.Pow(x, 2) + Math.Pow(y, 2)) <= Math.Pow(sourceWidth / 2, 2))
												{
													int realX = (blockX + acreI * ExplorerConstants.Map.AcreWidth) + x;
													int realY = (blockY + acreJ * ExplorerConstants.Map.AcreWidth) + y;

													if (realX >= 0 && realY >= 0 && realX < lightMap.GetLength(0) && realY < lightMap.GetLength(1))
														lightMap[realX, realY] = lightLevel;
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
		}
		public Bitmap GetFullMapImage(bool exploredOnly = true, int expand = 2)
		{
			var myOwnValues = ExplorerConstants.Light.GetOwnableColorValues();
			Bitmap image = new Bitmap((WidthFull + ExplorerConstants.Map.AcreWidth * 2) * expand, (HeightFull + ExplorerConstants.Map.AcreWidth * 2) * expand);
			Pen grid = new Pen(Color.FromArgb(0x22FFFFFF), 1);
			Brush ownAcreBrush = new SolidBrush(Color.FromArgb(unchecked((int)0x3F000000)));

			int acreWidth = ExplorerConstants.Map.AcreWidth * expand;
			int acreHeight = ExplorerConstants.Map.AcreHeight * expand;
			int offsetX = ExplorerConstants.Map.AcreWidth * expand;
			int offsetY = ExplorerConstants.Map.AcreHeight * expand;
			int mapWidth = WidthFull * expand;
			int mapHeight = HeightFull * expand;

			//Set up the font and format to prepare for drawing
			StringFormat format = new StringFormat();
			format.Alignment = StringAlignment.Center;
			format.LineAlignment = StringAlignment.Near;

			int fontHeight = offsetX / 2;
			Font font = new Font("Consolas", fontHeight, GraphicsUnit.Pixel);
			byte[,] lightImage = new byte[WidthFull, HeightFull];
			GetAcreLightMap(0, 0, lightImage, true);	//Full lightmap
			int[] acreCounts = new int[byte.MaxValue];
			ExplorerConstants.Items.IDS acreFill;

			Dictionary<ExplorerConstants.Items.IDS, SolidBrush> pixelBrushes = new Dictionary<ExplorerConstants.Items.IDS, SolidBrush>();
			foreach (ItemBlueprint itemInfo in ExplorerConstants.Items.AllBlueprints.Values)
				pixelBrushes.Add(itemInfo.ID, new SolidBrush(itemInfo.PixelColor));

			using (Graphics g = Graphics.FromImage(image))
			{
				for (int i = 0; i < WidthAcres; i++)
				{
					for (int j = 0; j < HeightAcres; j++)
					{
						int acreBlockX = i * ExplorerConstants.Map.AcreWidth;
						int acreBlockY = j * ExplorerConstants.Map.AcreHeight;
						int acreBlockXUpper = acreBlockX + ExplorerConstants.Map.AcreWidth;
						int acreBlockYUpper = acreBlockY + ExplorerConstants.Map.AcreHeight;

						if (!exploredOnly || GetExplorerID(i, j) > 0)
						{
							//First, to reduce drawing operations, find the block that is filled in the most.
							acreCounts.Fill(0);

							for (int x = acreBlockX; x < acreBlockXUpper; x++)
								for (int y = acreBlockY; y < acreBlockYUpper; y++)
									acreCounts[(int)GetTopObjectUnsafe(x, y)]++;

							acreFill = ExplorerConstants.Items.IntToID(acreCounts.ToList().IndexOf(acreCounts.Max()));
							g.FillRectangle(pixelBrushes[acreFill], offsetX + acreBlockX * expand, offsetY + acreBlockY * expand, acreWidth, acreHeight);

							for (int x = acreBlockX; x < acreBlockXUpper; x++)
							{
								for (int y = acreBlockY; y < acreBlockYUpper; y++)
								{
									ExplorerConstants.Items.IDS topObject = GetTopObjectUnsafe(x, y);
									if(topObject != acreFill)
										g.FillRectangle(pixelBrushes[topObject], offsetX + x * expand, offsetY + y * expand, expand, expand);
									if(lightImage[x,y] != (byte)ExplorerConstants.Light.Levels.FullLight)
										g.FillRectangle(myOwnValues[(ExplorerConstants.Light.Levels)lightImage[x, y]].MapBrush, offsetX + x * expand, offsetY + y * expand, expand, expand);
								}
							}
						}

						if (GetOwner(i, j) != 0)
						{
							g.FillRectangle(ownAcreBrush, offsetX + i * acreWidth, offsetY + j * acreHeight, acreWidth, acreHeight);
						}

						if(i == WidthAcres - 1)
							g.DrawLine(grid, offsetX , offsetY + j * acreHeight, offsetX + mapWidth, offsetY + j * acreHeight);
					}

					g.DrawLine(grid, offsetX + i * acreWidth, offsetY, offsetX + i * acreWidth, offsetY + mapHeight);
				}

				for (int i = 0; i < WidthAcres; i++)
				{
					int xs = offsetX + i * acreWidth;
					int ys = acreHeight / 4;
					g.DrawString(i.ToString(), font, Brushes.Black,
						new Rectangle(xs, ys, acreWidth, acreHeight / 2), format);
					g.DrawString(i.ToString(), font, Brushes.Black,
						new Rectangle(xs, ys + mapHeight + offsetY, acreWidth, acreHeight / 2), format);
				}

				for (int i = 0; i < HeightAcres; i++)
				{
					int xs = 0;//offsetX + i * acreWidth;
					int ys = acreHeight / 4 + offsetY + i * acreHeight;
					g.DrawString(i.ToString(), font, Brushes.Black,
						new Rectangle(xs, ys, acreWidth, acreHeight / 2), format);
					g.DrawString(i.ToString(), font, Brushes.Black,
						new Rectangle(xs + mapWidth + offsetX, ys, acreWidth, acreHeight / 2), format);
				}
			}

			return image;
		}

		public List<Tuple<int, int, int>> GetOwnedAcres()
		{
			List<Tuple<int, int, int>> owned = new List<Tuple<int, int, int>>();
			for (int i = 0; i < WidthAcres; i++)
			{
				for (int j = 0; j < HeightAcres; j++)
				{
					int owner = GetOwner(i, j);

					if (owner > 0)
						owned.Add(Tuple.Create(i, j, owner));
				}
			}
			return owned;
		}
		public List<Tuple<int, int>> GetEmptyBlocks(int emptyRange = 1)
		{
			List<Tuple<int, int>> emptyBlocks = new List<Tuple<int,int>>();
			for (int i = emptyRange; i < WidthFull - emptyRange; i++)
			{
				for (int j = emptyRange; j < HeightFull - emptyRange; j++)
				{
					bool empty = true;

					for (int ii = -emptyRange; ii <= emptyRange; ii++)
						for (int jj = -emptyRange; jj <= emptyRange; jj++)
							if (!PlayerCanHalt(i + ii, j + jj))
								empty = false;

					if (empty)
						emptyBlocks.Add(Tuple.Create(i, j));
				}
			}

			return emptyBlocks;
		}
		public Dictionary<Tuple<ExplorerConstants.Player.Directions, int>, bool> GetAcreEdgeBlocks(int acreX, int acreY)
		{
			CheckAcre(acreX, acreY);
			Dictionary<Tuple<ExplorerConstants.Player.Directions, int>, bool> edges = new Dictionary<Tuple<ExplorerConstants.Player.Directions, int>, bool>();

			int x, y;
			for (int i = 0; i < ExplorerConstants.Map.AcreWidth; i++)
			{
				y = acreY * ExplorerConstants.Map.AcreHeight - 1;
				x = acreX * ExplorerConstants.Map.AcreWidth + i;
				edges.Add(Tuple.Create(ExplorerConstants.Player.Directions.Up, i), ValidBlock(x, y) && PlayerCanHalt(x, y));
				y = (acreY + 1) * ExplorerConstants.Map.AcreHeight;
				edges.Add(Tuple.Create(ExplorerConstants.Player.Directions.Down, i), ValidBlock(x, y) && PlayerCanHalt(x, y));
			}

			for (int i = 0; i < ExplorerConstants.Map.AcreHeight; i++)
			{
				y = acreY * ExplorerConstants.Map.AcreHeight + i;
				x = acreX * ExplorerConstants.Map.AcreWidth - 1;
				edges.Add(Tuple.Create(ExplorerConstants.Player.Directions.Left, i), ValidBlock(x, y) && PlayerCanHalt(x, y));
				x = (acreX + 1) * ExplorerConstants.Map.AcreHeight;
				edges.Add(Tuple.Create(ExplorerConstants.Player.Directions.Right, i), ValidBlock(x, y) && PlayerCanHalt(x, y));
			}

			return edges;
		}

		public bool ValidBlock(int blockX, int blockY)
		{
			return blockX >= 0 && blockX < WidthFull &&
				blockY >= 0 && blockY < HeightFull;
		}
		public bool ValidAcre(int acreX, int acreY)
		{
			return acreX >= 0 && acreX < WidthAcres &&
				acreY >= 0 && acreY < HeightAcres;
		}
		public bool ValidLayer(ExplorerConstants.Map.Layers layer)
		{
			return (int)layer >= 0 && (int)layer < ExplorerConstants.Map.MapLayers;
		}
	}

	//Each player instance is per-world, so there will be multiple player instances for each user
	[Serializable()]
	public class Player
	{
		public enum PlayerOptions
		{
			AutoFruit,
			SquareMap,
			Autopickup,
			FullLock
		}

		public static Dictionary<PlayerOptions, Tuple<bool, string>> DefaultOptions = new Dictionary<PlayerOptions, Tuple<bool, string>>
		{
			{PlayerOptions.AutoFruit, Tuple.Create(false,"Toggle automatic fruit eating when stamina gets too low")},
			{PlayerOptions.Autopickup, Tuple.Create(false,"Toggle automatic pickup of certain resources (trees, stone, etc.)")},
			{PlayerOptions.FullLock, Tuple.Create(false,"Toggle whether locks affect your ability to alter your own acres")},
			{PlayerOptions.SquareMap, Tuple.Create(false,"Toggle whether or not to display the map as a square")}
		};

      public readonly string Username = "";

		//stats
		private int score = 0;
		private int stepsTaken = 0;
		private Dictionary<ExplorerConstants.Items.IDS, int> materialsCollected = new Dictionary<ExplorerConstants.Items.IDS,int>();

		//Items/Data
		private Random random = new Random();
		private Dictionary<ExplorerConstants.Items.IDS, int> inventory = new Dictionary<ExplorerConstants.Items.IDS, int>();
		private int playerID = -1;
		private List<int> blockXList = new List<int>();
		private List<int> blockYList = new List<int>();
		private int blockListDepth = -1;
		private int maxStamina = ExplorerConstants.Player.StartingStamina;
		private int stamina = ExplorerConstants.Player.StartingStamina;
		private ExplorerConstants.Items.IDS equipped = ExplorerConstants.Items.IDS.Empty;
		private ExplorerConstants.Player.Directions facingDirection = ExplorerConstants.Player.Directions.Down;

		//Oops, extra stuff added after serialization
		[OptionalField]
		private Dictionary<ExplorerConstants.Items.IDS, int> allEquippedSteps = new Dictionary<ExplorerConstants.Items.IDS,int>();
		[OptionalField]
		private Tuple<int, int> inCaveLocation = Tuple.Create(-1, -1);
		[OptionalField]
		private Dictionary<PlayerOptions, bool> myOptions = new Dictionary<PlayerOptions, bool>();

		[OnDeserializing]
		public void SetDefaults(StreamingContext SC)
		{
			allEquippedSteps = new Dictionary<ExplorerConstants.Items.IDS, int>();
			inCaveLocation = Tuple.Create(-1, -1);
			myOptions = new Dictionary<PlayerOptions, bool>();
		}

      public Player(int ID, int spawnX, int spawnY, string username)
		{
			playerID = ID;
			AdvanceWorldDepth(spawnX, spawnY);
         Username = username;
		}

		public bool ToggleOptions(PlayerOptions option)
		{
			if (!myOptions.ContainsKey(option))
				myOptions.Add(option, DefaultOptions[option].Item1);

			myOptions[option] = !myOptions[option];
			return myOptions[option];
		}
		public bool GetOption(PlayerOptions option)
		{
			if (!myOptions.ContainsKey(option))
				myOptions.Add(option, DefaultOptions[option].Item1);

			return myOptions[option];
		}
		public int RestoreStamina(int amount)
		{
			stamina += amount;

			if (stamina > maxStamina)
				stamina = maxStamina;

			return stamina;
		}
		public int IncreaseMaxStamina(int amount)
		{
			maxStamina += amount;
			return maxStamina;
		}
		public void UseEquippedItem()
		{
			RemoveItem(equipped, 1);

			EquippedSteps = 0;
		}
		public bool RemoveItem(ExplorerConstants.Items.IDS item, int amount = 1)
		{
			if (!inventory.ContainsKey(item) || inventory[item] < amount)
				return false;

			inventory[item] -= amount;

			if (ExplorerConstants.Items.AfterUseLeftovers.ContainsKey(item))
				GetItem(ExplorerConstants.Items.AfterUseLeftovers[item].Item1,
					ExplorerConstants.Items.AfterUseLeftovers[item].Item2 * amount);

			return true;
		}
		public void AdvanceWorldDepth(int newBlockX, int newBlockY)
		{
			blockListDepth++;
			blockXList.Add(newBlockX);
			blockYList.Add(newBlockY);
		}
		public bool RetreatWorldDepth()
		{
			if (blockListDepth == 0)
				return false;

			blockXList.RemoveAt(blockListDepth);
			blockYList.RemoveAt(blockListDepth);
			blockListDepth--;

			return true;
		}

		//Perform the given actions in the given world. Returns all the errors that occurred
		public string PerformActions(List<ExplorerConstants.Player.Actions> actions, World world, World baseWorld, out int getChatCoins)
		{
			getChatCoins = 0;
			bool stopActions = false;
			bool strafing = false;
			int oblockX = BlockX;
			int oblockY = BlockY;
			Tuple<int, int> acre;
			List<string> errorMessages = new List<string>();
			int beforeFruit = ItemAmount(ExplorerConstants.Items.IDS.Fruit) + ItemAmount(ExplorerConstants.Items.IDS.SuperFruit);

			//Force the removal of your token so it doesn't get in the way
			if(!InCave)
				world.ForceRemoval(ExplorerConstants.Map.Layers.PlayerLayer, BlockX, BlockY);

			foreach (ExplorerConstants.Player.Actions action in actions)
			{
				if (stopActions)
				{
					break;
				}

				int facingX = BlockX;
				int facingY = BlockY;
				int newX = BlockX;
				int newY = BlockY;

				switch (facingDirection)
				{
					case ExplorerConstants.Player.Directions.Right:
						facingX++;
						break;
					case ExplorerConstants.Player.Directions.Left:
						facingX--;
						break;
					case ExplorerConstants.Player.Directions.Down:
						facingY++;
						break;
					case ExplorerConstants.Player.Directions.Up:
						facingY--;
						break;
				}

				ExplorerConstants.Items.IDS facingItem = ExplorerConstants.Items.IDS.Empty;
				if(world.ValidBlock(facingX, facingY))
					facingItem = world.GetObject(facingX, facingY, ExplorerConstants.Map.Layers.ObjectLayer);

				ItemBlueprint facingInfo = ExplorerConstants.Items.AllBlueprints[facingItem];

				switch (action)
				{
					case ExplorerConstants.Player.Actions.Pickup:
						if (!world.ValidBlock(facingX, facingY))
						{
							errorMessages.Add("-You cannot pickup items here");
						}
						else if (stamina >= facingInfo.StaminaRequired)
						{
							acre = world.ConvertToAcre(facingX, facingY);
							if (facingInfo.OwnedItem && world.GetOwner(acre.Item1, acre.Item2) != playerID && !world.IsCave)
							{
								errorMessages.Add("-You cannot pick up items that aren't yours!");
							}
							else if (world.InLockedArea(LockID, acre.Item1, acre.Item2))
							{
								errorMessages.Add("-You cannot pick up items in a locked area!");
							}
							else if (world.PickupObject(facingX, facingY))
							{
								GetItem(facingItem);
								stamina -= facingInfo.StaminaRequired;
								if (facingItem == ExplorerConstants.Items.IDS.Statue)
								{
									score -= ExplorerConstants.Player.StatueScore;
								}
								else if (facingItem == ExplorerConstants.Items.IDS.AreaLocker && !world.IsCave)
								{
									world.SetLocked(acre.Item1, acre.Item2, false);
									errorMessages.Add("-You've unlocked acre " + acre.Item1 + "-" + acre.Item2 + " plus the surrounding acres");
								}

								if (facingItem == ExplorerConstants.Items.IDS.ChatCoins)
								{
                           //This is just a temporary solution!
                           List<ExplorerConstants.Items.IDS> possiblePickups = new List<ExplorerConstants.Items.IDS> {
                              ExplorerConstants.Items.IDS.Coal,
                              ExplorerConstants.Items.IDS.Dirt,
                              ExplorerConstants.Items.IDS.Fence,
                              ExplorerConstants.Items.IDS.Flower,
                              ExplorerConstants.Items.IDS.Fruit,
                              ExplorerConstants.Items.IDS.Grass,
                              ExplorerConstants.Items.IDS.Iron,
                              ExplorerConstants.Items.IDS.Planks,
                              ExplorerConstants.Items.IDS.Sand,
                              ExplorerConstants.Items.IDS.Sapling,
                              ExplorerConstants.Items.IDS.Seed,
                              ExplorerConstants.Items.IDS.Stone,
                              ExplorerConstants.Items.IDS.Torch,
                              ExplorerConstants.Items.IDS.Wood
                           };
                           possiblePickups.Shuffle();
									int coins = ExplorerConstants.Probability.ChatCoinGetLow + random.Next(ExplorerConstants.Probability.ChatCoinRange);
                           GetItem(possiblePickups[0], coins);
                           errorMessages.Add("-You picked up " + coins + " " + ExplorerConstants.Items.AllBlueprints[possiblePickups[0]].DisplayName + "s! Cool!");
                           //errorMessages.Add("-You picked up " + coins + " chat coins! Cool!");
									//getChatCoins += coins;
								}

								//You picked up the cave item! We need to "collapse" the cave.
								if (ExplorerConstants.Items.CaveLoot.Contains(facingItem) && world.IsCave)
								{
									errorMessages.Add("-Picking up the " + facingInfo.DisplayName + " caused you to teleport out of the cave!");
									LeaveCave(baseWorld);
									world = baseWorld;
									stopActions = true;
								}
							}
							else
							{
								errorMessages.Add("-There was no object to pick up");
							}
						}
						break;
					case ExplorerConstants.Player.Actions.LookUp:
						facingDirection = ExplorerConstants.Player.Directions.Up;
						break;
					case ExplorerConstants.Player.Actions.LookDown:
						facingDirection = ExplorerConstants.Player.Directions.Down;
						break;
					case ExplorerConstants.Player.Actions.LookLeft:
						facingDirection = ExplorerConstants.Player.Directions.Left;
						break;
					case ExplorerConstants.Player.Actions.LookRight:
						facingDirection = ExplorerConstants.Player.Directions.Right;
						break;
					case ExplorerConstants.Player.Actions.MoveDown:
						if(!strafing)
							facingDirection = ExplorerConstants.Player.Directions.Down;
						newY++;
						TryMove(newX, newY, world);
						break;
					case ExplorerConstants.Player.Actions.MoveUp:
						if (!strafing)
							facingDirection = ExplorerConstants.Player.Directions.Up;
						newY--;
						TryMove(newX, newY, world);
						break;
					case ExplorerConstants.Player.Actions.MoveLeft:
						if (!strafing)
							facingDirection = ExplorerConstants.Player.Directions.Left;
						newX--;
						TryMove(newX, newY, world);
						break;
					case ExplorerConstants.Player.Actions.MoveRight:
						if (!strafing)
							facingDirection = ExplorerConstants.Player.Directions.Right;
						newX++;
						TryMove(newX, newY, world);
						break;
					case ExplorerConstants.Player.Actions.Strafe:
						strafing = !strafing;
						break;
					case ExplorerConstants.Player.Actions.UseEquippedItem:
						acre = world.ConvertToAcre(facingX, facingY);
						if(equipped == ExplorerConstants.Items.IDS.Fruit)
						{
							RestoreStamina(ExplorerConstants.Items.FruitStaminaRestore);
							UseEquippedItem();
						}
						else if (equipped == ExplorerConstants.Items.IDS.SuperFruit)
						{
							RestoreStamina(ExplorerConstants.Items.SuperFruitStaminaRestore);
							UseEquippedItem();
						}
						else if (equipped == ExplorerConstants.Items.IDS.MagicStone)
						{
							IncreaseMaxStamina(ExplorerConstants.Items.MagicStoneStaminaIncrease);
							UseEquippedItem();
							errorMessages.Add("-You used a " + ExplorerConstants.Items.AllBlueprints[equipped].DisplayName + " and increased your max stamina!");
						}
						else if (world.CanPutObject(equipped, facingX, facingY))
						{
							string claimProblems = world.CanClaim(acre.Item1, acre.Item2, playerID);
							ExplorerConstants.Items.IDS actualObject = equipped;

							if (ExplorerConstants.Items.Transformations.ContainsKey(equipped))
								actualObject = ExplorerConstants.Items.Transformations[equipped];

							ItemBlueprint objectInfo = ExplorerConstants.Items.AllBlueprints[actualObject];

							if (objectInfo.RequiresOwnedAcre &&
								world.GetOwner(acre.Item1, acre.Item2) != playerID)
							{
								errorMessages.Add("-You can't place the equipped item (" + objectInfo.DisplayName + ") in an acre you don't own!");
							}
							else if (objectInfo.CannotOwn && (world.GetOwner(acre.Item1, acre.Item2) > 0 || world.IsSpawn(acre.Item1, acre.Item2)))
							{
								errorMessages.Add("-You can't place the equipped item (" + objectInfo.DisplayName + ") in an owned or spawning acre!");
							}
							else if (actualObject == ExplorerConstants.Items.IDS.Tower &&
								!string.IsNullOrWhiteSpace(claimProblems))
							{
								errorMessages.Add("-You can't claim this acre! " + claimProblems);
							}
							else if (actualObject == world.GetObject(facingX, facingY, objectInfo.Layer))
							{
								errorMessages.Add("-You can't place the equipped item (" + objectInfo.DisplayName + "); it's already there!");
							}
							else if (world.InLockedArea(LockID, acre.Item1, acre.Item2))
							{
								errorMessages.Add("-You can't place items in a locked area!");
							}
							else if (world.GetLocked(acre.Item1, acre.Item2) && actualObject == ExplorerConstants.Items.IDS.AreaLocker)
							{
								errorMessages.Add("-You can't lock this acre! It's already locked!");
							}
							else if (world.PutObject(actualObject, facingX, facingY))
							{
								if (actualObject == ExplorerConstants.Items.IDS.Tower)
								{
									score += ExplorerConstants.Player.TowerScore;
									errorMessages.Add("-You claimed acre " + acre.Item1 + "-" + acre.Item2 + "!");
									world.SetOwner(playerID, acre.Item1, acre.Item2);
								}
								else if (actualObject == ExplorerConstants.Items.IDS.AreaLocker)
								{
									world.SetLocked(acre.Item1, acre.Item2, true);
									errorMessages.Add("-You locked acre " + acre.Item1 + "-" + acre.Item2 + " and the surrounding acres");
								}
								else if (actualObject == ExplorerConstants.Items.IDS.Statue)
								{
									score += ExplorerConstants.Player.StatueScore;
								}
								UseEquippedItem();
							}
						}
						else
						{
							if (equipped == ExplorerConstants.Items.IDS.Empty)
								errorMessages.Add("-You don't have anything equipped");
							else
								errorMessages.Add("-You can't use the equipped item: " + ExplorerConstants.Items.AllBlueprints[equipped].DisplayName);
						}
							
						break;
				}

				if (stamina < MaxStamina / 2 && GetOption(PlayerOptions.AutoFruit))
				{
					if (ItemAmount(ExplorerConstants.Items.IDS.Fruit) > 0)
					{
						if (!RemoveItem(ExplorerConstants.Items.IDS.Fruit, 1))
							errorMessages.Add("-Fatal error: Inconsistent item counts. Please report this bug!");
						else
							RestoreStamina(ExplorerConstants.Items.FruitStaminaRestore);
					}
					else if (ItemAmount(ExplorerConstants.Items.IDS.SuperFruit) > 0)
					{
						if (!RemoveItem(ExplorerConstants.Items.IDS.SuperFruit, 1))
							errorMessages.Add("-Fatal error: Inconsistent item counts. Please report this bug!");
						else
							RestoreStamina(ExplorerConstants.Items.SuperFruitStaminaRestore);
					}
				}

				if (EquippedSteps >= ExplorerConstants.Items.TorchSteps &&
					equipped == ExplorerConstants.Items.IDS.Torch)
				{
					UseEquippedItem();
				}

				if (equipped != ExplorerConstants.Items.IDS.Empty && inventory[equipped] == 0)
				{
					errorMessages.Add("-You ran out of your equipped item (" + ExplorerConstants.Items.AllBlueprints[equipped].DisplayName + ")");
					equipped = ExplorerConstants.Items.IDS.Empty;
				}

				if (stamina == 0)
				{
					errorMessages.Add("-You're out of stamina! Don't forget: you can refill it with Fruit or use Chat Coins.");
					break;
				}
			}

			if ((oblockX != BlockX || oblockY != BlockY) && !world.PlayerCanHalt(BlockX, BlockY))
			{
				errorMessages.Add("-You halted in a block which cannot be occupied, so you have been moved to a nearby location");
				Tuple<int, int> newLocation = world.FindCloseHaltable(BlockX, BlockY);

				if (!TryMove(newLocation.Item1, newLocation.Item2, world, true))
					errorMessages.Add("-Fatal error! You could not be moved from the previous bad location!");
			}
			else if (world.GetObject(BlockX, BlockY, ExplorerConstants.Items.AllBlueprints[ExplorerConstants.Items.IDS.CaveEntrance].Layer) == ExplorerConstants.Items.IDS.CaveEntrance)
			{
				//Oh crap, we landed on a cave entrance! Try to enter the cave.
				if (world.HasCave(BlockX, BlockY))
				{
					//Get rid of your dang player token
					//world.ForceRemoval(ExplorerConstants.Map.Layers.PlayerLayer, BlockX, BlockY);

					//Get the cave and stick you right in there
					World cave = world.GetCave(BlockX, BlockY);
					inCaveLocation = Tuple.Create(BlockX, BlockY);
					AdvanceWorldDepth(cave.SpawnAcreX, cave.SpawnAcreY);
					
					//Put a block over the cave so people can't get in
					world.ForcePut(ExplorerConstants.Items.IDS.BlockingBlock, inCaveLocation.Item1, inCaveLocation.Item2);

					errorMessages.Add("-You entered a cave! Equip a torch!");
				}
				else
				{
					errorMessages.Add("-You tried to enter the cave, but the cave collapsed!");
					world.ForceRemoval(ExplorerConstants.Items.AllBlueprints[ExplorerConstants.Items.IDS.CaveEntrance].Layer, BlockX, BlockY);
				}
			}
			else if (world.GetObject(BlockX, BlockY, ExplorerConstants.Items.AllBlueprints[ExplorerConstants.Items.IDS.CaveExit].Layer) == ExplorerConstants.Items.IDS.CaveExit)
			{
				errorMessages.Add("-As you leave the cave, the entrance collapses behind you");
				LeaveCave(baseWorld);
				world = baseWorld;
			}

			if (beforeFruit > 0 && (ItemAmount(ExplorerConstants.Items.IDS.Fruit) + ItemAmount(ExplorerConstants.Items.IDS.SuperFruit)) == 0)
				errorMessages.Add("-You've run out of fruit");

			//Now put your player token back
			if(!InCave)
				world.ForcePut(ExplorerConstants.Items.IDS.PlayerToken, BlockX, BlockY);

			return string.Join("\n", errorMessages.Distinct());
		}
		public bool CraftItem(ExplorerConstants.Items.IDS item)
		{
			//Oops, not a craftable item
			if (!ExplorerConstants.Items.CraftingRecipes.ContainsKey(item))
				return false;

			//First, a quick check for amounts
			if (ExplorerConstants.Items.CraftingRecipes[item].Any(x => ItemAmount(x.Key) < x.Value))
				return false;

			//Now get rid of all the items
			foreach (var craftItem in ExplorerConstants.Items.CraftingRecipes[item])
				RemoveItem(craftItem.Key, craftItem.Value);

			//Get the new item
			GetItem(item, 1);

			return true;
		}
		public bool EquipItem(ExplorerConstants.Items.IDS item)
		{
			if (ItemAmount(item) == 0)
				return false;
			else if (item == equipped)
				return true;	//Just do nothing if it's the same

			//if (inventory.ContainsKey(equipped) && inventory[equipped] > 0 && equipped == ExplorerConstants.Items.IDS.Torch && equippedSteps > 0)
			//	inventory[equipped]--;

			equipped = item;
			//equippedSteps = 0;
			return true;
		}
		public bool Respawn(World world)
		{
			//Get the hell outta dodge
			int retreatCount = 0;
			while (RetreatWorldDepth())
				retreatCount++;

			Tuple<int, int> spawn = world.GetASpawn();

			//You only need to move the player token if they were still in the world when they respawned
			if (retreatCount == 0 && !world.MovePlayerToken(BlockX, BlockY, spawn.Item1, spawn.Item2))
				return false;

			return TryMove(spawn.Item1, spawn.Item2, world, true);
		}
		public bool Teleport(Tuple<int, int> location, World world)
		{
			if (!world.MovePlayerToken(BlockX, BlockY, location.Item1, location.Item2))
				return false;

			return TryMove(location.Item1, location.Item2, world, true);
		}
		public void CheatResources(int amount)
		{
			GetItem(ExplorerConstants.Items.IDS.Wood, amount);
			GetItem(ExplorerConstants.Items.IDS.StarCrystal, amount);
			GetItem(ExplorerConstants.Items.IDS.Stone, amount);
			GetItem(ExplorerConstants.Items.IDS.Coal, amount);
			GetItem(ExplorerConstants.Items.IDS.Iron, amount);
			GetItem(ExplorerConstants.Items.IDS.Fruit, amount);
			GetItem(ExplorerConstants.Items.IDS.Seed, amount);
			GetItem(ExplorerConstants.Items.IDS.Sapling, amount);
			GetItem(ExplorerConstants.Items.IDS.AreaLocker, amount);
		}
		public int ItemAmount(ExplorerConstants.Items.IDS item)
		{
			if (!inventory.ContainsKey(item))
				return 0;

			return inventory[item];
		}

		//The amount is taken care of automatically
		private void GetItem(ExplorerConstants.Items.IDS ID, int forceAmount = 0)
		{
			ItemBlueprint itemInfo = ExplorerConstants.Items.AllBlueprints[ID];
			int amount = itemInfo.MinYield + random.Next(1 + itemInfo.MaxYield - itemInfo.MinYield);

			//Even if I accidentally call this function for unobtainable items, it
			//STILL won't give you the item
			if (!itemInfo.CanObtain)
				return;

			if(!materialsCollected.ContainsKey(ID))
				materialsCollected.Add(ID, 0);
			if(!inventory.ContainsKey(ID))
				inventory.Add(ID, 0);

			if (forceAmount > 0)
				amount = forceAmount;

			materialsCollected[ID] += amount;
			inventory[ID] += amount;

			//Get extra items if necessary. This also chains?
			if(ExplorerConstants.Items.GetExtra.ContainsKey(ID))
			{
				var info = ExplorerConstants.Items.GetExtra[ID];
				GetItem(info.Item1, info.Item2 + random.Next(1 + info.Item3 - info.Item2));
			}
		}
		private bool TryMove(int newX, int newY, World world, bool teleport = false)
		{
			if (!world.ValidBlock(newX, newY))
				return false;

			Tuple<int, int> acres = world.ConvertToAcre(newX, newY);
			ExplorerConstants.Items.IDS newBlockItem = world.GetTopObject(newX, newY);
			ItemBlueprint newBlockInfo = ExplorerConstants.Items.AllBlueprints[newBlockItem];

			//First, go ahead and just get the item if we have autopickup turned on
			if (newBlockInfo.CanAutoPickup && GetOption(PlayerOptions.Autopickup) && !world.InLockedArea(LockID, acres.Item1, acres.Item2) &&
				world.PickupObject(newX, newY))
			{
				GetItem(newBlockItem);
			}

			//If we still can't occupy the space, we need to stop
			if (!world.PlayerCanPass(newX, newY) && !(newBlockItem == ExplorerConstants.Items.IDS.Gate &&
				world.GetOwner(acres.Item1, acres.Item2) == playerID))
				return false;
			if (!teleport && stamina < newBlockInfo.StaminaRequired)
				return false;

			BlockX = newX;
			BlockY = newY;

			//Assume we moved one space
			if (!teleport)
			{
				stepsTaken++;
				EquippedSteps++;
				stamina -= newBlockInfo.StaminaRequired;
			}

			if(world.SetExplorer(playerID, acres.Item1, acres.Item2))
				score+=ExplorerConstants.Player.ExploreScore;

			return true;
		}
		private void LeaveCave(World world)
		{
			world.RemoveCave(inCaveLocation.Item1, inCaveLocation.Item2);
			world.ForceRemoval(ExplorerConstants.Map.Layers.ObjectLayer, inCaveLocation.Item1, inCaveLocation.Item2);
			RetreatWorldDepth();
			inCaveLocation = Tuple.Create(-1, -1);
		}
		public List<Tuple<ExplorerConstants.Items.IDS, int>> GetSortedItems()
		{
			return inventory.Select(x => Tuple.Create(x.Key, x.Value)).OrderByDescending(x => x.Item2).ToList();
		}

		public int Score
		{
			get { return score; }
		}
		public int PlayerID
		{
			get { return playerID; }
		}
		public int Stamina
		{
			get { return stamina; }
		}
		public int MaxStamina
		{
			get { return maxStamina; }
		}
		public int BlockX
		{
			get { return blockXList[blockListDepth]; }
			private set { blockXList[blockListDepth] = value; }
		}
		public int BlockY
		{
			get { return blockYList[blockListDepth]; }
			private set { blockYList[blockListDepth] = value; }
		}
		public int WorldDepth
		{
			get { return blockListDepth; }
		}
		public int AcreX
		{
			get { return BlockX / ExplorerConstants.Map.AcreWidth; }
		}
		public int AcreY
		{
			get { return BlockY / ExplorerConstants.Map.AcreHeight; }
		}
		public int EquippedUses
		{
			get
			{
				switch (equipped)
				{
					case ExplorerConstants.Items.IDS.Torch:
						return ExplorerConstants.Items.TorchSteps - EquippedSteps;
					default:
						return -1;
				}
			}
		}
		private int EquippedSteps
		{
			get
			{
				if (!allEquippedSteps.ContainsKey(equipped))
					allEquippedSteps.Add(equipped, 0);

				return allEquippedSteps[equipped];
			}
			set
			{
				if (!allEquippedSteps.ContainsKey(equipped))
					allEquippedSteps.Add(equipped, 0);

				allEquippedSteps[equipped] = value;
			}
		}
		public int LockID
		{
			get
			{
				if (GetOption(PlayerOptions.FullLock))
					return 0;
				else
					return PlayerID;
			}
		}
		public ExplorerConstants.Player.Directions FacingDirection
		{
			get { return facingDirection; }
		}
		public ExplorerConstants.Items.IDS EquippedItem
		{
			get { return equipped; }
		}
		public Tuple<int, int> InCaveLocation
		{
			get { return inCaveLocation; }
		}
		public bool InCave
		{
			get { return inCaveLocation.Item1 > 0 && inCaveLocation.Item2 > 0; }
		}
	}

	//This is the actual class that the module will see. All actions should be performed on this module
	[Serializable()]
	public class WorldInstance
	{
		public static int nextWorldID = 0;
		private World world = new World();
		private Dictionary<int, Player> players = new Dictionary<int, Player>();
		private int nextPlayerID = 2;
		private int worldID = -1;

		public bool GenerateWorld()
		{
			if (world.Generated)
				return false;

			worldID = nextWorldID;
			world.Generate(nextWorldID % ExplorerConstants.Generation.PresetBases.Count);
			nextWorldID++;

			return true;
		}

		//The world can no longer be accessed, but stats will be kept. Irreversibly erases world!
		public void CloseWorld()
		{
			world = null;
		}

		//Place yourself in the world! Returns false if you can't due to no space or the fact that
		//you're already playing
      public bool StartGame(int player, string username)
		{
			//Can't place a player in a world that doesn't exist!
			//Also if you're already playing, you can't start
			if (!CanPlay || players.ContainsKey(player))
				return false;

			//Couldn't get a spawn
			Tuple<int, int> spawn = world.GetASpawn();
			if (spawn.Item1 < 0 || spawn.Item2 < 0)
				return false;

			players.Add(player, new Player(nextPlayerID, spawn.Item1, spawn.Item2, username));
			world.SetExplorer(nextPlayerID, spawn.Item1 / ExplorerConstants.Map.AcreWidth, spawn.Item2 / ExplorerConstants.Map.AcreHeight);
			world.ForcePut(ExplorerConstants.Items.IDS.PlayerToken, spawn.Item1, spawn.Item2);

			nextPlayerID++;

			return true;
		}
		public bool Playing(int player)
		{
			return players.ContainsKey(player);
		}
		public string PerformActions(int player, string actionList, out int chatCoins)
		{
			chatCoins = 0;

			if (!string.IsNullOrWhiteSpace(CheckPlayer(player)))
				return CheckPlayer(player);

			List<ExplorerConstants.Player.Actions> actions = new List<ExplorerConstants.Player.Actions>();
			actionList = actionList.ToLower();

			bool didReplace = false;

			do
			{
				didReplace = false;
				Regex regex = new Regex(@"([^0-9\(\)])([0-9]+)");
				foreach (Match match in regex.Matches(actionList))
				{
					int repeat;

					if (!int.TryParse(match.Groups[2].Value, out repeat) || repeat > 100)
						return "There was something wrong with your action repeat count";

					didReplace = true;
					actionList = actionList.Replace(match.Value, new string(match.Groups[1].Value.ElementAt(0), repeat));
				}

				regex = new Regex(@"\(([^\)\(]*)\)([0-9]+)");
				foreach (Match match in regex.Matches(actionList))
				{
					int repeat;

					if (!int.TryParse(match.Groups[2].Value, out repeat) || repeat > 100)
						return "There was something wrong with your action repeat count";

					didReplace = true;
					actionList = actionList.Replace(match.Value, String.Concat(Enumerable.Repeat(match.Groups[1], repeat)));
				}

				if (actionList.Length > 10000)
				{
					return "This is too many actions to perform!";
				}
			} while (didReplace);

			foreach (char c in actionList)
			{
				if (string.IsNullOrWhiteSpace(c.ToString()))
					continue;

				if(!ExplorerConstants.Player.ActionMapping.ContainsKey(c))
					return c + " is an invalid action!";

				actions.Add(ExplorerConstants.Player.ActionMapping[c]);
			}

			//Check for cave status; if so, well damn son use the cave as the world!
			World playWorld = world;
			Tuple<int, int> caveLocation = players[player].InCaveLocation;
			if (world.HasCave(caveLocation.Item1, caveLocation.Item2))
				playWorld = world.GetCave(caveLocation.Item1, caveLocation.Item2);

			string errors = players[player].PerformActions(actions, playWorld, world, out chatCoins);

			//Gotta do it AGAIN just in case we enter the cave 
			playWorld = world;
			caveLocation = players[player].InCaveLocation;
			if (world.HasCave(caveLocation.Item1, caveLocation.Item2))
				playWorld = world.GetCave(caveLocation.Item1, caveLocation.Item2);

			int blockX = players[player].BlockX;
			int blockY = players[player].BlockY;
			Tuple<int, int> acres = playWorld.ConvertToAcre(blockX, blockY);

			string[] acreText = playWorld.GetAcreText(blockX, blockY, players[player].EquippedItem == ExplorerConstants.Items.IDS.Torch);
			string output = "Acre: " + acres.Item1 + "-" + acres.Item2 + (playWorld.InLockedArea(players[player].LockID, acres.Item1, acres.Item2) ? " *" : "") + " [" + world.WorldTime.Hours.ToString().PadLeft(2, '0') + ":" + world.WorldTime.Minutes.ToString().PadLeft(2, '0') + "]";
			string spacer = (players[player].GetOption(Player.PlayerOptions.SquareMap) ? " " : "");
			List<Tuple<ExplorerConstants.Items.IDS, int>> inventory = players[player].GetSortedItems().Where(x => x.Item2 > 0).ToList();

			if (playWorld.GetOwner(acres.Item1, acres.Item2) > 0)
				output += " (" + GetPlayerName(playWorld.GetOwner(acres.Item1, acres.Item2)) + ")";
			else if(playWorld.IsSpawn(acres.Item1, acres.Item2) && !playWorld.IsCave)
				output += " (Spawn)";

			var edges = playWorld.GetAcreEdgeBlocks(acres.Item1, acres.Item2);

			output += "\n+" + spacer;
			foreach (bool isEdgeSafe in edges.Where(x => x.Key.Item1 == ExplorerConstants.Player.Directions.Up).OrderBy(x => x.Key.Item2).Select(x => x.Value))
				output += (isEdgeSafe ? "-" : "~") + spacer;
			output += "+\n";

			for (int i = 0; i < acreText.Length; i++)
			{
				output += (edges[Tuple.Create(ExplorerConstants.Player.Directions.Left, i)] ? "|" : "}") + spacer +
					Regex.Replace(acreText[i], "(.)", @"$1" + spacer) + (edges[Tuple.Create(ExplorerConstants.Player.Directions.Right, i)] ? "|" : "{");

				if(i < inventory.Count)
					output += (inventory[i].Item1 == players[player].EquippedItem ? "*" : " ") + ExplorerConstants.Items.AllBlueprints[inventory[i].Item1].ShorthandName + ": " + inventory[i].Item2;

				output += "\n";
			}

			output += "+" + spacer;
			foreach (bool isEdgeSafe in edges.Where(x => x.Key.Item1 == ExplorerConstants.Player.Directions.Down).OrderBy(x => x.Key.Item2).Select(x => x.Value))
				output += (isEdgeSafe ? "-" : "~") + spacer;
			output += "+\n";

			output += "Stamina: " + players[player].Stamina + "/" + players[player].MaxStamina + " Facing: " + players[player].FacingDirection;
			if (players[player].EquippedUses >= 0)
				output += "\nEquipment uses: " + players[player].EquippedUses;

			return (!string.IsNullOrWhiteSpace(errors) ? errors + "\n\n" : "") + output;
		}
		public void Cheat(int player)
		{
			if (CanPlay && Playing(player))
			{
				players[player].CheatResources(999);
			}
		}
		public string CraftItem(int player, string item, int amount)
		{
			if (!ExplorerConstants.Items.AllBlueprints.Any(x => x.Value.ShorthandName == item))
				return "I couldn't find an item with this name";

			if (!string.IsNullOrWhiteSpace(CheckPlayer(player)))
				return CheckPlayer(player);

			ExplorerConstants.Items.IDS itemID = ExplorerConstants.Items.AllBlueprints.FirstOrDefault(x => x.Value.ShorthandName == item).Key;

			if(!ExplorerConstants.Items.CraftingRecipes.ContainsKey(itemID))
				return "This is not a craftable item";

			if (ExplorerConstants.Items.CraftingProximityRequirements.ContainsKey(itemID) &&
				!world.IsClose(ExplorerConstants.Items.CraftingProximityRequirements[itemID], players[player].BlockX, players[player].BlockY))
				return "You need to be close to " + ExplorerConstants.Items.AllBlueprints[ExplorerConstants.Items.CraftingProximityRequirements[itemID]].DisplayName +
					" to craft " + ExplorerConstants.Items.AllBlueprints[itemID].DisplayName;

			int craftAmount = 0;
			for (int i = 0; i < amount; i++)
			{
				if(players[player].CraftItem(itemID))
					craftAmount++;
			}

			if(craftAmount == 0)
				return "You couldn't craft the item: " + ExplorerConstants.Items.AllBlueprints[itemID].DisplayName;

			return "Crafted " + craftAmount + " of the item: " + ExplorerConstants.Items.AllBlueprints[itemID].DisplayName;
		}
		public string EquipItem(int player, string item)
		{
			if (!ExplorerConstants.Items.AllBlueprints.Any(x => x.Value.ShorthandName == item))
				return "I couldn't find an item with this name";

			if (!string.IsNullOrWhiteSpace(CheckPlayer(player)))
				return CheckPlayer(player);

			ExplorerConstants.Items.IDS itemID = ExplorerConstants.Items.AllBlueprints.FirstOrDefault(x => x.Value.ShorthandName == item).Key;

			if (players[player].EquipItem(itemID))
				return "You equipped item: " + ExplorerConstants.Items.AllBlueprints[itemID].DisplayName;
			else
				return "You can't equip item: " + ExplorerConstants.Items.AllBlueprints[itemID].DisplayName;
		}
		public string Respawn(int player)
		{
			if(!string.IsNullOrWhiteSpace(CheckPlayer(player)))
				return CheckPlayer(player);

			if (!players[player].Respawn(world))
				return "Something terrible happened and you weren't able to respawn!";

			int whatever;
			return "You've been returned to the spawn" +
				"\n\n" + PerformActions(player, "", out whatever); ;
		}
		public string PlayerAcres(int player)
		{
			if (!string.IsNullOrWhiteSpace(CheckPlayer(player)))
				return CheckPlayer(player);

			List<Tuple<int, int, int>> ownedAcres = world.GetOwnedAcres().Where(x => x.Item3 == players[player].PlayerID).ToList();

			if (ownedAcres.Count == 0)
				return "You don't own any acres on World " + worldID;

			string output = "You own these acres on World " + worldID + ":\n";

			foreach (var ownedAcre in ownedAcres)
			{
				output += ownedAcre.Item1 + "-" + ownedAcre.Item2 + ", ";
			}

			return output.Remove(output.Length - 2);
		}
		public string TeleportToTower(int player, Tuple<int, int> towerAcre)
		{
			if (!string.IsNullOrWhiteSpace(CheckPlayer(player)))
				return CheckPlayer(player);

			List<Tuple<int, int, int>> ownedAcres = world.GetOwnedAcres().Where(x => x.Item3 == players[player].PlayerID).ToList();

			if (!ownedAcres.Any(x => x.Item1 == towerAcre.Item1 && x.Item2 == towerAcre.Item2))
				return "You can't teleport here because you don't own this acre!";
			else if (players[player].WorldDepth != 0)
				return "You can't teleport from here. Go back to the surface first.";

			Tuple<int, int> towerLocation = world.FindFirstItem(ExplorerConstants.Items.IDS.Tower, towerAcre.Item1, towerAcre.Item2);
			Tuple<int, int> teleportLocation = world.FindCloseHaltable(towerLocation.Item1, towerLocation.Item2);

			if (!players[player].Teleport(teleportLocation, world))
				return "The teleportation failed! This shouldn't happen!";

			int whatever;
			return "You've been teleported close to your tower in Acre " + towerAcre.Item1 + "-" + towerAcre.Item2 + 
				"\n\n" + PerformActions(player, "", out whatever);
		}
		public string PlayerItems(int player)
		{
			if (!string.IsNullOrWhiteSpace(CheckPlayer(player)))
				return CheckPlayer(player);

			List<Tuple<ExplorerConstants.Items.IDS, int>> inventory = players[player].GetSortedItems().Where(x => x.Item2 > 0).OrderBy(x => ExplorerConstants.Items.AllBlueprints[x.Item1].ShorthandName).ToList();
			string output = "Your World " + worldID + " inventory: ";

			for (int i = 0; i < inventory.Count; i++)
			{
				output += "\n" + (inventory[i].Item1 == players[player].EquippedItem ? "*" : "-") + ExplorerConstants.Items.AllBlueprints[inventory[i].Item1].ShorthandName + ": " + inventory[i].Item2;
			}

			return output;
		}
		public int RestoreStamina(int player, int amount, bool amountIsPercent = false)
		{
			if (!string.IsNullOrWhiteSpace(CheckPlayer(player)))
				return -1;

			if (amountIsPercent)
			{
				amount = (int)(amount / 100.0 * players[player].MaxStamina);
			}

			return players[player].RestoreStamina(amount);
		}

		public string CheckPlayer(int player)
		{
			if (!CanPlay)
				return "This world is unplayable! You cannot use this command";
			else if (!Playing(player))
				return "For some reason you're not a player in World " + worldID;

			return "";
		}

		public void RefreshStamina(int amount)
		{
			foreach (Player player in players.Values.ToList())
				player.RestoreStamina(amount);
		}
		public void SimulateTime()
		{
         if(CanPlay)
			   world.SimulateTime();
		}

		public string ToggleOption(int player, string option)
		{
			List<Tuple<Player.PlayerOptions, string>> options = Enum.GetValues(typeof(Player.PlayerOptions)).Cast<Player.PlayerOptions>().Select(x => Tuple.Create(x, x.ToString().ToLower())).ToList();

			if (!options.Any(x => x.Item2 == option.ToLower()))
				return "This was not a valid option";

			if (players[player].ToggleOptions(options.FirstOrDefault(x => x.Item2 == option.ToLower()).Item1))
				return "You've enabled the " + option + " option for world " + worldID;
			else
				return "You've disabled the " + option + " option for world " + worldID;
		}
		public int GetScore(int player)
		{
			if (!players.ContainsKey(player))
				return 0;

			return players[player].Score;
		}
		public int GetPlayer(int id)
		{
			foreach (var player in players)
			{
				if (player.Value.PlayerID == id)
					return player.Key;
			}

			return -1;
		}
      public string GetPlayerName(int id)
      {
         foreach (var player in players)
         {
            if (player.Value.PlayerID == id)
               return player.Value.Username;
         }

         return "";
      }
		public List<Tuple<int, int, int>> GetOwnedAcres()
		{
			return world.GetOwnedAcres().Select(x => Tuple.Create(x.Item1, x.Item2, GetPlayer(x.Item3))).ToList();
		}

		public World WorldData
		{
			get { return world; }
		}
		public bool CanPlay
		{
			get { return Operating && world.Generated; }
		}
		public bool Operating
		{
			get { return world != null; }
		}
		public int WorldID
		{
			get { return worldID; }
		}
	}
}
