using AVFoundation;
using Foundation;
using ObjCRuntime;
using System;
using System.ComponentModel;
using UIKit;

namespace FaceDetectionPOC
{
    [DesignTimeVisible(true)]  // This makes it visible in the Custom Controls panel in the iOS Designer.  The code-behind file already has the Register attribute 
    public partial class PreviewView :  UIView, IComponent {


        public AVCaptureSession Session {
            get {
                return (Layer as AVCaptureVideoPreviewLayer).Session;
            }
            set {
                (Layer as AVCaptureVideoPreviewLayer).Session = value;
            }
        }

        #region IComponent implementation

        public ISite Site { get; set; }
        public event EventHandler Disposed;

        #endregion
        public PreviewView (IntPtr handle) : base (handle)
        {
        }

        [Export("layerClass")]
        public static Class LayerClass()
        {
            return new Class(typeof(AVCaptureVideoPreviewLayer));
        }


    }
}