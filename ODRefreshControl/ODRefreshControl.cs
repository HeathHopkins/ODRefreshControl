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

        public Action Action { get; set; }

        public bool Refreshing { get { return _refreshing; } }
        public bool WasManuallyStarted { get; private set; }

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
                return UIActivityIndicatorViewStyle.Gray;
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
            this.scrollView.AddObserver(this, new NSString("contentOffset"), NSKeyValueObservingOptions.New, IntPtr.Zero);
            this.scrollView.AddObserver(this, new NSString("contentInset"), NSKeyValueObservingOptions.New, IntPtr.Zero);

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
                if (this.scrollView != null)
                {
                    this.scrollView.RemoveObserver(this, new NSString("contentOffset"));
                    this.scrollView.RemoveObserver(this, new NSString("contentInset"));
                    this.scrollView = null;
                }
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

                    // removed behavior Heath 
                    //_canRefresh = false;
                    //dontDraw = true;
                }
                if (dontDraw)
                {
                    _shapeLayer.Path = null;
                    _shapeLayer.ShadowPath = new CGPath(IntPtr.Zero);
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

            float currentTopPadding = lerp(kMinTopPadding, kMaxTopPadding, percentage);
            float currentTopRadius = lerp(kMinTopRadius, kMaxTopRadius, percentage);
            float currentBottomRadius = lerp(kMinBottomRadius, kMaxBottomRadius, percentage);
            float currentBottomPadding = lerp(kMinBottomPadding, kMaxBottomPadding, percentage);

            PointF bottomOrigin = new PointF((float)Math.Floor(this.Bounds.Size.Width / 2), this.Bounds.Size.Height - currentBottomPadding - currentBottomRadius);
            PointF topOrigin = PointF.Empty;
            if (distance == 0)
            {
                topOrigin = new PointF((float)Math.Floor(this.Bounds.Size.Width / 2), bottomOrigin.Y);
            }
            else
            {
                topOrigin = new PointF((float)Math.Floor(this.Bounds.Size.Width / 2), this.Bounds.Size.Height + offset + currentTopPadding + currentTopRadius);
                if (percentage == 0)
                {
                    bottomOrigin.Y -= (float)Math.Abs(verticalShift) - kMaxDistance;
                    triggered = true;
                }
            }

            // top semicircle
            path.AddArc(topOrigin.X, topOrigin.Y, currentTopRadius, 0, (float)Math.PI, true);

            // left curve
            PointF leftCp1 = new PointF(lerp((topOrigin.X - currentTopRadius), (bottomOrigin.X - currentBottomRadius), 0.1f), lerp(topOrigin.Y, bottomOrigin.Y, 0.2f));
            PointF leftCp2 = new PointF(lerp((topOrigin.X - currentTopRadius), (bottomOrigin.X - currentBottomRadius), 0.9f), lerp(topOrigin.Y, bottomOrigin.Y, 0.2f));
            PointF leftDestination = new PointF(bottomOrigin.X - currentBottomRadius, bottomOrigin.Y);
            
            path.AddCurveToPoint(leftCp1, leftCp2, leftDestination);

            // bottom semicircle
            path.AddArc(bottomOrigin.X, bottomOrigin.Y, currentBottomRadius, (float)Math.PI, 0, true);
            
            // right curve
            PointF rightCp2 = new PointF(lerp((topOrigin.X + currentTopRadius), (bottomOrigin.X + currentBottomRadius), 0.1f), lerp(topOrigin.Y, bottomOrigin.Y, 0.2f));
            PointF rightCp1 = new PointF(lerp((topOrigin.X + currentTopRadius), (bottomOrigin.X + currentBottomRadius), 0.9f), lerp(topOrigin.Y, bottomOrigin.Y, 0.2f));
            PointF rightDestination = new PointF(bottomOrigin.X + currentTopRadius, topOrigin.Y);
            
            path.AddCurveToPoint (rightCp1, rightCp2, rightDestination);
            path.CloseSubpath();

            if (!triggered) // line 309
            {
                // set paths
                _shapeLayer.Path = path;
                _shapeLayer.ShadowPath = path;

                // add the arrow shape
                float currentArrowSize = lerp(kMinArrowSize, kMaxArrowSize, percentage);
                float currentArrowRadius = lerp(kMinArrowRadius, kMaxArrowRadius, percentage);
                float arrowBigRadius = currentArrowRadius + (currentArrowSize / 2);
                float arrowSmallRadius = currentArrowRadius - (currentArrowSize / 2);
                CGPath arrowPath = new CGPath();
                /*
                arrowPath.AddArc(topOrigin.X, topOrigin.Y, arrowBigRadius, 0, 3 * (float)Math.PI, false);
                arrowPath.AddLineToPoint(topOrigin.X, topOrigin.Y - arrowBigRadius - currentArrowSize);
                arrowPath.AddLineToPoint(topOrigin.X + (2 * currentArrowSize), topOrigin.Y - arrowBigRadius + (currentArrowSize / 2));
                arrowPath.AddLineToPoint(topOrigin.X, topOrigin.Y - arrowBigRadius + (2 * currentArrowSize));
                arrowPath.AddLineToPoint(topOrigin.X, topOrigin.Y - arrowBigRadius + currentArrowSize);
                arrowPath.AddArc(topOrigin.X, topOrigin.Y, arrowSmallRadius, 3 * (float)Math.PI, 0, true);
                */
                arrowPath.AddArc (topOrigin.X, topOrigin.Y, arrowBigRadius, 0, 3 * (float) Math.PI / 2.0f, false);
                arrowPath.AddLineToPoint (topOrigin.X, topOrigin.Y - arrowBigRadius - currentArrowSize);
                arrowPath.AddLineToPoint (topOrigin.X + (2 * currentArrowSize), topOrigin.Y - arrowBigRadius + (currentArrowSize / 2.0f));
                arrowPath.AddLineToPoint (topOrigin.X, topOrigin.Y - arrowBigRadius + (2 * currentArrowSize));
                arrowPath.AddLineToPoint (topOrigin.X, topOrigin.Y - arrowBigRadius + currentArrowSize);
                arrowPath.AddArc (topOrigin.X, topOrigin.Y, arrowSmallRadius, 3 * (float) Math.PI / 2.0f, 0, true);

                arrowPath.CloseSubpath();
                _arrowLayer.Path = arrowPath;
                _arrowLayer.FillRule = CAShapeLayer.FillRuleEvenOdd;
                arrowPath.Dispose();

                // add the highlight shape
                CGPath highlightPath = new CGPath();
                highlightPath.AddArc(topOrigin.X, topOrigin.Y, currentTopRadius, 0, (float)Math.PI, true);
                highlightPath.AddArc(topOrigin.X, topOrigin.Y + 1.25f, currentTopRadius, (float)Math.PI, 0, false);

                _highlightLayer.Path = highlightPath;
                _highlightLayer.FillRule = CAShapeLayer.FillRuleNonZero;
                highlightPath.Dispose();
            }
            else
            {
                // start the shape disappearance animation
                float radius = lerp(kMinBottomRadius, kMaxBottomRadius, 0.2f);
                CABasicAnimation pathMorph = CABasicAnimation.FromKeyPath("path");
                pathMorph.Duration = 0.15f;
                pathMorph.FillMode = CAFillMode.Forwards;
                pathMorph.RemovedOnCompletion = false;

                CGPath toPath = new CGPath();
                toPath.AddArc(topOrigin.X, topOrigin.Y, radius, 0, (float)Math.PI, true);
                toPath.AddCurveToPoint(topOrigin.X - radius, topOrigin.Y, topOrigin.X - radius, topOrigin.Y, topOrigin.X - radius, topOrigin.Y);
                toPath.AddArc(topOrigin.X, topOrigin.Y, radius, (float)Math.PI, 0, true);
                toPath.AddCurveToPoint(topOrigin.X + radius, topOrigin.Y, topOrigin.X + radius, topOrigin.Y, topOrigin.X + radius, topOrigin.Y);
                toPath.CloseSubpath();

                pathMorph.To = new NSValue(toPath.Handle);
                _shapeLayer.AddAnimation(pathMorph, null);
                
                CABasicAnimation shadowPathMorph = CABasicAnimation.FromKeyPath("shadowPath");
                shadowPathMorph.Duration = 0.15f;
                shadowPathMorph.FillMode = CAFillMode.Forwards;
                shadowPathMorph.RemovedOnCompletion = false;
                shadowPathMorph.To = new NSValue(toPath.Handle);
                
                _shapeLayer.AddAnimation(shadowPathMorph, null);
                toPath.Dispose();
                
                CABasicAnimation shapeAlphaAnimation = CABasicAnimation.FromKeyPath("opacity");
                shapeAlphaAnimation.Duration = 0.1f;
                shapeAlphaAnimation.BeginTime = CAAnimation.CurrentMediaTime() + 0.1f;
                shapeAlphaAnimation.To = new NSNumber(0);
                shapeAlphaAnimation.FillMode = CAFillMode.Forwards;
                shapeAlphaAnimation.RemovedOnCompletion = false;
                _shapeLayer.AddAnimation(shapeAlphaAnimation, null);
                
                CABasicAnimation alphaAnimation = CABasicAnimation.FromKeyPath("opacity");
                alphaAnimation.Duration = 0.1f;
                alphaAnimation.To = new NSNumber (0);
                alphaAnimation.FillMode = CAFillMode.Forwards;
                alphaAnimation.RemovedOnCompletion = false;
                
                _arrowLayer.AddAnimation(alphaAnimation, null);
                _highlightLayer.AddAnimation(alphaAnimation, null);
                
                CATransaction.Begin();
                CATransaction.DisableActions = true;
                activity.Layer.Transform = CATransform3D.MakeScale(0.1f, 0.1f, 1f);
                CATransaction.Commit();
                
                UIView.Animate (0.2f, 0.15f, UIViewAnimationOptions.CurveLinear, () => {
                    activity.Alpha = 1;
                    activity.Layer.Transform = CATransform3D.MakeScale(1, 1, 1);
                }, null);
                
                _refreshing = true;
                _canRefresh = false;
                this.SendActionForControlEvents(UIControlEvent.ValueChanged);

                if (this.Action != null)
                    this.Action();
            }
            path.Dispose();
        }

        public void BeginRefreshing()
        {
            if (_refreshing)
                return;

            CABasicAnimation alphaAnimation = CABasicAnimation.FromKeyPath("opacity");
            alphaAnimation.Duration = 0.0001f;
            alphaAnimation.To = new NSNumber(0);
            alphaAnimation.FillMode = CAFillMode.Forwards;
            alphaAnimation.RemovedOnCompletion = false;
            _shapeLayer.AddAnimation(alphaAnimation, null);
            _arrowLayer.AddAnimation(alphaAnimation, null);
            _highlightLayer.AddAnimation(alphaAnimation, null);

            this.activity.Center = new PointF((float)Math.Floor(this.Frame.Size.Width / 2), (float)Math.Min(this.Frame.Size.Height + Math.Floor(kOpenedViewHeight / 2), this.Frame.Size.Height - kOpenedViewHeight / 2));
            this.activity.Alpha = 1;
            this.activity.Layer.Transform = CATransform3D.MakeScale (1, 1, 1);
            
            PointF offset = this.scrollView.ContentOffset;
            _ignoreInset = true;
            this.scrollView.ContentInset = new UIEdgeInsets(kOpenedViewHeight + this.originalContentInset.Top, this.originalContentInset.Left, this.originalContentInset.Bottom, this.originalContentInset.Right);
            _ignoreInset = false;

            offset.Y -= kMaxDistance;
            this.scrollView.SetContentOffset(offset, false);

            _refreshing = true;
            _canRefresh = false;

            this.WasManuallyStarted = true;

            if (this.Action != null)
                this.Action();
        }

        public void EndRefreshing()
        {
            if (!_refreshing)
                return;

            _refreshing = false;
            // create a temporary retain-cycle, so the scrollview won't be released
            // halfway through the end animation.
            // this allows for the refresh control to clean up the observer,
            // in the case the scrollView is released while the animation is running

            UIView.Animate (0.4, () => {
                _ignoreInset = true;
                this.scrollView.ContentInset = originalContentInset;
                _ignoreInset = false;
                activity.Alpha = 0;
                activity.Layer.Transform = CATransform3D.MakeScale(0.1f, 0.1f, 1);
            }, () => {
                _shapeLayer.RemoveAllAnimations();
                _shapeLayer.Path = null;
                _shapeLayer.ShadowPath = new CGPath(IntPtr.Zero);
                _shapeLayer.Position = PointF.Empty;
                _arrowLayer.RemoveAllAnimations();
                _arrowLayer.Path = null;
                _highlightLayer.RemoveAllAnimations();
                _highlightLayer.Path = null;

                // we need to use the scrollview somehow in the end block,
                // or it'll get released in the animation block.  ?? not true for xamarin.ios ??
                _ignoreInset = true;
                this.scrollView.ContentInset = originalContentInset;
                _ignoreInset = false;

                this.WasManuallyStarted = false;
            });
        }

        static float lerp (float a, float b, float p)
        {
            return a + (b - a) * p;
        }
    }
}
