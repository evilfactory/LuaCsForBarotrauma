﻿using Barotrauma.Extensions;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Barotrauma
{
    class AIObjectiveFindSafety : AIObjective
    {
        public override Identifier Identifier { get; set; } = "find safety".ToIdentifier();
        public override bool ForceRun => true;
        public override bool KeepDivingGearOn => true;
        public override bool IgnoreUnsafeHulls => true;
        protected override bool ConcurrentObjectives => true;
        protected override bool AllowOutsideSubmarine => true;
        protected override bool AllowInAnySub => true;
        public override bool AbandonWhenCannotCompleteSubObjectives => false;

        private const float PriorityIncrease = 100;
        private const float PriorityDecrease = 10;
        private const float SearchHullInterval = 3.0f;

        private float currentHullSafety;

        private float searchHullTimer;

        private AIObjectiveGoTo goToObjective;
        private AIObjectiveFindDivingGear divingGearObjective;

        public AIObjectiveFindSafety(Character character, AIObjectiveManager objectiveManager, float priorityModifier = 1) : base(character, objectiveManager, priorityModifier) { }

        protected override bool CheckObjectiveState() => false;
        public override bool CanBeCompleted => true;

        private bool resetPriority;

        protected override float GetPriority()
        {
            if (character.CurrentHull == null)
            {
                Priority = (
                    objectiveManager.CurrentOrder is AIObjectiveGoTo ||
                    objectiveManager.HasActiveObjective<AIObjectiveRescue>() ||
                    objectiveManager.Objectives.Any(o => o is AIObjectiveCombat or AIObjectiveReturn && o.Priority > 0))
                    && ((!character.IsLowInOxygen && character.IsImmuneToPressure)|| HumanAIController.HasDivingSuit(character)) ? 0 : AIObjectiveManager.EmergencyObjectivePriority - 10;
            }
            else
            {
                bool isSuffocatingInDivingSuit = character.IsLowInOxygen && !character.AnimController.HeadInWater && HumanAIController.HasDivingSuit(character, requireOxygenTank: false);
                static bool IsSuffocatingWithoutDivingGear(Character c) => c.IsLowInOxygen && c.AnimController.HeadInWater && !HumanAIController.HasDivingGear(c, requireOxygenTank: true);

                if (isSuffocatingInDivingSuit || (!objectiveManager.HasActiveObjective<AIObjectiveFindDivingGear>() && IsSuffocatingWithoutDivingGear(character)))
                {
                    Priority = AIObjectiveManager.MaxObjectivePriority;
                }
                else if (NeedMoreDivingGear(character.CurrentHull, AIObjectiveFindDivingGear.GetMinOxygen(character)))
                {
                    if (objectiveManager.FailedToFindDivingGearForDepth &&
                        HumanAIController.HasDivingSuit(character, requireSuitablePressureProtection: false))
                    {
                        //we have a suit that's not suitable for the pressure,
                        //but we've failed to find a better one
                        // shit, not much we can do here, let's just allow the bot to get on with their current objective
                        Priority = 0;
                    }
                    else
                    {
                        Priority = AIObjectiveManager.MaxObjectivePriority;
                    }
                }
                else if (objectiveManager.CurrentOrder is AIObjectiveGoTo { IsFollowOrder: true })
                {
                    // Ordered to follow -> Don't flee from the enemies/fires (doesn't get here if we need more oxygen).
                    Priority = 0;
                }
                else if ((objectiveManager.IsCurrentOrder<AIObjectiveGoTo>() || objectiveManager.IsCurrentOrder<AIObjectiveReturn>()) &&
                         character.Submarine != null && !character.IsOnFriendlyTeam(character.Submarine.TeamID))
                {
                    // Ordered to follow, hold position, or return back to main sub inside a hostile sub
                    // -> ignore find safety unless we need to find a diving gear
                    Priority = 0;
                }
                else if (objectiveManager.Objectives.Any(o => o is AIObjectiveCombat && o.Priority > 0))
                {
                    Priority = 0;
                }
                Priority = MathHelper.Clamp(Priority, 0, AIObjectiveManager.MaxObjectivePriority);
                if (divingGearObjective is { IsCompleted: false, CanBeCompleted: true, Priority: > 0f })
                {
                    // Boost the priority while seeking the diving gear
                    Priority = Math.Max(Priority, Math.Min(AIObjectiveManager.EmergencyObjectivePriority - 1, AIObjectiveManager.MaxObjectivePriority));
                }
            }
            return Priority;
        }

        public override void Update(float deltaTime)
        {
            if (retryTimer > 0)
            {
                retryTimer -= deltaTime;
                if (retryTimer <= 0)
                {
                    retryCounter = 0;
                }
            }
            if (resetPriority)
            {
                Priority = 0;
                resetPriority = false;
                return;
            }
            if (character.CurrentHull == null)
            {
                currentHullSafety = 0;
            }
            else
            {
                currentHullSafety = HumanAIController.CurrentHullSafety;
                if (currentHullSafety > HumanAIController.HULL_SAFETY_THRESHOLD)
                {
                    Priority -= PriorityDecrease * deltaTime;
                    if (currentHullSafety >= 100 && !character.IsLowInOxygen)
                    {
                        // Reduce the priority to zero so that the bot can get switch to other objectives immediately, e.g. when entering the airlock.
                        Priority = 0;
                    }
                }
                else
                {
                    float dangerFactor = (100 - currentHullSafety) / 100;
                    Priority += dangerFactor * PriorityIncrease * deltaTime;
                }
                Priority = MathHelper.Clamp(Priority, 0, AIObjectiveManager.MaxObjectivePriority);
            }
        }

        private Hull currentSafeHull;
        private Hull previousSafeHull;
        private bool cannotFindSafeHull;
        private bool cannotFindDivingGear;
        private readonly int findDivingGearAttempts = 2;
        private int retryCounter;
        private readonly float retryResetTime = 5;
        private float retryTimer;
        protected override void Act(float deltaTime)
        {
            if (resetPriority) { return; }
            var currentHull = character.CurrentHull;
            bool dangerousPressure =  (currentHull == null || currentHull.LethalPressure > 0) && !character.IsProtectedFromPressure;
            bool shouldActOnSuffocation = character.IsLowInOxygen && !character.AnimController.HeadInWater && HumanAIController.HasDivingSuit(character, requireOxygenTank: false);
            if (!character.LockHands && (!dangerousPressure || shouldActOnSuffocation || cannotFindSafeHull))
            {
                bool needsDivingGear = HumanAIController.NeedsDivingGear(currentHull, out bool needsDivingSuit, objectiveManager);
                if (character.TeamID == CharacterTeamType.FriendlyNPC && character.Submarine?.Info is { IsOutpost: true })
                {
                    // In outposts, the NPCs don't try to use diving suits, because otherwise there's probably not enough for those trying to fix the leaks.
                    // This is not a hard rule: the bots may still grab a suit, unless they find a diving mask.
                    needsDivingSuit = false;
                }
                bool needsEquipment = shouldActOnSuffocation;
                if (needsDivingSuit)
                {
                    needsEquipment = !HumanAIController.HasDivingSuit(character, AIObjectiveFindDivingGear.GetMinOxygen(character));
                }
                else if (needsDivingGear)
                {
                    needsEquipment = !HumanAIController.HasDivingGear(character, AIObjectiveFindDivingGear.GetMinOxygen(character));
                }
                if (needsEquipment)
                {
                    if (cannotFindDivingGear && retryCounter < findDivingGearAttempts)
                    {
                        retryTimer = retryResetTime;
                        retryCounter++;
                        needsDivingSuit = !needsDivingSuit;
                        RemoveSubObjective(ref divingGearObjective);
                    }
                    if (divingGearObjective == null)
                    {
                        cannotFindDivingGear = false;
                        RemoveSubObjective(ref goToObjective);
                        TryAddSubObjective(ref divingGearObjective,
                        constructor: () => new AIObjectiveFindDivingGear(character, needsDivingSuit, objectiveManager),
                        onAbandon: () =>
                        {
                            searchHullTimer = Math.Min(1, searchHullTimer);
                            cannotFindDivingGear = true;
                            // Don't reset the diving gear objective, because it's possible that there is no diving gear -> seek a safe hull and then reset so that we can check again.
                        },
                        onCompleted: () =>
                        {
                            resetPriority = true;
                            searchHullTimer = Math.Min(1, searchHullTimer);
                            RemoveSubObjective(ref divingGearObjective);
                        });
                    }
                }
            }
            if (divingGearObjective == null || !divingGearObjective.CanBeCompleted)
            {
                if (currentHullSafety < HumanAIController.HULL_SAFETY_THRESHOLD)
                {
                    searchHullTimer = Math.Min(1, searchHullTimer);
                }
                if (searchHullTimer > 0.0f)
                {
                    searchHullTimer -= deltaTime;
                }
                else
                {
                    HullSearchStatus hullSearchStatus = FindBestHull(out Hull potentialSafeHull, allowChangingSubmarine: character.TeamID != CharacterTeamType.FriendlyNPC);
                    if (hullSearchStatus != HullSearchStatus.Finished)
                    {
                        UpdateSimpleEscape(deltaTime);
                        return;
                    }
                    searchHullTimer = SearchHullInterval * Rand.Range(0.9f, 1.1f);
                    previousSafeHull = currentSafeHull;
                    currentSafeHull = potentialSafeHull;
                    cannotFindSafeHull = currentSafeHull == null || NeedMoreDivingGear(currentSafeHull);
                    currentSafeHull ??= previousSafeHull;
                    if (currentSafeHull != null && currentSafeHull != currentHull)
                    {
                        if (goToObjective?.Target != currentSafeHull)
                        {
                            RemoveSubObjective(ref goToObjective);
                        }
                        TryAddSubObjective(ref goToObjective,
                        constructor: () => new AIObjectiveGoTo(currentSafeHull, character, objectiveManager, getDivingGearIfNeeded: true)
                        {
                            SpeakIfFails = false,
                            AllowGoingOutside =
                                character.IsProtectedFromPressure ||
                                character.CurrentHull == null || 
                                character.CurrentHull.IsAirlock ||
                                character.CurrentHull.LeadsOutside(character)
                        },
                        onCompleted: () =>
                        {
                            if (currentHullSafety > HumanAIController.HULL_SAFETY_THRESHOLD ||
                                HumanAIController.NeedsDivingGear(currentHull, out bool needsSuit) && (needsSuit ? HumanAIController.HasDivingSuit(character) : HumanAIController.HasDivingMask(character)))
                            {
                                resetPriority = true;
                                searchHullTimer = Math.Min(1, searchHullTimer);
                            }
                            RemoveSubObjective(ref goToObjective);
                            if (cannotFindDivingGear)
                            {
                                // If diving gear objective failed, let's reset it here.
                                RemoveSubObjective(ref divingGearObjective);
                            }
                        },
                        onAbandon: () =>
                        {
                            // Don't ignore any hulls if outside, because apparently it happens that we can't find a path, in which case we just want to try again.
                            // If we ignore the hull, it might be the only airlock in the target sub, which ignores the whole sub.
                            // If the target hull is inside a submarine that is not our main sub, just ignore it normally when it cannot be reached. This happens with outposts.
                            if (goToObjective != null)
                            {
                                if (goToObjective.Target is Hull hull)
                                {
                                    if (currentHull != null || !Submarine.MainSubs.Contains(hull.Submarine))
                                    {
                                        HumanAIController.UnreachableHulls.Add(hull);
                                    }
                                }
                            }
                            RemoveSubObjective(ref goToObjective);
                        });
                    }
                    else
                    {
                        RemoveSubObjective(ref goToObjective);
                    }
                }
                if (subObjectives.Any(so => so.CanBeCompleted)) { return; }
                UpdateSimpleEscape(deltaTime);

                bool inFriendlySub = 
                    character.IsInFriendlySub || 
                    (character.IsEscorted && character.IsInPlayerSub);
                if (cannotFindSafeHull && !inFriendlySub && character.IsOnPlayerTeam && objectiveManager.Objectives.None(o => o is AIObjectiveReturn))
                {
                    if (OrderPrefab.Prefabs.TryGet("return".ToIdentifier(), out OrderPrefab orderPrefab))
                    {
                        objectiveManager.AddObjective(new AIObjectiveReturn(character, character, objectiveManager));
                    }
                }
            }
        }

        public void UpdateSimpleEscape(float deltaTime)
        {
            Vector2 escapeVel = Vector2.Zero;
            if (character.CurrentHull != null)
            {
                foreach (Hull hull in HumanAIController.VisibleHulls)
                {
                    foreach (FireSource fireSource in hull.FireSources)
                    {
                        Vector2 dir = character.Position - fireSource.Position;
                        float distMultiplier = MathHelper.Clamp(100.0f / Vector2.Distance(fireSource.Position, character.Position), 0.1f, 10.0f);
                        escapeVel += new Vector2(Math.Sign(dir.X) * distMultiplier, !character.IsClimbing ? 0 : Math.Sign(dir.Y) * distMultiplier);
                    }
                }
                foreach (Character enemy in Character.CharacterList)
                {
                    if (!HumanAIController.IsActive(enemy) || HumanAIController.IsFriendly(enemy) || enemy.IsHandcuffed) { continue; }
                    if (HumanAIController.VisibleHulls.Contains(enemy.CurrentHull))
                    {
                        Vector2 dir = character.Position - enemy.Position;
                        float distMultiplier = MathHelper.Clamp(100.0f / Vector2.Distance(enemy.Position, character.Position), 0.1f, 10.0f);
                        escapeVel += new Vector2(Math.Sign(dir.X) * distMultiplier, !character.IsClimbing ? 0 : Math.Sign(dir.Y) * distMultiplier);
                    }
                }
            }
            if (escapeVel != Vector2.Zero && character.CurrentHull is Hull currentHull)
            {
                float left = currentHull.Rect.X + 50;
                float right = currentHull.Rect.Right - 50;
                //only move if we haven't reached the edge of the room
                if (escapeVel.X < 0 && character.Position.X > left || escapeVel.X > 0 && character.Position.X < right)
                {
                    character.ReleaseSecondaryItem();
                    character.AIController.SteeringManager.SteeringManual(deltaTime, escapeVel);
                }
                else
                {
                    character.AnimController.TargetDir = escapeVel.X < 0.0f ? Direction.Right : Direction.Left;
                    character.AIController.SteeringManager.Reset();
                }
            }
            else
            {
                objectiveManager.GetObjective<AIObjectiveIdle>().Wander(deltaTime);
            }
        }

        public enum HullSearchStatus
        {
            Running,
            Finished
        }

        private readonly List<Hull> hulls = new List<Hull>();
        private int hullSearchIndex = -1;
        float bestHullValue = 0;
        bool bestHullIsAirlock = false;
        Hull potentialBestHull;
        
#if DEBUG
        private readonly Stopwatch stopWatch = new Stopwatch();
#endif

        /// <summary>
        /// Tries to find the best (safe, nearby) hull the character can find a path to.
        /// Checks one hull at a time, and returns HullSearchStatus.Finished when all potential hulls have been checked.
        /// </summary>
        public HullSearchStatus FindBestHull(out Hull bestHull, IEnumerable<Hull> ignoredHulls = null, bool allowChangingSubmarine = true)
        {
            if (hullSearchIndex == -1)
            {
                bestHullValue = 0;
                potentialBestHull = null;
                bestHullIsAirlock = false;
                hulls.Clear();
                var connectedSubs = character.Submarine?.GetConnectedSubs();
#if DEBUG
                stopWatch.Restart();
#endif
                foreach (Hull hull in Hull.HullList)
                {
                    if (hull.Submarine == null) { continue; }
                    // Ruins are mazes filled with water. There's no safe hulls and we don't want to use the resources on it.
                    if (hull.Submarine.Info.IsRuin) { continue; }
                    if (!allowChangingSubmarine && hull.Submarine != character.Submarine) { continue; }
                    if (hull.Rect.Height < ConvertUnits.ToDisplayUnits(character.AnimController.ColliderHeightFromFloor) * 2) { continue; }
                    if (ignoredHulls != null && ignoredHulls.Contains(hull)) { continue; }
                    if (HumanAIController.UnreachableHulls.Contains(hull)) { continue; }
                    if (connectedSubs != null && !connectedSubs.Contains(hull.Submarine)) { continue; }
                    if (hulls.None())
                    {
                        hulls.Add(hull);
                    }
                    else
                    {
                        //sort the hulls first based on distance and a rough suitability estimation
                        //tends to make the method much faster, because we find a potential hull earlier and can discard further-away hulls more easily
                        //(for instance, an NPC in an outpost might otherwise go through all the hulls in the main sub first and do tons of expensive
                        //path calculations, only to discard all of them when going through the hulls in the outpost)
                        bool addLast = true;
                        float hullSuitability = EstimateHullSuitability(hull);
                        for (int i = 0; i < hulls.Count; i++)
                        {
                            Hull otherHull = hulls[i];
                            float otherHullSuitability = EstimateHullSuitability(otherHull);
                            if (hullSuitability > otherHullSuitability)
                            {
                                hulls.Insert(i, hull);
                                addLast = false;
                                break;
                            }
                        }
                        if (addLast)
                        {
                            hulls.Add(hull);
                        }
                    }
                    
                    float EstimateHullSuitability(Hull h)
                    {
                        float distX = Math.Abs(h.WorldPosition.X - character.WorldPosition.X);
                        float distY = Math.Abs(h.WorldPosition.Y - character.WorldPosition.Y);
                        if (character.CurrentHull != null)
                        {
                            distY *= 3;
                        }
                        float dist = distX + distY;
                        float suitability = -dist;
                        const float suitabilityReduction = 10000.0f;
                        if (h.Submarine != character.Submarine)
                        {
                            suitability -= suitabilityReduction;
                        }
                        if (character.CurrentHull != null)
                        {
                            if (h.AvoidStaying)
                            {
                                suitability -= suitabilityReduction;
                            }
                            if (HumanAIController.UnsafeHulls.Contains(h))
                            {
                                suitability -= suitabilityReduction;
                            }
                            if (HumanAIController.NeedsDivingGear(h, out _))
                            {
                                suitability -= suitabilityReduction;
                            }
                        }
                        return suitability;
                    }
                }
                if (hulls.None())
                {
                    bestHull = null;
                    return HullSearchStatus.Finished;
                }
                hullSearchIndex = 0;
#if DEBUG
                stopWatch.Stop();
                DebugConsole.Log($"({character.DisplayName}) Sorted hulls by suitability in {stopWatch.ElapsedMilliseconds} ms");
#endif
            }

            Hull potentialHull = hulls[hullSearchIndex];

            float hullSafety = 0;
            bool hullIsAirlock = false;
            bool isCharacterInside = character.CurrentHull != null && character.Submarine != null;
            if (isCharacterInside)
            {
                hullSafety = HumanAIController.GetHullSafety(potentialHull, potentialHull.GetConnectedHulls(true, 1), character);
                float distanceFactor = GetDistanceFactor(potentialHull.WorldPosition, factorAtMaxDistance: 0.9f);
                hullSafety *= distanceFactor;
                //skip the hull if the safety is already less than the best hull
                //(no need to do the expensive pathfinding if we already know we're not going to choose this hull)
                if (hullSafety > bestHullValue) 
                { 
                    //avoid airlock modules if not allowed to change the sub
                    if (allowChangingSubmarine || potentialHull.OutpostModuleTags.All(t => t != "airlock"))
                    {
                        // Don't allow to go outside if not already outside.
                        var path = PathSteering.PathFinder.FindPath(character.SimPosition, character.GetRelativeSimPosition(potentialHull), character.Submarine, nodeFilter: node => node.Waypoint.CurrentHull != null);
                        if (path.Unreachable)
                        {
                            hullSafety = 0;
                            HumanAIController.UnreachableHulls.Add(potentialHull);
                        }
                        else
                        {
                            // Check the path safety. Each unsafe node reduces the hull safety value.
                            Hull previousHull = null;
                            foreach (WayPoint node in path.Nodes)
                            {
                                Hull hull = node.CurrentHull;
                                if (hull == previousHull)
                                {
                                    // Let's evaluate each hull only once. If we'd want to make this foolproof, we'd have to add the checked hulls to a list,
                                    // yet in practice there shouldn't be a case where the path would get back to a hull once it has exited it.
                                    continue;
                                }
                                previousHull = hull;
                                if (hull == character.CurrentHull)
                                {
                                    // Ignore the current hull, because otherwise we couldn't find a path out.
                                    continue;
                                }
                                if (HumanAIController.UnsafeHulls.Contains(hull))
                                {
                                    // Compare safety of the node hull to the current hull safety.
                                    float nodeHullSafety = HumanAIController.GetHullSafety(hull, hull.GetConnectedHulls(true, 1), character);
                                    if (nodeHullSafety < HumanAIController.HULL_SAFETY_THRESHOLD && nodeHullSafety < HumanAIController.CurrentHullSafety)
                                    {
                                        // If the node hull is considered unsafe and less safe than the current hull, let's ignore the target.
                                        hullSafety = 0;
                                        break;
                                    }
                                    else
                                    {
                                        // Otherwise, each unsafe hull on the path reduces the safety of the target hull by 50% of their threat value.
                                        float hullThreat = 100 - nodeHullSafety;
                                        hullSafety -= hullThreat / 2;
                                        if (hullSafety <= 0)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                            // If the target is not inside a friendly submarine, considerably reduce the hull safety.
                            if (!character.Submarine.IsEntityFoundOnThisSub(potentialHull, includingConnectedSubs: true))
                            {
                                hullSafety /= 10;
                            }
                        }
                    }
                    else
                    {
                        hullSafety = 0;
                    }
                }
            }
            else
            {
                if (potentialHull.IsAirlock)
                {
                    hullSafety = 100;
                    hullIsAirlock = true;
                }
                else if (!bestHullIsAirlock && potentialHull.LeadsOutside(character))
                {
                    hullSafety = 100;
                }
                float characterY = character.CurrentHull?.WorldPosition.Y ?? character.WorldPosition.Y;
                // Huge preference for closer targets
                float distanceFactor = GetDistanceFactor(new Vector2(character.WorldPosition.X, characterY), potentialHull.WorldPosition, factorAtMaxDistance: 0.2f);
                hullSafety *= distanceFactor;
                // If the target is not inside a friendly submarine, considerably reduce the hull safety.
                // Intentionally exclude wrecks from this check
                if (potentialHull.Submarine.TeamID != character.TeamID && potentialHull.Submarine.TeamID != CharacterTeamType.FriendlyNPC)
                {
                    hullSafety /= 10;
                }
            }
            if (hullSafety > bestHullValue || (!isCharacterInside && hullIsAirlock && !bestHullIsAirlock))
            {
                potentialBestHull = potentialHull;
                bestHullValue = hullSafety;
                bestHullIsAirlock = hullIsAirlock;
            }

            bestHull = potentialBestHull;
            hullSearchIndex++;

            if (hullSearchIndex >= hulls.Count)
            {
                hullSearchIndex = -1;
                return HullSearchStatus.Finished;
            }
            return HullSearchStatus.Running;
        }

        public override void Reset()
        {
            base.Reset();
            goToObjective = null;
            divingGearObjective = null;
            currentSafeHull = null;
            previousSafeHull = null;
            retryCounter = 0;
            cannotFindDivingGear = false;
            cannotFindSafeHull = false;
        }

        private bool NeedMoreDivingGear(Hull targetHull, float minOxygen = 0)
        {
            if (!HumanAIController.NeedsDivingGear(targetHull, out bool needsSuit)) { return false; }
            if (needsSuit)
            {
                return !HumanAIController.HasDivingSuit(character, minOxygen);
            }
            else
            {
                return !HumanAIController.HasDivingGear(character, minOxygen);
            }
        }
    }
}
