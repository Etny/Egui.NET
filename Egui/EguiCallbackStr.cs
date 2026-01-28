using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Egui;

/// <summary>
/// A callback that may be passed to unmanaged code.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe partial struct EguiCallbackStr : IDisposable
{

    /// <summary>
    ///  The function to call.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, nint> func;
    /// <summary>
    ///  Data to pass as the second function argument.
    /// </summary>
    public void* data;

    /// <summary>
    /// The last exception that occurred.
    /// </summary>
    [ThreadStatic]
    private static ExceptionDispatchInfo? _lastException;

    /// <summary>
    /// Creates a new object for invoking the given callback.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    public EguiCallbackStr(Func<double, string> callback)
    {
        func = &InvokeCallbackSTR;
        data = (void*)(nint)GCHandle.Alloc(callback);
    }


    /// <inheritdoc/>
    void IDisposable.Dispose()
    {
        GCHandle.FromIntPtr((nint)data).Free();

        if (_lastException is not null)
        {
            var last = _lastException;
            _lastException = null;
            last.Throw();
        }
    }

    /// <summary>
    /// Invokes a C# callback.
    /// </summary>
    /// <param name="callback">A GC handle to the callback that should be invoked.</param>
    /// <param name="data">The data to provide to the callback.</param>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint InvokeCallbackSTR(void* argument, void* data)
    {
        try
        {
            var action = (Func<double, string>)GCHandle.FromIntPtr((nint)data).Target!;
            var str = action(*((double*)argument));
            return Marshal.StringToCoTaskMemUTF8(str);
        }
        catch (Exception e)
        {
            _lastException = ExceptionDispatchInfo.Capture(e);
        }

        return 0;
    }

    /// <summary>
    /// Serializes an instance of this value.
    /// </summary>
    /// <param name="serializer">The serializer to use.</param>
    internal static void Serialize(BincodeSerializer serializer, EguiCallbackStr obj)
    {
        serializer.increase_container_depth();
        serializer.serialize_u64((ulong)obj.func);
        serializer.serialize_u64((ulong)obj.data);
        serializer.decrease_container_depth();
    }

    /// <summary>
    /// Deserializes an instance of this value.
    /// </summary>
    /// <param name="deserializer">The deserializer to use.</param>
    /// <returns>The object that was deserialized.</returns>
    internal static EguiCallbackStr Deserialize(BincodeDeserializer deserializer)
    {
        deserializer.increase_container_depth();
        EguiCallbackStr obj = default;
        obj.func = (delegate* unmanaged[Cdecl]<void*, void*, nint>)deserializer.deserialize_u64();
        obj.data = (void*)deserializer.deserialize_u64();
        deserializer.decrease_container_depth();
        return obj;
    }
}
