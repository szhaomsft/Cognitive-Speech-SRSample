// This file is modified from Mischel's answer in stackoverflow
// https://stackoverflow.com/questions/22047900/ienumerable-to-stream

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace UnifiedSpeechServicesSTT
{
    // This class is safe for 1 producer and 1 consumer.
    public class ProducerConsumerStream : Stream
    {
        private readonly byte[] _circleBuff;
        private int _head;
        private int _tail;

        public bool IsAddingCompleted { get; private set; }
        public bool IsCompleted { get; private set; }

        // For debugging
        private long _totalBytesRead = 0;
        private long _totalBytesWritten = 0;

        public ProducerConsumerStream(int size)
        {
            _circleBuff = new byte[size];
            _head = 1;
            _tail = 0;
        }

        public void Reset()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("The stream has been disposed.");
            }
            lock (_circleBuff)
            {
                _head = 1;
                _tail = 0;
                IsAddingCompleted = false;
                IsCompleted = false;
            }
        }

        [Conditional("STT_DEBUG")]
        private static void DebugOut(string msg)
        {
            Console.WriteLine(msg);
        }

        [Conditional("STT_DEBUG")]
        private static void DebugOut(string fmt, params object[] parms)
        {
            DebugOut(string.Format(fmt, parms));
        }

        private int ReadBytesAvailable
        {
            get
            {
                if (_head > _tail)
                    return _head - _tail - 1;
                return _circleBuff.Length - _tail + _head - 1;
            }
        }

        private int WriteBytesAvailable => _circleBuff.Length - ReadBytesAvailable - 1;

        private void IncrementTail()
        {
            _tail = (_tail + 1) % _circleBuff.Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("The stream has been disposed.");
            }
            if (IsCompleted)
            {
                throw new EndOfStreamException("The stream is empty and has been marked complete for adding.");
            }
            if (count == 0)
            {
                return 0;
            }

            lock (_circleBuff)
            {
                DebugOut("Read: requested {0:N0} bytes. Available = {1:N0}.", count, ReadBytesAvailable);
                while (ReadBytesAvailable == 0)
                {
                    if (IsAddingCompleted)
                    {
                        IsCompleted = true;
                        return 0;
                    }
                    Monitor.Wait(_circleBuff);
                }

                // If Head < Tail, then there are bytes available at the end of the buffer
                // and also at the front of the buffer.
                // If reading from Tail to the end doesn't fulfill the request,
                // and there are still bytes available,
                // then read from the start of the buffer.
                DebugOut("Read: Head={0}, Tail={1}, Avail={2}", _head, _tail, ReadBytesAvailable);

                IncrementTail();
                int bytesToRead;
                if (_tail > _head)
                {
                    // When Tail > Head, we know that there are at least
                    // (CircleBuff.Length - Tail) bytes available in the buffer.
                    bytesToRead = _circleBuff.Length - _tail;
                }
                else
                {
                    bytesToRead = _head - _tail;
                }

                // Don't read more than count bytes!
                bytesToRead = Math.Min(bytesToRead, count);

                Buffer.BlockCopy(_circleBuff, _tail, buffer, offset, bytesToRead);
                _tail += (bytesToRead - 1);
                var bytesRead = bytesToRead;

                // At this point, either we've exhausted the buffer,
                // or Tail is at the end of the buffer and has to wrap around.
                if (bytesRead < count && ReadBytesAvailable > 0)
                {
                    // We haven't fulfilled the read.
                    IncrementTail();
                    // Tail is always equal to 0 here.
                    bytesToRead = Math.Min((count - bytesRead), (_head - _tail));
                    Buffer.BlockCopy(_circleBuff, _tail, buffer, offset + bytesRead, bytesToRead);
                    bytesRead += bytesToRead;
                    _tail += (bytesToRead - 1);
                }

                _totalBytesRead += bytesRead;
                DebugOut("Read: returning {0:N0} bytes. TotalRead={1:N0}", bytesRead, _totalBytesRead);
                DebugOut("Read: Head={0}, Tail={1}, Avail={2}", _head, _tail, ReadBytesAvailable);

                Monitor.Pulse(_circleBuff);
                return bytesRead;
            }
        }

        public void Clear()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("The stream has been disposed.");
            }
            else
            {
                lock (_circleBuff)
                {
                    _head = 1;
                    _tail = 0;
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("The stream has been disposed.");
            }
            if (IsAddingCompleted)
            {
                throw new InvalidOperationException("The stream has been marked as complete for adding.");
            }
            lock (_circleBuff)
            {
                DebugOut("Write: requested {0:N0} bytes. Available = {1:N0}", count, WriteBytesAvailable);
                int bytesWritten = 0;
                while (bytesWritten < count)
                {
                    while (WriteBytesAvailable == 0)
                    {
                        Monitor.Wait(_circleBuff);
                    }
                    DebugOut("Write: Head={0}, Tail={1}, Avail={2}", _head, _tail, WriteBytesAvailable);
                    int bytesToCopy = Math.Min((count - bytesWritten), WriteBytesAvailable);
                    CopyBytes(buffer, offset + bytesWritten, bytesToCopy);
                    _totalBytesWritten += bytesToCopy;
                    DebugOut("Write: {0} bytes written. TotalWritten={1:N0}", bytesToCopy, _totalBytesWritten);
                    DebugOut("Write: Head={0}, Tail={1}, Avail={2}", _head, _tail, WriteBytesAvailable);
                    bytesWritten += bytesToCopy;
                    Monitor.Pulse(_circleBuff);
                }
            }
        }


        private void CopyBytes(byte[] buffer, int srcOffset, int count)
        {
            // Insert at head
            // The copy might require two separate operations.

            // copy as much as can fit between Head and end of the circular buffer
            int offset = srcOffset;
            int bytesCopied = 0;
            int bytesToCopy = Math.Min(_circleBuff.Length - _head, count);
            if (bytesToCopy > 0)
            {
                Buffer.BlockCopy(buffer, offset, _circleBuff, _head, bytesToCopy);
                bytesCopied = bytesToCopy;
                _head = (_head + bytesToCopy) % _circleBuff.Length;
                offset += bytesCopied;
            }

            // Copy the remainder, which will go from the beginning of the buffer.
            if (bytesCopied >= count) return;
            bytesToCopy = count - bytesCopied;
            Buffer.BlockCopy(buffer, offset, _circleBuff, _head, bytesToCopy);
            _head = (_head + bytesToCopy) % _circleBuff.Length;
        }

        public void CompleteAdding()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("The stream has been disposed.");
            }
            lock (_circleBuff)
            {
                DebugOut("CompleteAdding: {0:N0} bytes written.", _totalBytesWritten);
                IsAddingCompleted = true;
                Monitor.Pulse(_circleBuff);
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override void Flush() { /* does nothing */ }

        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        private bool _disposed = false;

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            base.Dispose(disposing);
            _disposed = true;
        }
    }
}
