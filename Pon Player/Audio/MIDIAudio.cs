using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using Pon_Player.MIDI;
using Pon_Player.Misc;
using System.Runtime.InteropServices;

namespace Pon_Player.Audio
{
    class MIDIAudio
    {
        [DllImport("ntdll", CallingConvention = CallingConvention.StdCall)]
        public static extern int NtDelayExecution([MarshalAs(UnmanagedType.I1)] bool alertable, ref long DelayInterval);

        private Thread playback;

        public MIDIAudio()
        {
            
        }

        public void Open()
        {
            KDMAPI.InitializeKDMAPIStream();
        }

        public void Close()
        {
            if (playback != null)
            {
                playback.Abort();
                playback = null;
            }
            KDMAPI.TerminateKDMAPIStream();
        }

        Thread SpawnAudioThread(IEnumerable<RawEvent> evs, GlobalTime time, double pauseTime)
        {
            if (playback != null) playback.Abort();
            return new Thread(() =>
            {
                double playbackTime = time.GetTime();
                IEnumerator<RawEvent> ev = evs.Where(e_ => e_.time >= pauseTime).GetEnumerator();
                RawEvent e;
                while (ev.MoveNext())
                {
                    e = ev.Current;
                    //if (e.time < pauseTime) continue;
                    while (playbackTime < e.time || time.Paused)
                    {
                        playbackTime = time.GetTime();
                    }
                    if (((e.rawData >> 0) & 0xFF) > 16)
                        KDMAPI.SendDirectData(e.rawData);
                }
            });
        }

        private void RestartAudio(IEnumerable<RawEvent> evs, GlobalTime time)
        {
            if (playback != null)
            {
                playback.Abort();
                KDMAPI.ResetKDMAPIStream();
                playback = SpawnAudioThread(evs, time, time.GetTime());
                playback.Start();
            }
        }

        public void StartAudio(IEnumerable<RawEvent> evs, GlobalTime time)
        {
            KDMAPI.SendDirectData(0x7BB0);
            playback = SpawnAudioThread(evs, time, -1);
            playback.Start();

            time.PauseChanged += () => { KDMAPI.SendDirectData(0x7BB0); };

            time.TimeChanged += () => { RestartAudio(evs, time); };
            //time.SpeedChanged += () => { RestartAudio(evs, time); };
        }
    }
}
