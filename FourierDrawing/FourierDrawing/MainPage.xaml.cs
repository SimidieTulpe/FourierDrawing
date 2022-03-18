using MathNet.Numerics.IntegralTransforms;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using FourierDrawing.TouchAction;
using Xamarin.Forms;

namespace FourierDrawing
{
    public partial class MainPage
    {
        private const double AnimationFrequency = 25; // [Hz]
        private const double RotationFrequency = 0.05; // [Hz]
        private const int NumberOfPoints = 100;
        private const float ScaleFactor = 2f;

        private readonly Dictionary<long, SKPath> _inProgressPaths = new Dictionary<long, SKPath>();
        private readonly List<SKPath> _completedPaths = new List<SKPath>();
        private readonly Queue<(double, double)> _fixPath = new Queue<(double, double)>();
        private readonly SKPath _path = new SKPath();
        private readonly SKPath _animatedPath = new SKPath();
        private readonly SKPaint _fixPathPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.CornflowerBlue.WithAlpha(0x60),
            StrokeWidth = 5,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        private readonly SKPaint _animatedPathPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Blue,
            StrokeWidth = 8,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
        private readonly SKPaint _jointPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Red,
            StrokeWidth = 3,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        private readonly SKPaint _circlePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Gray.WithAlpha(0x40),
            StrokeWidth = 3,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly Complex[] _complexArray = new Complex[NumberOfPoints];

        private SKPoint _position;
        private bool _pageIsActive;
        private bool _isUpdating;
        private double _currentAngle; // ranges from 0 to 2 * Pi
        private float _tipPositionX;
        private float _tipPositionY;

        private string _stringNumberOfFrequencies;
        private int _intNumberOfFrequencies;
        private bool _followTip;

        public MainPage()
        {
            InitializeComponent();
            slider.Minimum = 0;
            slider.Maximum = NumberOfPoints;
            IntNumberOfFrequencies = NumberOfPoints;
        }

        public string StringNumberOfFrequencies
        {
            get => _stringNumberOfFrequencies;
            set
            {
                _stringNumberOfFrequencies = value;
                OnPropertyChanged(nameof(StringNumberOfFrequencies));
                if (!int.TryParse(value, out int result) || result > NumberOfPoints || result < 0)
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

        private void OnTouchEffectAction(object sender, TouchActionEventArgs args)
        {
            switch (args.Type)
            {
                case TouchActionType.Pressed:
                    if (_inProgressPaths.Count != 0) return;
                    if (!_inProgressPaths.ContainsKey(args.Id))
                    {
                        var path = new SKPath();
                        path.MoveTo(ConvertToPixel(args.Location));
                        _inProgressPaths.Add(args.Id, path);
                    }
                    break;

                case TouchActionType.Moved:
                    if (_inProgressPaths.ContainsKey(args.Id))
                    {
                        var path = _inProgressPaths[args.Id];
                        path.LineTo(ConvertToPixel(args.Location));
                    }
                    break;

                case TouchActionType.Released:
                    if (_inProgressPaths.ContainsKey(args.Id))
                    {
                        _completedPaths.Clear();
                        _inProgressPaths[args.Id].Close();
                        _completedPaths.Add(_inProgressPaths[args.Id]);
                        _inProgressPaths.Remove(args.Id);
                        _fixPath.Clear();
                        CreateFourierSerie();
                    }
                    break;

                case TouchActionType.Cancelled:
                    if (_inProgressPaths.ContainsKey(args.Id))
                    {
                        _inProgressPaths.Remove(args.Id);
                    }
                    break;
            }
        }

        private void CreateFourierSerie()
        {
            var pathMeasure = new SKPathMeasure(_completedPaths.First(), true, 1);
            for (var i = 0; i < NumberOfPoints; i++)
            {
                pathMeasure.GetPosition(pathMeasure.Length / NumberOfPoints * i, out _position);
                _complexArray[i] = new Complex(_position.X, _position.Y);
            }

            Fourier.Forward(_complexArray, FourierOptions.NoScaling);
        }

        private SKPoint ConvertToPixel(Point pt)
        {
            return new SKPoint((float)(canvasView.CanvasSize.Width * pt.X / canvasView.Width),
                               (float)(canvasView.CanvasSize.Height * pt.Y / canvasView.Height));
        }

        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            if (_isUpdating == true) return;
            _isUpdating = true;

            var canvas = args.Surface.Canvas;
            canvas.Clear();

            _animatedPath.Reset();

            if (!_inProgressPaths.Any() && FollowTip)
            {
                var xTranslate = args.Info.Width / 2f - _tipPositionX * ScaleFactor;
                var yTranslate = args.Info.Height / 2f - _tipPositionY * ScaleFactor;
                canvas.Translate(xTranslate, yTranslate);
                canvas.Scale(ScaleFactor);
            }

            if (_completedPaths.Any())
            {
                var x0 = _complexArray[0].Magnitude / _complexArray.Length * Math.Cos(_complexArray[0].Phase);
                var y0 = _complexArray[0].Magnitude / _complexArray.Length * Math.Sin(_complexArray[0].Phase);

                _animatedPath.MoveTo((float)x0, (float)y0);

                var frequencies = new int[NumberOfPoints];
                for (var i = 1; i < _complexArray.Length; i++)
                {
                    if (i > _complexArray.Length / 2) frequencies[i] = -(_complexArray.Length - i);
                    else frequencies[i] = i;
                }

                var idxs = _complexArray
                    .Select((x, i) => new KeyValuePair<Complex, int>(x, i)).Where(i => i.Value > 0)
                    .OrderByDescending(x => x.Key.Magnitude)
                    .Select(x => x.Value)
                    .Take(_intNumberOfFrequencies);

                foreach (var idx in idxs)
                {
                    if (idx > _complexArray.Length / 2) frequencies[idx] = -(_complexArray.Length - idx);

                    var xi = x0 + _complexArray[idx].Magnitude / _complexArray.Length * Math.Cos(frequencies[idx] * _currentAngle + _complexArray[idx].Phase);
                    var yi = y0 + _complexArray[idx].Magnitude / _complexArray.Length * Math.Sin(frequencies[idx] * _currentAngle + _complexArray[idx].Phase);
                    _animatedPath.LineTo((float)xi, (float)yi);

                    canvas.DrawCircle((float)x0, (float)y0, (float)(_complexArray[idx].Magnitude / _complexArray.Length), _circlePaint);

                    x0 = xi;
                    y0 = yi;
                }

                _tipPositionX = (float)x0;
                _tipPositionY = (float)y0;

                _path.Reset();
                _fixPath.Enqueue((x0, y0));
                if (_fixPath.Count > AnimationFrequency / RotationFrequency * 0.98) _fixPath.Dequeue();
                if (_fixPath.Count > 2)
                {
                    _path.MoveTo((float)_fixPath.Peek().Item1, (float)_fixPath.Peek().Item2);
                    foreach (var item in _fixPath)
                    {
                        _path.LineTo((float)item.Item1, (float)item.Item2);
                    }
                }

                canvas.DrawPath(_path, _animatedPathPaint);
                canvas.DrawPath(_animatedPath, _jointPaint);
            }

            foreach (var path in _completedPaths)
            {
                canvas.DrawPath(path, _fixPathPaint);
            }
            foreach (var path in _inProgressPaths.Values)
            {
                canvas.DrawPath(path, _fixPathPaint);
            }

            _isUpdating = false;
        }

        private async Task AnimationLoop()
        {
            _stopwatch.Start();

            while (_pageIsActive)
            {
                canvasView.InvalidateSurface();
                _currentAngle = (_currentAngle + RotationFrequency / AnimationFrequency * 2 * Math.PI) % (2 * Math.PI);
                var timeToDelay = 1 / AnimationFrequency - _stopwatch.Elapsed.TotalSeconds;
                await Task.Delay(TimeSpan.FromSeconds(timeToDelay));
                _stopwatch.Restart();
            }

            _stopwatch.Stop();
        }

        private void Button_Clicked(object sender, System.EventArgs e)
        {
            FollowTip = false;
            _completedPaths.Clear();
            _inProgressPaths.Clear();
            _fixPath.Clear();
            canvasView.InvalidateSurface();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _pageIsActive = true;
            _ = AnimationLoop();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _pageIsActive = false;
        }

        private void Entry_Unfocused(object sender, FocusEventArgs e)
        {
            _stringNumberOfFrequencies = _intNumberOfFrequencies.ToString();
            OnPropertyChanged(nameof(StringNumberOfFrequencies));
        }
    }
}
