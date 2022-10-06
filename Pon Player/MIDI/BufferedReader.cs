using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Pon_Player.MIDI
{
    class BufferedReader
    {
        private Stream reader;
        private ulong start;
        private ulong length;
        private ulong pos;
        private byte[] buf;
        private uint bufSize;
        private uint bufPos = 0;
        private ulong bufStart;
        private object bufMutex;

        public BufferedReader(Stream stream, ulong start, ulong length, uint bufSize, object bufMutex)
        {
            reader = stream;
            this.start = start;
            this.length = length;
            this.bufSize = bufSize;
            pos = this.start;
            this.bufMutex = bufMutex;

            if (bufSize > length) this.bufSize = (uint)this.length;
            buf = new byte[this.bufSize];

            UpdateBuffer();
        }

        public void Seek(long offs, int origin)
        {
            ulong realOffs = (ulong)offs;
            if (origin == 0) realOffs += start;
            else realOffs += pos;

            if (realOffs < start) throw new Exception("Seek before start. Offset: " + realOffs);
            if (realOffs > start + length) throw new Exception("Seek past end. Offset: " + realOffs);

            pos = realOffs;

            if (bufStart <= realOffs && realOffs < bufStart + bufSize)
            {
                bufPos = (uint)(pos - bufStart);
                return;
            }

            UpdateBuffer();
        }

        public byte ReadByte()
        {
            if (bufStart + bufPos + 1 > bufStart + bufSize) UpdateBuffer();
            pos++;
            return buf[bufPos++];
        }

        public void SkipBytes(ulong size)
        {
            Seek((long)size, 1);
        }

        private void UpdateBuffer()
        {
            uint rd = bufSize;

            if ((pos + rd) > (start + length)) rd = (uint)(start + length - pos);

            lock (bufMutex) {
                reader.Position = (long)pos;
                reader.Read(buf, 0, (int)rd);
            }
            bufStart = pos;
            bufPos = 0;
        }
    }
}
