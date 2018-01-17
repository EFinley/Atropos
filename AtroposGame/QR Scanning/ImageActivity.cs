
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
using Android;

namespace Atropos
{
    [Activity(Label = "ImageActivity")]
    public class QRImageActivity : Activity
    {
        ImageView imageBarcode;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            SetContentView(Resource.Layout.QRImageActivity);

            imageBarcode = FindViewById<ImageView>(Resource.Id.imageBarcode);

            var barcodeWriter = new ZXing.Mobile.BarcodeWriter
            {
                Format = ZXing.BarcodeFormat.QR_CODE,
                Options = new ZXing.Common.EncodingOptions
                {
                    Width = 300,
                    Height = 300
                }
            };
            var barcode = barcodeWriter.Write("ZXing.Net.Mobile");

            imageBarcode.SetImageBitmap(barcode);
        }
    }
}
