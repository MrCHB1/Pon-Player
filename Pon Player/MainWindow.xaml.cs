using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.IO;
using Pon_Player.Rendering;
using Pon_Player.Misc;
using Pon_Player.MIDI;
using Pon_Player.Audio;

namespace Pon_Player
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        D3D11 d3d;
        Scene s;
        GlobalTime Time;
        MIDIAudio audioPlayback = new MIDIAudio();
        double midiTime { get; set; } = 0;

        bool midiLoaded = false;

        MIDIFile f;

        public MainWindow()
        {
            InitializeComponent();
            Time = new GlobalTime();
            d3d = new D3D11();
            s = new Scene() { Renderer = d3d };
            MainRenderer.Renderer = s;
            s.Time = Time;
            d3d.FPSLock = 60;
            d3d.SingleThreadedRender = true;
            d3d.SyncRender = false;

            audioPlayback.Open();

            Closed += OnClose;
            KeyDown += MainWindow_KeyDown;

            CompositionTarget.Rendering += (s, e) =>
            {
                ncLabel.Text = (midiLoaded) ? this.s.NotesPassed.ToString("#,###") : "N/A";
                tnLabel.Text = (midiLoaded) ? this.f.nc.ToString("#,###") : "N/A";
                timeLabel.Text = (midiLoaded) ? FormatTime(this.s.Time) : "0:00";
                playbackSpeedLabel.Text = Time.Speed.ToString("0.00");
            };
        }

        string FormatTime(GlobalTime time)
        {
            return Math.Floor(time.GetTime() / 60).ToString()+":"+(time.GetTime()%60).ToString("#,#00.00");
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (Time.Paused)
                {
                    if (s.rndr.notesToRender != null) Time.Play();
                }
                else Time.Pause();
            }
            if (e.Key == Key.Right)
            {
                Time.Navigate(Math.Min(Math.Max(Time.GetTime() + 5, 0), midiTime));
            }
            if (e.Key == Key.Left)
            {
                Time.Navigate(Math.Min(Math.Max(Time.GetTime() - 5, 0), midiTime));
            }
        }

        private void OnClose(object sender, EventArgs e)
        {
            if (audioPlayback != null) audioPlayback.Close();
            GC.Collect(2, GCCollectionMode.Forced);
        }

        private void MIDIFileBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openedMIDIFile = new OpenFileDialog();
            openedMIDIFile.Filter = "MIDI Files (*.mid)|*.mid";
            if (openedMIDIFile.ShowDialog() == true)
            {
                try
                {
                    f = new MIDIFile(openedMIDIFile.FileName);
                    s.rndr.notesToRender = null;
                    //audioPlayback.Close();
                    midiLoaded = false;
                    f.MIDILoaded += () =>
                    {
                        Time.Reset();
                        midiTime = 0;
                        GC.Collect(2, GCCollectionMode.Forced);
                        Console.WriteLine("Please wait, KDMAPI's still being initialized...");
                        //audioPlayback.Open();
                        Console.WriteLine("Done");
                    };
                    f.ParseFinished += () =>
                    {
                        Time.Navigate(-1);
                        s.rndr.notesToRender = f.notes;
                        midiTime = f.midiTimeLen;
                        GC.Collect(2, GCCollectionMode.Forced);
                        Time.Play();
                        midiLoaded = true;
                        audioPlayback.StartAudio(f.midiEvents, Time);
                    };
                }
                catch (Exception ex)
                {
                    string errorText = ex.Message;
                    MessageBox.Show(errorText, "MIDI failed to load!");
                }
            }
        }

        private void playbackSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double val = e.NewValue;
            if (val > -0.25 && val < 0.25)
                val = 0;
            playbackSpeedSlider.Value = val;
            Time.ChangeSpeed(Math.Round(Math.Pow(2,val), 2));
        }
    }
}
