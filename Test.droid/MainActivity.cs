using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

using Android.Graphics;
using System.Threading.Tasks;
using System.IO;

namespace Test
{
	[Activity (Label = "HttpStreamTest", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);
            loadAsync();
		}

        async Task loadAsync()
        {
            var cacheFileName = System.IO.Path.Combine(Application.CacheDir.AbsolutePath, "cache.pdf");
            var fs = File.Create(cacheFileName);

            var imageView = FindViewById<ImageView>(Resource.Id.imageView1);
            imageView.Visibility = ViewStates.Gone;
            var progBar = FindViewById<ProgressBar>(Resource.Id.progressBar1);

            var uri = new Uri(@"https://dl.dropboxusercontent.com/u/150906/trc_m.jpg");
            var httpStream = new HttpStream.HttpStream(uri, fs, true);
            httpStream.RangeDownloaded += (sender, e) =>
            {
                progBar.Max = 1000;
                progBar.Progress = (int)(1000 * httpStream.CoveredRatio);
            };

            var bmp = await BitmapFactory.DecodeStreamAsync(httpStream);

            imageView.SetImageBitmap(bmp);
            imageView.Visibility = ViewStates.Visible;
        }
    }
}


