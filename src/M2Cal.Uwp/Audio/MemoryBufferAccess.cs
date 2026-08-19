using System;
using System.Runtime.InteropServices;

namespace M2Cal.Uwp.Audio
{
    /// <summary>
    /// Dostęp do surowego bufora ramki audio. AudioGraph oddaje próbki wyłącznie przez tę
    /// niezarządzaną ścieżkę — stąd <c>AllowUnsafeBlocks</c> w projekcie.
    /// </summary>
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
