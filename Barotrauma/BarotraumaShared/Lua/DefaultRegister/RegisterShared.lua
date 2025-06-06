local Register = LuaSetup.LuaUserData.RegisterType
local RegisterExtension = LuaSetup.LuaUserData.RegisterExtensionType
local RegisterBarotrauma = LuaSetup.LuaUserData.RegisterTypeBarotrauma

Register("System.TimeSpan")
Register("System.Exception")
Register("System.Console")
Register("System.Exception")

RegisterBarotrauma("Success`2")
RegisterBarotrauma("Failure`2")

RegisterBarotrauma("LuaSByte")
RegisterBarotrauma("LuaByte")
RegisterBarotrauma("LuaInt16")
RegisterBarotrauma("LuaUInt16")
RegisterBarotrauma("LuaInt32")
RegisterBarotrauma("LuaUInt32")
RegisterBarotrauma("LuaInt64")
RegisterBarotrauma("LuaUInt64")
RegisterBarotrauma("LuaSingle")
RegisterBarotrauma("LuaDouble")

RegisterBarotrauma("GameMain")
RegisterBarotrauma("Networking.BanList")
RegisterBarotrauma("Networking.BannedPlayer")

RegisterBarotrauma("Range`1")

RegisterBarotrauma("RichString")
RegisterBarotrauma("Identifier")
RegisterBarotrauma("LanguageIdentifier")

RegisterBarotrauma("Job")
RegisterBarotrauma("JobPrefab")
RegisterBarotrauma("JobVariant")

Register("Voronoi2.DoubleVector2")
Register("Voronoi2.Site")
Register("Voronoi2.Edge")
Register("Voronoi2.Halfedge")
Register("Voronoi2.VoronoiCell")
Register("Voronoi2.GraphEdge")

RegisterBarotrauma("WayPoint")
RegisterBarotrauma("Level")
RegisterBarotrauma("LevelData")
RegisterBarotrauma("Level+InterestingPosition")
RegisterBarotrauma("LevelGenerationParams")
RegisterBarotrauma("LevelObjectManager")
RegisterBarotrauma("LevelObject")
RegisterBarotrauma("LevelObjectPrefab")
RegisterBarotrauma("LevelTrigger")
RegisterBarotrauma("CaveGenerationParams")
RegisterBarotrauma("CaveGenerator")
RegisterBarotrauma("OutpostGenerationParams")
RegisterBarotrauma("OutpostGenerator")
RegisterBarotrauma("OutpostModuleInfo")
RegisterBarotrauma("BeaconStationInfo")
RegisterBarotrauma("NPCSet")
RegisterBarotrauma("RuinGeneration.Ruin")
RegisterBarotrauma("RuinGeneration.RuinGenerationParams")
RegisterBarotrauma("LevelWall")
RegisterBarotrauma("DestructibleLevelWall")
RegisterBarotrauma("Biome")
RegisterBarotrauma("Map")
RegisterBarotrauma("Networking.RespawnManager")
RegisterBarotrauma("Networking.RespawnManager+TeamSpecificState")

RegisterBarotrauma("Character")
RegisterBarotrauma("CharacterPrefab")
RegisterBarotrauma("CharacterInfo")
RegisterBarotrauma("CharacterInfoPrefab")
RegisterBarotrauma("CharacterInfo+HeadPreset")
RegisterBarotrauma("CharacterInfo+HeadInfo")
RegisterBarotrauma("CharacterHealth")
RegisterBarotrauma("CharacterHealth+LimbHealth")
RegisterBarotrauma("DamageModifier")
RegisterBarotrauma("CharacterInventory")
RegisterBarotrauma("CharacterParams")
RegisterBarotrauma("CharacterParams+AIParams")
RegisterBarotrauma("CharacterParams+TargetParams")
RegisterBarotrauma("CharacterParams+InventoryParams")
RegisterBarotrauma("CharacterParams+HealthParams")
RegisterBarotrauma("CharacterParams+ParticleParams")
RegisterBarotrauma("CharacterParams+SoundParams")
RegisterBarotrauma("SteeringManager")
RegisterBarotrauma("IndoorsSteeringManager")
RegisterBarotrauma("SteeringPath")
RegisterBarotrauma("CreatureMetrics")

RegisterBarotrauma("Item")
RegisterBarotrauma("DeconstructItem")
RegisterBarotrauma("PurchasedItem")
RegisterBarotrauma("PurchasedItemSwap")
RegisterBarotrauma("PurchasedUpgrade")
RegisterBarotrauma("SoldItem")
RegisterBarotrauma("StartItem")
RegisterBarotrauma("StartItemSet")
RegisterBarotrauma("RelatedItem")
RegisterBarotrauma("UpgradeManager")
RegisterBarotrauma("CargoManager")
RegisterBarotrauma("HireManager")
RegisterBarotrauma("FabricationRecipe")
RegisterBarotrauma("PreferredContainer")
RegisterBarotrauma("SwappableItem")
RegisterBarotrauma("FabricationRecipe+RequiredItemByIdentifier")
RegisterBarotrauma("FabricationRecipe+RequiredItemByTag")
RegisterBarotrauma("Submarine")

RegisterBarotrauma("Networking.AccountInfo")
RegisterBarotrauma("Networking.AccountId")
RegisterBarotrauma("Networking.SteamId")
RegisterBarotrauma("Networking.EpicAccountId")
RegisterBarotrauma("Networking.Address")
RegisterBarotrauma("Networking.UnknownAddress")
RegisterBarotrauma("Networking.P2PAddress")
RegisterBarotrauma("Networking.EosP2PAddress")
RegisterBarotrauma("Networking.SteamP2PAddress")
RegisterBarotrauma("Networking.PipeAddress")
RegisterBarotrauma("Networking.LidgrenAddress")
RegisterBarotrauma("Networking.Endpoint")
RegisterBarotrauma("Networking.SteamP2PEndpoint")
RegisterBarotrauma("Networking.PipeEndpoint")
RegisterBarotrauma("Networking.LidgrenEndpoint")

RegisterBarotrauma("INetSerializableStruct")
RegisterBarotrauma("Networking.Client")
RegisterBarotrauma("Networking.TempClient")
RegisterBarotrauma("Networking.NetworkConnection")
RegisterBarotrauma("Networking.LidgrenConnection")
RegisterBarotrauma("Networking.SteamP2PConnection")
RegisterBarotrauma("Networking.VoipQueue")
RegisterBarotrauma("Networking.ChatMessage")

RegisterBarotrauma("AnimController")
RegisterBarotrauma("HumanoidAnimController")
RegisterBarotrauma("FishAnimController")
RegisterBarotrauma("Limb")
RegisterBarotrauma("Ragdoll")
RegisterBarotrauma("RagdollParams")

RegisterBarotrauma("AfflictionPrefab")
RegisterBarotrauma("Affliction")
RegisterBarotrauma("AttackResult")
RegisterBarotrauma("Attack")
RegisterBarotrauma("Entity")
RegisterBarotrauma("EntityGrid")
RegisterBarotrauma("EntitySpawner")
RegisterBarotrauma("MapEntity")
RegisterBarotrauma("MapEntityPrefab")
RegisterBarotrauma("CauseOfDeath")
RegisterBarotrauma("Hull")
RegisterBarotrauma("WallSection")
RegisterBarotrauma("Structure")
RegisterBarotrauma("Gap")
RegisterBarotrauma("PhysicsBody")
RegisterBarotrauma("AbilityFlags")
RegisterBarotrauma("ItemPrefab")
RegisterBarotrauma("ItemAssemblyPrefab")
RegisterBarotrauma("InputType")

RegisterBarotrauma("FireSource")
RegisterBarotrauma("SerializableProperty")
LuaUserData.MakeFieldAccessible(RegisterBarotrauma("StatusEffect"), "user")
RegisterBarotrauma("DurationListElement")
RegisterBarotrauma("PropertyConditional")
RegisterBarotrauma("DelayedListElement")
RegisterBarotrauma("DelayedEffect")


RegisterBarotrauma("ContentPackageManager")
RegisterBarotrauma("ContentPackageManager+PackageSource")
RegisterBarotrauma("ContentPackageManager+EnabledPackages")
RegisterBarotrauma("ContentPackage")
RegisterBarotrauma("RegularPackage")
RegisterBarotrauma("CorePackage")
RegisterBarotrauma("ContentXElement")
RegisterBarotrauma("ContentPath")
RegisterBarotrauma("ContentPackageId")
RegisterBarotrauma("SteamWorkshopId")
RegisterBarotrauma("Md5Hash")

RegisterBarotrauma("AfflictionsFile")
RegisterBarotrauma("BackgroundCreaturePrefabsFile")
RegisterBarotrauma("BallastFloraFile")
RegisterBarotrauma("BeaconStationFile")
RegisterBarotrauma("CaveGenerationParametersFile")
RegisterBarotrauma("CharacterFile")
RegisterBarotrauma("ContentFile")
RegisterBarotrauma("CorpsesFile")
RegisterBarotrauma("DecalsFile")
RegisterBarotrauma("EnemySubmarineFile")
RegisterBarotrauma("EventManagerSettingsFile")
RegisterBarotrauma("FactionsFile")
RegisterBarotrauma("ItemAssemblyFile")
RegisterBarotrauma("ItemFile")
RegisterBarotrauma("JobsFile")
RegisterBarotrauma("LevelGenerationParametersFile")
RegisterBarotrauma("LevelObjectPrefabsFile")
RegisterBarotrauma("LocationTypesFile")
RegisterBarotrauma("MapGenerationParametersFile")
RegisterBarotrauma("MissionsFile")
RegisterBarotrauma("NPCConversationsFile")
RegisterBarotrauma("NPCPersonalityTraitsFile")
RegisterBarotrauma("NPCSetsFile")
RegisterBarotrauma("OrdersFile")
RegisterBarotrauma("OtherFile")
RegisterBarotrauma("OutpostConfigFile")
RegisterBarotrauma("OutpostFile")
RegisterBarotrauma("OutpostModuleFile")
RegisterBarotrauma("ParticlesFile")
RegisterBarotrauma("RandomEventsFile")
RegisterBarotrauma("RuinConfigFile")
RegisterBarotrauma("ServerExecutableFile")
RegisterBarotrauma("SkillSettingsFile")
RegisterBarotrauma("SoundsFile")
RegisterBarotrauma("StartItemsFile")
RegisterBarotrauma("StructureFile")
RegisterBarotrauma("SubmarineFile")
RegisterBarotrauma("TalentsFile")
RegisterBarotrauma("TalentTreesFile")
RegisterBarotrauma("TextFile")
RegisterBarotrauma("TutorialsFile")
RegisterBarotrauma("UIStyleFile")
RegisterBarotrauma("UpgradeModulesFile")
RegisterBarotrauma("WreckAIConfigFile")
RegisterBarotrauma("WreckFile")

Register("System.Xml.Linq.XElement")
Register("System.Xml.Linq.XName")
Register("System.Xml.Linq.XAttribute")
Register("System.Xml.Linq.XContainer")
Register("System.Xml.Linq.XDocument")
Register("System.Xml.Linq.XNode")


RegisterBarotrauma("SubmarineBody")
RegisterBarotrauma("Explosion")
RegisterBarotrauma("Networking.ServerSettings")
RegisterBarotrauma("Networking.ServerSettings+SavedClientPermission")
RegisterBarotrauma("Inventory")
RegisterBarotrauma("ItemInventory")
RegisterBarotrauma("Inventory+ItemSlot")
RegisterBarotrauma("FireSource")
RegisterBarotrauma("AutoItemPlacer")
RegisterBarotrauma("CircuitBoxConnection")
RegisterBarotrauma("CircuitBoxComponent")
RegisterBarotrauma("CircuitBoxNode")
RegisterBarotrauma("CircuitBoxWire")
RegisterBarotrauma("CircuitBoxInputOutputNode")
RegisterBarotrauma("CircuitBoxSelectable")
RegisterBarotrauma("CircuitBoxSizes")

local componentsToRegister = { "DockingPort", "Door", "GeneticMaterial", "Growable", "Holdable", "LevelResource", "ItemComponent", "ItemLabel", "LightComponent", "Controller", "Deconstructor", "Engine", "Fabricator", "OutpostTerminal", "Pump", "Reactor", "Steering", "PowerContainer", "Projectile", "Repairable", "Rope", "Scanner", "ButtonTerminal", "ConnectionPanel", "CustomInterface", "MemoryComponent", "Terminal", "WifiComponent", "Wire", "TriggerComponent", "ElectricalDischarger", "EntitySpawnerComponent", "ProducedItem", "VineTile", "GrowthSideExtension", "IdCard", "MeleeWeapon", "Pickable", "AbilityItemPickingTime", "Propulsion", "RangedWeapon", "AbilityRangedWeapon", "RepairTool", "Sprayer", "Throwable", "ItemContainer", "AbilityItemContainer", "Ladder", "LimbPos", "AbilityDeconstructedItem", "AbilityItemCreationMultiplier", "AbilityItemDeconstructedInventory", "MiniMap", "OxygenGenerator", "Sonar", "SonarTransducer", "Vent", "NameTag", "Planter", "Powered", "PowerTransfer", "Quality", "RemoteController", "AdderComponent", "AndComponent", "ArithmeticComponent", "ColorComponent", "ConcatComponent", "Connection", "CircuitBox", "DelayComponent", "DivideComponent", "EqualsComponent", "ExponentiationComponent", "FunctionComponent", "GreaterComponent", "ModuloComponent", "MotionSensor", "MultiplyComponent", "NotComponent", "OrComponent", "OscillatorComponent", "OxygenDetector", "RegExFindComponent", "RelayComponent", "SignalCheckComponent", "SmokeDetector", "StringComponent", "SubtractComponent", "TrigonometricFunctionComponent", "WaterDetector", "XorComponent", "StatusHUD", "Turret", "Wearable",
"GridInfo", "PowerSourceGroup"
}

for key, value in pairs(componentsToRegister) do
    RegisterBarotrauma("Items.Components." .. value)
end

LuaUserData.MakeFieldAccessible(RegisterBarotrauma("Items.Components.CustomInterface"), "customInterfaceElementList")
RegisterBarotrauma("Items.Components.CustomInterface+CustomInterfaceElement")

RegisterBarotrauma("WearableSprite")

RegisterBarotrauma("AIController")
RegisterBarotrauma("EnemyAIController")
RegisterBarotrauma("HumanAIController")
RegisterBarotrauma("AICharacter")
RegisterBarotrauma("AITarget")
RegisterBarotrauma("AITargetMemory")
RegisterBarotrauma("AIChatMessage")
RegisterBarotrauma("AIObjectiveManager")
RegisterBarotrauma("WreckAI")
RegisterBarotrauma("WreckAIConfig")

RegisterBarotrauma("AIObjectiveChargeBatteries")
RegisterBarotrauma("AIObjective")
RegisterBarotrauma("AIObjectiveCleanupItem")
RegisterBarotrauma("AIObjectiveCleanupItems")
RegisterBarotrauma("AIObjectiveCombat")
RegisterBarotrauma("AIObjectiveContainItem")
RegisterBarotrauma("AIObjectiveDeconstructItem")
RegisterBarotrauma("AIObjectiveDeconstructItems")
RegisterBarotrauma("AIObjectiveEscapeHandcuffs")
RegisterBarotrauma("AIObjectiveExtinguishFire")
RegisterBarotrauma("AIObjectiveExtinguishFires")
RegisterBarotrauma("AIObjectiveFightIntruders")
RegisterBarotrauma("AIObjectiveFindDivingGear")
RegisterBarotrauma("AIObjectiveFindSafety")
RegisterBarotrauma("AIObjectiveFixLeak")
RegisterBarotrauma("AIObjectiveFixLeaks")
RegisterBarotrauma("AIObjectiveGetItem")
RegisterBarotrauma("AIObjectiveGoTo")
RegisterBarotrauma("AIObjectiveIdle")
RegisterBarotrauma("AIObjectiveOperateItem")
RegisterBarotrauma("AIObjectivePumpWater")
RegisterBarotrauma("AIObjectiveRepairItem")
RegisterBarotrauma("AIObjectiveRepairItems")
RegisterBarotrauma("AIObjectiveRescue")
RegisterBarotrauma("AIObjectiveRescueAll")
RegisterBarotrauma("AIObjectiveReturn")

RegisterBarotrauma("Order")
RegisterBarotrauma("OrderPrefab")
RegisterBarotrauma("OrderTarget")

RegisterBarotrauma("TalentPrefab")
RegisterBarotrauma("TalentOption")
RegisterBarotrauma("TalentSubTree")
RegisterBarotrauma("TalentTree")
RegisterBarotrauma("CharacterTalent")
RegisterBarotrauma("Upgrade")
RegisterBarotrauma("UpgradeCategory")
RegisterBarotrauma("UpgradePrefab")
RegisterBarotrauma("UpgradeManager")

RegisterBarotrauma("Screen")
RegisterBarotrauma("GameScreen")
RegisterBarotrauma("GameSession")
RegisterBarotrauma("GameSettings")
RegisterBarotrauma("CrewManager")
RegisterBarotrauma("KarmaManager")

RegisterBarotrauma("GameMode")
RegisterBarotrauma("MissionMode")
RegisterBarotrauma("PvPMode")
RegisterBarotrauma("Mission")
RegisterBarotrauma("AbandonedOutpostMission")
RegisterBarotrauma("EliminateTargetsMission")
RegisterBarotrauma("EndMission")
RegisterBarotrauma("BeaconMission")
RegisterBarotrauma("CargoMission")
RegisterBarotrauma("CombatMission")
RegisterBarotrauma("EscortMission")
RegisterBarotrauma("GoToMission")
RegisterBarotrauma("MineralMission")
RegisterBarotrauma("MonsterMission")
RegisterBarotrauma("NestMission")
RegisterBarotrauma("PirateMission")
RegisterBarotrauma("SalvageMission")
RegisterBarotrauma("ScanMission")
RegisterBarotrauma("MissionPrefab")
RegisterBarotrauma("CampaignMode")
RegisterBarotrauma("CoOpMode")
RegisterBarotrauma("MultiPlayerCampaign")
RegisterBarotrauma("Radiation")

RegisterBarotrauma("CampaignMetadata")
RegisterBarotrauma("Wallet")

RegisterBarotrauma("Faction")
RegisterBarotrauma("FactionPrefab")
RegisterBarotrauma("Reputation")

RegisterBarotrauma("Location")
RegisterBarotrauma("LocationConnection")
RegisterBarotrauma("LocationType")
RegisterBarotrauma("LocationTypeChange")

RegisterBarotrauma("DebugConsole")
RegisterBarotrauma("DebugConsole+Command")

RegisterBarotrauma("TextManager")
RegisterBarotrauma("TextPack")

local descriptor = RegisterBarotrauma("NetLobbyScreen")

if SERVER then
    LuaUserData.MakeFieldAccessible(descriptor, "subs")
end

RegisterBarotrauma("EventManager")
RegisterBarotrauma("EventManagerSettings")
RegisterBarotrauma("Event")
RegisterBarotrauma("ArtifactEvent")
RegisterBarotrauma("MonsterEvent")
RegisterBarotrauma("ScriptedEvent")
RegisterBarotrauma("MalfunctionEvent")
RegisterBarotrauma("EventSet")
RegisterBarotrauma("EventPrefab")

RegisterBarotrauma("Networking.NetConfig")
RegisterBarotrauma("Networking.IWriteMessage")
RegisterBarotrauma("Networking.IReadMessage")
RegisterBarotrauma("Networking.NetEntityEvent")
RegisterBarotrauma("Networking.INetSerializable")
Register("Lidgren.Network.NetIncomingMessage")
Register("Lidgren.Network.NetConnection")
Register("System.Net.IPEndPoint")
Register("System.Net.IPAddress")

RegisterBarotrauma("Skill")
RegisterBarotrauma("SkillPrefab")
RegisterBarotrauma("SkillSettings")

RegisterBarotrauma("TraitorManager")
RegisterBarotrauma("TraitorEvent")
RegisterBarotrauma("TraitorEventPrefab")
RegisterBarotrauma("TraitorManager+TraitorResults")

Register("FarseerPhysics.Dynamics.Body")
Register("FarseerPhysics.Dynamics.World")
Register("FarseerPhysics.Dynamics.Fixture")
Register("FarseerPhysics.ConvertUnits")
Register("FarseerPhysics.Collision.AABB")
Register("FarseerPhysics.Collision.ContactFeature")
Register("FarseerPhysics.Collision.ManifoldPoint")
Register("FarseerPhysics.Collision.ContactID")
Register("FarseerPhysics.Collision.Manifold")
Register("FarseerPhysics.Collision.RayCastInput")
Register("FarseerPhysics.Collision.ClipVertex")
Register("FarseerPhysics.Collision.RayCastOutput")
Register("FarseerPhysics.Collision.EPAxis")
Register("FarseerPhysics.Collision.ReferenceFace")
Register("FarseerPhysics.Collision.Collision")

RegisterBarotrauma("Physics")

local toolBox = RegisterBarotrauma("ToolBox")
if CLIENT then
    LuaUserData.RemoveMember(toolBox, "OpenFileWithShell")
end

RegisterBarotrauma("Camera")
RegisterBarotrauma("Key")

RegisterBarotrauma("PrefabCollection`1")

RegisterBarotrauma("PrefabSelector`1")

RegisterBarotrauma("Pair`2")

RegisterBarotrauma("Items.Components.Signal")
RegisterBarotrauma("SubmarineInfo")

RegisterBarotrauma("MapCreatures.Behavior.BallastFloraBehavior")
RegisterBarotrauma("MapCreatures.Behavior.BallastFloraBranch")

RegisterBarotrauma("PetBehavior")
RegisterBarotrauma("SwarmBehavior")
RegisterBarotrauma("LatchOntoAI")

RegisterBarotrauma("Decal")
RegisterBarotrauma("DecalPrefab")
RegisterBarotrauma("DecalManager")

RegisterBarotrauma("PriceInfo")

RegisterBarotrauma("Voting")

Register("Microsoft.Xna.Framework.Vector2")
Register("Microsoft.Xna.Framework.Vector3")
Register("Microsoft.Xna.Framework.Vector4")
Register("Microsoft.Xna.Framework.Color")
Register("Microsoft.Xna.Framework.Point")
Register("Microsoft.Xna.Framework.Rectangle")
Register("Microsoft.Xna.Framework.Matrix")

local friend = Register("Steamworks.Friend")

LuaUserData.RemoveMember(friend, "InviteToGame")
LuaUserData.RemoveMember(friend, "SendMessage")

local workshopItem = Register("Steamworks.Ugc.Item")

LuaUserData.RemoveMember(workshopItem, "Subscribe")
LuaUserData.RemoveMember(workshopItem, "DownloadAsync")
LuaUserData.RemoveMember(workshopItem, "Unsubscribe")
LuaUserData.RemoveMember(workshopItem, "AddFavorite")
LuaUserData.RemoveMember(workshopItem, "RemoveFavorite")
LuaUserData.RemoveMember(workshopItem, "Vote")
LuaUserData.RemoveMember(workshopItem, "GetUserVote")
LuaUserData.RemoveMember(workshopItem, "Edit")

RegisterExtension("Barotrauma.MathUtils")
RegisterExtension("Barotrauma.XMLExtensions")