using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Pon_Player.Misc;
using Pon_Player.Rendering;

namespace Pon_Player.MIDI
{
    public struct TrackPointers
    {
        public ulong start;
        public ulong length;
    };

    class MIDIFile
    {
        public ushort TrackCount;
        public ushort PPQ;

        public List<TrackPointers> trackPos = new List<TrackPointers>();
        public IEnumerable<RawEvent> midiEvents = new List<RawEvent>();
        public Note[][] notes = new Note[128][];

        public long nc = 0;

        public double midiTimeLen = 0;

        public event Action ParseFinished;
        public event Action MIDILoaded;

        private Stream reader;
        
        public MIDIFile(string path)
        {
            reader = File.Open(path, FileMode.Open);
            Task.Factory.StartNew(() =>
            {
                if (ParseHeader())
                {
                    if (ParseTrackChunks())
                    {
                        Console.WriteLine("Success!");
                        Console.WriteLine("Track count: " + trackPos.Count);
                        Console.WriteLine("PPQ: " + PPQ);
                        MIDILoaded?.Invoke();

                        object m = new object();

                        int ct = 0;

                        List<IEnumerable<Note>[]> allNotes = new List<IEnumerable<Note>[]>();
                        List<IEnumerable<RawEvent>> midiEvs = new List<IEnumerable<RawEvent>>();
                        //List<IEnumerable<TempoEvent>> tempoEvs = new List<IEnumerable<TempoEvent>>();

                        MIDIParser[] parsers = new MIDIParser[trackPos.Count];

                        // Phase 1
                        Console.WriteLine("------- PASS 1 -------");
                        Parallel.For(0, trackPos.Count, (i) =>
                        {
                            TrackPointers tp = trackPos[i];
                            parsers[i] = new MIDIParser(reader, tp, m, PPQ);
                            parsers[i].parseNext();
                            //if (parsers[i].tempoEvs.Count > 0) tempoEvs.Add(parsers[i].tempoEvs);
                            //IEnumerable<Note>[] trackNotes = trackParser.parsePass2(trackParser.evs, tempoEvs);
                            lock (m)
                            {
                                //nc += trackNotes.Length;
                                //allNotes.Add(trackNotes);
                                //midiEvs.Add(parsers[i].playbackEvs.AsEnumerable());
                                Console.WriteLine("Track " + (++ct) + " of " + trackPos.Count + " parsed");
                            }
                        });

                        // Phase 2
                        Console.WriteLine("Merging tempo events...");
                        List<TempoEvent> mergedTempos = Mergers.MergeAllTempoEvents(parsers.Select(p => p.tempoEvs)).ToList();
                        //tempoEvs = null;
                        ct = 0;

                        Console.WriteLine("------- PASS 2 -------");
                        Parallel.For(0, trackPos.Count, (i) =>
                        {
                            parsers[i].parsePass2(parsers[i].evs, mergedTempos);
                            lock (m)
                            {
                                foreach (var k in parsers[i].notes) nc += k.Count();
                                //allNotes.Add(tracknotes);
                                Console.WriteLine("Track " + (++ct) + " of " + trackPos.Count + " parsed (" + nc + " total MIDI notes).");
                            }
                        });

                        midiTimeLen = parsers.Select(p => p.trackTime).Max();
                        Console.WriteLine("MIDI Length (s): " + midiTimeLen);

                        Console.WriteLine("Merging tracks...");

                        int ky = 0;
                        Parallel.For(0, 128, i =>
                        {
                            Console.WriteLine(ky++ + " / 128 keys");
                            notes[i] = Mergers.MergeAllTrackNotes(parsers.Select(p => p.notes[i].AsEnumerable())).ToArray();
                        });

                        allNotes = null;

                        Console.WriteLine("Merging audio events...");

                        midiEvents = Mergers.MergeAllRawEvents(parsers.Select(p => p.playbackEvs.AsEnumerable()).ToList());

                        Console.WriteLine("Done.");

                        parsers = null;
                        midiEvs = null;
                        mergedTempos = null;

                        ParseFinished?.Invoke();
                    }
                    else
                    {
                        throw new Exception("MIDI File's probably corrupted.");
                    }
                }
                else
                {
                    throw new Exception("Header of MIDI File's probably corrupted.");
                }
            });
        }

        private bool ParseHeader()
        {
            // MThd = 4D 54 68 64
            if (Read32() == 0x4D546864)
            {
                uint length = Read32();
                if (length != 6) throw new Exception("Header length isn't 6.");
                ushort Format = Read16();
                if (Format == 2) throw new Exception("Format 2 not supported.");
                TrackCount = Read16();
                PPQ = Read16();
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool ParseTrackChunks()
        {
            while (reader.Position < reader.Length)
            {
                // MTrk = 4D 54 72 6B
                if (Read32() == 0x4D54726B)
                {
                    uint trackLen = Read32();
                    ulong trackStart = (ulong)reader.Position;
                    trackPos.Add(new TrackPointers { start = trackStart, length = (ulong)trackLen });
                    reader.Position += trackLen;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private ushort Read16()
        {
            ushort d = 0;
            for (int i = 0; i != 2; i++)
                d = (ushort)((d << 8) | (byte)reader.ReadByte());
            return d;
        }

        private uint Read32()
        {
            uint d = 0;
            for (int i = 0; i != 4; i++)
                d = (uint)((d << 8) | (byte)reader.ReadByte());
            return d;
        }

        public IEnumerable<Note>[] SplitNotesByKey(IEnumerable<Note> notes)
        {
            IEnumerable<Note>[] res = new List<Note>[256];
            for (int i = 0; i < 256; i++) res[i] = new List<Note>();
            foreach (Note n in notes)
            {
                res[n.key].Concat(new[] { n });
            }
            return res;
        }
    }
}
