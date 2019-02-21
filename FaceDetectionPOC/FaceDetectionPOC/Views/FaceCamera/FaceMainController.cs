using System;
using System.Linq;

using UIKit;
using AVFoundation;
using CoreAnimation;
using Foundation;
using CoreMedia;
using CoreVideo;
using CoreFoundation;
using CoreImage;
using CoreGraphics;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Microsoft.ProjectOxford.Face;
using System.Linq.Expressions;
using FaceDetectionPOC.Views.FaceCamera.Source;
using FaceDetectionPOC.Views.FaceCamera.Cell;
using System.Diagnostics.Tracing;
using FaceDetectionPOC.Utils;
using static FaceDetectionPOC.Views.FaceDetectionViewController;
using System.Collections.Generic;

namespace FaceDetectionPOC
{
    public partial class FaceMainController : UIViewController
    {
        static DispatchQueue sessionQueue = new DispatchQueue("com.itexico.FaceDetectionPOCqueue");
        static AVCaptureSession captureSession = new AVCaptureSession();
        static bool rightPanelViewHidden = true;

        static CIDetector faceDetector;


        public static bool IsFaceDetected = false;
        CALayer previewLayer;
        CALayer featureLayer = null;
        AVCaptureVideoDataOutput videoOut;

        AVCaptureDevice captureDevice;

        UIImage borderImage;

        public static bool isFaceRegistered = false;

        private RightPanelTableViewSource source;
        private AVCaptureMetadataOutput metadataOutput;
        Dictionary<int, FaceView> faceViews;

        public bool IsUsingFrontFacingCamera
        {
            get { return captureDevice?.Position == AVCaptureDevicePosition.Front; }
        }

        public FaceMainController()
        {

        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            this.Title = "Intelligent Kiosk";

            SetupSource();

            var options = new CIContextOptions();

            faceDetector = CIDetector.CreateFaceDetector(null, true);
            borderImage = UIImage.FromFile("square.png");

            UIDevice.CurrentDevice.BeginGeneratingDeviceOrientationNotifications();
        }

        private void SetupSource()
        {
            source = new RightPanelTableViewSource();
            tblViewRightPanel.SeparatorStyle = UITableViewCellSeparatorStyle.None;
            //tblViewRightPanel.RegisterNibForCellReuse()TODO
            tblViewRightPanel.Source = source;
        }

        public async override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            AddEventHandlers();

            //TODO: Just for POC
            LiveCamHelper.RegisterFaces();

            PrepareCamera();
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            RemoveEventHandlers();

            EndSession();
        }

        public override bool ShouldAutorotateToInterfaceOrientation(UIInterfaceOrientation toInterfaceOrientation)
        {
            return toInterfaceOrientation == UIInterfaceOrientation.Portrait;
        }


        //async Task RegisterFaces()
        //{
        //    try
        //    {
        //        var persongroupId = Guid.NewGuid().ToString();
        //        await FaceServiceHelper.CreatePersonGroupAsync(persongroupId,
        //                                                "Xamarin",
        //                                             AppDelegate.WorkspaceKey);

        //        await FaceServiceHelper.CreatePersonAsync(persongroupId, "Albert Einstein");

        //        var personsInGroup = await FaceServiceHelper.GetPersonsAsync(persongroupId);

        //        await FaceServiceHelper.AddPersonFaceAsync(persongroupId, personsInGroup[0].PersonId,
        //                        "https://upload.wikimedia.org/wikipedia/commons/d/d3/Albert_Einstein_Head.jpg", null, null);

        //        await FaceServiceHelper.TrainPersonGroupAsync(persongroupId);

        //        isFaceRegistered = true;
        //    }
        //    catch (FaceAPIException ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //        isFaceRegistered = false;
        //    }
        //}


        private void PrepareCamera()
        {
            captureSession.SessionPreset = AVCaptureSession.PresetMedium;
            captureDevice = AVCaptureDevice.DevicesWithMediaType(AVMediaType.Video)
                                            .FirstOrDefault(d => d.Position == AVCaptureDevicePosition.Front) ??
                                                     AVCaptureDevice.GetDefaultDevice(AVMediaType.Video);

            BeginSession();
        }


        private void BeginSession()
        {
            try
            {
                NSError error = null;
                var deviceInput = new AVCaptureDeviceInput(captureDevice, out error);
                if (error == null && captureSession.CanAddInput(deviceInput))
                    captureSession.AddInput(deviceInput);
                previewLayer = new AVCaptureVideoPreviewLayer(captureSession)
                {
                    VideoGravity = AVLayerVideoGravity.ResizeAspect
                };
                //this.HomeView.BackgroundColor = UIColor.Black;
                previewLayer.Frame = this.HomeView.Layer.Bounds;

                this.HomeView.Layer.AddSublayer(previewLayer);

                captureDevice.LockForConfiguration(out error);
                if (error != null)
                {
                    Console.WriteLine(error);
                    captureDevice.UnlockForConfiguration();
                    return;
                }

                if (UIDevice.CurrentDevice.CheckSystemVersion(7, 0))
                    captureDevice.ActiveVideoMinFrameDuration = new CMTime(1, 15);
                captureDevice.UnlockForConfiguration();

                captureSession.StartRunning();

                // create a VideoDataOutput and add it to the sesion
                videoOut = new AVCaptureVideoDataOutput()
                {
                    AlwaysDiscardsLateVideoFrames = true,
                    WeakVideoSettings = new CVPixelBufferAttributes()
                    {

                        PixelFormatType = CVPixelFormatType.CV32BGRA
                    }.Dictionary
                };

                if (captureSession.CanAddOutput(videoOut))
                    captureSession.AddOutput(videoOut);

               

                captureSession.CommitConfiguration();

                setupAVFoundationFaceDetection();

                //var OutputSampleDelegate = new VideoCapture(
                    //(s) =>
                    //{
                    //    GreetingsLabel.Text = s;
                    //    PopulateList(s);
                    //}, new Action<CIImage, CGRect>(DrawFaces));

                //videoOut.SetSampleBufferDelegateQueue(OutputSampleDelegate, sessionQueue);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        void setupAVFoundationFaceDetection()
        {
            faceViews = new Dictionary<int, FaceView>();

            metadataOutput = new AVCaptureMetadataOutput();
            if (!captureSession.CanAddOutput(metadataOutput)) {
                metadataOutput = null;
                return;
            }

            var metaDataObjectDelegate = new MetaDataObjectDelegate();
            metaDataObjectDelegate.DidOutputMetadataObjectsAction = DidOutputMetadataObjects;

            metadataOutput.SetDelegate(metaDataObjectDelegate, DispatchQueue.MainQueue);
            captureSession.AddOutput(metadataOutput);

           

            if (!metadataOutput.AvailableMetadataObjectTypes.HasFlag(AVMetadataObjectType.Face)) {
                //teardownAVFoundationFaceDetection();
                return;
            }

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

        public void DidOutputMetadataObjects(AVCaptureMetadataOutput captureOutput, AVMetadataObject[] faces, AVCaptureConnection connection)
        {


            //List<int> unseen = faceViews.Keys.ToList();
            //List<int> seen = new List<int>();

            //CATransaction.Begin();
            //CATransaction.SetValueForKey(NSObject.FromObject(true), (NSString)(CATransaction.DisableActions.ToString()));

            //foreach (var face in faces) {
            //    // HACK: int faceId = (face as AVMetadataFaceObject).FaceID;
            //    int faceId = (int)(face as AVMetadataFaceObject).FaceID;
            //    unseen.Remove(faceId);
            //    seen.Add(faceId);

            //    FaceView view;
            //    if (faceViews.ContainsKey(faceId))
            //        view = faceViews[faceId];
            //    else {
            //        view = new FaceView();
            //        view.Layer.CornerRadius = 10;
            //        view.Layer.BorderWidth = 3;
            //        view.Layer.BorderColor = UIColor.Green.CGColor;
            //        previewView.AddSubview(view);
            //        faceViews.Add(faceId, view);
            //        view.Id = faceId;
            //        view.Callback = TouchCallBack;
            //        if (lockedFaceID != null)
            //            view.Alpha = 0;
            //    }

            //    AVMetadataFaceObject adjusted = (AVMetadataFaceObject)(previewView.Layer as AVCaptureVideoPreviewLayer).GetTransformedMetadataObject(face);
            //    view.Frame = adjusted.Bounds;
            //}

            //foreach (int faceId in unseen) {
            //    FaceView view = faceViews[faceId];
            //    view.RemoveFromSuperview();
            //    faceViews.Remove(faceId);
            //    if (faceId == lockedFaceID)
            //        clearLockedFace();
            //}

            //if (lockedFaceID != null) {
            //    FaceView view = faceViews[lockedFaceID.GetValueOrDefault()];
            //    // HACK: Cast resulting nfloat to float
            //    // float size = (float)Math.Max (view.Frame.Size.Width, view.Frame.Size.Height) / device.VideoZoomFactor;
            //    float size = (float)(Math.Max(view.Frame.Size.Width, view.Frame.Size.Height) / device.VideoZoomFactor);
            //    float zoomDelta = lockedFaceSize / size;
            //    float lockTime = (float)(CATransition.CurrentMediaTime() - this.lockTime);
            //    float zoomRate = (float)(Math.Log(zoomDelta) / lockTime);
            //    if (Math.Abs(zoomDelta) > 0.1)
            //        device.RampToVideoZoom(zoomRate > 0 ? MaxZoom : 1, zoomRate);
            //}

            //CATransaction.Commit();


        }

            private void PopulateList(string s)
        {
            if (!s.Contains("No") && !s.Contains("Who"))
                source.FaceListItems.Add(s);
                //txthistory.Text += $"\n{s}";

        }

        private void EndSession()
        {
            captureSession.StopRunning();

            foreach (var i in captureSession.Inputs)
                captureSession.RemoveInput(i);
                
        }

        #region EventHandlers

        private void AddEventHandlers()
        {
            btnInfo.TouchUpInside += BtnInfo_TouchUpInside;
            source.RowSelectedEH += Source_RowSelectedEH;
        }

        private void RemoveEventHandlers()
        {
            btnInfo.TouchUpInside -= BtnInfo_TouchUpInside;
        }

        private void BtnInfo_TouchUpInside(object sender, EventArgs e)
        {
            rightPanelViewHidden = !rightPanelViewHidden;
            rightPanelView.Hidden = rightPanelViewHidden;
            tblViewRightPanel.ReloadData();
        }

        private void Source_RowSelectedEH(object sender, string nameSelected)
        {
            var alert = UIAlertController.Create("Selection", nameSelected, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("Ok", UIAlertActionStyle.Cancel, null));
            PresentViewController(alert, true, null);
        }


        #endregion

        #region Draw Faces

        private void DrawFaces(CIImage image, CGRect cleanAperture)
        {
            if (image == null)
                return;

            var features = faceDetector.FeaturesInImage(image);

            if (features.Count() > 0)
                IsFaceDetected = true;

            DrawFaces(features, cleanAperture, UIDeviceOrientation.Portrait);

        }

        private CIImageOrientation GetExifOrientation(UIDeviceOrientation orientation)
        {
            CIImageOrientation exifOrientation;

            switch (orientation)
            {
                case UIDeviceOrientation.PortraitUpsideDown:
                    exifOrientation = CIImageOrientation.LeftBottom;
                    break;
                case UIDeviceOrientation.LandscapeLeft:
                    if (IsUsingFrontFacingCamera)
                        exifOrientation = CIImageOrientation.BottomRight;
                    else
                        exifOrientation = CIImageOrientation.TopLeft;
                    break;
                case UIDeviceOrientation.LandscapeRight:
                    if (IsUsingFrontFacingCamera)
                        exifOrientation = CIImageOrientation.TopLeft;
                    else
                        exifOrientation = CIImageOrientation.BottomRight;

                    break;
                case UIDeviceOrientation.Portrait:
                default:
                    exifOrientation = CIImageOrientation.RightTop;
                    break;

            }

            return exifOrientation;
        }

        private void DrawFaces(CIFeature[] features,
                       CGRect clearAperture,
                       UIDeviceOrientation deviceOrientation)
        {

            var pLayer = this.previewLayer as AVCaptureVideoPreviewLayer;
            var subLayers = pLayer.Sublayers;
            var subLayersCount = subLayers.Count();

            var featureCount = features.Count();

            nint currentSubLayer = 0, currentFeature = 0;
            CATransaction.Begin();
            CATransaction.DisableActions = true;
            foreach (var layer in subLayers)
            {
                if (layer.Name == "FaceLayer")
                    layer.Hidden = true;

            }

            Console.WriteLine("Feature: " + featureCount);
            if (featureCount == 0)
            {
                CATransaction.Commit();
                return;
            }

            var parentFameSize = this.HomeView.Frame.Size;
            var gravity = pLayer.VideoGravity;
            var isMirrored = pLayer.Connection.VideoMirrored;
            var previewBox = VideoPreviewBoxForGravity(gravity, parentFameSize, clearAperture.Size);


            foreach (var feature in features)
            {

                // find the correct position for the square layer within the previewLayer
                // the feature box originates in the bottom left of the video frame.
                // (Bottom right if mirroring is turned on)
                CGRect faceRect = feature.Bounds;

                // flip preview width and height
                var tempCGSize = new CGSize(faceRect.Size.Height, faceRect.Size.Width);

                faceRect.Size = tempCGSize;
                faceRect.X = faceRect.Y;
                faceRect.Y = faceRect.X;

                //// scale coordinates so they fit in the preview box, which may be scaled
                var widthScaleBy = previewBox.Size.Width / clearAperture.Size.Height;
                var heightScaleBy = previewBox.Size.Height / clearAperture.Size.Width;
                var newWidth = faceRect.Size.Width * widthScaleBy;
                var newheight = faceRect.Size.Height * heightScaleBy;
                faceRect.Size = new CGSize(newWidth, newheight);
                faceRect.X *= widthScaleBy;
                faceRect.Y *= heightScaleBy;

                if (isMirrored)
                    faceRect.Offset(previewBox.X + previewBox.Size.Width - faceRect.Size.Width - (faceRect.X * 2),
                                             previewBox.Y);
                else
                    faceRect.Offset(previewBox.X, previewBox.Y);


                while (featureLayer != null && currentSubLayer < subLayersCount)
                {
                    CALayer currentLayer = subLayers[currentSubLayer++];
                    if (currentLayer.Name == "FaceLayer")
                    {
                        featureLayer = currentLayer;
                        currentLayer.Hidden = false;
                    }
                }

                if (featureLayer == null)
                {
                    featureLayer = new CALayer();
                    featureLayer.Contents = borderImage.CGImage;
                    featureLayer.Name = "FaceLayer";
                    this.previewLayer.AddSublayer(featureLayer);

                }

                featureLayer.Frame = faceRect;


                switch (deviceOrientation)
                {
                    case UIDeviceOrientation.Portrait:
                        featureLayer.AffineTransform = CGAffineTransform.MakeRotation(DegreesToRadians(0));
                        break;
                    case UIDeviceOrientation.PortraitUpsideDown:
                        featureLayer.AffineTransform = (CGAffineTransform.MakeRotation(DegreesToRadians(180)));
                        break;
                    case UIDeviceOrientation.LandscapeLeft:
                        featureLayer.AffineTransform = CGAffineTransform.MakeRotation(DegreesToRadians(90));
                        break;

                    case UIDeviceOrientation.LandscapeRight:
                        featureLayer.AffineTransform = CGAffineTransform.MakeRotation(DegreesToRadians(-90));

                        break;
                    case UIDeviceOrientation.FaceUp:
                    case UIDeviceOrientation.FaceDown:
                    default:
                        break; // leave the layer in its last known orientation
                }
                currentFeature++;

            }

            CATransaction.Commit();


        }


        public nfloat DegreesToRadians(nfloat deg)
        {
            return (nfloat)(Math.PI * deg / 180.0);
        }

        private CGRect VideoPreviewBoxForGravity(AVLayerVideoGravity gravity, CGSize frameSize, CGSize apertureSize)
        {
            var apertureRatio = apertureSize.Height / apertureSize.Width;
            var viewRatio = frameSize.Width / frameSize.Height;

            CGSize size = CGSize.Empty;

            if (gravity == AVLayerVideoGravity.ResizeAspectFill)
            {
                if (viewRatio > apertureRatio)
                {
                    size.Width = frameSize.Width;
                    size.Height = apertureSize.Width * (frameSize.Width / apertureSize.Height);
                }
                else
                {
                    size.Width = apertureSize.Height * (frameSize.Height / apertureSize.Width);
                    size.Height = frameSize.Height;
                }
            }
            else if (gravity == AVLayerVideoGravity.ResizeAspect)
            {
                if (viewRatio > apertureRatio)
                {
                    size.Width = apertureSize.Height * (frameSize.Height / apertureSize.Width);
                    size.Height = frameSize.Height;
                }
                else
                {
                    size.Width = frameSize.Width;
                    size.Height = apertureSize.Width * (frameSize.Width / apertureSize.Height);
                }
            }
            else if (gravity == AVLayerVideoGravity.Resize)
            {
                size.Width = frameSize.Width;
                size.Height = frameSize.Height;
            }


            CGRect videoBox = CGRect.Empty;
            videoBox.Size = size;
            if (size.Width < frameSize.Width)
                videoBox.X = (frameSize.Width - size.Width) / 2;
            else
                videoBox.X = (size.Width - frameSize.Width) / 2;

            if (size.Height < frameSize.Height)
                videoBox.Y = (frameSize.Height - size.Height) / 2;
            else
                videoBox.Y = (size.Height - frameSize.Height) / 2;

            return videoBox;
        }

        #endregion

    }



}
