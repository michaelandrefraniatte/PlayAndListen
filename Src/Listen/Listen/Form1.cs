using System;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using NAudio.Wave;
using NAudio.Extras;
using WebSocketSharp;
using System.Threading.Tasks;
using CSCore.SoundIn;
using CSCore.Streams;
using System.Collections.Generic;
using WasapiCapture = CSCore.SoundIn.WasapiCapture;
using CSCore.DSP;
using CSCore;
using System.Drawing;
using System.Linq;
using WinformsVisualization.Visualization;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.ComponentModel;
using WaveFormat = NAudio.Wave.WaveFormat;

namespace Listen
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        public static uint CurrentResolution = 0;
        public static bool listening = false;
        public static WebSocket wslisten1;
        public static string currentAudioBytes = null;
        public static BufferedWaveProvider src;
        public static WaveOut soundOut, soundOutEq;
        public static EqualizerBand[] bands;
        public static float volumeleft, volumeright;
        private static NAudio.Extras.Equalizer equalizer;
        private static MediaFoundationReader audioFileReader;
        private static VolumeStereoSampleProvider stereo;
        public static int numBars = 69;
        public float[] barData = new float[numBars];
        public int minFreq = 0;
        public int maxFreq = 24000;
        public int barSpacing = 0;
        public bool logScale = true;
        public bool isAverage = false;
        public float highScaleAverage = 1.0f;
        public float highScaleNotAverage = 2.0f;
        public LineSpectrum lineSpectrum;
        public WasapiCapture capture;
        public FftSize fftSize;
        public float[] fftBuffer;
        public BasicSpectrumProvider spectrumProvider;
        public IWaveSource finalSource;
        private static Image img;
        public static int size = 0;
        public static int width, height;
        public static Brush brush = (Brush)Brushes.MediumPurple;
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            listening = false;
            System.Threading.Thread.Sleep(100);
            if (TextBoxServerIP.Text != "")
            {
                using (StreamWriter createdfile = new StreamWriter("tempsave"))
                {
                    createdfile.WriteLine(TextBoxServerIP.Text);
                    createdfile.WriteLine(trackBar1.Value);
                    createdfile.WriteLine(trackBar2.Value);
                    createdfile.WriteLine(trackBar3.Value);
                    createdfile.WriteLine(trackBar4.Value);
                    createdfile.WriteLine(trackBar5.Value);
                    createdfile.WriteLine(trackBar6.Value);
                    createdfile.WriteLine(trackBar7.Value);
                    createdfile.WriteLine(trackBar8.Value);
                    createdfile.WriteLine(trackBar9.Value);
                    createdfile.WriteLine(trackBar10.Value);
                    createdfile.WriteLine(trackBar11.Value);
                    createdfile.WriteLine(trackBar12.Value);
                    createdfile.WriteLine(trackBar13.Value);
                }
            }
            try
            {
                soundOut.Stop();
            }
            catch { }
            try
            {
                soundOutEq.Stop();
            }
            catch { }
            try
            {
                wslisten1.Close();
            }
            catch { }
            try
            {
                wslisten1.OnMessage -= Ws_OnMessage;
            }
            catch { }
            try
            {
                capture.Stop();
            }
            catch { }
            Process.GetCurrentProcess().Kill();
        }
        private void Form1_Shown(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            InitSoundPlay();
            img = Image.FromFile("play.png");
            width = img.Width;
            height = img.Height;
            if (File.Exists("tempsave"))
            {
                using (StreamReader file = new StreamReader("tempsave"))
                {
                    TextBoxServerIP.Text = file.ReadLine();
                    trackBar1.Value = Convert.ToInt32(file.ReadLine());
                    trackBar2.Value = Convert.ToInt32(file.ReadLine());
                    trackBar3.Value = Convert.ToInt32(file.ReadLine());
                    trackBar4.Value = Convert.ToInt32(file.ReadLine());
                    trackBar5.Value = Convert.ToInt32(file.ReadLine());
                    trackBar6.Value = Convert.ToInt32(file.ReadLine());
                    trackBar7.Value = Convert.ToInt32(file.ReadLine());
                    trackBar8.Value = Convert.ToInt32(file.ReadLine());
                    trackBar9.Value = Convert.ToInt32(file.ReadLine());
                    trackBar10.Value = Convert.ToInt32(file.ReadLine());
                    trackBar11.Value = Convert.ToInt32(file.ReadLine());
                    trackBar12.Value = Convert.ToInt32(file.ReadLine());
                    trackBar13.Value = Convert.ToInt32(file.ReadLine());
                }
            }
            label1.Text = trackBar1.Value > 0 ? "+" + trackBar1.Value.ToString() : trackBar1.Value.ToString();
            label2.Text = trackBar2.Value > 0 ? "+" + trackBar2.Value.ToString() : trackBar2.Value.ToString();
            label3.Text = trackBar3.Value > 0 ? "+" + trackBar3.Value.ToString() : trackBar3.Value.ToString();
            label4.Text = trackBar4.Value > 0 ? "+" + trackBar4.Value.ToString() : trackBar4.Value.ToString();
            label5.Text = trackBar5.Value > 0 ? "+" + trackBar5.Value.ToString() : trackBar5.Value.ToString();
            label6.Text = trackBar6.Value > 0 ? "+" + trackBar6.Value.ToString() : trackBar6.Value.ToString();
            label7.Text = trackBar7.Value > 0 ? "+" + trackBar7.Value.ToString() : trackBar7.Value.ToString();
            label8.Text = trackBar8.Value > 0 ? "+" + trackBar8.Value.ToString() : trackBar8.Value.ToString();
            label9.Text = trackBar9.Value > 0 ? "+" + trackBar9.Value.ToString() : trackBar9.Value.ToString();
            label10.Text = trackBar10.Value > 0 ? "+" + trackBar10.Value.ToString() : trackBar10.Value.ToString();
            label11.Text = trackBar11.Value > 0 ? "+" + trackBar11.Value.ToString() : trackBar11.Value.ToString();
            label12.Text = trackBar12.Value > 0 ? trackBar12.Value.ToString() + " %" : "0 %";
            label13.Text = trackBar13.Value > 0 ? trackBar13.Value.ToString() + " %" : "0 %";
        }
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e.KeyData);
        }
        private void OnKeyDown(Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (keyData == Keys.Escape)
            {
                this.Close();
            }
        }
        private void InitSoundPlay()
        {
            audioFileReader = new MediaFoundationReader("silence.mp3");
            soundOut = new WaveOut();
            soundOut.Init(audioFileReader);
            soundOut.Play();
            soundOut.PlaybackStopped += SoundOut_PlaybackStopped;
        }
        private void SoundOut_PlaybackStopped(object sender, NAudio.Wave.StoppedEventArgs e)
        {
            soundOut.Play();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (!listening)
            {
                listening = true;
                button1.Text = "Listening";
                Task.Run(() => Start());
            }
            else if (listening)
            {
                listening = false;
                button1.Text = "Listen";
                try
                {
                    wslisten1.Close();
                }
                catch { }
                try
                {
                    soundOutEq.Stop();
                }
                catch { }
                try
                {
                    wslisten1.OnMessage -= Ws_OnMessage;
                }
                catch { }
                try
                {
                    capture.Stop();
                }
                catch { }
                this.pictureBox1.BackgroundImage = img;
            }
        }
        public void Start()
        {
            string ip = TextBoxServerIP.Text;
            int result = Convert.ToInt32(ip.Substring(ip.LastIndexOf('.') + 1)) + 62000;
            string port = result.ToString();
            bool connected = false;
            while (!connected & listening)
            {
                try
                {
                    wslisten1 = new WebSocket("ws://" + ip + ":" + port + "/Audio");
                    wslisten1.OnMessage += Ws_OnMessage;
                    wslisten1.Connect();
                    wslisten1.Send("Hello from client");
                    connected = wslisten1.IsAlive;
                }
                catch { }
                System.Threading.Thread.Sleep(1);
            }
            if (listening)
            {
                SetEqualizer();
                src = new BufferedWaveProvider(new WaveFormat(44100, 16, 2));
                stereo = new VolumeStereoSampleProvider(src.ToSampleProvider());
                equalizer = new NAudio.Extras.Equalizer(stereo, Form1.bands);
                soundOutEq = new WaveOut();
                soundOutEq.Init(equalizer);
                soundOutEq.Play();
                GetAudioByteArray();
                Task.Run(() => Poll());
            }
        }
        private static void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            byte[] rawdata = e.RawData;
            src.AddSamples(rawdata, 0, rawdata.Length);
        }
        private void SetEqualizer()
        {
            bands = new EqualizerBand[]
                    {
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 50, Gain = trackBar1.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 100, Gain = trackBar2.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 200, Gain = trackBar3.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 400, Gain = trackBar4.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 800, Gain = trackBar5.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 1200, Gain = trackBar6.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 2400, Gain = trackBar7.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 4800, Gain = trackBar8.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 9600, Gain = trackBar9.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 13500, Gain = trackBar10.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 21000, Gain = trackBar11.Value},
                    };
            volumeleft = trackBar12.Value / 100f;
            volumeright = trackBar13.Value / 100f;
        }
        public void GetAudioByteArray()
        {
            capture = new CSCore.SoundIn.WasapiLoopbackCapture();
            capture.Initialize();
            IWaveSource source = new SoundInSource(capture);
            fftSize = FftSize.Fft4096;
            fftBuffer = new float[(int)fftSize];
            spectrumProvider = new BasicSpectrumProvider(capture.WaveFormat.Channels, capture.WaveFormat.SampleRate, fftSize);
            lineSpectrum = new LineSpectrum(fftSize)
            {
                SpectrumProvider = spectrumProvider,
                UseAverage = true,
                BarCount = numBars,
                BarSpacing = 2,
                IsXLogScale = false,
                ScalingStrategy = ScalingStrategy.Sqrt
            };
            var notificationSource = new SingleBlockNotificationStream(source.ToSampleSource());
            notificationSource.SingleBlockRead += NotificationSource_SingleBlockRead;
            finalSource = notificationSource.ToWaveSource();
            capture.DataAvailable += Capture_DataAvailable;
            capture.Start();
        }
        public void Capture_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            finalSource.Read(e.Data, e.Offset, e.ByteCount);
        }
        public void NotificationSource_SingleBlockRead(object sender, SingleBlockReadEventArgs e)
        {
            spectrumProvider.Add(e.Left, e.Right);
        }
        public float[] GetFFtData()
        {
            lock (barData)
            {
                lineSpectrum.BarCount = numBars;
                if (numBars != barData.Length)
                {
                    barData = new float[numBars];
                }
            }
            if (spectrumProvider.IsNewDataAvailable)
            {
                lineSpectrum.MinimumFrequency = minFreq;
                lineSpectrum.MaximumFrequency = maxFreq;
                lineSpectrum.IsXLogScale = logScale;
                lineSpectrum.BarSpacing = barSpacing;
                lineSpectrum.SpectrumProvider.GetFftData(fftBuffer, this);
                return lineSpectrum.GetSpectrumPoints(100.0f, fftBuffer);
            }
            else
            {
                return null;
            }
        }
        public void ComputeData()
        {
            float[] resData = GetFFtData();
            int numBars = barData.Length;
            if (resData == null)
            {
                return;
            }
            lock (barData)
            {
                for (int i = 0; i < numBars && i < resData.Length; i++)
                {
                    barData[i] = resData[i] / 100.0f;
                }
                for (int i = 0; i < numBars && i < resData.Length; i++)
                {
                    if (lineSpectrum.UseAverage)
                    {
                        barData[i] = barData[i] + highScaleAverage * (float)Math.Sqrt(i / (numBars + 0.0f)) * barData[i];
                    }
                    else
                    {
                        barData[i] = barData[i] + highScaleNotAverage * (float)Math.Sqrt(i / (numBars + 0.0f)) * barData[i];
                    }
                }
            }
        }
        public void Poll()
        {
            while (listening)
            {
                if (this.WindowState != FormWindowState.Minimized)
                {
                    ComputeData();
                    Bitmap bmp = new Bitmap(img);
                    Graphics graphics = Graphics.FromImage(bmp as Image);
                    int[] bar = new int[numBars];
                    for (int n = 0; n < numBars; n++)
                    {
                        bar[n] = Convert.ToInt32(barData[n] * 100f);
                        graphics.FillRectangle(brush, n * width / (float)numBars + 0.5f, height - bar[n], width / (float)numBars - 1, bar[n]);
                    }
                    this.pictureBox1.BackgroundImage = bmp;
                    graphics.Dispose();
                }
                System.Threading.Thread.Sleep(40);
            }
        }
        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            label1.Text = trackBar1.Value > 0 ? "+" + trackBar1.Value.ToString() : trackBar1.Value.ToString();
        }
        private void trackBar2_ValueChanged(object sender, EventArgs e)
        {
            label2.Text = trackBar2.Value > 0 ? "+" + trackBar2.Value.ToString() : trackBar2.Value.ToString();
        }
        private void trackBar3_ValueChanged(object sender, EventArgs e)
        {
            label3.Text = trackBar3.Value > 0 ? "+" + trackBar3.Value.ToString() : trackBar3.Value.ToString();
        }
        private void trackBar4_ValueChanged(object sender, EventArgs e)
        {
            label4.Text = trackBar4.Value > 0 ? "+" + trackBar4.Value.ToString() : trackBar4.Value.ToString();
        }
        private void trackBar5_ValueChanged(object sender, EventArgs e)
        {
            label5.Text = trackBar5.Value > 0 ? "+" + trackBar5.Value.ToString() : trackBar5.Value.ToString();
        }
        private void trackBar6_ValueChanged(object sender, EventArgs e)
        {
            label6.Text = trackBar6.Value > 0 ? "+" + trackBar6.Value.ToString() : trackBar6.Value.ToString();
        }
        private void trackBar7_ValueChanged(object sender, EventArgs e)
        {
            label7.Text = trackBar7.Value > 0 ? "+" + trackBar7.Value.ToString() : trackBar7.Value.ToString();
        }
        private void trackBar8_ValueChanged(object sender, EventArgs e)
        {
            label8.Text = trackBar8.Value > 0 ? "+" + trackBar8.Value.ToString() : trackBar8.Value.ToString();
        }
        private void trackBar9_ValueChanged(object sender, EventArgs e)
        {
            label9.Text = trackBar9.Value > 0 ? "+" + trackBar9.Value.ToString() : trackBar9.Value.ToString();
        }
        private void trackBar10_ValueChanged(object sender, EventArgs e)
        {
            label10.Text = trackBar10.Value > 0 ? "+" + trackBar10.Value.ToString() : trackBar10.Value.ToString();
        }
        private void trackBar11_ValueChanged(object sender, EventArgs e)
        {
            label11.Text = trackBar11.Value > 0 ? "+" + trackBar11.Value.ToString() : trackBar11.Value.ToString();
        }
        private void trackBar12_ValueChanged(object sender, EventArgs e)
        {
            label12.Text = trackBar12.Value > 0 ? trackBar12.Value.ToString() + " %" : "0 %";
        }
        private void trackBar13_ValueChanged(object sender, EventArgs e)
        {
            label13.Text = trackBar13.Value > 0 ? trackBar13.Value.ToString() + " %" : "0 %";
        }
    }
    /// <summary>
    /// Very simple sample provider supporting adjustable gain
    /// </summary>
    public class VolumeStereoSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;

        /// <summary>
        /// Allows adjusting the volume left channel, 1.0f = full volume
        /// </summary>
        public float VolumeLeft { get; set; }

        /// <summary>
        /// Allows adjusting the volume right channel, 1.0f = full volume
        /// </summary>
        public float VolumeRight { get; set; }

        /// <summary>
        /// Initializes a new instance of VolumeStereoSampleProvider
        /// </summary>
        /// <param name="source">Source sample provider, must be stereo</param>
        public VolumeStereoSampleProvider(ISampleProvider source)
        {
            this.source = source;
            VolumeLeft = Form1.volumeleft;
            VolumeRight = Form1.volumeright;
        }

        /// <summary>
        /// WaveFormat
        /// </summary>
        public NAudio.Wave.WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>
        /// Reads samples from this sample provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <param name="offset">Offset into sample buffer</param>
        /// <param name="sampleCount">Number of samples desired</param>
        /// <returns>Number of samples read</returns>
        public int Read(float[] buffer, int offset, int sampleCount)
        {
            int samplesRead = source.Read(buffer, offset, sampleCount);

            for (int n = 0; n < sampleCount; n += 2)
            {
                buffer[offset + n] *= VolumeLeft;
                buffer[offset + n + 1] *= VolumeRight;
            }

            return samplesRead;
        }
    }
}
namespace WinformsVisualization.Visualization
{
    /// <summary>
    ///     BasicSpectrumProvider
    /// </summary>
    public class BasicSpectrumProvider : FftProvider, ISpectrumProvider
    {
        public readonly int _sampleRate;
        public readonly List<object> _contexts = new List<object>();

        public BasicSpectrumProvider(int channels, int sampleRate, FftSize fftSize)
            : base(channels, fftSize)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException("sampleRate");
            _sampleRate = sampleRate;
        }

        public int GetFftBandIndex(float frequency)
        {
            int fftSize = (int)FftSize;
            double f = _sampleRate / 2.0;
            // ReSharper disable once PossibleLossOfFraction
            return (int)((frequency / f) * (fftSize / 2));
        }

        public bool GetFftData(float[] fftResultBuffer, object context)
        {
            if (_contexts.Contains(context))
                return false;

            _contexts.Add(context);
            GetFftData(fftResultBuffer);
            return true;
        }

        public override void Add(float[] samples, int count)
        {
            base.Add(samples, count);
            if (count > 0)
                _contexts.Clear();
        }

        public override void Add(float left, float right)
        {
            base.Add(left, right);
            _contexts.Clear();
        }
    }
}
namespace WinformsVisualization.Visualization
{
    public interface ISpectrumProvider
    {
        bool GetFftData(float[] fftBuffer, object context);
        int GetFftBandIndex(float frequency);
    }
}
namespace WinformsVisualization.Visualization
{
    internal class GradientCalculator
    {
        public Color[] _colors;

        public GradientCalculator()
        {
        }

        public GradientCalculator(params Color[] colors)
        {
            _colors = colors;
        }

        public Color[] Colors
        {
            get { return _colors ?? (_colors = new Color[] { }); }
            set { _colors = value; }
        }

        public Color GetColor(float perc)
        {
            if (_colors.Length > 1)
            {
                int index = Convert.ToInt32((_colors.Length - 1) * perc - 0.5f);
                float upperIntensity = (perc % (1f / (_colors.Length - 1))) * (_colors.Length - 1);
                if (index + 1 >= Colors.Length)
                    index = Colors.Length - 2;

                return Color.FromArgb(
                    255,
                    (byte)(_colors[index + 1].R * upperIntensity + _colors[index].R * (1f - upperIntensity)),
                    (byte)(_colors[index + 1].G * upperIntensity + _colors[index].G * (1f - upperIntensity)),
                    (byte)(_colors[index + 1].B * upperIntensity + _colors[index].B * (1f - upperIntensity)));
            }
            return _colors.FirstOrDefault();
        }
    }
}
namespace WinformsVisualization.Visualization
{
    public class LineSpectrum : SpectrumBase
    {
        public int _barCount;
        public double _barSpacing;
        public double _barWidth;
        public Size _currentSize;

        public LineSpectrum(FftSize fftSize)
        {
            FftSize = fftSize;
        }

        [Browsable(false)]
        public double BarWidth
        {
            get { return _barWidth; }
        }

        public double BarSpacing
        {
            get { return _barSpacing; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");
                _barSpacing = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("BarSpacing");
                RaisePropertyChanged("BarWidth");
            }
        }

        public int BarCount
        {
            get { return _barCount; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("value");
                _barCount = value;
                SpectrumResolution = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("BarCount");
                RaisePropertyChanged("BarWidth");
            }
        }

        [BrowsableAttribute(false)]
        public Size CurrentSize
        {
            get { return _currentSize; }
            set
            {
                _currentSize = value;
                RaisePropertyChanged("CurrentSize");
            }
        }

        public Bitmap CreateSpectrumLine(Size size, Brush brush, Color background, bool highQuality)
        {
            if (!UpdateFrequencyMappingIfNessesary(size))
                return null;

            var fftBuffer = new float[(int)FftSize];

            //get the fft result from the spectrum provider
            if (SpectrumProvider.GetFftData(fftBuffer, this))
            {
                using (var pen = new Pen(brush, (float)_barWidth))
                {
                    var bitmap = new Bitmap(size.Width, size.Height);

                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        PrepareGraphics(graphics, highQuality);
                        graphics.Clear(background);

                        CreateSpectrumLineInternal(graphics, pen, fftBuffer, size);
                    }

                    return bitmap;
                }
            }
            return null;
        }

        public Bitmap CreateSpectrumLine(Size size, Color color1, Color color2, Color background, bool highQuality)
        {
            if (!UpdateFrequencyMappingIfNessesary(size))
                return null;

            using (
                Brush brush = new LinearGradientBrush(new RectangleF(0, 0, (float)_barWidth, size.Height), color2,
                    color1, LinearGradientMode.Vertical))
            {
                return CreateSpectrumLine(size, brush, background, highQuality);
            }
        }

        public void CreateSpectrumLineInternal(Graphics graphics, Pen pen, float[] fftBuffer, Size size)
        {
            int height = size.Height;
            //prepare the fft result for rendering 
            SpectrumPointData[] spectrumPoints = CalculateSpectrumPoints(height, fftBuffer);

            //connect the calculated points with lines
            for (int i = 0; i < spectrumPoints.Length; i++)
            {
                SpectrumPointData p = spectrumPoints[i];
                int barIndex = p.SpectrumPointIndex;
                double xCoord = BarSpacing * (barIndex + 1) + (_barWidth * barIndex) + _barWidth / 2;

                var p1 = new PointF((float)xCoord, height);
                var p2 = new PointF((float)xCoord, height - (float)p.Value - 1);

                graphics.DrawLine(pen, p1, p2);
            }
        }

        public override void UpdateFrequencyMapping()
        {
            _barWidth = Math.Max(((_currentSize.Width - (BarSpacing * (BarCount + 1))) / BarCount), 0.00001);
            base.UpdateFrequencyMapping();
        }

        public bool UpdateFrequencyMappingIfNessesary(Size newSize)
        {
            if (newSize != CurrentSize)
            {
                CurrentSize = newSize;
                UpdateFrequencyMapping();
            }

            return newSize.Width > 0 && newSize.Height > 0;
        }

        public void PrepareGraphics(Graphics graphics, bool highQuality)
        {
            if (highQuality)
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.CompositingQuality = CompositingQuality.AssumeLinear;
                graphics.PixelOffsetMode = PixelOffsetMode.Default;
                graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            }
            else
            {
                graphics.SmoothingMode = SmoothingMode.HighSpeed;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.PixelOffsetMode = PixelOffsetMode.None;
                graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            }
        }
        public float[] GetSpectrumPoints(float height, float[] fftBuffer)
        {
            SpectrumPointData[] dats = CalculateSpectrumPoints(height, fftBuffer);
            float[] res = new float[dats.Length];
            for (int i = 0; i < dats.Length; i++)
            {
                res[i] = (float)dats[i].Value;
            }

            return res;
        }
    }
}
namespace WinformsVisualization.Visualization
{
    public class SpectrumBase : INotifyPropertyChanged
    {
        public const int ScaleFactorLinear = 9;
        public const int ScaleFactorSqr = 2;
        public const double MinDbValue = -90;
        public const double MaxDbValue = 0;
        public const double DbScale = (MaxDbValue - MinDbValue);

        public int _fftSize;
        public bool _isXLogScale;
        public int _maxFftIndex;
        public int _maximumFrequency = 20000;
        public int _maximumFrequencyIndex;
        public int _minimumFrequency = 20; //Default spectrum from 20Hz to 20kHz
        public int _minimumFrequencyIndex;
        public ScalingStrategy _scalingStrategy;
        public int[] _spectrumIndexMax;
        public int[] _spectrumLogScaleIndexMax;
        public ISpectrumProvider _spectrumProvider;

        public int SpectrumResolution;
        public bool _useAverage;

        public int MaximumFrequency
        {
            get { return _maximumFrequency; }
            set
            {
                if (value <= MinimumFrequency)
                {
                    throw new ArgumentOutOfRangeException("value",
                        "Value must not be less or equal the MinimumFrequency.");
                }
                _maximumFrequency = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("MaximumFrequency");
            }
        }

        public int MinimumFrequency
        {
            get { return _minimumFrequency; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");
                _minimumFrequency = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("MinimumFrequency");
            }
        }

        [BrowsableAttribute(false)]
        public ISpectrumProvider SpectrumProvider
        {
            get { return _spectrumProvider; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                _spectrumProvider = value;

                RaisePropertyChanged("SpectrumProvider");
            }
        }

        public bool IsXLogScale
        {
            get { return _isXLogScale; }
            set
            {
                _isXLogScale = value;
                UpdateFrequencyMapping();
                RaisePropertyChanged("IsXLogScale");
            }
        }

        public ScalingStrategy ScalingStrategy
        {
            get { return _scalingStrategy; }
            set
            {
                _scalingStrategy = value;
                RaisePropertyChanged("ScalingStrategy");
            }
        }

        public bool UseAverage
        {
            get { return _useAverage; }
            set
            {
                _useAverage = value;
                RaisePropertyChanged("UseAverage");
            }
        }

        [BrowsableAttribute(false)]
        public FftSize FftSize
        {
            get { return (FftSize)_fftSize; }
            set
            {
                if ((int)Math.Log((int)value, 2) % 1 != 0)
                    throw new ArgumentOutOfRangeException("value");

                _fftSize = (int)value;
                _maxFftIndex = _fftSize / 2 - 1;

                RaisePropertyChanged("FFTSize");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void UpdateFrequencyMapping()
        {
            _maximumFrequencyIndex = Math.Min(_spectrumProvider.GetFftBandIndex(MaximumFrequency) + 1, _maxFftIndex);
            _minimumFrequencyIndex = Math.Min(_spectrumProvider.GetFftBandIndex(MinimumFrequency), _maxFftIndex);

            int actualResolution = SpectrumResolution;

            int indexCount = _maximumFrequencyIndex - _minimumFrequencyIndex;
            double linearIndexBucketSize = Math.Round(indexCount / (double)actualResolution, 3);

            _spectrumIndexMax = _spectrumIndexMax.CheckBuffer(actualResolution, true);
            _spectrumLogScaleIndexMax = _spectrumLogScaleIndexMax.CheckBuffer(actualResolution, true);

            double maxLog = Math.Log(actualResolution, actualResolution);
            for (int i = 1; i < actualResolution; i++)
            {
                int logIndex =
                    (int)((maxLog - Math.Log((actualResolution + 1) - i, (actualResolution + 1))) * indexCount) +
                    _minimumFrequencyIndex;

                _spectrumIndexMax[i - 1] = _minimumFrequencyIndex + (int)(i * linearIndexBucketSize);
                _spectrumLogScaleIndexMax[i - 1] = logIndex;
            }

            if (actualResolution > 0)
            {
                _spectrumIndexMax[_spectrumIndexMax.Length - 1] =
                    _spectrumLogScaleIndexMax[_spectrumLogScaleIndexMax.Length - 1] = _maximumFrequencyIndex;
            }
        }

        public virtual SpectrumPointData[] CalculateSpectrumPoints(double maxValue, float[] fftBuffer)
        {
            var dataPoints = new List<SpectrumPointData>();

            double value0 = 0, value = 0;
            double lastValue = 0;
            double actualMaxValue = maxValue;
            int spectrumPointIndex = 0;

            for (int i = _minimumFrequencyIndex; i <= _maximumFrequencyIndex; i++)
            {
                switch (ScalingStrategy)
                {
                    case ScalingStrategy.Decibel:
                        value0 = (((20 * Math.Log10(fftBuffer[i])) - MinDbValue) / DbScale) * actualMaxValue;
                        break;
                    case ScalingStrategy.Linear:
                        value0 = (fftBuffer[i] * ScaleFactorLinear) * actualMaxValue;
                        break;
                    case ScalingStrategy.Sqrt:
                        value0 = ((Math.Sqrt(fftBuffer[i])) * ScaleFactorSqr) * actualMaxValue;
                        break;
                }

                bool recalc = true;

                value = Math.Max(0, Math.Max(value0, value));

                while (spectrumPointIndex <= _spectrumIndexMax.Length - 1 &&
                       i ==
                       (IsXLogScale
                           ? _spectrumLogScaleIndexMax[spectrumPointIndex]
                           : _spectrumIndexMax[spectrumPointIndex]))
                {
                    if (!recalc)
                        value = lastValue;

                    if (value > maxValue)
                        value = maxValue;

                    if (_useAverage && spectrumPointIndex > 0)
                        value = (lastValue + value) / 2.0;

                    dataPoints.Add(new SpectrumPointData { SpectrumPointIndex = spectrumPointIndex, Value = value });

                    lastValue = value;
                    value = 0.0;
                    spectrumPointIndex++;
                    recalc = false;
                }

                //value = 0;
            }

            return dataPoints.ToArray();
        }

        public void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null && !String.IsNullOrEmpty(propertyName))
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        [DebuggerDisplay("{Value}")]
        public struct SpectrumPointData
        {
            public int SpectrumPointIndex;
            public double Value;
        }
    }
}
namespace WinformsVisualization.Visualization
{
    public enum ScalingStrategy
    {
        Decibel,
        Linear,
        Sqrt
    }
}