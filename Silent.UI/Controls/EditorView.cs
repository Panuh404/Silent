using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Silent.Core.Text;
using System;

namespace Silent.UI.Controls
{
    public partial class EditorView : Control
    {
        private const double Padding = 2;
        private readonly Typeface _typeFace = new("Inter");
        private readonly double _fontSize = 14;

        private readonly IBuffer _buffer;
        private int _caret;
        private bool _caretVisible = true;
        private double _desiredCaretX = -1;

        private readonly DispatcherTimer _blinkTimer;

        private TextLayout? _layout;
        private string _cachedText = string.Empty;
        private double _cachedWidth = -1;

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
            var maxWidth = Math.Max(0, Bounds.Width - Padding * 2);

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

            // Draw text
            var origin = new Point(Padding, Padding);
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
        }
    }
}
