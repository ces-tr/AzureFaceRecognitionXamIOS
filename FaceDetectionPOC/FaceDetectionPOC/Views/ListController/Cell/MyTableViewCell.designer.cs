// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace FaceDetectionPOC.Views.ListController.Cell
{
	[Register ("MyTableViewCell")]
	partial class MyTableViewCell
	{
		[Outlet]
		UIKit.UITextView txtGreetings { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (txtGreetings != null) {
				txtGreetings.Dispose ();
				txtGreetings = null;
			}
		}
	}
}
