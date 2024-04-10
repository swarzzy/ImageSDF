#if IMAGE_SDF_BURST_INSTALLED && IMAGE_SDF_MATHEMATICS_INSTALLED
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ImgSDF.Editor
{
    public class SdfGenerator
    {
        public static NativeArray<byte> GenerateDistanceField(NativeArray<byte> inImage, int inWidth, int inHeight, float spread, int downscale, out int outWidth, out int outHeight)
        {
            spread = math.min(spread, 127);

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

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
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

                var centerXx = math.mad(xx, 1, (1 / 2));
                var centerYx = math.mad(yy, downscale, (downscale / 2));
                var centerXy = math.mad(xx, downscale, (downscale / 2));
                var centerYy = math.mad(yy, 1, (1 / 2));

                var b = originalImage[math.mad(centerYx, inWidthY, centerXy)];

                var bx = xPrecomp[math.mad(centerYx, inWidthX, centerXx)];
                var by = yPrecomp[math.mad(centerYy, inWidthY, centerXy)];

                var startY = math.max(0, centerYx - delta);
                var endY = math.min(inHeightX - 1, centerYx + delta);

                var startX = math.max(0, centerXy - delta);
                var endX = math.min(inWidthY - 1, centerXy + delta);

                int closestSquareDist = int.MaxValue;

                for (int y = startY; y <= endY; ++y)
                {
                    var d = xPrecomp[math.mad(y, inWidthX, centerXx)];

                    int dy = centerYx - y;
                    int squareDist = dy * dy + d * d;

                    if (squareDist < closestSquareDist)
                    {
                        closestSquareDist = squareDist;
                    }
                }

                for (int x = startX; x <= endX; ++x)
                {
                    var d = yPrecomp[math.mad(centerYy, inWidthY, x)];

                    int dx = centerXy - x;

                    int squareDist = dx * dx + d * d;

                    if (squareDist < closestSquareDist)
                    {
                        closestSquareDist = squareDist;
                    }
                }

                float sign = b != 0 ? 1.0f : -1.0f;
                float closestDist = (float)math.sqrt(closestSquareDist);
                closestDist = sign * math.min(closestDist, spread);
                float norm = math.mad((closestDist / spread), 0.5f, 0.5f);
                norm = math.clamp(norm, 0.0f, 1.0f);
                outImage[index] = (byte)(norm * 255.0f);
            }
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
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
                var centerX = math.mad(index % outPitch, downscale, (downscale / 2));
                var centerY = index / outPitch;

                var b = inImage[math.mad(centerY, inPitch, centerX)];

                var startX = centerX - delta;
                var endX = centerX + delta;

                byte closestDist = byte.MaxValue;

                for (int x = startX; x <= endX; ++x)
                {
                    int i = math.clamp(math.mad(centerY, inPitch, x), 0, inImage.Length - 1);
                    if (b != inImage[i])
                    {
                        byte dist = (byte)math.min(math.abs(centerX - x), 255);

                        if (dist < closestDist)
                        {
                            closestDist = dist;
                        }
                    }
                }

                outImage[index] = closestDist;
            }
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
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
                var centerY = math.mad(index / pitch, downscale, (downscale / 2));

                var b = inImage[math.mad(centerY, pitch, centerX)];

                var startY = centerY - delta;
                var endY = centerY + delta;

                byte closestDist = byte.MaxValue;

                for (int y = startY; y <= endY; ++y)
                {
                    int i = math.clamp(math.mad(y, pitch, centerX), 0, inImage.Length - 1);
                    if (b != inImage[i])
                    {
                        byte dist = (byte)math.min(math.abs(centerY - y), 255);

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