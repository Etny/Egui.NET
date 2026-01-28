using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Egui;


unsafe static class EguiCallbackFnFunc
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static nint InvokeCallbackWrapper(void* argument, void* data)
    {
        var func = (Func<nint, nint>)GCHandle.FromIntPtr((nint)data).Target!;
        return func((nint)argument);
    }
}

/// <summary>
/// A callback that may be passed to unmanaged code.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe partial struct EguiCallbackFn<A, R> : IDisposable
    where A : unmanaged
{

    /// <summary>
    ///  The function to call.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, nint> func;
    public void* callbackHandle;

    /// <summary>
    /// The last exception that occurred.
    /// </summary>
    [ThreadStatic]
    private static ExceptionDispatchInfo? _lastException;

    /// <summary>
    /// Creates a new object for invoking the given callback.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    public EguiCallbackFn(Func<A, R> callback)
    {
        func = &EguiCallbackFnFunc.InvokeCallbackWrapper;
        callbackHandle = (void*)(nint)GCHandle.Alloc((nint a) =>
        {
            try
            {
                R ret = callback(*((A*)a));
                if (ret is string s)
                    return Marshal.StringToCoTaskMemUTF8(s);

                nint alloc = Marshal.AllocCoTaskMem(sizeof(R));
                *((R*)alloc) = ret;
                return alloc;
            }
            catch (Exception e)
            {
                _lastException = ExceptionDispatchInfo.Capture(e);
            }

            return 0;
        });
    }


    /// <inheritdoc/>
    void IDisposable.Dispose()
    {
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
    internal static void Serialize(BincodeSerializer serializer, EguiCallbackFn<A, R> obj)
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
    internal static EguiCallbackFn<A, R> Deserialize(BincodeDeserializer deserializer)
    {
        deserializer.increase_container_depth();
        EguiCallbackFn<A, R> obj = default;
        obj.func = (delegate* unmanaged[Cdecl]<void*, void*, nint>)deserializer.deserialize_u64();
        obj.callbackHandle = (void*)deserializer.deserialize_u64();
        deserializer.decrease_container_depth();
        return obj;
    }
}
