using System;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using ODRefreshControl;

using System.Threading.Tasks;

namespace Demo
{
    public class TestViewController : UIViewController
    {
        UITableView tableview;
        ODRefreshControl.ODRefreshControl refreshControl;

        public TestViewController()
            : base ()
        {
        }

        public override void LoadView()
        {
            var view = new UIView(UIScreen.MainScreen.Bounds)
            {
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
                BackgroundColor = UIColor.White
            };

            tableview = new UITableView(view.Frame, UITableViewStyle.Grouped)
            {
                Source = new DataSource(),
                BackgroundColor = UIColor.White,
                BackgroundView = null,
                AutoresizingMask = view.AutoresizingMask
            };
            view.AddSubview(tableview);

            refreshControl = new ODRefreshControl.ODRefreshControl(tableview);
            refreshControl.Action = delegate() 
            {
                Task.Factory.StartNew(() => {
                    Console.WriteLine("sleeping for 1.5)");
                    System.Threading.Thread.Sleep(1500);
                }).ContinueWith(t => {
                    Console.WriteLine("done");
                    refreshControl.EndRefreshing();
                }, TaskScheduler.FromCurrentSynchronizationContext());
            };


            this.View = view;
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            refreshControl.BeginRefreshing();
            //Console.WriteLine("refreshing: " + refreshControl.IsRefreshing.ToString());
        }

        [Obsolete ("Deprecated in iOS6. Replace it with both GetSupportedInterfaceOrientations and PreferredInterfaceOrientationForPresentation")]
        public override bool ShouldAutorotateToInterfaceOrientation(UIInterfaceOrientation toInterfaceOrientation)
        {
            return true;
        }

        class DataSource : UITableViewSource
        {
            public DataSource()
            {
            }

            public override int NumberOfSections(UITableView tableView)
            {
                return 1;
            }

            public override int RowsInSection(UITableView tableView, int section)
            {
                return 20;
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                string cellid = "row";

                var cell = tableView.DequeueReusableCell(cellid);
                if (cell == null)
                    cell = new UITableViewCell(UITableViewCellStyle.Default, cellid);

                cell.TextLabel.Text = indexPath.Row.ToString();

                return cell;
            }
        }
    }
}

