using System.Text;

namespace Silent.Core.Text
{
    public sealed class PieceTable : IBuffer
    {
        // Immutable original text
        private readonly string _Original;

        // Appends edits
        private readonly StringBuilder _Add = new();
        private readonly List<Piece> _Pieces = new();

        private readonly struct Piece
        {
            public readonly bool FromAdd;
            public readonly int Start;
            public readonly int Length;

            public Piece(bool fromAdd, int start, int length)
            {
                FromAdd = fromAdd;
                Start = start;
                Length = length;
            }
        }

        public event EventHandler? Changed;

        public PieceTable(string Initial = "")
        {
            _Original = Initial ?? string.Empty;
            if (_Original.Length > 0)
                _Pieces.Add(new Piece(false, 0, _Original.Length));
        }

        public int Length => _Pieces.Sum(piece => piece.Length);

        public string GetText(int start, int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            if (start + length > Length) 
                throw new ArgumentOutOfRangeException();

            var remaining = length;
            var pos = 0;
            var sb = new StringBuilder(length);

            foreach (var piece in _Pieces)
            {
                if (remaining == 0) break;
                if (pos + piece.Length <= start) { pos += piece.Length; continue; }

                var takeStart = Math.Max(0, start - pos);
                var takeLen = Math.Min(piece.Length - takeStart, remaining);
                sb.Append(Read(piece, takeStart, takeLen));
                remaining -= takeLen;
                pos += piece.Length;
            }
            return sb.ToString();
        }

        public void Insert(int position, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if ((uint)position > (uint)Length) throw new ArgumentOutOfRangeException(nameof(position));

            var addStart = _Add.Length;
            _Add.Append(text);
            var newPiece = new Piece(true, addStart, text.Length);

            // find piece index + offset
            var (index, offset) = FindPiece(position);
            if (index < 0) { _Pieces.Add(newPiece); return; }

            var cur = _Pieces[index];
            //Insert before current piece
            if (offset == 0) _Pieces.Insert(index, newPiece);
            else if (offset == cur.Length) _Pieces.Insert(index + 1, newPiece);
            else
            {
                var left = new Piece(cur.FromAdd, cur.Start, offset);
                var right = new Piece(cur.FromAdd, cur.Start + offset, cur.Length - offset);
                _Pieces[index] = left;
                _Pieces.Insert(index + 1, newPiece);
                _Pieces.Insert(index + 2, right);
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Delete(int start, int length)
        {
            if (length <= 0) return;
            if (start < 0 || start + length > Length) throw new ArgumentOutOfRangeException();

            var end = start + length;
            var pos = 0;
            for (int i = 0; i < _Pieces.Count && length > 0;)
            {
                var p = _Pieces[i];
                var pStart = pos;
                var pEnd = pos + p.Length;

                if (pEnd <= start) { pos = pEnd; i++; continue; }
                if (pStart >= end) break;

                var cutStart = Math.Max(start, pStart);
                var cutEnd = Math.Min(end, pEnd);
                var cutLen = cutEnd - cutStart;

                var leftLen = cutStart - pStart;
                var rightLen = pEnd - cutEnd;

                if (leftLen > 0 && rightLen > 0)
                {
                    // Split into left + right
                    var left = new Piece(p.FromAdd, p.Start, leftLen);
                    var right = new Piece(p.FromAdd, p.Start + p.Length - rightLen, rightLen);
                    _Pieces[i] = left;
                    _Pieces.Insert(i + 1, right);
                    // Skip left, now at right
                    i++;
                }
                else if (leftLen > 0)
                {
                    _Pieces[i] = new Piece(p.FromAdd, p.Start, leftLen);
                    i++;
                }
                else if (rightLen > 0)
                {
                    _Pieces[i] = new Piece(p.FromAdd, p.Start + p.Length - rightLen, rightLen);
                    i++;
                }
                else
                {
                    _Pieces.RemoveAt(i);
                }
                length -= cutLen;
                pos = cutEnd;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private (int index, int offset) FindPiece(int position)
        {
            if (_Pieces.Count == 0) return (-1, 0);
            var pos = 0;

            for (int i = 0; i < _Pieces.Count; i++)
            {
                var p = _Pieces[i];
                if (pos + p.Length >= position)
                    return (i, position - pos);
                pos += p.Length;
            }

            return (_Pieces.Count - 1, _Pieces[^1].Length);
        }

        private ReadOnlySpan<char> Read(Piece p, int start, int length)
        {
            if (length == 0) return ReadOnlySpan<char>.Empty;
            if (p.FromAdd)
                return _Add.ToString(p.Start + start, length);

            return _Original.AsSpan(p.Start + start, length);
        }
    }
}
