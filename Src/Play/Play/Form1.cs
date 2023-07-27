using System;
using System.Windows.Forms;
using System.Net;
using System.Runtime.InteropServices;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Net.NetworkInformation;
using System.Linq;
using System.Data;
using CSCore.Streams;
using NAudio.Wave;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Play
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
        public static bool running;
        public static WaveOut soundOut;
        private static MediaFoundationReader audioFileReader;
        public static WebSocketServer wsaudio;
        public static byte[] audiorawdataavailable;
        public static CSCore.SoundIn.WasapiCapture soundIn;
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            running = false;
            System.Threading.Thread.Sleep(100);
            try
            {
                soundOut.Stop();
            }
            catch { }
            try
            {
                soundIn.Stop();
            }
            catch { }
            try
            {
                wsaudio.Stop();
            }
            catch { }
            Process.GetCurrentProcess().Kill();
        }
        private void Form1_Shown(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            InitSoundPlay();
        }
        private void InitSoundPlay()
        {
            audioFileReader = new MediaFoundationReader("silence.mp3");
            soundOut = new WaveOut();
            soundOut.Init(audioFileReader);
            soundOut.Play();
            soundOut.PlaybackStopped += SoundOut_PlaybackStopped;
        }
        private void SoundOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            soundOut.Play();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (!running)
            {
                running = true;
                button1.Text = "Playing";
                Task.Run(() => Start());
            }
            else if (running)
            {
                running = false;
                button1.Text = "Play";
                try
                {
                    wsaudio.Stop();
                }
                catch { }
                try
                {
                    soundIn.Stop();
                }
                catch { }
            }
        }
        public void Start()
        {
            string localip = GetLocalIP();
            int result = Convert.ToInt32(localip.Substring(localip.LastIndexOf('.') + 1)) + 62000;
            string port = result.ToString();
            bool connected = false;
            while (!connected & running)
            {
                try
                {
                    wsaudio = new WebSocketServer("ws://" + localip + ":" + port);
                    wsaudio.AddWebSocketService<Audio>("/Audio");
                    wsaudio.Start();
                    connected = wsaudio.IsListening;
                }
                catch { }
                System.Threading.Thread.Sleep(1);
            }
            if (running)
            {
                soundIn = new CSCore.SoundIn.WasapiLoopbackCapture(0, new CSCore.WaveFormat(44100, 16, 2));
                soundIn.Initialize();
                soundIn.DataAvailable += (sound, card) =>
                {
                    byte[] rawdata = new byte[card.ByteCount];
                    Array.Copy(card.Data, card.Offset, rawdata, 0, card.ByteCount);
                    audiorawdataavailable = rawdata;
                };
                soundIn.Start();
            }
        }
        public static string GetLocalIP()
        {
            string firstAddress = (from address in NetworkInterface.GetAllNetworkInterfaces().Select(x => x.GetIPProperties()).SelectMany(x => x.UnicastAddresses).Select(x => x.Address)
                                   where !IPAddress.IsLoopback(address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                   select address).FirstOrDefault().ToString();
            return firstAddress;
        }
    }
    public class Audio : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);
            while (Form1.running)
            {
                if (Form1.audiorawdataavailable != null)
                {
                    try
                    {
                        Send(Form1.audiorawdataavailable);
                        Form1.audiorawdataavailable = null;
                    }
                    catch { }
                }
                System.Threading.Thread.Sleep(1);
            }
        }
    }
}