using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;
using Android.Text;
using Android.Graphics;

namespace Atropos.Utilities
{
    public class VerticalTextView : TextView
    {
        private bool m_topDown;
        public int _leftOffset, _topOffset;
        public float measuredWidth, measuredHeight, textSize;

        // TODO: Add all the other constructor overloadings
        public VerticalTextView(Context context) : base(context)
        {
            if (Android.Views.Gravity.IsVertical(Gravity) && (Gravity & GravityFlags.VerticalGravityMask) == GravityFlags.Bottom)
            {
                Gravity = (Gravity & GravityFlags.HorizontalGravityMask) | GravityFlags.Top;
                m_topDown = false;
            }
            else
            {
                m_topDown = true;
            }
        }

        public VerticalTextView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            if (Android.Views.Gravity.IsVertical(Gravity) && (Gravity & GravityFlags.VerticalGravityMask) == GravityFlags.Bottom)
            {
                Gravity = (Gravity & GravityFlags.HorizontalGravityMask) | GravityFlags.Top;
                m_topDown = false;
            }
            else
            {
                m_topDown = true;
            }
        }

        //protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        //{
        //    base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        //    SetMeasuredDimension(MeasuredHeight, MeasuredWidth);
        //}

        //protected override bool SetFrame(int l, int t, int r, int b)
        //{
        //    return base.SetFrame(l, t, l+(b-t), t+(r-l));
        //}

        //protected override void OnDraw(Android.Graphics.Canvas canvas)
        //{
        //    base.OnDraw(canvas);
        //    TextPaint textPaint = Paint;
        //    textPaint.Color = new Color(CurrentTextColor);
        //    // Maybe do this in a loop
        //    textPaint.DrawableState[0] = GetDrawableState()[0];

        //    canvas.Save();
        //    if (m_topDown)
        //    {
        //        canvas.Translate(Width, 0);
        //        canvas.Rotate(90);
        //    }
        //    else
        //    {
        //        canvas.Translate(0, Height);
        //        canvas.Rotate(-90);
        //    }
        //    canvas.Translate(CompoundPaddingLeft, CompoundPaddingTop);
        //    Layout.Draw(canvas);
        //    canvas.Restore();
        //}

        protected override void OnDraw(Canvas canvas)
        {
            //base.OnDraw(canvas);

            if (this.Text.Length > 0)
            {
                measuredWidth = MeasuredWidth;
                measuredHeight = MeasuredHeight;
                textSize = TextSize;

                float thX = this.MeasuredWidth / 2f;
                float thY = this.MeasuredHeight / 2f;
                float textS = this.TextSize / 2f;

                canvas.Translate((this.m_topDown) ? 65 : 143, (this.m_topDown) ? 250 - thY + _topOffset : 250 + thY + _topOffset);
                canvas.Rotate((this.m_topDown) ? 90 : -90);

                var paint = this.Paint;
                paint.Color = new Color(CurrentTextColor);
                canvas.DrawText(this.Text, 0, 0, paint);
            }
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            try
            {
                //Rect outRectangle = new Rect();
                //this.Paint.GetTextBounds(this.Text, 0, this.Text.Length, outRectangle);

                var _tempView = new TextView(Application.Context);
                _tempView.SetPadding(this.PaddingLeft, this.PaddingTop, this.PaddingRight, this.PaddingBottom);
                _tempView.Text = Text;
                _tempView.SetTextSize(ComplexUnitType.Px, this.TextSize);
                _tempView.SetTypeface(this.Typeface, this.Typeface?.Style ?? TypefaceStyle.Normal);

                _tempView.Measure(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);

                SetMeasuredDimension(_tempView.MeasuredHeight + 20, _tempView.MeasuredWidth + 20);

                //this._ascent = this._textBounds.height() / 2 + this._measuredWidth / 2;
            }
            catch (Exception e)
            {
                SetMeasuredDimension(widthMeasureSpec, heightMeasureSpec);
                Log.Error("RotatedText", e.ToString());
            }
        }

        public void SetTopDown(bool value)
        {
            m_topDown = value;
        }

        public void IncrementLength()
        {
            //var sign = (_leftOffset < 0) ? -1 : 1;
            //_leftOffset += sign;
            //_leftOffset *= -1;
            Text = "_" + Text + "_";
            RequestLayout();
            Invalidate();
        }

        public void DecrementLength()
        {
            //var sign = (_topOffset < 0) ? -1 : 1;
            //_topOffset += sign;
            //_topOffset *= -1;
            Text = Text.Substring(1, Text.Length - 2);
            RequestLayout();
            Invalidate();
        }
    }
}