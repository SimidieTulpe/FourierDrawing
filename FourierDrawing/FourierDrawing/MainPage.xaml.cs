using MathNet.Numerics.IntegralTransforms;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using TouchTracking;
using Xamarin.Forms;

namespace FourierDrawing
{
    public partial class MainPage : ContentPage
    {
        private const double animationFrequency = 25; // [s]
        private const double rotationFrequency = 0.05; // [s]
        private const int numberOfPoints = 100;
        private const float scaleFactor = 2f;

        private readonly Dictionary<long, SKPath> inProgressPaths = new Dictionary<long, SKPath>();
        private readonly List<SKPath> completedPaths = new List<SKPath>();
        private readonly Queue<(double, double)> fixPath = new Queue<(double, double)>();
        private readonly SKPath path = new SKPath();
        private readonly SKPath animatedPath = new SKPath();
        private readonly SKPaint fixPathPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Blue.WithAlpha(0x60),
            StrokeWidth = 5,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        private readonly SKPaint animatedPathPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Blue,
            StrokeWidth = 6,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,

        };
        private readonly SKPaint jointPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Red,
            StrokeWidth = 3,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        private readonly SKPaint circlePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Gray.WithAlpha(0x40),
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly Complex[] complexArray = new Complex[numberOfPoints];

        private SKPoint position;
        private bool pageIsActive;
        private bool isUpdating;
        private double currentAngle = 0; // ranges from 0 to 2 * Pi
        private float tipPositionX = 0;
        private float tipPositionY = 0;

        private string _stringNumberOfFrequencies;
        private int _intNumberOfFrequencies;
        private bool _followTip;

        public MainPage()
        {
            InitializeComponent();
            slider.Minimum = 0;
            slider.Maximum = numberOfPoints;
            IntNumberOfFrequencies = numberOfPoints;
        }

        public string StringNumberOfFrequencies
        {
            get => _stringNumberOfFrequencies;
            set
            {
                _stringNumberOfFrequencies = value;
                OnPropertyChanged(nameof(StringNumberOfFrequencies));
                if (!int.TryParse(value, out int result) || result > numberOfPoints || result < 0)
                {
                    return;
                };
                _intNumberOfFrequencies = result;
                OnPropertyChanged(nameof(IntNumberOfFrequencies));
            }
        }
        public int IntNumberOfFrequencies
        {
            get => _intNumberOfFrequencies;
            set
            {
                _stringNumberOfFrequencies = value.ToString();
                _intNumberOfFrequencies = value;
                OnPropertyChanged(nameof(StringNumberOfFrequencies));
                OnPropertyChanged(nameof(IntNumberOfFrequencies));
            }
        }

        public bool FollowTip
        {
            get => _followTip;
            set
            {
                _followTip = value;
                OnPropertyChanged(nameof(FollowTip));
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
                        //canvasView.InvalidateSurface();
                    }
                    break;

                case TouchActionType.Moved:
                    if (inProgressPaths.ContainsKey(args.Id))
                    {
                        SKPath path = inProgressPaths[args.Id];
                        path.LineTo(ConvertToPixel(args.Location));
                        //canvasView.InvalidateSurface();
                    }
                    break;

                case TouchActionType.Released:
                    if (inProgressPaths.ContainsKey(args.Id))
                    {
                        completedPaths.Clear();
                        inProgressPaths[args.Id].Close();
                        completedPaths.Add(inProgressPaths[args.Id]);
                        inProgressPaths.Remove(args.Id);
                        fixPath.Clear();
                        //canvasView.InvalidateSurface();
                        CreateFourierSerie();
                    }
                    break;

                case TouchActionType.Cancelled:
                    if (inProgressPaths.ContainsKey(args.Id))
                    {
                        inProgressPaths.Remove(args.Id);
                        //canvasView.InvalidateSurface();
                    }
                    break;
            }
        }

        void CreateFourierSerie()
        {
            SKPathMeasure pathMeasure = new SKPathMeasure(completedPaths.First(), true, 1);
            for (int i = 0; i < numberOfPoints; i++)
            {
                pathMeasure.GetPosition(pathMeasure.Length / numberOfPoints * i, out position);
                complexArray[i] = new Complex(position.X, position.Y);
            }

            Fourier.Forward(complexArray, FourierOptions.NoScaling);
        }

        SKPoint ConvertToPixel(Point pt)
        {
            return new SKPoint((float)(canvasView.CanvasSize.Width * pt.X / canvasView.Width),
                               (float)(canvasView.CanvasSize.Height * pt.Y / canvasView.Height));
        }

        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            if (isUpdating == true) return;
            isUpdating = true;

            SKCanvas canvas = args.Surface.Canvas;
            canvas.Clear();

            animatedPath.Reset();

            if (!inProgressPaths.Any() && FollowTip)
            {
                var xTranslate = args.Info.Width / 2f - tipPositionX * scaleFactor;
                var yTranslate = args.Info.Height / 2f - tipPositionY * scaleFactor;
                canvas.Translate(xTranslate, yTranslate);
                canvas.Scale(scaleFactor);
            }

            if (completedPaths.Any())
            {
                var x0 = complexArray[0].Magnitude / complexArray.Length * Math.Cos(complexArray[0].Phase);
                var y0 = complexArray[0].Magnitude / complexArray.Length * Math.Sin(complexArray[0].Phase);

                animatedPath.MoveTo((float)x0, (float)y0);

                var frequencies = new int[numberOfPoints];
                for (int i = 1; i < complexArray.Length; i++)
                {
                    if (i > complexArray.Length / 2) frequencies[i] = -(complexArray.Length - i);
                    else frequencies[i] = i;
                }

                var idxs = complexArray
                    .Select((x, i) => new KeyValuePair<Complex, int>(x, i)).Where(i => i.Value > 0)
                    .OrderByDescending(x => x.Key.Magnitude)
                    .Select(x => x.Value)
                    .Take(_intNumberOfFrequencies);

                foreach (var idx in idxs)
                {
                    if (idx > complexArray.Length / 2) frequencies[idx] = -(complexArray.Length - idx);

                    var xi = x0 + complexArray[idx].Magnitude / complexArray.Length * Math.Cos(frequencies[idx] * currentAngle + complexArray[idx].Phase);
                    var yi = y0 + complexArray[idx].Magnitude / complexArray.Length * Math.Sin(frequencies[idx] * currentAngle + complexArray[idx].Phase);
                    animatedPath.LineTo((float)xi, (float)yi);

                    canvas.DrawCircle((float)x0, (float)y0, (float)(complexArray[idx].Magnitude / complexArray.Length), circlePaint);

                    x0 = xi;
                    y0 = yi;
                }

                tipPositionX = (float)x0;
                tipPositionY = (float)y0;

                path.Reset();
                fixPath.Enqueue((x0, y0));
                if (fixPath.Count > animationFrequency / rotationFrequency * 0.98) fixPath.Dequeue();
                if (fixPath.Count > 2)
                {
                    path.MoveTo((float)fixPath.Peek().Item1, (float)fixPath.Peek().Item2);
                    foreach (var item in fixPath)
                    {
                        path.LineTo((float)item.Item1, (float)item.Item2);
                    }
                }

                canvas.DrawPath(path, animatedPathPaint);
                canvas.DrawPath(animatedPath, jointPaint);
            }

            foreach (var path in completedPaths)
            {
                canvas.DrawPath(path, fixPathPaint);
            }
            foreach (var path in inProgressPaths.Values)
            {
                canvas.DrawPath(path, fixPathPaint);
            }

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
            FollowTip = false;
            completedPaths.Clear();
            inProgressPaths.Clear();
            fixPath.Clear();
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

        private void Entry_Unfocused(object sender, FocusEventArgs e)
        {
            _stringNumberOfFrequencies = _intNumberOfFrequencies.ToString();
            OnPropertyChanged(nameof(StringNumberOfFrequencies));
        }
    }
}
