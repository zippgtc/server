﻿#region

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using AgentInterface.Api.Models;
using AgentInterface.Api.ScreenShare.DesktopDuplication;
using AgentInterface.Api.Win32;

#endregion

namespace AgentInterface.Api.ScreenShare
{
    public static class ScreenData
    {
        public static int ActiveDisplay = 0;
        public static Bitmap NewBitmap = new Bitmap(1, 1);
        public static Bitmap PrevBitmap;
        private static DesktopDuplicator _desktopDuplicator;
        private static bool _nullFrame;
        private static bool _canUseGpuAcceleration;


        static ScreenData()
        {
        }


        public static int NumByteFullScreen { get; set; } = 1;

        public static byte[] PackScreenCaptureData(Bitmap image, Rectangle bounds)
        {
            byte[] results;
            using (var screenStream = new MemoryStream())
            {
                using (var binaryWriter = new BinaryWriter(screenStream))
                {
                    //write the id of the frame
                    binaryWriter.Write(Guid.NewGuid().ToByteArray());
                    //write the x and y coords of the 
                    binaryWriter.Write(bounds.X);
                    binaryWriter.Write(bounds.Y);
                    //write the rect data
                    binaryWriter.Write(bounds.Top);
                    binaryWriter.Write(bounds.Bottom);
                    binaryWriter.Write(bounds.Left);
                    binaryWriter.Write(bounds.Right);
                    using (var ms = new MemoryStream())
                    {
                        image.Save(ms, ImageFormat.Jpeg);

                        var imgData = ms.ToArray();

                        //write the image
                        binaryWriter.Write(imgData);
                    }
                }
                results = screenStream.ToArray();
            }
            return results;
        }


        private static Rectangle GetBoundingBoxForChanges(ref Bitmap prevBitmap, ref Bitmap newBitmap)
        {
            // The search algorithm starts by looking
            //	for the top and left bounds. The search
            //	starts in the upper-left corner and scans
            //	left to right and then top to bottom. It uses
            //	an adaptive approach on the pixels it
            //	searches. Another pass is looks for the
            //	lower and right bounds. The search starts
            //	in the lower-right corner and scans right
            //	to left and then bottom to top. Again, an
            //	adaptive approach on the search area is used.
            //

            // Notice: The GetPixel member of the Bitmap class
            //	is too slow for this purpose. This is a good
            //	case of using unsafe code to access pointers
            //	to increase the speed.
            //

            // Validate the images are the same shape and type.
            //
            if (prevBitmap.Width != newBitmap.Width ||
                prevBitmap.Height != newBitmap.Height ||
                prevBitmap.PixelFormat != newBitmap.PixelFormat)
            {
                // Not the same shape...can't do the search.
                //
                return Rectangle.Empty;
            }

            // Init the search parameters.
            //
            var width = newBitmap.Width;
            var height = newBitmap.Height;
            var left = width;
            var right = 0;
            var top = height;
            var bottom = 0;

            BitmapData bmNewData = null;
            BitmapData bmPrevData = null;

            try
            {
                // Lock the bits into memory.
                //
                bmNewData = newBitmap.LockBits(
                    new Rectangle(0, 0, newBitmap.Width, newBitmap.Height),
                    ImageLockMode.ReadOnly, newBitmap.PixelFormat);
                bmPrevData = prevBitmap.LockBits(
                    new Rectangle(0, 0, prevBitmap.Width, prevBitmap.Height),
                    ImageLockMode.ReadOnly, prevBitmap.PixelFormat);

                // The images are ARGB (4 bytes)
                //
                const int numBytesPerPixel = 4;

                // Get the number of integers (4 bytes) in each row
                //	of the image.
                //
                var strideNew = bmNewData.Stride/numBytesPerPixel;
                var stridePrev = bmPrevData.Stride/numBytesPerPixel;

                // Get a pointer to the first pixel.
                //
                // Notice: Another speed up implemented is that I don't
                //	need the ARGB elements. I am only trying to detect
                //	change. So this algorithm reads the 4 bytes as an
                //	integer and compares the two numbers.
                //
                var scanNew0 = bmNewData.Scan0;
                var scanPrev0 = bmPrevData.Scan0;

                // Enter the unsafe code.
                //
                unsafe
                {
                    // Cast the safe pointers into unsafe pointers.
                    //

                    var pNew = (int*) scanNew0.ToPointer();
                    var pPrev = (int*) scanPrev0.ToPointer();
                    for (var y = 0; y < newBitmap.Height; ++y)
                    {
                        // For pixels up to the current bound (left to right)
                        //
                        for (var x = 0; x < left; ++x)
                        {
                            // Use pointer arithmetic to index the
                            //	next pixel in this row.
                            //
                            var test1 = (pNew + x)[0];
                            var test2 = (pPrev + x)[0];
                            var b1 = test1 & 0xff;
                            var g1 = (test1 & 0xff00) >> 8;
                            var r1 = (test1 & 0xff0000) >> 16;
                            var a1 = (test1 & 0xff000000) >> 24;

                            var b2 = test2 & 0xff;
                            var g2 = (test2 & 0xff00) >> 8;
                            var r2 = (test2 & 0xff0000) >> 16;
                            var a2 = (test2 & 0xff000000) >> 24;
                            if (b1 != b2 || g1 != g2 || r1 != r2 || a1 != a2)
                            {
                                if (left > x)
                                    left = x;
                                if (top > y)
                                    top = y;
                            }
                        }

                        // Move the pointers to the next row.
                        //
                        pNew += strideNew;
                        pPrev += stridePrev;
                    }

                    pNew = (int*) scanNew0.ToPointer();
                    pPrev = (int*) scanPrev0.ToPointer();
                    pNew += (newBitmap.Height - 1)*strideNew;
                    pPrev += (prevBitmap.Height - 1)*stridePrev;

                    for (var y = newBitmap.Height - 1; y > top; y--)
                    {
                        for (var x = newBitmap.Width - 1; x > left; x--)
                        {
                            var test1 = (pNew + x)[0];
                            var test2 = (pPrev + x)[0];
                            var b1 = test1 & 0xff;
                            var g1 = (test1 & 0xff00) >> 8;
                            var r1 = (test1 & 0xff0000) >> 16;
                            var a1 = (test1 & 0xff000000) >> 24;

                            var b2 = test2 & 0xff;
                            var g2 = (test2 & 0xff00) >> 8;
                            var r2 = (test2 & 0xff0000) >> 16;
                            var a2 = (test2 & 0xff000000) >> 24;
                            if (b1 == b2 && g1 == g2 && r1 == r2 && a1 == a2) continue;
                            if (x > right)
                            {
                                right = x;
                            }
                            if (y > bottom)
                            {
                                bottom = y;
                            }
                        }
                        pNew -= strideNew;
                        pPrev -= stridePrev;
                    }
                }
            }
            catch (Exception)
            {
                // Do something with this info.
            }
            finally
            {
                // Unlock the bits of the image.
                //
                if (bmNewData != null)
                {
                    newBitmap.UnlockBits(bmNewData);
                }
                if (bmPrevData != null)
                {
                    prevBitmap.UnlockBits(bmPrevData);
                }
            }

            // Validate we found a bounding box. If not
            //	return an empty rectangle.
            //
            var diffImgWidth = right - left + 1;
            var diffImgHeight = bottom - top + 1;
            if (diffImgHeight < 0 || diffImgWidth < 0)
            {
                // Nothing changed
                return Rectangle.Empty;
            }

            // Return the bounding box.
            //

            return new Rectangle(left, top, diffImgWidth, diffImgHeight);
        }


        public static Bitmap GetImageFromByteArray(byte[] byteArray)
        {
            Bitmap newBitmap;
            using (var memoryStream = new MemoryStream(byteArray))
            using (var newImage = Image.FromStream(memoryStream))
                newBitmap = new Bitmap(newImage);
            return newBitmap;
        }

        private static DesktopFrame GetScreenPicDxgi()
        {
            DesktopFrame frame = null;
            try
            {
                frame = _desktopDuplicator.GetLatestFrame();
            }
            catch (Exception)
            {
                _desktopDuplicator = new DesktopDuplicator(0);
            }
            return frame;
        }


        public static ScreenModel GetImageChange(Bitmap image)
        {
            var screenModel = new ScreenModel
            {
                Rectangle = Rectangle.Empty,
                ScreenBitmap = null
            };

            NewBitmap = image;
            if (NewBitmap == null)
            {
                return screenModel;
            }
            lock (NewBitmap)
            {
                if (PrevBitmap != null)
                {
                    screenModel.Rectangle = GetBoundingBoxForChanges(ref PrevBitmap, ref NewBitmap);
                    if (screenModel.Rectangle == Rectangle.Empty) return screenModel;
                    // Get the minimum rectangular area
                    //
                    //diff = new Bitmap(bounds.Width, bounds.Height);
                    screenModel.ScreenBitmap = NewBitmap.Clone(screenModel.Rectangle,
                        NewBitmap.PixelFormat);
                    PrevBitmap = NewBitmap;
                }
                else
                {
                    // Create a bounding rectangle.
                    //
                    screenModel.Rectangle = new Rectangle(0, 0, NewBitmap.Width, NewBitmap.Height);

                    // Set the previous bitmap to the current to prepare
                    //	for the next screen capture.
                    //

                    screenModel.ScreenBitmap = NewBitmap.Clone(screenModel.Rectangle,
                        NewBitmap.PixelFormat);
                    PrevBitmap = NewBitmap;
                }
            }
            return screenModel;
        }

        public static FrameInformation DesktopCapture()
        {
            var frameInfo  = new FrameInformation();
            if (_canUseGpuAcceleration)
            {
                frameInfo.UsingGpu = true;
                var desktopFrame = GpuCapture();
                if (desktopFrame != null)
                {
                    frameInfo.FinishedRegions = desktopFrame.FinishedRegions;
                }
            }
            else
            {
                    var screenData = GetImageChange(CaptureDesktop());
                    if (screenData.ScreenBitmap == null || screenData.Rectangle == Rectangle.Empty) return null;
                    frameInfo.Bounds = screenData.Rectangle;
                    frameInfo.ScreenImage = screenData.ScreenBitmap;
                
            }
            return frameInfo;
        }
        private static DesktopFrame GpuCapture()
        {
            var frame = GetScreenPicDxgi();
            if (frame == null) return null;
            if (!_nullFrame) return frame;
            _nullFrame = false;
            return null;
        }


        private static Bitmap CaptureActiveScreen(int screenIndex)
        {
            var screens = Display.DisplayInformation();
            if (screens.Count == 0 || screens.ElementAtOrDefault(screenIndex) == null)
            {
                return CaptureDesktop();
            }
            var screen = screens[screenIndex];
            return CaptureDesktopScreen(screen.CurrentResolution);
        }

        private static Bitmap CaptureDesktopScreen(ResolutionInformation bounds)
        {
            var desktopBmp = new Bitmap(bounds.Width, bounds.Height);
            var g = Graphics.FromImage(desktopBmp);
            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, new Size(bounds.Width, bounds.Height),
                CopyPixelOperation.SourceCopy);
            g.Dispose();
            return desktopBmp;
        }


        public static byte[] ImageToByteArray(Image img)
        {
            using (var stream = new MemoryStream())
            {
                img.Save(stream, ImageFormat.Jpeg);
                return stream.ToArray();
            }
        }

        public static Bitmap CaptureDesktop()
        {
            var desktopBounds = Display.GetWindowRectangle();
            var desktopBmp = new Bitmap(
                desktopBounds.Width,
                desktopBounds.Height);

            var g = Graphics.FromImage(desktopBmp);

            g.CopyFromScreen(0, 0, 0, 0,
                new Size(
                    desktopBounds.Width,
                    desktopBounds.Height));
            g.Dispose();
            return desktopBmp;
        }

        public static void SetupDuplication()
        {
            _canUseGpuAcceleration = false;
            var win8Version = new Version(6, 2, 9200, 0);
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || Environment.OSVersion.Version < win8Version)
            {
                Console.WriteLine("Cannot use GPU for Screen Share");
                return;
            }
            try
            {
                _desktopDuplicator = new DesktopDuplicator(0);
                _nullFrame = true;
                _canUseGpuAcceleration = true;
                Console.WriteLine("desktop duplication setup");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public class ScreenModel : IDisposable
        {
            public Rectangle Rectangle { get; set; }
            public Bitmap ScreenBitmap { get; set; }

            public void Dispose()
            {
                ScreenBitmap?.Dispose();
                Rectangle = Rectangle.Empty;
            }
        }
    }
}