using System;

namespace HoytSys.DataStructures
{
    /// <summary>
    ///     A simple bit store used for compacting values.
    /// </summary>
    public class BitStore
    {
        private const int shift = 6;
        private const ulong posMask = 63;
        private ulong[] values;
        private readonly ulong mask;
        private readonly ulong bits;
        private readonly int length;
        private readonly int size;

        public BitStore(
            int size,
            int bits)
        {
            this.mask = (ulong) (1 << bits) - 1;
            this.bits = (ulong) bits;
            this.length = (int) Math.Ceiling((bits * size) / 64.0);
            this.values = new ulong[this.length];
            this.size = size;
        }

        public ulong BinarySearch(ulong value, ulong increments)
        {
            // Divided by the increments the total length.  Don't allow to overflow!
            var searchLength = (ulong) this.size / increments;
            var start = 0ul;
            var middle = searchLength / 2;
            var end = searchLength;
            var pos = increments * middle;
            while (true)
            {
                var valueAtPos = Read(pos);
                if (value > valueAtPos)
                {
                    start = pos + 1;
                    pos = CalculateHigh(start, end, increments);
                    if (pos == end)
                    {
                        break;
                    }
                } 
                else if (value < valueAtPos)
                {
                    end = pos - 1;
                    pos = CalculateLow(start, end, increments);
                    if (pos == start)
                    {
                        break;
                    }
                }
                else
                {
                    // Value is equal
                    end = pos;
                    pos = CalculateLow(start, end, increments);
                    if (pos == end)
                    {
                        break;
                    }
                }
            }

            var matchedValue = Read(pos);
            if (matchedValue == value)
            {
                return pos;
            }
            else
            {
                return UInt64.MaxValue;
            }
        }

        private ulong CalculateLow(ulong start, ulong end, ulong increments)
        {
            var range = end - start;
            var middle = range / 2;
            return middle * increments + start;
        }

        private ulong CalculateHigh(ulong start, ulong end, ulong increments)
        {
            var range = end - start;
            var middle = range / 2;
            return middle * increments + start;
        }

        /// <summary>
        ///     Read the specific position in the block.
        /// </summary>
        /// <param name="pos">The position to read in.</param>
        /// <returns>The unit value at that position.</returns>
        public ulong Read(ulong pos)
        {
            var (start, reminder) = Start(pos);
            
            // All we need to do here is shift the mask to the correct position.
            var valueMask = this.mask << reminder;
            // Now we take the mask of the array.
            var value = this.values[start];
            // Get the bits at that position.
            value &= valueMask;
            // Shift it to the start.
            value = value >> reminder;
            
            var (end, shiftBits) = End(start, reminder);
            ulong shiftValue = (ulong) (end - start);
            valueMask = (shiftValue << shiftBits) - shiftValue;
            var endValue = this.values[end];
            endValue &= valueMask;
            var fix = ((int) this.bits - shiftBits) * (end - start);
            endValue = endValue << fix;
            value |= endValue;
            return value;
        }

        public void Write(ulong pos, ulong value)
        {
            var (start, reminder) = Start(pos);
            
            // Need the mask for the position we want to write to.
            var newMask = this.mask << reminder;
            // Zero out that position with an XOR.
            var zeroPosition = UInt64.MaxValue ^ newMask;
            var valueAtPos = this.values[start];
            // Shift the value to that position.
            var longValue = value << reminder;
            // Zero out the position.
            valueAtPos &= zeroPosition;
            // Added our new value.
            valueAtPos |= longValue;
            this.values[start] = valueAtPos;
            
            // We will go through similar logic.
            longValue = value;
            var (end, shiftBits) = End(start, reminder);
            var shiftValue = (ulong) (end - start);
            var fix = ((int) this.bits - shiftBits);
            // Get the end position. zero out the value if start matches end.
            newMask = ((shiftValue << shiftBits) - shiftValue) * shiftValue;
            zeroPosition = UInt64.MaxValue ^ newMask;
            valueAtPos = this.values[end];
            // If we are at the start/end zero out the value.
            longValue = (longValue >> fix) * shiftValue;
            valueAtPos &= zeroPosition;
            valueAtPos |= longValue;
            this.values[end] = valueAtPos;
        }

        /// <summary>
        ///     Used to calculate the start index in the array.
        /// </summary>
        /// <param name="pos">The to get in the bit store.</param>
        /// <returns>The starting index of the value in the bit vector.</returns>
        private (int, int) Start(ulong pos)
        {
            var posInBits = pos * bits;
            return ((int) (posInBits >> shift)
                ,(int) (posInBits & posMask));
        }

        /// <summary>
        ///     Calculates the ending position in the array.
        /// </summary>
        /// <returns>The ending position in the array.</returns>
        private (int, int) End(int start, int reminder)
        {
            var bitsU = (int) this.bits;
            var stopPosition = reminder + bitsU;
            var endReminder = stopPosition & 63;
            return (start + (stopPosition >> 6), endReminder);
        }
    }
}