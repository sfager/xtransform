// XSLT 2.0 and XQuery 1.0 Serialization §6 — characters that the output encoding cannot represent are
// written as numeric character references (xml, xhtml and html methods).

using System.Globalization;
using System.Text;

namespace XTransform.Serialization;

/// <summary>An encoder fallback that writes <c>&amp;#N;</c> for a character the encoding cannot represent.</summary>
internal sealed class CharacterReferenceFallback : EncoderFallback
{
    // "&#1114111;" is the longest reference (10 characters); a surrogate pair is one code point.
    public override int MaxCharCount => 10;

    public override EncoderFallbackBuffer CreateFallbackBuffer() => new Buffer();

    private sealed class Buffer : EncoderFallbackBuffer
    {
        private string _pending = string.Empty;
        private int _position;

        public override int Remaining => _pending.Length - _position;

        public override bool Fallback(char charUnknown, int index)
        {
            // A lone surrogate half has no character reference.
            if (char.IsSurrogate(charUnknown))
            {
                throw new EncoderFallbackException("A lone surrogate cannot be written as a character reference.");
            }

            return Set(charUnknown);
        }

        public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index) =>
            Set(char.ConvertToUtf32(charUnknownHigh, charUnknownLow));

        public override char GetNextChar() => _position < _pending.Length ? _pending[_position++] : '\0';

        public override bool MovePrevious()
        {
            if (_position == 0)
            {
                return false;
            }

            _position--;
            return true;
        }

        public override void Reset()
        {
            _pending = string.Empty;
            _position = 0;
        }

        private bool Set(int codePoint)
        {
            _pending = "&#" + codePoint.ToString(CultureInfo.InvariantCulture) + ";";
            _position = 0;
            return true;
        }
    }
}
