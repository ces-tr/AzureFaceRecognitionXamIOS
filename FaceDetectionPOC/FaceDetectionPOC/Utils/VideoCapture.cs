using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AVFoundation;
using CoreAnimation;
using CoreFoundation;
using CoreGraphics;
using CoreImage;
using CoreMedia;
using CoreVideo;
using FaceDetectionPOC.Extensions;
using FaceDetectionPOC.Views;
using UIKit;

namespace FaceDetectionPOC.Utils {
   

    public class VideoCapture : AVCaptureVideoDataOutputSampleBufferDelegate {
        bool isProcessingBuffer = false;
        Action<string> greetingsCallback;
        Action<UIImage, CGRect> drawFacesCallback;

        CALayer previewLayer;

        public VideoCapture(Action<string> greetingsCallback, Action<UIImage, CGRect> drawFacesCallback)
        {
            this.greetingsCallback = greetingsCallback;
            this.drawFacesCallback = drawFacesCallback;
        }


        ImageAnalyzer imageAnalyzer = null;
        public static object lockerobj = new object();

        public override  void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
        {
            Console.WriteLine("Got Sample Fromn Buffer");
            lock (FaceDetectionViewController.lockerobj) {
                if (!FaceDetectionViewController.processingFaceDetection || isProcessingBuffer) {
                    sampleBuffer.Dispose();
                    return;
                }
                isProcessingBuffer = true;

            }

            try {
                CIImage ciImage = null;
                CGRect cleanAperture = default(CGRect);
                using (sampleBuffer) {
                    //CVPixelBuffer renderedOutputPixelBuffer = null;

                    byte[] managedArray ;
                    int width;
                    int height;
                    int bytesPerRow;

                    using (var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer) {


                        pixelBuffer.Lock(CVPixelBufferLock.None);

                        CVPixelFormatType ft = pixelBuffer.PixelFormatType;

                        IntPtr baseAddress = pixelBuffer.BaseAddress;
                        bytesPerRow = (int)pixelBuffer.BytesPerRow;

                         width = (int)pixelBuffer.Width;
                         height = (int)pixelBuffer.Height;

                        //managedArray = new byte[width * height];
                        managedArray = new byte[pixelBuffer.Height * pixelBuffer.BytesPerRow];
                        Marshal.Copy(baseAddress, managedArray, 0, managedArray.Length);


                        pixelBuffer.Unlock(CVPixelBufferLock.None);



                    }
                    sampleBuffer.Dispose();

                    //int bytesPerPixel = 4;
                    //int bytesPerRow = bytesPerPixel * width;
                    int bitsPerComponent = 8;
                    //CGColorSpace colorSpace = CGColorSpace.CreateDeviceRGB();

                    //CGContext context = new CGBitmapContext(managedArray, width, height,
                    //bitsPerComponent, bytesPerRow, colorSpace,
                    //CGBitmapFlags.PremultipliedLast | CGBitmapFlags.ByteOrder32Big);


                    var flags = CGBitmapFlags.PremultipliedFirst | CGBitmapFlags.ByteOrder32Little;
                    // Create a CGImage on the RGB colorspace from the configured parameter above
                    using (var cs = CGColorSpace.CreateDeviceRGB()) {
                        using (var context = new CGBitmapContext(managedArray, width, height, bitsPerComponent, bytesPerRow, cs, (CGImageAlphaInfo)flags)) {

                            ciImage = context.ToImage();

                            //using (CGImage cgImage = context.ToImage()) {
                            //    //pixelBuffer.Unlock(CVPixelBufferLock.None);

                            //    //return UIImage.FromImage(cgImage);
                            //}

                            context.Dispose();
                        }
                    }

                    //var a = new CMSampleBuffer.;
                    //using () {

                    //}

                    //UIImage image = GetImageFromSampleBuffer(sampleBuffer);

                    //if (!FaceMainController.isFaceRegistered || isProcessing)

                    //{
                    //    //      Console.WriteLine("OutputDelegate - Exit (isProcessing: " + DateTime.Now);
                    //    sampleBuffer.Dispose();
                    //    Console.WriteLine("processing..");

                    //    return;
                    //}


                    //Console.WriteLine("IsProcessing: ");

                    //isProcessing = true;
                    connection.VideoOrientation = AVCaptureVideoOrientation.Portrait;
                    connection.VideoScaleAndCropFactor = 1.0f;

                    //var bufferCopy = sampleBuffer.c

                    //UIImage image = GetImageFromSampleBuffer(sampleBuffer);

                    //ciImage = CIImage.FromCGImage(image.CGImage);

                    //cleanAperture = sampleBuffer.GetVideoFormatDescription().GetCleanAperture(false);


                }
                /*For Face Detection using iOS APIs*/
                //DispatchQueue.MainQueue.DispatchAsync(() =>
                using (ciImage) {
                    if (ciImage != null)
                        drawFacesCallback(UIImage.FromImage(ciImage), cleanAperture);
                }

                isProcessingBuffer = false;

                //Console.WriteLine(ciImage);
                //Task.Run(async () => {
                //    try {
                //        //if (ViewController.IsFaceDetected)
                //        //{
                //        Console.WriteLine("face detected: ");

                //        imageAnalyzer = new ImageAnalyzer(() => Task.FromResult<Stream>(image.ResizeImageWithAspectRatio(300, 400).AsPNG().AsStream()));
                //        await ProcessCameraCapture(imageAnalyzer);
                //        //}

                //    }

                //    finally {
                //        imageAnalyzer = null;
                //        isProcessing = false;
                //        Console.WriteLine("OUT ");

                //    }

                //});
            }
            catch (Exception ex) {
                Console.Write(ex);
            }
            finally {
                sampleBuffer.Dispose();
            }

        }

         

        private async Task ProcessCameraCapture(ImageAnalyzer e)
        {

            DateTime start = DateTime.Now;

            await e.DetectFacesAsync();

            if (e.DetectedFaces.Any()) {
                await e.IdentifyFacesAsync();
                string greetingsText = GetGreettingFromFaces(e);

                if (e.IdentifiedPersons.Any()) {

                    if (greetingsCallback != null) {
                        DisplayMessage(greetingsText);
                    }

                    Console.WriteLine(greetingsText);
                }
                else {
                    DisplayMessage("No Idea, who you're.. Register your face.");

                    Console.WriteLine("No Idea");

                }
            }
            else {
                DisplayMessage("No face detected.");

                Console.WriteLine("No Face ");

                //this.UpdateUIForNoFacesDetected();

            }

            TimeSpan latency = DateTime.Now - start;
            var latencyString = string.Format("Face API latency: {0}ms", (int)latency.TotalMilliseconds);
            Console.WriteLine(latencyString);
            //this.isProcessingPhoto = false;
        }

        void DisplayMessage(string greetingsText)
        {
            DispatchQueue.MainQueue.DispatchAsync(() =>
                                                  greetingsCallback(greetingsText));
        }

        private string GetGreettingFromFaces(ImageAnalyzer img)
        {
            if (img.IdentifiedPersons.Any()) {
                string names = img.IdentifiedPersons.Count() > 1 ? string.Join(", ", img.IdentifiedPersons.Select(p => p.Person.Name)) : img.IdentifiedPersons.First().Person.Name;

                if (img.DetectedFaces.Count() > img.IdentifiedPersons.Count()) {
                    return string.Format("Hi, {0} and company!", names);
                }
                else {
                    return string.Format("Hi, {0}!", names);
                }
            }
            else {
                if (img.DetectedFaces.Count() > 1) {
                    return "Hi everyone! If I knew any of you by name I would say it...";
                }
                else {
                    return "Hi there! If I knew you by name I would say it...";
                }
            }
        }


        UIImage GetImageFromSampleBuffer(CMSampleBuffer sampleBuffer)
        {
            // Get the CoreVideo image
            using (var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer) {
                // Lock the base address
                pixelBuffer.Lock(CVPixelBufferLock.None);
                // Get the number of bytes per row for the pixel buffer
                var baseAddress = pixelBuffer.BaseAddress;
                var bytesPerRow = (int)pixelBuffer.BytesPerRow;
                var width = (int)pixelBuffer.Width;

                var height = (int)pixelBuffer.Height;
                var flags = CGBitmapFlags.PremultipliedFirst | CGBitmapFlags.ByteOrder32Little;
                // Create a CGImage on the RGB colorspace from the configured parameter above
                using (var cs = CGColorSpace.CreateDeviceRGB()) {
                    using (var context = new CGBitmapContext(baseAddress, width, height, 8, bytesPerRow, cs, (CGImageAlphaInfo)flags)) {
                        using (CGImage cgImage = context.ToImage()) {
                            pixelBuffer.Unlock(CVPixelBufferLock.None);

                            return UIImage.FromImage(cgImage);
                        }
                    }
                }
            }
        }
    }
}
