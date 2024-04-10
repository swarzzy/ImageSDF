#if !IMAGE_SDF_BURST_INSTALLED || !IMAGE_SDF_MATHEMATICS_INSTALLED
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace ImgSDF.Editor
{
    public class SdfGenerator
    {
        public static NativeArray<byte> GenerateDistanceField(NativeArray<byte> inImage, int inWidth, int inHeight, float spread, int downscale, out int outWidth, out int outHeight)
        {
            spread = Mathf.Min(spread, 127);

            outWidth = inWidth / downscale;
            outHeight = inHeight / downscale;
            int delta = (int)Math.Ceiling(spread);

            var arraySize = outWidth * outHeight;
            var xTempArraySize = outWidth * inHeight;
            var yTempArraySize = inWidth * outHeight;

            using var tempBufferX = new NativeArray<byte>(xTempArraySize, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using var tempBufferY = new NativeArray<byte>(yTempArraySize, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            var outImage = new NativeArray<byte>(arraySize, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            var horzPrepassJob = new HorizontalPassJob();
            horzPrepassJob.outImage = tempBufferX;
            horzPrepassJob.inImage = inImage;
            horzPrepassJob.downscale = downscale;
            horzPrepassJob.spread = spread;
            horzPrepassJob.outPitch = outWidth;
            horzPrepassJob.inPitch = inWidth;
            horzPrepassJob.delta = delta;

            horzPrepassJob.Schedule(xTempArraySize, 1).Complete();

            var vertPrepassJob = new VerticalPassJob();
            vertPrepassJob.outImage = tempBufferY;
            vertPrepassJob.inImage = inImage;
            vertPrepassJob.downscale = downscale;
            vertPrepassJob.spread = spread;
            vertPrepassJob.pitch = inWidth;
            vertPrepassJob.delta = delta;

            vertPrepassJob.Schedule(yTempArraySize, 1).Complete();

            var mergeJob = new MergeDistancesJob();
            mergeJob.outImage = outImage;
            mergeJob.xPrecomp = tempBufferX;
            mergeJob.yPrecomp = tempBufferY;
            mergeJob.originalImage = inImage;

            mergeJob.downscale = downscale;
            mergeJob.spread = spread;
            mergeJob.outPitch = outWidth;
            mergeJob.inWidthX = outWidth;
            mergeJob.inHeightX = inHeight;
            mergeJob.inWidthY = inWidth;
            mergeJob.delta = delta;

            mergeJob.Schedule(arraySize, 1).Complete();

            return outImage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Mad(float a, float b, float c)
        {
            return a * b + c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Mad(int a, int b, int c)
        {
            return a * b + c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Max(float a, float b)
        {
            return a > b ? a : b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Min(float a, float b)
        {
            return a < b ? a : b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Max(int a, int b)
        {
            return a > b ? a : b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Min(int a, int b)
        {
            return a < b ? a : b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Clamp(int a, int min, int max)
        {
            return Min(max, Max(min, a));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Clamp(float a, float min, float max)
        {
            return Min(max, Max(min, a));
        }

        private static int Abs(int a)
        {
            return a < 0 ? -a : a;
        }

        private struct MergeDistancesJob : IJobParallelFor
        {
            [WriteOnly]
            public NativeArray<byte> outImage;

            [ReadOnly]
            public NativeArray<byte> originalImage;

            [ReadOnly]
            public NativeArray<byte> xPrecomp;

            [ReadOnly]
            public NativeArray<byte> yPrecomp;

            public int downscale;
            public int outPitch;

            public int inWidthX;
            public int inHeightX;
            public int inWidthY;

            public int delta;
            public float spread;

            public void Execute(int index)
            {
                var yy = index / outPitch;
                var xx = index % outPitch;

                var centerXx = Mad(xx, 1, (1 / 2));
                var centerYx = Mad(yy, downscale, (downscale / 2));
                var centerXy = Mad(xx, downscale, (downscale / 2));
                var centerYy = Mad(yy, 1, (1 / 2));

                var b = originalImage[Mad(centerYx, inWidthY, centerXy)];

                var bx = xPrecomp[Mad(centerYx, inWidthX, centerXx)];
                var by = yPrecomp[Mad(centerYy, inWidthY, centerXy)];

                var startY = Max(0, centerYx - delta);
                var endY = Min(inHeightX - 1, centerYx + delta);

                var startX = Max(0, centerXy - delta);
                var endX = Min(inWidthY - 1, centerXy + delta);

                int closestSquareDist = int.MaxValue;

                for (int y = startY; y <= endY; ++y)
                {
                    var d = xPrecomp[Mad(y, inWidthX, centerXx)];

                    int dy = centerYx - y;
                    int squareDist = dy * dy + d * d;

                    if (squareDist < closestSquareDist)
                    {
                        closestSquareDist = squareDist;
                    }
                }

                for (int x = startX; x <= endX; ++x)
                {
                    var d = yPrecomp[Mad(centerYy, inWidthY, x)];

                    int dx = centerXy - x;

                    int squareDist = dx * dx + d * d;

                    if (squareDist < closestSquareDist)
                    {
                        closestSquareDist = squareDist;
                    }
                }

                float sign = b != 0 ? 1.0f : -1.0f;
                float closestDist = (float)Mathf.Sqrt(closestSquareDist);
                closestDist = sign * Min(closestDist, spread);
                float norm = Mad((closestDist / spread), 0.5f, 0.5f);
                norm = Clamp(norm, 0.0f, 1.0f);
                outImage[index] = (byte)(norm * 255.0f);
            }
        }

        private struct HorizontalPassJob : IJobParallelFor
        {
            [WriteOnly]
            public NativeArray<byte> outImage;

            [ReadOnly]
            public NativeArray<byte> inImage;

            public int downscale;
            public int outPitch;
            public int inPitch;
            public int delta;
            public float spread;

            public void Execute(int index)
            {
                var centerX = Mad(index % outPitch, downscale, (downscale / 2));
                var centerY = index / outPitch;

                var b = inImage[Mad(centerY, inPitch, centerX)];

                var startX = centerX - delta;
                var endX = centerX + delta;

                byte closestDist = byte.MaxValue;

                for (int x = startX; x <= endX; ++x)
                {
                    int i = Clamp(Mad(centerY, inPitch, x), 0, inImage.Length - 1);
                    if (b != inImage[i])
                    {
                        byte dist = (byte)Min(Abs(centerX - x), 255);

                        if (dist < closestDist)
                        {
                            closestDist = dist;
                        }
                    }
                }

                outImage[index] = closestDist;
            }
        }

        private struct VerticalPassJob : IJobParallelFor
        {
            [WriteOnly]
            public NativeArray<byte> outImage;

            [ReadOnly]
            public NativeArray<byte> inImage;

            public int downscale;
            public int pitch;
            public int delta;
            public float spread;

            public void Execute(int index)
            {
                var centerX = index % pitch;
                var centerY = Mad(index / pitch, downscale, (downscale / 2));

                var b = inImage[Mad(centerY, pitch, centerX)];

                var startY = centerY - delta;
                var endY = centerY + delta;

                byte closestDist = byte.MaxValue;

                for (int y = startY; y <= endY; ++y)
                {
                    int i = Clamp(Mad(y, pitch, centerX), 0, inImage.Length - 1);
                    if (b != inImage[i])
                    {
                        byte dist = (byte)Min(Abs(centerY - y), 255);

                        if (dist < closestDist)
                        {
                            closestDist = dist;
                        }
                    }
                }

                outImage[index] = closestDist;
            }
        }
    }
}
#endif

// Unoptimized version left here for reference.
/*
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Generates sdf from png images.
/// </summary>
public class SdfGenerator
{
    /// <summary>
    /// Generate byte array with sdf from byte array with png.
    /// </summary>
    public static NativeArray<byte> GenerateDistanceField(NativeArray<byte> inImage, int inWidth, int inHeight, float spread, int downscale, out int outWidth, out int outHeight)
    {
        outWidth = inWidth / downscale;
        outHeight = inHeight / downscale;
        var arraySize = outWidth * outHeight;

        var outImage = new NativeArray<byte>(arraySize, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int delta = (int)Math.Ceiling(spread);

        FindSignedDistanceJob job = new FindSignedDistanceJob();

        job.outImage = outImage;
        job.inImage = inImage;
        job.downscale = downscale;
        job.spread = spread;
        job.outWidth = outWidth;
        job.outHeight = outHeight;
        job.inWidth = inWidth;
        job.inHeight = inHeight;
        job.delta = delta;

        var handle = job.Schedule(arraySize, 1);
        handle.Complete();

        return outImage;
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    private static byte FindSignedDistance(int centerX, int centerY, NativeSlice<byte> bitmap, int width, int height, int delta, float spread)
    {
        var b = bitmap[Mad(centerY, width, centerX)];

        var startX = Max(0, centerX - delta);
        var endX = Min(width - 1, centerX + delta);
        var startY = Max(0, centerY - delta);
        var endY = Min(height - 1, centerY + delta);

        int closestSquareDist = delta * delta;

        for (int y = startY; y <= endY; ++y)
        {
            for (int x = startX; x <= endX; ++x)
            {
                if (b != bitmap[Mad(y, width, x)])
                {
                    int dx = centerX - x;
                    int dy = centerY - y;
                    int squareDist = Mad(dx, dx, dy * dy);

                    if (squareDist < closestSquareDist)
                    {
                        closestSquareDist = squareDist;
                    }
                }
            }
        }

        float closestDist = (float)math.sqrt(closestSquareDist);
        closestDist = (b != 0 ? 1 : -1) * Min(closestDist, spread);
        float norm = Mad((closestDist / spread), 0.5f, 0.5f);
        norm = math.clamp(norm, 0.0f, 1.0f);
        return (byte)(norm * 255.0f);
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    private struct FindSignedDistanceJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray<byte> outImage;

        private int x;
        private int y;
        private int centerX;
        private int centerY;

        public int downscale;
        public int outWidth;
        public int outHeight;
        public int inWidth;
        public int inHeight;
        public int delta;

        [ReadOnly]
        public NativeArray<byte> inImage;

        public float spread;

        public void Execute(int index)
        {
            y = index / outWidth;
            x = index % outWidth;

            centerX = x * downscale + (downscale / 2);


            centerX = Mad(x, downscale, (downscale / 2));
            centerY = Mad(y, downscale, (downscale / 2));
            outImage[index] = FindSignedDistance(centerX, centerY, inImage, inWidth, inHeight, delta, spread);
        }
    }
}
*/