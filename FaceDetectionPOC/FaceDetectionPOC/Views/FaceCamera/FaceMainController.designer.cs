// WARNING
//
// This file has been generated automatically by Visual Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using Foundation;
using System;
using System.CodeDom.Compiler;

namespace FaceDetectionPOC
{
    [Register ("FaceMainController")]
    partial class FaceMainController
    {
        [Outlet]
        UIKit.UIButton btnInfo { get; set; }


        [Outlet]
        UIKit.UILabel GreetingsLabel { get; set; }


        [Outlet]
        UIKit.UIView HomeView { get; set; }


        [Outlet]
        UIKit.UIView rightPanelView { get; set; }


        [Outlet]
        UIKit.UITableView tblViewRightPanel { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (btnInfo != null) {
                btnInfo.Dispose ();
                btnInfo = null;
            }

            if (GreetingsLabel != null) {
                GreetingsLabel.Dispose ();
                GreetingsLabel = null;
            }

            if (HomeView != null) {
                HomeView.Dispose ();
                HomeView = null;
            }

            if (rightPanelView != null) {
                rightPanelView.Dispose ();
                rightPanelView = null;
            }

            if (tblViewRightPanel != null) {
                tblViewRightPanel.Dispose ();
                tblViewRightPanel = null;
            }
        }
    }
}