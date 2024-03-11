using NAudio.Wave;
using System;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp.Server;
using WebSocketSharp;
using System.Net.NetworkInformation;
using System.Net;
using NAudio.CoreAudioApi;

namespace AlwaysTogether
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
        public static bool running = false;
        public static string audioportu1 = "62000", audioportu2 = "63000", ip;
        public WebSocket wscaudio;
        public BufferedWaveProvider src;
        public WasapiOut soundOut;
        private static bool closeonicon = false;
        private void Form1_Load(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            TrayMenuContext();
            if (System.IO.File.Exists("tempsave"))
            {
                using (System.IO.StreamReader file = new System.IO.StreamReader("tempsave"))
                {
                    textBox1.Text = file.ReadLine();
                }
            }
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
        private void button1_Click(object sender, EventArgs e)
        {
            if (!running)
            {
                button1.Text = "Stop";
                running = true;
                ip = textBox1.Text;
                Task.Run(() => LSPAudioHost.Connect());
                Task.Run(() => ConnectAudio());
            }
            else
            {
                button1.Text = "Start";
                running = false;
                System.Threading.Thread.Sleep(100);
                Task.Run(() => LSPAudioHost.Disconnect());
                Task.Run(() => DisconnectAudio());
            }
        }
        public void ConnectAudio()
        {
            String connectionString = "ws://" + ip + ":" + audioportu1 + "/Audio";
            wscaudio = new WebSocket(connectionString);
            wscaudio.OnMessage += Ws_OnMessageAudio;
            while (!wscaudio.IsAlive & running)
            {
                try
                {
                    wscaudio.Connect();
                    wscaudio.Send("Hello from client");
                }
                catch { }
                System.Threading.Thread.Sleep(1);
            }
            var enumerator = new MMDeviceEnumerator();
            MMDevice wasapi = null;
            foreach (var mmdevice in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                wasapi = mmdevice;
                break;
            }
            soundOut = new WasapiOut(wasapi, AudioClientShareMode.Shared, false, 2);
            src = new BufferedWaveProvider(soundOut.OutputWaveFormat);
            soundOut.Init(src);
            soundOut.Play();
            while (wscaudio.IsAlive & running)
            {
                System.Threading.Thread.Sleep(1);
            }
            try
            {
                DisconnectAudio();
                if (running)
                    Task.Run(() => ConnectAudio());
            }
            catch { }
        }
        private void Ws_OnMessageAudio(object sender, MessageEventArgs e)
        {
            src.AddSamples(e.RawData, 0, e.RawData.Length);
        }
        public void DisconnectAudio()
        {
            wscaudio.Close();
            soundOut.Stop();
        }
        public class LSPAudioHost
        {
            public static string ip;
            public static string port;
            public static WebSocketServer wss;
            public static byte[] rawdataavailable;
            private static WaveIn waveIn = null;
            public static void Connect()
            {
                try
                {
                    ip = GetLocalIP();
                    port = Form1.audioportu2;
                    String connectionString = "ws://" + ip + ":" + port;
                    wss = new WebSocketServer(connectionString);
                    wss.AddWebSocketService<Audio>("/Audio");
                    wss.Start();
                    GetAudioByteArray();
                }
                catch { }
            }
            public static string GetLocalIP()
            {
                string firstAddress = (from address in NetworkInterface.GetAllNetworkInterfaces().Select(x => x.GetIPProperties()).SelectMany(x => x.UnicastAddresses).Select(x => x.Address)
                                       where !IPAddress.IsLoopback(address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                       select address).FirstOrDefault().ToString();
                return firstAddress;
            }
            public static void Disconnect()
            {
                wss.RemoveWebSocketService("/Audio");
                wss.Stop();
                waveIn.StopRecording();
                waveIn.Dispose();
            }
            public static void GetAudioByteArray()
            {
                waveIn = new WaveIn();
                waveIn.BufferMilliseconds = 10;
                waveIn.DataAvailable += waveIn_DataAvailable;
                waveIn.StartRecording();
            }
            private static void waveIn_DataAvailable(object sender, WaveInEventArgs e)
            {
                if (e.BytesRecorded > 0)
                {
                    byte[] rawdata = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, 0, rawdata, 0, e.BytesRecorded);
                    rawdataavailable = rawdata;
                }
            }
        }
        public class Audio : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                base.OnMessage(e);
                while (Form1.running)
                {
                    if (LSPAudioHost.rawdataavailable != null)
                    {
                        try
                        {
                            Send(LSPAudioHost.rawdataavailable);
                            LSPAudioHost.rawdataavailable = null;
                        }
                        catch { }
                    }
                    System.Threading.Thread.Sleep(1);
                }
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!closeonicon)
            {
                e.Cancel = true;
                MinimzedTray();
                return;
            }
            running = false;
            System.Threading.Thread.Sleep(300);
            using (System.IO.StreamWriter createdfile = new System.IO.StreamWriter("tempsave"))
            {
                createdfile.WriteLine(textBox1.Text);
            }
        }
        private void TrayMenuContext()
        {
            this.notifyIcon1.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            this.notifyIcon1.ContextMenuStrip.Items.Add("Quit", null, this.MenuTest1_Click);
        }
        void MenuTest1_Click(object sender, EventArgs e)
        {
            closeonicon = true;
            this.Close();
        }
        private void MinimzedTray()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
        }
        private void MaxmizedFromTray()
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Show();
        }
        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            MaxmizedFromTray();
        }
    }
}