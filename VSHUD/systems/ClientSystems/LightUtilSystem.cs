﻿using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace VSHUD
{
    class LightUtilSystem : ClientSystem
    {
        ICoreClientAPI capi;
        VSHUDConfig config;

        public LightUtilSystem(ClientMain game, VSHUDConfig config) : base(game)
        {
            this.config = config;
            capi = (ICoreClientAPI)game.Api;
        }

        public override string Name => "LightUtil";

        public override EnumClientSystemType GetSystemType() => EnumClientSystemType.Misc;

        public override void OnSeperateThreadGameTick(float dt) => LightHighlight();

        ConcurrentDictionary<BlockPos, int> colors = new ConcurrentDictionary<BlockPos, int>();
        ConcurrentQueue<BlockPos> emptyList = new ConcurrentQueue<BlockPos>();
        
        BlockPos start = new BlockPos();
        BlockPos end = new BlockPos();
        BlockPos dPos = new BlockPos();
        BlockPos cPos = new BlockPos();
        BlockPos uPos = new BlockPos();

        public void LightHighlight(BlockPos pos = null)
        {
            try
            {
                if (!config.LightLevels)
                {
                    ClearLightLevelHighlights();
                    return;
                }

                colors.Clear();

                pos = pos ?? capi.World.Player.Entity.SidedPos.AsBlockPos.UpCopy();
                int rad = config.LightRadius;

                start.X = pos.X - rad;
                start.Y = pos.Y - rad;
                start.Z = pos.Z - rad;

                end.X = pos.X + rad;
                end.Y = pos.Y + rad;
                end.Z = pos.Z + rad;

                capi.World.BlockAccessor.WalkBlocks(start, end, (block, iPos) =>
                {
                    if (block == null || iPos == null) return;
                    
                    dPos.X = pos.X - iPos.X; 
                    dPos.Y = pos.Y - iPos.Y;
                    dPos.Z = pos.Z - iPos.Z;
                    
                    if (!rad.InsideRadius(dPos.X, dPos.Y, dPos.Z)) return;
                    
                    cPos.X = iPos.X;
                    cPos.Y = iPos.Y;
                    cPos.Z = iPos.Z;

                    uPos.X = iPos.X;
                    uPos.Y = iPos.Y + 1;
                    uPos.Z = iPos.Z;

                    BlockEntityFarmland blockEntityFarmland = capi.World.BlockAccessor.GetBlockEntity(iPos) as BlockEntityFarmland;

                    if (blockEntityFarmland == null && config.LUShowAbove) cPos.Y++;

                    int level = capi.World.BlockAccessor.GetLightLevel(cPos, config.LightLevelType);

                    bool rep = config.LUSpawning ? blockEntityFarmland != null || capi.World.BlockAccessor.GetBlock(uPos).IsReplacableBy(block) : true;
                    bool opq = config.LUOpaque ? blockEntityFarmland != null || block.AllSidesOpaque : true;

                    if (block.BlockId != 0 && rep && opq)
                    {
                        int c = 0;

                        float fLevel = level / 32.0f;
                        int alpha = (int)Math.Round(config.LightLevelAlpha * 255);
                        if (config.Nutrients && blockEntityFarmland != null)
                        {
                            int I = config.MXNutrients ? blockEntityFarmland.Nutrients.IndexOf(blockEntityFarmland.Nutrients.Max()) : blockEntityFarmland.Nutrients.IndexOf(blockEntityFarmland.Nutrients.Min());
                            var nuti = blockEntityFarmland.Nutrients[I];
                            int scale = (int)(nuti / 50.0f * 255.0f);
                            switch (I)
                            {
                                case 0:
                                    c = ColorUtil.ToRgba(alpha, 0, 0, scale);
                                    break;
                                case 1:
                                    c = ColorUtil.ToRgba(alpha, 0, scale, 0);
                                    break;
                                case 2:
                                    c = ColorUtil.ToRgba(alpha, scale, 0, 0);
                                    break;
                                default:
                                    break;
                            }
                        }
                        else
                        {
                            c = level > config.LightLevelRed ? ColorUtil.ToRgba(alpha, 0, (int)(fLevel * 255), 0) : ColorUtil.ToRgba(alpha, 0, 0, (int)(Math.Max(fLevel, 0.2) * 255));
                        }

                        colors[iPos.Copy()] = c;
                    }
                });
                if (colors.Count < 1)
                {
                    ClearLightLevelHighlights();
                    return;
                }

                capi.Event.EnqueueMainThreadTask(() => capi.World.HighlightBlocks(capi.World.Player, config.MinLLID, colors.Keys.ToList(), colors.Values.ToList(), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary), "addLU");
            }
            catch (Exception) { }
        }

        public void ClearLightLevelHighlights()
        {
            capi.Event.EnqueueMainThreadTask(() => capi.World.HighlightBlocks(capi.World.Player, config.MinLLID, emptyList.ToList(), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cubes), "removeLU");
        }
    }
}
