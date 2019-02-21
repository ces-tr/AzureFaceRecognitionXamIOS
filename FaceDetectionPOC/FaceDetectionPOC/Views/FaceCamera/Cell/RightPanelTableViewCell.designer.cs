// WARNING
//
// This file has been generated automatically by Visual Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using Foundation;
using System;
using System.CodeDom.Compiler;

namespace FaceDetectionPOC.Views.FaceCamera.Cell
{
    [Register ("RightPanelTableViewCell")]
    partial class RightPanelTableViewCell
    {
        [Outlet]
        UIKit.UILabel lblText { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (lblText != null) {
                lblText.Dispose ();
                lblText = null;
            }
        }
    }
}