using System;
using System.Runtime.InteropServices;

public static class UtilitySerialization
{
    /// <summary> Convert byte array to structure </summary>
    public static T ByteArrayToStruct<T>(byte[] data) where T : struct
    {
        var sizeOfStruct = Marshal.SizeOf(typeof(T));

        T result = new T();
        IntPtr ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.AllocHGlobal(sizeOfStruct);
            Marshal.Copy(data, 0, ptr, data.Length);
            result = (T)Marshal.PtrToStructure(ptr, typeof(T));
        }
        finally
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        return result;
    }

    /// <summary> Convert structure to byte array </summary>
    public static byte[] StructToByteArray<T>(T structure) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        byte[] array = new byte[size];

        IntPtr ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(structure, ptr, true);
            Marshal.Copy(ptr, array, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
        return array;
    }
}