using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Egui;

/// <summary>
/// A callback that may be passed to unmanaged code.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe partial struct EguiCallbackFn : IDisposable
{

    /// <summary>
    ///  The function to call.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, nint> func;
    public void* callbackHandle;


    [ThreadStatic]
    private static nint _returnAllocation = 0;

    /// <summary>
    /// The last exception that occurred.
    /// </summary>
    [ThreadStatic]
    private static ExceptionDispatchInfo? _lastException;


    private EguiCallbackFn(void* callbackHandle)
    {
        this.func = &InvokeCallbackWrapper;
        this.callbackHandle = callbackHandle;
    }

    // For unmanaged types, we don't have to allocate anything
    public static EguiCallbackFn Make<A, R>(Func<A, R> callback)
        where A : unmanaged
        where R : unmanaged
         => new((void*)(nint)GCHandle.Alloc(
                     (nint argPtr) => callback(*((A*)argPtr))
                 ));

    // We can pass strings as a CString, but we have to 
    // allocate them
    public static EguiCallbackFn Make<A>(Func<A, string> callback)
        where A : unmanaged
        => new((void*)(nint)GCHandle.Alloc((nint argPtr) =>
            {
                string ret = callback(*((A*)argPtr));
                _returnAllocation = Marshal.StringToCoTaskMemUTF8(ret);
                return _returnAllocation;
            }));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static nint InvokeCallbackWrapper(void* argument, void* data)
    {
        try
        {
            var func = (Func<nint, nint>)GCHandle.FromIntPtr((nint)data).Target!;
            return func((nint)argument);
        }
        catch (Exception e)
        {
            _lastException = ExceptionDispatchInfo.Capture(e);
            return 0;
        }
    }

    /// <inheritdoc/>
    void IDisposable.Dispose()
    {
        if (_returnAllocation > 0)
        {
            Marshal.FreeCoTaskMem(_returnAllocation);
            _returnAllocation = 0;
        }
        GCHandle.FromIntPtr((nint)callbackHandle).Free();

        if (_lastException is not null)
        {
            var last = _lastException;
            _lastException = null;
            last.Throw();
        }
    }

    /// <summary>
    /// Serializes an instance of this value.
    /// </summary>
    /// <param name="serializer">The serializer to use.</param>
    internal static void Serialize(BincodeSerializer serializer, EguiCallbackFn obj)
    {
        serializer.increase_container_depth();
        serializer.serialize_u64((ulong)obj.func);
        serializer.serialize_u64((ulong)obj.callbackHandle);
        serializer.decrease_container_depth();
    }

    /// <summary>
    /// Deserializes an instance of this value.
    /// </summary>
    /// <param name="deserializer">The deserializer to use.</param>
    /// <returns>The object that was deserialized.</returns>
    internal static EguiCallbackFn Deserialize(BincodeDeserializer deserializer)
    {
        deserializer.increase_container_depth();
        EguiCallbackFn obj = default;
        obj.func = (delegate* unmanaged[Cdecl]<void*, void*, nint>)deserializer.deserialize_u64();
        obj.callbackHandle = (void*)deserializer.deserialize_u64();
        deserializer.decrease_container_depth();
        return obj;
    }
}
