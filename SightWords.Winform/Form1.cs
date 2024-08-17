using NInk;
using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace SightWords.Winform;

public partial class Form1 : Form
{
    static readonly string[] SightWords = [
        "mloaas",
        "Jamie",
        "Alan",
        "Mama",
        "Daddy",
        "teacher",
        "to",
        "listen",
        "me"
    ];

    private NInk.StrokeModeler? _strokeModeler;
    private FontFamily? _fontFamily;
    private readonly List<PointF> _points = new List<PointF>(100);
    private readonly List<PointF[]> _curves = [];
    private bool _isDown;
    private DateTime _downTime;
    private readonly float _tension = 0.01f;
    private bool _fill;

    public Form1()
    {
        InitializeComponent();
    }

    internal FontFamily FontFamily => _fontFamily ?? FontFamily.GenericSansSerif;

    private void Form1_Paint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var word = SightWords[0];
        var fontSize = Math.Min(Width, Height) * 0.3f;
        var center = new Point(Width / 2, Height / 2);
        var dpiScale = g.DpiY / 72;
        var emSize = dpiScale * fontSize;
        var fontFamily = FontFamily;
        var stroke = Math.Max(fontSize * 0.05f, 8);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        using var curveBrush = new SolidBrush(Color.Pink);
        using var curvePen = new Pen(curveBrush, stroke)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        if (_fill)
        {
            using var font = new Font(fontFamily, fontSize);
            g.DrawString(
                word,
                font,
                curveBrush,
                center,
                format
            );
        }

        var outlinePath = new GraphicsPath();
        outlinePath.AddString(word,
            fontFamily,
            (int)FontStyle.Regular,      // font style (bold, italic, etc.)
            emSize,
            center,
            format
        );

        using var outlineBrush = new SolidBrush(Color.DarkGray);
        using var outlinePen = new Pen(outlineBrush, 4)
        {
            LineJoin= LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap= LineCap.Round
        };
        outlinePath.Widen(outlinePen);

        g.FillPath(outlineBrush, outlinePath);

        foreach (var curve in _curves)
        {
            DrawCurve(g, curvePen, curve, stroke);
        }

        using var currentBrush = new SolidBrush(Color.Red);
        using var currentPen = new Pen(currentBrush, stroke)
        {
            LineJoin= LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        DrawCurve(g, currentPen, [.. _points], stroke);
    }

    void DrawCurve(Graphics g, Pen pen, PointF[] points, float strokeWidth)
    {
        var r = strokeWidth * 0.5f;

        if (points.Length < 3)
        {
            for (int i = 0; i < points.Length; i++)
            {
                g.FillEllipse(pen.Brush, points[i].X - r, points[i].Y - r, strokeWidth, strokeWidth);
            }
        }
        else
        {
            g.DrawCurve(pen, points, _tension);
        }
    }

    private void Form1_Resize(object sender, EventArgs e)
    {
        _curves.Clear();
        _points.Clear();
        Invalidate();
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        /*
         *     .wobble_smoother_params{
        .timeout{.04}, .speed_floor = 1.31, .speed_ceiling = 1.44},
        .position_modeler_params{.spring_mass_constant = 11.f / 32400,
                                 .drag_constant = 72.f},
        .sampling_params{.min_output_rate = 180,
                         .end_of_stroke_stopping_distance = .001,
                         .end_of_stroke_max_iterations = 20},
        .stylus_state_modeler_params{.max_input_samples = 20}
        */
        /*
        _strokeModeler = new StrokeModeler(
            new WobbleSmootherParams() { timeout = 0.04, speed_ceiling = 1.44f, speed_floor = 1.31f },
            new PositionModelerParams() { spring_mass_constant = 11f / 32400, drag_constant = 72.0f },
            new SamplingParams() { min_output_rate = 180, end_of_stroke_stopping_distance = 0.001f, end_of_stroke_max_iterations = 20 },
            new StylusStateModelerParams() { max_input_samples = 20 }
        ); */
        _fontFamily = FontFamily.Families.FirstOrDefault(p => p.GetName(0).Equals("VIC MODERN  CURSIVE"));
        if (!Debugger.IsAttached)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private unsafe void Form1_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_strokeModeler is { } modeler && modeler.Reset())
            {
                var vec = new Vec2 { X = e.X, Y = e.Y };
                using (var resultsHandle = modeler.Update(EventType.Down, vec, TimeSpan.Zero, 1, out var results, out var resultsCount))
                {
                    _points.EnsureCapacity(resultsCount);

                    for (var i = 0; i < resultsCount; i++)
                    {
                        _points.Add(new PointF(results->position.X, results->position.Y));
                        results++;
                    }
                }
            }
            else
            {
                _points.Add(new PointF(e.X, e.Y));
            }
            _downTime = DateTime.UtcNow;
            _isDown = true;
            Invalidate();
        }
    }

    private unsafe void Form1_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_strokeModeler is { } modeler)
            {
                var vec = new Vec2 { X = e.X, Y = e.Y };
                using (var resultsHandle = modeler.Update(EventType.Up, vec, DateTime.UtcNow - _downTime, 1, out var results, out var resultsCount))
                {
                    _points.EnsureCapacity(resultsCount);

                    for (var i = 0; i < resultsCount; i++)
                    {
                        _points.Add(new PointF(results->position.X, results->position.Y));
                        results++;
                    }
                }
            }

            _curves.Add([.. _points]);
            _points.Clear();
            _isDown = false;
            Invalidate();
        }
    }

    private unsafe void Form1_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDown)
        {
            if (_strokeModeler is { } modeler)
            {
                var vec = new Vec2 { X = e.X, Y = e.Y };
                using (var resultsHandle = modeler.Update(EventType.Move, vec, DateTime.UtcNow - _downTime, 1, out var results, out var resultsCount))
                {
                    _points.EnsureCapacity(resultsCount);

                    for (var i = 0; i < resultsCount; i++)
                    {
                        _points.Add(new PointF(results->position.X, results->position.Y));
                        results++;
                    }
                }
            }
            else
            {
                // var last = _points[^1];
                var @new = new PointF(e.X, e.Y);
                // var dist = MathF.Sqrt(MathF.Pow(@new.X - last.X, 2) + MathF.Pow(@new.Y - last.Y, 2));
                _points.Add(@new);
            }
            Invalidate();
        }
    }

    private void Form1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    private void Form1_FormClosed(object sender, FormClosedEventArgs e)
    {
        _strokeModeler?.Dispose();
        _strokeModeler = null;
    }
}
