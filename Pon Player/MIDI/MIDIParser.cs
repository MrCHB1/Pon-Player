using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Pon_Player.Misc;

namespace Pon_Player.MIDI
{
    public struct RawEvent
    {
        public double time;
        public uint rawData;
    }

    public struct MIDIEvent
    {
        public uint delta;
        public byte command;
        public uint rawData;
    }

    public struct TempoEvent
    {
        public uint time;
        public uint tempo;
    }

    public struct Note
    {
        public double start;
        public double length;
        public byte key;
        public byte vel;
        public byte channel;
    }

    class MIDIParser
    {
        private struct UnendedNote
        {
            public int id;
        }
        public List<MIDIEvent> evs = new List<MIDIEvent>();
        public List<TempoEvent> tempoEvs = new List<TempoEvent>();
        public List<Note>[] notes = new List<Note>[128];

        public List<RawEvent> playbackEvs = new List<RawEvent>();

        int currGlobalTempoID = 0;
        double tempoMultiplier;

        public bool trackEnded = false;
        private BufferedReader reader;
        public int PPQ = 960;

        uint tTime = 0;
        public double trackTime = 0;

        private byte prevCommand;

        public MIDIParser(Stream stream, TrackPointers trackLocation, object mtx, int PPQ = 960)
        {
            reader = new BufferedReader(stream, trackLocation.start, trackLocation.length, 100000, mtx);
            this.PPQ = PPQ;
            tempoMultiplier = ((double)500000 / PPQ) / 1000000;
        }

        uint parseVQL()
        {
            uint d = 0;
            for (int i = 0; i < 4; i++)
            {
                byte c = reader.ReadByte();

                if (c > 0x7F) d = (d << 7) | ((uint)c & 0x7F);
                else
                {
                    d = (d << 7) | (uint)c;
                    break;
                }
            }
            return d;
        }

        public void parseNext()
        {
            while (!trackEnded)
            {
                MIDIEvent ev = new MIDIEvent();
                bool validEv = false;
                uint delta = 0;
                while (!validEv && !trackEnded)
                {
                    delta += parseVQL();
                    byte comm = reader.ReadByte();
                    if (comm < 0x80)
                    {
                        reader.Seek(-1, 1);
                        comm = prevCommand;
                    }
                    prevCommand = comm;

                    byte cmd = (byte)(comm & 0xF0);

                    switch (cmd)
                    {
                        case 0x80:
                            {
                                byte key = reader.ReadByte();
                                byte vel = reader.ReadByte();
                                ev.delta = delta;
                                ev.command = 0x80;
                                ev.rawData = (uint)((vel << 16) | (key << 8) | comm);
                                tTime += delta;
                                validEv = true;
                                break;
                            }
                        case 0x90:
                            {
                                byte key = reader.ReadByte();
                                byte vel = reader.ReadByte();
                                ev.delta = delta;
                                ev.command = (byte)((vel == 0) ? 0x80 : 0x90);
                                ev.rawData = (uint)((vel << 16) | (key << 8) | comm);
                                tTime += delta;
                                validEv = true;
                                break;
                            }
                        case 0xA0:
                        case 0xB0:
                        case 0xE0:
                            {
                                byte v1 = reader.ReadByte();
                                byte v2 = reader.ReadByte();
                                ev.delta = delta;
                                ev.command = cmd;
                                ev.rawData = (uint)(comm | (v1 << 8) | (v2 << 16));
                                tTime += delta;
                                validEv = true;
                                break;
                            }
                        case 0xC0:
                        case 0xD0:
                            {
                                byte v1 = reader.ReadByte();
                                ev.delta = delta;
                                ev.command = cmd;
                                ev.rawData = (uint)(comm | (v1 << 8));
                                tTime += delta;
                                validEv = true;
                                break;
                            }
                        default:
                            {
                                switch (comm)
                                {
                                    case 0xF0:
                                        {
                                            while (reader.ReadByte() != 0xF7) { }
                                            break;
                                        }
                                    case 0xF2:
                                        {
                                            reader.SkipBytes(2);
                                            break;
                                        }
                                    case 0xF3:
                                        {
                                            reader.SkipBytes(1);
                                            break;
                                        }
                                    case 0xFF:
                                        {
                                            byte c = reader.ReadByte();
                                            uint size = parseVQL();
                                            switch (c)
                                            {
                                                case 0x2F:
                                                    {
                                                        ev.delta = delta;
                                                        ev.command = 0x2F;
                                                        ev.rawData = 0x00;
                                                        validEv = true;
                                                        trackEnded = true;
                                                        break;
                                                    }
                                                case 0x51:
                                                    {
                                                        uint btempo = 0;
                                                        for (int i = 0; i != 3; i++) btempo = (btempo << 8) | reader.ReadByte();
                                                        tTime += delta;
                                                        tempoEvs.Add(new TempoEvent()
                                                        {
                                                            time = tTime,
                                                            tempo = btempo
                                                        });
                                                        delta = 0;
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        reader.SkipBytes((ulong)size);
                                                        break;
                                                    }
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            break;
                                        }
                                }
                                break;
                            }
                    }
                }
                if (validEv)
                    tTime += delta;
                evs.Add(ev);
            }
        }

        public void parsePass2(List<MIDIEvent> evs, List<TempoEvent> tempoEvents)
        {
            List<Note>[] notes = new List<Note>[256];
            for (int i = 0; i < 256; i++) notes[i] = new List<Note>();
            double tempoTime = 0;
            FastList<UnendedNote>[] unendedNotes = new FastList<UnendedNote>[256 * 16];
            for (int i = 0; i < unendedNotes.Length; i++) unendedNotes[i] = new FastList<UnendedNote>();

            int[] ids = new int[256];

            foreach (MIDIEvent e in evs)
            {
                double delt = 0;
                tempoTime += e.delta;
                if (currGlobalTempoID < tempoEvents.Count() && tempoTime > tempoEvents[currGlobalTempoID].time)
                {
                    long t = (long)(tempoTime - e.delta);
                    double v = 0;
                    while (currGlobalTempoID < tempoEvents.Count() && tempoTime > tempoEvents[currGlobalTempoID].time)
                    {
                        v += (tempoEvents[currGlobalTempoID].time - t) * tempoMultiplier;
                        t = tempoEvents[currGlobalTempoID].time;
                        tempoMultiplier = ((double)tempoEvents[currGlobalTempoID].tempo / PPQ) / 1000000;
                        currGlobalTempoID++;
                    }
                    v += (tempoTime - t) * tempoMultiplier;
                    delt = v;
                }
                else
                {
                    delt = e.delta * tempoMultiplier;
                }
                trackTime += delt;
                if (e.command == 0x90)
                {
                    byte chan = (byte)((e.rawData & 0xFF) & 0x0F);
                    byte key = (byte)((e.rawData >> 8) & 0xFF);
                    byte vel = (byte)((e.rawData >> 16) & 0xFF);
                    UnendedNote note = new UnendedNote()
                    {
                        id = ids[key]
                    };
                    unendedNotes[key * 16 + chan].Add(note);
                    notes[key].Add(new Note()
                    {
                        start = trackTime,
                        length = 100000,
                        channel = chan,
                        key = key,
                        vel = vel
                    });
                    ids[key]++;
                    playbackEvs.Add(new RawEvent()
                    {
                        time = trackTime,
                        rawData = (uint)((byte)(0x90 | chan) | (key << 8) | (vel << 16))
                    });
                }
                else if (e.command == 0x80)
                {
                    byte chan = (byte)((e.rawData & 0xFF) & 0x0F);
                    byte key = (byte)((e.rawData >> 8) & 0xFF);
                    byte vel = (byte)((e.rawData >> 16) & 0xFF);
                    var arr = unendedNotes[key * 16 + chan];
                    if (arr.ZeroLen) continue;
                    var note = unendedNotes[key * 16 + chan].Pop();
                    var n = notes[key][note.id];
                    n.length = trackTime - n.start;
                    notes[key][note.id] = n;
                    playbackEvs.Add(new RawEvent()
                    {
                        time = trackTime,
                        rawData = (uint)((byte)(0x80 | chan) | (key << 8) | (vel << 16))
                    });
                }
                else
                {
                    playbackEvs.Add(new RawEvent()
                    {
                        time = trackTime,
                        rawData = e.rawData
                    });
                }

                /*if (e.command == 0x51)
                {
                    tempo = e.rawData;
                    exTicks = e.delta + lastDiff;
                    lastDiff = 0;
                    continue;
                }*/

                //if (!noteQueue.First.Equals(default(UnendedNote)) && noteQueue.First.ended)
                //{
                //    Note n = noteQueue.Pop().n;
                //    notes[n.key].Add(n);
                //}
            }
            /*foreach (UnendedNote u in noteQueue)
            {
                var un = u;
                if (!un.ended)
                {
                    un.n.length = time - un.n.start;
                    //un.n.length = 0.1;
                }
                notes[un.n.key].Add(un.n);
            }
            noteQueue.Unlink();
            foreach (var s in unendedNotes) s.Unlink();*/
            //return notes;
            this.notes = notes;
        }
    }
}
