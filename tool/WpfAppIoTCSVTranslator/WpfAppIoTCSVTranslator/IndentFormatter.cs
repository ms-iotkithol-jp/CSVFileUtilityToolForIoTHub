using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WpfAppIoTCSVTranslator
{
    public class IndentFormatter
    {
        string indentUnit = "  ";
        string indent = "";
        int indentLevel = 0;

        StringBuilder sb = new StringBuilder();
        TextWriter writer = null;

        public IndentFormatter()
        {
            writer = new StringWriter(sb);
        }

        public void Inc() {
            indentLevel++;
            indent += indentUnit;
        }
        public void Dec() { 
            indentLevel--;
            indent = indent.Substring(indentUnit.Length);
        }

        public void WriteLine(string line, bool incrementAfter = false, bool declementBefore = false, bool withIndent=true)
        {
            if (declementBefore)
            {
                Dec();
            }
            if (withIndent)
            {
                writer.WriteLine($"{indent}{line}");
            }
            else
            {
                writer.WriteLine(line);
            }
            if (incrementAfter)
            {
                Inc();
            }
        }

        public void Write(string content)
        {
            writer.Write(content);
        }

        public override string ToString()
        {
            return sb.ToString();
        }
    }
}
