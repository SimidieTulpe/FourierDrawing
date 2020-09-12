using SkiaSharp;
using SkiaSharp.Views.Forms;
using System.Collections.Generic;
using Xamarin.Forms;
using TouchTracking;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Threading.Tasks;
using System.Diagnostics;
using System;

namespace FourierDrawing
{
    public partial class MainPage : ContentPage
    {
        private readonly Dictionary<long, SKPath> inProgressPaths = new Dictionary<long, SKPath>();
        private readonly List<SKPath> completedPaths = new List<SKPath>();
        private SKPath animatedPath = new SKPath();
        private SKPath fixPath = new SKPath();
        private readonly SKPaint paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Blue,
            StrokeWidth = 10,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        private readonly SKPaint paint1 = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Red,
            StrokeWidth = 10,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        private int numberOfFrequencies;

        Stopwatch stopwatch = new Stopwatch();
        bool pageIsActive;
        private bool isUpdating;

        private const double animationFrequency = 20; // [s]
        private const double rotationFrequency = 0.1; // [s]
        private double currentAngle = 0; // ranges from 0 to 2 * Pi
        const int numberOfPoints = 100;

        
        private readonly double[] x = new double[numberOfPoints];
        //private readonly double[] x = new double[] { 100, 50, 0, 50};
        //private readonly double[] x = new double[] { 0, 10, 20, 30, 40, 50, 60, 50, 40, 30, 20, 10 };
        private readonly double[] y = new double[numberOfPoints];
        //private readonly double[] y = new double[] { 0, 50, 0, -50};
        //private readonly double[] y = new double[] { 0, 10, 15, 10, -10, -20, -10, 10, 20, 10, 10, 5 };

        Complex[] complexPath;

        public MainPage()
        {
            InitializeComponent();

            for (int i = 0; i < numberOfPoints; i++)
            {
                var t = i * Math.PI * 2 / numberOfPoints;
                x[i] = 100 * Math.Cos(t*1) + 20 * Math.Cos(t * 10);
                y[i] = 100 * Math.Sin(t*1);
            }

            for (int i = 0; i < x.Length; i++)
            {
                x[i] = x[i] + 500;
            }
            for (int i = 0; i < y.Length; i++)
            {
                y[i] = y[i] + 500;
            }

            complexPath = new Complex[x.Length];


            for (int i = 0; i < x.Length; i++)
            {
                complexPath[i] = new Complex(x[i], y[i]);
            }

            

            


            Fourier.Forward(complexPath, FourierOptions.NoScaling);
        }

        public int NumberOfFrequencies
        {
            get => numberOfFrequencies;
            set
            {
                if (value == numberOfFrequencies) return;
                numberOfFrequencies = value;
                OnPropertyChanged(nameof(NumberOfFrequencies));
                canvasView.InvalidateSurface();
            }
        }

        void OnTouchEffectAction(object sender, TouchActionEventArgs args)
        {
            switch (args.Type)
            {
                case TouchActionType.Pressed:
                    if (inProgressPaths.Count != 0) return;
                    if (!inProgressPaths.ContainsKey(args.Id))
                    {
                        SKPath path = new SKPath();
                        path.MoveTo(ConvertToPixel(args.Location));
                        inProgressPaths.Add(args.Id, path);
                        canvasView.InvalidateSurface();
                    }
                    break;

                case TouchActionType.Moved:
                    if (inProgressPaths.ContainsKey(args.Id))
                    {
                        SKPath path = inProgressPaths[args.Id];
                        path.LineTo(ConvertToPixel(args.Location));
                        canvasView.InvalidateSurface();
                    }
                    break;

                case TouchActionType.Released:
                    if (inProgressPaths.ContainsKey(args.Id))
                    {
                        completedPaths.Add(inProgressPaths[args.Id]);
                        inProgressPaths.Remove(args.Id);
                        canvasView.InvalidateSurface();
                    }
                    break;

                case TouchActionType.Cancelled:
                    if (inProgressPaths.ContainsKey(args.Id))
                    {
                        inProgressPaths.Remove(args.Id);
                        canvasView.InvalidateSurface();
                    }
                    break;
            }
        }

        SKPoint ConvertToPixel(Point pt)
        {
            return new SKPoint((float)(canvasView.CanvasSize.Width * pt.X / canvasView.Width),
                               (float)(canvasView.CanvasSize.Height * pt.Y / canvasView.Height));
        }

        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            var xCenter = args.Info.Width / 2f;
            var yCenter = args.Info.Height / 2f;
            if (isUpdating == true) return;
            isUpdating = true;

            SKCanvas canvas = args.Surface.Canvas;
            canvas.Clear();

            foreach (var path in completedPaths)
            {
                canvas.DrawPath(path, paint);
            }

            foreach (var path in inProgressPaths.Values)
            {
                canvas.DrawPath(path, paint);
            }

            fixPath.MoveTo((float)x[0], (float)y[0]);
            for (int i = 1; i < x.Length; i++)
            {
                fixPath.LineTo((float)x[i], (float)y[i]);
            }
            canvas.DrawPath(fixPath, paint);

            animatedPath.Reset();
            var x0 = complexPath[0].Magnitude / complexPath.Length * Math.Cos(complexPath[0].Phase);
            var y0 = complexPath[0].Magnitude / complexPath.Length * Math.Sin(complexPath[0].Phase);
            animatedPath.MoveTo((float)x0, (float)y0);

            for (int i = 1; i < complexPath.Length; i++)
            {
                int frequency = i;
                if (i > complexPath.Length / 2)
                {
                    frequency = - (complexPath.Length - i);
                }

                var xi = x0 + complexPath[i].Magnitude / complexPath.Length * Math.Cos(frequency * currentAngle + complexPath[i].Phase);
                var yi = y0 + complexPath[i].Magnitude / complexPath.Length * Math.Sin(frequency * currentAngle + complexPath[i].Phase);
                animatedPath.LineTo((float)xi, (float)yi);
                x0 = xi;
                y0 = yi;
            }
            canvas.DrawPath(animatedPath, paint1);

            isUpdating = false;
        }
        async Task AnimationLoop()
        {
            stopwatch.Start();

            while (pageIsActive)
            {
                canvasView.InvalidateSurface();
                currentAngle = (currentAngle + rotationFrequency / animationFrequency * 2 * Math.PI) % (2 * Math.PI);
                var timeToDelay = 1 / animationFrequency - stopwatch.Elapsed.TotalSeconds;
                await Task.Delay(TimeSpan.FromSeconds(timeToDelay));
                stopwatch.Restart();
            }

            stopwatch.Stop();
        }

        private void Button_Clicked(object sender, System.EventArgs e)
        {
            completedPaths.Clear();
            inProgressPaths.Clear();
            canvasView.InvalidateSurface();
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();
            pageIsActive = true;
            AnimationLoop();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            pageIsActive = false;
        }
    }
}
