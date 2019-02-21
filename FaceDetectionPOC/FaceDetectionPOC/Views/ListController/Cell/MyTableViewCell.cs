using System;

using Foundation;
using UIKit;

namespace FaceDetectionPOC.Views.ListController.Cell
{
    public partial class MyTableViewCell : UITableViewCell
    {
        public static readonly NSString Key = new NSString("MyTableViewCell");
        public static readonly UINib Nib;

        static MyTableViewCell()
        {
            Nib = UINib.FromName("MyTableViewCell", NSBundle.MainBundle);
        }

        protected MyTableViewCell(IntPtr handle) : base(handle)
        {
            // Note: this .ctor should not contain any initialization logic.
        }

        internal void UpdateCell(string v)
        {
            txtGreetings.Text = v;
            //cell.SelectedBackgroundView = new UIView();
            //this.BackgroundColor = UIColor.Red;
        }
    }
}
