using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>单段三次贝塞尔，全部为绝对 SVG 坐标（与 Inkscape / SVG 文档一致）。</summary>
[Serializable]
public struct SvgCubicAbsolute
{
    public Vector2 P0;
    public Vector2 C1;
    public Vector2 C2;
    public Vector2 P3;
}

/// <summary>解析后的路径图元（绝对坐标），不含展平采样点。</summary>
public enum SvgPathOpKind
{
    MoveTo,
    LineTo,
    CubicTo,
    ClosePath
}

[Serializable]
public struct SvgPathOp
{
    public SvgPathOpKind Kind;
    /// <summary>MoveTo / LineTo 的终点。</summary>
    public Vector2 P;
    /// <summary>CubicTo 时有效：绝对控制点与终点。</summary>
    public Vector2 CubicC1;
    public Vector2 CubicC2;
    public Vector2 CubicP3;
}

public static class SvgPathParser
{
    public static List<Vector2> Parse(string pathData, float stepLength = -1f)
    {
        List<List<Vector2>> segments = ParseSegments(pathData, stepLength);
        List<Vector2> result = new List<Vector2>();
        foreach (var seg in segments)
            result.AddRange(seg);
        return result;
    }

    public static List<List<Vector2>> ParseSegments(string pathData, float stepLength = -1f)
    {
        List<List<Vector2>> segments = new List<List<Vector2>>();
        if (string.IsNullOrEmpty(pathData)) return segments;

        List<Vector2> current_segment = new List<Vector2>();
        segments.Add(current_segment);

        StringReader reader = new StringReader(pathData.Trim());
        Vector2 current = Vector2.zero;
        Vector2 start = Vector2.zero;
        bool startSet = false;
        bool firstMove = true;

        bool relative = true;
        bool curve = false;
        int hvMode = 0; // 0=none, 1=horizontal, 2=vertical

        while (reader.Peek() != -1)
        {
            SkipWhitespaceAndCommas(reader);
            if (reader.Peek() == -1) break;

            char peeked = (char)reader.Peek();

            if (char.IsLetter(peeked))
            {
                char cmd = (char)reader.Read();

                switch (cmd)
                {
                    case 'M':
                        curve = false; relative = false; hvMode = 0;
                        if (!firstMove && current_segment.Count > 0)
                        {
                            current_segment = new List<Vector2>();
                            segments.Add(current_segment);
                        }
                        firstMove = false;
                        startSet = false;
                        ReadAbsolutePosition(reader, current_segment, current, stepLength, out current);
                        break;

                    case 'm':
                        curve = false; relative = true; hvMode = 0;
                        if (!firstMove && current_segment.Count > 0)
                        {
                            current_segment = new List<Vector2>();
                            segments.Add(current_segment);
                        }
                        firstMove = false;
                        startSet = false;
                        ReadRelativePosition(reader, current_segment, current, stepLength, out current);
                        break;

                    case 'L':
                        curve = false; relative = false; hvMode = 0;
                        ReadAbsolutePosition(reader, current_segment, current, stepLength, out current);
                        break;

                    case 'l':
                        curve = false; relative = true; hvMode = 0;
                        ReadRelativeLineTo(reader, current_segment, current, stepLength, out current);
                        break;

                    case 'H':
                        curve = false; relative = false; hvMode = 1;
                        ReadStraightLine(reader, current_segment, ref current, false, 1, stepLength);
                        break;
                    case 'h':
                        curve = false; relative = true; hvMode = 1;
                        ReadStraightLine(reader, current_segment, ref current, true, 1, stepLength);
                        break;
                    case 'V':
                        curve = false; relative = false; hvMode = 2;
                        ReadStraightLine(reader, current_segment, ref current, false, 2, stepLength);
                        break;
                    case 'v':
                        curve = false; relative = true; hvMode = 2;
                        ReadStraightLine(reader, current_segment, ref current, true, 2, stepLength);
                        break;

                    case 'C':
                        curve = true; relative = false; hvMode = 0;
                        ReadCurveTo(reader, current_segment, ref current, false, stepLength);
                        break;
                    case 'c':
                        curve = true; relative = true; hvMode = 0;
                        ReadCurveTo(reader, current_segment, ref current, true, stepLength);
                        break;

                    case 'Z':
                    case 'z':
                        if (startSet)
                            AddWithInterpolation(current_segment, current, start, stepLength);
                        current = start;
                        break;

                    default:
                        break;
                }
            }
            else if (IsNumberStart(peeked))
            {
                if (!curve)
                {
                    if (hvMode == 0)
                    {
                        if (!relative)
                            ReadAbsolutePosition(reader, current_segment, current, stepLength, out current);
                        else
                            ReadRelativeLineTo(reader, current_segment, current, stepLength, out current);
                    }
                    else
                    {
                        ReadStraightLine(reader, current_segment, ref current, relative, hvMode, stepLength);
                    }
                }
                else
                {
                    ReadCurveTo(reader, current_segment, ref current, relative, stepLength);
                }
            }
            else
            {
                reader.Read();
            }

            if (!startSet && current_segment.Count > 0)
            {
                start = current_segment[0];
                startSet = true;
            }
        }

        segments.RemoveAll(s => s.Count == 0);
        return segments;
    }

    // --- Command readers (matching RWR behavior) ---

    static void ReadAbsolutePosition(StringReader reader, List<Vector2> positions,
        Vector2 current, float stepLength, out Vector2 newCurrent)
    {
        Vector2 pos = ReadXY(reader);

        if (positions.Count > 0 && stepLength > 0f)
            InterpolateLinear(positions, current, pos, stepLength);

        positions.Add(pos);
        newCurrent = pos;
    }

    static void ReadRelativePosition(StringReader reader, List<Vector2> positions,
        Vector2 current, float stepLength, out Vector2 newCurrent)
    {
        Vector2 delta = ReadXY(reader);
        Vector2 pos = (positions.Count > 0 ? positions[positions.Count - 1] : current) + delta;

        if (positions.Count > 0 && stepLength > 0f)
        {
            Vector2 prev = positions[positions.Count - 1];
            InterpolateLinear(positions, prev, pos, stepLength);
        }

        positions.Add(pos);
        newCurrent = pos;
    }

    static void ReadRelativeLineTo(StringReader reader, List<Vector2> positions,
        Vector2 current, float stepLength, out Vector2 newCurrent)
    {
        Vector2 delta = ReadXY(reader);
        Vector2 prev = positions.Count > 0 ? positions[positions.Count - 1] : current;
        Vector2 pos = prev + delta;

        if (stepLength > 0f)
            InterpolateLinear(positions, prev, pos, stepLength);

        positions.Add(pos);
        newCurrent = pos;
    }

    static void ReadStraightLine(StringReader reader, List<Vector2> positions,
        ref Vector2 current, bool isRelative, int hvMode, float stepLength)
    {
        float value = ReadFloat(reader);
        Vector2 pos;

        if (isRelative)
        {
            pos = current;
            if (hvMode == 1) pos.x += value;
            else pos.y += value;
        }
        else
        {
            pos = current;
            if (hvMode == 1) pos.x = value;
            else pos.y = value;
        }

        if (positions.Count > 0 && stepLength > 0f)
            InterpolateLinear(positions, current, pos, stepLength);

        positions.Add(pos);
        current = pos;
    }

    static void ReadCurveTo(StringReader reader, List<Vector2> positions,
        ref Vector2 current, bool isRelative, float stepLength)
    {
        if (stepLength < 0f) stepLength = 2.0f;

        Vector2 prev = positions.Count > 0 ? positions[positions.Count - 1] : current;

        Vector2 cp1 = ReadXY(reader);
        if (isRelative) cp1 += prev;

        Vector2 cp2 = ReadXY(reader);
        if (isRelative) cp2 += prev;

        Vector2 end = ReadXY(reader);
        if (isRelative) end += prev;

        // Estimate arc length with 20 samples (matching RWR)
        float totalLength = 0f;
        Vector2 prevSample = prev;
        const int LENGTH_STEPS = 20;
        for (int i = 1; i <= LENGTH_STEPS; i++)
        {
            float t = i / (float)LENGTH_STEPS;
            Vector2 sample = CubicBezier(prev, cp1, cp2, end, t);
            totalLength += Vector2.Distance(sample, prevSample);
            prevSample = sample;
        }

        int steps = Mathf.Max(1, (int)(totalLength / stepLength));
        for (int i = 1; i < steps; i++)
        {
            float t = i / (float)steps;
            positions.Add(CubicBezier(prev, cp1, cp2, end, t));
        }

        positions.Add(end);
        current = end;
    }

    // --- Math (ported from RWR's hermite / de Casteljau) ---

    static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        Vector2 a = Vector2.Lerp(p0, p1, t);
        Vector2 b = Vector2.Lerp(p1, p2, t);
        Vector2 c = Vector2.Lerp(p2, p3, t);
        Vector2 d = Vector2.Lerp(a, b, t);
        Vector2 e = Vector2.Lerp(b, c, t);
        return Vector2.Lerp(d, e, t);
    }

    static void InterpolateLinear(List<Vector2> positions, Vector2 from, Vector2 to, float stepLength)
    {
        float totalLength = Vector2.Distance(from, to);
        int steps = (int)(totalLength / stepLength);
        for (int i = 1; i < steps; i++)
        {
            float t = i / (float)steps;
            positions.Add(Vector2.Lerp(from, to, t));
        }
    }

    static void AddWithInterpolation(List<Vector2> positions, Vector2 from, Vector2 to, float stepLength)
    {
        if (stepLength > 0f)
            InterpolateLinear(positions, from, to, stepLength);
        positions.Add(to);
    }

    // --- Token reading helpers ---

    static Vector2 ReadXY(StringReader reader)
    {
        float x = ReadFloat(reader);
        SkipWhitespaceAndCommas(reader);
        float y = ReadFloat(reader);
        return new Vector2(x, y);
    }

    static float ReadFloat(StringReader reader)
    {
        SkipWhitespaceAndCommas(reader);

        var sb = new System.Text.StringBuilder(16);
        bool hasDecimal = false;
        bool hasExponent = false;

        // Leading sign
        if (reader.Peek() == '-' || reader.Peek() == '+')
            sb.Append((char)reader.Read());

        while (reader.Peek() != -1)
        {
            char c = (char)reader.Peek();

            if (char.IsDigit(c))
            {
                sb.Append((char)reader.Read());
            }
            else if (c == '.' && !hasDecimal && !hasExponent)
            {
                hasDecimal = true;
                sb.Append((char)reader.Read());
            }
            else if ((c == 'e' || c == 'E') && !hasExponent)
            {
                hasExponent = true;
                sb.Append((char)reader.Read());
                if (reader.Peek() == '-' || reader.Peek() == '+')
                    sb.Append((char)reader.Read());
            }
            else
            {
                break;
            }
        }

        if (sb.Length == 0) return 0f;
        float.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float val);
        return val;
    }

    static void SkipWhitespaceAndCommas(StringReader reader)
    {
        while (reader.Peek() != -1)
        {
            char c = (char)reader.Peek();
            if (c == ' ' || c == ',' || c == '\t' || c == '\n' || c == '\r')
                reader.Read();
            else
                break;
        }
    }

    static bool IsNumberStart(char c)
    {
        return char.IsDigit(c) || c == '-' || c == '+' || c == '.';
    }

    // --- 曲线结构解析（不展平为折线）---

    /// <summary>
    /// 解析 SVG path 的 <c>d</c>，输出绝对坐标的图元序列（Move / Line / Cubic / Close）。
    /// 支持 Mm Ll Hh Vv Cc 及命令后隐式重复的坐标、Zz。
    /// CubicTo 时 <see cref="SvgPathOp.P"/> 为 P0，<see cref="SvgPathOp.CubicP3"/> 为终点。
    /// </summary>
    public static List<SvgPathOp> ParsePathDataToOps(string pathData)
    {
        var ops = new List<SvgPathOp>();
        if (string.IsNullOrWhiteSpace(pathData)) return ops;

        StringReader reader = new StringReader(pathData.Trim());
        Vector2 pen = Vector2.zero;
        Vector2 subpathStart = Vector2.zero;
        bool relative = true;
        bool curve = false;
        int hvMode = 0;

        while (reader.Peek() != -1)
        {
            SkipWhitespaceAndCommas(reader);
            if (reader.Peek() == -1) break;

            char peeked = (char)reader.Peek();

            if (char.IsLetter(peeked))
            {
                char cmd = (char)reader.Read();
                switch (cmd)
                {
                    case 'M':
                        curve = false;
                        relative = false;
                        hvMode = 0;
                        pen = ReadXY(reader);
                        subpathStart = pen;
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.MoveTo, P = pen });
                        break;

                    case 'm':
                        curve = false;
                        relative = true;
                        hvMode = 0;
                        {
                            Vector2 d = ReadXY(reader);
                            pen += d;
                        }
                        subpathStart = pen;
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.MoveTo, P = pen });
                        break;

                    case 'L':
                        curve = false;
                        relative = false;
                        hvMode = 0;
                        pen = ReadXY(reader);
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.LineTo, P = pen });
                        break;

                    case 'l':
                        curve = false;
                        relative = true;
                        hvMode = 0;
                        pen += ReadXY(reader);
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.LineTo, P = pen });
                        break;

                    case 'H':
                        curve = false;
                        relative = false;
                        hvMode = 1;
                        pen = new Vector2(ReadFloat(reader), pen.y);
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.LineTo, P = pen });
                        break;
                    case 'h':
                        curve = false;
                        relative = true;
                        hvMode = 1;
                        pen.x += ReadFloat(reader);
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.LineTo, P = pen });
                        break;
                    case 'V':
                        curve = false;
                        relative = false;
                        hvMode = 2;
                        pen = new Vector2(pen.x, ReadFloat(reader));
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.LineTo, P = pen });
                        break;
                    case 'v':
                        curve = false;
                        relative = true;
                        hvMode = 2;
                        pen.y += ReadFloat(reader);
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.LineTo, P = pen });
                        break;

                    case 'C':
                        curve = true;
                        relative = false;
                        hvMode = 0;
                        AppendCubicOp(reader, ops, ref pen, false);
                        break;
                    case 'c':
                        curve = true;
                        relative = true;
                        hvMode = 0;
                        AppendCubicOp(reader, ops, ref pen, true);
                        break;

                    case 'Z':
                    case 'z':
                        curve = false;
                        hvMode = 0;
                        pen = subpathStart;
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.ClosePath });
                        break;

                    default:
                        curve = false;
                        hvMode = 0;
                        break;
                }
            }
            else if (IsNumberStart(peeked))
            {
                if (!curve)
                {
                    if (hvMode == 0)
                    {
                        if (!relative)
                        {
                            pen = ReadXY(reader);
                            ops.Add(new SvgPathOp { Kind = SvgPathOpKind.LineTo, P = pen });
                        }
                        else
                        {
                            pen += ReadXY(reader);
                            ops.Add(new SvgPathOp { Kind = SvgPathOpKind.LineTo, P = pen });
                        }
                    }
                    else
                    {
                        float v = ReadFloat(reader);
                        if (relative)
                        {
                            if (hvMode == 1) pen.x += v;
                            else pen.y += v;
                        }
                        else
                        {
                            if (hvMode == 1) pen.x = v;
                            else pen.y = v;
                        }
                        ops.Add(new SvgPathOp { Kind = SvgPathOpKind.LineTo, P = pen });
                    }
                }
                else
                {
                    AppendCubicOp(reader, ops, ref pen, relative);
                }
            }
            else
            {
                reader.Read();
            }
        }

        return ops;
    }

    static void AppendCubicOp(StringReader reader, List<SvgPathOp> ops, ref Vector2 pen, bool isRelative)
    {
        Vector2 p0 = pen;
        Vector2 cp1 = ReadXY(reader);
        Vector2 cp2 = ReadXY(reader);
        Vector2 p3 = ReadXY(reader);
        if (isRelative)
        {
            cp1 += p0;
            cp2 += p0;
            p3 += p0;
        }
        ops.Add(new SvgPathOp
        {
            Kind = SvgPathOpKind.CubicTo,
            P = p0,
            CubicC1 = cp1,
            CubicC2 = cp2,
            CubicP3 = p3
        });
        pen = p3;
    }

    /// <summary>
    /// 从 <c>d</c> 中只提取各段三次贝塞尔（绝对坐标），顺序与路径一致；直线段不产生条目。
    /// 与 <see cref="Parse"/> 的展平折线不同，保留真实曲线控制点。
    /// </summary>
    public static List<SvgCubicAbsolute> ParsePathDataToAbsoluteCubics(string pathData)
    {
        var ops = ParsePathDataToOps(pathData);
        var cubics = new List<SvgCubicAbsolute>(ops.Count / 2);
        foreach (SvgPathOp op in ops)
        {
            if (op.Kind != SvgPathOpKind.CubicTo) continue;
            cubics.Add(new SvgCubicAbsolute
            {
                P0 = op.P,
                C1 = op.CubicC1,
                C2 = op.CubicC2,
                P3 = op.CubicP3
            });
        }
        return cubics;
    }
}
