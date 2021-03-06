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
        public class TurretMonitor
        {
            public Action<MyDetectedEntityInfo> OnTargetDetected;

            private List<IMyLargeTurretBase> turretList;
            private IEnumerator<bool> loadSpreader;

            public TurretMonitor(Program p)
            {
                turretList = new List<IMyLargeTurretBase>();
                p.GridTerminalSystem.GetBlocksOfType(turretList);
            }

            public void Scan()
            {
                foreach (var turret in turretList)
                {
                    if (turret.HasTarget)
                    {
                        OnTargetDetected?.Invoke(turret.GetTargetedEntity());
                    }
                }
            }

            public void SlowScan()
            {
                if(loadSpreader != null)
                {
                    if (!loadSpreader.MoveNext())
                    {
                        loadSpreader.Dispose();
                        loadSpreader = LoadSpreaderScan();
                    }
                }
                else
                {
                    loadSpreader = LoadSpreaderScan();
                }

            }

            public IEnumerator<bool> LoadSpreaderScan()
            {
                while (true)
                {
                    foreach (var turret in turretList)
                    {
                        if (turret.HasTarget)
                        {
                            OnTargetDetected?.Invoke(turret.GetTargetedEntity());
                        }

                        yield return true;
                    }

                    yield return true;
                }
            }
        }
    }
}
