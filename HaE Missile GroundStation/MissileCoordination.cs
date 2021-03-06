﻿using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class MissileCoordination
        {
            private int targetTimeOutSec;

            public Action OnSystemOverwhelmed;
            public Action<MissileManagement.MissileInfo, MyDetectedEntityInfo> OnTargetFiredAt;
            public Action<MyDetectedEntityInfo> OnCantFireAtTarget;

            private MissileManagement management;
            private IMyTerminalBlock reference;
            private ACPWrapper antennas;

            private Dictionary<long, MyDetectedEntityInfo> targets;
            private Dictionary<long, DateTime> firedAt;
            private Dictionary<long, MissileManagement.MissileInfo> firedMissiles;

            private List<IEnumerator<bool>> missileStaging;

            private TargetFilter tFilter;

            public MissileCoordination(MissileManagement management, IMyTerminalBlock rc, ACPWrapper antennas, TargetFilter tFilter, int targetTimeOutSec = 15)
            {
                this.management = management;
                this.reference = rc;
                this.antennas = antennas;
                this.targetTimeOutSec = targetTimeOutSec;
                this.tFilter = tFilter;

                targets = new Dictionary<long, MyDetectedEntityInfo>();
                missileStaging = new List<IEnumerator<bool>>();
                firedMissiles = new Dictionary<long, MissileManagement.MissileInfo>();
                firedAt = new Dictionary<long, DateTime>();
            }

            public void Main(UpdateType uType)
            {
                if (targets.Count == 0)
                    return;
                else if (targets.Count > management.SRIMissileCount)
                    OnSystemOverwhelmed?.Invoke();

                if ((uType & UpdateType.Update10) != 0)
                    OnUpdate10();
            }

            private void OnUpdate10()
            {
                MoveNextState();
                CheckTimeoutExpired();
            }

            private void IssueMissileCommands()
            {
                foreach (var targetPair in targets)
                {
                    if (firedAt.ContainsKey(targetPair.Key))
                        return;

                    MyDetectedEntityInfo target = targetPair.Value;
                    Vector3D targetDirection = target.Position - reference.GetPosition();
                    double distance = targetDirection.Length();
                    targetDirection.Normalize();

                    MissileManagement.MissileInfo missile;
                    missile = management.GetMissileCloseToAndInDirection(reference.GetPosition(), targetDirection, MissileManagement.MissileType.SRInterceptor | MissileManagement.MissileType.MRInterceptor, distance / 4, 0, false);

                    if (missile == default(MissileManagement.MissileInfo))
                    {
                        OnCantFireAtTarget?.Invoke(target);
                        continue;
                    }

                    LaunchNewMissile(missile, targetPair.Key);
                    break;
                }
            }

            public void FireMissileInDirection(Vector3D direction, bool activeTurretTracking)
            {
                MissileManagement.MissileInfo missile;
                missile = management.GetMissileCloseToAndInDirection(reference.GetPosition(), direction, MissileManagement.MissileType.SRInterceptor | MissileManagement.MissileType.MRInterceptor, 1000 / 4, 0, false);

                if (missile == default(MissileManagement.MissileInfo))
                    reference.CustomData += "Hi";

                string[] commands = {
                    "LaunchInDirection",
                    direction.ToString()
                };
                management.SendCommand(missile, commands);

                firedMissiles[missile.id] = missile;
                management.RemoveMissile(missile);

                management.SendCommand(missile, "FullTurretGuidance");
            }

            private void LaunchNewMissile(MissileManagement.MissileInfo missile, long targetId)
            {
                IEnumerator<bool> tempSM = MissileSM(missile, targetId);
                firedAt[targetId] = DateTime.Now;
                tempSM.MoveNext();

                missileStaging.Add(tempSM);
            }

            private IEnumerator<bool> MissileSM(MissileManagement.MissileInfo missile, long targetId)
            {
                management.SendCommand(missile, "LaunchOut");
                firedMissiles[missile.id] = missile;
                management.RemoveMissile(missile);
                yield return true;

                for (int i = 0; i < 5; i++)         //Wait 5 ticks to allow missile to get out of tube
                    yield return true;

                var predictedPos = targets[targetId].Position + targets[targetId].Velocity * 0.17f;

                string[] command = {
                    "AttackLoc",
                    predictedPos.ToString()
                };

                management.SendCommand(missile, command);

                if ((missile.missileType & MissileManagement.MissileType.SRInterceptor) != 0)
                    management.SendCommand(missile, "UseTurretLockon");

                OnTargetFiredAt?.Invoke(missile, targets[targetId]);
            }

            private void MoveNextState()
            {
                for (int i = missileStaging.Count -1; i >=0; i--)
                {
                    if (!missileStaging[i].MoveNext())
                    {
                        missileStaging[i].Dispose();
                        missileStaging.RemoveAt(i);
                    }    
                }
            }

            private void CheckTimeoutExpired()
            {
                List<long> deletKeys = new List<long>();

                foreach(var temp in firedAt)
                {
                    if (temp.Value.AddSeconds(targetTimeOutSec) < DateTime.Now)
                        deletKeys.Add(temp.Key);
                }

                foreach(var key in deletKeys)
                {
                    firedAt.Remove(key);
                    targets.Remove(key);
                }
            }

            private bool CheckTargetOnFilter(MyDetectedEntityInfo target)
            {
                double targetSizeSQ = target.BoundingBox.Size.LengthSquared();
                double distanceSQ = Vector3D.DistanceSquared(reference.GetPosition(), target.Position);
                MyDetectedEntityType type = target.Type;

                if (distanceSQ < tFilter.minDistance * tFilter.minDistance)
                    return false;
                if (!tFilter.allowedType.Contains(type))
                    return false;
                return true;
            }

            /*==========| Event callbacks |==========*/
            public void OnTargetDetected(MyDetectedEntityInfo target)
            {
                if (target.IsEmpty())
                    return;

                if (!CheckTargetOnFilter(target))
                    return;

                targets[target.EntityId] = target;

                IssueMissileCommands();
            }

            /*==========| structsAndStuff |==========*/

            public struct TargetFilter
            {
                public double minDistance;
                public MyDetectedEntityType[] allowedType;

                public TargetFilter(double minDistance, MyDetectedEntityType[] allowedType)
                {
                    this.minDistance = minDistance;
                    this.allowedType = allowedType;
                }
            }
        }
    }
}
