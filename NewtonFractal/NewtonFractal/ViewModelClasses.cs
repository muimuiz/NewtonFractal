using System;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using System.ComponentModel;
using System.Windows.Input;
using System.Runtime.CompilerServices;
using Xamarin.Forms;
using SkiaSharp;

namespace NewtonFractal
{

    public class TaskManager_c
    {

        private Model_c model;

        private Task task;
        private Progress<double?> reporter;
        private CancellationTokenSource canceller;
        private Action finalizer;

        // invoke a task asynchronously
        public void StartTask(Complex alpha, Complex beta, Complex gamma)
        {
            model.α = alpha;
            model.β = beta;
            model.γ = gamma;
            canceller = new CancellationTokenSource();
            task = Task.Run((() => model.Compute(reporter, canceller)), canceller.Token);
            task.ContinueWith((_) => { canceller = null; finalizer(); });
            return;
        }

        public void CancelTask()
        {
            canceller?.Cancel();
            return;
        }

        // constructor
        public TaskManager_c(Model_c model_, Action<double?> reportProgress, Action refreshCanvas)
        {
            model = model_;
            reporter = new Progress<double?>(reportProgress);
            finalizer = refreshCanvas;
        }

    } // TaskManager_c

    public class BitmapManager_c
    {

        private Model_c model;

        private SKBitmap bitmap = null;
        private SKColor[] pixels = null;

        private SKColor basinInfoToColor(CubicNewton_c.BasinInfo_s basinInfo)
        {
            if (basinInfo.Basin == CubicNewton_c.BasinEnum.unknown) return SKColors.Black;
            int d = basinInfo.Depth;
            int di = (d < 0x08) ? 0x100 - 0x10 * d : (d < 0x10) ? 0x80 - 0x08 * (d - 0x08) : 0x40 - (d - 0x10);
            byte db = (byte)((di < 0x00) ? 0x00 : (di <= 0xFF) ? di : 0xFF);
            byte r = 0x00, g = 0x00, b = 0x00;
            switch (basinInfo.Basin) {
                case CubicNewton_c.BasinEnum.α: { r = db; g = b = 0x00; } break;
                case CubicNewton_c.BasinEnum.β: { g = db; b = r = 0x00; } break;
                case CubicNewton_c.BasinEnum.γ: { b = db; r = g = 0x00; } break;
                default: break;
            }
            return new SKColor(r, g, b);
        }

        public SKBitmap GetBitmap(int width, int height)
        {
            if ((bitmap == null) || (bitmap.Width != width) || (bitmap.Height != height)) {
                bitmap = new SKBitmap(width, height);
                pixels = new SKColor[width * height];
            }
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int ir = x * model.BasinMap.NTicksRe / (width - 1);
                    int ii = y * model.BasinMap.NTicksIm / (height - 1);
                    CubicNewton_c.BasinInfo_s basinInfo = model.BasinMap.Map[ir, ii];
                    pixels[width * y + x] = basinInfoToColor(basinInfo);
                }
            }
            bitmap.Pixels = pixels;
            return bitmap;
        }

        public (int x, int y) ComplexToBitmapXY(Complex z)
        {
            double minRe = model.BasinMap.MinRe, maxRe = model.BasinMap.MaxRe;
            double minIm = model.BasinMap.MinIm, maxIm = model.BasinMap.MaxIm;
            int x = (int)Math.Round((double)bitmap.Width  * (z.Real      - minRe) / (maxRe - minRe));
            int y = (int)Math.Round((double)bitmap.Height * (z.Imaginary - maxIm) / (minIm - maxIm));
            return (x, y);
        }

        public Complex BitmapXYToComplex(int x, int y)
        {
            double minRe = model.BasinMap.MinRe, maxRe = model.BasinMap.MaxRe;
            double minIm = model.BasinMap.MinIm, maxIm = model.BasinMap.MaxIm;
            double zr = ((double)x / (double)bitmap.Width)  * (maxRe - minRe) + minRe;
            double zi = ((double)y / (double)bitmap.Height) * (minIm - maxIm) + maxIm;
            return new Complex(zr, zi);
        }

        public BitmapManager_c(Model_c model_)
        {
            model = model_;
        }

    } // BitmapManager_c

    public class ViewModel_c : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;
        private void onPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public Model_c Model { get; }

        public TaskManager_c TaskManager { get; }
        public BitmapManager_c BitmapManager { get; }

        // bound properties: AlphaString, BetaString, GammaString

        private string alphaString;
        public string AlphaString {
            get => alphaString;
            private set {
                alphaString = value;
                onPropertyChanged("AlphaString");
            }
        }

        private string betaString;
        public string BetaString {
            get => betaString;
            private set {
                betaString = value;
                onPropertyChanged("BetaString");
            }
        }

        private string gammaString;
        public string GammaString {
            get => gammaString;
            private set {
                gammaString = value;
                onPropertyChanged("GammaString");
            }
        }

        private string complexString(Complex z)
        {
            string sre = (0.0 <= z.Real) ? "+" : "−";
            double are = Math.Abs(z.Real);
            string sim = (0.0 <= z.Imaginary) ? "+" : "−";
            double aim = Math.Abs(z.Imaginary);
            return string.Format("{0} {1:0.00} {2} {3:0.00} i", sre, are, sim, aim);
        }


        public Complex Alpha { get; set; }
        public Complex Beta  { get; set; }
        public Complex Gamma { get; set; }

        // bound properties: AlphaRe, AlphaIm, BetaRe, BetaIm, GammaRe, GammaIm

        public double AlphaRe {
            get => Alpha.Real;
            set {
                TaskManager.CancelTask();
                Alpha = new Complex(value, AlphaIm);
                AlphaString = complexString(Alpha);
                onPropertyChanged("AlphaRe");
                TaskManager.StartTask(Alpha, Beta, Gamma);
            }
        }
        public double AlphaIm {
            get => Alpha.Imaginary;
            set {
                TaskManager.CancelTask();
                Alpha = new Complex(AlphaRe, value);
                AlphaString = complexString(Alpha);
                onPropertyChanged("AlphaIm");
                TaskManager.StartTask(Alpha, Beta, Gamma);
            }
        }

        public double BetaRe {
            get => Beta.Real;
            set {
                TaskManager.CancelTask();
                Beta = new Complex(value, BetaIm);
                BetaString = complexString(Beta);
                onPropertyChanged("BetaRe");
                TaskManager.StartTask(Alpha, Beta, Gamma);
            }
        }
        public double BetaIm {
            get => Beta.Imaginary;
            set {
                TaskManager.CancelTask();
                Beta = new Complex(BetaRe, value);
                BetaString = complexString(Beta);
                onPropertyChanged("BetaIm");
                TaskManager.StartTask(Alpha, Beta, Gamma);
            }
        }

        public double GammaRe {
            get => Gamma.Real;
            set {
                TaskManager.CancelTask();
                Gamma = new Complex(value, GammaIm);
                GammaString = complexString(Gamma);
                onPropertyChanged("GammaRe");
                TaskManager.StartTask(Alpha, Beta, Gamma);
            }
        }
        public double GammaIm {
            get => Gamma.Imaginary;
            set {
                TaskManager.CancelTask();
                Gamma = new Complex(GammaRe, value);
                GammaString = complexString(Gamma);
                onPropertyChanged("GammaIm");
                TaskManager.StartTask(Alpha, Beta, Gamma);
            }
        }

        // bound properties: RestoreCommand

        public ICommand RestoreCommand { get; private set; }

        private void restore()
        {
            TaskManager.CancelTask();
            Model.InitSolutions();
            Alpha = Model.α; AlphaString = complexString(Alpha);
            Beta  = Model.β; BetaString  = complexString(Beta);
            Gamma = Model.γ; GammaString = complexString(Gamma);
            onPropertyChanged("AlphaRe");
            onPropertyChanged("AlphaIm");
            onPropertyChanged("BetaRe");
            onPropertyChanged("BetaIm");
            onPropertyChanged("GammaRe");
            onPropertyChanged("GammaIm");
            TaskManager.StartTask(Alpha, Beta, Gamma);
        }

        // bound properties: ProgressIsVisible, Progress

        private bool progressIsVisible;
        public bool ProgressIsVisible {
            get => progressIsVisible;
            private set {
                if (progressIsVisible != value) {
                    progressIsVisible = value;
                    onPropertyChanged("ProgressIsVisible");
                }
            }
        }
        private double progress;
        public double Progress {
            get => progress;
            private set {
                progress = value;
                onPropertyChanged("Progress");
            }
        }

        // actions passed to Model

        private void reportProgress(double? progress)
        {
            ProgressIsVisible = (progress != null);
            Progress = progress ?? 0.0;
        }

        public Action InvalidateCanvasAction { get; set; }

        private void refreshCanvas()
        {
            InvalidateCanvasAction();
        }

        // constructor
        public ViewModel_c()
        {
            Model = new Model_c();

            Alpha = Model.α; alphaString = complexString(Alpha);
            Beta  = Model.β; betaString  = complexString(Beta);
            Gamma = Model.γ; gammaString = complexString(Gamma);
            progressIsVisible = false; progress = 0.0;

            RestoreCommand = new Command(restore);

            BitmapManager = new BitmapManager_c(Model);

            TaskManager = new TaskManager_c(Model, reportProgress, refreshCanvas);
            TaskManager.StartTask(Alpha, Beta, Gamma);
        }

    } // ViewModel_c

}
