using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pon_Player.MIDI;

namespace Pon_Player.Misc
{
    public static class Mergers
    {
        public static IEnumerable<RawEvent> MergeRawEvents(IEnumerable<RawEvent> s1, IEnumerable<RawEvent> s2)
        {
            var e1 = s1.GetEnumerator();
            var e2 = s2.GetEnumerator();
            RawEvent n1 = new RawEvent();
            RawEvent n2 = new RawEvent();
            if (e1.MoveNext()) n1 = e1.Current;
            if (e2.MoveNext()) n2 = e2.Current;

            while (true)
            {
                if (!n1.Equals(default(RawEvent)))
                {
                    if (!n2.Equals(default(RawEvent)))
                    {
                        if (n1.time < n2.time)
                        {
                            yield return n1;
                            if (e1.MoveNext()) n1 = e1.Current;
                            else n1 = new RawEvent();
                        }
                        else
                        {
                            yield return n2;
                            if (e2.MoveNext()) n2 = e2.Current;
                            else n2 = new RawEvent();
                        }
                    }
                    else
                    {
                        yield return n1;
                        if (e1.MoveNext()) n1 = e1.Current;
                        else n1 = new RawEvent();
                    }
                }
                else
                {
                    if (n2.Equals(default(RawEvent))) break;
                    else yield return n2;
                    if (e2.MoveNext()) n2 = e2.Current;
                    else n2 = new RawEvent();
                }
            }
        }
        public static IEnumerable<Note> MergeSequences(IEnumerable<Note> s1, IEnumerable<Note> s2)
        {
            var e1 = s1.GetEnumerator();
            var e2 = s2.GetEnumerator();
            Note n1 = new Note();
            Note n2 = new Note();
            if (e1.MoveNext()) n1 = e1.Current;
            if (e2.MoveNext()) n2 = e2.Current;

            while (true)
            {
                if (!n1.Equals(default(Note)))
                {
                    if (!n2.Equals(default(Note)))
                    {
                        if (n1.start < n2.start)
                        {
                            yield return n1;
                            if (e1.MoveNext()) n1 = e1.Current;
                            else n1 = new Note();
                        }
                        else
                        {
                            yield return n2;
                            if (e2.MoveNext()) n2 = e2.Current;
                            else n2 = new Note();
                        }
                    }
                    else
                    {
                        yield return n1;
                        if (e1.MoveNext()) n1 = e1.Current;
                        else n1 = new Note();
                    }
                } else
                {
                    if (n2.Equals(default(Note))) break;
                    else yield return n2;
                    if (e2.MoveNext()) n2 = e2.Current;
                    else n2 = new Note();
                }
            }
        }

        public static IEnumerable<TempoEvent> MergeTempos(IEnumerable<TempoEvent> s1, IEnumerable<TempoEvent> s2)
        {
            var e1 = s1.GetEnumerator();
            var e2 = s2.GetEnumerator();
            TempoEvent n1 = new TempoEvent();
            TempoEvent n2 = new TempoEvent();
            if (e1.MoveNext()) n1 = e1.Current;
            if (e2.MoveNext()) n2 = e2.Current;

            while (true)
            {
                if (!n1.Equals(default(TempoEvent)))
                {
                    if (!n2.Equals(default(TempoEvent)))
                    {
                        if (n1.time < n2.time)
                        {
                            yield return n1;
                            if (e1.MoveNext()) n1 = e1.Current;
                            else n1 = new TempoEvent();
                        }
                        else
                        {
                            yield return n2;
                            if (e2.MoveNext()) n2 = e2.Current;
                            else n2 = new TempoEvent();
                        }
                    }
                    else
                    {
                        yield return n1;
                        if (e1.MoveNext()) n1 = e1.Current;
                        else n1 = new TempoEvent();
                    }
                }
                else
                {
                    if (n2.Equals(default(TempoEvent))) break;
                    else yield return n2;
                    if (e2.MoveNext()) n2 = e2.Current;
                    else n2 = new TempoEvent();
                }
            }
        }

        public static IEnumerable<Note> MergeAllTrackNotes(IEnumerable<IEnumerable<Note>> trackNotes)
        {
            List<IEnumerable<Note>> b1 = new List<IEnumerable<Note>>();
            List<IEnumerable<Note>> b2 = new List<IEnumerable<Note>>();
            foreach (List<Note> s in trackNotes) b1.Add(s.AsEnumerable());
            if (b1.Count == 0) return new List<Note>();
            while (b1.Count > 1)
            {
                int pos = 0;
                while (pos < b1.Count)
                {
                    if (b1.Count - pos == 1)
                    {
                        b2.Add(b1[pos]);
                        pos++;
                    }
                    else
                    {
                        b2.Add(MergeSequences(b1[pos], b1[pos + 1]));
                        pos += 2;
                    }
                }
                b1 = b2;
                b2 = new List<IEnumerable<Note>>();
            }
            return b1[0];
        }

        public static IEnumerable<RawEvent> MergeAllRawEvents(List<IEnumerable<RawEvent>> rEvents)
        {
            List<IEnumerable<RawEvent>> b1 = new List<IEnumerable<RawEvent>>();
            List<IEnumerable<RawEvent>> b2 = new List<IEnumerable<RawEvent>>();
            foreach (List<RawEvent> s in rEvents) b1.Add(s.AsEnumerable());
            if (b1.Count == 0) return new List<RawEvent>();
            while (b1.Count > 1)
            {
                int pos = 0;
                while (pos < b1.Count)
                {
                    if (b1.Count - pos == 1)
                    {
                        b2.Add(b1[pos]);
                        pos++;
                    }
                    else
                    {
                        b2.Add(MergeRawEvents(b1[pos], b1[pos + 1]));
                        pos += 2;
                    }
                }
                b1 = b2;
                b2 = new List<IEnumerable<RawEvent>>();
            }
            return b1[0];
        }

        public static IEnumerable<TempoEvent> MergeAllTempoEvents(IEnumerable<IEnumerable<TempoEvent>> rEvents)
        {
            List<IEnumerable<TempoEvent>> b1 = new List<IEnumerable<TempoEvent>>();
            List<IEnumerable<TempoEvent>> b2 = new List<IEnumerable<TempoEvent>>();
            foreach (List<TempoEvent> s in rEvents) b1.Add(s.AsEnumerable());
            if (b1.Count == 0) return new List<TempoEvent>();
            while (b1.Count > 1)
            {
                int pos = 0;
                while (pos < b1.Count)
                {
                    if (b1.Count - pos == 1)
                    {
                        b2.Add(b1[pos]);
                        pos++;
                    }
                    else
                    {
                        b2.Add(MergeTempos(b1[pos], b1[pos + 1]));
                        pos += 2;
                    }
                }
                b1 = b2;
                b2 = new List<IEnumerable<TempoEvent>>();
            }
            return b1[0];
        }
    }
}
