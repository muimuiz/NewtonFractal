using System;
using System.Numerics;
using System.Diagnostics;
using System.Threading;

namespace NewtonFractal
{

    // abstract Newton method class for complex numbers
    // derived classes have to implements f and fd (derivative of f)
    public abstract class ComplexNewton_c
    {
        public int MaxIteration { get; }
        public double Epsilon { get; }

        // f(z) = 0: target equation
        protected abstract Complex f(Complex z);
        // fd(z) = df(z)/dz
        protected abstract Complex fd(Complex z);

        // constructor
        public ComplexNewton_c(int maxIteration, double epsilon)
        {
            MaxIteration = maxIteration; // upper boundary of iteration
            Epsilon = epsilon; // tolerance to determine z reaches a solution
        }

        // whether z is not ±∞ nor NaN
        private bool isNormal(Complex z)
        {
            double zr = z.Real, zi = z.Imaginary;
            if (double.IsInfinity(zr) || double.IsNaN(zr)) return false;
            if (double.IsInfinity(zi) || double.IsNaN(zi)) return false;
            return true;
        }

        // Newton method iteration starting with an initial value z0
        // returns not only the solution but actual iteration count
        // and returns null as solution if failed
        public (Complex? solution, int iteration) Solve(Complex z0)
        {
            Complex z = z0;
            for (int i = 0; i < MaxIteration; i++) {
                Complex fz = f(z);
                if (Complex.Abs(fz) < Epsilon) return (z, i); // reached
                Complex fdz = fd(z);
                z -= fz / fdz;
                if (!isNormal(z)) return (null, i); // failed
            }
            return (null, MaxIteration); // not reached within iteration
        }

    } // ComplexNewton_c

    // Newton method for a cubic equation explicitly given three solutions α, β, γ
    public class CubicNewton_c : ComplexNewton_c
    {

        // enumeration type to indicate which solution
        public enum BasinEnum { unknown = 0, α, β, γ }

        public struct BasinInfo_s
        {
            public BasinEnum Basin { get; }
            public int Depth { get; }
            public BasinInfo_s(BasinEnum basin, int depth)
            { Basin = basin; Depth = depth; }
        }
        
#       pragma warning disable IDE1006
        public Complex α { get; }
        public Complex β { get; }
        public Complex γ { get; }
#       pragma warning restore IDE1006

        protected override Complex f(Complex z)
            => (z - α) * (z - β) * (z - γ);
        protected override Complex fd(Complex z)
            => 3.0 * z * z - 2.0 * (α + β + γ) * z + (β * γ + γ * α + α * β);

        // find out which solution an initial value z will reach
        // (which solution's basin z belongs to)
        public BasinInfo_s WhichBasin(Complex z)
        {
            (Complex? solution, int depth) = Solve(z);
            if (solution == null) {
                return new BasinInfo_s(BasinEnum.unknown, depth);
            } else {
                Complex s = (Complex)solution; // ensured not null
                if (Complex.Abs(s - α) < Epsilon) return new BasinInfo_s(BasinEnum.α, depth);
                if (Complex.Abs(s - β) < Epsilon) return new BasinInfo_s(BasinEnum.β, depth);
                if (Complex.Abs(s - γ) < Epsilon) return new BasinInfo_s(BasinEnum.γ, depth);
                return new BasinInfo_s(BasinEnum.unknown, depth);
            }
        }

        // constructor
        // three solutions are given explicitly
        public CubicNewton_c(Complex α_, Complex β_, Complex γ_, int maxIteration, double epsilon)
            : base(maxIteration, epsilon)
        {
            α = α_; β = β_; γ = γ_;
        }

    } // CubicNewton_c

    // a basin map of a rectangular region on complex space
    public class BasinMap_c
    {
        public double MinRe { get; }
        public double MaxRe { get; }
        public double MinIm { get; }
        public double MaxIm { get; }
        public int NTicksRe { get; }
        public int NTicksIm { get; }

        // two-dimensional array of pair of basin and depth
        public CubicNewton_c.BasinInfo_s[,] Map { get; }

        // constructor
        // ticks arguments are one less than the total number of points including both ends
        public BasinMap_c(double minRe, double maxRe, double minIm, double maxIm, int nTicksRe, int nTicksIm)
        {
            MinRe = minRe; MaxRe = maxRe; MinIm = minIm; MaxIm = maxIm;
            NTicksRe = nTicksRe; NTicksIm = nTicksIm;
            Debug.Assert((NTicksRe >= 0) && (NTicksIm >= 0));
            Map = new CubicNewton_c.BasinInfo_s[nTicksRe + 1, nTicksIm + 1];
        }

        private double indexToRe(int ir)
            => (MaxRe - MinRe) * (double)ir / (double)NTicksRe + MinRe;
        private double indexToIm(int ii)
            => (MaxIm - MinIm) * (double)ii / (double)NTicksIm + MinIm;
        private Complex indicesToComplex(int ir, int ii)
            => new Complex(indexToRe(ir), indexToIm(ii));

        private int reToNearestIndex(double re)
            => (int)Math.Round(NTicksRe * (re - MinRe) / (MaxRe - MinRe));
        private int imToNearestIndex(double im)
            => (int)Math.Round(NTicksIm * (im - MinIm) / (MaxIm - MinIm));
        private (int ir, int ii) complexToNearestIndices(Complex z)
            => (reToNearestIndex(z.Real), imToNearestIndex(z.Imaginary));

        // actual computaion of basins for each point
        // takes time a while
        // progress reporting and cancelling feature are provided
        public void Compute(CubicNewton_c cubicNewton, IProgress<double?> reporter, CancellationTokenSource canceller)
        {
            int s = (int)Math.Ceiling((double)(NTicksIm + 1) / 100.0);
            for (int ii = 0; ii <= NTicksIm; ii++) {
                if (canceller.Token.IsCancellationRequested) break;
                if (ii % s == 0) reporter.Report((double)ii / (double)(NTicksIm + 1));
                for (int ir = 0; ir <= NTicksRe; ir++) {
                    Complex z0 = indicesToComplex(ir, ii);
                    Map[ir, ii] = cubicNewton.WhichBasin(z0);
                }
            }
            reporter.Report(null);
            return;
        }

    } // BasinMap_c

    public class Model_c
    {

#       pragma warning disable IDE1006
        public Complex α { get; set; }
        public Complex β { get; set; }
        public Complex γ { get; set; }
#       pragma warning restore IDE1006

        private const int maxIteration = 32;
        private const double epsilon = 1e-9;

        private const double halfRange = 2.0;
        private const int nTicks = 512;

        public BasinMap_c BasinMap { get; }

        public void Compute(Progress<double?> reporter, CancellationTokenSource canceller)
        {
            CubicNewton_c cubicNewton = new CubicNewton_c(α, β, γ, maxIteration, epsilon);
            BasinMap.Compute(cubicNewton, reporter, canceller);
            return;
        }

        public void InitSolutions()
        {
            // initial values of the solutions
            α = Complex.One;
            β = Complex.Exp(Complex.ImaginaryOne * (+2.0 * Math.PI / 3.0)); // ω: primitive 3rd root of 1
            γ = Complex.Exp(Complex.ImaginaryOne * (-2.0 * Math.PI / 3.0)); // ω²
        }

        // constructor
        public Model_c()
        {
            // a square region centered on the origin
            BasinMap = new BasinMap_c(-halfRange, +halfRange, -halfRange, +halfRange, nTicks, nTicks);
            // Initialize the solutions
            InitSolutions();
        }

    } // Model_c

}
