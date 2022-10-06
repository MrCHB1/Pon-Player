﻿float Height;
float Left;
float Right;
float Aspect;
int ScreenWidth;
int ScreenHeight;

struct KEY
{
	dword colorl : COLORL;
	dword colorr : COLORR;
	float left : LEFT;
	float right : RIGHT;
	float distance : DISTANCE;
	dword meta : META;
};

struct QUAD 
{
	float2 v1;
	float4 c1;
	float2 v2;
	float4 c2;
	float2 v3;
	float4 c3;
	float2 v4;
	float4 c4;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float4 col : COLOR;
};

KEY VS(KEY input)
{
	return input;
}

float4 PS(PS_IN input) : SV_Target
{
	return input.col;
}

QUAD dim(QUAD q, float val) {
	q.c1.xyz += val;
	q.c2.xyz += val;
	q.c3.xyz += val;
	q.c4.xyz += val;
	return q;
}

void renderQuad(inout TriangleStream<PS_IN> OutputStream, QUAD quad) {
	PS_IN v = (PS_IN)0;

	v.pos = float4(quad.v1, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.col = quad.c1;
	OutputStream.Append(v);
	v.pos = float4(quad.v2, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.col = quad.c2;
	OutputStream.Append(v);
	v.pos = float4(quad.v3, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.col = quad.c3;
	OutputStream.Append(v);
	OutputStream.RestartStrip();

	v.pos = float4(quad.v1, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.col = quad.c1;
	OutputStream.Append(v);
	v.pos = float4(quad.v3, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.col = quad.c3;
	OutputStream.Append(v);
	v.pos = float4(quad.v4, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.col = quad.c4;
	OutputStream.Append(v);
	OutputStream.RestartStrip();
}

[maxvertexcount(6*3)]
void GS_Bar(point KEY input[1], inout TriangleStream<PS_IN> OutputStream) {
	KEY k = input[0];
	PS_IN v = (PS_IN)0;
	QUAD q = (QUAD)0;

	float4 color = float4(0.5, 0, 0, 1);
	q.c1 = color;
	q.c2 = color;
	q.c3 = color;
	q.c4 = color;
	q.v1 = float2(0, Height * 0.98 + (1.0 / ScreenHeight));
	q.v2 = float2(1, Height * 0.98 + (1.0 / ScreenHeight));
	q.v3 = float2(1, Height * 0.94 - (1.0 / ScreenHeight));
	q.v4 = float2(0, Height * 0.94 - (1.0 / ScreenHeight));
	renderQuad(OutputStream, dim(q, -0.0));

	color = float4(0.9, 0.1, 0.1, 1);
	q.c1 = color;
	q.c2 = color;
	q.c3 = color;
	q.c4 = color;
	q.v1 = float2(0, Height * 0.98);
	q.v2 = float2(1, Height * 0.98);
	q.v3 = float2(1, Height * 0.94);
	q.v4 = float2(0, Height * 0.94);
	renderQuad(OutputStream, dim(q, -0.0));
}

[maxvertexcount(6*4)]
void GS_White(point KEY input[1], inout TriangleStream<PS_IN> OutputStream) {
	KEY k = input[0];
	if (k.meta & 1) return;
	int pressed = k.meta & 2;
	PS_IN v = (PS_IN)0;
	QUAD q = (QUAD)0;

	float height = Height * 0.94;
	float left = (k.left - Left) / (Right - Left);
	float right = (k.right - Left) / (Right - Left);
	float top = height;
	float bottom = 0;

	float bez = 0.04;
	float itop = top - bez * height;
	float ibottom = bottom + bez * height * 1.4;
	if (pressed) ibottom = bottom + bez * height / 3;

	float4 colorlConv = float4((float)(k.colorl >> 24 & 0xff) / 255.0, (float)(k.colorl >> 16 & 0xff) / 255.0, (float)(k.colorl >> 8 & 0xff) / 255.0, (float)(k.colorl & 0xff) / 255.0);
	float4 colorrConv = float4((float)(k.colorr >> 24 & 0xff) / 255.0, (float)(k.colorr >> 16 & 0xff) / 255.0, (float)(k.colorr >> 8 & 0xff) / 255.0, (float)(k.colorr & 0xff) / 255.0);
	float4 colorl = float4(colorlConv.xyz * colorlConv.w + (1 - colorlConv.w), 1);
	float4 colorr = float4(colorrConv.xyz * colorrConv.w + (1 - colorrConv.w), 1);
	float4 coll = colorl;
	float4 colr = colorr;

	float4 colorL = float4(1, 0, 0, 1);
	float4 colorR = float4(1, 0, 0, 1);

	float4 coll2 = coll;
	coll2.xyz *= 0.8;
	q.c1 = coll2;
	q.c2 = coll2;
	q.c3 = colr;
	q.c4 = colr;
	q.v1 = float2(left, top);
	q.v2 = float2(right, top);
	q.v3 = float2(right, ibottom);
	q.v4 = float2(left, ibottom);
	renderQuad(OutputStream, dim(q, -0.0));

	colr.xyz *= 1.0;
	q.c1 = colr;
	q.c2 = colr;
	q.c3 = colr;
	q.c4 = colr;
	q.v1 = float2(left, ibottom);
	q.v2 = float2(right, ibottom);
	q.v3 = float2(right, bottom);
	q.v4 = float2(left, bottom);
	renderQuad(OutputStream, dim(q, -0.4));

	q.c1 = float4(0.1, 0.1, 0.1, 1);
	q.c2 = float4(0.1, 0.1, 0.1, 1);
	q.c3 = float4(0.1, 0.1, 0.1, 1);
	q.c4 = float4(0.1, 0.1, 0.1, 1);
	q.v1 = float2(left, ibottom);
	q.v2 = float2(right, ibottom);
	q.v3 = float2(right, ibottom + (1.0 / ScreenHeight));
	q.v4 = float2(left, ibottom + (1.0 / ScreenHeight));
	renderQuad(OutputStream, dim(q, 0.0));

	left = right - (1.0 / ScreenWidth);
	q.c1 = float4(0.1, 0.1, 0.1, 1);
	q.c2 = float4(0.1, 0.1, 0.1, 1);
	q.c3 = float4(0.1, 0.1, 0.1, 1);
	q.c4 = float4(0.1, 0.1, 0.1, 1);
	q.v1 = float2(left, top);
	q.v2 = float2(right, top);
	q.v3 = float2(right, bottom);
	q.v4 = float2(left, bottom);
	renderQuad(OutputStream, dim(q, 0.0));
}

[maxvertexcount(6 * 4)]
void GS_Black(point KEY input[1], inout TriangleStream<PS_IN> OutputStream) {
	KEY k = input[0];
	if (!(k.meta & 1)) return;
	int pressed = k.meta & 2;
	PS_IN v = (PS_IN)0;
	QUAD q = (QUAD)0;

	float height = Height * 0.94;

	float bez = 0.01;

	float left = (k.left - Left) / (Right - Left);
	float right = (k.right - Left) / (Right - Left);
	float top = height;
	float bottom = height * 0.3 + bez * height;

	float ileft = left + bez * height * Aspect;
	float iright = right - bez * height * Aspect;
	float itop = top + bez * height * 2.5;
	if (pressed) itop = top;
	float ibottom = bottom + bez * height;
	if (!pressed) ibottom = bottom + bez * height * 2.5;

	float4 colorlConv = float4((float)(k.colorl >> 24 & 0xff) / 255.0, (float)(k.colorl >> 16 & 0xff) / 255.0, (float)(k.colorl >> 8 & 0xff) / 255.0, (float)(k.colorl & 0xff) / 255.0);
	float4 colorrConv = float4((float)(k.colorr >> 24 & 0xff) / 255.0, (float)(k.colorr >> 16 & 0xff) / 255.0, (float)(k.colorr >> 8 & 0xff) / 255.0, (float)(k.colorr & 0xff) / 255.0);
	float4 colorl = float4(colorlConv.xyz * colorlConv.w, 1);
	float4 colorr = float4(colorrConv.xyz * colorrConv.w, 1);
	float4 coll = colorl;
	float4 colr = colorr;

	q.c1 = coll;
	q.c2 = coll;
	q.c3 = colr;
	q.c4 = colr;
	q.v1 = float2(ileft, itop);
	q.v2 = float2(iright, itop);
	q.v3 = float2(iright, ibottom);
	q.v4 = float2(ileft, ibottom);
	if (pressed)
		renderQuad(OutputStream, dim(q, -0.8));
	else
		renderQuad(OutputStream, dim(q, 0.0));

	q.c1 = coll;
	q.c2 = coll;
	q.c3 = colr;
	q.c4 = colr;
	q.v1 = float2(ileft+(1.0/ScreenWidth), itop);
	q.v2 = float2(iright-(1.0/ScreenWidth), itop);
	q.v3 = float2(iright-(1.0/ScreenWidth), ibottom+(1.0/ScreenHeight));
	q.v4 = float2(ileft+(1.0/ScreenWidth), ibottom+(1.0/ScreenHeight));
	if (pressed)
		renderQuad(OutputStream, dim(q, -0.0));
	else
		renderQuad(OutputStream, dim(q, 0.0));
}