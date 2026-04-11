using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class SvgPathParser
{
    public static List<Vector2> Parse(string pathData, float stepLength = -1f)
    {
        List<Vector2> result = new List<Vector2>();
        if (string.IsNullOrEmpty(pathData)) return result;

        StringReader reader = new StringReader(pathData.Trim());
        Vector2 current = Vector2.zero;
        Vector2 start = Vector2.zero;
        bool startSet = false;

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
                        if (result.Count > 0)
                        {
                            // mid-path M acts as a jump; keep accumulated points
                        }
                        ReadAbsolutePosition(reader, result, current, stepLength, out current);
                        break;

                    case 'm':
                        curve = false; relative = true; hvMode = 0;
                        Vector2 offset = current;
                        ReadRelativePosition(reader, result, current, stepLength, out current);
                        break;

                    case 'L':
                        curve = false; relative = false; hvMode = 0;
                        ReadAbsolutePosition(reader, result, current, stepLength, out current);
                        break;

                    case 'l':
                        curve = false; relative = true; hvMode = 0;
                        ReadRelativeLineTo(reader, result, current, stepLength, out current);
                        break;

                    case 'H':
                        curve = false; relative = false; hvMode = 1;
                        ReadStraightLine(reader, result, ref current, false, 1, stepLength);
                        break;
                    case 'h':
                        curve = false; relative = true; hvMode = 1;
                        ReadStraightLine(reader, result, ref current, true, 1, stepLength);
                        break;
                    case 'V':
                        curve = false; relative = false; hvMode = 2;
                        ReadStraightLine(reader, result, ref current, false, 2, stepLength);
                        break;
                    case 'v':
                        curve = false; relative = true; hvMode = 2;
                        ReadStraightLine(reader, result, ref current, true, 2, stepLength);
                        break;

                    case 'C':
                        curve = true; relative = false; hvMode = 0;
                        ReadCurveTo(reader, result, ref current, false, stepLength);
                        break;
                    case 'c':
                        curve = true; relative = true; hvMode = 0;
                        ReadCurveTo(reader, result, ref current, true, stepLength);
                        break;

                    case 'Z':
                    case 'z':
                        if (startSet)
                            AddWithInterpolation(result, current, start, stepLength);
                        current = start;
                        break;

                    default:
                        // Unknown command, skip
                        break;
                }
            }
            else if (IsNumberStart(peeked))
            {
                // Implicit continuation of previous command
                if (!curve)
                {
                    if (hvMode == 0)
                    {
                        if (!relative)
                            ReadAbsolutePosition(reader, result, current, stepLength, out current);
                        else
                            ReadRelativeLineTo(reader, result, current, stepLength, out current);
                    }
                    else
                    {
                        ReadStraightLine(reader, result, ref current, relative, hvMode, stepLength);
                    }
                }
                else
                {
                    ReadCurveTo(reader, result, ref current, relative, stepLength);
                }
            }
            else
            {
                reader.Read(); // consume unrecognized character
            }

            if (!startSet && result.Count > 0)
            {
                start = result[0];
                startSet = true;
            }
        }

        return result;
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
}
