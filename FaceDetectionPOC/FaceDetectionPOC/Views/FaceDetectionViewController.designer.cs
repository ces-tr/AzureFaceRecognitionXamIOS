// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace FaceDetectionPOC.Views
{
	[Register ("FaceDetectionViewController")]
	partial class FaceDetectionViewController
	{
		[Outlet]
		UIKit.UIImageView ivPictureTaken { get; set; }

		[Outlet]
		FaceDetectionPOC.PreviewView previewView { get; set; }

		[Outlet]
		UIKit.UITableView tvGreetings { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (ivPictureTaken != null) {
				ivPictureTaken.Dispose ();
				ivPictureTaken = null;
			}

			if (previewView != null) {
				previewView.Dispose ();
				previewView = null;
			}

			if (tvGreetings != null) {
				tvGreetings.Dispose ();
				tvGreetings = null;
			}
		}
	}
}
