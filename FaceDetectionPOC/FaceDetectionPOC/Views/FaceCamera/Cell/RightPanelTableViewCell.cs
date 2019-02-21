using System;

using Foundation;
using UIKit;

namespace FaceDetectionPOC.Views.FaceCamera.Cell
{
    public partial class RightPanelTableViewCell : UITableViewCell
    {
        public static readonly NSString Key = new NSString("RightPanelTableViewCell");
        public static readonly UINib Nib;

        static RightPanelTableViewCell()
        {
            Nib = UINib.FromName("RightPanelTableViewCell", NSBundle.MainBundle);
        }

        protected RightPanelTableViewCell(IntPtr handle) : base(handle)
        {
            // Note: this .ctor should not contain any initialization logic.
        }

        public void UpdateCell(string text)
        {
            lblText.Text = text;
        }
    }
}
