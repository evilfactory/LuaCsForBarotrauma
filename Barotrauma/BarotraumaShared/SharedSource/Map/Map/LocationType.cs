﻿using Barotrauma.Extensions;
using Barotrauma.IO;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Barotrauma
{
    class LocationType : PrefabWithUintIdentifier
    {
        public static readonly PrefabCollection<LocationType> Prefabs = new PrefabCollection<LocationType>();

        private readonly ImmutableArray<string> rawNames;
        private readonly ImmutableArray<Sprite> portraits;

        //<name, commonness>
        private readonly ImmutableArray<(Identifier Identifier, float Commonness, bool AlwaysAvailableIfMissingFromCrew)> hireableJobs;
        private readonly float totalHireableWeight;

        public readonly Dictionary<int, float> CommonnessPerZone = new Dictionary<int, float>();
        public readonly Dictionary<int, int> MinCountPerZone = new Dictionary<int, int>();

        public readonly LocalizedString Name;
        public readonly LocalizedString Description;

        public readonly Identifier ForceLocationName;

        public readonly float BeaconStationChance;

        public readonly CharacterTeamType OutpostTeam;

        /// <summary>
        /// Is this location type considered valid for e.g. events and missions that are should be available in "any outpost"
        /// </summary>
        public bool IsAnyOutpost;

        public readonly List<LocationTypeChange> CanChangeTo = new List<LocationTypeChange>();

        public readonly ImmutableArray<Identifier> MissionIdentifiers;
        public readonly ImmutableArray<Identifier> MissionTags;

        public readonly List<string> HideEntitySubcategories = new List<string>();

        public bool IsEnterable { get; private set; }

        public bool AllowAsBiomeGate { get; private set; }

        /// <summary>
        /// Can this location type be used in the random, non-campaign levels that don't take place in any specific zone
        /// </summary>
        public bool AllowInRandomLevels { get; private set; }

        public bool UsePortraitInRandomLoadingScreens
        {
            get;
            private set;
        }

        private readonly ImmutableArray<Identifier>? nameIdentifiers = null;

        private LanguageIdentifier nameFormatLanguage;

        private ImmutableArray<string>? nameFormats = null;
        public IReadOnlyList<string> NameFormats
        {
            get
            {
                if (nameFormats == null || GameSettings.CurrentConfig.Language != nameFormatLanguage)
                {
                    nameFormats = TextManager.GetAll($"LocationNameFormat.{Identifier}").ToImmutableArray();
                    nameFormatLanguage = GameSettings.CurrentConfig.Language;
                }
                return nameFormats;
            }
        }

        public bool HasHireableCharacters
        {
            get { return hireableJobs.Any(); }
        }

        public bool HasOutpost
        {
            get;
            private set;
        }

        public Identifier ReplaceInRadiation { get; }

        public Identifier DescriptionInRadiation { get; }

        /// <summary>
        /// If set, forces the location to be assigned to this faction. Set to "None" if you don't want the location to be assigned to any faction.
        /// </summary>
        public Identifier Faction { get; }

        /// <summary>
        /// If set, forces the location to be assigned to this secondary faction. Set to "None" if you don't want the location to be assigned to any secondary faction.
        /// </summary>
        public Identifier SecondaryFaction { get; }

        public Sprite Sprite { get; private set; }
        public Sprite RadiationSprite { get; }

        private readonly Identifier forceOutpostGenerationParamsIdentifier;

        /// <summary>
        /// If set to true, only event sets that explicitly define this location type in <see cref="EventSet.LocationTypeIdentifiers"/> can be selected at this location. Defaults to false.
        /// </summary>
        public bool IgnoreGenericEvents { get; }

        public Color SpriteColor
        {
            get;
            private set;
        }

        public float StoreMaxReputationModifier { get; } = 0.1f;
        public float StoreMinReputationModifier { get; } = 1.0f;
        public float StoreSellPriceModifier { get; } = 0.3f;
        public float StoreBuyPriceModifier { get; } = 1f;
        public float DailySpecialPriceModifier { get; } = 0.5f;
        public float RequestGoodPriceModifier { get; } = 2f;
        public float RequestGoodBuyPriceModifier { get; } = 5f;
        public int StoreInitialBalance { get; } = 5000;
        /// <summary>
        /// In percentages
        /// </summary>
        public int StorePriceModifierRange { get; } = 5;
        public int DailySpecialsCount { get; } = 1;
        public int RequestedGoodsCount { get; } = 1;

        public readonly bool ShowSonarMarker = true;

        public override string ToString()
        {
            return $"LocationType (" + Identifier + ")";
        }

        public LocationType(ContentXElement element, LocationTypesFile file) : base(file, element.GetAttributeIdentifier("identifier", element.Name.LocalName))
        {
            Name = TextManager.Get("LocationName." + Identifier, "unknown");
            Description = TextManager.Get("LocationDescription." + Identifier, "");

            BeaconStationChance = element.GetAttributeFloat("beaconstationchance", 0.0f);

            UsePortraitInRandomLoadingScreens = element.GetAttributeBool(nameof(UsePortraitInRandomLoadingScreens), true);
            HasOutpost = element.GetAttributeBool("hasoutpost", true);
            IsEnterable = element.GetAttributeBool("isenterable", HasOutpost);
            AllowAsBiomeGate = element.GetAttributeBool(nameof(AllowAsBiomeGate), true);
            AllowInRandomLevels = element.GetAttributeBool(nameof(AllowInRandomLevels), true);

            Faction = element.GetAttributeIdentifier(nameof(Faction), Identifier.Empty);
            SecondaryFaction = element.GetAttributeIdentifier(nameof(SecondaryFaction), Identifier.Empty);

            ShowSonarMarker = element.GetAttributeBool("showsonarmarker", true);

            MissionIdentifiers = element.GetAttributeIdentifierArray("missionidentifiers", Array.Empty<Identifier>()).ToImmutableArray();
            MissionTags = element.GetAttributeIdentifierArray("missiontags", Array.Empty<Identifier>()).ToImmutableArray();

            HideEntitySubcategories = element.GetAttributeStringArray("hideentitysubcategories", Array.Empty<string>()).ToList();

            ReplaceInRadiation = element.GetAttributeIdentifier(nameof(ReplaceInRadiation), Identifier.Empty);
            DescriptionInRadiation = element.GetAttributeIdentifier(nameof(DescriptionInRadiation), "locationdescription.abandonedirradiated");

            forceOutpostGenerationParamsIdentifier = element.GetAttributeIdentifier("forceoutpostgenerationparams", Identifier.Empty);

            IgnoreGenericEvents = element.GetAttributeBool(nameof(IgnoreGenericEvents), false);

            IsAnyOutpost = element.GetAttributeBool(nameof(IsAnyOutpost), def: HasOutpost);

            string teamStr = element.GetAttributeString("outpostteam", "FriendlyNPC");
            Enum.TryParse(teamStr, out OutpostTeam);

            if (element.GetAttribute("name") != null)
            {
                ForceLocationName = element.GetAttributeIdentifier("name", string.Empty);
            }
            else
            {
                var names = new List<string>();
                //backwards compatibility for location names defined in a text file
                string[] rawNamePaths = element.GetAttributeStringArray("namefile", Array.Empty<string>());
                if (rawNamePaths.Any())
                {
                    foreach (string rawPath in rawNamePaths)
                    {
                        try
                        {
                            var path = ContentPath.FromRaw(element.ContentPackage, rawPath.Trim());
                            names.AddRange(File.ReadAllLines(path.Value, catchUnauthorizedAccessExceptions: false).ToList());
                        }
                        catch (Exception e)
                        {
                            DebugConsole.ThrowError($"Failed to read name file \"rawPath\" for location type \"{Identifier}\"!", e);
                        }
                    }
                    if (!names.Any())
                    {
                        names.Add("ERROR: No names found");
                    }
                    this.rawNames = names.ToImmutableArray();
                }
                else
                {
                    nameIdentifiers = element.GetAttributeIdentifierArray("nameidentifiers", new Identifier[] { Identifier }).ToImmutableArray();
                }
            }

            string[] commonnessPerZoneStrs = element.GetAttributeStringArray("commonnessperzone", Array.Empty<string>());
            foreach (string commonnessPerZoneStr in commonnessPerZoneStrs)
            {
                string[] splitCommonnessPerZone = commonnessPerZoneStr.Split(':');                
                if (splitCommonnessPerZone.Length != 2 ||
                    !int.TryParse(splitCommonnessPerZone[0].Trim(), out int zoneIndex) ||
                    !float.TryParse(splitCommonnessPerZone[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float zoneCommonness))
                {
                    DebugConsole.ThrowError("Failed to read commonness values for location type \"" + Identifier + "\" - commonness should be given in the format \"zone1index: zone1commonness, zone2index: zone2commonness\"");
                    break;
                }
                CommonnessPerZone[zoneIndex] = zoneCommonness;
            }

            string[] minCountPerZoneStrs = element.GetAttributeStringArray("mincountperzone", Array.Empty<string>());
            foreach (string minCountPerZoneStr in minCountPerZoneStrs)
            {
                string[] splitMinCountPerZone = minCountPerZoneStr.Split(':');
                if (splitMinCountPerZone.Length != 2 ||
                    !int.TryParse(splitMinCountPerZone[0].Trim(), out int zoneIndex) ||
                    !int.TryParse(splitMinCountPerZone[1].Trim(), out int minCount))
                {
                    DebugConsole.ThrowError("Failed to read minimum count values for location type \"" + Identifier + "\" - minimum counts should be given in the format \"zone1index: zone1mincount, zone2index: zone2mincount\"");
                    break;
                }
                MinCountPerZone[zoneIndex] = minCount;
            }
            var portraits = new List<Sprite>();
            var hireableJobs = new List<(Identifier, float, bool)>();
            foreach (var subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "hireable":
                        Identifier jobIdentifier = subElement.GetAttributeIdentifier("identifier", Identifier.Empty);
                        float jobCommonness = subElement.GetAttributeFloat("commonness", 1.0f);
                        bool availableIfMissing = subElement.GetAttributeBool("AlwaysAvailableIfMissingFromCrew", false);
                        totalHireableWeight += jobCommonness;
                        hireableJobs.Add((jobIdentifier, jobCommonness, availableIfMissing));
                        break;
                    case "symbol":
                        Sprite = new Sprite(subElement, lazyLoad: true);
                        SpriteColor = subElement.GetAttributeColor("color", Color.White);
                        break;
                    case "radiationsymbol":
                        RadiationSprite = new Sprite(subElement, lazyLoad: true);
                        break;
                    case "changeto":
                        CanChangeTo.Add(new LocationTypeChange(Identifier, subElement, requireChangeMessages: true));
                        break;
                    case "portrait":
                        var portrait = new Sprite(subElement, lazyLoad: true);
                        if (portrait != null)
                        {
                            portraits.Add(portrait);
                        }
                        break;
                    case "store":
                        StoreMaxReputationModifier = subElement.GetAttributeFloat("maxreputationmodifier", StoreMaxReputationModifier);
                        StoreBuyPriceModifier = subElement.GetAttributeFloat("buypricemodifier", StoreBuyPriceModifier);
                        StoreMinReputationModifier = subElement.GetAttributeFloat("minreputationmodifier", StoreMaxReputationModifier);
                        StoreSellPriceModifier = subElement.GetAttributeFloat("sellpricemodifier", StoreSellPriceModifier);
                        DailySpecialPriceModifier = subElement.GetAttributeFloat("dailyspecialpricemodifier", DailySpecialPriceModifier);
                        RequestGoodPriceModifier = subElement.GetAttributeFloat("requestgoodpricemodifier", RequestGoodPriceModifier);
                        RequestGoodBuyPriceModifier = subElement.GetAttributeFloat("requestgoodbuypricemodifier", RequestGoodBuyPriceModifier);
                        StoreInitialBalance = subElement.GetAttributeInt("initialbalance", StoreInitialBalance);
                        StorePriceModifierRange = subElement.GetAttributeInt("pricemodifierrange", StorePriceModifierRange);
                        DailySpecialsCount = subElement.GetAttributeInt("dailyspecialscount", DailySpecialsCount);
                        RequestedGoodsCount = subElement.GetAttributeInt("requestedgoodscount", RequestedGoodsCount);
                        break;
                }
            }
            this.portraits = portraits.ToImmutableArray();
            this.hireableJobs = hireableJobs.ToImmutableArray();
        }

        public IEnumerable<JobPrefab> GetHireablesMissingFromCrew()
        {
            if (GameMain.GameSession?.CrewManager != null)
            {
                var missingJobs = hireableJobs
                    .Where(j => j.AlwaysAvailableIfMissingFromCrew)
                    .Where(j => GameMain.GameSession.CrewManager.GetCharacterInfos().None(c => c.Job?.Prefab.Identifier == j.Identifier));
                if (missingJobs.Any())
                {
                    foreach (var missingJob in missingJobs)
                    {
                        if (JobPrefab.Prefabs.TryGet(missingJob.Identifier, out JobPrefab job))
                        {
                            yield return job;
                        }
                    }
                }
            }
        }

        public JobPrefab GetRandomHireable()
        {
            Identifier selectedJobId = hireableJobs.GetRandomByWeight(j => j.Commonness, Rand.RandSync.ServerAndClient).Identifier;
            if (JobPrefab.Prefabs.TryGet(selectedJobId, out JobPrefab job))
            {
                return job;
            }
            return null;
        }

        public Sprite GetPortrait(int randomSeed)
        {
            if (portraits.Length == 0) { return null; }
            return portraits[Math.Abs(randomSeed) % portraits.Length];
        }

        public Identifier GetRandomNameId(Random rand, IEnumerable<Location> existingLocations)
        {
            if (nameIdentifiers == null)
            {
                return Identifier.Empty;
            }
            List<Identifier> nameIds = new List<Identifier>();
            foreach (var nameId in nameIdentifiers)
            {
                int index = 0;
                while (true)
                {
                    Identifier tag = $"LocationName.{nameId}.{index}".ToIdentifier();
                    if (TextManager.ContainsTag(tag, TextManager.DefaultLanguage))
                    {
                        nameIds.Add(tag);
                        index++;
                    }
                    else
                    {
                        if (index == 0)
                        {
                            DebugConsole.ThrowError($"Could not find any location names for the location type {Identifier}. Name identifier: {nameId}");
                        }
                        break;
                    }
                }
            }
            if (nameIds.None())
            {
                return Identifier.Empty;
            }
            if (existingLocations != null)
            {
                var unusedNameIds = nameIds.FindAll(nameId => existingLocations.None(l => l.NameIdentifier == nameId));
                if (unusedNameIds.Count > 0)
                {
                    return unusedNameIds[rand.Next() % unusedNameIds.Count];
                }
            }
            return nameIds[rand.Next() % nameIds.Count];
        }

        /// <summary>
        /// For backwards compatibility. Chooses a random name from the names defined in the .txt name files (<see cref="rawNamePaths"/>).
        /// </summary>
        public string GetRandomRawName(Random rand, IEnumerable<Location> existingLocations)
        {
            if (rawNames == null || rawNames.None()) { return string.Empty; }
            if (existingLocations != null)
            {
                var unusedNames = rawNames.Where(name => !existingLocations.Any(l => l.DisplayName.Value == name)).ToList();
                if (unusedNames.Count > 0)
                {
                    return unusedNames[rand.Next() % unusedNames.Count];
                }
            }
            return rawNames[rand.Next() % rawNames.Length];
        }

        public static LocationType Random(Random rand, int? zone = null, bool requireOutpost = false, Func<LocationType, bool> predicate = null)
        {
            Debug.Assert(Prefabs.Any(), "LocationType.list.Count == 0, you probably need to initialize LocationTypes");

            LocationType[] allowedLocationTypes =
                Prefabs.Where(lt =>
                    (predicate == null || predicate(lt)) && IsValid(lt))
                    .OrderBy(p => p.UintIdentifier).ToArray();

            bool IsValid(LocationType lt)
            {
                if (requireOutpost && !lt.HasOutpost) { return false; }
                if (zone.HasValue)
                {
                    if (!lt.CommonnessPerZone.ContainsKey(zone.Value)) { return false; }
                }
                //if zone is not defined, this is a "random" (non-campaign) level
                //-> don't choose location types that aren't allowed in those
                else if (!lt.AllowInRandomLevels)
                {
                    return false;
                }
                return true;
            }

            if (allowedLocationTypes.Length == 0)
            {
                DebugConsole.ThrowError("Could not generate a random location type - no location types for the zone " + zone + " found!");
            }

            if (zone.HasValue)
            {
                return ToolBox.SelectWeightedRandom(
                    allowedLocationTypes, 
                    allowedLocationTypes.Select(a => a.CommonnessPerZone[zone.Value]).ToArray(),
                    rand);
            }
            else
            {
                return allowedLocationTypes[rand.Next() % allowedLocationTypes.Length];
            }
        }

        public OutpostGenerationParams GetForcedOutpostGenerationParams()
        {
            if (OutpostGenerationParams.OutpostParams.TryGet(forceOutpostGenerationParamsIdentifier, out var parameters))
            {
                return parameters;
            }
            return null;
        }

        public override void Dispose() { }
    }
}
