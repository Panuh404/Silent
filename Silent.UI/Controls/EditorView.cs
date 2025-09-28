using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Silent.Core.Text;
using System;
using System.Collections.Generic;

namespace Silent.UI.Controls
{
    public partial class EditorView : Control
    {
        private const double Padding = 2;
        private readonly Typeface _typeFace = new("Inter");
        private readonly double _fontSize = 14;

        private Document? _document;
        private IBuffer _buffer = new PieceTable("");
        private int _caret;
        private bool _caretVisible = true;
        private double _desiredCaretX = -1;

        private readonly DispatcherTimer _blinkTimer;

        private TextLayout? _layout;
        private string _cachedText = string.Empty;
        private double _cachedWidth = -1;

        private bool _showLineNumbers = true;
        private double _gutterWidth = 0;

        private readonly Brush _gutterBackground = new SolidColorBrush(Color.Parse("#1b1b1b"));
        private readonly Brush _gutterForeground = new SolidColorBrush(Color.Parse("#A9A9A9"));
        private readonly Pen _gutterSeparator = new(new SolidColorBrush(Color.Parse("#2a2a2a")), 1);
        
        private void ResetDesiredX() => _desiredCaretX = -1;

        public EditorView()
        {
            Focusable = true;
            _buffer = new PieceTable("");

            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
            _blinkTimer.Tick += (_, _) => { _caretVisible = !_caretVisible; InvalidateVisual(); };
            _blinkTimer.Start();

            AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: true);
            AddHandler(TextInputEvent, OnTextInput, handledEventsToo: true);
        }

        public Document? Document
        {
            get => _document;
            set
            {
                if (_document == value) return;

                _document = value;
                _buffer = value?.Buffer ?? new PieceTable("");

                // Layout refresh
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Focus();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _blinkTimer.Stop();
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
            _caretVisible = true;
            _blinkTimer.Start();
            InvalidateVisual();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            _blinkTimer.Stop();
            _caretVisible = false;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var text = _buffer.GetText(0, _buffer.Length);
            
            // Gutter
            var lineStarts = ComputeLineStarts(text);
            var totalLines = Math.Max(1, lineStarts.Count);
            var digits = Math.Max(2, (int)Math.Floor(Math.Log10(totalLines)) + 1);
            _gutterWidth = _showLineNumbers ? MeasureGutterWidth(digits) : 0;

            var maxWidth = Math.Max(0, Bounds.Width - Padding * 2 - _gutterWidth);

            // Background
            if (_layout is null || _cachedText != text || Math.Abs(_cachedWidth - maxWidth) > 0.5)
            {
                _layout = new TextLayout(
                    text,
                    _typeFace,
                    _fontSize,
                    foreground: Brushes.Gainsboro,
                    textWrapping: TextWrapping.Wrap,
                    maxWidth: maxWidth
                );
                _cachedText = text;
                _cachedWidth = maxWidth;
            }

            // Draw gutter background and separator
            if (_showLineNumbers)
            {
                var gutterRect = new Rect(0, 0, _gutterWidth, Bounds.Height);
                context.FillRectangle(_gutterBackground, gutterRect);
                
                // Separator line (slightly inside the gutter)
                var sepX = _gutterWidth - 0.5;
                context.DrawLine(_gutterSeparator, new Point(sepX, 0), new Point(sepX, Bounds.Height));
            }

            // Draw line numbers
            if (_showLineNumbers && _layout is not null)
            {
                for (int i = 0; i < totalLines; i++)
                {
                    int lineStartIndex = lineStarts[i];
                    // Y position of the first visual line for this logical line
                    var rect = _layout.HitTestTextPosition(Math.Clamp(lineStartIndex, 0, _buffer.Length));
                    var y = Padding + rect.Y;

                    var s = (i + 1).ToString();
                    var numLayout = new TextLayout(s, _typeFace, _fontSize, foreground: _gutterForeground);

                    // Right-align within gutter
                    var nx = _gutterWidth - Padding - numLayout.Width;
                    numLayout.Draw(context, new Point(nx, y));
                }
            }

            // Draw text
            var origin = new Point(Padding + 8 + _gutterWidth, Padding);
            _layout.Draw(context, origin);

            // Map caret index to(x, y) and height
            var caretIndex = Math.Clamp(_caret, 0, _buffer.Length);
            var caretRect = _layout.HitTestTextPosition(caretIndex);
            var caretH = caretRect.Height > 0 ? caretRect.Height : _fontSize * 1.2;

            if (_caretVisible && IsFocused)
            {
                var pen = new Pen(Brushes.White, 1);
                var x = origin.X + caretRect.X;
                var y = origin.Y + caretRect.Y;
                context.DrawLine(pen, new Point(x, y), new Point(x, y + caretH));
            }
        }
        public bool ShowLineNumbers
        {
            get => _showLineNumbers;
            set { if (_showLineNumbers != value) { _showLineNumbers = value; InvalidateVisual(); } }
        }
        private void InvalidateLayout()
        {
            _layout = null;
            InvalidateVisual();
        }

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;
            // Filter control chars except newline
            if (e.Text == "\r") return; // ignore CR
            if (e.Text == "\n") { Insert("\n"); return; }
            Insert(e.Text);
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private int NextWordBoundary(int pos, bool IncludeTrailingWhitespace = false)
        {
            var len = _buffer.Length;
            if (pos >= len) return len;

            var text = _buffer.GetText(0, len);
            int i = pos;
            
            // If starting on whitespace, first skip it
            if (i < len && char.IsWhiteSpace(text[i]))
            {
                while (i < len && char.IsWhiteSpace(text[i])) i++;
                if (!IncludeTrailingWhitespace) return i;
            }

            // If on a word run, skip it
            if (i < len && IsWordChar(text[i]))
            {
                while (i < len && IsWordChar(text[i])) i++;
            }
            else
            {
                // Otherwise skip a non-word, non-space run (punctuation etc.)
                while (i < len && !IsWordChar(text[i]) && !char.IsWhiteSpace(text[i])) i++;
            }

            if (IncludeTrailingWhitespace)
                while (i < len && char.IsWhiteSpace(text[i])) i++;

            return i;
        }
        
        private int PrevWordBoundary(int pos)
        {
            if (pos <= 0) return 0;

            var len = _buffer.Length;
            var text = _buffer.GetText(0, len);
            int i = Math.Min(pos, len) - 1;

            // Skip whitespace leftwards
            while (i >= 0 && char.IsWhiteSpace(text[i])) i--;
            if (i < 0) return 0;

            // If now on word, go to start of that word
            if (IsWordChar(text[i]))
            {
                while (i >= 0 && IsWordChar(text[i])) i--;
                return i + 1;
            }

            // Else skip a non-word, non-space run (punctuation etc.)
            while (i >= 0 && !IsWordChar(text[i]) && !char.IsWhiteSpace(text[i])) i--;
            return i + 1;
        }

        private void Insert(string s)
        {
            _buffer.Insert(_caret, s);
            _caret += s.Length;
            ResetDesiredX();
            InvalidateLayout();
        }

        private void MoveCaretVertical(int direction, TextLayout layout, Point origin)
        {
            // direction: -1 = Up, +1 = Down
            var caretRect = layout.HitTestTextPosition(Math.Clamp(_caret, 0, _buffer.Length));
            // remember desired X on first vertical move
            if (_desiredCaretX < 0) _desiredCaretX = caretRect.X;

            var lineHeight = caretRect.Height > 0 ? caretRect.Height : _fontSize * 1.2;
            var targetY = caretRect.Y + (direction * lineHeight);

            // Convert desired x/y to text index
            var hit = layout.HitTestPoint(new Point(_desiredCaretX, targetY));
            // Snap inside text range
            _caret = Math.Clamp(hit.TextPosition, 0, _buffer.Length);
            InvalidateVisual();
        }

        private static List<int> ComputeLineStarts(ReadOnlySpan<char> text)
        {
            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') starts.Add(i + 1);
            }
            return starts;
        }

        private double MeasureGutterWidth(int digits)
        {
            // Measure a "worst-case" width by using all 8's (widest digit in most fonts)
            var sample = new string('8', digits);
            var tl = new TextLayout(
                sample,
                _typeFace,
                _fontSize,
                foreground: _gutterForeground,
                textWrapping: TextWrapping.NoWrap
            );
            
            // Padding: left + right + a little spacing before the separator
            return Math.Ceiling(tl.Width) + Padding * 3;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Back:
                    if (_caret > 0)
                    {
                        _buffer.Delete(_caret - 1, 1);
                        _caret--;
                        InvalidateLayout();
                    }
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (_caret < _buffer.Length)
                    {
                        _buffer.Delete(_caret, 1);
                        InvalidateLayout();
                    }
                    e.Handled = true;
                    break;
                case Key.Left:
                    _caret = Math.Max(0, _caret - 1);
                    ResetDesiredX();
                    _caretVisible = true;
                    InvalidateVisual();
                    e.Handled = true;
                    break;
                case Key.Right:
                    _caret = Math.Min(_buffer.Length, _caret + 1);
                    ResetDesiredX();
                    _caretVisible = true;
                    InvalidateVisual();
                    e.Handled = true;
                    break;
                case Key.Up:
                {
                    var text = _buffer.GetText(0, _buffer.Length);
                    var layout = new TextLayout(text, _typeFace, _fontSize,
                        foreground: Brushes.Gainsboro,
                        textWrapping: TextWrapping.Wrap,
                        maxWidth: Math.Max(0, Bounds.Width - Padding * 2));
                    MoveCaretVertical(-1, layout, new Point(4, 4));
                    e.Handled = true;
                    break;
                }
                case Key.Down:
                {
                    var text = _buffer.GetText(0, _buffer.Length);
                    var layout = new TextLayout(text, _typeFace, _fontSize,
                        foreground: Brushes.Gainsboro,
                        textWrapping: TextWrapping.Wrap,
                        maxWidth: Math.Max(0, Bounds.Width - Padding * 2));
                    MoveCaretVertical(+1, layout, new Point(4, 4));
                    e.Handled = true;
                    break;
                }
                case Key.Home:
                    _caret = 0;
                    ResetDesiredX();
                    _caretVisible = true;
                    InvalidateVisual();
                    e.Handled = true;
                    break;
                case Key.End:
                    _caret = _buffer.Length;
                    ResetDesiredX();
                    _caretVisible = true;
                    InvalidateVisual();
                    e.Handled = true;
                    break;
                case Key.Enter:
                    Insert("\n");
                    e.Handled = true;
                    break;
            }

            // Ctrl-modified navigation
            if ((e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        _caret = PrevWordBoundary(_caret);
                        ResetDesiredX();
                        _caretVisible = true;
                        InvalidateVisual();
                        e.Handled = true;
                        return;
                    case Key.Right:
                        _caret = NextWordBoundary(_caret);
                        ResetDesiredX();
                        _caretVisible = true;
                        InvalidateVisual();
                        e.Handled = true;
                        return;
                    case Key.Delete:
                    {
                        int end = NextWordBoundary(_caret, IncludeTrailingWhitespace: true);
                        if (end > _caret)
                        {
                            _buffer.Delete(_caret, end - _caret);
                            _caretVisible = true;
                            InvalidateVisual();
                        }
                        e.Handled = true;
                        return;
                    }
                    case Key.Back:
                        int start = PrevWordBoundary(_caret);
                        if (start < _caret)
                        {
                            _buffer.Delete(start, _caret - start);
                            _caret = start;
                            _caretVisible = true;
                            InvalidateVisual();
                        }
                        e.Handled = true;
                        return;
                }
            }
        }
    }
}
