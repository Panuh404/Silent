using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silent.Core.Text;

namespace Silent.Core.IO
{
    public static class FileService
    {
        public static Document Open(string path, Encoding? enc = null)
        {
            enc ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var text = File.ReadAllText(path, enc);
            var doc = new Document(path, text);
            doc.MarkSaved(path);
            return doc;
        }

        public static void Save(Document doc, string? path = null, Encoding? enc = null)
        {
            enc ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var target = path ?? doc.FilePath ?? throw new InvalidOperationException("No target path.");
            var text = doc.Buffer.GetText(0, doc.Buffer.Length);
            File.WriteAllText(target, text, enc);
            doc.MarkSaved(target);
        }
    }
}
