﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Server;
using Vintagestory.ServerMods;

namespace WorldGenTests
{
    public class FractalNoise : NormalizedSimplexNoise
    {
        readonly double[] mat = new double[]
        {
                1.6,  1.2,
                -1.2,  1.6
        };

        public FractalNoise(double[] inputAmplitudes, double[] frequencies, long seed) : base(inputAmplitudes, frequencies, seed)
        {
        }

        public static new FractalNoise FromDefaultOctaves(int quantityOctaves, double baseFrequency, double persistence, long seed)
        {
            double[] frequencies = new double[quantityOctaves];
            double[] amplitudes = new double[quantityOctaves];

            for (int i = 0; i < quantityOctaves; i++)
            {
                frequencies[i] = Math.Pow(2, i) * baseFrequency;
                amplitudes[i] = Math.Pow(persistence, i);
            }

            return new FractalNoise(amplitudes, frequencies, seed);
        }

        public new virtual double Noise(double x, double y, double[] amplitudes = null)
        {
            double ox = x, oy = y;

            double f = 0.5000 * (amplitudes == null ? base.Noise(x, y) : base.Noise(x, y, amplitudes));
            x = mat[0] * ox + mat[1] * oy;
            y = mat[2] * ox + mat[3] * oy;
            ox = x;
            oy = y;

            f += 0.2500 * (amplitudes == null ? base.Noise(x, y) : base.Noise(x, y, amplitudes));
            x = mat[0] * ox + mat[1] * oy;
            y = mat[2] * ox + mat[3] * oy;
            ox = x;
            oy = y;

            f += 0.1250 * (amplitudes == null ? base.Noise(x, y) : base.Noise(x, y, amplitudes));
            x = mat[0] * ox + mat[1] * oy;
            y = mat[2] * ox + mat[3] * oy;

            f += 0.0625 * (amplitudes == null ? base.Noise(x, y) : base.Noise(x, y, amplitudes));

            return f;
        }
    }

    public class MapLayerOreVeins : MapLayerBase
    {
        double cullTest;

        private FractalNoise noisegenA, noisegenR, noisegenG, noisegenB;
        private double ridgedMul;
        private double[] thresholds;

        Type mblurT = AccessTools.GetTypesFromAssembly(typeof(MapLayerBase).Assembly).Where(t => t.Name == "MapLayerBlur").Single();
        object mblurInst;

        public MapLayerOreVeins(long seed, int octaves, float persistence, int scaleA, int scaleR, int scaleG, int scaleB, double ridgedMul = 1.0, double cullTest = 0.8) : base(seed)
        {
            this.ridgedMul = ridgedMul;
            this.cullTest = cullTest;

            noisegenA = FractalNoise.FromDefaultOctaves(octaves, 1f / scaleA, persistence, seed + 7312654);
            noisegenR = FractalNoise.FromDefaultOctaves(octaves, 1f / scaleR, persistence, seed + 5498987);
            noisegenG = FractalNoise.FromDefaultOctaves(octaves, 1f / scaleG, persistence, seed + 2987992);
            noisegenB = FractalNoise.FromDefaultOctaves(octaves, 1f / scaleB, persistence, seed + 4987462);
            mblurInst = AccessTools.CreateInstance(mblurT);
        }

        public MapLayerOreVeins(long seed, int octaves, float persistence, int scale, int scaleA, int scaleR, int scaleG, int scaleB, double[] thresholds, double ridgedMul = 1.0, double cullTest = 0.8) : base(seed)
        {
            this.ridgedMul = ridgedMul;
            this.cullTest = cullTest;

            noisegenA = FractalNoise.FromDefaultOctaves(octaves, 1f / scaleA, persistence, seed + 7312654);
            noisegenR = FractalNoise.FromDefaultOctaves(octaves, 1f / scaleR, persistence, seed + 5498987);
            noisegenG = FractalNoise.FromDefaultOctaves(octaves, 1f / scaleG, persistence, seed + 2987992);
            noisegenB = FractalNoise.FromDefaultOctaves(octaves, 1f / scaleB, persistence, seed + 4987462);
            this.thresholds = thresholds;
            mblurInst = AccessTools.CreateInstance(mblurT);
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            int[] outData = new int[sizeX * sizeZ];

            int flags = 0b1010001;

            if (thresholds != null)
            {
                for (int z = 0; z < sizeZ; ++z)
                {
                    for (int x = 0; x < sizeX; ++x)
                    {
                        outData[z * sizeX + x] = GetRGBANoise(xCoord, x, zCoord, z, flags, thresholds);
                    }
                }
            }
            else
            {
                for (int z = 0; z < sizeZ; ++z)
                {
                    for (int x = 0; x < sizeX; ++x)
                    {
                        outData[z * sizeX + x] = GetRGBANoise(xCoord, x, zCoord, z, flags, thresholds);
                    }
                }
            }

            return outData;
        }

        public int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ, double[] thresholds)
        {
            int[] outData = new int[sizeX * sizeZ];

            for (int z = 0; z < sizeZ; ++z)
            {
                for (int x = 0; x < sizeX; ++x)
                {
                    int flags = 0b10001;
                    outData[z * sizeX + x] = GetRGBANoise(xCoord, x, zCoord, z, flags, thresholds);
                }
            }

            return outData;
        }

        public int[] BoxBlur(int[] data, int range, int sizeX, int sizeZ)
        {
            mblurInst.CallMethod("BoxBlurHorizontal", data, range, 0, 0, sizeX, sizeZ);
            mblurInst.CallMethod("BoxBlurVertical", data, range, 0, 0, sizeX, sizeZ);
            return data;
        }

        public virtual int[] GenLayerDiffuse(int xCoord, int zCoord, int smallSize, int largeSize, int flags, int diffusionSize, int blurSize, int maxTries = 8, int padding = 2)
        {
            int step = largeSize / smallSize / 2;
            int paddedSize = largeSize + (padding * 2 * step);

            int[] largeData = new int[largeSize * largeSize];

            int[] paddedData = GenLayerSized(xCoord - (padding * step), zCoord - (padding * step), smallSize, paddedSize, flags);
            int[] diffusedData = (int[])paddedData.Clone();

            //diffusion
            for (int z = 0; z < paddedSize; ++z)
            {
                for (int x = 0; x < paddedSize; ++x)
                {
                    int sample = 0;

                    for (int i = 0; sample == 0 && i < maxTries; i++)
                    {
                        int rz = GameMath.oaatHash(x, i + 0, z) % diffusionSize;
                        int rx = GameMath.oaatHash(x, i + 0, z) % diffusionSize;

                        rz -= diffusionSize / 2;
                        rx -= diffusionSize / 2;

                        rz += z;
                        rx += x;

                        rz = GameMath.Clamp(rz, 0, paddedSize - 1);
                        rx = GameMath.Clamp(rx, 0, paddedSize - 1);
                        sample = paddedData[rz * paddedSize + rx];
                    }

                    diffusedData[z * paddedSize + x] = sample;
                }
            }

            //blur
            if (blurSize > 0) BoxBlur(diffusedData, blurSize, paddedSize, paddedSize);

            //crop
            IntDataMap2D data = new IntDataMap2D()
            {
                BottomRightPadding = padding * 2 * step,
                TopLeftPadding = padding * 2 * step,
                Data = diffusedData,
                Size = paddedSize
            };

            for (int z = 0; z < largeSize; ++z)
            {
                for (int x = 0; x < largeSize; ++x)
                {
                    largeData[z * largeSize + x] = data.GetUnpaddedInt(x, z);
                }
            }

            return largeData;
        }

        public virtual int[] GenLayerSized(int xCoord, int zCoord, int smallSize, int largeSize, int flags)
        {
            int[] smallData = GenLayerStepped(xCoord, zCoord, smallSize, smallSize, largeSize, largeSize, flags);
            int[] largeData = new int[largeSize * largeSize];

            for (int z = 0; z < largeSize; ++z)
            {
                for (int x = 0; x < largeSize; ++x)
                {
                    int pX = (int)((float)x / largeSize * smallSize);
                    int pZ = (int)((float)z / largeSize * smallSize);

                    largeData[z * largeSize + x] = smallData[pZ * smallSize + pX];
                }
            }

            return largeData;
        }

        public virtual int[] GenLayerStepped(int xCoord, int zCoord, int smallX, int smallZ, int sizeX, int sizeZ, int flags)
        {
            int[] outData = new int[smallX * smallZ];

            int? li = null;
            for (int z = 0; z < sizeZ; ++z)
            {
                for (int x = 0; x < sizeX; ++x)
                {
                    int lx = (int)((float)x / sizeX * smallX);
                    int lz = (int)((float)z / sizeZ * smallZ);

                    int li2 = lz * smallX + lx;

                    if (li2 == li) continue;

                    li = li2;

                    outData[li ?? 0] = GetRGBANoise(xCoord, x, zCoord, z, flags, thresholds);
                }
            }

            return outData;
        }

        public int GetRGBANoise(int xCoord, int x, int zCoord, int z, int flags = 0, double[] thresholds = null)
        {
            bool inverse = (flags & 0b10000) > 0;

            double nR, nG, nB, nA;

            double nRX = xCoord + x;
            double nRZ = zCoord + z;
            double nGX = xCoord + x;
            double nGZ = zCoord + z;
            double nBX = xCoord + x;
            double nBZ = zCoord + z;
            double nAX = xCoord + x;
            double nAZ = zCoord + z;
            
            int onCol = flags >> 5;

            switch (onCol)
            {
                case 1:
                    nA = noisegenA.Noise(nAX, nAZ, thresholds);
                    if ((flags & 0b01000) > 0) nA = Math.Abs((nA - 0.5) * 2) * ridgedMul;
                    if (nA < cullTest)
                    {
                        nR = noisegenR.Noise(nRX, nRZ, thresholds);
                        nG = noisegenG.Noise(nGX, nGZ, thresholds);
                        nB = noisegenB.Noise(nBX, nBZ, thresholds);
                        if ((flags & 0b00001) > 0) nR = Math.Abs((nR - 0.5) * 2) * ridgedMul;
                        if ((flags & 0b00010) > 0) nG = Math.Abs((nG - 0.5) * 2) * ridgedMul;
                        if ((flags & 0b00100) > 0) nB = Math.Abs((nB - 0.5) * 2) * ridgedMul;
                    }
                    else nA = nR = nG = nB = inverse ? 1 : 0;
                    break;
                case 2:
                    nR = noisegenR.Noise(nRX, nRZ, thresholds);
                    if ((flags & 0b00001) > 0) nR = Math.Abs((nR - 0.5) * 2) * ridgedMul;
                    if (nR < cullTest)
                    {
                        nA = noisegenA.Noise(nAX, nAZ, thresholds);
                        nG = noisegenG.Noise(nGX, nGZ, thresholds);
                        nB = noisegenB.Noise(nBX, nBZ, thresholds);
                        if ((flags & 0b01000) > 0) nA = Math.Abs((nA - 0.5) * 2) * ridgedMul;
                        if ((flags & 0b00001) > 0) nR = Math.Abs((nR - 0.5) * 2) * ridgedMul;
                        if ((flags & 0b00100) > 0) nB = Math.Abs((nB - 0.5) * 2) * ridgedMul;
                    }
                    else nA = nR = nG = nB = inverse ? 1 : 0;
                    break;
                case 3:
                    nG = noisegenG.Noise(nGX, nGZ, thresholds);
                    if ((flags & 0b00010) > 0) nG = Math.Abs((nG - 0.5) * 2) * ridgedMul;
                    if (nG < cullTest)
                    {
                        nA = noisegenA.Noise(nAX, nAZ, thresholds);
                        nR = noisegenR.Noise(nRX, nRZ, thresholds);
                        nB = noisegenB.Noise(nBX, nBZ, thresholds);
                        if ((flags & 0b01000) > 0) nA = Math.Abs((nA - 0.5) * 2) * ridgedMul;
                        if ((flags & 0b00001) > 0) nR = Math.Abs((nR - 0.5) * 2) * ridgedMul;
                        if ((flags & 0b00100) > 0) nB = Math.Abs((nB - 0.5) * 2) * ridgedMul;
                    }
                    else nA = nR = nG = nB = inverse ? 1 : 0;
                    break;
                case 4:
                    nB = noisegenB.Noise(nBX, nBZ, thresholds);
                    if ((flags & 0b00100) > 0) nB = Math.Abs((nB - 0.5) * 2) * ridgedMul;
                    if (nB < cullTest)
                    {
                        nA = noisegenA.Noise(nAX, nAZ, thresholds);
                        nR = noisegenR.Noise(nRX, nRZ, thresholds);
                        nG = noisegenG.Noise(nGX, nGZ, thresholds);
                        if ((flags & 0b01000) > 0) nA = Math.Abs((nA - 0.5) * 2) * ridgedMul;
                        if ((flags & 0b00001) > 0) nR = Math.Abs((nR - 0.5) * 2) * ridgedMul;
                        if ((flags & 0b00010) > 0) nG = Math.Abs((nG - 0.5) * 2) * ridgedMul;
                    }
                    else nA = nR = nG = nB = inverse ? 1 : 0;
                    break;
                default:
                    nA = noisegenA.Noise(nAX, nAZ, thresholds);
                    nR = noisegenR.Noise(nRX, nRZ, thresholds);
                    nG = noisegenG.Noise(nGX, nGZ, thresholds);
                    nB = noisegenB.Noise(nBX, nBZ, thresholds);
                    if ((flags & 0b01000) > 0) nA = Math.Abs((nA - 0.5) * 2) * ridgedMul;
                    if ((flags & 0b00001) > 0) nR = Math.Abs((nR - 0.5) * 2) * ridgedMul;
                    if ((flags & 0b00010) > 0) nG = Math.Abs((nG - 0.5) * 2) * ridgedMul;
                    if ((flags & 0b00100) > 0) nB = Math.Abs((nB - 0.5) * 2) * ridgedMul;
                    break;
            }

            Argb8 argb = new Argb8((byte)(nA * 255), (byte)(nR * 255), (byte)(nG * 255), (byte)(nB * 255));

            return inverse ? argb.Inverse : argb.Value;
        }
    }
}