using System;
using System.Collections.Generic;
using UnityEngine;

namespace DT_Tools.Patches.Fun.LiveDraw
{
    /// <summary>单条笔画的基准数据（动画永远从基准点偏移，不累计漂移）。</summary>
    internal sealed class LiveStroke
    {
        public int Id;                 // 实际落笔后从 DrawingManager 拿到的 StrokeId（动画 REPLACE 复用同一 id）
        public float Width;
        public float Ph;               // 每条笔画的抖动相位
        public List<Vector2> Base = new List<Vector2>();
    }

    /// <summary>
    /// 照片 → 笔画管线（原 DT_LiveDraw v1.0.0 移植，逻辑不变）：
    /// 缩放 → 灰度 → 盒式模糊×3 → 多阈值 marching squares → Douglas-Peucker 简化 →
    /// 最小点距 2 → 预算裁剪（笔画/点数上限走配置），绘制顺序按从上往下像真人作画。
    /// 画布几何 879×696（X±439.5 / Y±348），输出点始终落在画布范围内。
    /// </summary>
    internal static class DrawConverter
    {
        private const float CanvasHalfW = 439.5f;
        private const float CanvasHalfH = 348f;
        private const float Margin = 32f;

        /// <summary>把照片字节解析为线稿笔画计划；失败/无内容返回 null。</summary>
        public static List<LiveStroke> BuildPlan(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(tex, bytes, false)) return null;
                return BuildPlanFromPixels(tex.GetPixels32(), tex.width, tex.height);
            }
            catch (Exception e)
            {
                Log.Error<LiveDrawFeature>($"照片解析失败: {e.Message}");
                return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }

        private static List<LiveStroke> BuildPlanFromPixels(Color32[] src, int iw, int ih)
        {
            float availW = (CanvasHalfW * 2f - Margin * 2f);
            float availH = (CanvasHalfH * 2f - Margin * 2f);
            float ratio = (float)iw / ih;
            float fitW, fitH;
            if (ratio > 1f) { fitW = availW; fitH = availW / ratio; }
            else { fitH = availH; fitW = availH * ratio; }

            // 缩到工作网格（长边 200），同时算灰度
            int gw = fitW >= fitH ? 200 : Mathf.Max(8, Mathf.RoundToInt(200 * fitW / fitH));
            int gh = fitW >= fitH ? Mathf.Max(8, Mathf.RoundToInt(200 * fitH / fitW)) : 200;
            float[] gray = new float[gw * gh];
            for (int y = 0; y < gh; y++)
            {
                int sy0 = y * ih / gh, sy1 = Mathf.Max(sy0 + 1, (y + 1) * ih / gh);
                for (int x = 0; x < gw; x++)
                {
                    int sx0 = x * iw / gw, sx1 = Mathf.Max(sx0 + 1, (x + 1) * iw / gw);
                    float acc = 0f; int n = 0;
                    for (int sy = sy0; sy < sy1 && sy < ih; sy++)
                    for (int sx = sx0; sx < sx1 && sx < iw; sx++)
                    {
                        Color32 c = src[sy * iw + sx];
                        acc += (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                        n++;
                    }
                    gray[y * gw + x] = n > 0 ? acc / n : 1f;
                }
            }

            for (int pass = 0; pass < 3; pass++) gray = BoxBlur(gray, gw, gh);

            float[] sorted = (float[])gray.Clone();
            Array.Sort(sorted);
            float lo = sorted[Mathf.Min(sorted.Length - 1, sorted.Length * 6 / 100)];
            float hi = sorted[Mathf.Min(sorted.Length - 1, sorted.Length * 94 / 100)];
            if (hi - lo < 0.03f) { lo = 0.25f; hi = 0.75f; }

            var all = new List<LiveStroke>();
            const int K = 4;
            for (int k = 0; k < K; k++)
            {
                float level = lo + (hi - lo) * (0.30f + 0.52f * k / (K - 1));
                foreach (var poly in TraceLevel(gray, gw, gh, level))
                {
                    var simp = Simplify(poly, 0.48f);
                    var pts = new List<Vector2>();
                    for (int i = 0; i < simp.Count; i++)
                    {
                        // y 映射：图像顶部（y=0）→ 画布 +fitH/2（画布 y 轴朝下时为顶部）；
                        // 原版方向为 (0.5 - y/gh) 会把照片上下颠倒（庭审画布 y 轴朝下），此处修正。
                        var v = new Vector2(
                            (simp[i].x / gw - 0.5f) * fitW,
                            (simp[i].y / gh - 0.5f) * fitH);
                        int n = pts.Count;
                        if (n == 0 || (v - pts[n - 1]).sqrMagnitude >= 4f) pts.Add(v); // 原版点距 ≥2
                    }
                    if (pts.Count < 2) continue;
                    float len = 0f;
                    for (int i = 1; i < pts.Count; i++) len += Vector2.Distance(pts[i], pts[i - 1]);
                    if (len < 16f) continue;
                    all.Add(new LiveStroke
                    {
                        Base = pts,
                        Width = k == 0 ? LiveDrawFeature.StrokeWidth : Mathf.Max(2f, LiveDrawFeature.StrokeWidth * 0.6f),
                        Ph = UnityEngine.Random.value * 6.2832f
                    });
                }
            }

            // 预算裁剪：优先保留最长的轮廓
            all.Sort((a, b) => StrokeLen(b).CompareTo(StrokeLen(a)));
            var plan = new List<LiveStroke>();
            int total = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (plan.Count >= LiveDrawFeature.MaxStrokes) break;
                int c = all[i].Base.Count;
                if (total + c > LiveDrawFeature.MaxPoints) continue;
                plan.Add(all[i]);
                total += c;
            }

            // 绘制顺序：从上往下，看起来像真人作画
            plan.Sort((a, b) =>
            {
                int t = b.Base[0].y.CompareTo(a.Base[0].y);
                return t != 0 ? t : a.Base[0].x.CompareTo(b.Base[0].x);
            });
            return plan;
        }

        private static float StrokeLen(LiveStroke s)
        {
            float l = 0f;
            for (int i = 1; i < s.Base.Count; i++) l += Vector2.Distance(s.Base[i], s.Base[i - 1]);
            return l;
        }

        private static float[] BoxBlur(float[] v, int gw, int gh)
        {
            var o = new float[gw * gh];
            for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
            {
                float s = 0f; int c = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                    s += v[ny * gw + nx]; c++;
                }
                o[y * gw + x] = s / c;
            }
            return o;
        }

        // marching squares：对灰度场做等值线追踪，返回网格坐标折线
        private static List<List<Vector2>> TraceLevel(float[] v, int gw, int gh, float L)
        {
            var segs = new List<Vector2[]>();
            for (int y = 0; y < gh - 1; y++)
            for (int x = 0; x < gw - 1; x++)
            {
                float tl = v[y * gw + x], tr = v[y * gw + x + 1];
                float br = v[(y + 1) * gw + x + 1], bl = v[(y + 1) * gw + x];
                int code = (tl > L ? 8 : 0) | (tr > L ? 4 : 0) | (br > L ? 2 : 0) | (bl > L ? 1 : 0);
                if (code == 0 || code == 15) continue;
                var T = new Vector2(x + Ip(tl, tr, L), y);
                var R = new Vector2(x + 1, y + Ip(tr, br, L));
                var B = new Vector2(x + Ip(bl, br, L), y + 1);
                var Lf = new Vector2(x, y + Ip(tl, bl, L));
                switch (code)
                {
                    case 1: segs.Add(new[] { Lf, B }); break;
                    case 2: segs.Add(new[] { B, R }); break;
                    case 3: segs.Add(new[] { Lf, R }); break;
                    case 4: segs.Add(new[] { T, R }); break;
                    case 5: segs.Add(new[] { T, Lf }); segs.Add(new[] { B, R }); break;
                    case 6: segs.Add(new[] { T, B }); break;
                    case 7: segs.Add(new[] { T, Lf }); break;
                    case 8: segs.Add(new[] { T, Lf }); break;
                    case 9: segs.Add(new[] { T, B }); break;
                    case 10: segs.Add(new[] { T, R }); segs.Add(new[] { Lf, B }); break;
                    case 11: segs.Add(new[] { T, R }); break;
                    case 12: segs.Add(new[] { Lf, R }); break;
                    case 13: segs.Add(new[] { B, R }); break;
                    case 14: segs.Add(new[] { Lf, B }); break;
                }
            }

            // 端点配对连成折线
            var map = new Dictionary<long, List<int>>();
            Func<Vector2, long> key = p => Mathf.RoundToInt(p.x * 100f) * 100000L + Mathf.RoundToInt(p.y * 100f);
            for (int i = 0; i < segs.Count; i++)
            {
                AddIdx(map, key(segs[i][0]), i);
                AddIdx(map, key(segs[i][1]), i);
            }
            var used = new bool[segs.Count];
            var polys = new List<List<Vector2>>();
            for (int i = 0; i < segs.Count; i++)
            {
                if (used[i]) continue;
                used[i] = true;
                var line = new List<Vector2> { segs[i][0], segs[i][1] };
                bool ext = true;
                while (ext)
                {
                    ext = false;
                    var cands = GetOr(map, key(line[line.Count - 1]));
                    if (cands != null)
                        foreach (int j in cands)
                        {
                            if (used[j]) continue;
                            used[j] = true;
                            var sg = segs[j];
                            line.Add(key(sg[0]) == key(line[line.Count - 1]) ? sg[1] : sg[0]);
                            ext = true;
                            break;
                        }
                    if (ext) continue;
                    cands = GetOr(map, key(line[0]));
                    if (cands != null)
                        foreach (int j in cands)
                        {
                            if (used[j]) continue;
                            used[j] = true;
                            var sg = segs[j];
                            line.Insert(0, key(sg[0]) == key(line[0]) ? sg[1] : sg[0]);
                            ext = true;
                            break;
                        }
                }
                if (line.Count > 1) polys.Add(line);
            }
            return polys;
        }

        private static float Ip(float a, float b, float l) => (l - a) / (b - a);

        private static void AddIdx(Dictionary<long, List<int>> map, long k, int i)
        {
            if (!map.TryGetValue(k, out var l)) { l = new List<int>(); map[k] = l; }
            l.Add(i);
        }

        private static List<int> GetOr(Dictionary<long, List<int>> d, long k)
            => d.TryGetValue(k, out var v) ? v : null;

        // Douglas-Peucker
        private static List<Vector2> Simplify(List<Vector2> pts, float eps)
        {
            if (pts.Count < 3) return pts;
            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            var stack = new Stack<int[]>();
            stack.Push(new[] { 0, pts.Count - 1 });
            float e2 = eps * eps;
            while (stack.Count > 0)
            {
                var se = stack.Pop();
                int a = se[0], b = se[1];
                if (b - a < 2) continue;
                Vector2 A = pts[a], B = pts[b];
                Vector2 d = B - A;
                float dd = d.sqrMagnitude;
                float mx = -1f; int mi = -1;
                for (int i = a + 1; i < b; i++)
                {
                    float t = dd > 0f ? Vector2.Dot(pts[i] - A, d) / dd : 0f;
                    t = Mathf.Clamp01(t);
                    var off = A + d * t - pts[i];
                    float dist = off.sqrMagnitude;
                    if (dist > mx) { mx = dist; mi = i; }
                }
                if (mx > e2 && mi > 0)
                {
                    keep[mi] = true;
                    stack.Push(new[] { a, mi });
                    stack.Push(new[] { mi, b });
                }
            }
            var o = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++) if (keep[i]) o.Add(pts[i]);
            return o;
        }
    }
}
