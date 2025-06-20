﻿using Barotrauma.Extensions;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.RuinGeneration;

namespace Barotrauma
{
    class LevelData
    {
        [Flags]
        public enum LevelType
        {
            LocationConnection = 1,
            Outpost = 2
        }

        public readonly LevelType Type;

        public readonly string Seed;

        public readonly float Difficulty;

        public readonly Biome Biome;

        public LevelGenerationParams GenerationParams { get; private set; }

        public bool HasBeaconStation;
        public bool IsBeaconActive;

        public bool HasHuntingGrounds, OriginallyHadHuntingGrounds;

        /// <summary>
        /// Minimum difficulty of the level before hunting grounds can appear.
        /// </summary>
        public const float HuntingGroundsDifficultyThreshold = 25;

        /// <summary>
        /// Probability of hunting grounds appearing in 100% difficulty levels.
        /// </summary>
        public const float MaxHuntingGroundsProbability = 0.3f;

        public OutpostGenerationParams ForceOutpostGenerationParams;

        public SubmarineInfo ForceBeaconStation;

        public SubmarineInfo ForceWreck;

        public RuinGenerationParams ForceRuinGenerationParams;

        public enum ThalamusSpawn
        {
            Random,
            Forced,
            Disabled
        }
        
        public static SubmarineInfo ConsoleForceWreck;
        public static SubmarineInfo ConsoleForceBeaconStation;
        public static ThalamusSpawn ForceThalamus = ThalamusSpawn.Random;

        public bool AllowInvalidOutpost;

        public readonly Point Size;

        /// <summary>
        /// The depth at which the level starts at, in in-game coordinates. E.g. if this was set to 100 000 (= 1000 m), the nav terminal would display the depth as 1000 meters at the top of the level.
        /// </summary>
        public readonly int InitialDepth;

        /// <summary>
        /// Determined during level generation based on the size of the submarine. Null if the level hasn't been generated.
        /// </summary>
        public int? MinMainPathWidth;

        /// <summary>
        /// Events that have previously triggered in this level. Used for making events the player hasn't seen yet more likely to trigger when re-entering the level. Has a maximum size of <see cref="EventManager.MaxEventHistory"/>.
        /// </summary>
        public readonly List<Identifier> EventHistory = new List<Identifier>();

        /// <summary>
        /// Events that have already triggered in this level and can never trigger again. <see cref="EventSet.OncePerLevel"/>.
        /// </summary>
        public readonly List<Identifier> NonRepeatableEvents = new List<Identifier>();

        public readonly Dictionary<EventSet, int> FinishedEvents = new Dictionary<EventSet, int>();

        /// <summary>
        /// For backwards compatibility (previously "exhausting" one event set exhausted all of them (now we use <see cref="exhaustedEventSets"/> instead).
        /// </summary>
        private bool allEventsExhausted;

        /// <summary>
        /// 'Exhaustible' sets won't appear in the same level until after one world step (~10 min, see Map.ProgressWorld) has passed. <see cref="EventSet.Exhaustible"/>.
        /// </summary>
        private HashSet<Identifier> exhaustedEventSets = new HashSet<Identifier>();

        /// <summary>
        /// The crush depth of a non-upgraded submarine in in-game coordinates. Note that this can be above the top of the level!
        /// </summary>
        public float CrushDepth
        {
            get
            {
                return Math.Max(Size.Y, Level.DefaultRealWorldCrushDepth / Physics.DisplayToRealWorldRatio) - InitialDepth;
            }
        }

        /// <summary>
        /// The crush depth of a non-upgraded submarine in "real world units" (meters from the surface of Europa). Note that this can be above the top of the level!
        /// </summary>
        public float RealWorldCrushDepth
        {
            get
            {
                return Math.Max(Size.Y * Physics.DisplayToRealWorldRatio, Level.DefaultRealWorldCrushDepth);
            }
        }
        
        /// <summary>
        /// Inclusive (matching the min an max values is accepted).
        /// </summary>
        public bool IsAllowedDifficulty(float minDifficulty, float maxDifficulty) => Difficulty >= minDifficulty && Difficulty <= maxDifficulty;

        public LevelData(string seed, float difficulty, float sizeFactor, LevelGenerationParams generationParams, Biome biome)
        {
            Seed = seed ?? throw new ArgumentException("Seed was null");
            Biome = biome ?? throw new ArgumentException("Biome was null");
            GenerationParams = generationParams ?? throw new ArgumentException("Level generation parameters were null");
            Type = GenerationParams.Type;
            Difficulty = difficulty;

            sizeFactor = MathHelper.Clamp(sizeFactor, 0.0f, 1.0f);
            int width = (int)MathHelper.Lerp(generationParams.MinWidth, generationParams.MaxWidth, sizeFactor);

            InitialDepth = (int)MathHelper.Lerp(generationParams.InitialDepthMin, generationParams.InitialDepthMax, sizeFactor);

            Size = new Point(
                (int)MathUtils.Round(width, Level.GridCellSize),
                (int)MathUtils.Round(generationParams.Height, Level.GridCellSize));
        }

        public LevelData(XElement element, float? forceDifficulty = null, bool clampDifficultyToBiome = false)
        {
            Seed = element.GetAttributeString("seed", "");
            Size = element.GetAttributePoint("size", new Point(1000));
            Enum.TryParse(element.GetAttributeString("type", "LocationConnection"), out Type);

            HasBeaconStation = element.GetAttributeBool("hasbeaconstation", false);
            IsBeaconActive = element.GetAttributeBool("isbeaconactive", false);

            HasHuntingGrounds = element.GetAttributeBool("hashuntinggrounds", false);
            OriginallyHadHuntingGrounds = element.GetAttributeBool("originallyhadhuntinggrounds", HasHuntingGrounds);

            string generationParamsId = element.GetAttributeString("generationparams", "");
            GenerationParams = LevelGenerationParams.LevelParams.Find(l => l.Identifier == generationParamsId || (!l.OldIdentifier.IsEmpty && l.OldIdentifier == generationParamsId));
            if (GenerationParams == null)
            {
                DebugConsole.ThrowError($"Error while loading a level. Could not find level generation params with the ID \"{generationParamsId}\".");
                GenerationParams = LevelGenerationParams.LevelParams.FirstOrDefault(l => l.Type == Type);
                GenerationParams ??= LevelGenerationParams.LevelParams.First();
            }

            InitialDepth = element.GetAttributeInt("initialdepth", GenerationParams.InitialDepthMin);

            string biomeIdentifier = element.GetAttributeString("biome", "");
            Biome = Biome.Prefabs.FirstOrDefault(b => b.Identifier == biomeIdentifier || (!b.OldIdentifier.IsEmpty && b.OldIdentifier == biomeIdentifier));
            if (Biome == null)
            {
                DebugConsole.ThrowError($"Error in level data: could not find the biome \"{biomeIdentifier}\".");
                Biome = Biome.Prefabs.First();
            }

            Difficulty = forceDifficulty ?? element.GetAttributeFloat("difficulty", 0.0f);
            if (clampDifficultyToBiome)
            {
                Difficulty = MathHelper.Clamp(Difficulty, Biome.MinDifficulty, Biome.AdjustedMaxDifficulty);
            }

            string[] prefabNames = element.GetAttributeStringArray("eventhistory", Array.Empty<string>());
            EventHistory.AddRange(EventPrefab.Prefabs.Where(p => prefabNames.Any(n => p.Identifier == n)).Select(p => p.Identifier));

            string[] nonRepeatablePrefabNames = element.GetAttributeStringArray("nonrepeatableevents", Array.Empty<string>());
            NonRepeatableEvents.AddRange(EventPrefab.Prefabs.Where(p => nonRepeatablePrefabNames.Any(n => p.Identifier == n)).Select(p => p.Identifier));

            string finishedEventsName = nameof(FinishedEvents);
            if (element.GetChildElement(finishedEventsName) is { } finishedEventsElement)
            {
                foreach (var childElement in finishedEventsElement.GetChildElements(finishedEventsName))
                {
                    Identifier eventSetIdentifier = childElement.GetAttributeIdentifier("set", Identifier.Empty);
                    if (eventSetIdentifier.IsEmpty) { continue; }
                    if (!EventSet.Prefabs.TryGet(eventSetIdentifier, out EventSet eventSet))
                    {
                        foreach (var prefab in EventSet.Prefabs)
                        {
                            if (FindSetRecursive(prefab, eventSetIdentifier) is { } foundSet)
                            {
                                eventSet = foundSet;
                                break;
                            }
                        }
                    }
                    if (eventSet is null) { continue; }
                    int count = childElement.GetAttributeInt("count", 0);
                    if (count < 1) { continue; }
                    FinishedEvents.TryAdd(eventSet, count);
                }

                static EventSet FindSetRecursive(EventSet parentSet, Identifier setIdentifier)
                {
                    foreach (var childSet in parentSet.ChildSets)
                    {
                        if (childSet.Identifier == setIdentifier)
                        {
                            return childSet;
                        }
                        if (FindSetRecursive(childSet, setIdentifier) is { } foundSet)
                        {
                            return foundSet;
                        }
                    }
                    return null;
                }
            }

            exhaustedEventSets = element.GetAttributeIdentifierArray(nameof(exhaustedEventSets), Array.Empty<Identifier>()).ToHashSet();
            //backwards compatibility: previously we didn't track which individual event sets have been exhausted
            allEventsExhausted = element.GetAttributeBool("EventsExhausted", false);
        }

        /// <summary>
        /// Instantiates level data using the properties of the connection (seed, size, difficulty)
        /// </summary>
        public LevelData(LocationConnection locationConnection)
        {
            Seed = locationConnection.Locations[0].LevelData.Seed + locationConnection.Locations[1].LevelData.Seed;
            bool connectionIsBiomeTransition = locationConnection.Locations[0].Biome.Identifier != locationConnection.Locations[1].Biome.Identifier;
            Biome = locationConnection.Biome;
            Type = LevelType.LocationConnection;
            Difficulty = locationConnection.Difficulty;
            GenerationParams = LevelGenerationParams.GetRandom(Seed, LevelType.LocationConnection, Difficulty, Biome.Identifier, biomeTransition: connectionIsBiomeTransition);

            float sizeFactor = MathUtils.InverseLerp(
                MapGenerationParams.Instance.SmallLevelConnectionLength,
                MapGenerationParams.Instance.LargeLevelConnectionLength,
                locationConnection.Length);
            int width = (int)MathHelper.Lerp(GenerationParams.MinWidth, GenerationParams.MaxWidth, sizeFactor);
            Size = new Point(
                (int)MathUtils.Round(width, Level.GridCellSize),
                (int)MathUtils.Round(GenerationParams.Height, Level.GridCellSize));

            var rand = new MTRandom(ToolBox.StringToInt(Seed));
            InitialDepth = (int)MathHelper.Lerp(GenerationParams.InitialDepthMin, GenerationParams.InitialDepthMax, (float)rand.NextDouble());
            if (Biome.IsEndBiome)
            {
                HasHuntingGrounds = false;
                HasBeaconStation = false;
            }
            else
            {
                HasHuntingGrounds = OriginallyHadHuntingGrounds = rand.NextDouble() < MathUtils.InverseLerp(HuntingGroundsDifficultyThreshold, 100.0f, Difficulty) * MaxHuntingGroundsProbability;
                HasBeaconStation = !HasHuntingGrounds && rand.NextDouble() < locationConnection.Locations.Select(l => l.Type.BeaconStationChance).Max();
            }            
            IsBeaconActive = false;
        }

        /// <summary>
        /// Instantiates level data using the properties of the location
        /// </summary>
        public LevelData(Location location, Map map, float difficulty)
        {
            Seed = location.NameIdentifier.Value + map.Locations.IndexOf(location);
            Biome = location.Biome;
            Type = LevelType.Outpost;
            Difficulty = difficulty;
            GenerationParams = LevelGenerationParams.GetRandom(Seed, LevelType.Outpost, Difficulty, Biome.Identifier);

            var rand = new MTRandom(ToolBox.StringToInt(Seed));
            int width = (int)MathHelper.Lerp(GenerationParams.MinWidth, GenerationParams.MaxWidth, (float)rand.NextDouble());
            InitialDepth = (int)MathHelper.Lerp(GenerationParams.InitialDepthMin, GenerationParams.InitialDepthMax, (float)rand.NextDouble());
            Size = new Point(
                (int)MathUtils.Round(width, Level.GridCellSize),
                (int)MathUtils.Round(GenerationParams.Height, Level.GridCellSize));
        }

        public static LevelData CreateRandom(string seed = "", float? difficulty = null, LevelGenerationParams generationParams = null, Identifier biomeId = default, bool requireOutpost = false, bool pvpOnly = false)
        {
            if (string.IsNullOrEmpty(seed))
            {
                seed = Rand.Range(0, int.MaxValue, Rand.RandSync.ServerAndClient).ToString();
            }

            Rand.SetSyncedSeed(ToolBox.StringToInt(seed));

            LevelType type = generationParams?.Type ??
                             (requireOutpost
                                      ? LevelType.Outpost
                                      : LevelType.LocationConnection);

            float selectedDifficulty = difficulty ?? Rand.Range(30.0f, 80.0f, Rand.RandSync.ServerAndClient);

            Biome biome = null;
            if (!biomeId.IsEmpty && biomeId != "Random")
            {
                Biome.Prefabs.TryGet(biomeId, out biome);
            }
            generationParams ??= LevelGenerationParams.GetRandom(seed, type, selectedDifficulty, pvpOnly: pvpOnly, biomeId: biomeId);

            biome ??=
                Biome.Prefabs.FirstOrDefault(b => generationParams?.AllowedBiomeIdentifiers.Contains(b.Identifier) ?? false) ??
                Biome.Prefabs.GetRandom(Rand.RandSync.ServerAndClient);

            var levelData = new LevelData(
                seed,
                selectedDifficulty,
                Rand.Range(0.0f, 1.0f, Rand.RandSync.ServerAndClient),
                generationParams,
                biome);
            if (type == LevelType.LocationConnection)
            {
                float beaconRng = Rand.Range(0.0f, 1.0f, Rand.RandSync.ServerAndClient);
                levelData.HasBeaconStation = beaconRng < 0.5f;
                levelData.IsBeaconActive = beaconRng > 0.25f;
            }
            if (GameMain.GameSession?.GameMode != null)
            {
                foreach (Mission mission in GameMain.GameSession.GameMode.Missions)
                {
                    mission.AdjustLevelData(levelData);
                }
            }
            return levelData;
        }

        /// <summary>
        /// Marks the event set as "exhausted". Exhausted sets won't appear in the same level until after one world step (~10 min, see Map.ProgressWorld) has passed. <see cref="EventSet.Exhaustible"/>.
        /// </summary>
        public void ExhaustEventSet(EventSet eventSet)
        {
            exhaustedEventSets.Add(eventSet.Identifier);
        }

        /// <summary>
        /// Has the event set been "exhausted"? Exhausted sets won't appear in the same level until after one world step (~10 min, see Map.ProgressWorld) has passed. <see cref="EventSet.Exhaustible"/>.
        /// </summary>
        public bool IsEventSetExhausted(EventSet eventSet)
        {
            if (allEventsExhausted) { return true; }
            return exhaustedEventSets.Contains(eventSet.Identifier);
        }

        /// <summary>
        /// Resets all "exhausted" event sets, allowing them to appear in the level again.
        /// </summary>
        public void ResetExhaustedEventSets()
        {
            allEventsExhausted = false;
            exhaustedEventSets.Clear();
        }

        public void ReassignGenerationParams(string seed)
        {
            GenerationParams = LevelGenerationParams.GetRandom(seed, Type, Difficulty, Biome.Identifier);
        }
        public bool OutpostGenerationParamsExist => ForceOutpostGenerationParams != null || OutpostGenerationParams.OutpostParams.Any();

        public static IEnumerable<OutpostGenerationParams> GetSuitableOutpostGenerationParams(Location location, LevelData levelData)
        {
            var paramsForGameMode = OutpostGenerationParams.OutpostParams.Where(p => 
                p.AllowedGameModeIdentifiers.None() || GameMain.GameSession?.GameMode is not GameMode gameMode || p.AllowedGameModeIdentifiers.Contains(gameMode.Preset.Identifier));

            var paramsWithMatchingLevelType = paramsForGameMode
                .Where(p => p.LevelType == null || levelData.Type == p.LevelType);

            //1. try finding params specifically for this location type
            var suitableParams = paramsWithMatchingLevelType
                    .Where(p => location == null || p.AllowedLocationTypes.Contains(location.Type.Identifier));
            if (!suitableParams.Any())
            {
                //2. not found, if the location type is configured to use the modules of some other location type,
                //   see if we could use that location type's generation params
                if (!location.Type.UseOutpostModulesOfLocationType.IsEmpty)
                {
                    suitableParams = paramsWithMatchingLevelType
                        .Where(p => p.AllowedLocationTypes.Contains(location.Type.UseOutpostModulesOfLocationType));
                }
                if (!suitableParams.Any())
                {
                    //3. still not found, choose some parameters that are suitable for any location type
                    suitableParams = paramsWithMatchingLevelType
                              .Where(p => location == null || !p.AllowedLocationTypes.Any());
                    if (!suitableParams.Any())
                    {
                        DebugConsole.ThrowError($"No suitable outpost generation parameters found for the location type \"{location.Type.Identifier}\". Selecting random parameters.");
                        suitableParams = paramsForGameMode;
                    }
                }

            }
            return suitableParams;
        }

        public void Save(XElement parentElement)
        {
            var newElement = new XElement("Level",
                    new XAttribute("seed", Seed),
                    new XAttribute("biome", Biome.Identifier),
                    new XAttribute("type", Type.ToString()),
                    new XAttribute("difficulty", Difficulty.ToString("G", CultureInfo.InvariantCulture)),
                    new XAttribute("size", XMLExtensions.PointToString(Size)),
                    new XAttribute("generationparams", GenerationParams.Identifier),
                    new XAttribute("initialdepth", InitialDepth),
                    new XAttribute("exhaustedeventsets", allEventsExhausted));

            newElement.Add(
                new XAttribute(nameof(exhaustedEventSets), string.Join(',', exhaustedEventSets.Select(e => e.Value))));

            if (HasBeaconStation)
            {
                newElement.Add(
                    new XAttribute("hasbeaconstation", HasBeaconStation.ToString()),
                    new XAttribute("isbeaconactive", IsBeaconActive.ToString()));
            }

            if (HasHuntingGrounds)
            {
                newElement.Add(
                    new XAttribute("hashuntinggrounds", true));
            }
            if (HasHuntingGrounds || OriginallyHadHuntingGrounds)
            {
                newElement.Add(
                    new XAttribute("originallyhadhuntinggrounds", true));
            }

            if (Type == LevelType.Outpost)
            {
                if (EventHistory.Any())
                {
                    newElement.Add(new XAttribute("eventhistory", string.Join(',', EventHistory)));
                }
                if (NonRepeatableEvents.Any())
                {
                    newElement.Add(new XAttribute("nonrepeatableevents", string.Join(',', NonRepeatableEvents)));
                }
                if (FinishedEvents.Any())
                {
                    var finishedEventsElement = new XElement(nameof(FinishedEvents));
                    foreach (var (set, count) in FinishedEvents)
                    {
                        var element = new XElement(nameof(FinishedEvents),
                            new XAttribute("set", set.Identifier),
                            new XAttribute("count", count));
                        finishedEventsElement.Add(element);
                    }
                    newElement.Add(finishedEventsElement);
                }
            }

            parentElement.Add(newElement);
        }
    }
}