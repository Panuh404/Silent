using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Silent.Core.Text
{
    public sealed class Document
    {
        public string? FilePath { get; private set; }
        public IBuffer Buffer { get; }
        public bool IsDirty { get; private set; }

        public event EventHandler? DirtyChanged;

        public Document(string? filePath, string initialText = "")
        {
            FilePath = filePath;
            Buffer = new PieceTable(initialText);
            Buffer.Changed += (_, __) => SetDirty(true);
            IsDirty = false;
        }

        public void MarkSaved(string? newPath = null)
        {
            if (!string.IsNullOrEmpty(newPath))
                FilePath = newPath;
            SetDirty(false);
        }

        private void SetDirty(bool value)
        {
            if (IsDirty == value) return;
            IsDirty = value;
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
