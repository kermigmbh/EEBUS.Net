namespace EEBUS.Enums
{
    public class SHIPMessageType
    {
        public const byte INIT    = 0;
        public const byte CONTROL = 1;
        public const byte DATA    = 2;
        public const byte END     = 3;
    }

    public class SHIPMessageTimeout
    {
        public const int CMI_TIMEOUT                 = 30 * 1000;   // maximum allowed are 30 seconds, according to spec
        public const int T_HELLO_INIT                = 240 * 1000;  // maximum alowed are 240 seconds, according to the spec
        public const int T_HELLO_INC                 = T_HELLO_INIT;
        public const int T_HELLO_PROLONG_THR_INC     = 30 * 1000;   // maximum allowed are 30 seconds, according to spec
        public const int T_HELLO_PROLONG_WAITING_GAP = 15 * 1000;   // maximum allowed are 15 seconds, according to spec
        public const int T_HELLO_PROLONG_MIN         = 1000;        // maximum allowed is 1 second, according to spec
    }

    public class SHIPMessageValue
    {
        public const byte CMI_HEAD = 0;
    }

    public class SHIPMessageFormat
    {
        public const string JSON_UTF8 = "JSON-UTF8";
    }

    /// <summary>
    /// Maximum SHIP specification version implemented by this stack. Per spec 13.4.4.2.2 every
    /// version from 1.0 up to this one must be supported, so the supported range is [1.0, MAX].
    /// </summary>
    public class SHIPVersion
    {
        /// <summary>
        /// Minimum SHIP specification major version implemented by this stack (1, per spec)
        /// </summary>
        public const ushort MIN_MAJOR = 1;
        /// <summary>
        /// Minimum SHIP specification minor version implemented by this stack (0, per spec)
        /// </summary>
        public const ushort MIN_MINOR = 0;
        /// <summary>
        /// Maximum SHIP specification major version implemented by this stack
        /// </summary>
        public const ushort MAX_MAJOR = 1;
        /// <summary>
        /// Maximum SHIP specification minor version implemented by this stack
        /// </summary>
        public const ushort MAX_MINOR = 0;
    }

    public class SHIPHandshakeError
    {
        public const byte RFU                = 0;
        public const byte TIMEOUT            = 1;
        public const byte UNEXPECTED_MESSAGE = 2;
        public const byte SELECTION_MISMATCH = 3;
    }
}