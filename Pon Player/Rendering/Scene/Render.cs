
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using SharpDX.Direct3D;
using System.Collections.Concurrent;
using IO = System.IO;
using System.Reflection;
using System.Threading;
using SharpDX.Mathematics.Interop;
using Pon_Player.MIDI;
using Pon_Player.Misc;

namespace Pon_Player.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NoteCol
    {
        public uint rgba;
        public uint rgba2;

        public static uint Compress(byte r, byte g, byte b, byte a)
        {
            return (uint)((r << 24) & 0xff000000) |
                       (uint)((g << 16) & 0xff0000) |
                       (uint)((b << 8) & 0xff00) |
                       (uint)(a & 0xff);
        }

        public static uint Blend(uint from, uint with)
        {
            RawVector4 fromv = new RawVector4((float)(from >> 24 & 0xff) / 255.0f, (float)(from >> 16 & 0xff) / 255.0f, (float)(from >> 8 & 0xff) / 255.0f, (float)(from & 0xff) / 255.0f);
            RawVector4 withv = new RawVector4((float)(with >> 24 & 0xff) / 255.0f, (float)(with >> 16 & 0xff) / 255.0f, (float)(with >> 8 & 0xff) / 255.0f, (float)(with & 0xff) / 255.0f);

            float blend = withv.W;
            float revBlend = (1 - withv.W) * fromv.W;

            return Compress(
                    (byte)((fromv.X * revBlend + withv.X * blend) * 255),
                    (byte)((fromv.Y * revBlend + withv.Y * blend) * 255),
                    (byte)((fromv.Z * revBlend + withv.Z * blend) * 255),
                    (byte)((blend + revBlend) * 255)
                );
        }
    }


    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    struct NotesGlobalConstants
    {
        public float NoteLeft;
        public float NoteRight;
        public float NoteBorder;
        public float ScreenAspect;
        public float KeyboardHeight;
        public int ScreenWidth;
        public int ScreenHeight;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    struct KeyboardGlobalConstants
    {
        public float Height;
        public float Left;
        public float Right;
        public float Aspect;
        public int ScreenWidth;
        public int ScreenHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RenderNote
    {
        public float start;
        public float end;
        public NoteCol color;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RenderKey
    {
        public uint colorl;
        public uint colorr;
        public float left;
        public float right;
        public float distance;
        public uint meta;

        public void MarkPressed(bool ispressed)
        {
            meta = (uint)(meta & 0b11111101);
            if (ispressed)
                meta = (uint)(meta | 0b10);
        }

        public void MarkBlack(bool black)
        {
            meta = (uint)(meta & 0b11111110);
            if (black)
                meta = (uint)(meta | 0b1);
        }
    }

    class Render : IDisposable
    {
        ShaderManager noteShader;
        ShaderManager whiteKeyShader;
        ShaderManager blackKeyShader;
        ShaderManager kbBarShader;
        InputLayout noteLayout;
        InputLayout keyLayout;
        Buffer globalNoteConstants;
        Buffer keyBuffer;

        NotesGlobalConstants noteConstants;

        RenderKey[] renderKeys = new RenderKey[128];
        bool[] blackKeys = new bool[128];
        int[] keynum = new int[128];
        int[] bkIDs;
        int[] wkIDs;

        double[] x1array = new double[128];
        double[] wdtharray = new double[128];
        bool[] pressedKeys = new bool[128];

        Buffer noteBuffer;
        int noteBufferLength = 1 << 12;

        bool singleThreaded = true;

        object fileLock = new object();
        object addLock = new object();

        public Note[][] notesToRender = null;
        public double lastTime = 0;

        private GlobalTime _time = new GlobalTime();
        public GlobalTime Time 
        {
            get => _time;
            set
            {
                if (Time != null)
                    Time.TimeChanged -= onTimeChanged;
                _time = value;
                Time.TimeChanged += onTimeChanged;
            }
        }

        bool timeChanged = false;
        void onTimeChanged() => timeChanged = true;

        int[] firstUnhitNote = new int[128];
        int[] firstRenderNote = new int[128];

        public int NotesPassed = 0;

        DisposeGroup dispose = new DisposeGroup();
        public Render(Device dev)
        {
            for (int i = 0; i < 128; i++)
            {
                firstRenderNote[i] = 0;
                firstUnhitNote[i] = 0;
            }
            string GetShaderCode(string filename, string resourceName)
            {
                if (IO.File.Exists(filename))
                {
                    return IO.File.ReadAllText(filename);
                }
                else
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var names = assembly.GetManifestResourceNames();
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    using (var reader = new IO.StreamReader(stream))
                        return reader.ReadToEnd();
                }
            }

            string noteShaderData = GetShaderCode("Notes.fx", "Pon_Player.Rendering.Scene.Notes.fx");
            noteShader = dispose.Add(new ShaderManager(
                dev,
                ShaderBytecode.Compile(noteShaderData, "VS_Note", "vs_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(noteShaderData, "PS", "ps_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(noteShaderData, "GS_Note", "gs_4_0", ShaderFlags.None, EffectFlags.None)
            ));

            string keyboardShaderData = GetShaderCode("Keyboard.fx", "Pon_Player.Rendering.Scene.Keyboard.fx");
            whiteKeyShader = dispose.Add(new ShaderManager(
                dev,
                ShaderBytecode.Compile(keyboardShaderData, "VS", "vs_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(keyboardShaderData, "PS", "ps_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(keyboardShaderData, "GS_White", "gs_4_0", ShaderFlags.None, EffectFlags.None)
            ));
            blackKeyShader = dispose.Add(new ShaderManager(
                dev,
                ShaderBytecode.Compile(keyboardShaderData, "VS", "vs_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(keyboardShaderData, "PS", "ps_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(keyboardShaderData, "GS_Black", "gs_4_0", ShaderFlags.None, EffectFlags.None)
            ));

            kbBarShader = dispose.Add(new ShaderManager(
                dev,
                ShaderBytecode.Compile(keyboardShaderData, "VS", "vs_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(keyboardShaderData, "PS", "ps_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(keyboardShaderData, "GS_Bar", "gs_4_0", ShaderFlags.None, EffectFlags.None)
            ));

            noteLayout = dispose.Add(new InputLayout(dev, ShaderSignature.GetInputSignature(noteShader.vertexShaderByteCode), new[] {
                new InputElement("START",0,Format.R32_Float,0),
                new InputElement("END",0,Format.R32_Float,0),
                new InputElement("COLORL",0,Format.R32_UInt,0),
                new InputElement("COLORR",0,Format.R32_UInt,0),
            }));

            keyLayout = dispose.Add(new InputLayout(dev, ShaderSignature.GetInputSignature(whiteKeyShader.vertexShaderByteCode), new[] {
                new InputElement("COLORL",0,Format.R32_UInt,0),
                new InputElement("COLORR",0,Format.R32_UInt,0),
                new InputElement("LEFT",0,Format.R32_Float,0),
                new InputElement("RIGHT",0,Format.R32_Float,0),
                new InputElement("DISTANCE",0,Format.R32_Float,0),
                new InputElement("META",0,Format.R32_UInt,0),
            }));

            noteConstants = new NotesGlobalConstants()
            {
                NoteBorder = 0.002f,
                NoteLeft = -0.2f,
                NoteRight = 0.0f,
                ScreenAspect = 1f
            };

            noteBuffer = dispose.Add(new Buffer(dev, new BufferDescription()
            {
                BindFlags = BindFlags.VertexBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = 40 * noteBufferLength,
                Usage = ResourceUsage.Dynamic,
                StructureByteStride = 0
            }));


            keyBuffer = dispose.Add(new Buffer(dev, new BufferDescription()
            {
                BindFlags = BindFlags.VertexBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = 24 * renderKeys.Length,
                Usage = ResourceUsage.Dynamic,
                StructureByteStride = 0
            }));

            globalNoteConstants = dispose.Add(new Buffer(dev, new BufferDescription()
            {
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = 32,
                Usage = ResourceUsage.Dynamic,
                StructureByteStride = 0
            }));

            for (int i = 0; i < blackKeys.Length; i++) 
            {
                var k = i % 12;
                blackKeys[i] = (k == 1) || (k == 3) || (k == 6) || (k == 8) || (k == 10);
            }
            int b= 0, w = 0;
            List<int> black = new List<int>();
            List<int> white = new List<int>();
            for (int i = 0; i < keynum.Length; i++)
            {
                if (blackKeys[i])
                {
                    keynum[i] = b++;
                    if (i < 128) black.Add(i);
                }
                else
                {
                    keynum[i] = w++;
                    if (i < 128) white.Add(i);
                }
            }

            bkIDs = black.ToArray();
            wkIDs = white.ToArray();

            int fn = 0;
            int ln = 127;

            double wdth;

            double blackKeyScale = 0.75;
            double offset2set = 0.3;
            double offset3set = 0.5;

            double knmfn = keynum[fn];
            double knmln = keynum[ln - 1];
            if (blackKeys[fn]) knmfn = keynum[fn - 1] + 0.5;
            if (blackKeys[ln - 1]) knmln = keynum[ln] - 0.5;
            for (int i = 0; i < 128; i++)
            {
                if (!blackKeys[i])
                {
                    x1array[i] = (float)(keynum[i] - knmfn) / (knmln - knmfn + 1);
                    wdtharray[i] = 1.0f / (knmln - knmfn + 1);
                }
                else
                {
                    int _i = i + 1;
                    wdth = blackKeyScale / (knmln - knmfn + 1);
                    int bknum = keynum[i] % 5;
                    double offset = wdth / 2;
                    if (bknum == 0) offset += offset * offset2set;
                    if (bknum == 2) offset += offset * offset3set;
                    if (bknum == 1) offset -= offset * offset2set;
                    if (bknum == 4) offset -= offset * offset3set;

                    x1array[i] = (float)(keynum[_i] - knmfn) / (knmln - knmfn + 1) - offset;
                    wdtharray[i] = wdth;
                    renderKeys[i].MarkBlack(true);
                }
                renderKeys[i].left = (float)x1array[i];
                renderKeys[i].right = (float)(x1array[i] + wdtharray[i]);
            }

            var renderTargetDesc = new RenderTargetBlendDescription();
            renderTargetDesc.IsBlendEnabled = true;
            renderTargetDesc.SourceBlend = BlendOption.SourceAlpha;
            renderTargetDesc.DestinationBlend = BlendOption.InverseSourceAlpha;
            renderTargetDesc.BlendOperation = BlendOperation.Add;
            renderTargetDesc.SourceAlphaBlend = BlendOption.One;
            renderTargetDesc.DestinationAlphaBlend = BlendOption.One;
            renderTargetDesc.AlphaBlendOperation = BlendOperation.Add;
            renderTargetDesc.RenderTargetWriteMask = ColorWriteMaskFlags.All;

            BlendStateDescription desc = new BlendStateDescription();
            desc.AlphaToCoverageEnable = false;
            desc.IndependentBlendEnable = false;
            desc.RenderTarget[0] = renderTargetDesc;

            var blendStateEnabled = dispose.Add(new BlendState(dev, desc));

            dev.ImmediateContext.OutputMerger.SetBlendState(blendStateEnabled);

            RasterizerStateDescription renderStateDesc = new RasterizerStateDescription
            {
                CullMode = CullMode.None,
                DepthBias = 0,
                DepthBiasClamp = 0,
                FillMode = FillMode.Solid,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = false,
                IsFrontCounterClockwise = false,
                IsMultisampleEnabled = true,
                IsScissorEnabled = false,
                SlopeScaledDepthBias = 0
            };
            RasterizerState rasterStateSolid = dispose.Add(new RasterizerState(dev, renderStateDesc));
            dev.ImmediateContext.Rasterizer.State = rasterStateSolid;
        }

        public void DrawFrame(Device dev, RenderTargetView target, DrawEventArgs args)
        {
            var ctx = dev.ImmediateContext;
            ctx.InputAssembler.InputLayout = noteLayout;

            double time = Time.GetTime();
            double timeScale = 0.25;
            double renderCutoff = time + timeScale;

            int fn = 0;
            int ln = 128;
            int kbfn = fn;
            int kbln = ln;
            if (blackKeys[fn]) kbfn--;
            if (blackKeys[ln-1]) kbln++;

            noteShader.SetShaders(ctx);
            noteConstants.ScreenAspect = (float)(args.RenderSize.Height / args.RenderSize.Width);
            noteConstants.NoteBorder = 0.0015f;
            noteConstants.ScreenWidth = (int)args.RenderSize.Width;
            noteConstants.ScreenHeight = (int)args.RenderSize.Height;
            SetNoteShaderConstants(ctx, noteConstants);

            ctx.ClearRenderTargetView(target, new RawColor4(0.0f, 0.0f, 0.0f, 1.0f));

            double fullLeft = x1array[fn];
            double fullRight = x1array[ln - 1] + wdtharray[ln - 1];
            double fullWidth = fullRight - fullLeft;

            float kbHeight = (float)(args.RenderSize.Width / args.RenderSize.Height / fullWidth);
            noteConstants.KeyboardHeight = kbHeight*0.095f;

            lock (fileLock)
            {
                if (notesToRender != null)
                {
                    var lt = lastTime;
                    int[] ids;
                    for (int black = 1; black >= 0; black--)
                    {
                        if (black == 0) ids = bkIDs;
                        else ids = wkIDs;
                        Parallel.ForEach(ids, new ParallelOptions() { MaxDegreeOfParallelism = 8 }, k =>
                        {
                            long _nr = 0;
                            float left = (float)((x1array[k] - fullLeft) / fullWidth);
                            float right = (float)((x1array[k] + wdtharray[k] - fullLeft) / fullWidth);
                            bool pressed = false;
                            int lastHitNote = firstUnhitNote[k] - 1;
                            NoteCol col = new NoteCol();
                            unsafe
                            {
                                RenderNote* rn = stackalloc RenderNote[noteBufferLength];
                                int nid = 0;
                                int noff = firstRenderNote[k];
                                Note[] notes = notesToRender[k];
                                if (notes.Length == 0) goto skipLoop;
                                if (lt > time)
                                {
                                    for (noff = 0; noff < notes.Length; noff++)
                                    {
                                        if (notes[noff].start + notes[noff].length > time)
                                            break;
                                    }
                                    firstRenderNote[k] = noff;
                                }
                                else if (lt < time)
                                {
                                    for (; noff < notes.Length; noff++)
                                    {
                                        if (notes[noff].start + notes[noff].length > time)
                                            break;
                                    }
                                    firstRenderNote[k] = noff;
                                }
                                while (noff != notes.Length && notes[noff].start < renderCutoff)
                                {
                                    var n = notes[noff++];
                                    if (n.start + n.length < time)
                                    {
                                        lastHitNote = noff - 1;
                                        continue;
                                    }
                                    if (n.start < time)
                                    {
                                        pressed = true;
                                        //col.rgba = NoteCol.Compress((byte)(n.channel * 16), (byte)(n.channel * 16), (byte)(n.channel * 16), 255);
                                        //col.rgba2 = NoteCol.Compress((byte)(n.channel * 16), (byte)(n.channel * 16), (byte)(n.channel * 16), 255);
                                        col.rgba = (uint)Colors.HSVtoRGB(n.channel * (360/16),1,1);
                                        col.rgba2 = (uint)Colors.HSVtoRGB(n.channel * (360/16), 1, 1);
                                        lastHitNote = noff - 1;
                                    }
                                    _nr++;
                                    rn[nid++] = new RenderNote()
                                    {
                                        start = (float)((n.start - time) / timeScale),
                                        end = (float)(((n.start + n.length) - time) / timeScale),
                                        color = new NoteCol { rgba = (uint)Colors.HSVtoRGB(n.channel * (360 / 16), 1, 1), rgba2 = (uint)Colors.HSVtoRGB(n.channel * (360 / 16), 1, 1) }
                                    };
                                    if (nid == noteBufferLength)
                                    {
                                        FlushNoteBuffer(ctx, left, right, (IntPtr)rn, nid);
                                        nid = 0;
                                    }
                                }
                                FlushNoteBuffer(ctx, left, right, (IntPtr)rn, nid);
                            skipLoop:
                                renderKeys[k].colorl = col.rgba;
                                renderKeys[k].colorr = col.rgba2;
                                renderKeys[k].MarkPressed(pressed);
                                if (_nr == 0) lastHitNote = firstRenderNote[k] - 1;
                                firstUnhitNote[k] = lastHitNote + 1;
                            }
                        });
                    }
                    NotesPassed = firstUnhitNote.Select(s => s).Sum();
                    lastTime = time;
                }
                else
                {
                    for (int i = 0; i < renderKeys.Length; i++)
                    {
                        renderKeys[i].colorl = 0;
                        renderKeys[i].colorr = 0;
                        renderKeys[i].distance = 0;
                    }
                    lastTime = 0;
                    NotesPassed = 0;
                    int[] firstUnhitNote = new int[128];
                    int[] firstRenderNote = new int[128];
                }
            }

            DataStream data;
            SetKeyboardShaderConstants(ctx, new KeyboardGlobalConstants()
            {
                Height = kbHeight*0.1f,
                Left = (float)fullLeft,
                Right = (float)fullRight,
                Aspect = noteConstants.ScreenAspect,
                ScreenWidth = (int)args.RenderSize.Width,
                ScreenHeight = (int)args.RenderSize.Height
            });
            ctx.InputAssembler.InputLayout = keyLayout;
            ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.PointList;
            ctx.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(keyBuffer, 24, 0));
            ctx.MapSubresource(keyBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Position = 0;
            data.WriteRange(renderKeys, 0, 128);
            ctx.UnmapSubresource(keyBuffer, 0);
            data.Close();
            whiteKeyShader.SetShaders(ctx);
            ctx.Draw(kbln - kbfn, kbfn);
            kbBarShader.SetShaders(ctx);
            ctx.Draw(1, 0);
            blackKeyShader.SetShaders(ctx);
            ctx.Draw(kbln - kbfn, kbfn);
        }

        unsafe void FlushNoteBuffer(DeviceContext context, float left, float right, IntPtr notes, int count)
        {
            if (count == 0) return;
            if (singleThreaded) Monitor.Enter(context);
            DataStream data;
            context.MapSubresource(noteBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Position = 0;
            data.WriteRange(notes, count * sizeof(RenderNote));
            context.UnmapSubresource(noteBuffer, 0);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.PointList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(noteBuffer, 16, 0));
            noteConstants.NoteLeft = left;
            noteConstants.NoteRight = right;
            SetNoteShaderConstants(context, noteConstants);
            context.Draw(count, 0);
            data.Dispose();
            if (singleThreaded) Monitor.Exit(context);
        }

        void SetNoteShaderConstants(DeviceContext context, NotesGlobalConstants constants)
        {
            DataStream data;
            context.MapSubresource(globalNoteConstants, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Write(constants);
            context.UnmapSubresource(globalNoteConstants, 0);
            context.VertexShader.SetConstantBuffer(0, globalNoteConstants);
            context.GeometryShader.SetConstantBuffer(0, globalNoteConstants);
            data.Dispose();
        }

        void SetKeyboardShaderConstants(DeviceContext context, KeyboardGlobalConstants constants)
        {
            DataStream data;
            context.MapSubresource(globalNoteConstants, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Write(constants);
            context.UnmapSubresource(globalNoteConstants, 0);
            context.VertexShader.SetConstantBuffer(0, globalNoteConstants);
            context.GeometryShader.SetConstantBuffer(0, globalNoteConstants);
            data.Dispose();
        }

        public void Dispose()
        {
            dispose.Dispose();
        }
    }
}
