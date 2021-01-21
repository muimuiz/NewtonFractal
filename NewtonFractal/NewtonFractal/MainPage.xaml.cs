using System;
using System.Numerics;
using Xamarin.Forms;
using SkiaSharp;
using SkiaSharp.Views.Forms;

namespace NewtonFractal
{

    public partial class MainPage : ContentPage
    {

        public ViewModel_c ViewModel;

        public MainPage()
        {
            InitializeComponent();
            ViewModel = new ViewModel_c();
            ViewModel.InvalidateCanvasAction = BitmapCanvasView.InvalidateSurface;
            BindingContext = ViewModel;
        }

        // MainPage code-behind treats BitmapView through conventional event handlings

        private SKPaint textPaint = new SKPaint {
            Style = SKPaintStyle.Fill,
            Color = SKColors.Black,
            TextSize = 40,
        };

        private void drawSolutionLabel(SKCanvas canvas, int offsX, int offsY, Complex z, string labelText)
        {
            (int x, int y) = ViewModel.BitmapManager.ComplexToBitmapXY(z);
            SKRect bounds = new SKRect();
            textPaint.MeasureText(labelText, ref bounds);
            canvas.DrawText(labelText, x + offsX - bounds.MidX, y + offsY - bounds.MidY, textPaint);
        }

        private void BitmapCanvasView_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKImageInfo info = e.Info;
            int w = info.Width;
            int h = info.Height;
            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear();

            int bitmapSize = (w < h) ? w : h;
            SKBitmap bitmap = ViewModel.BitmapManager.GetBitmap(bitmapSize, bitmapSize);
            int x0 = (w - bitmapSize) / 2;
            int y0 = (h - bitmapSize) / 2;
            SKRect rect = new SKRect(x0, y0, x0 + bitmapSize, y0 + bitmapSize);
            canvas.DrawBitmap(bitmap, rect);
            drawSolutionLabel(canvas, x0, y0, ViewModel.Alpha, "α");
            drawSolutionLabel(canvas, x0, y0, ViewModel.Beta,  "β");
            drawSolutionLabel(canvas, x0, y0, ViewModel.Gamma, "γ");

            return;
        }

        void BitmapCanvasView_PinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            // not implemented yet
        }

    } // MainPage

}
