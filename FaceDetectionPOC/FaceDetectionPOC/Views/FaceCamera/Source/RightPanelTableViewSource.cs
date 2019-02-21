using System;
using System.Collections.Generic;
using Foundation;
using UIKit;
using FaceDetectionPOC.Views.FaceCamera.Cell;
namespace FaceDetectionPOC.Views.FaceCamera.Source
{
    public class RightPanelTableViewSource : UITableViewSource
    {
        public event EventHandler<string> RowSelectedEH;

        public List<string> FaceListItems { get; set; } = new List<string>();

        public override nint RowsInSection(UITableView tableview, nint section)
        {
            return FaceListItems != null ? FaceListItems.Count : 0;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var item = FaceListItems[indexPath.Row];

            //TODO
            //var cellContent = tableView?.DequeueReusableCell(RightPanelTableViewCell.Key, indexPath)
            //    as RightPanelTableViewCell;

            //cellContent?.UpdateCell(item);

            var cellContent = tableView.DequeueReusableCell("cell");
            if (cellContent == null)
            {
                cellContent = new UITableViewCell(UITableViewCellStyle.Default, "cell");
            }

            cellContent.TextLabel.Text = item;

            return cellContent;
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            var item = FaceListItems[indexPath.Row];
            RowSelectedEH.Invoke(null, item);
        }
    }
}
