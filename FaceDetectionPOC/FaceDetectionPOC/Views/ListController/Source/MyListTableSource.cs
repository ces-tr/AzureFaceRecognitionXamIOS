using System;
using Foundation;
using UIKit;
using System.Collections.Generic;
using FaceDetectionPOC.Views.ListController.Cell;

namespace FaceDetectionPOC.Views.ListController.Source
{
    public class MyListTableSource : UITableViewSource
    {
        public List<string> TableItems { get; set; } 

        public MyListTableSource()
        {
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {

            var cell = tableView.DequeueReusableCell(MyTableViewCell.Key, indexPath) as MyTableViewCell;
            cell.BackgroundView = null;
            cell.BackgroundColor = UIColor.Clear;
            cell.UpdateCell(TableItems[indexPath.Row]);
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;

            return cell;



            //UITableViewCell cell = tableView.DequeueReusableCell("cell");
            //if (cell == null)
            //{
            //    cell = new UITableViewCell(UITableViewCellStyle.Default, "cell");
            //}
            //cell.TextLabel.Text = item;
            //return cell;

        }



        public override nint RowsInSection(UITableView tableview, nint section)
        {
            return  TableItems?.Count ?? 0;
        }
    }
}
    