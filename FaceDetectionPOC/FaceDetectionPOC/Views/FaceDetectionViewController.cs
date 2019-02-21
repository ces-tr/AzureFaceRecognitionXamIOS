using System;
using System.Collections.Generic;
using CoreGraphics;
using System.Linq;
using AVFoundation;
using CoreAnimation;
using CoreFoundation;
using CoreMedia;
using Foundation;
using UIKit;
using FaceDetectionPOC.Utils;
using CoreImage;
using CoreVideo;
using FaceDetectionPOC.Extensions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using MessageUI;
using FaceDetectionPOC.Views.ListController.Source;
using FaceDetectionPOC.Views.ListController.Cell;

namespace FaceDetectionPOC.Views {

    public partial class FaceDetectionViewController : UIViewController {

        AVCaptureSession session;
        AVCaptureDevice device;
        AVCaptureMetadataOutput metadataOutput;
        Dictionary<int, FaceView> faceViews;
        int? lockedFaceID;
        float lockedFaceSize;
        double lockTime;
        AVPlayer memeEffect;
        AVPlayer beepEffect;
        const float MEME_FLASH_DELAY = 0.7f;
        const float MEME_ZOOM_DELAY = 1.1f;
        const float MEME_ZOOM_TIME = 0.25f;
        IntPtr VideoZoomFactorContext,
            VideoZoomRampingContext,
            MemePlaybackContext;

        private CALayer rootLayer;
        DispatchQueue sessionQueue;

        public event Action FaceDetected;

        public static object lockerobj = new object();
        public static bool processingFaceDetection = false;

        public static bool faceDetected;

        public static string FaceDetectedKey = "faceDetectedkey";

        public FaceDetectionViewController() //: base(UserInterfaceIdiomIsPhone ? "ViewController_iPhone" : "ViewController_iPad", null)
        {
            VideoZoomFactorContext = new IntPtr();
            VideoZoomRampingContext = new IntPtr();
            MemePlaybackContext = new IntPtr();

            sessionQueue = new DispatchQueue("sessionQueue");


        }



        float MaxZoom {
            get {
                return (float)Math.Min(device != null ? device.ActiveFormat.VideoMaxZoomFactor : 1, 6);
            }
        }

        static bool UserInterfaceIdiomIsPhone {
            get { return UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone; }
        }

        void setupAVCapture()
        {
            session = new AVCaptureSession();
            //AVCaptureSession.Notifications.ObserveRuntimeError((object sender, AVCaptureSessionRuntimeErrorEventArgs e) => {
            //    Console.WriteLine("AVCaptureSessionError"+e.Error);
            //});

            //AVCaptureSession.Notifications.ObserveWasInterrupted((object sender, NSNotificationEventArgs e) => {
            //    Console.WriteLine("Interrupted"+e.Notification);
            //});
            //AVCaptureSession.Notifications.ObserveInterruptionEnded((object sender, NSNotificationEventArgs e) => {
            //    Console.WriteLine("InterruptedEnded" + e.Notification);
            //});
            //AVCaptureSession.Notifications.ObserveDidStopRunning((object sender, NSNotificationEventArgs e) => {
            //    Console.WriteLine("DidStopRunning" + e.Notification);
            //});

            //AVCaptureSession.Notifications.ObserveDidStartRunning((object sender, NSNotificationEventArgs e) => {
            //    Console.WriteLine("DidStartRunning" + e.Notification);
            //});


            session.SessionPreset = AVCaptureSession.PresetHigh;
            previewView.Session = session;

            updateCameraSelection();
            rootLayer = previewView.Layer;
            rootLayer.MasksToBounds = true;
            // HACK: Remove .ToString() for AVLayerVideoGravity
            // (previewView.Layer as AVCaptureVideoPreviewLayer).VideoGravity = AVLayerVideoGravity.ResizeAspectFill.ToString();
            (previewView.Layer as AVCaptureVideoPreviewLayer).VideoGravity = AVLayerVideoGravity.ResizeAspectFill;
            previewView.Layer.BackgroundColor = UIColor.Black.CGColor;

            setupAVFoundationFaceDetection();
            setUpStilImage();
            //setupVideoOutputCapture();

            if (device != null) {
                //

                device.AddObserver(this, (NSString)"videoZoomFactor", (NSKeyValueObservingOptions)0,
                                    VideoZoomFactorContext);
                device.AddObserver(this, (NSString)"rampingVideoZoom", (NSKeyValueObservingOptions)0,
                                    VideoZoomRampingContext);
            }

            session.StartRunning();
        }
        AVCaptureStillImageOutput stillImageOutput;
        void setUpStilImage()
        {
            stillImageOutput  = new AVCaptureStillImageOutput();
            var dict = new NSMutableDictionary();
            dict[AVVideo.CodecKey] = new NSNumber((int)AVVideoCodec.JPEG);
            //session.AddOutput(output);

            if (session.CanAddOutput(stillImageOutput))
                session.AddOutput(stillImageOutput);
        }


        void setupVideoOutputCapture() {

            // create a VideoDataOutput and add it to the sesion
            var videoOut = new AVCaptureVideoDataOutput()
            {
                AlwaysDiscardsLateVideoFrames = true,
                WeakVideoSettings = new CVPixelBufferAttributes()
                {

                    PixelFormatType = CVPixelFormatType.CV32BGRA
                }.Dictionary
            };

            if (session.CanAddOutput(videoOut))
                session.AddOutput(videoOut);

            var OutputSampleDelegate = new VideoCapture(
                (s) => {
                    Console.WriteLine("greetings Callback");
                    //GreetingsLabel.Text = s;
                    //PopulateList(s);
                }, new Action<UIImage, CGRect>(DrawFaces));

            videoOut.SetSampleBufferDelegateQueue(OutputSampleDelegate, sessionQueue);


        }

        //[Export("captureSessionNotification:")]
        void captureSessionNotification(NSNotification notification)
        {
            sessionQueue.DispatchAsync(delegate {
                if (notification.Name == AVCaptureSession.WasInterruptedNotification.ToString()) {
                    Console.WriteLine("Session interrupted");

                    //captureSessionStoppedRunning();
                }
                else if (notification.Name == AVCaptureSession.InterruptionEndedNotification.ToString())
                    Console.WriteLine("Session interruption ended");
                else if (notification.Name == AVCaptureSession.RuntimeErrorNotification.ToString()) {
                    //captureSessionStoppedRunning();

                    NSError error = (NSError)notification.UserInfo[AVCaptureSession.ErrorKey];
                    if (error.Code == (int)AVError.DeviceIsNotAvailableInBackground) {
                        Console.WriteLine("Device not available in background");
                    }
                    else if (error.Code == (int)AVError.MediaServicesWereReset)
                        Console.WriteLine("Media services were reset");
                    else
                        handleNonRecoverableCaptureSessionRuntimeError(error);
                }
                else if (notification.Name == AVCaptureSession.DidStartRunningNotification)
                    Console.WriteLine("Session started running");
                else if (notification.Name == AVCaptureSession.DidStopRunningNotification)
                    Console.WriteLine("Session stopped running");
            });
        }

        void handleNonRecoverableCaptureSessionRuntimeError(NSError error)
        {
            Console.WriteLine(String.Format("Fatal runtime error {0}, code {1}", error.Description, error.Code));
        }

        private void DrawFaces(UIImage ciImage, CGRect cleanAperture)
        {
           
            Console.WriteLine("Image Gotten from buffer");

            if (ciImage == null)
                return;

            DispatchQueue.MainQueue.DispatchAsync(delegate
            {
                ivPictureTaken.Image = ciImage;

            });
                //lock (lockerobj) {

            //}


            Task.Delay(5000).ConfigureAwait(false);

            lock (lockerobj) {
               
                processingFaceDetection = false;
            }


            //var features = faceDetector.FeaturesInImage(image);

            //if (features.Count() > 0)
            //    IsFaceDetected = true;

            //DrawFaces(features, cleanAperture, UIDeviceOrientation.Portrait);

        }


        void setupAVFoundationFaceDetection()
        {
            faceViews = new Dictionary<int, FaceView>();

            metadataOutput = new AVCaptureMetadataOutput();
            if (!session.CanAddOutput(metadataOutput)) {
                metadataOutput = null;
                return;
            }

            var metaDataObjectDelegate = new MetaDataObjectDelegate();
            metaDataObjectDelegate.DidOutputMetadataObjectsAction = DidOutputMetadataObjects;

            metadataOutput.SetDelegate(metaDataObjectDelegate, DispatchQueue.MainQueue);
            session.AddOutput(metadataOutput);

            //foreach (var t in metadataOutput.AvailableMetadataObjectTypes) {
            //    Console.WriteLine(t);
            //}

            if (!metadataOutput.AvailableMetadataObjectTypes.HasFlag(AVMetadataObjectType.Face)) {
                teardownAVFoundationFaceDetection();
                return;
            }

            //metadataOutput.

            metadataOutput.MetadataObjectTypes = AVMetadataObjectType.Face;

            //DispatchQueue.MainQueue.DispatchAsync();
            //sessionQueue.DispatchAsync(updateAVFoundationFaceDetection);
            //updateAVFoundationFaceDetection();
        }

        void updateAVFoundationFaceDetection()
        {
            if (metadataOutput != null) {
                AVCaptureConnection connection = metadataOutput.ConnectionFromMediaType(AVMediaType.Metadata);
                connection.Enabled = true;
            }

        }

        void teardownAVFoundationFaceDetection()
        {
            if (metadataOutput != null)
                session.RemoveOutput(metadataOutput);

            metadataOutput = null;
            faceViews = null;
        }

        void teardownAVCapture()
        {
            session.StopRunning();

            teardownAVFoundationFaceDetection();

            device.UnlockForConfiguration();
            device.RemoveObserver(this, (NSString)"videoZoomFactor");
            device.RemoveObserver(this, (NSString)"rampingVideoZoom");
            device = null;

            session = null;
        }

        AVCaptureDeviceInput pickCamera()
        {
            AVCaptureDevicePosition desiredPosition = AVCaptureDevicePosition.Front;
            bool hadError = false;

            foreach (var device in AVCaptureDevice.DevicesWithMediaType(AVMediaType.Video)) {
                if (device.Position == desiredPosition) {
                    NSError error = null;
                    AVCaptureDeviceInput input = AVCaptureDeviceInput.FromDevice(device, out error);

                    if (error != null) {
                        hadError = true;
                        displayErrorOnMainQueue(error, "Could not initialize for AVMediaTypeVideo");
                    }
                    else if (session.CanAddInput(input))
                        return input;
                }
            }

            if (!hadError)
                displayErrorOnMainQueue(null, "No camera found for requested orientation");

            return null;
        }

        void updateCameraSelection()
        {
            session.BeginConfiguration();

            AVCaptureInput[] oldInputs = session.Inputs;
            foreach (var oldInput in oldInputs)
                session.RemoveInput(oldInput);

            AVCaptureDeviceInput input = pickCamera();
            if (input == null) {
                foreach (var oldInput in oldInputs)
                    session.AddInput(oldInput);
            }
            else {
                session.AddInput(input);
                device = input.Device;

                NSError error;
                if (!device.LockForConfiguration(out error))
                    Console.WriteLine("Could not lock for device: " + error.LocalizedDescription);

                //updateAVFoundationFaceDetection();
            }

            session.CommitConfiguration();
        }


        string ruta;
        string archivoLocal;
        byte[] arregloJPG;
        UIDeviceOrientation deviceOrientation= UIDeviceOrientation.Portrait;

        public async void DidOutputMetadataObjects(AVCaptureMetadataOutput captureOutput, AVMetadataObject[] faces, AVCaptureConnection connection)
        {

            Console.WriteLine("Got metadata");

            try {
                List<int> unseen = faceViews.Keys.ToList();
                List<int> seen = new List<int>();


                CATransaction.Flush();
                CATransaction.Begin();
                CATransaction.SetValueForKey(NSObject.FromObject(true), (NSString)(CATransaction.DisableActions.ToString()));



                foreach (var face in faces) {
                    // HACK: int faceId = (face as AVMetadataFaceObject).FaceID;
                    int faceId = (int)(face as AVMetadataFaceObject).FaceID;
                    unseen.Remove(faceId);
                    seen.Add(faceId);

                    FaceView view;
                    if (faceViews.ContainsKey(faceId))
                        view = faceViews[faceId];
                    else {
                        view = new FaceView();
                        view.Layer.CornerRadius = 10;
                        view.Layer.BorderWidth = 3;
                        view.Layer.BorderColor = UIColor.Green.CGColor;
                        previewView.AddSubview(view);
                        faceViews.Add(faceId, view);
                        view.Id = faceId;
                        view.Callback = TouchCallBack;
                        if (lockedFaceID != null)
                            view.Alpha = 0;
                    }

                    AVMetadataFaceObject adjusted = (AVMetadataFaceObject)(previewView.Layer as AVCaptureVideoPreviewLayer).GetTransformedMetadataObject(face);
                    view.Frame = adjusted.Bounds;
                }

                foreach (int faceId in unseen) {
                    FaceView view = faceViews[faceId];
                    view.RemoveFromSuperview();
                    faceViews.Remove(faceId);
                    if (faceId == lockedFaceID)
                        clearLockedFace();
                }

                if (lockedFaceID != null) {
                    FaceView view = faceViews[lockedFaceID.GetValueOrDefault()];
                    // HACK: Cast resulting nfloat to float
                    // float size = (float)Math.Max (view.Frame.Size.Width, view.Frame.Size.Height) / device.VideoZoomFactor;
                    float size = (float)(Math.Max(view.Frame.Size.Width, view.Frame.Size.Height) / device.VideoZoomFactor);
                    float zoomDelta = lockedFaceSize / size;
                    float lockTime = (float)(CATransition.CurrentMediaTime() - this.lockTime);
                    float zoomRate = (float)(Math.Log(zoomDelta) / lockTime);
                    if (Math.Abs(zoomDelta) > 0.1)
                        device.RampToVideoZoom(zoomRate > 0 ? MaxZoom : 1, zoomRate);
                }




            }
            catch {
                Console.WriteLine("error weird");
            }
            finally {

                CATransaction.Commit();
            }

            lock (lockerobj) {
                if (processingFaceDetection) {

                    return;
                }
                processingFaceDetection = true;
            }

            //CATransaction.Begin();
            //CATransaction.SetValueForKey(NSObject.FromObject(true), (NSString)(CATransaction.DisableActions.ToString()));



            AVCaptureConnection avcaptureconnection =stillImageOutput.ConnectionFromMediaType(AVMediaType.Video);
            //AVCaptureAutoExposureBracketedStillImageSettings bracketedstillimagesettings = AVCaptureAutoExposureBracketedStillImageSettings.Create(exposureTargetBias: AVCaptureDevice.ExposureTargetBiasCurrent);

            //var settings = new AVCaptureBracketedStillImageSettings[] { bracketedstillimagesettings };


            //stillImageOutput.PrepareToCaptureStillImageBracket(avcaptureconnection,settings, (status,error)=> {
            //    if (error == null) {

            //        stillImageOutput.CaptureStillImageAsynchronously(avcaptureconnection,
            //                                    (CMSampleBuffer imageDataSampleBuffer, NSError nserror) => {
            //                                        if (nserror == null) {
            //                                            using (var sampleBuffer = imageDataSampleBuffer) {

            //                                                if (sampleBuffer != null) {
            //                                                    using (NSData imageData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer)) {
            //                                                        if (imageData != null) {
            //                                                            uIImage = UIImage.LoadFromData(imageData);
            //                                                            /// operater your image
            //                                                            //Console.WriteLine(image);

            //                                                            SetImage(uIImage);



            //                                                        }
            //                                                    }


            //                                                }
            //                                                else {
            //                                                    Console.WriteLine("something was wrong");
            //                                                }
            //                                            }
            //                                        }


            //                                    });
            //    }
            //});

            //CATransaction.Commit();

            //DispatchQueue.MainQueue.DispatchAsync(() => {
            //    CaptureImageWithMetadata(stillImageOutput, avcaptureconnection);
            //});


            //stillImageOutput.CaptureStillImageAsynchronously(avcaptureconnection,
                            //(CMSampleBuffer imageDataSampleBuffer, NSError nserror) => {



                            //    if (nserror == null) {

                            //        //DispatchQueue.GetGlobalQueue(DispatchQueuePriority.Default).DispatchAsync(() =>
                            //        //{
                            //        DispatchQueue.MainQueue.DispatchAsync(() => {
                            //            UIAlertView alert = new UIAlertView();
                            //            alert.Show();
                            //        });
                            //        //});
                            //        //DispatchQueue.MainQueue.DispatchAsync(delegate
                            //        //{
                            //        CIImage image = null;
                            //        using (var sampleBuffer = imageDataSampleBuffer) {
                            //            NSData imageData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);
                            //            image = CIImage.FromData(imageData);


                            //        }




                            //         uIImage = image.MakeUIImageFromCIImage();
                            //        ivPictureTaken.BackgroundColor = UIColor.Blue;
                            //        //ivPictureTaken.Image = uIImage;
                            //        //Thread.Sleep(2000);
                            //        //processingFaceDetection = false;


                            //        //});

                            //    }
                            //    else {

                            //        Console.WriteLine("Something went wrong");
                            //    }

                            //});
            ivPictureTaken.BackgroundColor= (ivPictureTaken.BackgroundColor==UIColor.Blue)? UIColor.Black : UIColor.Blue;

            await Task.Delay(1000);
            CMSampleBuffer sampleBuffer = await stillImageOutput.CaptureStillImageTaskAsync(avcaptureconnection);
            foreach (var face in faces) {
                int faceId = (int)(face as AVMetadataFaceObject).FaceID;
                if (faceViews != null && faceViews.ContainsKey(faceId)) {

                    var view = faceViews[faceId];
                    view.Frame = CGRect.Empty;
                    view.RemoveFromSuperview();
                }
            }
            teardownAVFoundationFaceDetection();


            CIImage ciImage = null;
            UIImage uIImage = null;
            UIImage transformeduIImage = null;
            using (sampleBuffer ) {
                NSData imageData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);
                arregloJPG = imageData.ToArray();
                ciImage = CIImage.FromData(imageData);
                uIImage= new UIImage(imageData);

                CGAffineTransform cGAffineTransform = new CGAffineTransform();

                switch (deviceOrientation) {
                    case UIDeviceOrientation.Portrait:

                        cGAffineTransform = CGAffineTransform.MakeRotation(DegreesToRadians(0));

                        break;
                    case UIDeviceOrientation.PortraitUpsideDown:
                        cGAffineTransform = (CGAffineTransform.MakeRotation(DegreesToRadians(180)));
                        break;
                    case UIDeviceOrientation.LandscapeLeft:
                        cGAffineTransform = CGAffineTransform.MakeRotation(DegreesToRadians(90));
                        break;

                    case UIDeviceOrientation.LandscapeRight:
                        cGAffineTransform = CGAffineTransform.MakeRotation(DegreesToRadians(-90));
                        //cGAffineTransform.Translate(uIImage.CGImage.Width,0);

                        break;
                    case UIDeviceOrientation.FaceUp:
                    case UIDeviceOrientation.FaceDown:
                    default:
                        break; // leave the layer in its last known orientation
                }

                var flags = CGBitmapFlags.PremultipliedFirst | CGBitmapFlags.ByteOrder32Little;
               
                // Create a CGImage on the RGB colorspace from the configured parameter above
                using (var cs = CGColorSpace.CreateDeviceRGB()) {
                    using (CGBitmapContext context = new CGBitmapContext(null, (int)uIImage.CGImage.Width, (int)uIImage.CGImage.Height, uIImage.CGImage.BitsPerComponent, uIImage.CGImage.BytesPerRow, cs, (CGImageAlphaInfo)flags)) {

                        context.ConcatCTM(cGAffineTransform);
                        var cgRect = new CGRect(0, 0, uIImage.CGImage.Width, uIImage.CGImage.Height);
                        context.DrawImage(cgRect, uIImage.CGImage);
                        //ciImage = context.ToImage();

                        using (CGImage cgImage2 = context.ToImage()) {
                            //pixelBuffer.Unlock(CVPixelBufferLock.None);
                            transformeduIImage= UIImage.FromImage(cgImage2);
                            //return UIImage.FromImage(cgImage);
                        }


                    }
                }


            }

            sampleBuffer.Dispose();

            //UIImage uIImage = image2.MakeUIImageFromCIImage();
            NSData nsdata = uIImage.ResizeImageWithAspectRatio(640, 480).AsPNG();
            ivPictureTaken.Image = UIImage.LoadFromData(nsdata);//uIImage;
            //byte[] bytes = nsdata.ToArray();
            //WriteToFile(nsdata);
            //string encoded = Base64.EncodeToString(localdata, Base64Flags.Default);
            //byte[] b = System.IO.File.ReadAllBytes(FileName);


            //string rutaCarpeta = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            //string resultado = "FotoLAB12";
            //archivoLocal = resultado + ".jpg";
            //ruta = Path.Combine(rutaCarpeta, archivoLocal);
            //File.WriteAllBytes(ruta, arregloJPG);
            //ivPictureTaken.Image= UIImage.FromFile(ruta);
            //DispatchQueue.MainQueue.DispatchAsync(() => {
            //ivPictureTaken.Image = null;
            //InvokeOnMainThread(() => {
            //    ivPictureTaken.BackgroundColor = UIColor.Black;
            //    ivPictureTaken = new UIImageView(uIImage);
            //});

            //ivPictureTaken.SetNeedsDisplay();

            //CATransaction.Commit();

            //});
            //DispatchQueue.GetGlobalQueue(DispatchQueuePriority.Default).DispatchAsync(() =>
            //{
            ProcessingImage(nsdata);
            //});
            //DispatchQueue.GetGlobalQueue(DispatchQueuePriority.Default).DispatchAsync(() => {
            //    //NSNotificationCenter.DefaultCenter.PostNotificationName("OnFaceDetected", uIImage);
            //});
            //session.StopRunning();
            //await Task.Delay(3000);
            //processingFaceDetection = false;
            var a = -1;

        }

        MFMailComposeViewController mailController;

        void WriteToFile(NSData nsdata)
        {
            try {

                // string encoded = Base64.EncodeToString(localdata, Base64Flags.Default);
                string encoded = nsdata.GetBase64EncodedData(NSDataBase64EncodingOptions.SixtyFourCharacterLineLength).ToString();//Convert.ToBase64String(localdata);
                //txtoutput.Text = encoded;
                //txtoutput.SizeToFit();
                //txtoutput.LayoutIfNeeded();

                if (MFMailComposeViewController.CanSendMail) {

                    mailController = new MFMailComposeViewController();

                    // do mail operations here
                    mailController.SetToRecipients(new string[] { "ces.tr.rv@gmail.com" });
                    mailController.SetSubject("mail test");
                    mailController.SetMessageBody("this is a test "+ encoded, false);

                    mailController.Finished += (object s, MFComposeResultEventArgs args) =>
                    {
                        Console.WriteLine(args.Result.ToString());
                        args.Controller.DismissViewController(true, null);
                    };

                    this.PresentViewController(mailController, true, null);
                }


                //var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

                //if (!System.IO.Directory.Exists(documentsPath.ToString())) {
                //    Directory.CreateDirectory(documentsPath.ToString());
                //}

                //var filePath = Path.Combine(documentsPath, "image" + DateTime.Now.Ticks);

                //// In this line where i create FileStream i get an Exception
                //FileStream fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                //using (var streamWriter = new StreamWriter(fileStream)) {
                //    streamWriter.Write(encoded);
                //}

            }
            catch (Exception e) {
                Console.WriteLine(e.StackTrace);
            }


        }

        void RestartSession() {
            session.StartRunning();
        }

        void SetImage(UIImage uIImage) {

           
                ivPictureTaken.Image = uIImage;
                processingFaceDetection = false;

           
        }

        static object processingobjlocker = new object();
        static bool isAPIprocessing;
        CancellationTokenSource cancellationTokenSource;


        Task<bool> task;
        private MyListTableSource tvGreetingsSource;

        async void ProcessingImage(NSData uIImage)
        {

            lock (processingobjlocker) { 
                if (isAPIprocessing) {
                    return; 
                }
                isAPIprocessing = true;
            }

            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
            //UIImage uIImage = null;
            //DispatchQueue.MainQueue.DispatchSync(() => {

            //    uIImage = ivPictureTaken.Image;
            //});

          

            try {
                if (task != null && (task.Status == TaskStatus.Running || task.Status == TaskStatus.WaitingToRun || task.Status == TaskStatus.WaitingForActivation)) {
                    Console.WriteLine("Task has attempted to start while already running");
                }
                else {
                    Console.WriteLine("running api face recognition: ");
                    task =   await Task.Factory.StartNew(async() => {



                        //await Task.Delay(10000);
                        //await Task.Delay(10000);
                        //await Task.Delay(10000);
                        //UIImage uIImage = UIImage.FromFile(ruta);

                        //using (uIImage) {
                            ImageAnalyzer imageAnalyzer = new ImageAnalyzer(() => Task.FromResult<Stream>(uIImage.AsStream()),null);
                            await LiveCamHelper.ProcessCameraCapture(imageAnalyzer).ConfigureAwait(false);

                        //}


                        return true;

                }, cancellationTokenSource.Token,
                    TaskCreationOptions.None,
                    TaskScheduler.Default).ConfigureAwait(false); ;
                await task;

                }

            }
            catch (Exception e) {
                Console.WriteLine("error api face recogniion ");
            }

            finally {

                //processingFaceDetection = false;
                //lock (lockerobj) {
                await Task.Delay(2000);
                    
                    processingFaceDetection = false;
                    isAPIprocessing = false;
                    setupAVFoundationFaceDetection();
            }
            Console.WriteLine("finished processing ");

        }



        CIImage createMaskImageFromMetadata(AVMetadataObject[] metadataObjects)
        {
            CIImage maskImage = null;

            foreach (AVMetadataObject avmObj in metadataObjects) {
                //if ( [[object type] isEqual: AVMetadataObjectTypeFace] )
                if (avmObj.Type.Equals(AVMetadataObjectType.Face)) {
                    AVMetadataFaceObject face = (AVMetadataFaceObject)avmObj;
                    CGPoint origin = face.Bounds.Location;
                    CGRect faceRectangle = face.Bounds;
                    //int height = inputImage.extent.size.height;
                    //int width = inputImage.extent.size.w dth;
                    //CGImage imageRef = new CGImage(,);
                    //CGImage.WithImageInRect(faceRectangle);
                    //UIImage* result = [UIImage imageWithCGImage: imageRef scale: self.scale orientation: self.imageOrientation];
                    //CGImageRelease(imageRef);
                    //    CGFloat centerY = (height * (1 - (faceRectangle.origin.x + faceRectangle.size.width / 2.0)));
                    //    CGFloat centerX = width * (1 - (faceRectangle.origin.y + faceRectangle.size.height / 2.0));
                    //    CGFloat radiusX = width * (faceRectangle.size.width / 1.5);
                    //    CGFloat radiusY = height * (faceRectangle.size.height / 1.5);

                    //    CIImage* circleImage = [self createCircleImageWithCenter: CGPointMake(centerX, centerY)


                    //                                                      radius: CGVectorMake(radiusX, radiusY)


                    //                                                       angle: 0];

                    //    maskImage = [self compositeImage: circleImage ontoBaseImage: maskImage];
                }

            }

            return maskImage;
        }



        //private void CaptureImageWithMetadata(AVCaptureStillImageOutput output, AVCaptureConnection connection)
        //{
            //CIImage ciImage = null;

            //NSData imageData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);

            //CIImage image = CIImage.FromData(imageData);
            //NSMutableDictionary metadata = image.Properties.Dictionary.MutableCopy() as NSMutableDictionary;



            //DispatchQueue.MainQueue.DispatchAsync(delegate
            //{
            //    //UIImage uIImage = image.MakeUIImageFromCIImage();
            //    //SetImage(uIImage);

            //    //byte[] imageBuffer = imageData.ToArray();
            //    //if (imageBuffer != null) {
            //    //    using (var ms = new MemoryStream(imageBuffer)) {
            //    //        var uIImage = UIImage.LoadFromData(NSData.FromStream(ms));

            //    //        this.Add(new UIImageView(uIImage));
            //    using (ciImage) {
            //        if (ciImage != null) {
            //            ivPictureTaken.Image = UIImage.FromImage(ciImage);

            //        }
            //        processingFaceDetection = false;
            //    }


            //    //    }
            //    //}

            //});


            //...manipulate metadata here...

            //ALAssetsLibrary library = new ALAssetsLibrary();
            //library.WriteImageToSavedPhotosAlbum(imageData, metadata, (assetUrl, error) => {
            //    if (error == null) {
            //        Console.WriteLine("assetUrl:" + assetUrl);
            //    }
            //    else {
            //        Console.WriteLine(error);
            //    }
            //});
        //}


        void TouchCallBack(int faceId, FaceView view)
        {
            lockedFaceID = faceId;
            // HACK: Cast double to float
            // lockedFaceSize = Math.Max (view.Frame.Size.Width, view.Frame.Size.Height) / device.VideoZoomFactor;
            lockedFaceSize = (float)(Math.Max(view.Frame.Size.Width, view.Frame.Size.Height) / device.VideoZoomFactor);
            lockTime = CATransition.CurrentMediaTime();

            UIView.BeginAnimations(null, IntPtr.Zero);
            UIView.SetAnimationDuration(0.3f);
            view.Layer.BorderColor = UIColor.Red.CGColor;
            foreach (var face in faceViews.Values) {
                if (face != view)
                    face.Alpha = 0;
            }
            UIView.CommitAnimations();

            beepEffect.Seek(CMTime.Zero);
            beepEffect.Play();
        }

        void displayErrorOnMainQueue(NSError error, string message)
        {
            DispatchQueue.MainQueue.DispatchAsync(delegate {
                UIAlertView alert = new UIAlertView();
                if (error != null) {
                    alert.Title = message + " (" + error.Code + ")";
                    alert.Message = error.LocalizedDescription;
                }
                else
                    alert.Title = message;

                alert.AddButton("Dismiss");
                alert.Show();
            });
        }

        public override void TouchesEnded(NSSet touches, UIEvent evt)
        {
            if (device != null) {
                if (lockedFaceID != null)
                    clearLockedFace();
                else {
                    UITouch touch = (UITouch)touches.AnyObject;
                    CGPoint point = touch.LocationInView(previewView);
                    point = (previewView.Layer as AVCaptureVideoPreviewLayer).CaptureDevicePointOfInterestForPoint(point);

                    if (device.FocusPointOfInterestSupported)
                        device.FocusPointOfInterest = point;
                    if (device.ExposurePointOfInterestSupported)
                        device.ExposurePointOfInterest = point;
                    // HACK: Change AVCaptureFocusMode.ModeAutoFocus to AVCaptureFocusMode.AutoFocus
                    // if (device.IsFocusModeSupported (AVCaptureFocusMode.ModeAutoFocus))
                    //          device.FocusMode = AVCaptureFocusMode.ModeAutoFocus;
                    if (device.IsFocusModeSupported(AVCaptureFocusMode.AutoFocus))
                        device.FocusMode = AVCaptureFocusMode.AutoFocus;
                }
            }

            base.TouchesEnded(touches, evt);
        }

        public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
        {
            if (device == null)
                return;

            if (context == VideoZoomFactorContext) {
                // HACK: Cast nfloat to float
                // setZoomSliderValue (device.VideoZoomFactor);
                setZoomSliderValue((float)device.VideoZoomFactor);
                //memeButton.Enabled = (device.VideoZoomFactor > 1);
            }
            else if (context == VideoZoomRampingContext) {
                //slider.Enabled = device.RampingVideoZoom;
                //if (slider.Enabled && memeEffect.Rate == 0f)
                    //clearLockedFace();
            }
            else if (context == MemePlaybackContext) {
                if (device.TorchAvailable)
                    device.TorchMode = AVCaptureTorchMode.Off;
                fadeInFaces();
            }
            else
                Console.WriteLine("Unhandled observation: " + keyPath);
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            string path = NSBundle.MainBundle.PathForResource("Dramatic2", "m4a");
            if (path != null) {
                memeEffect = AVPlayer.FromUrl(NSUrl.FromFilename(path));
                memeEffect.AddObserver(this, (NSString)"rate", (NSKeyValueObservingOptions)0, MemePlaybackContext);
            }
            path = NSBundle.MainBundle.PathForResource("Sosumi", "wav");
            if (path != null)
                beepEffect = AVPlayer.FromUrl(NSUrl.FromFilename(path));

            //sessionQueue.DispatchAsync(setupAVCapture);
            setupAVCapture();

            if (MaxZoom == 1f && device != null) {
                displayErrorOnMainQueue(null, "Device does not support zoom");
                //slider.Enabled = false;
            }

            SetTableProperties();
            LiveCamHelper.GreetingsCallback = (s) =>
            {
                InvokeOnMainThread(() => {
                    if (tvGreetingsSource.TableItems == null)
                        tvGreetingsSource.TableItems = new List<string>();
                    tvGreetingsSource.TableItems.Add(s);
                    tvGreetings.ReloadData();
                });

            };
        }


        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);
            //TODO: Just for POC

                   

        }

        public override void WillRotate(UIInterfaceOrientation toInterfaceOrientation, double duration)
        {
            (previewView.Layer as AVCaptureVideoPreviewLayer).Connection.VideoOrientation =
                (AVCaptureVideoOrientation)toInterfaceOrientation;
        }

        private void SetTableProperties()
        {

            tvGreetingsSource = new MyListTableSource();
            tvGreetings.RegisterNibForCellReuse(MyTableViewCell.Nib, MyTableViewCell.Key);
            tvGreetings.Source = tvGreetingsSource;
        }


        //partial void meme(NSObject sender)
        //{
        //    memeEffect.Seek(CMTime.Zero);
        //    memeEffect.Play();
        //    NSObject.CancelPreviousPerformRequest(this);
        //    PerformSelector(new ObjCRuntime.Selector("flash"), null, MEME_FLASH_DELAY);
        //    PerformSelector(new ObjCRuntime.Selector("startZoom:"), NSNumber.FromFloat(getZoomSliderValue()), MEME_ZOOM_DELAY);
        //    device.VideoZoomFactor = 1;

        //    if (faceViews == null)
        //        return;

        //    foreach (var faceId in faceViews.Keys) {
        //        FaceView view = faceViews[faceId];
        //        view.Alpha = 0;
        //    }
        //}

        [Export("flash")]
        void flash()
        {
            if (device.TorchAvailable)
                device.TorchMode = AVCaptureTorchMode.On;
        }

        [Export("startZoom:")]
        void startZoom(NSNumber target)
        {
            float zoomPower = (float)Math.Log(target.FloatValue);
            device.RampToVideoZoom(target.FloatValue, zoomPower / MEME_ZOOM_TIME);
        }

        //partial void sliderChanged(NSObject sender)
        //{
        //    if (device != null && !device.RampingVideoZoom)
        //        device.VideoZoomFactor = getZoomSliderValue();
        //}

        void clearLockedFace()
        {
            lockedFaceID = null;
            fadeInFaces();
            device.CancelVideoZoomRamp();
        }

        void fadeInFaces()
        {
            UIView.BeginAnimations(null, IntPtr.Zero);
            UIView.SetAnimationDuration(0.3);
            foreach (var face in faceViews.Values) {
                face.Alpha = 1;
                face.Layer.BorderColor = UIColor.Green.CGColor;
            }
            UIView.CommitAnimations();
        }

        float getZoomSliderValue()
        {
            return 0;// (float)Math.Pow(MaxZoom, slider.Value);
        }

        void setZoomSliderValue(float value)
        {
            //slider.Value = (float)Math.Log(value) / (float)Math.Log(MaxZoom);
        }

        public nfloat DegreesToRadians(nfloat deg)
        {
            return (nfloat)(Math.PI * deg / 180.0);
        }

        public class MetaDataObjectDelegate : AVCaptureMetadataOutputObjectsDelegate {
            public Action<AVCaptureMetadataOutput, AVMetadataObject[], AVCaptureConnection> DidOutputMetadataObjectsAction;

            public override void DidOutputMetadataObjects(AVCaptureMetadataOutput captureOutput, AVMetadataObject[] faces, AVCaptureConnection connection)
            {
                DidOutputMetadataObjectsAction?.Invoke(captureOutput, faces, connection);
            }




        }



    }
}

/*


namespace SoZoomy
{
    public partial class ViewController : UIViewController
    {
        

        

        

     }
}

*/

