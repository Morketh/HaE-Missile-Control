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
    partial class Program : MyGridProgram
    {
        public const string guidanceComputerTag = "[GuidanceComputer]";
        public const string attachmentPointTag = "[Attachment]";

        public List<DeployPair> missileDeployPairs = new List<DeployPair>();
        
        public struct DeployPair
        {
            public IMyProgrammableBlock guidanceComputer;
            public IMyMotorStator attachmentPoint;
        }
        

        public Program()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch (argument)
            {
                case "DeploySingleMissile":
                    FetchBlocks();
                    DeployMissile();
                    break;
            }
        }

        List<IMyProgrammableBlock> programmableBlocks = new List<IMyProgrammableBlock>();
        public void FetchBlocks()
        {
            missileDeployPairs.Clear();

            var attachmentpoints = new List<IMyMotorStator>();

            GridTerminalSystem.GetBlocksOfType(attachmentpoints, x => x.CustomName.Contains(attachmentPointTag) && x.TopGrid != null);

            foreach (var attachmentPoint in attachmentpoints)
            {
                var rotorTopGrid = attachmentPoint.TopGrid;

                programmableBlocks.Clear();
                GridTerminalSystem.GetBlocksOfType(programmableBlocks, x => x.CubeGrid == rotorTopGrid && x.CustomName.Contains(guidanceComputerTag));
                if (programmableBlocks.Count > 0)
                {
                    DeployPair deployPair;
                    deployPair.guidanceComputer = programmableBlocks[0];
                    deployPair.attachmentPoint = attachmentPoint;

                    missileDeployPairs.Add(deployPair);
                }
                
            }
        }

        public void DeployMissile()
        {
            int deployPairCount = missileDeployPairs.Count;

            if (deployPairCount > 0)
            {
                DeployPair pair = missileDeployPairs[deployPairCount - 1];

                if (pair.guidanceComputer.TryRun("TurretControll"))
                {
                    pair.attachmentPoint.Detach();

                    missileDeployPairs.RemoveAt(deployPairCount - 1);
                }
            }
        }
    }
}