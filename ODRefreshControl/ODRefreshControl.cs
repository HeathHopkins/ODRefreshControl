using System;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.CoreAnimation;
using MonoTouch.CoreGraphics;

namespace ODRefreshControl
{
    public class ODRefreshControl : UIControl
    {
        CAShapeLayer _shapeLayer, _arrowLayer, _highlightLayer;
        UIActivityIndicatorView activity;
        bool _refreshing, _canRefresh, _ignoreInset, _ignoreOffset, _didSetInset, _hasSectionHeaders;
        float _lastOffset;
        UIScrollView scrollView;
        UIEdgeInsets originalContentInset;

        public bool Refreshing { get { return _refreshing; } }

        public UIColor ActivityIndicatorViewColor
        { 
            get
            {
                if (this.activity != null)
                    return this.activity.Color;
                return null;
            }
            set
            {
                if (this.activity != null)
                    this.activity.Color = value;
            }
        }


        public UIActivityIndicatorViewStyle ActivityIndicatorViewStyle 
        { 
            get
            {
                if (this.activity != null)
                    return this.activity.ActivityIndicatorViewStyle;
                return (UIActivityIndicatorViewStyle)0;
            }
            set
            {
                if (this.activity != null)
                    this.activity.ActivityIndicatorViewStyle = value;
            }
        }

        private UIColor _tintColor;
        public UIColor TintColor 
        { 
            get
            {
                return _tintColor;
            }
            set
            {
                _tintColor = value;
                if (_shapeLayer != null)
                    _shapeLayer.FillColor = _tintColor.CGColor;
            }
        }

        const float kTotalViewHeight = 400;
        const float kOpenedViewHeight = 44;
        const float kMinTopPadding = 9;
        const float kMaxTopPadding = 5;
        const float kMinTopRadius = 12.5f;
        const float kMaxTopRadius = 16;
        const float kMinBottomRadius = 3;
        const float kMaxBottomRadius = 16;
        const float kMinBottomPadding = 4;
        const float kMaxBottomPadding = 6;
        const float kMinArrowSize = 2;
        const float kMaxArrowSize = 3;
        const float kMinArrowRadius = 5;
        const float kMaxArrowRadius = 7;
        const float kMaxDistance = 53;

        public ODRefreshControl(UIScrollView scrollView)
            : this (scrollView, null)
        {
        }

        public ODRefreshControl(UIScrollView scrollView, UIActivityIndicatorView activity)
            : base (new RectangleF(0, -1 * (kTotalViewHeight + scrollView.ContentInset.Top), scrollView.Frame.Size.Width, kTotalViewHeight))
        {
            this.scrollView = scrollView;
            this.originalContentInset = this.scrollView.ContentInset;

            this.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            this.scrollView.AddSubview(this);
            //scrollView.AddObserver

            if (activity == null)
                activity = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Gray);
            this.activity = activity;
            this.activity.Center = new PointF((float)Math.Floor(this.Frame.Size.Width / 2), (float)Math.Floor(this.Frame.Size.Height / 2));
            this.activity.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleRightMargin;
            this.activity.Alpha = 0;
            this.activity.StartAnimating();
            this.AddSubview(this.activity);

            _refreshing = false;
            _canRefresh = true;
            _ignoreInset = false;
            _ignoreOffset = false;
            _didSetInset = false;
            _hasSectionHeaders = false;
            TintColor = UIColor.FromRGBA(155 / 255, 162 / 255, 172 / 255, 1.0f);

            _shapeLayer = new CAShapeLayer();
            _shapeLayer.FillColor = TintColor.CGColor;
            _shapeLayer.StrokeColor = UIColor.DarkGray.ColorWithAlpha(0.5f).CGColor;
            _shapeLayer.LineWidth = 0.5f;
            _shapeLayer.ShadowColor = UIColor.Black.CGColor;
            _shapeLayer.ShadowOffset = new SizeF(0, 1);
            _shapeLayer.ShadowOpacity = 0.4f;
            _shapeLayer.ShadowRadius = 0.5f;
            this.Layer.AddSublayer(_shapeLayer);

            _arrowLayer = new CAShapeLayer();
            _arrowLayer.StrokeColor = UIColor.DarkGray.ColorWithAlpha(0.5f).CGColor;
            _arrowLayer.LineWidth = 0.5f;
            _arrowLayer.FillColor = UIColor.White.CGColor;
            _shapeLayer.AddSublayer(_arrowLayer);

            _highlightLayer = new CAShapeLayer();
            _highlightLayer.FillColor = UIColor.White.ColorWithAlpha(0.2f).CGColor;
            _shapeLayer.AddSublayer(_highlightLayer);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // remove observers

                this.scrollView = null;
            }
            base.Dispose(disposing);
        }

        public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
        {
            //base.ObserveValue(keyPath, ofObject, change, context);
            if (keyPath.ToString().ToLower() == "contentInset".ToLower())
            {
                if (!_ignoreInset)
                {
                    this.originalContentInset = (change["new"] as NSValue).UIEdgeInsetsValue;
                    this.Frame = new RectangleF(0, -1 * (kTotalViewHeight + this.scrollView.ContentInset.Top), this.scrollView.Frame.Size.Width, kTotalViewHeight);
                }
                return;
            }

            if (!this.Enabled || this._ignoreOffset)
                return;

            float offset = (change["new"] as NSValue).PointFValue.Y + this.originalContentInset.Top;

            if (_refreshing)
            {
                if (offset != 0)
                {
                    // keep thing pinned at the top

                    CATransaction.Begin();
                    CATransaction.SetValueForKey(NSNumber.FromBoolean(true), CATransaction.DisableActionsKey);
                    _shapeLayer.Position = new PointF(0, kMaxDistance + offset + kOpenedViewHeight);
                    CATransaction.Commit();

                    this.activity.Center = new PointF((float)Math.Floor(this.Frame.Size.Width / 2), (float)Math.Min(offset + this.Frame.Size.Height + Math.Floor(kOpenedViewHeight / 2), this.Frame.Size.Height - kOpenedViewHeight / 2));

                    _ignoreInset = true;
                    _ignoreOffset = true;

                    if (offset < 0)
                    {
                        // set the inset depending on the situation
                        if (offset >= kOpenedViewHeight * -1)
                        {
                            if (!this.scrollView.Dragging)
                            {
                                if (!_didSetInset)
                                {
                                    _didSetInset = true;
                                    _hasSectionHeaders = false;
                                    if (this.scrollView is UITableView)
                                    {
                                        for (int i = 0; i < (this.scrollView as UITableView).NumberOfSections(); ++i)
                                        {
                                            if ((this.scrollView as UITableView).RectForHeaderInSection(i).Size.Height != 0)
                                            {
                                                _hasSectionHeaders = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                if (_hasSectionHeaders)
                                    this.scrollView.ContentInset = new UIEdgeInsets(Math.Min(offset * -1, kOpenedViewHeight) + this.originalContentInset.Top, this.originalContentInset.Left, this.originalContentInset.Bottom, this.originalContentInset.Right);
                                else
                                    this.scrollView.ContentInset = new UIEdgeInsets(kOpenedViewHeight + this.originalContentInset.Top, this.originalContentInset.Left, this.originalContentInset.Bottom, this.originalContentInset.Right);
                            }
                            else if(_didSetInset && _hasSectionHeaders)
                            {
                                this.scrollView.ContentInset = new UIEdgeInsets(-1 * offset + this.originalContentInset.Top, this.originalContentInset.Left, this.originalContentInset.Bottom, this.originalContentInset.Right);
                            }
                        }
                    }
                    else if (_hasSectionHeaders)
                    {
                        this.scrollView.ContentInset = this.originalContentInset;
                    }
                    _ignoreInset = false;
                    _ignoreOffset = false;
                }
                return;
            }
            else
            {
                // check if we can trigger a new refresh and if we can draw the control
                bool dontDraw = false;  // line 230
                if (!_canRefresh)
                {
                    if (offset >= 0)
                    {
                        // we can refresh again after the control is scrolled out of view
                        _canRefresh = true;
                        _didSetInset = false;
                    }
                    else
                    {
                        dontDraw = true;
                    }
                }
                else
                {
                    if (offset >= 0)
                    {
                        // don't draw if the control is not visible
                        dontDraw = true;
                    }
                }
                if (offset > 0 && _lastOffset > offset  && !this.scrollView.Tracking)
                {
                    // if we are scrolling too fast, don't draw, and don't trigger unless the scrollView bounced back
                    _canRefresh = false;
                    dontDraw = true;
                }
                if (dontDraw)
                {
                    _shapeLayer.Path = null;
                    _shapeLayer.ShadowPath = null;
                    _arrowLayer.Path = null;
                    _highlightLayer.Path = null;
                    _lastOffset = offset;
                    return;
                }
            }

            _lastOffset = offset;  // line 260

            bool triggered = false;

            CGPath path = new CGPath();

            // calculate some useful points and values
            float verticalShift = (float)Math.Max(0, -1 * ((kMaxTopRadius + kMaxBottomRadius + kMaxTopPadding + kMaxBottomPadding) + offset));
            float distance = (float)Math.Min(kMaxDistance, (float)Math.Abs(verticalShift));
            float percentage = 1 - (distance / kMaxDistance);

            float currentTopPadding = Math.


        }

    }
}

